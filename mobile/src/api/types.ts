// Friendly names for the shapes generated from the OpenAPI document (schema.d.ts).
import type { components } from "./schema"

type Schemas = components["schemas"]

export type Role = "Client" | "Manager" | "Admin"
export type OrderStatus = Schemas["OrderStatus"]
export type ProductType = Schemas["ProductType"]
export type SensorType = Schemas["SensorType"]
export type DayOfWeek = Schemas["DayOfWeek"]
export type NotificationType = Schemas["NotificationType"]

export type TokenPair = Schemas["TokenResponse"]
export type UserAccount = Omit<Schemas["UserAccountResponse"], "role"> & { role: Role }
export type Profile = Schemas["ProfileResponse"]
export type Farm = Schemas["FarmResponse"]
export type PickupSchedule = Schemas["PickupScheduleResponse"]
export type Product = Schemas["ProductResponse"]
export type Order = Schemas["OrderResponse"]
export type OrderItem = Schemas["OrderItemResponse"]
export type Review = Schemas["ReviewResponse"]
export type AppNotification = Schemas["NotificationResponse"]
export type NotificationPage = Schemas["NotificationPageResponse"]
export type Reading = Schemas["ReadingResponse"]
export type Alert = Schemas["AlertResponse"]
export type Threshold = Schemas["ThresholdResponse"]

export interface Paged<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}
