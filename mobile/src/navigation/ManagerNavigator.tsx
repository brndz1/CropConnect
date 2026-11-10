import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { useMemo } from "react"
import { ManagerOrderDetailScreen } from "@/features/manager/ManagerOrderDetailScreen"
import { ManagerOrdersScreen } from "@/features/manager/ManagerOrdersScreen"
import { useFarmOrders } from "@/features/orders/queries"
import { PlaceholderScreen } from "./PlaceholderScreen"
import { createTabBar, type TabMeta } from "./TabBar"
import type { ManagerStackParamList, ManagerTabParamList } from "./types"

const Tabs = createBottomTabNavigator<ManagerTabParamList>()
const Stack = createNativeStackNavigator<ManagerStackParamList>()

const tabs: Record<keyof ManagerTabParamList, TabMeta> = {
  Dashboard: { label: "Painel", icon: "dashboard" },
  ManagerOrders: { label: "Encomendas", icon: "receipt-long" },
  Products: { label: "Produtos", icon: "inventory-2" },
  Sensors: { label: "Sensores", icon: "sensors" },
}

function ManagerTabs() {
  // Counter of new orders waiting for confirmation on Encomendas (G01).
  const pending = useFarmOrders("Pending").data?.length ?? 0
  const TabBar = useMemo(
    () =>
      createTabBar((name) =>
        name === "ManagerOrders" ? { ...tabs.ManagerOrders, count: pending } : tabs[name as keyof ManagerTabParamList],
      ),
    [pending],
  )
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }} tabBar={(props) => <TabBar {...props} />}>
      <Tabs.Screen name="Dashboard" component={PlaceholderScreen} />
      <Tabs.Screen name="ManagerOrders" component={ManagerOrdersScreen} />
      <Tabs.Screen name="Products" component={PlaceholderScreen} />
      <Tabs.Screen name="Sensors" component={PlaceholderScreen} />
    </Tabs.Navigator>
  )
}

// Gestor: Painel, Encomendas, Produtos and Sensores; A minha quinta from the Painel (table 9).
export function ManagerNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <Stack.Screen name="ManagerTabs" component={ManagerTabs} />
      <Stack.Screen name="ManagerOrder" component={ManagerOrderDetailScreen} />
    </Stack.Navigator>
  )
}
