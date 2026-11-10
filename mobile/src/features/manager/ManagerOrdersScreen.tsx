import { useState } from "react"
import type { OrderStatus } from "@/api/types"
import { AppText } from "@/components/AppText"
import { ChipGroup } from "@/components/Chip"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { plural } from "@/lib/format"
import type { ManagerTabProps } from "@/navigation/types"
import { NotificationsButton } from "../notifications/NotificationsButton"
import { OrderCard } from "../orders/OrderCard"
import { useFarmOrders } from "../orders/queries"

const tabs: { value: OrderStatus; label: string }[] = [
  { value: "Pending", label: "Pendentes" },
  { value: "Confirmed", label: "Confirmadas" },
  { value: "ReadyForPickup", label: "Prontas" },
  { value: "PickedUp", label: "Levantadas" },
  { value: "Cancelled", label: "Canceladas" },
]

// G02 Encomendas of the farm, organised by status (RF12), oldest first so nothing waits too long.
export function ManagerOrdersScreen({ navigation }: ManagerTabProps<"ManagerOrders">) {
  const [status, setStatus] = useState<OrderStatus>("Pending")
  const orders = useFarmOrders(status)
  const list = [...(orders.data ?? [])].sort((a, b) => (a.createdAt ?? "").localeCompare(b.createdAt ?? ""))
  const finished = status === "PickedUp" || status === "Cancelled"
  if (finished) list.reverse()

  return (
    <Screen
      title="Encomendas"
      right={<NotificationsButton />}
      refreshing={orders.isRefetching}
      onRefresh={() => void orders.refetch()}
      testID="manager-orders-screen"
    >
      <ChipGroup label="Estado das encomendas" options={tabs} value={status} onChange={setStatus} />
      {orders.isPending ? (
        <LoadingState rows={3} height={120} />
      ) : orders.isError ? (
        <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
      ) : list.length > 0 ? (
        <>
          <AppText variant="caption">{plural(list.length, "encomenda", "encomendas")}</AppText>
          {list.map((order) => (
            <OrderCard
              key={order.id}
              order={order}
              perspective="manager"
              onPress={() => navigation.navigate("ManagerOrder", { orderId: order.id })}
            />
          ))}
        </>
      ) : (
        <EmptyState
          title={`Sem encomendas ${tabs.find((tab) => tab.value === status)?.label.toLowerCase() ?? ""}`}
          message={status === "Pending" ? "As novas reservas aparecem aqui e chegam também por email." : undefined}
        />
      )}
    </Screen>
  )
}
