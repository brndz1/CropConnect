import { useNavigation } from "@react-navigation/native"
import type { ReactNode } from "react"
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  View,
  type StyleProp,
  type ViewStyle,
} from "react-native"
import { SafeAreaView, useSafeAreaInsets } from "react-native-safe-area-context"
import { colors, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

interface HeaderProps {
  title?: string
  subtitle?: string
  // Back always in the top left corner (table 24); "close" for the end of a flow.
  back?: "back" | "close" | false
  onBack?: () => void
  right?: ReactNode
}

export function ScreenHeader({ title, subtitle, back = false, onBack, right }: HeaderProps) {
  const navigation = useNavigation()
  const icon: IconName = back === "close" ? "close" : "arrow-back-ios-new"
  return (
    <View style={styles.header}>
      {back ? (
        <Pressable
          onPress={onBack ?? (() => navigation.goBack())}
          accessibilityRole="button"
          accessibilityLabel={back === "close" ? "Fechar" : "Voltar"}
          style={styles.back}
          testID="header-back"
        >
          <Icon name={icon} size={22} />
        </Pressable>
      ) : null}
      <View style={styles.titles}>
        {title ? (
          <AppText variant={back ? "h2" : "h1"} accessibilityRole="header" numberOfLines={2}>
            {title}
          </AppText>
        ) : null}
        {subtitle ? <AppText variant="caption">{subtitle}</AppText> : null}
      </View>
      {right ? <View style={styles.right}>{right}</View> : null}
    </View>
  )
}

interface ScreenProps extends HeaderProps {
  children: ReactNode
  // Sticky area at the bottom for the main action, above the safe area.
  footer?: ReactNode
  scroll?: boolean
  // A custom header instead of the title row (home screens).
  header?: ReactNode
  refreshing?: boolean
  onRefresh?: () => void
  contentStyle?: StyleProp<ViewStyle>
  testID?: string
}

// Layout of every screen: header, scrolling content with 16 side margins and an optional sticky footer.
export function Screen({
  children,
  footer,
  scroll = true,
  header,
  refreshing,
  onRefresh,
  contentStyle,
  testID,
  ...headerProps
}: ScreenProps) {
  const insets = useSafeAreaInsets()
  const hasHeader = header || headerProps.title || headerProps.back || headerProps.right
  const body = scroll ? (
    <ScrollView
      style={styles.flex}
      contentContainerStyle={[styles.content, contentStyle]}
      keyboardShouldPersistTaps="handled"
      refreshControl={
        onRefresh ? (
          <RefreshControl refreshing={refreshing ?? false} onRefresh={onRefresh} tintColor={colors.green700} />
        ) : undefined
      }
    >
      {children}
    </ScrollView>
  ) : (
    <View style={[styles.flex, styles.content, contentStyle]}>{children}</View>
  )

  return (
    <SafeAreaView style={styles.safe} edges={["top", "left", "right"]} testID={testID}>
      <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === "ios" ? "padding" : undefined}>
        {hasHeader ? (header ?? <ScreenHeader {...headerProps} />) : null}
        {body}
        {footer ? (
          <View style={[styles.footer, { paddingBottom: Math.max(insets.bottom, spacing.md) }]}>{footer}</View>
        ) : null}
      </KeyboardAvoidingView>
    </SafeAreaView>
  )
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  flex: { flex: 1 },
  header: {
    minHeight: sizes.topBar,
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xs,
    gap: spacing.xs,
  },
  back: { width: sizes.touch, height: sizes.touch, justifyContent: "center", marginLeft: -spacing.xs },
  titles: { flex: 1 },
  right: { flexDirection: "row", alignItems: "center" },
  content: { padding: spacing.md, gap: spacing.md, paddingBottom: spacing.xl },
  footer: {
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
    backgroundColor: colors.background,
    paddingHorizontal: spacing.md,
    paddingTop: spacing.sm,
    gap: spacing.xs,
  },
})
