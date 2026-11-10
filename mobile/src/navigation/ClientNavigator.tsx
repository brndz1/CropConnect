import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { useMemo } from "react"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { useSession } from "@/auth/AuthProvider"
import { CartProvider } from "@/cart/CartProvider"
import { CartScreen } from "@/features/cart/CartScreen"
import { ReservationConfirmedScreen } from "@/features/cart/ReservationConfirmedScreen"
import { ReviewReservationScreen } from "@/features/cart/ReviewReservationScreen"
import { ExploreScreen } from "@/features/catalog/ExploreScreen"
import { FarmScreen } from "@/features/catalog/FarmScreen"
import { HomeScreen } from "@/features/catalog/HomeScreen"
import { ProductScreen } from "@/features/catalog/ProductScreen"
import { OrderDetailScreen } from "@/features/orders/OrderDetailScreen"
import { OrdersScreen } from "@/features/orders/OrdersScreen"
import { useMyOrders } from "@/features/orders/queries"
import { isOpen } from "@/lib/orders"
import { PlaceholderScreen } from "./PlaceholderScreen"
import { createTabBar, type TabMeta } from "./TabBar"
import type { ClientStackParamList, ClientTabParamList } from "./types"

const Tabs = createBottomTabNavigator<ClientTabParamList>()
const Stack = createNativeStackNavigator<ClientStackParamList>()

const tabs: Record<keyof ClientTabParamList, TabMeta> = {
  Home: { label: "Início", icon: "home" },
  Explore: { label: "Explorar", icon: "explore" },
  Orders: { label: "Encomendas", icon: "receipt-long" },
  Profile: { label: "Perfil", icon: "person" },
}

function ClientTabs() {
  // Counter of orders in progress on Encomendas (C10).
  const orders = useMyOrders()
  const open = (orders.data ?? []).filter((order) => isOpen(order.status)).length
  const TabBar = useMemo(
    () =>
      createTabBar((name) =>
        name === "Orders" ? { ...tabs.Orders, count: open } : tabs[name as keyof ClientTabParamList],
      ),
    [open],
  )
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }} tabBar={(props) => <TabBar {...props} />}>
      <Tabs.Screen name="Home" component={HomeScreen} />
      <Tabs.Screen name="Explore" component={ExploreScreen} />
      <Tabs.Screen name="Orders" component={OrdersScreen} />
      <Tabs.Screen name="Profile" component={PlaceholderScreen} />
    </Tabs.Navigator>
  )
}

// Cliente: Início, Explorar, Encomendas and Perfil; the cart and the notifications on the top bar (table 9).
export function ClientNavigator() {
  const { userId } = useSession()
  return (
    <CartProvider userId={userId}>
      <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
        <Stack.Screen name="ClientTabs" component={ClientTabs} />
        <Stack.Screen name="Product" component={ProductScreen} />
        <Stack.Screen name="Farm" component={FarmScreen} />
        <Stack.Screen name="Cart" component={CartScreen} />
        <Stack.Screen name="ReviewReservation" component={ReviewReservationScreen} />
        <Stack.Screen
          name="ReservationConfirmed"
          component={ReservationConfirmedScreen}
          options={{ gestureEnabled: false }}
        />
        <Stack.Screen name="Order" component={OrderDetailScreen} />
        <Stack.Screen name="Notifications" component={PlaceholderScreen} />
      </Stack.Navigator>
    </CartProvider>
  )
}
