import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { Search, SearchX, Tractor } from "lucide-react"
import { useEffect, useState } from "react"
import { useSearchParams } from "react-router-dom"
import { FarmCard, farmGrid } from "@/components/farm-card"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { api } from "@/lib/api"
import type { Farm, Paged } from "@/lib/types"
import { cn } from "@/lib/utils"

const PAGE_SIZE = 24

// RF09: every active farm, to open its page with the products, pickup hours and reviews.
export function FarmsPage() {
  const [params, setParams] = useSearchParams()
  const q = params.get("q") ?? ""
  const page = Number(params.get("pagina") ?? 1)
  const [search, setSearch] = useState(q)

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
      if (q !== search.trim()) update({ q: search.trim() || null })
    }, 300)
    return () => window.clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  const farms = useQuery({
    queryKey: ["farms", "list", { q, page }],
    queryFn: () => api.get<Paged<Farm>>("/api/farms", { search: q || undefined, page, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  })

  const pages = farms.data ? Math.max(1, Math.ceil(farms.data.totalCount / PAGE_SIZE)) : 1

  return (
    <>
      <PageHeader title="Quintas" description="Os produtores da tua região." />
      <div className="relative mb-6">
        <Search
          className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <Input
          type="search"
          placeholder="Procurar por nome ou localidade…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="rounded-full bg-muted pl-12"
          aria-label="Procurar quintas"
        />
      </div>
      {farms.isPending ? (
        <LoadingList rows={6} className={farmGrid} />
      ) : farms.isError ? (
        <ErrorState error={farms.error} onRetry={() => farms.refetch()} />
      ) : farms.data.items.length === 0 ? (
        q ? (
          <EmptyState icon={SearchX} title="Sem resultados" description="Experimente outro nome ou localidade." />
        ) : (
          <EmptyState icon={Tractor} title="Ainda não há quintas" description="Volte em breve." />
        )
      ) : (
        <>
          <div className={cn(farmGrid, farms.isFetching && "opacity-70")}>
            {farms.data.items.map((farm) => (
              <FarmCard key={farm.id} farm={farm} />
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
    </>
  )
}
