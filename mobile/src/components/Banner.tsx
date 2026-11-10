import { Pressable, StyleSheet, View } from "react-native"
import { colors, radii, sizes, spacing, type ColorName } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

export type BannerTone = "error" | "warning" | "info" | "success"

const tones: Record<BannerTone, { background: string; circle: string; icon: IconName; iconColor: ColorName }> = {
  error: { background: colors.error100, circle: colors.error, icon: "warning", iconColor: "white" },
  warning: { background: colors.warning100, circle: colors.warning, icon: "error", iconColor: "ink" },
  info: { background: colors.info100, circle: colors.info, icon: "info", iconColor: "white" },
  success: { background: colors.green100, circle: colors.success, icon: "check", iconColor: "white" },
}

interface BannerProps {
  tone: BannerTone
  title?: string
  message: string
  action?: { label: string; onPress: () => void }
  testID?: string
}

// Banner with an icon, a short sentence and an optional action ("Ver", "Tentar de novo").
export function Banner({ tone, title, message, action, testID }: BannerProps) {
  const style = tones[tone]
  return (
    <View
      testID={testID}
      style={[styles.banner, { backgroundColor: style.background }]}
      accessibilityRole={tone === "error" ? "alert" : undefined}
      accessibilityLiveRegion={tone === "error" ? "assertive" : "polite"}
    >
      <View style={[styles.circle, { backgroundColor: style.circle }]}>
        <Icon name={style.icon} size={20} color={style.iconColor} />
      </View>
      <View style={styles.text}>
        {title ? <AppText variant="label">{title}</AppText> : null}
        <AppText variant="caption" color="ink" style={styles.message}>
          {message}
        </AppText>
      </View>
      {action ? (
        <Pressable onPress={action.onPress} accessibilityRole="button" style={styles.action} hitSlop={8}>
          <AppText variant="label" color={tone === "error" ? "error" : tone === "info" ? "info" : "green700"}>
            {action.label}
          </AppText>
        </Pressable>
      ) : null}
    </View>
  )
}

const styles = StyleSheet.create({
  banner: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
    padding: spacing.md,
    borderRadius: radii.card,
  },
  circle: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: "center",
    justifyContent: "center",
  },
  text: { flex: 1, gap: 2 },
  message: { fontSize: 14, lineHeight: 20 },
  action: { minHeight: sizes.touch, justifyContent: "center", paddingHorizontal: spacing.xxs },
})
