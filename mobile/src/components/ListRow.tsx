import { Pressable, StyleSheet, View } from "react-native"
import { colors, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

interface ListRowProps {
  label: string
  icon?: IconName
  description?: string
  onPress: () => void
  testID?: string
}

// Row of a settings list (Perfil): icon, label and a chevron, with a divider below.
export function ListRow({ label, icon, description, onPress, testID }: ListRowProps) {
  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={description}
      style={({ pressed }) => [styles.row, pressed && { backgroundColor: colors.surface }]}
    >
      {icon ? <Icon name={icon} color="textSecondary" /> : null}
      <View style={styles.text}>
        <AppText>{label}</AppText>
        {description ? <AppText variant="caption">{description}</AppText> : null}
      </View>
      <Icon name="chevron-right" color="textSecondary" />
    </Pressable>
  )
}

const styles = StyleSheet.create({
  row: {
    minHeight: sizes.touch + 24,
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  text: { flex: 1 },
})
