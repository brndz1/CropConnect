import { useState } from "react"
import { AppText } from "@/components/AppText"
import { ChipGroup } from "@/components/Chip"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { splitOrders } from "@/lib/orders"
import type { ClientTabProps } from "@/navigation/types"
import { useFarmsById } from "../catalog/queries"
import { NotificationsButton } from "../notifications/NotificationsButton"
import { OrderCard } from "./OrderCard"
import { useMyOrders } from "./queries"

type View = "active" | "history"

// C10 Encomendas: orders in progress first, finished and cancelled ones in Anteriores (RF13, corrected P2).
export function OrdersScreen({ navigation }: ClientTabProps<"Orders">) {
  const [view, setView] = useState<View>("active")
  const orders = useMyOrders()
  const { active, past } = splitOrders(orders.data ?? [])
  const farms = useFarmsById((orders.data ?? []).map((order) => order.farmId))

  const card = (order: (typeof active)[number]) => (
    <OrderCard
      key={order.id}
      order={order}
      perspective="client"
      title={farms.get(order.farmId)?.name}
      onPress={() => navigation.navigate("Order", { orderId: order.id })}
    />
  )

  return (
    <Screen
      title="Encomendas"
      right={<NotificationsButton />}
      refreshing={orders.isRefetching}
      onRefresh={() => void orders.refetch()}
      testID="orders-screen"
    >
      <ChipGroup<View>
        label="Encomendas a mostrar"
        scroll={false}
        value={view}
        onChange={setView}
        options={[
          { value: "active", label: "Ativas" },
          { value: "history", label: "Histórico" },
        ]}
      />
      {orders.isPending ? (
        <LoadingState rows={3} height={120} />
      ) : orders.isError ? (
        <ErrorState error={orders.error} onRetry={() => void orders.refetch()} />
      ) : view === "active" ? (
        <>
          <AppText variant="label" color="textSecondary" accessibilityRole="header">
            Ativas
          </AppText>
          {active.length > 0 ? (
            active.map(card)
          ) : (
            <EmptyState
              illustration="carrinhoVazio"
              title="Sem encomendas em curso"
              message="Reserve produtos das quintas perto de si e acompanhe-os aqui."
              action={{ label: "Explorar produtos", onPress: () => navigation.navigate("Explore") }}
            />
          )}
          {past.length > 0 ? (
            <>
              <AppText variant="label" color="textSecondary" accessibilityRole="header">
                Anteriores
              </AppText>
              {past.slice(0, 3).map(card)}
            </>
          ) : null}
        </>
      ) : past.length > 0 ? (
        past.map(card)
      ) : (
        <EmptyState title="Ainda sem histórico" message="As encomendas levantadas e canceladas aparecem aqui." />
      )}
    </Screen>
  )
}
