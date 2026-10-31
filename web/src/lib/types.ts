// Shapes of the gateway's REST API (docs/api.md, docs/openapi.json).

export type Role = "Client" | "Manager" | "Admin"

export interface Paged<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface UserAccount {
  id: string
  username: string
  email: string
  role: Role
  isActive: boolean
}

export interface Profile {
  id: string
  firstName: string
  lastName: string
  phoneNumber: string
}

export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday"

export interface PickupSchedule {
  id: string
  farmId: string
  dayOfWeek: DayOfWeek
  openTime: string
  closeTime: string
  version: number
}

export interface Farm {
  id: string
  ownerId: string
  name: string
  description: string
  location: string
  latitude: number | null
  longitude: number | null
  isActive: boolean
  pickupSchedules: PickupSchedule[]
  version: number
}

export type ProductType = "Product" | "PreDefinedBasket" | "SurpriseBasket"

export interface Product {
  id: string
  farmId: string
  farmName: string
  name: string
  description: string
  unit: string
  price: number
  stockQuantity: number
  isAvailable: boolean
  type: ProductType
  availableUntil: string | null
  contents: string | null
  version: number
}

export type OrderStatus = "Pending" | "Confirmed" | "ReadyForPickup" | "PickedUp" | "Cancelled"

export interface OrderItem {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  totalPrice: number
}

export interface Order {
  id: string
  customerId: string
  farmId: string
  status: OrderStatus
  totalAmount: number
  items: OrderItem[]
  notes: string | null
  cancellationReason: string | null
  createdAt: string
  updatedAt: string | null
  version: number
}

export interface Review {
  id: string
  orderId: string
  customerId: string
  farmId: string
  rating: number
  comment: string
  createdAt: string
}

export type NotificationType = "OrderCreated" | "OrderStatusChanged" | "OrderCancelled" | "SensorAlert"

export interface AppNotification {
  id: string
  type: NotificationType
  title: string
  message: string
  relatedEntityId: string
  farmId: string
  createdAt: string
  readAt: string | null
}

export interface NotificationPage extends Paged<AppNotification> {
  unreadCount: number
}

export type SensorType = "Humidity" | "Temperature"

export interface Reading {
  id: string
  farmId: string
  sensorId: string
  type: SensorType
  value: number
  unit: string
  timestamp: string
}

export interface Alert {
  id: string
  farmId: string
  type: SensorType
  value: number
  minValue: number
  maxValue: number
  unit: string
  description: string
  weatherContext: string | null
  createdAt: string
  acknowledgedAt: string | null
}

export interface Threshold {
  type: SensorType
  minValue: number
  maxValue: number
  unit: string
  updatedAt: string | null
  version: number
}
