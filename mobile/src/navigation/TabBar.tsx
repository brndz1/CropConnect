import type { BottomTabBarProps } from "@react-navigation/bottom-tabs"
import { Pressable, StyleSheet, View } from "react-native"
import { useSafeAreaInsets } from "react-native-safe-area-context"
import { AppText } from "@/components/AppText"
import { Icon, type IconName } from "@/components/Icon"
import { CountBadge } from "@/components/IconButton"
import { colors, fonts, sizes, spacing } from "@/theme/tokens"

export interface TabMeta {
  label: string
  icon: IconName
  // Counter on the destination (open orders, section 20.5).
  count?: number
}

// Bottom navigation of the prototype: icon with label, a 40 × 40 circle behind the active icon and counters.
export function createTabBar(meta: (routeName: string) => TabMeta) {
  return function TabBar({ state, navigation }: BottomTabBarProps) {
    const insets = useSafeAreaInsets()
    return (
      <View style={[styles.bar, { paddingBottom: Math.max(insets.bottom, spacing.xs) }]} accessibilityRole="tablist">
        {state.routes.map((route, index) => {
          const focused = state.index === index
          const { label, icon, count } = meta(route.name)
          const onPress = () => {
            const event = navigation.emit({ type: "tabPress", target: route.key, canPreventDefault: true })
            if (!focused && !event.defaultPrevented) navigation.navigate(route.name, route.params)
          }
          return (
            <Pressable
              key={route.key}
              onPress={onPress}
              accessibilityRole="tab"
              accessibilityState={{ selected: focused }}
              accessibilityLabel={count ? `${label}, ${count}` : label}
              style={styles.tab}
              testID={`tab-${route.name}`}
            >
              <View style={[styles.indicator, focused && styles.indicatorActive]}>
                <Icon name={icon} color={focused ? "green900" : "textSecondary"} />
                {count ? <CountBadge count={count} /> : null}
              </View>
              <AppText
                style={[styles.label, { fontFamily: focused ? fonts.headingSemiBold : fonts.headingRegular }]}
                color={focused ? "green900" : "textSecondary"}
                numberOfLines={1}
              >
                {label}
              </AppText>
            </Pressable>
          )
        })}
      </View>
    )
  }
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: "row",
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
    backgroundColor: colors.background,
    paddingTop: spacing.xxs,
  },
  tab: { flex: 1, alignItems: "center", minHeight: sizes.bottomBar, justifyContent: "center", gap: 2 },
  indicator: {
    width: sizes.navIndicator,
    height: sizes.navIndicator,
    borderRadius: sizes.navIndicator / 2,
    alignItems: "center",
    justifyContent: "center",
  },
  indicatorActive: { backgroundColor: colors.green100 },
  label: { fontSize: 12, lineHeight: 16 },
})
