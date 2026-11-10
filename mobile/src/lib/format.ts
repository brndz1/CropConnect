// European Portuguese formats (RNF09): 1,50 €, dd/mm/aaaa, days of the week in Portuguese.
import type { DayOfWeek, OrderStatus, ProductType, Role, SensorType } from "@/api/types"

const currency = new Intl.NumberFormat("pt-PT", { style: "currency", currency: "EUR" })
const number = new Intl.NumberFormat("pt-PT", { maximumFractionDigits: 1 })

const pad = (value: number) => String(value).padStart(2, "0")

export const formatPrice = (value: number) => currency.format(value)
export const formatNumber = (value: number) => number.format(value)

export function formatDate(value: string | Date) {
  const date = new Date(value)
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}/${date.getFullYear()}`
}

export function formatShortDate(value: string | Date) {
  const date = new Date(value)
  return `${pad(date.getDate())}/${pad(date.getMonth() + 1)}`
}

export function formatClock(value: string | Date) {
  const date = new Date(value)
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export const formatDateTime = (value: string | Date) => `${formatDate(value)}, ${formatClock(value)}`

// "09:00:00" → "09:00"
export const formatTime = (value: string) => value.slice(0, 5)

// Orders are identified by a GUID; the screens show its first characters, like the web app (#3F2A9C1B).
export const shortId = (id: string) => `#${id.slice(0, 8).toUpperCase()}`

// "há 5 min", "há 3 h", "ontem", or the date.
export function formatRelative(value: string | Date, now: Date = new Date()) {
  const date = new Date(value)
  const minutes = Math.floor((now.getTime() - date.getTime()) / 60_000)
  if (minutes < 1) return "agora"
  if (minutes < 60) return `há ${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24 && date.getDate() === now.getDate()) return `há ${hours} h`
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (date.toDateString() === yesterday.toDateString()) return "ontem"
  return formatDate(date)
}

export const orderStatusLabel: Record<OrderStatus, string> = {
  Pending: "Pendente",
  Confirmed: "Confirmada",
  ReadyForPickup: "Pronta a levantar",
  PickedUp: "Levantada",
  Cancelled: "Cancelada",
}

export const productTypeLabel: Record<ProductType, string> = {
  Product: "Produto",
  PreDefinedBasket: "Pré-definido",
  SurpriseBasket: "Surpresa",
}

export const sensorTypeLabel: Record<SensorType, string> = {
  Humidity: "Humidade",
  Temperature: "Temperatura",
}

export const roleLabel: Record<Role, string> = { Client: "Cliente", Manager: "Gestor", Admin: "Administrador" }

export const dayOrder: DayOfWeek[] = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]

export const dayLabel: Record<DayOfWeek, string> = {
  Monday: "Segunda-feira",
  Tuesday: "Terça-feira",
  Wednesday: "Quarta-feira",
  Thursday: "Quinta-feira",
  Friday: "Sexta-feira",
  Saturday: "Sábado",
  Sunday: "Domingo",
}

export const dayShort: Record<DayOfWeek, string> = {
  Monday: "Seg",
  Tuesday: "Ter",
  Wednesday: "Qua",
  Thursday: "Qui",
  Friday: "Sex",
  Saturday: "Sáb",
  Sunday: "Dom",
}

// "1 artigo", "2 artigos"
export const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`

export function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const first = parts[0]?.[0] ?? ""
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? "") : ""
  return (first + last).toUpperCase() || "?"
}
