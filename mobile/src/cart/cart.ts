import type { Product, ProductType } from "@/api/types"
import { maxQuantity } from "@/lib/catalog"

// The cart can mix farms; it is grouped by farm and becomes one order per farm (report section 2.1).

export interface CartLine {
  productId: string
  name: string
  farmId: string
  farmName: string
  unit: string
  price: number
  type: ProductType
  availableUntil: string | null
  // The most this client can take: the stock, or the per-client limit of surprise baskets.
  max: number
  quantity: number
}

export interface CartGroup {
  farmId: string
  farmName: string
  lines: CartLine[]
  total: number
}

const clamp = (quantity: number, max: number) => Math.max(1, Math.min(quantity, Math.max(max, 1)))

export function addLine(lines: CartLine[], product: Product, quantity = 1): CartLine[] {
  const max = maxQuantity(product)
  if (max <= 0) return lines
  const existing = lines.find((line) => line.productId === product.id)
  if (existing) {
    return lines.map((line) =>
      line.productId === product.id
        ? { ...line, price: product.price, max, quantity: clamp(line.quantity + quantity, max) }
        : line,
    )
  }
  return [
    ...lines,
    {
      productId: product.id,
      name: product.name,
      farmId: product.farmId,
      farmName: product.farmName,
      unit: product.unit,
      price: product.price,
      type: product.type,
      availableUntil: product.availableUntil,
      max,
      quantity: clamp(quantity, max),
    },
  ]
}

export const setLineQuantity = (lines: CartLine[], productId: string, quantity: number) =>
  lines.map((line) => (line.productId === productId ? { ...line, quantity: clamp(quantity, line.max) } : line))

export const removeLine = (lines: CartLine[], productId: string) => lines.filter((line) => line.productId !== productId)

// Puts back a removed line where it was ("Anular" on the snackbar).
export function restoreLine(lines: CartLine[], line: CartLine, index: number): CartLine[] {
  if (lines.some((existing) => existing.productId === line.productId)) return lines
  const next = [...lines]
  next.splice(Math.min(index, next.length), 0, line)
  return next
}

export const removeFarm = (lines: CartLine[], farmId: string) => lines.filter((line) => line.farmId !== farmId)

// After the reservation failed for lack of stock: the line now allows only what is left (C06b).
export function adjustToStock(lines: CartLine[], productId: string, available: number): CartLine[] {
  if (available <= 0) return removeLine(lines, productId)
  return lines.map((line) =>
    line.productId === productId
      ? { ...line, max: Math.min(line.max, available), quantity: Math.min(line.quantity, available) }
      : line,
  )
}

export function groupByFarm(lines: CartLine[]): CartGroup[] {
  const groups = new Map<string, CartGroup>()
  for (const line of lines) {
    const group = groups.get(line.farmId) ?? { farmId: line.farmId, farmName: line.farmName, lines: [], total: 0 }
    group.lines.push(line)
    group.total = Math.round((group.total + line.price * line.quantity) * 100) / 100
    groups.set(line.farmId, group)
  }
  return [...groups.values()]
}

export const cartTotal = (lines: CartLine[]) =>
  Math.round(lines.reduce((sum, line) => sum + line.price * line.quantity, 0) * 100) / 100

// Number of distinct articles, shown on the cart icon.
export const cartCount = (lines: CartLine[]) => lines.length

export function parseStoredCart(raw: string | null): CartLine[] {
  if (!raw) return []
  try {
    const value: unknown = JSON.parse(raw)
    return Array.isArray(value)
      ? value.filter(
          (line): line is CartLine =>
            typeof line === "object" &&
            line !== null &&
            typeof (line as CartLine).productId === "string" &&
            typeof (line as CartLine).quantity === "number",
        )
      : []
  } catch {
    return []
  }
}
