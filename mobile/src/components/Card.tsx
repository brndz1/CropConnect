import type { ReactNode } from "react"
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from "react-native"
import { colors, radii, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"

interface CardProps {
  children: ReactNode
  title?: string
  // Text action on the right of the title ("Editar", "Ver todos").
  action?: { label: string; onPress: () => void }
  onPress?: () => void
  accessibilityLabel?: string
  highlight?: "error" | "selected"
  style?: StyleProp<ViewStyle>
  testID?: string
}

// Flat card (elevation level 0) with a border and a 12 radius.
export function Card({ children, title, action, onPress, accessibilityLabel, highlight, style, testID }: CardProps) {
  const border =
    highlight === "error" ? { borderColor: colors.error, borderWidth: 2 } : highlight === "selected" ? selected : null
  const header =
    title || action ? (
      <View style={styles.header}>
        {title ? (
          <AppText variant="label" color="textSecondary" accessibilityRole="header" style={styles.title}>
            {title}
          </AppText>
        ) : null}
        {action ? (
          <Pressable onPress={action.onPress} accessibilityRole="button" hitSlop={12}>
            <AppText variant="label" color="green700">
              {action.label}
            </AppText>
          </Pressable>
        ) : null}
      </View>
    ) : null

  if (onPress) {
    return (
      <Pressable
        testID={testID}
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        style={({ pressed }) => [styles.card, border, pressed && { backgroundColor: colors.surface }, style]}
      >
        {header}
        {children}
      </Pressable>
    )
  }
  return (
    <View testID={testID} style={[styles.card, border, style]} accessibilityLabel={accessibilityLabel}>
      {header}
      {children}
    </View>
  )
}

const selected = { borderColor: colors.green700, borderWidth: 2 }

const styles = StyleSheet.create({
  card: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.card,
    backgroundColor: colors.background,
    padding: spacing.md,
    gap: spacing.sm,
  },
  header: { flexDirection: "row", justifyContent: "space-between", alignItems: "center" },
  title: { flex: 1 },
})
