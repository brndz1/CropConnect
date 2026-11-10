import { StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { isExpired, sortForCatalog } from "@/lib/catalog"
import { formatDate } from "@/lib/format"
import { nextPickupLabel, summarizeSchedules } from "@/lib/schedule"
import type { ClientScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { FarmMap } from "./FarmMap"
import { InfoRow } from "./InfoRow"
import { ProductGrid } from "./ProductGrid"
import { ratingOf, useFarm, useFarmReviews, useProducts } from "./queries"
import { RatingText } from "./RatingText"

// C08 Detalhe da quinta: location, pickup schedule, products and the reviews of other clients (RF05, RF15).
export function FarmScreen({ route }: ClientScreenProps<"Farm">) {
  const { farmId } = route.params
  const farm = useFarm(farmId)
  const products = useProducts({ farmId })
  const reviews = useFarmReviews(farmId)

  if (farm.isPending) {
    return (
      <Screen back="back">
        <LoadingState rows={3} height={140} />
      </Screen>
    )
  }
  if (farm.isError) {
    return (
      <Screen back="back">
        <ErrorState error={farm.error} onRetry={() => void farm.refetch()} />
      </Screen>
    )
  }

  const data = farm.data
  const rating = reviews.data ? ratingOf(reviews.data.items, reviews.data.totalCount) : null
  const items = sortForCatalog((products.data ?? []).filter((product) => !isExpired(product)))

  return (
    <Screen back="back" contentStyle={styles.content} testID="farm-screen">
      <FarmMap location={data.location} latitude={data.latitude} longitude={data.longitude} />
      <View style={styles.body}>
        <View style={styles.title}>
          <AppText variant="h2" accessibilityRole="header">
            {data.name}
          </AppText>
          <RatingText rating={rating} suffix="avaliações" />
          {data.description ? <AppText color="textSecondary">{data.description}</AppText> : null}
        </View>

        <Card title="Levantamento e localização">
          <InfoRow icon="place" title={data.location} subtitle="Toque no mapa para abrir" />
          <InfoRow
            icon="schedule"
            title={summarizeSchedules(data.pickupSchedules)}
            subtitle={nextPickupLabel(data.pickupSchedules)}
          />
          <InfoRow icon="euro" title="Pagamento em mão no levantamento" />
        </Card>

        <AppText variant="h3" accessibilityRole="header">
          Produtos da quinta
        </AppText>
        {products.isPending ? (
          <LoadingState rows={2} height={180} />
        ) : items.length > 0 ? (
          <ProductGrid products={items} />
        ) : (
          <EmptyState title="Sem produtos de momento" message="Esta quinta ainda não publicou produtos." />
        )}

        <AppText variant="h3" accessibilityRole="header">
          Avaliações
        </AppText>
        {reviews.data && reviews.data.items.length > 0 ? (
          reviews.data.items.slice(0, 5).map((review) => (
            <Card key={review.id}>
              <View style={styles.stars} accessibilityLabel={`${review.rating} em 5 estrelas`}>
                {Array.from({ length: 5 }, (_, index) => (
                  <Icon key={index} name={index < review.rating ? "star" : "star-border"} size={18} color="warning" />
                ))}
                <AppText variant="caption">{review.createdAt ? formatDate(review.createdAt) : ""}</AppText>
              </View>
              {review.comment ? <AppText>{review.comment}</AppText> : null}
            </Card>
          ))
        ) : (
          <AppText color="textSecondary">Ainda sem avaliações. As avaliações surgem depois dos levantamentos.</AppText>
        )}
      </View>
    </Screen>
  )
}

const styles = StyleSheet.create({
  content: { padding: 0, gap: 0 },
  body: { padding: spacing.md, gap: spacing.md },
  title: { gap: spacing.xxs },
  stars: { flexDirection: "row", alignItems: "center", gap: 2 },
})
