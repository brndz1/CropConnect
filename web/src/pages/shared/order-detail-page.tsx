import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Check, Loader2, MapPin, Star } from "lucide-react"
import { useState } from "react"
import { Link, useParams } from "react-router-dom"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { PageHeader } from "@/components/page-header"
import { PickupScheduleList } from "@/components/pickup-schedule"
import { StarInput, Stars } from "@/components/rating"
import { StatusBadge } from "@/components/status-badge"
import { ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { api, ApiError, errorMessage } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { formatDateTime, formatPrice, orderStatusLabel, shortId } from "@/lib/format"
import type { Farm, Order, OrderStatus, Review } from "@/lib/types"
import { cn } from "@/lib/utils"

const steps: OrderStatus[] = ["Pending", "Confirmed", "ReadyForPickup", "PickedUp"]

// What the farm manager does next from each state (task model G1: confirm → ready → picked up).
const nextStep: Partial<Record<OrderStatus, { status: OrderStatus; label: string; description: string }>> = {
  Pending: {
    status: "Confirmed",
    label: "Confirmar encomenda",
    description: "O cliente é avisado de que a encomenda foi aceite.",
  },
  Confirmed: {
    status: "ReadyForPickup",
    label: "Marcar pronta a levantar",
    description: "O cliente é avisado de que pode vir levantar.",
  },
  ReadyForPickup: {
    status: "PickedUp",
    label: "Registar levantamento",
    description: "Confirma que o cliente levantou e pagou a encomenda.",
  },
}

function Progress({ status }: { status: OrderStatus }) {
  if (status === "Cancelled") return null
  const current = steps.indexOf(status)
  return (
    <ol className="flex items-start" aria-label="Progresso da encomenda">
      {steps.map((step, index) => {
        const done = index <= current
        return (
          <li
            key={step}
            className="flex flex-1 flex-col items-center gap-2 text-center"
            aria-current={index === current ? "step" : undefined}
          >
            <div className="flex w-full items-center">
              <span className={cn("h-0.5 flex-1", index === 0 ? "invisible" : done ? "bg-primary" : "bg-border")} />
              <span
                className={cn(
                  "flex size-8 shrink-0 items-center justify-center rounded-full border-2",
                  done
                    ? "border-primary bg-primary text-primary-foreground"
                    : "border-border bg-background text-muted-foreground",
                )}
              >
                {done ? <Check className="size-4" aria-hidden /> : <span className="text-xs">{index + 1}</span>}
              </span>
              <span
                className={cn(
                  "h-0.5 flex-1",
                  index === steps.length - 1 ? "invisible" : index < current ? "bg-primary" : "bg-border",
                )}
              />
            </div>
            <span className={cn("px-1 text-xs", done ? "font-semibold text-brand-900" : "text-muted-foreground")}>
              {orderStatusLabel[step]}
            </span>
          </li>
        )
      })}
    </ol>
  )
}

function ReviewSection({ order }: { order: Order }) {
  const queryClient = useQueryClient()
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState("")
  const review = useQuery({
    queryKey: ["review", order.id],
    queryFn: async () => {
      try {
        return await api.get<Review>(`/api/orders/${order.id}/review`)
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return null
        throw e
      }
    },
  })
  const create = useMutation({
    mutationFn: () => api.post<Review>(`/api/orders/${order.id}/review`, { rating, comment: comment.trim() || null }),
    onSuccess: (created) => {
      queryClient.setQueryData(["review", order.id], created)
      queryClient.invalidateQueries({ queryKey: ["reviews", order.farmId] })
      toast.success("Obrigado pela avaliação!")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  if (review.isPending) return null
  if (review.data) {
    return (
      <div className="rounded-xl border p-4">
        <h2 className="mb-2 text-lg font-semibold">A sua avaliação</h2>
        <Stars value={review.data.rating} />
        {review.data.comment && <p className="mt-2 text-sm">{review.data.comment}</p>}
      </div>
    )
  }
  return (
    <div className="rounded-xl border p-4">
      <h2 className="flex items-center gap-2 text-lg font-semibold">
        <Star className="size-5 text-terracotta" aria-hidden /> Avaliar a encomenda
      </h2>
      <p className="mb-3 text-sm text-muted-foreground">Como correu? A avaliação ajuda outros clientes.</p>
      <StarInput value={rating} onChange={setRating} />
      <Label htmlFor="comment" className="mt-3 mb-2 block">
        Comentário (opcional)
      </Label>
      <Textarea id="comment" rows={3} maxLength={1000} value={comment} onChange={(e) => setComment(e.target.value)} />
      <Button className="mt-3" disabled={rating === 0 || create.isPending} onClick={() => create.mutate()}>
        {create.isPending && <Loader2 className="animate-spin" />}
        Enviar avaliação
      </Button>
    </div>
  )
}

export function OrderDetailPage() {
  const { id } = useParams()
  const { session } = useAuth()
  const queryClient = useQueryClient()
  const [reason, setReason] = useState("")
  const order = useQuery({ queryKey: ["order", id], queryFn: () => api.get<Order>(`/api/orders/${id}`) })
  const farm = useQuery({
    queryKey: ["farm", order.data?.farmId],
    queryFn: () => api.get<Farm>(`/api/farms/${order.data!.farmId}`),
    enabled: !!order.data,
  })

  const onChanged = (updated: Order) => {
    queryClient.setQueryData(["order", id], updated)
    queryClient.invalidateQueries({ queryKey: ["orders"] })
  }
  const advance = useMutation({
    mutationFn: (status: OrderStatus) =>
      api.put<Order>(`/api/orders/${id}/status`, { status, expectedVersion: order.data?.version }),
    onSuccess: (updated) => {
      onChanged(updated)
      toast.success(`Encomenda ${orderStatusLabel[updated.status].toLowerCase()}.`)
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const cancel = useMutation({
    mutationFn: () =>
      api.post<Order>(`/api/orders/${id}/cancel`, {
        reason: reason.trim() || null,
        expectedVersion: order.data?.version,
      }),
    onSuccess: (updated) => {
      onChanged(updated)
      queryClient.invalidateQueries({ queryKey: ["products"] })
      toast.success("Encomenda cancelada.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  if (order.isPending) return <LoadingList rows={3} />
  if (order.isError) return <ErrorState error={order.error} onRetry={() => order.refetch()} />

  const o = order.data
  const isClient = session?.role === "Client"
  const isManager = session?.role === "Manager" || session?.role === "Admin"
  // RF09: the client cancels until the order is ready for pickup; the manager until it is picked up.
  const canCancel = isClient
    ? o.status === "Pending" || o.status === "Confirmed"
    : isManager && o.status !== "PickedUp" && o.status !== "Cancelled"
  const step = isManager ? nextStep[o.status] : undefined
  const busy = advance.isPending || cancel.isPending

  return (
    <>
      <PageHeader
        title={`Encomenda ${shortId(o.id)}`}
        back
        description={`Feita em ${formatDateTime(o.createdAt)}`}
        actions={<StatusBadge status={o.status} className="px-4 py-1.5 text-sm" />}
      />

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="grid h-fit gap-6">
          <div className="rounded-xl border p-4 sm:p-6">
            <Progress status={o.status} />
            {o.status === "Cancelled" && (
              <p className="text-sm">
                Esta encomenda foi cancelada{o.cancellationReason ? `: ${o.cancellationReason}` : "."}
              </p>
            )}
          </div>

          <div className="rounded-xl border">
            <h2 className="border-b px-4 py-3 text-lg font-semibold">Artigos</h2>
            <ul className="divide-y">
              {o.items.map((item) => (
                <li key={item.productId} className="flex items-center justify-between gap-4 px-4 py-3">
                  <span>
                    <span className="font-medium">{item.productName}</span>
                    <span className="block text-sm text-muted-foreground">
                      {item.quantity} × {formatPrice(item.unitPrice)}
                    </span>
                  </span>
                  <span className="font-heading font-semibold tabular-nums">{formatPrice(item.totalPrice)}</span>
                </li>
              ))}
            </ul>
            <div className="flex justify-between border-t px-4 py-3 font-heading text-lg font-semibold text-brand-900">
              <span>Total</span>
              <span className="tabular-nums">{formatPrice(o.totalAmount)}</span>
            </div>
            {o.notes && (
              <p className="border-t px-4 py-3 text-sm">
                <span className="font-semibold">Nota: </span>
                {o.notes}
              </p>
            )}
          </div>

          {isClient && o.status === "PickedUp" && <ReviewSection order={o} />}
        </div>

        <aside className="grid h-fit gap-4">
          {(step || canCancel) && (
            <div className="grid gap-3 rounded-xl bg-muted p-4">
              {step && (
                <ConfirmDialog
                  trigger={
                    <Button disabled={busy} className="w-full">
                      {advance.isPending && <Loader2 className="animate-spin" />}
                      {step.label}
                    </Button>
                  }
                  title={`${step.label}?`}
                  description={step.description}
                  confirmLabel={step.label}
                  onConfirm={() => advance.mutate(step.status)}
                />
              )}
              {canCancel && (
                <ConfirmDialog
                  trigger={
                    <Button
                      variant="outline"
                      disabled={busy}
                      className="w-full border-destructive text-destructive hover:bg-destructive/10"
                    >
                      Cancelar encomenda
                    </Button>
                  }
                  title="Cancelar a encomenda?"
                  description="O stock volta a ficar disponível e a outra parte é avisada. Não é possível reverter."
                  confirmLabel="Cancelar encomenda"
                  destructive
                  onConfirm={() => cancel.mutate()}
                >
                  <div className="grid gap-2">
                    <Label htmlFor="reason">Motivo (opcional)</Label>
                    <Textarea
                      id="reason"
                      rows={2}
                      maxLength={500}
                      value={reason}
                      onChange={(e) => setReason(e.target.value)}
                    />
                  </div>
                </ConfirmDialog>
              )}
            </div>
          )}

          <div className="rounded-xl border p-4">
            <h2 className="mb-1 text-lg font-semibold">Levantamento</h2>
            {farm.data ? (
              <>
                <Link to={`/quintas/${farm.data.id}`} className="font-heading font-semibold text-primary">
                  {farm.data.name}
                </Link>
                <p className="mb-3 flex items-center gap-1 text-sm text-muted-foreground">
                  <MapPin className="size-4" aria-hidden /> {farm.data.location}
                </p>
                <PickupScheduleList schedules={farm.data.pickupSchedules} />
              </>
            ) : (
              <LoadingList rows={2} />
            )}
            <p className="mt-3 text-sm text-muted-foreground">Pagamento no levantamento, diretamente ao produtor.</p>
          </div>
        </aside>
      </div>
    </>
  )
}
