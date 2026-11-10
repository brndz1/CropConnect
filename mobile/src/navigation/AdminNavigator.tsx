import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { PlaceholderScreen } from "./PlaceholderScreen"
import { createTabBar, type TabMeta } from "./TabBar"
import type { AdminStackParamList, AdminTabParamList } from "./types"

const Tabs = createBottomTabNavigator<AdminTabParamList>()
const Stack = createNativeStackNavigator<AdminStackParamList>()

const tabs: Record<keyof AdminTabParamList, TabMeta> = {
  Users: { label: "Utilizadores", icon: "person" },
  AdminFarms: { label: "Quintas", icon: "storefront" },
  Thresholds: { label: "Limites", icon: "tune" },
  AdminProfile: { label: "Perfil", icon: "shield" },
}

const TabBar = createTabBar((name) => tabs[name as keyof AdminTabParamList])

function AdminTabs() {
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }} tabBar={(props) => <TabBar {...props} />}>
      <Tabs.Screen name="Users" component={PlaceholderScreen} />
      <Tabs.Screen name="AdminFarms" component={PlaceholderScreen} />
      <Tabs.Screen name="Thresholds" component={PlaceholderScreen} />
      <Tabs.Screen name="AdminProfile" component={PlaceholderScreen} />
    </Tabs.Navigator>
  )
}

// Administrador: Utilizadores, Quintas, Limites and Perfil (table 9).
export function AdminNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <Stack.Screen name="AdminTabs" component={AdminTabs} />
    </Stack.Navigator>
  )
}
