import { StyleSheet, Text, type TextProps } from "react-native"
import { colors, typography, type ColorName, type TypographyVariant } from "@/theme/tokens"

export interface AppTextProps extends TextProps {
  variant?: TypographyVariant
  color?: ColorName
  align?: "left" | "center" | "right"
}

// Text in the type scale of the visual system. Sizes follow the system font size (RNF03).
export function AppText({ variant = "body", color, align, style, ...props }: AppTextProps) {
  return (
    <Text
      maxFontSizeMultiplier={1.5}
      style={[typography[variant], color && { color: colors[color] }, align && { textAlign: align }, style]}
      {...props}
    />
  )
}

export const textStyles = StyleSheet.create({
  strike: { textDecorationLine: "line-through" },
})
