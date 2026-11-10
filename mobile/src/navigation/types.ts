import type { NavigatorScreenParams } from "@react-navigation/native"
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
