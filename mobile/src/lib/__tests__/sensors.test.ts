import {
  alertSummary,
  alertTitle,
  parseWeatherContext,
  periodWindows,
  sensorOfAlert,
  sensorState,
  statsOf,
} from "../sensors"

const humidity = { minValue: 30, maxValue: 60 }

describe("sensors", () => {
  it("classifies a reading against the limits", () => {
    expect(sensorState(18, humidity)).toBe("out")
    expect(sensorState(65, humidity)).toBe("out")
    expect(sensorState(31, humidity)).toBe("near")
    expect(sensorState(45, humidity)).toBe("normal")
    expect(sensorState(45, undefined)).toBe("normal")
  })

  it("describes an alert in Portuguese", () => {
    const alert = { type: "Humidity" as const, value: 18, minValue: 30, maxValue: 60, unit: "%" }
    expect(alertTitle(alert)).toBe("Humidade baixa")
    expect(alertSummary(alert)).toBe("18 %, abaixo do mínimo de 30 %")
    expect(alertTitle({ ...alert, type: "Temperature", value: 38 })).toBe("Temperatura alta")
    expect(
      sensorOfAlert({ description: "Humidity of 22.4% from sensor sim-d771a8db is below the minimum of 35%." }),
    ).toBe("sim-d771a8db")
  })

  it("translates the rain forecast of the Sensors service", () => {
    expect(
      parseWeatherContext("Rain forecast in the next 24h (23.2 mm, up to 100% chance); irrigation may not be needed."),
    ).toMatchObject({ rain: true, hours: 24, millimetres: 23.2, chance: 100 })
    const dry = parseWeatherContext("No significant rain forecast in the next 24h (0.4 mm); consider irrigating.")
    expect(dry).toMatchObject({ rain: false, chance: null })
    expect(dry?.advice).toMatch(/Convém regar/)
    expect(parseWeatherContext(null)).toBeNull()
  })

  it("splits a period into windows that end now", () => {
    const now = new Date(2026, 10, 10, 12, 0)
    const day = periodWindows("24h", now)
    expect(day).toHaveLength(12)
    expect(day[11]?.to.getTime()).toBe(now.getTime())
    expect(day[0]?.from.getTime()).toBe(now.getTime() - 24 * 3_600_000)
    expect(periodWindows("30d", now)).toHaveLength(15)
  })

  it("computes minimum, average and maximum", () => {
    expect(statsOf([18, 38, 52])).toEqual({ min: 18, avg: 36, max: 52 })
    expect(statsOf([])).toBeNull()
  })
})
