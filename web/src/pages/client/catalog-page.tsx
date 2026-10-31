import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { Check, Search, SearchX } from "lucide-react"
import { useEffect, useState } from "react"
import { useSearchParams } from "react-router-dom"
import { PageHeader } from "@/components/page-header"
import { ProductCard, productGrid } from "@/components/product-card"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { api } from "@/lib/api"
import { useFarms } from "@/lib/queries"
import type { Paged, Product, ProductType } from "@/lib/types"
import { cn } from "@/lib/utils"

const PAGE_SIZE = 24

const typeChips: { value: ProductType | ""; label: string }[] = [
  { value: "", label: "Todos" },
  { value: "Product", label: "Produtos" },
  { value: "PreDefinedBasket", label: "Cabazes" },
  { value: "SurpriseBasket", label: "Cabazes surpresa" },
]

// RF04: the catalog of every farm, with search by name and filters by kind, farm and availability.
export function CatalogPage() {
  const [params, setParams] = useSearchParams()
  const type = (params.get("tipo") ?? "") as ProductType | ""
  const farmId = params.get("quinta") ?? ""
  const onlyInStock = params.get("disponivel") !== "0"
  const page = Number(params.get("pagina") ?? 1)
  const [search, setSearch] = useState(params.get("q") ?? "")
  const farms = useFarms()

  function update(changes: Record<string, string | null>) {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value) next.set(key, value)
      else next.delete(key)
    }
    if (!("pagina" in changes)) next.delete("pagina")
    setParams(next, { replace: true })
  }

  // Search as the user types, a moment after the last key.
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) update({ q: search.trim() || null })
    }, 300)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  const products = useQuery({
    queryKey: ["products", "catalog", { type, farmId, onlyInStock, page, q: params.get("q") }],
    queryFn: () =>
      api.get<Paged<Product>>("/api/products", {
        type: type || undefined,
        farmId: farmId || undefined,
        onlyInStock,
        search: params.get("q") || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    placeholderData: keepPreviousData,
  })

  const pages = products.data ? Math.max(1, Math.ceil(products.data.totalCount / PAGE_SIZE)) : 1

  return (
    <>
      <PageHeader title="Explorar" description="Produtos e cabazes de todas as quintas." />

      <div className="grid gap-4">
        <div className="relative">
          <Search
            className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            placeholder="Procurar produtos…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="rounded-full bg-muted pl-12"
            aria-label="Procurar produtos"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Tipo de produto">
          {typeChips.map((chip) => {
            const selected = type === chip.value
            return (
              <button
                key={chip.label}
                type="button"
                aria-pressed={selected}
                onClick={() => update({ tipo: chip.value || null })}
                className={cn(
                  "inline-flex h-10 items-center gap-1.5 rounded-lg border px-4 font-heading text-sm font-medium transition-colors",
                  selected ? "border-secondary bg-secondary text-brand-900" : "bg-background hover:bg-muted",
                )}
              >
                {selected && <Check className="size-4" aria-hidden />}
                {chip.label}
              </button>
            )
          })}
        </div>

        <div className="flex flex-wrap items-center gap-4">
          <Select value={farmId || "all"} onValueChange={(value) => update({ quinta: value === "all" ? null : value })}>
            <SelectTrigger className="w-full sm:w-64" aria-label="Quinta">
              <SelectValue placeholder="Todas as quintas" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Todas as quintas</SelectItem>
              {farms.data?.items.map((farm) => (
                <SelectItem key={farm.id} value={farm.id}>
                  {farm.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <div className="flex items-center gap-2">
            <Switch
              id="in-stock"
              checked={onlyInStock}
              onCheckedChange={(checked) => update({ disponivel: checked ? null : "0" })}
            />
            <Label htmlFor="in-stock">Só disponíveis</Label>
          </div>
        </div>
      </div>

      <div className="mt-6">
        {products.isPending ? (
          <LoadingList rows={8} className={productGrid} />
        ) : products.isError ? (
          <ErrorState error={products.error} onRetry={() => products.refetch()} />
        ) : products.data.items.length === 0 ? (
          <EmptyState
            icon={SearchX}
            title="Sem resultados"
            description="Experimente outro nome ou retire alguns filtros."
            action={
              <Button
                variant="outline"
                onClick={() => {
                  setSearch("")
                  setParams({}, { replace: true })
                }}
              >
                Limpar filtros
              </Button>
            }
          />
        ) : (
          <>
            <p className="mb-3 text-sm text-muted-foreground" aria-live="polite">
              {products.data.totalCount} {products.data.totalCount === 1 ? "resultado" : "resultados"}
            </p>
            <div className={cn(productGrid, products.isFetching && "opacity-70")}>
              {products.data.items.map((product) => (
                <ProductCard key={product.id} product={product} />
              ))}
            </div>
            {pages > 1 && (
              <div className="mt-6 flex items-center justify-center gap-3">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => update({ pagina: String(page - 1) })}
                >
                  Anterior
                </Button>
                <span className="text-sm text-muted-foreground">
                  Página {page} de {pages}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= pages}
                  onClick={() => update({ pagina: String(page + 1) })}
                >
                  Seguinte
                </Button>
              </div>
            )}
          </>
        )}
      </div>
    </>
  )
}
