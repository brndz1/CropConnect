import { StyleSheet, View } from "react-native"
import type { OrderStatus } from "@/api/types"
import { orderStatusLabel } from "@/lib/format"
import { colors, radii, spacing, type ColorName } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

export type BadgeTone =
  "warning" | "info" | "success" | "successSoft" | "error" | "neutral" | "terracotta" | "green" | "picked"

const tones: Record<BadgeTone, { background: string; text: ColorName }> = {
  warning: { background: colors.warning, text: "ink" },
  info: { background: colors.info, text: "white" },
  success: { background: colors.success, text: "white" },
  successSoft: { background: colors.green100, text: "green900" },
  error: { background: colors.error, text: "white" },
  neutral: { background: colors.surface, text: "textSecondary" },
  terracotta: { background: colors.terracotta600, text: "white" },
  green: { background: colors.green100, text: "green900" },
  picked: { background: colors.green100, text: "green900" },
}

interface BadgeProps {
  label: string
  tone: BadgeTone
  icon: IconName
}

// Pill badge: icon and text always present, never colour alone (table 12).
export function Badge({ label, tone, icon }: BadgeProps) {
  const style = tones[tone]
  return (
    <View style={[styles.badge, { backgroundColor: style.background }]} accessibilityLabel={label}>
      <Icon name={icon} size={16} color={style.text} />
      <AppText variant="badge" color={style.text} numberOfLines={1}>
        {label}
      </AppText>
    </View>
  )
}

const statusStyle: Record<OrderStatus, { tone: BadgeTone; icon: IconName }> = {
  Pending: { tone: "warning", icon: "schedule" },
  Confirmed: { tone: "info", icon: "check" },
  ReadyForPickup: { tone: "success", icon: "shopping-bag" },
  PickedUp: { tone: "picked", icon: "check-circle-outline" },
  Cancelled: { tone: "error", icon: "cancel" },
}

export function StatusBadge({ status }: { status: OrderStatus }) {
  const { tone, icon } = statusStyle[status]
  return <Badge label={orderStatusLabel[status]} tone={tone} icon={icon} />
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: "flex-start",
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.xxs,
    borderRadius: radii.pill,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xxs,
  },
})
