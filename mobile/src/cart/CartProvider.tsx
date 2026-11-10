import AsyncStorage from "@react-native-async-storage/async-storage"
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import type { Product } from "@/api/types"
import {
  addLine,
  adjustToStock,
  cartCount,
  cartTotal,
  groupByFarm,
  parseStoredCart,
  removeFarm,
  removeLine,
  restoreLine,
  setLineQuantity,
  type CartGroup,
  type CartLine,
} from "./cart"

interface CartContextValue {
  lines: CartLine[]
  groups: CartGroup[]
  count: number
  total: number
  add: (product: Product, quantity?: number) => void
  setQuantity: (productId: string, quantity: number) => void
  // Removes a line and returns a function that puts it back.
  remove: (productId: string) => () => void
  removeFarm: (farmId: string) => void
  adjustToStock: (productId: string, available: number) => void
}

const CartContext = createContext<CartContextValue | null>(null)

// The cart stays on the device, one per user, so it survives closing the app.
export function CartProvider({ userId, children }: { userId: string; children: ReactNode }) {
  const key = `cropconnect.cart.${userId}`
  const [lines, setLines] = useState<CartLine[]>([])
  const [loaded, setLoaded] = useState(false)

  useEffect(() => {
    let active = true
    AsyncStorage.getItem(key)
      .then((raw) => active && setLines(parseStoredCart(raw)))
      .catch(() => undefined)
      .finally(() => active && setLoaded(true))
    return () => {
      active = false
    }
  }, [key])

  useEffect(() => {
    if (loaded) AsyncStorage.setItem(key, JSON.stringify(lines)).catch(() => undefined)
  }, [key, lines, loaded])

  const add = useCallback(
    (product: Product, quantity = 1) => setLines((current) => addLine(current, product, quantity)),
    [],
  )
  const setQuantity = useCallback(
    (productId: string, quantity: number) => setLines((current) => setLineQuantity(current, productId, quantity)),
    [],
  )
  const remove = useCallback(
    (productId: string) => {
      const index = lines.findIndex((line) => line.productId === productId)
      const removed = lines[index]
      setLines((current) => removeLine(current, productId))
      return () => {
        if (removed) setLines((current) => restoreLine(current, removed, index))
      }
    },
    [lines],
  )
  const clearFarm = useCallback((farmId: string) => setLines((current) => removeFarm(current, farmId)), [])
  const adjust = useCallback(
    (productId: string, available: number) => setLines((current) => adjustToStock(current, productId, available)),
    [],
  )

  const value = useMemo<CartContextValue>(
    () => ({
      lines,
      groups: groupByFarm(lines),
      count: cartCount(lines),
      total: cartTotal(lines),
      add,
      setQuantity,
      remove,
      removeFarm: clearFarm,
      adjustToStock: adjust,
    }),
    [lines, add, setQuantity, remove, clearFarm, adjust],
  )

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}

export function useCart() {
  const context = useContext(CartContext)
  if (!context) throw new Error("useCart must be used inside CartProvider")
  return context
}
