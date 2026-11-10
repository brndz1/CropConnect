import type { Product } from "@/api/types"
import type { IllustrationName } from "@/components/illustrations"

// The API has no product category or picture, so both come from the product name, with the illustrations of the
// visual system (table 29). Unknown products fall back to the generic basket.

export type Category = "Legumes" | "Frutas" | "Ovos" | "Outros"

interface Kind {
  words: string[]
  illustration: IllustrationName
  category: Category
}

const kinds: Kind[] = [
  { words: ["tomate", "cherry", "coracao-de-boi", "coracao de boi"], illustration: "tomate", category: "Legumes" },
  { words: ["alface", "salada", "rucula", "espinafre", "couve"], illustration: "alface", category: "Legumes" },
  { words: ["cenoura"], illustration: "cenoura", category: "Legumes" },
  { words: ["batata"], illustration: "batata", category: "Legumes" },
  { words: ["abobora", "curgete"], illustration: "abobora", category: "Legumes" },
  { words: ["alho", "cebola"], illustration: "alhoFrances", category: "Legumes" },
  { words: ["maca", "pera", "laranja", "kiwi"], illustration: "maca", category: "Frutas" },
  { words: ["morango", "framboesa", "mirtilo", "cereja"], illustration: "morango", category: "Frutas" },
  { words: ["ovo"], illustration: "ovos", category: "Ovos" },
  { words: ["mel"], illustration: "mel", category: "Outros" },
]

// "Maçã Esmolfe" → "maca esmolfe"
export const normalize = (value: string) => value.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase()

function kindOf(name: string): Kind | undefined {
  const text = normalize(name)
  return kinds.find((kind) => kind.words.some((word) => new RegExp(`\\b${word}`).test(text)))
}

export function illustrationFor(product: Pick<Product, "name" | "type">): IllustrationName {
  if (product.type === "SurpriseBasket") return "cabazSurpresa"
  if (product.type === "PreDefinedBasket") return "cabaz"
  return kindOf(product.name)?.illustration ?? "cabaz"
}

export const isBasket = (product: Pick<Product, "type">) => product.type !== "Product"

export function categoryOf(product: Pick<Product, "name" | "type">): Category | "Cabazes" {
  if (isBasket(product)) return "Cabazes"
  return kindOf(product.name)?.category ?? "Outros"
}

export type StockLevel = "available" | "low" | "soldOut"

export const LOW_STOCK = 5

export function stockLevel(product: Pick<Product, "stockQuantity" | "isAvailable">): StockLevel {
  if (!product.isAvailable || product.stockQuantity <= 0) return "soldOut"
  return product.stockQuantity <= LOW_STOCK ? "low" : "available"
}

// One surprise basket per client (C05, P4 of the heuristic analysis); otherwise the stock is the limit.
export const SURPRISE_BASKET_LIMIT = 1

export function maxQuantity(product: Pick<Product, "type" | "stockQuantity">) {
  const stock = Math.max(0, product.stockQuantity)
  return product.type === "SurpriseBasket" ? Math.min(SURPRISE_BASKET_LIMIT, stock) : stock
}

// Portuguese plural of a unit word: "unidade" → "unidades", "cabaz" → "cabazes"; symbols ("kg") stay as they are.
function pluralUnit(unit: string) {
  if (unit.length <= 3 || unit.endsWith(".")) return unit
  if (unit.endsWith("z") || unit.endsWith("r")) return `${unit}es`
  return /[aeiou]$/i.test(unit) ? `${unit}s` : unit
}

// "kg" → "2 kg"; "unidade" → "3 unidades"; a basket → "1 cabaz".
export function formatQuantity(quantity: number, unit: string) {
  const clean = unit.trim() || "un."
  return `${quantity} ${quantity === 1 ? clean : pluralUnit(clean)}`
}

// The catalogue shows what can be bought today first ("disponíveis hoje primeiro").
export function sortForCatalog<T extends Pick<Product, "stockQuantity" | "isAvailable" | "name">>(products: T[]) {
  return [...products].sort((a, b) => {
    const soldOut = Number(stockLevel(a) === "soldOut") - Number(stockLevel(b) === "soldOut")
    return soldOut !== 0 ? soldOut : a.name.localeCompare(b.name, "pt")
  })
}

export type CatalogFilter = "all" | "vegetables" | "fruit" | "eggs" | "available" | "farms"

export const filterLabels: Record<CatalogFilter, string> = {
  all: "Todos",
  vegetables: "Legumes",
  fruit: "Frutas",
  eggs: "Ovos",
  available: "Disponíveis",
  farms: "Quintas",
}

export function applyFilter<T extends Pick<Product, "name" | "type" | "stockQuantity" | "isAvailable">>(
  products: T[],
  filter: CatalogFilter,
) {
  switch (filter) {
    case "vegetables":
      return products.filter((product) => categoryOf(product) === "Legumes")
    case "fruit":
      return products.filter((product) => categoryOf(product) === "Frutas")
    case "eggs":
      return products.filter((product) => categoryOf(product) === "Ovos")
    case "available":
      return products.filter((product) => stockLevel(product) !== "soldOut")
    default:
      return products
  }
}

// A basket that can no longer be picked up leaves the catalogue.
export const isExpired = (product: Pick<Product, "availableUntil">, now = new Date()) =>
  product.availableUntil !== null && new Date(product.availableUntil).getTime() < now.getTime()
