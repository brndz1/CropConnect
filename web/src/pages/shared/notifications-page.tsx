import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Bell, CheckCheck, ClipboardList, ThermometerSun, XCircle } from "lucide-react"
import { useNavigate } from "react-router-dom"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { api } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { formatDateTime } from "@/lib/format"
import type { AppNotification, NotificationPage, NotificationType } from "@/lib/types"
import { cn } from "@/lib/utils"

const icons: Record<NotificationType, typeof Bell> = {
  OrderCreated: ClipboardList,
  OrderStatusChanged: ClipboardList,
  OrderCancelled: XCircle,
  SensorAlert: ThermometerSun,
}

// RF10, RF18, RF20: the notification centre, newest first.
export function NotificationsPage() {
  const { session } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const notifications = useQuery({
    queryKey: ["notifications", "list"],
    queryFn: () => api.get<NotificationPage>("/api/notifications", { pageSize: 50 }),
  })
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["notifications"] })
  const markRead = useMutation({
    mutationFn: (id: string) => api.post(`/api/notifications/${id}/read`),
    onSuccess: refresh,
  })
  const markAll = useMutation({ mutationFn: () => api.post("/api/notifications/read-all"), onSuccess: refresh })

  function open(notification: AppNotification) {
    if (!notification.readAt) markRead.mutate(notification.id)
    const manager = session?.role !== "Client"
    if (notification.type === "SensorAlert") navigate(`/gestor/sensores?quinta=${notification.farmId}`)
    else
      navigate(
        manager ? `/gestor/encomendas/${notification.relatedEntityId}` : `/encomendas/${notification.relatedEntityId}`,
      )
  }

  return (
    <>
      <PageHeader
        title="Notificações"
        actions={
          (notifications.data?.unreadCount ?? 0) > 0 && (
            <Button variant="outline" size="sm" onClick={() => markAll.mutate()} disabled={markAll.isPending}>
              <CheckCheck /> Marcar todas como lidas
            </Button>
          )
        }
      />
      {notifications.isPending ? (
        <LoadingList />
      ) : notifications.isError ? (
        <ErrorState error={notifications.error} onRetry={() => notifications.refetch()} />
      ) : notifications.data.items.length === 0 ? (
        <EmptyState
          icon={Bell}
          title="Sem notificações"
          description="Os avisos sobre encomendas e alertas aparecem aqui."
        />
      ) : (
        <ul className="grid gap-2">
          {notifications.data.items.map((notification) => {
            const Icon = icons[notification.type]
            const unread = !notification.readAt
            return (
              <li key={notification.id}>
                <button
                  type="button"
                  onClick={() => open(notification)}
                  className={cn(
                    "flex w-full items-start gap-4 rounded-xl border p-4 text-left transition-colors hover:bg-muted",
                    unread && "border-primary/40 bg-secondary/50",
                  )}
                >
                  <span
                    className={cn(
                      "flex size-10 shrink-0 items-center justify-center rounded-full",
                      notification.type === "SensorAlert" ? "bg-warning text-foreground" : "bg-secondary text-primary",
                    )}
                  >
                    <Icon className="size-5" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center gap-2">
                      <span className="font-heading font-semibold text-brand-900">{notification.title}</span>
                      {unread && (
                        <span className="rounded-full bg-terracotta px-2 py-0.5 font-heading text-[11px] font-semibold text-white">
                          Nova
                        </span>
                      )}
                    </span>
                    <span className="mt-0.5 block text-sm">{notification.message}</span>
                    <span className="mt-1 block text-xs text-muted-foreground">
                      {formatDateTime(notification.createdAt)}
                    </span>
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </>
  )
}
