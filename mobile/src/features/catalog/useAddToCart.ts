import { useNavigation, type NavigationProp } from "@react-navigation/native"
import { useCallback } from "react"
import type { Product } from "@/api/types"
import { useCart } from "@/cart/CartProvider"
import { useSnackbar } from "@/components/Snackbar"
import { isBasket } from "@/lib/catalog"
import type { ClientStackParamList } from "@/navigation/types"

// Adds to the cart of the product's farm and confirms with a snackbar that leads to the cart (table 26).
export function useAddToCart() {
  const { add } = useCart()
  const snackbar = useSnackbar()
  const navigation = useNavigation<NavigationProp<ClientStackParamList>>()

  return useCallback(
    (product: Product, quantity = 1) => {
      add(product, quantity)
      snackbar.show({
        message: isBasket(product) ? "Cabaz adicionado ao carrinho." : "Produto adicionado ao carrinho.",
        action: { label: "Ver carrinho", onPress: () => navigation.navigate("Cart") },
      })
    },
    [add, snackbar, navigation],
  )
}
