import { useQuery } from "@tanstack/react-query"
import { MessageSquare } from "lucide-react"
import { Link } from "react-router-dom"
import { PageHeader } from "@/components/page-header"
import { Stars } from "@/components/rating"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { api } from "@/lib/api"
import { formatDate, shortId } from "@/lib/format"
import type { Paged, Review } from "@/lib/types"
import { FarmGate, FarmPicker, useSelectedFarm } from "./farm-picker"

// The API's largest page; the summary covers the latest ones when a farm has more.
const PAGE_SIZE = 100

// RF15: what customers said about the farm after picking up their orders.
export function ManagerReviewsPage() {
  const { farms, items, selected, select } = useSelectedFarm()
  const reviews = useQuery({
    queryKey: ["reviews", selected?.id],
    queryFn: () => api.get<Paged<Review>>(`/api/farms/${selected!.id}/reviews`, { pageSize: PAGE_SIZE }),
    enabled: !!selected,
  })

  const list = reviews.data?.items ?? []
  const average = list.length ? list.reduce((sum, r) => sum + r.rating, 0) / list.length : 0
  const total = reviews.data?.totalCount ?? 0

  return (
    <>
      <PageHeader title="Avaliações" description={selected?.name} />
      <FarmGate farms={farms}>
        <div className="mb-4 flex flex-wrap items-center gap-4">
          <FarmPicker items={items} selected={selected} onSelect={select} />
        </div>
        {reviews.isPending ? (
          <LoadingList />
        ) : reviews.isError ? (
          <ErrorState error={reviews.error} onRetry={() => reviews.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState
            icon={MessageSquare}
            title="Ainda sem avaliações"
            description="Os clientes podem avaliar cada encomenda depois de a levantarem."
          />
        ) : (
          <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
            <section className="h-fit rounded-xl border p-4" aria-label="Resumo">
              <p className="font-heading text-[32px] leading-10 font-bold text-brand-900 tabular-nums">
                {average.toFixed(1).replace(".", ",")}
              </p>
              <Stars value={average} className="mt-1" />
              <p className="mt-1 text-sm text-muted-foreground">
                {total} {total === 1 ? "avaliação" : "avaliações"}
                {total > list.length && ` (média das últimas ${list.length})`}
              </p>
              <ul className="mt-4 grid gap-1.5">
                {[5, 4, 3, 2, 1].map((stars) => {
                  const count = list.filter((r) => r.rating === stars).length
                  return (
                    <li key={stars} className="flex items-center gap-2 text-sm">
                      <span className="w-3 text-right tabular-nums">{stars}</span>
                      <span className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
                        <span
                          className="block h-full rounded-full bg-warning"
                          style={{ width: `${(count / list.length) * 100}%` }}
                        />
                      </span>
                      <span className="w-6 text-right text-muted-foreground tabular-nums">{count}</span>
                    </li>
                  )
                })}
              </ul>
            </section>

            <ul className="grid h-fit gap-3">
              {list.map((review) => (
                <li key={review.id} className="rounded-xl border p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <Stars value={review.rating} />
                    <span className="text-xs text-muted-foreground">{formatDate(review.createdAt)}</span>
                  </div>
                  {review.comment ? (
                    <p className="mt-2 text-sm">{review.comment}</p>
                  ) : (
                    <p className="mt-2 text-sm text-muted-foreground">Sem comentário.</p>
                  )}
                  <Link
                    to={`/gestor/encomendas/${review.orderId}`}
                    className="mt-2 inline-block text-sm font-medium text-primary hover:underline"
                  >
                    Encomenda {shortId(review.orderId)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}
      </FarmGate>
    </>
  )
}
