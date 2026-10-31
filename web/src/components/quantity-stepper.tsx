import { Minus, Plus } from "lucide-react"
import { Button } from "@/components/ui/button"

// − and + of 48; + turns off at the available stock.
export function QuantityStepper({
  value,
  max,
  onChange,
  label,
}: {
  value: number
  max: number
  onChange: (value: number) => void
  label: string
}) {
  return (
    <div className="inline-flex items-center gap-1 rounded-full border bg-background" role="group" aria-label={label}>
      <Button
        variant="ghost"
        size="icon"
        onClick={() => onChange(value - 1)}
        disabled={value <= 1}
        aria-label="Diminuir quantidade"
      >
        <Minus />
      </Button>
      <span className="min-w-8 text-center font-heading font-semibold tabular-nums" aria-live="polite">
        {value}
      </span>
      <Button
        variant="ghost"
        size="icon"
        onClick={() => onChange(value + 1)}
        disabled={value >= max}
        aria-label="Aumentar quantidade"
      >
        <Plus />
      </Button>
    </div>
  )
}
