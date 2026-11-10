import type { PickupSchedule } from "@/api/types"
import { isPlanEmpty, normalizeTime, planSchedules } from "../farm"

const schedule = (
  id: string,
  dayOfWeek: PickupSchedule["dayOfWeek"],
  openTime = "09:00:00",
  closeTime = "13:00:00",
) => ({
  id,
  version: 1,
  dayOfWeek,
  openTime,
  closeTime,
})

describe("pickup schedule planning", () => {
  it("normalises times of day", () => {
    expect(normalizeTime("9:00")).toBe("09:00")
    expect(normalizeTime("1330")).toBe("13:30")
    expect(normalizeTime("25:00")).toBeNull()
    expect(normalizeTime("manhã")).toBeNull()
  })

  it("creates, updates and removes days to match the form (G21)", () => {
    const existing = [schedule("sat", "Saturday"), schedule("fri", "Friday", "17:00:00", "19:30:00")]
    const plan = planSchedules(existing, ["Saturday", "Sunday"], "09:00", "13:30")
    expect(plan.create).toEqual([{ dayOfWeek: "Sunday", openTime: "09:00", closeTime: "13:30" }])
    expect(plan.update).toEqual([
      { id: "sat", version: 1, dayOfWeek: "Saturday", openTime: "09:00", closeTime: "13:30" },
    ])
    expect(plan.remove).toEqual(["fri"])
  })

  it("does nothing when nothing changed", () => {
    expect(isPlanEmpty(planSchedules([schedule("sat", "Saturday")], ["Saturday"], "09:00", "13:00"))).toBe(true)
  })
})
