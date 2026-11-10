import { Pressable, StyleSheet, View } from "react-native"
import type { Product } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Icon } from "@/components/Icon"
import { Illustration } from "@/components/Illustration"
import { formatQuantity, illustrationFor, isBasket, stockLevel } from "@/lib/catalog"
import { formatPrice } from "@/lib/format"
import { colors, radii, sizes, spacing } from "@/theme/tokens"
import { ProductBadge } from "./ProductBadges"
import type { Rating } from "./queries"
import { RatingText } from "./RatingText"

interface ProductCardProps {
  product: Product
  rating?: Rating | null
  onPress: () => void
  onAdd?: () => void
}

// Product card: 1:1 illustration, name, farm, price per unit and a 48 "+" button (table 12).
export function ProductCard({ product, rating, onPress, onAdd }: ProductCardProps) {
  const level = stockLevel(product)
  const soldOut = level === "soldOut"
  return (
    <View style={styles.card}>
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={`${product.name}, ${product.farmName}, ${formatPrice(product.price)} por ${product.unit}${soldOut ? ", esgotado" : ""}`}
        style={({ pressed }) => [styles.main, pressed && { backgroundColor: colors.surface }]}
        testID={`product-${product.id}`}
      >
        <View style={[styles.visual, { backgroundColor: isBasket(product) ? colors.terracotta100 : colors.green100 }]}>
          <Illustration name={illustrationFor(product)} size={88} faded={soldOut} />
          <View style={styles.badge}>
            <ProductBadge product={product} />
          </View>
        </View>
        <View style={styles.body}>
          <AppText variant="bodyBold" numberOfLines={2}>
            {product.name}
          </AppText>
          <AppText variant="caption" numberOfLines={1}>
            {product.farmName}
          </AppText>
          <RatingText rating={rating} />
          {level === "low" ? (
            <AppText variant="label" color="textSecondary">
              Restam {formatQuantity(product.stockQuantity, product.unit)}
            </AppText>
          ) : null}
          <AppText variant="number" style={styles.price}>
            {formatPrice(product.price)}
            <AppText variant="caption"> /{product.unit}</AppText>
          </AppText>
        </View>
      </Pressable>
      {onAdd && !soldOut ? (
        <Pressable
          onPress={onAdd}
          accessibilityRole="button"
          accessibilityLabel={`Adicionar ${product.name} ao carrinho`}
          style={({ pressed }) => [styles.add, pressed && { backgroundColor: colors.green900 }]}
          testID={`add-${product.id}`}
        >
          <Icon name="add" color="white" size={28} />
        </Pressable>
      ) : null}
    </View>
  )
}

const styles = StyleSheet.create({
  card: {
    flex: 1,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.card,
    overflow: "hidden",
    backgroundColor: colors.background,
  },
  main: { flex: 1 },
  visual: { aspectRatio: 1.3, alignItems: "center", justifyContent: "center" },
  badge: { position: "absolute", left: spacing.xs, bottom: spacing.xs },
  body: { padding: spacing.sm, paddingBottom: spacing.md, gap: 2, flex: 1 },
  // Leaves room for the + button, which sits on the card next to the price.
  price: { fontSize: 17, marginTop: "auto", paddingTop: spacing.xs, paddingRight: sizes.touch + spacing.xxs },
  add: {
    position: "absolute",
    right: spacing.sm,
    bottom: spacing.sm,
    width: sizes.touch,
    height: sizes.touch,
    borderRadius: sizes.touch / 2,
    backgroundColor: colors.green700,
    alignItems: "center",
    justifyContent: "center",
  },
})
