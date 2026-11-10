import { useState } from "react"
import { StyleSheet, View } from "react-native"
import type { Order } from "@/api/types"
import { errorMessage } from "@/api/errors"
import { AppText } from "@/components/AppText"
import { StatusBadge } from "@/components/Badge"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { formatDateTime, formatPrice, shortId } from "@/lib/format"
import { clientCanCancel, parseOrderNotes } from "@/lib/orders"
import type { ClientScreenProps } from "@/navigation/types"
import { colors, radii, spacing } from "@/theme/tokens"
import { InfoRow } from "../catalog/InfoRow"
import { useFarm } from "../catalog/queries"
import { OrderTimeline } from "./OrderTimeline"
import { useCancelOrder, useConfirmPickup, useOrder } from "./queries"
import { ReviewForm } from "./ReviewForm"

function heading(order: Order, slot: string | null) {
  switch (order.status) {
    case "Pending":
      return {
        title: "A aguardar confirmação da quinta",
        text: `Reservada a ${formatDateTime(order.createdAt ?? "")}. Recebe um aviso a cada mudança.`,
      }
    case "Confirmed":
      return { title: "A quinta aceitou a reserva", text: "Recebe um aviso quando estiver pronta a levantar." }
    case "ReadyForPickup":
      return { title: "Pronta a levantar!", text: slot ? `Levantar ${slot}.` : "Pode levantá-la no horário da quinta." }
    case "PickedUp":
      return { title: "Encomenda levantada", text: `Levantada a ${formatDateTime(order.updatedAt ?? "")}.` }
    case "Cancelled":
      return {
        title: "Encomenda cancelada",
        text: order.cancellationReason ?? "O stock voltou à quinta. Não há nada a pagar.",
      }
  }
}

// C11 Encomenda: state, progress, pickup and articles; cancel until it is ready (RF14), confirm the pickup at the
// farm and review it afterwards.
export function OrderDetailScreen({ route }: ClientScreenProps<"Order">) {
  const order = useOrder(route.params.orderId)
  const farm = useFarm(order.data?.farmId)
  const cancel = useCancelOrder()
  const pickup = useConfirmPickup()
  const snackbar = useSnackbar()
  const [dialog, setDialog] = useState<"cancel" | "pickup" | null>(null)

  if (order.isPending) {
    return (
      <Screen title="Encomenda" back="back">
        <LoadingState rows={3} height={140} />
      </Screen>
    )
  }
  if (order.isError) {
    return (
      <Screen title="Encomenda" back="back">
        <ErrorState error={order.error} onRetry={() => void order.refetch()} />
      </Screen>
    )
  }

  const data = order.data
  const { slot } = parseOrderNotes(data.notes)
  const farmName = farm.data?.name ?? "a quinta"
  const { title, text } = heading(data, slot)
  const ready = data.status === "ReadyForPickup"

  const run = (action: "cancel" | "pickup") => {
    const mutation = action === "cancel" ? cancel : pickup
    mutation.mutate(data, {
      onSuccess: () => {
        setDialog(null)
        snackbar.show({ message: action === "cancel" ? "Encomenda cancelada." : "Levantamento registado. Obrigado!" })
      },
      onError: (error) => {
        setDialog(null)
        snackbar.show({ tone: "error", message: errorMessage(error) })
      },
    })
  }

  const footer =
    ready || clientCanCancel(data.status) ? (
      <>
        {ready ? (
          <Button
            label="Confirmar levantamento"
            icon="shopping-bag"
            onPress={() => setDialog("pickup")}
            testID="confirm-pickup"
          />
        ) : null}
        {clientCanCancel(data.status) ? (
          <Button
            label="Cancelar encomenda"
            variant="destructiveText"
            onPress={() => setDialog("cancel")}
            testID="cancel-order"
          />
        ) : null}
      </>
    ) : undefined

  return (
    <Screen
      title={`Encomenda ${shortId(data.id)}`}
      back="back"
      footer={footer}
      refreshing={order.isRefetching}
      onRefresh={() => void order.refetch()}
      testID="order-screen"
    >
      <View style={[styles.hero, ready && styles.heroReady]}>
        <StatusBadge status={data.status} />
        <AppText variant="h3" color={ready ? "green900" : "ink"}>
          {title}
        </AppText>
        <AppText color={ready ? "ink" : "textSecondary"}>{text}</AppText>
      </View>

      {ready ? (
        <Banner
          tone="info"
          title="No levantamento"
          message={`Indique o seu nome e o número da encomenda (${shortId(data.id)}) ao produtor e pague em mão. Depois confirme aqui o levantamento.`}
        />
      ) : null}

      {data.status !== "Cancelled" ? (
        <Card title="Progresso">
          <OrderTimeline order={data} />
        </Card>
      ) : null}

      <Card title="Levantamento">
        <InfoRow icon="schedule" title={slot ?? "No horário da quinta"} />
        {farm.data ? <InfoRow icon="place" title={farm.data.location} subtitle={farm.data.name} /> : null}
      </Card>

      <Card title="Artigos">
        {data.items.map((item) => (
          <View key={item.productId} style={styles.item}>
            <AppText color="textSecondary" style={styles.flex}>
              {item.productName} × {item.quantity}
            </AppText>
            <AppText color="textSecondary">{formatPrice(item.totalPrice)}</AppText>
          </View>
        ))}
        <View style={styles.total}>
          <AppText variant="bodyBold">Total</AppText>
          <AppText variant="number">{formatPrice(data.totalAmount)}</AppText>
        </View>
        <InfoRow icon="euro" title="Pagamento em mão no levantamento" />
      </Card>

      {data.status === "PickedUp" ? <ReviewForm orderId={data.id} farmId={data.farmId} farmName={farmName} /> : null}

      <ConfirmDialog
        visible={dialog === "cancel"}
        destructive
        title={`Cancelar a encomenda ${shortId(data.id)}?`}
        message={`A ${farmName} deixa de a preparar e o stock volta ao catálogo. Esta ação não pode ser anulada.`}
        confirmLabel="Cancelar encomenda"
        cancelLabel="Manter"
        loading={cancel.isPending}
        onConfirm={() => run("cancel")}
        onCancel={() => setDialog(null)}
      />
      <ConfirmDialog
        visible={dialog === "pickup"}
        icon="shopping-bag"
        title="Confirmar o levantamento?"
        message="Confirme só depois de receber os produtos e pagar ao produtor."
        confirmLabel="Já levantei"
        loading={pickup.isPending}
        onConfirm={() => run("pickup")}
        onCancel={() => setDialog(null)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  hero: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.card,
    padding: spacing.md,
    gap: spacing.xs,
  },
  heroReady: { backgroundColor: colors.green100, borderColor: colors.green100 },
  item: { flexDirection: "row", gap: spacing.sm },
  flex: { flex: 1 },
  total: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingTop: spacing.xs,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
})
