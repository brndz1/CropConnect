import { StyleSheet, View } from "react-native"
import type { Order } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { StatusBadge } from "@/components/Badge"
import { Card } from "@/components/Card"
import { formatDate, formatPrice, plural, shortId } from "@/lib/format"
import { parseOrderNotes } from "@/lib/orders"
import { spacing } from "@/theme/tokens"

interface OrderCardProps {
  order: Order
  // Farm name for the client's list; the client's name for the farm's list.
  title?: string
  perspective: "client" | "manager"
  onPress: () => void
}

// Order card: number, farm or client, articles, pickup, total and status badge (table 12).
export function OrderCard({ order, title, perspective, onPress }: OrderCardProps) {
  const { slot, customer } = parseOrderNotes(order.notes)
  const items =
    order.items.length === 1 && order.items[0]
      ? order.items[0].productName
      : plural(order.items.length, "artigo", "artigos")
  const who = perspective === "client" ? (title ?? "Quinta") : (customer ?? "Cliente")
  const when =
    order.status === "PickedUp"
      ? `Levantada a ${formatDate(order.updatedAt ?? order.createdAt ?? "")}`
      : order.status === "Cancelled"
        ? `Cancelada a ${formatDate(order.updatedAt ?? order.createdAt ?? "")}`
        : slot
          ? `Levantar ${slot}`
          : order.createdAt
            ? `Reservada a ${formatDate(order.createdAt)}`
            : null
  const icon = perspective === "manager" ? "person" : order.status === "PickedUp" ? "task-alt" : "shopping-bag"

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={`Encomenda ${shortId(order.id)}, ${who}, ${formatPrice(order.totalAmount)}`}
      testID={`order-${order.id}`}
    >
      <View style={styles.row}>
        <Avatar icon={icon} />
        <View style={styles.body}>
          <View style={styles.header}>
            <AppText variant="bodyBold" style={styles.flex}>
              Encomenda {shortId(order.id)}
            </AppText>
            <AppText variant="bodyBold">{formatPrice(order.totalAmount)}</AppText>
          </View>
          <AppText variant="caption">
            {who} - {items}
          </AppText>
          {when ? <AppText variant="caption">{when}</AppText> : null}
          <StatusBadge status={order.status} />
        </View>
      </View>
    </Card>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md },
  body: { flex: 1, gap: spacing.xxs },
  header: { flexDirection: "row", gap: spacing.xs },
  flex: { flex: 1 },
})
