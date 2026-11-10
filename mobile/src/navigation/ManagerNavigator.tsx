import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
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

const TabBar = createTabBar((name) => tabs[name as keyof ManagerTabParamList])

function ManagerTabs() {
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }} tabBar={(props) => <TabBar {...props} />}>
      <Tabs.Screen name="Dashboard" component={PlaceholderScreen} />
      <Tabs.Screen name="ManagerOrders" component={PlaceholderScreen} />
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
    </Stack.Navigator>
  )
}
