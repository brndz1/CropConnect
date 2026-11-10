import type { DayOfWeek, PickupSchedule } from "@/api/types"
import { dayOrder, dayShort, formatShortDate, formatTime } from "./format"

type Slot = Pick<PickupSchedule, "dayOfWeek" | "openTime" | "closeTime">

const jsDay: Record<DayOfWeek, number> = {
  Sunday: 0,
  Monday: 1,
  Tuesday: 2,
  Wednesday: 3,
  Thursday: 4,
  Friday: 5,
  Saturday: 6,
}

const byDay = (a: Slot, b: Slot) =>
  dayOrder.indexOf(a.dayOfWeek) - dayOrder.indexOf(b.dayOfWeek) || a.openTime.localeCompare(b.openTime)

const hours = (slot: Slot) => `${formatTime(slot.openTime)}-${formatTime(slot.closeTime)}`

// "Sáb e dom - 09:00-13:00" when the days share the hours, else "Sex 17:00-19:30 - Sáb 09:00-13:00".
export function summarizeSchedules(schedules: Slot[]): string {
  if (schedules.length === 0) return "Sem horários de levantamento"
  const sorted = [...schedules].sort(byDay)
  const sameHours = sorted.every((slot) => hours(slot) === hours(sorted[0] as Slot))
  if (sameHours) {
    const days = sorted.map((slot) => dayShort[slot.dayOfWeek])
    const list =
      days.length === 1 ? days[0] : `${days.slice(0, -1).join(", ")} e ${days[days.length - 1]?.toLowerCase()}`
    return `${list} - ${hours(sorted[0] as Slot)}`
  }
  return sorted.map((slot) => `${dayShort[slot.dayOfWeek]} ${hours(slot)}`).join(" - ")
}

export interface PickupSlot {
  // Start of the slot.
  start: Date
  end: Date
  // "Sáb 31/10 - 09:00-13:00"
  label: string
}

function at(date: Date, time: string) {
  const [hour, minute] = time.split(":").map(Number)
  const result = new Date(date)
  result.setHours(hour ?? 0, minute ?? 0, 0, 0)
  return result
}

// The next pickup slots from now on, earliest first; a slot that already closed today is skipped.
export function nextPickupSlots(schedules: Slot[], now = new Date(), count = 3, until?: Date | null): PickupSlot[] {
  const slots: PickupSlot[] = []
  for (let offset = 0; offset < 14 && slots.length < count; offset++) {
    const day = new Date(now)
    day.setDate(now.getDate() + offset)
    const matches = schedules.filter((slot) => jsDay[slot.dayOfWeek] === day.getDay()).sort(byDay)
    for (const slot of matches) {
      const start = at(day, slot.openTime)
      const end = at(day, slot.closeTime)
      if (end <= now) continue
      if (until && start > until) continue
      const weekday = dayShort[slot.dayOfWeek]
      slots.push({ start, end, label: `${weekday} ${formatShortDate(start)} - ${hours(slot)}` })
      if (slots.length >= count) break
    }
  }
  return slots
}

// "Próximo: sáb 31/10"
export function nextPickupLabel(schedules: Slot[], now = new Date()) {
  const next = nextPickupSlots(schedules, now, 1)[0]
  if (!next) return null
  const weekday = next.label.split(" ")[0]?.toLowerCase()
  return `Próximo: ${weekday} ${formatShortDate(next.start)}`
}
