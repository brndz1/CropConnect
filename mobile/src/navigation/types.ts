import type { BottomTabScreenProps } from "@react-navigation/bottom-tabs"
import type { CompositeScreenProps, NavigatorScreenParams } from "@react-navigation/native"
import type { NativeStackScreenProps } from "@react-navigation/native-stack"
import type { SensorType } from "@/api/types"

// Navigation of each profile (site map, figure 1): four bottom destinations and screens stacked on top of them.

export type AuthStackParamList = {
  Login: undefined
  Register: undefined
  ForgotPassword: { email?: string } | undefined
  CheckEmail: { email: string; reason: "verify" | "reset" }
}

export type ClientTabParamList = {
  Home: undefined
  Explore: { query?: string; filter?: string } | undefined
  Orders: undefined
  Profile: undefined
}

export type ClientStackParamList = {
  ClientTabs: NavigatorScreenParams<ClientTabParamList> | undefined
  Product: { productId: string }
  Farm: { farmId: string }
  Cart: undefined
  ReviewReservation: undefined
  ReservationConfirmed: { orderIds: string[] }
  Order: { orderId: string }
  Notifications: undefined
  PersonalData: undefined
  ChangePassword: undefined
  Privacy: undefined
}

export type ManagerTabParamList = {
  Dashboard: undefined
  ManagerOrders: undefined
  Products: undefined
  Sensors: undefined
}

export type ManagerStackParamList = {
  ManagerTabs: NavigatorScreenParams<ManagerTabParamList> | undefined
  ManagerOrder: { orderId: string }
  ProductForm: { productId?: string } | undefined
  BasketForm: { productId?: string } | undefined
  Alert: { farmId: string; alertId: string }
  SensorHistory: { farmId: string; type: SensorType; sensorId?: string }
  MyFarm: undefined
  FarmForm: { farmId?: string } | undefined
  PickupSchedules: { farmId: string }
  Notifications: undefined
  ChangePassword: undefined
}

export type AdminTabParamList = {
  Users: undefined
  AdminFarms: undefined
  Thresholds: undefined
  AdminProfile: undefined
}

export type AdminStackParamList = {
  AdminTabs: NavigatorScreenParams<AdminTabParamList> | undefined
  User: { userId: string }
  UserForm: { userId?: string } | undefined
  ChangeRole: { userId: string }
  AdminFarm: { farmId: string }
  ChangePassword: undefined
}

export type ClientScreenProps<T extends keyof ClientStackParamList> = NativeStackScreenProps<ClientStackParamList, T>
export type ClientTabProps<T extends keyof ClientTabParamList> = CompositeScreenProps<
  BottomTabScreenProps<ClientTabParamList, T>,
  NativeStackScreenProps<ClientStackParamList>
>

export type ManagerScreenProps<T extends keyof ManagerStackParamList> = NativeStackScreenProps<ManagerStackParamList, T>
export type ManagerTabProps<T extends keyof ManagerTabParamList> = CompositeScreenProps<
  BottomTabScreenProps<ManagerTabParamList, T>,
  NativeStackScreenProps<ManagerStackParamList>
>

export type AdminScreenProps<T extends keyof AdminStackParamList> = NativeStackScreenProps<AdminStackParamList, T>
export type AdminTabProps<T extends keyof AdminTabParamList> = CompositeScreenProps<
  BottomTabScreenProps<AdminTabParamList, T>,
  NativeStackScreenProps<AdminStackParamList>
>
