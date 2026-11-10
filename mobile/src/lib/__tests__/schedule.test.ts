import type { PickupSchedule } from "@/api/types"
import { nextPickupLabel, nextPickupSlots, summarizeSchedules } from "../schedule"

const slot = (dayOfWeek: PickupSchedule["dayOfWeek"], openTime: string, closeTime: string) => ({
  dayOfWeek,
  openTime,
  closeTime,
})

const weekend = [slot("Sunday", "09:00:00", "13:00:00"), slot("Saturday", "09:00:00", "13:00:00")]
const serra = [slot("Saturday", "09:00:00", "13:00:00"), slot("Friday", "17:00:00", "19:30:00")]

describe("pickup schedules", () => {
  it("summarises days that share the hours", () => {
    expect(summarizeSchedules(weekend)).toBe("Sáb e dom - 09:00-13:00")
  })

  it("lists each day when the hours differ", () => {
    expect(summarizeSchedules(serra)).toBe("Sex 17:00-19:30 - Sáb 09:00-13:00")
    expect(summarizeSchedules([])).toBe("Sem horários de levantamento")
  })

  it("offers the next slots, skipping the ones already closed", () => {
    // Friday 30/10/2026 at 18:00: Friday's slot is still open.
    const now = new Date(2026, 9, 30, 18, 0)
    expect(nextPickupSlots(serra, now, 2).map((next) => next.label)).toEqual([
      "Sex 30/10 - 17:00-19:30",
      "Sáb 31/10 - 09:00-13:00",
    ])
    // At 20:00 it closed, so Saturday comes first.
    expect(nextPickupSlots(serra, new Date(2026, 9, 30, 20, 0), 1)[0]?.label).toBe("Sáb 31/10 - 09:00-13:00")
  })

  it("stops at the pickup limit of a basket", () => {
    const now = new Date(2026, 9, 30, 8, 0)
    const until = new Date(2026, 9, 31, 23, 59)
    expect(nextPickupSlots(weekend, now, 5, until).map((next) => next.label)).toEqual(["Sáb 31/10 - 09:00-13:00"])
  })

  it("names the next pickup day", () => {
    expect(nextPickupLabel(weekend, new Date(2026, 9, 29, 10, 0))).toBe("Próximo: sáb 31/10")
    expect(nextPickupLabel([])).toBeNull()
  })
})
