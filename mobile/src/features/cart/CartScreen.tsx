import { useState } from "react"
import { Pressable, StyleSheet, View } from "react-native"
import { useCart } from "@/cart/CartProvider"
import type { CartLine } from "@/cart/cart"
import { AppText } from "@/components/AppText"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Icon } from "@/components/Icon"
import { Illustration } from "@/components/Illustration"
import { QuantityStepper } from "@/components/QuantityStepper"
import { Screen } from "@/components/Screen"
import { EmptyState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { formatQuantity, illustrationFor, isBasket, SURPRISE_BASKET_LIMIT } from "@/lib/catalog"
import { formatPrice, formatShortDate, plural } from "@/lib/format"
import { summarizeSchedules } from "@/lib/schedule"
import type { ClientScreenProps } from "@/navigation/types"
import { colors, radii, sizes, spacing } from "@/theme/tokens"
import { useFarmsById } from "../catalog/queries"

// Note under the stepper that explains why + is disabled (P4 of the heuristic analysis).
function limitNote(line: CartLine) {
  if (line.quantity < line.max) return null
  if (line.type === "SurpriseBasket" && line.max <= SURPRISE_BASKET_LIMIT) {
    return `Limite de ${SURPRISE_BASKET_LIMIT} cabaz surpresa por cliente`
  }
  return `Stock disponível: ${formatQuantity(line.max, line.unit)}`
}

function LineItem({ line, onRemove }: { line: CartLine; onRemove: () => void }) {
  const { setQuantity } = useCart()
  const basket = isBasket(line)
  const note = limitNote(line)
  const detail = basket
    ? `${formatQuantity(line.quantity, "cabaz")} - ${formatPrice(line.price)}${line.availableUntil ? ` - até ${formatShortDate(line.availableUntil)}` : ""}`
    : `${formatPrice(line.price)} / ${line.unit}`

  return (
    <View style={styles.line} testID={`cart-line-${line.productId}`}>
      <Illustration
        name={illustrationFor(line)}
        size={44}
        background={basket ? "terracotta" : "green"}
        style={styles.thumb}
      />
      <View style={styles.lineBody}>
        <View style={styles.lineHeader}>
          <AppText variant="bodyBold" style={styles.flex}>
            {line.name}
          </AppText>
          <AppText variant="bodyBold">{formatPrice(line.price * line.quantity)}</AppText>
        </View>
        <AppText variant="caption">{detail}</AppText>
        <View style={styles.controls}>
          <QuantityStepper
            value={line.quantity}
            max={line.max}
            unit={basket ? undefined : line.unit}
            label={line.name}
            onChange={(quantity) => setQuantity(line.productId, quantity)}
          />
          <Pressable
            onPress={onRemove}
            accessibilityRole="button"
            accessibilityLabel={`Remover ${line.name}`}
            style={styles.remove}
            testID={`remove-${line.productId}`}
          >
            <Icon name="delete" color="textSecondary" />
          </Pressable>
        </View>
        {note ? (
          <View style={styles.note}>
            <Icon name="info" size={18} color="info" />
            <AppText variant="caption" color="info">
              {note}
            </AppText>
          </View>
        ) : null}
      </View>
    </View>
  )
}

// C06 Carrinho, grouped by farm with each farm's pickup hours; C06V when empty.
export function CartScreen({ navigation }: ClientScreenProps<"Cart">) {
  const { groups, lines, total, remove } = useCart()
  const farms = useFarmsById(groups.map((group) => group.farmId))
  const snackbar = useSnackbar()
  const [removing, setRemoving] = useState<CartLine | null>(null)

  if (lines.length === 0) {
    return (
      <Screen title="Carrinho" back="back">
        <EmptyState
          illustration="carrinhoVazio"
          title="O carrinho está vazio"
          message="Descubra os produtos e cabazes das quintas perto de si."
          action={{
            label: "Explorar produtos",
            onPress: () => navigation.navigate("ClientTabs", { screen: "Explore" }),
          }}
        />
      </Screen>
    )
  }

  const confirmRemove = () => {
    if (!removing) return
    const undo = remove(removing.productId)
    snackbar.show({ message: `${removing.name} removido do carrinho.`, action: { label: "Anular", onPress: undo } })
    setRemoving(null)
  }

  const footer = (
    <>
      <View style={styles.total}>
        <AppText variant="bodyBold">Total ({plural(groups.length, "encomenda", "encomendas")})</AppText>
        <AppText variant="number">{formatPrice(total)}</AppText>
      </View>
      <Button label="Rever reserva" onPress={() => navigation.navigate("ReviewReservation")} testID="cart-review" />
    </>
  )

  return (
    <Screen title="Carrinho" back="back" footer={footer} testID="cart-screen">
      {groups.map((group) => {
        const farm = farms.get(group.farmId)
        return (
          <Card key={group.farmId}>
            <View style={styles.farm}>
              <Icon name="storefront" color="green700" />
              <AppText variant="h3">{group.farmName}</AppText>
            </View>
            {farm ? (
              <AppText variant="caption">Levantamento: {summarizeSchedules(farm.pickupSchedules)}</AppText>
            ) : null}
            {group.lines.map((line) => (
              <LineItem key={line.productId} line={line} onRemove={() => setRemoving(line)} />
            ))}
            <View style={styles.subtotal}>
              <AppText color="textSecondary">Subtotal</AppText>
              <AppText variant="bodyBold">{formatPrice(group.total)}</AppText>
            </View>
          </Card>
        )
      })}

      <ConfirmDialog
        visible={removing !== null}
        destructive
        icon="delete"
        title={`Remover ${removing?.name ?? ""}?`}
        message={
          removing && groups.find((group) => group.farmId === removing.farmId)?.lines.length === 1
            ? `É o último artigo de ${removing.farmName}: a encomenda desta quinta deixa de ser feita.`
            : "Pode voltar a adicioná-lo a partir do catálogo."
        }
        confirmLabel="Remover"
        cancelLabel="Manter"
        onConfirm={confirmRemove}
        onCancel={() => setRemoving(null)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  farm: { flexDirection: "row", alignItems: "center", gap: spacing.sm },
  line: {
    flexDirection: "row",
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  thumb: { width: 56, height: 56, borderRadius: radii.card },
  lineBody: { flex: 1, gap: spacing.xxs },
  lineHeader: { flexDirection: "row", gap: spacing.xs },
  flex: { flex: 1 },
  controls: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  remove: { width: sizes.touch, height: sizes.touch, alignItems: "center", justifyContent: "center" },
  note: { flexDirection: "row", alignItems: "center", gap: spacing.xxs },
  subtotal: {
    flexDirection: "row",
    justifyContent: "space-between",
    paddingTop: spacing.sm,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  total: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
})
