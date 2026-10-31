import { Clock } from "lucide-react"
import { dayLabel, dayOrder, formatTime } from "@/lib/format"
import type { PickupSchedule } from "@/lib/types"

export function PickupScheduleList({ schedules }: { schedules: PickupSchedule[] }) {
  if (schedules.length === 0) {
    return <p className="text-sm text-muted-foreground">A quinta ainda não indicou horários de levantamento.</p>
  }
  const sorted = [...schedules].sort(
    (a, b) => dayOrder.indexOf(a.dayOfWeek) - dayOrder.indexOf(b.dayOfWeek) || a.openTime.localeCompare(b.openTime),
  )
  return (
    <ul className="grid gap-2">
      {sorted.map((schedule) => (
        <li key={schedule.id} className="flex items-center justify-between gap-4 rounded-lg bg-muted px-4 py-3">
          <span className="flex items-center gap-2 font-medium">
            <Clock className="size-4 text-primary" aria-hidden />
            {dayLabel[schedule.dayOfWeek]}
          </span>
          <span className="font-heading text-sm font-semibold text-brand-900 tabular-nums">
            {formatTime(schedule.openTime)} – {formatTime(schedule.closeTime)}
          </span>
        </li>
      ))}
    </ul>
  )
}
