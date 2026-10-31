import { ChevronRight, Leaf } from "lucide-react"
import { Link } from "react-router-dom"
import { StatusBadge } from "@/components/status-badge"
import { formatDate, formatPrice, shortId } from "@/lib/format"
import type { Order } from "@/lib/types"

// Number, date, farm, total and status badge (report section 6.5).
export function OrderCard({ order, farmName, to }: { order: Order; farmName?: string; to: string }) {
  const count = order.items.reduce((sum, item) => sum + item.quantity, 0)
  return (
    <Link to={to} className="flex items-center gap-4 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md">
      <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-secondary text-primary">
        <Leaf className="size-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-heading font-semibold text-brand-900">Encomenda {shortId(order.id)}</p>
        <p className="truncate text-sm text-muted-foreground">
          {count} {count === 1 ? "artigo" : "artigos"} · {farmName ?? "Quinta"} · {formatDate(order.createdAt)}
        </p>
      </div>
      <div className="flex flex-col items-end gap-1.5">
        <span className="font-heading font-semibold tabular-nums">{formatPrice(order.totalAmount)}</span>
        <StatusBadge status={order.status} />
      </div>
      <ChevronRight className="hidden size-5 text-muted-foreground sm:block" aria-hidden />
    </Link>
  )
}
