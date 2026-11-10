import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { call, client } from "@/api/client"
import { errorMessage } from "@/api/errors"
import { useSession } from "@/auth/AuthProvider"
import { useCart } from "@/cart/CartProvider"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Chip } from "@/components/Chip"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { EmptyState } from "@/components/ScreenStates"
import { formatQuantity, isBasket } from "@/lib/catalog"
import { formatPrice } from "@/lib/format"
import { nextPickupSlots } from "@/lib/schedule"
import type { ClientScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useFarmsById } from "../catalog/queries"
import { InfoRow } from "../catalog/InfoRow"
import { useCreateOrder } from "../orders/queries"
import { displayName, useMyProfile } from "../profile/queries"
import { placeOrders, type Shortage } from "./checkout"

// C07 Rever reserva: one order per farm, each with its pickup slot; the total is paid at pickup (RF10, RF11).
// A shortage shows the farm, the article and what is left (C07E, corrected P1) and leads to the adjusted cart.
export function ReviewReservationScreen({ navigation }: ClientScreenProps<"ReviewReservation">) {
  const session = useSession()
  const profile = useMyProfile()
  const cart = useCart()
  const farms = useFarmsById(cart.groups.map((group) => group.farmId))
  const createOrder = useCreateOrder()
  const [chosen, setChosen] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [shortage, setShortage] = useState<Shortage | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  const groups = cart.groups
  if (groups.length === 0) {
    return (
      <Screen title="Rever reserva" back="back">
        <EmptyState illustration="carrinhoVazio" title="O carrinho está vazio" />
      </Screen>
    )
  }

  // Slots offered per farm: the next ones, and none after the pickup limit of a basket in the order.
  const slotsOf = (farmId: string) => {
    const group = groups.find((item) => item.farmId === farmId)
    const limits = (group?.lines ?? [])
      .map((line) => line.availableUntil)
      .filter((value): value is string => Boolean(value))
      .map((value) => new Date(value))
    const until = limits.length ? new Date(Math.min(...limits.map((date) => date.getTime()))) : null
    return nextPickupSlots(farms.get(farmId)?.pickupSchedules ?? [], new Date(), 3, until)
  }
  const selectedSlot = (farmId: string) => chosen[farmId] ?? slotsOf(farmId)[0]?.label

  async function confirm() {
    setBusy(true)
    setFailure(null)
    setShortage(null)
    const slots = Object.fromEntries(groups.map((group) => [group.farmId, selectedSlot(group.farmId)]))
    const result = await placeOrders(groups, slots, displayName(profile.data, session.email), {
      stockOf: async (id) => (await call(client.GET("/api/products/{id}", { params: { path: { id } } }))).stockQuantity,
      createOrder: (body) => createOrder.mutateAsync(body),
    }).catch((error: unknown) => ({ orders: [], placedFarmIds: [], shortage: null, error }))
    setBusy(false)

    result.placedFarmIds.forEach((farmId) => cart.removeFarm(farmId))
    if (result.shortage) {
      setShortage(result.shortage)
      return
    }
    if (result.error) {
      setFailure(
        result.orders.length > 0
          ? `Criámos ${result.orders.length} de ${groups.length} encomendas. As restantes não foram feitas: ${errorMessage(result.error)}`
          : errorMessage(result.error),
      )
      return
    }
    navigation.replace("ReservationConfirmed", { orderIds: result.orders.map((order) => order.id) })
  }

  function adjustInCart() {
    if (shortage) cart.adjustToStock(shortage.productId, shortage.available)
    navigation.navigate("Cart")
  }

  const footer = (
    <>
      <View style={styles.total}>
        <AppText variant="bodyBold">Total a pagar no levantamento</AppText>
        <AppText variant="number">{formatPrice(cart.total)}</AppText>
      </View>
      {shortage ? (
        <Button label="Ajustar no carrinho" onPress={adjustInCart} testID="adjust-cart" />
      ) : (
        <Button
          label="Confirmar reserva"
          loadingLabel="A confirmar..."
          loading={busy}
          onPress={confirm}
          testID="confirm-reservation"
        />
      )}
    </>
  )

  return (
    <Screen title="Rever reserva" back="back" footer={footer} testID="review-screen">
      {shortage ? (
        <Banner
          tone="error"
          title={`Stock insuficiente na ${shortage.farmName}`}
          message={
            shortage.available > 0
              ? `Pediu ${formatQuantity(shortage.requested, shortage.unit)} de ${shortage.productName}, mas só resta ${formatQuantity(shortage.available, shortage.unit)}. Ajuste a quantidade no carrinho para continuar.`
              : `${shortage.productName} esgotou. Retire-o do carrinho para continuar.`
          }
          testID="shortage-banner"
        />
      ) : null}
      {failure ? (
        <Banner tone="error" message={failure} action={{ label: "Tentar de novo", onPress: confirm }} />
      ) : null}

      {groups.map((group, index) => {
        const farm = farms.get(group.farmId)
        const slots = slotsOf(group.farmId)
        const selected = selectedSlot(group.farmId)
        return (
          <Card key={group.farmId} highlight={shortage?.farmId === group.farmId ? "error" : undefined}>
            <AppText variant="label" color="textSecondary">
              Encomenda {index + 1} de {groups.length}
            </AppText>
            <View style={styles.farm}>
              <Icon name="storefront" color="green700" />
              <AppText variant="h3">{group.farmName}</AppText>
            </View>
            <AppText variant="label">Horário de levantamento</AppText>
            {slots.length > 0 ? (
              <View style={styles.slots} accessibilityRole="radiogroup">
                {slots.map((slot) => (
                  <Chip
                    key={slot.label}
                    label={slot.label}
                    selected={slot.label === selected}
                    onPress={() => setChosen((current) => ({ ...current, [group.farmId]: slot.label }))}
                  />
                ))}
              </View>
            ) : (
              <AppText variant="caption">A quinta combina o levantamento consigo depois de confirmar.</AppText>
            )}
            {farm ? <InfoRow icon="place" title={farm.location} /> : null}
            {group.lines.map((line) => (
              <View key={line.productId} style={styles.item}>
                <AppText color="textSecondary" style={styles.flex}>
                  {line.name} × {isBasket(line) ? line.quantity : formatQuantity(line.quantity, line.unit)}
                </AppText>
                <AppText color="textSecondary">{formatPrice(line.price * line.quantity)}</AppText>
              </View>
            ))}
            <View style={styles.subtotal}>
              <AppText color="textSecondary">Subtotal</AppText>
              <AppText variant="bodyBold">{formatPrice(group.total)}</AppText>
            </View>
          </Card>
        )
      })}
      <Banner tone="info" message="O pagamento é feito em mão, a cada produtor, quando levantar a encomenda." />
    </Screen>
  )
}

const styles = StyleSheet.create({
  farm: { flexDirection: "row", alignItems: "center", gap: spacing.sm },
  slots: { alignItems: "flex-start" },
  item: { flexDirection: "row", gap: spacing.sm },
  flex: { flex: 1 },
  subtotal: { flexDirection: "row", justifyContent: "space-between", paddingTop: spacing.xs },
  total: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
})
