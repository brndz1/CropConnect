import { StyleSheet, View } from "react-native"
import { initials } from "@/lib/format"
import { colors } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon, type IconName } from "./Icon"

interface AvatarProps {
  name?: string
  icon?: IconName
  size?: number
}

// Circle with the initials of a person, or an icon (farm, order).
export function Avatar({ name, icon, size = 48 }: AvatarProps) {
  return (
    <View
      style={[styles.circle, { width: size, height: size, borderRadius: size / 2 }]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      {icon ? (
        <Icon name={icon} size={size * 0.45} color="green900" />
      ) : (
        <AppText variant="label" color="green900" style={{ fontSize: size * 0.32, lineHeight: size * 0.42 }}>
          {initials(name ?? "")}
        </AppText>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  circle: { backgroundColor: colors.green100, alignItems: "center", justifyContent: "center" },
})
