import { ActivityIndicator, Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from "react-native"
import { colors, radii, sizes, spacing, typography, type ColorName } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

export type ButtonVariant = "primary" | "secondary" | "text" | "destructive" | "destructiveText"

interface ButtonProps {
  label: string
  onPress?: () => void
  variant?: ButtonVariant
  icon?: IconName
  disabled?: boolean
  loading?: boolean
  // Shown instead of the label while loading ("A entrar...", "A confirmar...").
  loadingLabel?: string
  accessibilityHint?: string
  style?: StyleProp<ViewStyle>
  testID?: string
}

const palette: Record<ButtonVariant, { background: string; border: string; text: ColorName; pressed: string }> = {
  primary: { background: colors.green700, border: colors.green700, text: "white", pressed: colors.green900 },
  secondary: { background: colors.background, border: colors.green700, text: "green700", pressed: colors.green100 },
  text: { background: "transparent", border: "transparent", text: "green700", pressed: colors.green100 },
  destructive: { background: colors.error, border: colors.error, text: "white", pressed: "#8C1D17" },
  destructiveText: { background: "transparent", border: "transparent", text: "error", pressed: colors.error100 },
}

// Pill button, 48 high, label in Poppins SemiBold 15 (table 12). One primary action per screen; destructive actions
// use the error colour, never the green of the primary style.
export function Button({
  label,
  onPress,
  variant = "primary",
  icon,
  disabled = false,
  loading = false,
  loadingLabel,
  accessibilityHint,
  style,
  testID,
}: ButtonProps) {
  const tone = palette[variant]
  const inactive = disabled || loading
  const filled = variant === "primary" || variant === "destructive"
  const textColor: ColorName = disabled && filled ? "textSecondary" : disabled ? "outline" : tone.text

  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      disabled={inactive}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: inactive, busy: loading }}
      style={({ pressed }) => [
        styles.base,
        {
          backgroundColor: disabled && filled ? colors.disabled : pressed ? tone.pressed : tone.background,
          borderColor: disabled && filled ? colors.disabled : disabled ? colors.border : tone.border,
        },
        style,
      ]}
    >
      <View style={styles.content}>
        {loading ? (
          <ActivityIndicator size="small" color={colors[textColor]} />
        ) : icon ? (
          <Icon name={icon} size={sizes.iconSmall} color={textColor} />
        ) : null}
        <AppText style={typography.button} color={textColor} numberOfLines={1}>
          {loading && loadingLabel ? loadingLabel : label}
        </AppText>
      </View>
    </Pressable>
  )
}

const styles = StyleSheet.create({
  base: {
    minHeight: sizes.touch,
    borderRadius: radii.pill,
    borderWidth: 1.5,
    paddingHorizontal: spacing.lg,
    justifyContent: "center",
    alignItems: "center",
  },
  content: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
})
