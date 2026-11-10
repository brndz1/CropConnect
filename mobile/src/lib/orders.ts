import type { Order, OrderStatus } from "@/api/types"

// The API stores only free notes with an order. The app writes the pickup slot the client chose and the client's
// name there, so the farm sees both (the API has no pickup slot field, and managers cannot read client profiles).

const SLOT_PREFIX = "Levantamento: "
const CUSTOMER_PREFIX = "Cliente: "

export function buildOrderNotes(slot: string | null, customer: string | null) {
  const lines = [slot ? `${SLOT_PREFIX}${slot}` : null, customer ? `${CUSTOMER_PREFIX}${customer}` : null]
  const text = lines.filter(Boolean).join("\n")
  return text || null
}

export function parseOrderNotes(notes: string | null | undefined) {
  let slot: string | null = null
  let customer: string | null = null
  const other: string[] = []
  for (const line of (notes ?? "").split("\n").map((value) => value.trim())) {
    if (!line) continue
    if (line.startsWith(SLOT_PREFIX)) slot = line.slice(SLOT_PREFIX.length)
    else if (line.startsWith(CUSTOMER_PREFIX)) customer = line.slice(CUSTOMER_PREFIX.length)
    else other.push(line)
  }
  return { slot, customer, other: other.join("\n") || null }
}

// The order of the life cycle (report section 2.1); Cancelled is outside it.
export const statusSteps: OrderStatus[] = ["Pending", "Confirmed", "ReadyForPickup", "PickedUp"]

export const stepOf = (status: OrderStatus) => statusSteps.indexOf(status) + 1

export const isOpen = (status: OrderStatus) => status !== "PickedUp" && status !== "Cancelled"

// The client can cancel until the order is ready for pickup (RF14).
export const clientCanCancel = (status: OrderStatus) => status === "Pending" || status === "Confirmed"

// Ativas first, newest first; cancelled orders go to Anteriores (P2 of the heuristic analysis).
export function splitOrders(orders: Order[]) {
  const byDate = (a: Order, b: Order) => (b.createdAt ?? "").localeCompare(a.createdAt ?? "")
  const sorted = [...orders].sort(byDate)
  return {
    active: sorted.filter((order) => isOpen(order.status)),
    past: sorted.filter((order) => !isOpen(order.status)),
  }
}

// The transition the farm makes next, if it is the farm's to make. Pickup is confirmed by the client in the API.
export function nextManagerStatus(status: OrderStatus): "Confirmed" | "ReadyForPickup" | null {
  if (status === "Pending") return "Confirmed"
  if (status === "Confirmed") return "ReadyForPickup"
  return null
}

// "Insufficient stock for product 'Tomate-cherry'." → "Tomate-cherry"
export function productInStockError(detail: string | undefined) {
  const match = /stock for product '([^']+)'/i.exec(detail ?? "")
  return match?.[1] ?? null
}
