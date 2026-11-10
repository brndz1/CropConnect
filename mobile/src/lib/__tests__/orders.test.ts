import type { Order } from "@/api/types"
import {
  buildOrderNotes,
  clientCanCancel,
  nextManagerStatus,
  parseOrderNotes,
  productInStockError,
  splitOrders,
  stepOf,
} from "../orders"

const order = (id: string, status: Order["status"], createdAt: string) => ({ id, status, createdAt }) as Order

describe("orders", () => {
  it("keeps the pickup slot and the client's name in the notes", () => {
    const notes = buildOrderNotes("Sáb 31/10 - 09:00-13:00", "Marta Ferreira")
    expect(notes).toBe("Levantamento: Sáb 31/10 - 09:00-13:00\nCliente: Marta Ferreira")
    expect(parseOrderNotes(notes)).toEqual({ slot: "Sáb 31/10 - 09:00-13:00", customer: "Marta Ferreira", other: null })
    expect(parseOrderNotes("Sem cebola")).toEqual({ slot: null, customer: null, other: "Sem cebola" })
    expect(buildOrderNotes(null, null)).toBeNull()
  })

  it("puts cancelled orders with the finished ones (P2)", () => {
    const { active, past } = splitOrders([
      order("a", "Pending", "2026-10-30T10:00:00Z"),
      order("b", "Cancelled", "2026-10-30T11:00:00Z"),
      order("c", "ReadyForPickup", "2026-10-30T12:00:00Z"),
      order("d", "PickedUp", "2026-10-20T12:00:00Z"),
    ])
    expect(active.map((item) => item.id)).toEqual(["c", "a"])
    expect(past.map((item) => item.id)).toEqual(["b", "d"])
  })

  it("follows the life cycle of an order", () => {
    expect(stepOf("Pending")).toBe(1)
    expect(stepOf("PickedUp")).toBe(4)
    expect(nextManagerStatus("Pending")).toBe("Confirmed")
    expect(nextManagerStatus("Confirmed")).toBe("ReadyForPickup")
    expect(nextManagerStatus("ReadyForPickup")).toBeNull()
    expect(clientCanCancel("Confirmed")).toBe(true)
    expect(clientCanCancel("ReadyForPickup")).toBe(false)
  })

  it("reads the product out of a stock error", () => {
    expect(productInStockError("Insufficient stock for product 'Tomate-cherry'.")).toBe("Tomate-cherry")
    expect(productInStockError("Something else")).toBeNull()
  })
})
