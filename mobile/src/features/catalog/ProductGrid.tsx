import { useNavigation, type NavigationProp } from "@react-navigation/native"
import { StyleSheet, View } from "react-native"
import type { Product } from "@/api/types"
import type { ClientStackParamList } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { ProductCard } from "./ProductCard"
import { useFarmRatings } from "./queries"
import { useAddToCart } from "./useAddToCart"

// Two-column grid of product cards (4-column grid of the visual system, each card two columns wide).
export function ProductGrid({ products }: { products: Product[] }) {
  const navigation = useNavigation<NavigationProp<ClientStackParamList>>()
  const addToCart = useAddToCart()
  const ratings = useFarmRatings(products.map((product) => product.farmId))

  const rows: Product[][] = []
  for (let index = 0; index < products.length; index += 2) rows.push(products.slice(index, index + 2))

  return (
    <View style={styles.grid}>
      {rows.map((row) => (
        <View key={row.map((product) => product.id).join()} style={styles.row}>
          {row.map((product) => (
            <ProductCard
              key={product.id}
              product={product}
              rating={ratings.get(product.farmId)}
              onPress={() => navigation.navigate("Product", { productId: product.id })}
              onAdd={() => addToCart(product)}
            />
          ))}
          {row.length === 1 ? <View style={styles.spacer} /> : null}
        </View>
      ))}
    </View>
  )
}

const styles = StyleSheet.create({
  grid: { gap: spacing.md },
  row: { flexDirection: "row", gap: spacing.md },
  spacer: { flex: 1 },
})
