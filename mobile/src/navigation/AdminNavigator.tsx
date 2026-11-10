import { createBottomTabNavigator } from "@react-navigation/bottom-tabs"
import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { AdminFarmScreen } from "@/features/admin/AdminFarmScreen"
import { AdminFarmsScreen } from "@/features/admin/AdminFarmsScreen"
import { AdminProfileScreen } from "@/features/admin/AdminProfileScreen"
import { ChangeRoleScreen } from "@/features/admin/ChangeRoleScreen"
import { ThresholdsScreen } from "@/features/admin/ThresholdsScreen"
import { UserDetailScreen } from "@/features/admin/UserDetailScreen"
import { UserFormScreen } from "@/features/admin/UserFormScreen"
import { UsersScreen } from "@/features/admin/UsersScreen"
import { ChangePasswordScreen } from "@/features/profile/ChangePasswordScreen"
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
      <Tabs.Screen name="Users" component={UsersScreen} />
      <Tabs.Screen name="AdminFarms" component={AdminFarmsScreen} />
      <Tabs.Screen name="Thresholds" component={ThresholdsScreen} />
      <Tabs.Screen name="AdminProfile" component={AdminProfileScreen} />
    </Tabs.Navigator>
  )
}

// Administrador: Utilizadores, Quintas, Limites and Perfil (table 9).
export function AdminNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <Stack.Screen name="AdminTabs" component={AdminTabs} />
      <Stack.Screen name="User" component={UserDetailScreen} />
      <Stack.Screen name="UserForm" component={UserFormScreen} />
      <Stack.Screen name="ChangeRole" component={ChangeRoleScreen} />
      <Stack.Screen name="AdminFarm" component={AdminFarmScreen} />
      <Stack.Screen name="ChangePassword" component={ChangePasswordScreen} />
    </Stack.Navigator>
  )
}
