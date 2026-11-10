// Sample API data for the tests.
import type { Product } from "@/api/types"

export function product(overrides: Partial<Product> = {}): Product {
  return {
    id: "p1",
    farmId: "f1",
    farmName: "Quinta do Vale",
    name: "Tomate-cherry",
    description: "",
    unit: "kg",
    price: 2.4,
    stockQuantity: 10,
    isAvailable: true,
    type: "Product",
    availableUntil: null,
    contents: null,
    createdAt: null,
    updatedAt: null,
    version: 1,
    ...overrides,
  }
}
