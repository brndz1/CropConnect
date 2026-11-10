import { product } from "@/test/fixtures"
import {
  addLine,
  adjustToStock,
  cartTotal,
  groupByFarm,
  parseStoredCart,
  removeLine,
  restoreLine,
  setLineQuantity,
} from "../cart"

const tomato = product({ id: "tomato", name: "Tomate-cherry", price: 2.4, stockQuantity: 2 })
const basket = product({
  id: "basket",
  farmId: "f2",
  farmName: "Horta da Serra",
  name: "Cabaz surpresa",
  type: "SurpriseBasket",
  price: 6,
  stockQuantity: 4,
})

describe("cart", () => {
  it("adds products and never goes over the stock", () => {
    let lines = addLine([], tomato)
    lines = addLine(lines, tomato, 5)
    expect(lines).toHaveLength(1)
    expect(lines[0]?.quantity).toBe(2)
  })

  it("keeps one surprise basket per client", () => {
    const lines = addLine(addLine([], basket), basket)
    expect(lines[0]?.quantity).toBe(1)
    expect(lines[0]?.max).toBe(1)
  })

  it("ignores sold-out products", () => {
    expect(addLine([], product({ stockQuantity: 0 }))).toEqual([])
  })

  it("groups the lines by farm with subtotals", () => {
    const lines = setLineQuantity(addLine(addLine([], tomato), basket), "tomato", 2)
    const groups = groupByFarm(lines)
    expect(groups.map((group) => [group.farmName, group.total])).toEqual([
      ["Quinta do Vale", 4.8],
      ["Horta da Serra", 6],
    ])
    expect(cartTotal(lines)).toBe(10.8)
  })

  it("puts a removed line back in its place", () => {
    const lines = addLine(addLine([], tomato), basket)
    const removed = lines[0]
    if (!removed) throw new Error("missing line")
    const after = removeLine(lines, "tomato")
    expect(restoreLine(after, removed, 0).map((line) => line.productId)).toEqual(["tomato", "basket"])
  })

  it("adjusts a line to the stock that is left (C06b)", () => {
    const lines = setLineQuantity(addLine([], tomato), "tomato", 2)
    expect(adjustToStock(lines, "tomato", 1)[0]).toMatchObject({ quantity: 1, max: 1 })
    expect(adjustToStock(lines, "tomato", 0)).toEqual([])
  })

  it("reads only valid lines from storage", () => {
    expect(parseStoredCart("not json")).toEqual([])
    expect(parseStoredCart('[{"productId":"a","quantity":1},{"bad":true}]')).toHaveLength(1)
  })
})
