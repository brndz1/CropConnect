import { StyleSheet, View } from "react-native"
import type { Farm } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { plural } from "@/lib/format"
import { summarizeSchedules } from "@/lib/schedule"
import { spacing } from "@/theme/tokens"
import type { Rating } from "./queries"
import { RatingText } from "./RatingText"

interface FarmCardProps {
  farm: Farm
  rating?: Rating | null
  availableProducts?: number
  onPress: () => void
}

// Farm card of Explorar > Quintas (C03q).
export function FarmCard({ farm, rating, availableProducts, onPress }: FarmCardProps) {
  return (
    <Card onPress={onPress} accessibilityLabel={`${farm.name}, ${farm.location}`} testID={`farm-${farm.id}`}>
      <View style={styles.row}>
        <Avatar icon="storefront" />
        <View style={styles.text}>
          <AppText variant="bodyBold">{farm.name}</AppText>
          <RatingText rating={rating} />
          <AppText variant="caption">{summarizeSchedules(farm.pickupSchedules)}</AppText>
          {availableProducts !== undefined ? (
            <AppText variant="label" color="green700">
              {plural(availableProducts, "produto disponível", "produtos disponíveis")}
            </AppText>
          ) : null}
        </View>
        <Icon name="chevron-right" color="textSecondary" />
      </View>
    </Card>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  text: { flex: 1, gap: 2 },
})
