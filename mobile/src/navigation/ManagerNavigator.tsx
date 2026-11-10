import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { useMemo } from "react"
import { BasketFormScreen } from "@/features/manager/BasketFormScreen"
import { DashboardScreen } from "@/features/manager/DashboardScreen"
import { FarmFormScreen } from "@/features/manager/FarmFormScreen"
import { ManagerOrderDetailScreen } from "@/features/manager/ManagerOrderDetailScreen"
import { ManagerOrdersScreen } from "@/features/manager/ManagerOrdersScreen"
import { MyFarmScreen } from "@/features/manager/MyFarmScreen"
import { PickupSchedulesScreen } from "@/features/manager/PickupSchedulesScreen"
import { ProductFormScreen } from "@/features/manager/ProductFormScreen"
import { ProductsScreen } from "@/features/manager/ProductsScreen"
import { useFarmOrders } from "@/features/orders/queries"
import { AlertScreen } from "@/features/sensors/AlertScreen"
import { SensorHistoryScreen } from "@/features/sensors/SensorHistoryScreen"
import { SensorsScreen } from "@/features/sensors/SensorsScreen"
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
      <Tabs.Screen name="Dashboard" component={DashboardScreen} />
      <Tabs.Screen name="ManagerOrders" component={ManagerOrdersScreen} />
      <Tabs.Screen name="Products" component={ProductsScreen} />
      <Tabs.Screen name="Sensors" component={SensorsScreen} />
    </Tabs.Navigator>
  )
}

// Gestor: Painel, Encomendas, Produtos and Sensores; A minha quinta from the Painel (table 9).
export function ManagerNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <Stack.Screen name="ManagerTabs" component={ManagerTabs} />
      <Stack.Screen name="ManagerOrder" component={ManagerOrderDetailScreen} />
      <Stack.Screen name="Alert" component={AlertScreen} />
      <Stack.Screen name="SensorHistory" component={SensorHistoryScreen} />
      <Stack.Screen name="ProductForm" component={ProductFormScreen} />
      <Stack.Screen name="BasketForm" component={BasketFormScreen} />
      <Stack.Screen name="MyFarm" component={MyFarmScreen} />
      <Stack.Screen name="FarmForm" component={FarmFormScreen} />
      <Stack.Screen name="PickupSchedules" component={PickupSchedulesScreen} />
      <Stack.Screen name="ChangePassword" component={PlaceholderScreen} />
      <Stack.Screen name="Notifications" component={PlaceholderScreen} />
    </Stack.Navigator>
  )
}
