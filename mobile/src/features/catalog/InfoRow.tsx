import type { ReactNode } from "react"
import { StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { Icon, type IconName } from "@/components/Icon"
import { spacing } from "@/theme/tokens"

interface InfoRowProps {
  icon: IconName
  title: string
  subtitle?: string | null
  right?: ReactNode
}

// Line of a details card: icon, text and an optional caption ("Sáb e dom - 09:00-13:00 / Próximo: sáb 31/10").
export function InfoRow({ icon, title, subtitle, right }: InfoRowProps) {
  return (
    <View style={styles.row} accessible accessibilityLabel={subtitle ? `${title}, ${subtitle}` : title}>
      <Icon name={icon} color="textSecondary" />
      <View style={styles.text}>
        <AppText>{title}</AppText>
        {subtitle ? <AppText variant="caption">{subtitle}</AppText> : null}
      </View>
      {right}
    </View>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md, alignItems: "flex-start" },
  text: { flex: 1 },
})
