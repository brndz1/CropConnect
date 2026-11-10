import { useState } from "react"
import { Pressable, StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { Illustration } from "@/components/Illustration"
import { QuantityStepper } from "@/components/QuantityStepper"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useCart } from "@/cart/CartProvider"
import {
  formatQuantity,
  illustrationFor,
  isBasket,
  isExpired,
  maxQuantity,
  stockLevel,
  SURPRISE_BASKET_LIMIT,
} from "@/lib/catalog"
import { dayLabel, formatDate, formatPrice } from "@/lib/format"
import { nextPickupLabel, summarizeSchedules } from "@/lib/schedule"
import type { ClientScreenProps } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"
import { InfoRow } from "./InfoRow"
import { ProductBadge } from "./ProductBadges"
import { ratingOf, useFarm, useFarmReviews, useProduct } from "./queries"
import { RatingText } from "./RatingText"
import { useAddToCart } from "./useAddToCart"

const weekday = (value: string) => {
  const day = new Date(value).getDay()
  return (["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const)[day]
}

// C04 Detalhe do produto and C05 Detalhe do cabaz: price, stock, pickup at the farm and the quantity to add (RF05).
export function ProductScreen({ navigation, route }: ClientScreenProps<"Product">) {
  const product = useProduct(route.params.productId)
  const farm = useFarm(product.data?.farmId)
  const reviews = useFarmReviews(product.data?.farmId)
  const { lines } = useCart()
  const addToCart = useAddToCart()
  const [quantity, setQuantity] = useState(1)

  if (product.isPending) {
    return (
      <Screen back="back">
        <LoadingState rows={3} height={160} />
      </Screen>
    )
  }
  if (product.isError) {
    return (
      <Screen back="back">
        <ErrorState error={product.error} onRetry={() => void product.refetch()} />
      </Screen>
    )
  }

  const item = product.data
  const basket = isBasket(item)
  const inCart = lines.find((line) => line.productId === item.id)?.quantity ?? 0
  const limit = Math.max(0, maxQuantity(item) - inCart)
  const unavailable = stockLevel(item) === "soldOut" || isExpired(item) || limit === 0
  const schedules = farm.data?.pickupSchedules ?? []
  const rating = reviews.data ? ratingOf(reviews.data.items, reviews.data.totalCount) : null
  const day = item.availableUntil ? weekday(item.availableUntil) : undefined

  const footer = (
    <View style={styles.footer}>
      <QuantityStepper
        value={Math.min(quantity, Math.max(limit, 1))}
        onChange={setQuantity}
        max={Math.max(limit, 1)}
        unit={basket ? undefined : item.unit}
        label={item.name}
      />
      <Button
        label={unavailable ? "Indisponível" : `Adicionar - ${formatPrice(item.price * Math.min(quantity, limit))}`}
        disabled={unavailable}
        onPress={() => {
          addToCart(item, Math.min(quantity, limit))
          navigation.goBack()
        }}
        style={styles.add}
        testID="product-add"
      />
    </View>
  )

  return (
    <Screen back="back" footer={footer} contentStyle={styles.content} testID="product-screen">
      <View style={[styles.hero, { backgroundColor: basket ? colors.terracotta100 : colors.green100 }]}>
        <Illustration name={illustrationFor(item)} size={160} faded={stockLevel(item) === "soldOut"} />
      </View>
      <View style={styles.body}>
        <ProductBadge product={item} detailed />
        <AppText variant="h1" accessibilityRole="header">
          {item.name}
        </AppText>

        <Pressable
          onPress={() => navigation.navigate("Farm", { farmId: item.farmId })}
          accessibilityRole="link"
          accessibilityLabel={`Ver a quinta ${item.farmName}`}
          style={styles.farm}
        >
          <Icon name="storefront" color="green700" />
          <AppText variant="bodyBold" color="green700">
            {item.farmName}
          </AppText>
          <RatingText rating={rating} />
          <Icon name="chevron-right" color="green700" />
        </Pressable>

        <AppText variant="h2" color={basket ? "terracotta600" : "ink"}>
          {formatPrice(item.price)} {basket ? null : <AppText color="textSecondary">por {item.unit}</AppText>}
        </AppText>

        {item.type === "SurpriseBasket" ? (
          <Banner
            tone="info"
            title="Conteúdo revelado no levantamento"
            message={`${item.description || "O produtor escolhe produtos da época perto do fim do prazo."} Limite de ${SURPRISE_BASKET_LIMIT} cabaz surpresa por cliente.`}
          />
        ) : item.description ? (
          <AppText color="textSecondary">{item.description}</AppText>
        ) : null}

        {item.type === "PreDefinedBasket" && item.contents ? (
          <Card title="O que leva">
            {item.contents
              .split(/[,;\n]/)
              .map((entry) => entry.trim())
              .filter(Boolean)
              .map((entry) => (
                <InfoRow key={entry} icon="check" title={entry} />
              ))}
          </Card>
        ) : null}

        {inCart > 0 ? (
          <Banner
            tone="info"
            message={`Já tem ${basket ? inCart : formatQuantity(inCart, item.unit)} no carrinho.`}
            action={{ label: "Ver carrinho", onPress: () => navigation.navigate("Cart") }}
          />
        ) : null}

        <Card title="Levantamento na quinta">
          {item.availableUntil ? (
            <InfoRow
              icon="event"
              title={`Levantar até ${formatDate(item.availableUntil)}`}
              subtitle={day ? dayLabel[day] : null}
            />
          ) : null}
          <InfoRow icon="schedule" title={summarizeSchedules(schedules)} subtitle={nextPickupLabel(schedules)} />
          {farm.data ? <InfoRow icon="place" title={farm.data.location} subtitle={farm.data.name} /> : null}
          <InfoRow icon="euro" title="Pagamento no levantamento" subtitle="Em mão, diretamente ao produtor" />
        </Card>
      </View>
    </Screen>
  )
}

const styles = StyleSheet.create({
  content: { padding: 0, gap: 0 },
  hero: { height: 220, alignItems: "center", justifyContent: "center" },
  body: { padding: spacing.md, gap: spacing.md },
  farm: { flexDirection: "row", alignItems: "center", gap: spacing.xs, minHeight: 48, flexWrap: "wrap" },
  footer: { flexDirection: "row", alignItems: "center", gap: spacing.sm },
  add: { flex: 1 },
})
