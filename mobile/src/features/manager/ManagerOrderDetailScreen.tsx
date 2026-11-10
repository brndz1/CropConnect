import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { StatusBadge } from "@/components/Badge"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Illustration } from "@/components/Illustration"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { illustrationFor } from "@/lib/catalog"
import { dayShort, formatClock, formatPrice, formatShortDate, shortId } from "@/lib/format"
import { isOpen, nextManagerStatus, parseOrderNotes, stepOf } from "@/lib/orders"
import type { ManagerScreenProps } from "@/navigation/types"
import { colors, radii, spacing } from "@/theme/tokens"
import { InfoRow } from "../catalog/InfoRow"
import { useFarm } from "../catalog/queries"
import { useCancelOrder, useOrder, useUpdateOrderStatus } from "../orders/queries"

const days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const

function received(value: string | null) {
  if (!value) return null
  const date = new Date(value)
  return `Recebida ${dayShort[days[date.getDay()] ?? "Monday"].toLowerCase()} ${formatShortDate(date)} às ${formatClock(date)}`
}

const actions: Record<
  "Confirmed" | "ReadyForPickup",
  { label: string; title: string; message: (name: string) => string; done: string }
> = {
  Confirmed: {
    label: "Confirmar encomenda",
    title: "Confirmar a encomenda",
    message: (name) => `Avisamos ${name} de que a encomenda foi aceite.`,
    done: "Encomenda confirmada. Enviámos o aviso ao cliente.",
  },
  ReadyForPickup: {
    label: "Marcar como pronta a levantar",
    title: "Marcar como pronta a levantar",
    message: (name) => `Avisamos ${name} de que já pode vir buscar a encomenda.`,
    done: "Marcada como pronta. Enviámos o aviso ao cliente.",
  },
}

// G03 to G06: the farm processes an order step by step, each change confirmed in a dialog (DG1, DG2) and notified
// to the client (RF12, RF19). The API lets only the client confirm the pickup, so the last step waits for them.
export function ManagerOrderDetailScreen({ route }: ManagerScreenProps<"ManagerOrder">) {
  const order = useOrder(route.params.orderId)
  const farm = useFarm(order.data?.farmId)
  const update = useUpdateOrderStatus()
  const cancel = useCancelOrder()
  const snackbar = useSnackbar()
  const [dialog, setDialog] = useState<"advance" | "cancel" | null>(null)

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
  const { slot, customer } = parseOrderNotes(data.notes)
  const name = customer ?? "o cliente"
  const next = nextManagerStatus(data.status)
  const step = stepOf(data.status)

  const advance = (status: "Confirmed" | "ReadyForPickup") =>
    update.mutate(
      { order: data, status },
      {
        onSuccess: () => {
          setDialog(null)
          snackbar.show({ message: actions[status].done })
        },
        onError: (error) => {
          setDialog(null)
          snackbar.show({
            tone: "error",
            message: errorMessage(error),
            action: { label: "Tentar de novo", onPress: () => advance(status) },
          })
        },
      },
    )

  const refuse = () =>
    cancel.mutate(
      { id: data.id, version: data.version, reason: "Cancelada pela quinta." },
      {
        onSuccess: () => {
          setDialog(null)
          snackbar.show({ message: "Encomenda cancelada. Enviámos o aviso ao cliente." })
        },
        onError: (error) => {
          setDialog(null)
          snackbar.show({ tone: "error", message: errorMessage(error) })
        },
      },
    )

  const footer = isOpen(data.status) ? (
    <>
      {next ? <Button label={actions[next].label} onPress={() => setDialog("advance")} testID="advance-order" /> : null}
      <Button
        label="Cancelar encomenda"
        variant="destructiveText"
        onPress={() => setDialog("cancel")}
        testID="refuse-order"
      />
    </>
  ) : undefined

  return (
    <Screen
      title={`Encomenda ${shortId(data.id)}`}
      back="back"
      footer={footer}
      refreshing={order.isRefetching}
      onRefresh={() => void order.refetch()}
      testID="manager-order-screen"
    >
      <Card>
        <View style={styles.statusRow}>
          <StatusBadge status={data.status} />
          {data.status !== "Cancelled" ? <AppText variant="caption">Passo {step} de 4</AppText> : null}
        </View>
        {data.status !== "Cancelled" ? (
          <View style={styles.segments} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
            {[1, 2, 3, 4].map((segment) => (
              <View key={segment} style={[styles.segment, segment <= step && styles.segmentDone]} />
            ))}
          </View>
        ) : null}
        <AppText variant="caption">{received(data.createdAt)}</AppText>
      </Card>

      {data.status === "ReadyForPickup" ? (
        <Banner
          tone="info"
          title="À espera do levantamento"
          message={`Entregue a ${name} e receba o pagamento em mão. O levantamento fica registado quando o cliente o confirmar na app.`}
        />
      ) : null}

      <Card title="Cliente">
        <View style={styles.client}>
          <Avatar name={customer ?? "?"} />
          <View>
            <AppText variant="bodyBold">{customer ?? "Cliente"}</AppText>
            <AppText variant="caption">Cliente</AppText>
          </View>
        </View>
      </Card>

      <Card title="Levantamento na quinta">
        <InfoRow icon="schedule" title={slot ?? "No horário da quinta"} />
        {farm.data ? <InfoRow icon="place" title={`Na quinta - ${farm.data.location}`} /> : null}
        {data.status === "ReadyForPickup" ? (
          <InfoRow
            icon="person"
            title={`Entregar a ${name}`}
            subtitle={`Confirme o nome e o n.º da encomenda (${shortId(data.id)}).`}
          />
        ) : null}
      </Card>

      <Card title="Produtos">
        {data.items.map((item) => (
          <View key={item.productId} style={styles.item}>
            <Illustration
              name={illustrationFor({ name: item.productName, type: "Product" })}
              size={36}
              background="green"
              style={styles.thumb}
            />
            <View style={styles.flex}>
              <AppText variant="bodyBold">{item.productName}</AppText>
              <AppText variant="caption">
                {item.quantity} × {formatPrice(item.unitPrice)}
              </AppText>
            </View>
            <AppText variant="bodyBold">{formatPrice(item.totalPrice)}</AppText>
          </View>
        ))}
        <View style={styles.total}>
          <AppText variant="bodyBold">Total</AppText>
          <AppText variant="number">{formatPrice(data.totalAmount)}</AppText>
        </View>
        <InfoRow icon="euro" title="Pagamento em mão no levantamento" />
      </Card>

      {next ? (
        <ConfirmDialog
          visible={dialog === "advance"}
          title={`${actions[next].title} ${shortId(data.id)}?`}
          message={actions[next].message(name)}
          confirmLabel={next === "Confirmed" ? "Confirmar" : "Marcar como pronta"}
          loading={update.isPending}
          onConfirm={() => advance(next)}
          onCancel={() => setDialog(null)}
        />
      ) : null}
      <ConfirmDialog
        visible={dialog === "cancel"}
        destructive
        title={`Cancelar a encomenda ${shortId(data.id)}?`}
        message={`Avisamos ${name} e o stock volta ao catálogo. Esta ação não pode ser anulada.`}
        confirmLabel="Cancelar encomenda"
        cancelLabel="Manter"
        loading={cancel.isPending}
        onConfirm={refuse}
        onCancel={() => setDialog(null)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  statusRow: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
  segments: { flexDirection: "row", gap: spacing.xxs },
  segment: { flex: 1, height: 6, borderRadius: 3, backgroundColor: colors.disabled },
  segmentDone: { backgroundColor: colors.green700 },
  client: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  item: { flexDirection: "row", alignItems: "center", gap: spacing.sm },
  thumb: { width: 48, height: 48, borderRadius: radii.card },
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
