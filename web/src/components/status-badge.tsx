import { CheckCircle2, Clock, PackageCheck, ShoppingBag, XCircle } from "lucide-react"
import { orderStatusLabel } from "@/lib/format"
import type { OrderStatus } from "@/lib/types"
import { cn } from "@/lib/utils"

// Every state has an icon and a word, never colour alone (report section 7).
const styles: Record<OrderStatus, { icon: typeof Clock; className: string }> = {
  Pending: { icon: Clock, className: "bg-warning text-foreground" },
  Confirmed: { icon: CheckCircle2, className: "bg-info/10 text-info" },
  ReadyForPickup: { icon: ShoppingBag, className: "bg-success/10 text-success" },
  PickedUp: { icon: PackageCheck, className: "bg-secondary text-brand-900" },
  Cancelled: { icon: XCircle, className: "bg-destructive/10 text-destructive" },
}

export function StatusBadge({ status, className }: { status: OrderStatus; className?: string }) {
  const { icon: Icon, className: tone } = styles[status]
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-3 py-1 font-heading text-xs font-semibold whitespace-nowrap",
        tone,
        className,
      )}
    >
      <Icon className="size-3.5" aria-hidden />
      {orderStatusLabel[status]}
    </span>
  )
}
