import { StyleSheet, View, type StyleProp, type ViewStyle } from "react-native"
import { SvgXml } from "react-native-svg"
import { colors, radii } from "@/theme/tokens"
import { illustrations, type IllustrationName } from "./illustrations"

interface IllustrationProps {
  name: IllustrationName
  size: number
  // Light green behind products, light terracotta behind baskets (table 10).
  background?: "green" | "terracotta" | "none"
  // Faded, for sold-out products.
  faded?: boolean
  style?: StyleProp<ViewStyle>
}

// Illustrations of the visual system instead of product photos (table 29). Decorative: the name is always next to it.
export function Illustration({ name, size, background = "none", faded = false, style }: IllustrationProps) {
  const svg = <SvgXml xml={illustrations[name]} width={size} height={size} opacity={faded ? 0.45 : 1} />
  if (background === "none") {
    return (
      <View style={style} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
        {svg}
      </View>
    )
  }
  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[styles.tile, { backgroundColor: background === "green" ? colors.green100 : colors.terracotta100 }, style]}
    >
      {svg}
    </View>
  )
}

const styles = StyleSheet.create({
  tile: { alignItems: "center", justifyContent: "center", borderRadius: radii.card },
})
