import { Gift, ShoppingBasket, Sprout } from "lucide-react"
import type { ProductType } from "@/lib/types"
import { cn } from "@/lib/utils"

// The API has no product photos yet, so each kind of product gets its own illustration tile.
const visuals = {
  Product: { icon: Sprout, className: "bg-secondary text-primary" },
  PreDefinedBasket: { icon: ShoppingBasket, className: "bg-olive/15 text-brand-900" },
  SurpriseBasket: { icon: Gift, className: "bg-terracotta/15 text-terracotta" },
} satisfies Record<ProductType, { icon: typeof Sprout; className: string }>

export function ProductVisual({ type, className }: { type: ProductType; className?: string }) {
  const { icon: Icon, className: tone } = visuals[type]
  return (
    <div className={cn("flex aspect-square items-center justify-center rounded-lg", tone, className)} aria-hidden>
      <Icon className="size-1/3" strokeWidth={1.5} />
    </div>
  )
}
