import { AlertTriangle, RotateCcw, type LucideIcon } from "lucide-react"
import type { ReactNode } from "react"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { errorMessage } from "@/lib/api"

// Screen states of the visual system: skeleton while loading, an empty state that suggests the next action and a
// network error with "Tentar de novo" (RNF04, RNF05).

export function LoadingList({ rows = 4, className }: { rows?: number; className?: string }) {
  return (
    <div className={className ?? "grid gap-3"} aria-busy="true" aria-label="A carregar">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-24 rounded-xl" />
      ))}
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 rounded-xl border bg-muted px-6 py-10 text-center">
      <AlertTriangle className="size-8 text-destructive" aria-hidden />
      <p className="font-heading font-semibold text-brand-900">Não foi possível carregar</p>
      <p className="max-w-sm text-sm text-muted-foreground">{errorMessage(error)}</p>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          <RotateCcw /> Tentar de novo
        </Button>
      )}
    </div>
  )
}

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: LucideIcon
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed px-6 py-12 text-center">
      <div className="flex size-14 items-center justify-center rounded-full bg-secondary text-primary">
        <Icon className="size-7" aria-hidden />
      </div>
      <p className="font-heading text-lg font-semibold text-brand-900">{title}</p>
      {description && <p className="max-w-sm text-sm text-muted-foreground">{description}</p>}
      {action}
    </div>
  )
}
