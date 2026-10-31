import { useQuery } from "@tanstack/react-query"
import { MapPin, MessageSquare, Package } from "lucide-react"
import { useParams } from "react-router-dom"
import { PageHeader } from "@/components/page-header"
import { PickupScheduleList } from "@/components/pickup-schedule"
import { ProductCard, productGrid } from "@/components/product-card"
import { Stars } from "@/components/rating"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { api } from "@/lib/api"
import { formatDate } from "@/lib/format"
import type { Farm, Paged, Product, Review } from "@/lib/types"

export function FarmPage() {
  const { id } = useParams()
  const farm = useQuery({ queryKey: ["farm", id], queryFn: () => api.get<Farm>(`/api/farms/${id}`) })
  const products = useQuery({
    queryKey: ["products", "farm", id],
    queryFn: () => api.get<Paged<Product>>("/api/products", { farmId: id, pageSize: 100 }),
  })
  const reviews = useQuery({
    queryKey: ["reviews", id],
    queryFn: () => api.get<Paged<Review>>(`/api/farms/${id}/reviews`, { pageSize: 20 }),
  })

  if (farm.isPending) return <LoadingList rows={3} />
  if (farm.isError) return <ErrorState error={farm.error} onRetry={() => farm.refetch()} />

  const f = farm.data
  const ratings = reviews.data?.items ?? []
  const average = ratings.length ? ratings.reduce((sum, r) => sum + r.rating, 0) / ratings.length : 0

  return (
    <>
      <PageHeader
        title={f.name}
        back
        description={
          <span className="flex items-center gap-1">
            <MapPin className="size-4" aria-hidden /> {f.location}
          </span>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="min-w-0">
          {f.description && <p className="mb-6 leading-6">{f.description}</p>}
          <h2 className="mb-4 text-xl font-semibold">Produtos</h2>
          {products.isPending ? (
            <LoadingList rows={4} className={productGrid} />
          ) : products.isError ? (
            <ErrorState error={products.error} onRetry={() => products.refetch()} />
          ) : products.data.items.length === 0 ? (
            <EmptyState
              icon={Package}
              title="Sem produtos à venda"
              description="Esta quinta ainda não tem produtos no catálogo."
            />
          ) : (
            <div className="grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-3">
              {products.data.items.map((product) => (
                <ProductCard key={product.id} product={product} />
              ))}
            </div>
          )}
        </div>

        <aside className="grid h-fit gap-6">
          <div className="rounded-xl border p-4">
            <h2 className="mb-3 text-lg font-semibold">Horário de levantamento</h2>
            <PickupScheduleList schedules={f.pickupSchedules} />
          </div>
          <div className="rounded-xl border p-4">
            <h2 className="mb-1 text-lg font-semibold">Avaliações</h2>
            {ratings.length > 0 ? (
              <>
                <p className="mb-4 flex items-center gap-2">
                  <Stars value={average} />
                  <span className="font-heading font-semibold">{average.toFixed(1).replace(".", ",")}</span>
                  <span className="text-sm text-muted-foreground">({reviews.data!.totalCount})</span>
                </p>
                <ul className="grid gap-4">
                  {ratings.slice(0, 5).map((review) => (
                    <li key={review.id} className="border-t pt-3">
                      <div className="flex items-center justify-between">
                        <Stars value={review.rating} />
                        <span className="text-xs text-muted-foreground">{formatDate(review.createdAt)}</span>
                      </div>
                      {review.comment && <p className="mt-1 text-sm">{review.comment}</p>}
                    </li>
                  ))}
                </ul>
              </>
            ) : (
              <p className="flex items-center gap-2 text-sm text-muted-foreground">
                <MessageSquare className="size-4" aria-hidden /> Ainda sem avaliações.
              </p>
            )}
          </div>
        </aside>
      </div>
    </>
  )
}
