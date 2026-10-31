import { Star } from "lucide-react"
import { cn } from "@/lib/utils"

export function Stars({ value, className }: { value: number; className?: string }) {
  return (
    <span className={cn("inline-flex gap-0.5", className)} role="img" aria-label={`${value} de 5 estrelas`}>
      {[1, 2, 3, 4, 5].map((n) => (
        <Star
          key={n}
          className={cn("size-4", n <= Math.round(value) ? "fill-warning text-warning" : "text-border")}
          aria-hidden
        />
      ))}
    </span>
  )
}

export function StarInput({ value, onChange }: { value: number; onChange: (value: number) => void }) {
  return (
    <div className="flex gap-1" role="radiogroup" aria-label="Classificação">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          aria-label={`${n} ${n === 1 ? "estrela" : "estrelas"}`}
          onClick={() => onChange(n)}
          className="flex size-12 items-center justify-center rounded-full hover:bg-muted"
        >
          <Star className={cn("size-7", n <= value ? "fill-warning text-warning" : "text-outline")} aria-hidden />
        </button>
      ))}
    </div>
  )
}
