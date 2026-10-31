import { CalendarClock, Plus } from "lucide-react"
import { Link } from "react-router-dom"
import { toast } from "sonner"
import { ProductVisual } from "@/components/product-visual"
import { Button } from "@/components/ui/button"
import { useAuth } from "@/lib/auth"
import { useCart } from "@/lib/cart"
import { formatDate, formatPrice, productTypeLabel } from "@/lib/format"
import type { Product } from "@/lib/types"
import { cn } from "@/lib/utils"

const LOW_STOCK = 5

export function StockNote({ product }: { product: Product }) {
  if (product.stockQuantity === 0) return <span className="text-sm font-semibold text-destructive">Esgotado</span>
  if (product.stockQuantity <= LOW_STOCK)
    return <span className="text-sm font-semibold text-terracotta">Restam {product.stockQuantity}</span>
  return <span className="text-sm text-muted-foreground">{product.stockQuantity} disponíveis</span>
}

export function ProductCard({ product }: { product: Product }) {
  const { session } = useAuth()
  const { add } = useCart()
  const soldOut = product.stockQuantity === 0
  const canBuy = !session || session.role === "Client"

  return (
    <article className="group relative flex flex-col overflow-hidden rounded-xl border bg-card transition-shadow hover:shadow-md">
      <ProductVisual type={product.type} className={cn("aspect-[4/3] rounded-none", soldOut && "opacity-50")} />
      {product.type !== "Product" && (
        <span className="absolute top-3 left-3 rounded-full bg-terracotta px-3 py-1 font-heading text-xs font-semibold text-white">
          {productTypeLabel[product.type]}
        </span>
      )}
      <div className="flex flex-1 flex-col gap-1 p-4">
        <h3 className="font-heading text-base leading-6 font-semibold">
          <Link to={`/produtos/${product.id}`} className="after:absolute after:inset-0">
            {product.name}
          </Link>
        </h3>
        <p className="text-sm text-muted-foreground">{product.farmName}</p>
        {product.availableUntil && (
          <p className="flex items-center gap-1 text-sm text-muted-foreground">
            <CalendarClock className="size-4" aria-hidden /> Até {formatDate(product.availableUntil)}
          </p>
        )}
        <StockNote product={product} />
        <div className="mt-auto flex items-end justify-between gap-2 pt-3">
          <p className="font-heading text-lg font-semibold text-brand-900">
            {formatPrice(product.price)}
            <span className="text-sm font-normal text-muted-foreground"> / {product.unit}</span>
          </p>
          {canBuy && (
            <Button
              size="icon"
              className="relative z-10"
              disabled={soldOut}
              aria-label={`Adicionar ${product.name} ao carrinho`}
              onClick={() => {
                add(product)
                toast.success(`${product.name} adicionado ao carrinho`)
              }}
            >
              <Plus />
            </Button>
          )}
        </div>
      </div>
    </article>
  )
}

export const productGrid = "grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-3 lg:grid-cols-4"
