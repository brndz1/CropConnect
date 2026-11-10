import { product } from "@/test/fixtures"
import {
  applyFilter,
  categoryOf,
  formatQuantity,
  illustrationFor,
  isExpired,
  maxQuantity,
  sortForCatalog,
  stockLevel,
} from "../catalog"

describe("catalog", () => {
  it("picks the illustration and the category from the name", () => {
    expect(illustrationFor(product({ name: "Maçã Esmolfe" }))).toBe("maca")
    expect(categoryOf(product({ name: "Maçã Esmolfe" }))).toBe("Frutas")
    expect(illustrationFor(product({ name: "Coração-de-boi" }))).toBe("tomate")
    expect(categoryOf(product({ name: "Ovos do campo" }))).toBe("Ovos")
    expect(categoryOf(product({ name: "Queijo" }))).toBe("Outros")
    expect(illustrationFor(product({ type: "SurpriseBasket", name: "Cabaz" }))).toBe("cabazSurpresa")
    expect(categoryOf(product({ type: "PreDefinedBasket", name: "Cabaz de sopa" }))).toBe("Cabazes")
  })

  it("classifies the stock", () => {
    expect(stockLevel(product({ stockQuantity: 20 }))).toBe("available")
    expect(stockLevel(product({ stockQuantity: 2 }))).toBe("low")
    expect(stockLevel(product({ stockQuantity: 0 }))).toBe("soldOut")
    expect(stockLevel(product({ isAvailable: false }))).toBe("soldOut")
  })

  it("limits surprise baskets to one per client", () => {
    expect(maxQuantity(product({ type: "SurpriseBasket", stockQuantity: 4 }))).toBe(1)
    expect(maxQuantity(product({ stockQuantity: 2 }))).toBe(2)
  })

  it("writes quantities with the unit in Portuguese", () => {
    expect(formatQuantity(2, "kg")).toBe("2 kg")
    expect(formatQuantity(1, "unidade")).toBe("1 unidade")
    expect(formatQuantity(3, "unidade")).toBe("3 unidades")
    expect(formatQuantity(2, "cabaz")).toBe("2 cabazes")
  })

  it("filters and lists the available products first", () => {
    const list = [
      product({ id: "a", name: "Alface", stockQuantity: 0 }),
      product({ id: "b", name: "Morango" }),
      product({ id: "c", name: "Cenoura" }),
    ]
    expect(sortForCatalog(list).map((item) => item.id)).toEqual(["c", "b", "a"])
    expect(applyFilter(list, "fruit").map((item) => item.id)).toEqual(["b"])
    expect(applyFilter(list, "available").map((item) => item.id)).toEqual(["b", "c"])
  })

  it("hides baskets past their pickup date", () => {
    const now = new Date("2026-11-01T12:00:00Z")
    expect(isExpired(product({ availableUntil: "2026-10-31T23:00:00Z" }), now)).toBe(true)
    expect(isExpired(product({ availableUntil: "2026-11-02T23:00:00Z" }), now)).toBe(false)
    expect(isExpired(product(), now)).toBe(false)
  })
})
