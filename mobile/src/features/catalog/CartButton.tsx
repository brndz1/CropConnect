import { useNavigation, type NavigationProp } from "@react-navigation/native"
import { useCart } from "@/cart/CartProvider"
import { IconButton } from "@/components/IconButton"
import type { ClientStackParamList } from "@/navigation/types"

// Cart on the top bar, with the number of articles (always reachable, report table 6).
export function CartButton() {
  const { count } = useCart()
  const navigation = useNavigation<NavigationProp<ClientStackParamList>>()
  return (
    <IconButton
      icon="shopping-cart"
      label="Carrinho"
      count={count}
      onPress={() => navigation.navigate("Cart")}
      testID="cart-button"
    />
  )
}
