import { useQuery } from "@tanstack/react-query"
import { CalendarClock, Gift, MapPin, ShoppingCart, Tractor } from "lucide-react"
import { useState } from "react"
import { Link, useNavigate, useParams } from "react-router-dom"
import { toast } from "sonner"
import { PageHeader } from "@/components/page-header"
import { PickupScheduleList } from "@/components/pickup-schedule"
import { StockNote } from "@/components/product-card"
import { ProductVisual } from "@/components/product-visual"
import { QuantityStepper } from "@/components/quantity-stepper"
import { ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { api } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { useCart } from "@/lib/cart"
import { formatDate, formatPrice, productTypeLabel } from "@/lib/format"
import type { Farm, Product } from "@/lib/types"

export function ProductPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { session } = useAuth()
  const { add } = useCart()
  const [quantity, setQuantity] = useState(1)

  const product = useQuery({ queryKey: ["product", id], queryFn: () => api.get<Product>(`/api/products/${id}`) })
  const farm = useQuery({
    queryKey: ["farm", product.data?.farmId],
    queryFn: () => api.get<Farm>(`/api/farms/${product.data!.farmId}`),
    enabled: !!product.data,
  })

  if (product.isPending) return <LoadingList rows={3} />
  if (product.isError) return <ErrorState error={product.error} onRetry={() => product.refetch()} />

  const p = product.data
  const soldOut = p.stockQuantity === 0
  const canBuy = !session || session.role === "Client"

  return (
    <>
      <PageHeader title={p.name} back description={productTypeLabel[p.type]} />
      <div className="grid gap-8 md:grid-cols-2">
        <ProductVisual type={p.type} className="rounded-2xl" />
        <div className="flex flex-col gap-5">
          <div>
            <p className="font-heading text-[28px] font-semibold text-brand-900">
              {formatPrice(p.price)}
              <span className="text-base font-normal text-muted-foreground"> / {p.unit}</span>
            </p>
            <StockNote product={p} />
          </div>

          {p.description && <p className="leading-6">{p.description}</p>}

          {p.type === "SurpriseBasket" ? (
            <div className="flex gap-3 rounded-xl bg-terracotta/10 p-4">
              <Gift className="size-5 shrink-0 text-terracotta" aria-hidden />
              <p className="text-sm">
                O conteúdo deste cabaz é escolhido pelo produtor e só é revelado no levantamento.
              </p>
            </div>
          ) : (
            p.contents && (
              <div className="rounded-xl bg-muted p-4">
                <p className="font-heading text-sm font-semibold text-brand-900">O cabaz inclui</p>
                <p className="mt-1">{p.contents}</p>
              </div>
            )
          )}

          {p.availableUntil && (
            <p className="flex items-center gap-2 text-sm">
              <CalendarClock className="size-4 text-primary" aria-hidden />
              Levantar até <strong>{formatDate(p.availableUntil)}</strong>
            </p>
          )}

          {canBuy && (
            <div className="flex flex-wrap items-center gap-3">
              <QuantityStepper
                value={quantity}
                max={Math.max(1, p.stockQuantity)}
                onChange={setQuantity}
                label="Quantidade"
              />
              <Button
                className="flex-1"
                disabled={soldOut}
                onClick={() => {
                  add(p, quantity)
                  toast.success(`${p.name} adicionado ao carrinho`, {
                    action: { label: "Ver carrinho", onClick: () => navigate("/carrinho") },
                  })
                }}
              >
                <ShoppingCart /> {soldOut ? "Esgotado" : "Adicionar ao carrinho"}
              </Button>
            </div>
          )}

          <div className="rounded-xl border p-4">
            <Link to={`/quintas/${p.farmId}`} className="flex items-center gap-3">
              <span className="flex size-10 items-center justify-center rounded-full bg-secondary text-primary">
                <Tractor className="size-5" aria-hidden />
              </span>
              <span>
                <span className="block font-heading font-semibold text-brand-900">{p.farmName}</span>
                {farm.data && (
                  <span className="flex items-center gap-1 text-sm text-muted-foreground">
                    <MapPin className="size-4" aria-hidden /> {farm.data.location}
                  </span>
                )}
              </span>
            </Link>
            <p className="mt-4 mb-2 font-heading text-sm font-semibold text-brand-900">Horário de levantamento</p>
            {farm.data ? <PickupScheduleList schedules={farm.data.pickupSchedules} /> : <LoadingList rows={2} />}
          </div>
        </div>
      </div>
    </>
  )
}
