import { StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { Icon } from "@/components/Icon"
import { formatNumber } from "@/lib/format"
import { spacing } from "@/theme/tokens"
import type { Rating } from "./queries"

// "★ 4,8 (32)"; nothing while the farm has no reviews.
export function RatingText({ rating, suffix }: { rating: Rating | null | undefined; suffix?: string }) {
  if (!rating) return null
  const text = `${formatNumber(rating.average)} (${rating.count})${suffix ? ` ${suffix}` : ""}`
  return (
    <View style={styles.row} accessibilityLabel={`Avaliação ${formatNumber(rating.average)} em 5, ${rating.count}`}>
      <Icon name="star" size={16} color="warning" />
      <AppText variant="caption">{text}</AppText>
    </View>
  )
}

const styles = StyleSheet.create({ row: { flexDirection: "row", alignItems: "center", gap: spacing.xxs } })
