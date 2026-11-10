import { useQueries } from "@tanstack/react-query"
import { StyleSheet, View } from "react-native"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"
import type { Order } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { formatPrice, plural } from "@/lib/format"
import type { ClientScreenProps } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"
import { useFarmsById } from "../catalog/queries"
import { OrderCard } from "../orders/OrderCard"

// C09 Reserva confirmada, with the total to pay right after the message (corrected P3).
export function ReservationConfirmedScreen({ navigation, route }: ClientScreenProps<"ReservationConfirmed">) {
  const results = useQueries({
    queries: route.params.orderIds.map((id) => ({
      queryKey: queryKeys.order(id),
      queryFn: () => call(client.GET("/api/orders/{id}", { params: { path: { id } } })),
    })),
  })
  const orders = results.map((result) => result.data).filter((order): order is Order => Boolean(order))
  const farms = useFarmsById(orders.map((order) => order.farmId))
  const total = orders.reduce((sum, order) => sum + order.totalAmount, 0)
  const count = route.params.orderIds.length

  const goToOrders = () => {
    navigation.popToTop()
    navigation.navigate("ClientTabs", { screen: "Orders" })
  }

  const footer = (
    <>
      <Button label="Ver as minhas encomendas" onPress={goToOrders} testID="see-orders" />
      <Button
        label="Continuar a explorar"
        variant="text"
        onPress={() => {
          navigation.popToTop()
          navigation.navigate("ClientTabs", { screen: "Explore" })
        }}
      />
    </>
  )

  return (
    <Screen back="close" onBack={goToOrders} footer={footer} testID="confirmed-screen">
      <View style={styles.hero}>
        <View style={styles.circle}>
          <View style={styles.check}>
            <Icon name="check" size={40} color="white" />
          </View>
        </View>
        <AppText variant="h2" align="center" accessibilityRole="header">
          Reserva confirmada!
        </AppText>
        <AppText color="textSecondary" align="center">
          {count === 1
            ? "Criámos a sua encomenda. Receberá um aviso quando estiver pronta a levantar."
            : `Criámos ${plural(count, "encomenda", "encomendas")}, uma por quinta. Receberá um aviso quando estiverem prontas a levantar.`}
        </AppText>
      </View>
      <Banner
        tone="info"
        title={`Total a pagar: ${formatPrice(total)}`}
        message="Paga a cada produtor quando levantar a encomenda."
      />
      {orders.map((order) => (
        <OrderCard
          key={order.id}
          order={order}
          perspective="client"
          title={farms.get(order.farmId)?.name}
          onPress={() => navigation.navigate("Order", { orderId: order.id })}
        />
      ))}
    </Screen>
  )
}

const styles = StyleSheet.create({
  hero: { alignItems: "center", gap: spacing.sm, paddingTop: spacing.md },
  circle: {
    width: 112,
    height: 112,
    borderRadius: 56,
    backgroundColor: colors.green100,
    alignItems: "center",
    justifyContent: "center",
  },
  check: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: colors.success,
    alignItems: "center",
    justifyContent: "center",
  },
})
