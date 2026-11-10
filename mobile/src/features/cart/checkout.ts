import { isApiError } from "@/api/errors"
import type { Order } from "@/api/types"
import type { CartGroup } from "@/cart/cart"
import { buildOrderNotes, productInStockError } from "@/lib/orders"

// What the reservation needs from the API, injected so the flow can be tested without a server.
export interface CheckoutApi {
  // Current stock of a product.
  stockOf: (productId: string) => Promise<number>
  createOrder: (body: { items: { productId: string; quantity: number }[]; notes: string | null }) => Promise<Order>
}

export interface Shortage {
  farmId: string
  farmName: string
  productId: string
  productName: string
  unit: string
  requested: number
  available: number
}

export interface CheckoutResult {
  // One order per farm that went through (report section 2.1).
  orders: Order[]
  // The farms whose order was created, to take them out of the cart.
  placedFarmIds: string[]
  // The first article without enough stock; its farm and the ones after it were not ordered (C07E).
  shortage: Shortage | null
  // Any other failure (network, server); the farms before it were ordered.
  error: unknown
}

async function findShortage(groups: CartGroup[], api: CheckoutApi): Promise<Shortage | null> {
  for (const group of groups) {
    for (const line of group.lines) {
      const available = await api.stockOf(line.productId)
      if (available < line.quantity) {
        return {
          farmId: group.farmId,
          farmName: group.farmName,
          productId: line.productId,
          productName: line.name,
          unit: line.unit,
          requested: line.quantity,
          available,
        }
      }
    }
  }
  return null
}

// Reserves the cart: checks the stock first, so a shortage is reported before any order is created, then creates
// one order per farm with the chosen pickup slot. If the stock runs out in between, the API refuses that order
// (422) and the shortage is reported with what is left.
export async function placeOrders(
  groups: CartGroup[],
  slots: Record<string, string | undefined>,
  customer: string | null,
  api: CheckoutApi,
): Promise<CheckoutResult> {
  const result: CheckoutResult = {
    orders: [],
    placedFarmIds: [],
    shortage: await findShortage(groups, api),
    error: null,
  }
  if (result.shortage) return result

  for (const group of groups) {
    try {
      const order = await api.createOrder({
        items: group.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
        notes: buildOrderNotes(slots[group.farmId] ?? null, customer),
      })
      result.orders.push(order)
      result.placedFarmIds.push(group.farmId)
    } catch (error) {
      if (!isApiError(error, 422)) {
        result.error = error
        return result
      }
      const name = productInStockError(error.detail)
      const line = group.lines.find((candidate) => candidate.name === name) ?? group.lines[0]
      if (!line) {
        result.error = error
        return result
      }
      result.shortage = {
        farmId: group.farmId,
        farmName: group.farmName,
        productId: line.productId,
        productName: line.name,
        unit: line.unit,
        requested: line.quantity,
        available: await api.stockOf(line.productId).catch(() => 0),
      }
      return result
    }
  }
  return result
}
