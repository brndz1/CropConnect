import { StyleSheet, View } from "react-native"
import type { Order } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Icon } from "@/components/Icon"
import { formatClock, formatShortDate, orderStatusLabel } from "@/lib/format"
import { statusSteps, stepOf } from "@/lib/orders"
import { colors, spacing } from "@/theme/tokens"

const when = (value: string | null | undefined) => (value ? `${formatShortDate(value)}, ${formatClock(value)}` : null)

// Progress of an order (C11): done steps with a check, the current one with a ring, the rest in grey. The API keeps
// the creation time and the time of the last change, so those are the dates shown.
export function OrderTimeline({ order }: { order: Order }) {
  const current = stepOf(order.status)
  return (
    <View accessibilityRole="list">
      {statusSteps.map((status, index) => {
        const step = index + 1
        const done = step < current || order.status === "PickedUp"
        const active = step === current && order.status !== "PickedUp"
        const date = step === 1 ? when(order.createdAt) : step === current ? when(order.updatedAt) : null
        const last = index === statusSteps.length - 1
        return (
          <View
            key={status}
            style={styles.row}
            accessible
            accessibilityLabel={`${orderStatusLabel[status]}${done ? ", concluído" : active ? ", estado atual" : ""}${date ? `, ${date}` : ""}`}
          >
            <View style={styles.rail}>
              <View style={[styles.dot, done ? styles.done : active ? styles.active : styles.todo]}>
                {done ? <Icon name="check" size={16} color="white" /> : null}
              </View>
              {last ? null : <View style={[styles.line, done ? styles.lineDone : null]} />}
            </View>
            <View style={styles.text}>
              <AppText variant={done || active ? "bodyBold" : "body"} color={done || active ? "ink" : "textSecondary"}>
                {orderStatusLabel[status]}
              </AppText>
              {date && (done || active) ? <AppText variant="caption">{date}</AppText> : null}
            </View>
          </View>
        )
      })}
    </View>
  )
}

const DOT = 28

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md, minHeight: 56 },
  rail: { alignItems: "center", width: DOT },
  dot: { width: DOT, height: DOT, borderRadius: DOT / 2, alignItems: "center", justifyContent: "center" },
  done: { backgroundColor: colors.green700 },
  active: { borderWidth: 3, borderColor: colors.green700, backgroundColor: colors.background },
  todo: { borderWidth: 2, borderColor: colors.border, backgroundColor: colors.background },
  line: { flex: 1, width: 2, backgroundColor: colors.border },
  lineDone: { backgroundColor: colors.green700 },
  text: { flex: 1, paddingBottom: spacing.sm },
})
