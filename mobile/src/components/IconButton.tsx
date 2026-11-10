import { Pressable, StyleSheet, View } from "react-native"
import { colors, radii, sizes, type ColorName } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

interface IconButtonProps {
  icon: IconName
  label: string
  onPress?: () => void
  color?: ColorName
  // Counter on the cart and the notifications (18 × 18 circle, section 20.5).
  count?: number
  testID?: string
}

export function IconButton({ icon, label, onPress, color = "ink", count, testID }: IconButtonProps) {
  const hasCount = count !== undefined && count > 0
  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={hasCount ? `${label}, ${count}` : label}
      hitSlop={4}
      style={({ pressed }) => [styles.button, pressed && { backgroundColor: colors.surface }]}
    >
      <Icon name={icon} color={color} />
      {hasCount ? <CountBadge count={count} /> : null}
    </Pressable>
  )
}

export function CountBadge({ count }: { count: number }) {
  return (
    <View style={styles.badge} accessibilityElementsHidden importantForAccessibility="no">
      <AppText variant="badge" color="white" style={styles.badgeText}>
        {count > 9 ? "9+" : count}
      </AppText>
    </View>
  )
}

const styles = StyleSheet.create({
  button: {
    width: sizes.touch,
    height: sizes.touch,
    borderRadius: radii.round,
    alignItems: "center",
    justifyContent: "center",
  },
  badge: {
    position: "absolute",
    top: 4,
    right: 2,
    minWidth: sizes.counter,
    height: sizes.counter,
    borderRadius: sizes.counter / 2,
    paddingHorizontal: 3,
    backgroundColor: colors.error,
    alignItems: "center",
    justifyContent: "center",
  },
  badgeText: { fontSize: 11, lineHeight: 14 },
})
