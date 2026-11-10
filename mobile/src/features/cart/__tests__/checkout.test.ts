import { ApiError } from "@/api/errors"
import type { Order } from "@/api/types"
import { addLine, groupByFarm, setLineQuantity } from "@/cart/cart"
import { product } from "@/test/fixtures"
import { placeOrders, type CheckoutApi } from "../checkout"

const tomato = product({ id: "tomato", name: "Tomate-cherry", price: 2.4, stockQuantity: 2 })
const basket = product({
  id: "basket",
  farmId: "f2",
  farmName: "Horta da Serra",
  name: "Cabaz surpresa da semana",
  type: "SurpriseBasket",
  price: 6,
  stockQuantity: 4,
})
const groups = groupByFarm(setLineQuantity(addLine(addLine([], basket), tomato), "tomato", 2))
const slots = { f1: "Sáb 31/10 - 09:00-13:00", f2: "Sex 30/10 - 17:00-19:30" }

function api(stock: Record<string, number>, refuse?: string): CheckoutApi & { created: unknown[] } {
  const created: unknown[] = []
  return {
    created,
    stockOf: jest.fn(async (id: string) => stock[id] ?? 0),
    createOrder: jest.fn(async (body) => {
      if (refuse && body.items.some((item) => item.productId === refuse)) {
        throw new ApiError(422, "rule", "Insufficient stock for product 'Tomate-cherry'.")
      }
      created.push(body)
      return { id: `order-${created.length}` } as Order
    }),
  }
}

describe("placeOrders", () => {
  it("creates one order per farm with the pickup slot and the client's name", async () => {
    const fake = api({ tomato: 2, basket: 4 })
    const result = await placeOrders(groups, slots, "Marta Ferreira", fake)

    expect(result.shortage).toBeNull()
    expect(result.placedFarmIds).toEqual(["f2", "f1"])
    expect(fake.created).toEqual([
      {
        items: [{ productId: "basket", quantity: 1 }],
        notes: "Levantamento: Sex 30/10 - 17:00-19:30\nCliente: Marta Ferreira",
      },
      {
        items: [{ productId: "tomato", quantity: 2 }],
        notes: "Levantamento: Sáb 31/10 - 09:00-13:00\nCliente: Marta Ferreira",
      },
    ])
  })

  it("reports a shortage before creating any order (C07E)", async () => {
    const fake = api({ tomato: 1, basket: 4 })
    const result = await placeOrders(groups, slots, null, fake)

    expect(fake.created).toEqual([])
    expect(result.shortage).toMatchObject({
      farmName: "Quinta do Vale",
      productName: "Tomate-cherry",
      requested: 2,
      available: 1,
    })
  })

  it("returns other failures with the orders already placed", async () => {
    const fake = api({ tomato: 2, basket: 4 })
    ;(fake.createOrder as jest.Mock)
      .mockResolvedValueOnce({ id: "order-1" })
      .mockRejectedValueOnce(new ApiError(0, "offline"))
    const result = await placeOrders(groups, slots, null, fake)

    expect(result.placedFarmIds).toEqual(["f2"])
    expect(result.error).toBeInstanceOf(ApiError)
  })

  it("keeps the orders already placed when the stock runs out in between", async () => {
    const fake = api({ tomato: 2, basket: 4 }, "tomato")
    ;(fake.stockOf as jest.Mock).mockResolvedValueOnce(4).mockResolvedValueOnce(2).mockResolvedValueOnce(1)
    const result = await placeOrders(groups, slots, null, fake)

    expect(result.placedFarmIds).toEqual(["f2"])
    expect(result.shortage).toMatchObject({ productId: "tomato", available: 1 })
  })
})
