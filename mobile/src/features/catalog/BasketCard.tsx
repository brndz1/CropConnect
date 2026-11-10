import { Pressable, StyleSheet, View } from "react-native"
import type { Product } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Icon } from "@/components/Icon"
import { Illustration } from "@/components/Illustration"
import { illustrationFor } from "@/lib/catalog"
import { formatDate, formatPrice } from "@/lib/format"
import { colors, radii, spacing } from "@/theme/tokens"
import { ProductBadge } from "./ProductBadges"

// "Cabazes da semana" card on Início (C02): illustration on light terracotta, kind, farm, pickup limit and price.
export function BasketCard({ product, onPress }: { product: Product; onPress: () => void }) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${product.name}, ${product.farmName}, ${formatPrice(product.price)}`}
      style={({ pressed }) => [styles.card, pressed && { backgroundColor: colors.surface }]}
      testID={`basket-${product.id}`}
    >
      <View style={styles.visual}>
        <Illustration name={illustrationFor(product)} size={72} />
      </View>
      <View style={styles.body}>
        <ProductBadge product={product} />
        <AppText variant="bodyBold" numberOfLines={2}>
          {product.name}
        </AppText>
        <AppText variant="caption">{product.farmName}</AppText>
        {product.availableUntil ? (
          <View style={styles.row}>
            <Icon name="event" size={16} color="textSecondary" />
            <AppText variant="caption" color="ink">
              Levantar até {formatDate(product.availableUntil)}
            </AppText>
          </View>
        ) : null}
        <View style={styles.row}>
          <AppText variant="number" color="terracotta600">
            {formatPrice(product.price)}
          </AppText>
          {product.type === "PreDefinedBasket" && product.contents ? (
            <AppText variant="caption" numberOfLines={1} style={styles.flex}>
              {product.contents}
            </AppText>
          ) : null}
        </View>
      </View>
    </Pressable>
  )
}

const styles = StyleSheet.create({
  card: {
    flexDirection: "row",
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.card,
    overflow: "hidden",
    backgroundColor: colors.background,
  },
  visual: { width: 112, backgroundColor: colors.terracotta100, alignItems: "center", justifyContent: "center" },
  body: { flex: 1, padding: spacing.sm, gap: spacing.xxs },
  row: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
  flex: { flex: 1 },
})
