import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
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

const TabBar = createTabBar((name) => tabs[name as keyof ClientTabParamList])

function ClientTabs() {
  return (
    <Tabs.Navigator screenOptions={{ headerShown: false }} tabBar={(props) => <TabBar {...props} />}>
      <Tabs.Screen name="Home" component={PlaceholderScreen} />
      <Tabs.Screen name="Explore" component={PlaceholderScreen} />
      <Tabs.Screen name="Orders" component={PlaceholderScreen} />
      <Tabs.Screen name="Profile" component={PlaceholderScreen} />
    </Tabs.Navigator>
  )
}

// Cliente: Início, Explorar, Encomendas and Perfil; the cart and the notifications on the top bar (table 9).
export function ClientNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <Stack.Screen name="ClientTabs" component={ClientTabs} />
    </Stack.Navigator>
  )
}
