import type { DayOfWeek, PickupSchedule } from "@/api/types"
import { formatTime } from "./format"

// "9:00" or "0900" → "09:00"; null when it is not a time of day.
export function normalizeTime(value: string): string | null {
  const match = /^(\d{1,2})[:h.]?(\d{2})$/.exec(value.trim())
  if (!match) return null
  const hour = Number(match[1])
  const minute = Number(match[2])
  if (hour > 23 || minute > 59) return null
  return `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`
}

type Existing = Pick<PickupSchedule, "id" | "version" | "dayOfWeek" | "openTime" | "closeTime">

// G21 sets the same hours for every pickup day; this works out what to create, change and remove.
export function planSchedules(existing: Existing[], days: DayOfWeek[], openTime: string, closeTime: string) {
  const create = days
    .filter((day) => !existing.some((item) => item.dayOfWeek === day))
    .map((dayOfWeek) => ({ dayOfWeek, openTime, closeTime }))
  const update = existing
    .filter((item) => days.includes(item.dayOfWeek))
    .filter((item) => formatTime(item.openTime) !== openTime || formatTime(item.closeTime) !== closeTime)
    .map((item) => ({ id: item.id, version: item.version, dayOfWeek: item.dayOfWeek, openTime, closeTime }))
  const remove = existing.filter((item) => !days.includes(item.dayOfWeek)).map((item) => item.id)
  return { create, update, remove }
}

export const isPlanEmpty = (plan: ReturnType<typeof planSchedules>) =>
  plan.create.length === 0 && plan.update.length === 0 && plan.remove.length === 0
