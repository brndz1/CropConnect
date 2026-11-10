import type { Product } from "@/api/types"
import { Badge } from "@/components/Badge"
import { formatQuantity, stockLevel } from "@/lib/catalog"

// Badge of a product: the basket kind, or the stock (Pouco stock, Esgotado), always with an icon and text.
export function ProductBadge({ product, detailed = false }: { product: Product; detailed?: boolean }) {
  const level = stockLevel(product)
  if (level === "soldOut") return <Badge label="Esgotado" tone="error" icon="warning" />
  if (product.type === "SurpriseBasket") return <Badge label="Surpresa" tone="terracotta" icon="redeem" />
  if (product.type === "PreDefinedBasket") return <Badge label="Pré-definido" tone="green" icon="eco" />
  if (level === "low") {
    const label = detailed
      ? `Pouco stock - restam ${formatQuantity(product.stockQuantity, product.unit)}`
      : "Pouco stock"
    return <Badge label={label} tone="warning" icon="error" />
  }
  return null
}
