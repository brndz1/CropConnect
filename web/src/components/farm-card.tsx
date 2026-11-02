import { MapPin, Tractor } from "lucide-react"
import { Link } from "react-router-dom"
import type { Farm } from "@/lib/types"

// Name and location, linking to the farm's public page.
export function FarmCard({ farm }: { farm: Farm }) {
  return (
    <Link
      to={`/quintas/${farm.id}`}
      className="flex items-center gap-4 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md"
    >
      <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-secondary text-primary">
        <Tractor className="size-6" aria-hidden />
      </span>
      <span className="min-w-0">
        <span className="block truncate font-heading font-semibold text-brand-900">{farm.name}</span>
        <span className="flex items-center gap-1 text-sm text-muted-foreground">
          <MapPin className="size-4" aria-hidden /> {farm.location}
        </span>
      </span>
    </Link>
  )
}

export const farmGrid = "grid gap-3 sm:grid-cols-2 lg:grid-cols-3"
