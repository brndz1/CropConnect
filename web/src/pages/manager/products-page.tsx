import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { EyeOff, Minus, Package, Pencil, Plus, Trash2 } from "lucide-react"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { PageHeader } from "@/components/page-header"
import { ProductVisual } from "@/components/product-visual"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { api, errorMessage } from "@/lib/api"
import { formatDate, formatPrice, productTypeLabel } from "@/lib/format"
import type { Paged, Product } from "@/lib/types"
import { cn } from "@/lib/utils"
import { FarmGate, FarmPicker, useSelectedFarm } from "./farm-picker"
import { ProductFormDialog } from "./product-form"

function StockControl({ product }: { product: Product }) {
  const queryClient = useQueryClient()
  // RF15: adjust the stock straight from the list.
  const adjust = useMutation({
    mutationFn: (delta: number) => api.post<Product>(`/api/products/${product.id}/stock-adjustments`, { delta }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["products"] }),
    onError: (e) => toast.error(errorMessage(e)),
  })
  return (
    <div
      className="inline-flex items-center gap-1 rounded-full border"
      role="group"
      aria-label={`Stock de ${product.name}`}
    >
      <Button
        variant="ghost"
        size="icon"
        disabled={product.stockQuantity === 0 || adjust.isPending}
        onClick={() => adjust.mutate(-1)}
        aria-label="Retirar uma unidade"
      >
        <Minus />
      </Button>
      <span
        className={cn(
          "min-w-10 text-center font-heading font-semibold tabular-nums",
          product.stockQuantity === 0 && "text-destructive",
          product.stockQuantity > 0 && product.stockQuantity <= 5 && "text-terracotta",
        )}
        aria-live="polite"
      >
        {product.stockQuantity}
      </span>
      <Button
        variant="ghost"
        size="icon"
        disabled={adjust.isPending}
        onClick={() => adjust.mutate(1)}
        aria-label="Juntar uma unidade"
      >
        <Plus />
      </Button>
    </div>
  )
}

export function ManagerProductsPage() {
  const { farms, items, selected, select } = useSelectedFarm()
  const queryClient = useQueryClient()
  const products = useQuery({
    queryKey: ["products", "manager", selected?.id],
    queryFn: () =>
      api.get<Paged<Product>>("/api/products", {
        farmId: selected!.id,
        includeUnavailable: true,
        onlyInStock: false,
        pageSize: 100,
      }),
    enabled: !!selected,
  })
  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/api/products/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["products"] })
      toast.success("Produto removido.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  return (
    <>
      <PageHeader
        title="Produtos"
        description={selected?.name}
        actions={
          selected && (
            <>
              <ProductFormDialog
                farmId={selected.id}
                defaultType="SurpriseBasket"
                trigger={<Button variant="outline">Criar cabaz</Button>}
              />
              <ProductFormDialog
                farmId={selected.id}
                trigger={
                  <Button>
                    <Plus /> Novo produto
                  </Button>
                }
              />
            </>
          )
        }
      />
      <FarmGate farms={farms}>
        <div className="mb-4">
          <FarmPicker items={items} selected={selected} onSelect={select} />
        </div>
        {products.isPending ? (
          <LoadingList />
        ) : products.isError ? (
          <ErrorState error={products.error} onRetry={() => products.refetch()} />
        ) : products.data.items.length === 0 ? (
          <EmptyState
            icon={Package}
            title="Ainda sem produtos"
            description="Junte o primeiro produto ou um cabaz para escoar excedentes."
          />
        ) : (
          <ul className="grid gap-3">
            {products.data.items.map((product) => (
              <li key={product.id} className="flex flex-wrap items-center gap-4 rounded-xl border p-4">
                <ProductVisual type={product.type} className="size-14 shrink-0" />
                <div className="min-w-0 flex-1">
                  <p className="flex flex-wrap items-center gap-2 font-heading font-semibold text-brand-900">
                    {product.name}
                    {product.type !== "Product" && (
                      <span className="rounded-full bg-terracotta px-2.5 py-0.5 text-xs text-white">
                        {productTypeLabel[product.type]}
                      </span>
                    )}
                    {!product.isAvailable && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-muted px-2.5 py-0.5 text-xs text-muted-foreground">
                        <EyeOff className="size-3" aria-hidden /> Oculto
                      </span>
                    )}
                  </p>
                  <p className="text-sm text-muted-foreground">
                    {formatPrice(product.price)} / {product.unit}
                    {product.availableUntil && ` · até ${formatDate(product.availableUntil)}`}
                  </p>
                </div>
                <StockControl product={product} />
                <div className="flex gap-1">
                  <ProductFormDialog
                    farmId={product.farmId}
                    product={product}
                    trigger={
                      <Button variant="ghost" size="icon" aria-label={`Editar ${product.name}`}>
                        <Pencil />
                      </Button>
                    }
                  />
                  <ConfirmDialog
                    trigger={
                      <Button variant="ghost" size="icon" aria-label={`Remover ${product.name}`}>
                        <Trash2 className="text-destructive" />
                      </Button>
                    }
                    title={`Remover ${product.name}?`}
                    description="Sai do catálogo. As encomendas já feitas não são afetadas."
                    confirmLabel="Remover"
                    destructive
                    onConfirm={() => remove.mutate(product.id)}
                  />
                </div>
              </li>
            ))}
          </ul>
        )}
      </FarmGate>
    </>
  )
}
