import MaterialIcons from "@expo/vector-icons/MaterialIcons"
import type { ComponentProps } from "react"
import { colors, sizes, type ColorName } from "@/theme/tokens"

export type IconName = ComponentProps<typeof MaterialIcons>["name"]

interface IconProps {
  name: IconName
  size?: number
  color?: ColorName
}

// Material Icons, filled style, on a 24 dp grid (report section 7.5). Decorative: the label sits next to it.
export function Icon({ name, size = sizes.icon, color = "ink" }: IconProps) {
  return (
    <MaterialIcons
      name={name}
      size={size}
      color={colors[color]}
      accessibilityElementsHidden
      importantForAccessibility="no"
    />
  )
}
