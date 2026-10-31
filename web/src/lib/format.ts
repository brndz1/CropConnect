import type { DayOfWeek, OrderStatus, ProductType, SensorType } from "@/lib/types"

// European Portuguese formats (RNF09): 1,50 €, dd/mm/aaaa.
const currency = new Intl.NumberFormat("pt-PT", { style: "currency", currency: "EUR" })
const date = new Intl.DateTimeFormat("pt-PT", { day: "2-digit", month: "2-digit", year: "numeric" })
const dateTime = new Intl.DateTimeFormat("pt-PT", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
})
const number = new Intl.NumberFormat("pt-PT", { maximumFractionDigits: 1 })

export const formatPrice = (value: number) => currency.format(value)
export const formatDate = (value: string | Date) => date.format(new Date(value))
export const formatDateTime = (value: string | Date) => dateTime.format(new Date(value))
export const formatNumber = (value: number) => number.format(value)
export const shortId = (id: string) => `#${id.slice(0, 8).toUpperCase()}`

export const orderStatusLabel: Record<OrderStatus, string> = {
  Pending: "Pendente",
  Confirmed: "Confirmada",
  ReadyForPickup: "Pronta a levantar",
  PickedUp: "Levantada",
  Cancelled: "Cancelada",
}

export const productTypeLabel: Record<ProductType, string> = {
  Product: "Produto",
  PreDefinedBasket: "Cabaz",
  SurpriseBasket: "Cabaz surpresa",
}

export const sensorTypeLabel: Record<SensorType, string> = {
  Humidity: "Humidade",
  Temperature: "Temperatura",
}

export const dayLabel: Record<DayOfWeek, string> = {
  Monday: "Segunda-feira",
  Tuesday: "Terça-feira",
  Wednesday: "Quarta-feira",
  Thursday: "Quinta-feira",
  Friday: "Sexta-feira",
  Saturday: "Sábado",
  Sunday: "Domingo",
}

export const dayOrder: DayOfWeek[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]

export const roleLabel = { Client: "Cliente", Manager: "Gestor", Admin: "Administrador" } as const

// "09:00:00" → "09:00"
export const formatTime = (value: string) => value.slice(0, 5)
