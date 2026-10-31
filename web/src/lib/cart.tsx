import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import type { Product } from "@/lib/types"

// The cart lives in the browser; on checkout it becomes one order per farm (report section 1.1).

export interface CartLine {
  productId: string
  name: string
  farmId: string
  farmName: string
  unit: string
  price: number
  stock: number
  type: Product["type"]
  quantity: number
}

export interface CartGroup {
  farmId: string
  farmName: string
  lines: CartLine[]
  total: number
}

interface CartContextValue {
  lines: CartLine[]
  groups: CartGroup[]
  count: number
  add: (product: Product, quantity?: number) => void
  setQuantity: (productId: string, quantity: number) => void
  remove: (productId: string) => void
  clearFarm: (farmId: string) => void
}

const CART_KEY = "cropconnect.cart"
const CartContext = createContext<CartContextValue | null>(null)

function load(): CartLine[] {
  try {
    return JSON.parse(localStorage.getItem(CART_KEY) ?? "[]")
  } catch {
    return []
  }
}

export function CartProvider({ children }: { children: ReactNode }) {
  const [lines, setLines] = useState<CartLine[]>(load)

  useEffect(() => {
    try {
      localStorage.setItem(CART_KEY, JSON.stringify(lines))
    } catch {
      // Private mode or full storage: the cart still works for this visit.
    }
  }, [lines])

  const add = useCallback((product: Product, quantity = 1) => {
    setLines((current) => {
      const existing = current.find((line) => line.productId === product.id)
      if (existing) {
        return current.map((line) =>
          line.productId === product.id
            ? {
                ...line,
                stock: product.stockQuantity,
                quantity: Math.min(line.quantity + quantity, product.stockQuantity),
              }
            : line,
        )
      }
      return [
        ...current,
        {
          productId: product.id,
          name: product.name,
          farmId: product.farmId,
          farmName: product.farmName,
          unit: product.unit,
          price: product.price,
          stock: product.stockQuantity,
          type: product.type,
          quantity: Math.min(quantity, product.stockQuantity),
        },
      ]
    })
  }, [])

  const setQuantity = useCallback((productId: string, quantity: number) => {
    setLines((current) =>
      current.map((line) =>
        line.productId === productId ? { ...line, quantity: Math.max(1, Math.min(quantity, line.stock)) } : line,
      ),
    )
  }, [])

  const remove = useCallback((productId: string) => {
    setLines((current) => current.filter((line) => line.productId !== productId))
  }, [])

  const clearFarm = useCallback((farmId: string) => {
    setLines((current) => current.filter((line) => line.farmId !== farmId))
  }, [])

  const value = useMemo(() => {
    const byFarm = new Map<string, CartGroup>()
    for (const line of lines) {
      const group = byFarm.get(line.farmId) ?? { farmId: line.farmId, farmName: line.farmName, lines: [], total: 0 }
      group.lines.push(line)
      group.total += line.price * line.quantity
      byFarm.set(line.farmId, group)
    }
    return {
      lines,
      groups: [...byFarm.values()],
      count: lines.reduce((sum, line) => sum + line.quantity, 0),
      add,
      setQuantity,
      remove,
      clearFarm,
    }
  }, [lines, add, setQuantity, remove, clearFarm])

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}

export function useCart() {
  const context = useContext(CartContext)
  if (!context) throw new Error("useCart must be used inside CartProvider")
  return context
}
