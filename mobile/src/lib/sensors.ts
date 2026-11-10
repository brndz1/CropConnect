import type { Alert, Reading, SensorType, Threshold } from "@/api/types"
import type { IconName } from "@/components/Icon"
import { dayShort, formatClock, formatNumber, formatShortDate } from "./format"

export const sensorLabel: Record<SensorType, string> = { Humidity: "Humidade", Temperature: "Temperatura" }
export const sensorIcon: Record<SensorType, IconName> = { Humidity: "water-drop", Temperature: "thermostat" }

export type SensorState = "normal" | "near" | "out"

// Out of the normal range, or within 10 % of the range from one of its limits ("perto do limite", G01).
export function sensorState(
  value: number,
  threshold: Pick<Threshold, "minValue" | "maxValue"> | undefined,
): SensorState {
  if (!threshold) return "normal"
  const { minValue, maxValue } = threshold
  if (value < minValue || value > maxValue) return "out"
  const margin = (maxValue - minValue) * 0.1
  return value - minValue <= margin || maxValue - value <= margin ? "near" : "normal"
}

export const formatValue = (value: number, unit: string) => `${formatNumber(value)} ${unit}`

export const rangeLabel = (threshold: Pick<Threshold, "minValue" | "maxValue" | "unit">) =>
  `${formatNumber(threshold.minValue)}-${formatNumber(threshold.maxValue)} ${threshold.unit}`

export const isBelow = (alert: Pick<Alert, "value" | "minValue">) => alert.value < alert.minValue

// "Humidade baixa", "Temperatura alta"
export function alertTitle(alert: Pick<Alert, "type" | "value" | "minValue">) {
  return `${sensorLabel[alert.type]} ${isBelow(alert) ? "baixa" : "alta"}`
}

// "22,4 %, abaixo do mínimo de 35 %"
export function alertSummary(alert: Pick<Alert, "value" | "minValue" | "maxValue" | "unit">) {
  return isBelow(alert)
    ? `${formatValue(alert.value, alert.unit)}, abaixo do mínimo de ${formatValue(alert.minValue, alert.unit)}`
    : `${formatValue(alert.value, alert.unit)}, acima do máximo de ${formatValue(alert.maxValue, alert.unit)}`
}

// "sim-d771a8db is below…" → the sensor in the backend's description of the alert.
export function sensorOfAlert(alert: Pick<Alert, "description">) {
  return /from sensor (\S+)/.exec(alert.description)?.[1] ?? null
}

export interface WeatherContext {
  rain: boolean
  hours: number
  millimetres: number
  chance: number | null
  // The sentence for the manager, in Portuguese.
  advice: string
}

// The Sensors service describes the rain forecast of Open-Meteo in English for low-humidity alerts
// ("Rain forecast in the next 24h (23.2 mm, up to 100% chance); irrigation may not be needed.").
export function parseWeatherContext(text: string | null | undefined): WeatherContext | null {
  if (!text) return null
  const match = /(No significant rain|Rain) forecast in the next (\d+)h \(([\d.]+) mm(?:, up to (\d+)% chance)?\)/.exec(
    text,
  )
  if (!match) return null
  const rain = match[1] === "Rain"
  const hours = Number(match[2])
  const millimetres = Number(match[3])
  const chance = match[4] ? Number(match[4]) : null
  return {
    rain,
    hours,
    millimetres,
    chance,
    advice: rain
      ? "Com esta chuva, talvez não seja preciso regar."
      : "Sem chuva à vista: a humidade do solo deverá continuar a descer. Convém regar.",
  }
}

export type Period = "24h" | "7d" | "30d"

export interface Window {
  from: Date
  to: Date
  label: string
}

const days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"] as const

// The period split into windows; each window becomes one point of the chart (the readings arrive every 30 s, too
// many to download for a month).
export function periodWindows(period: Period, now = new Date()): Window[] {
  const { count, hours } = {
    "24h": { count: 12, hours: 2 },
    "7d": { count: 14, hours: 12 },
    "30d": { count: 15, hours: 48 },
  }[period]
  const end = now.getTime()
  return Array.from({ length: count }, (_, index) => {
    const from = new Date(end - (count - index) * hours * 3_600_000)
    const to = new Date(end - (count - index - 1) * hours * 3_600_000)
    const label =
      period === "24h"
        ? `${formatClock(to).slice(0, 2)}h`
        : period === "7d"
          ? dayShort[days[to.getDay()] ?? "Monday"]
          : formatShortDate(to)
    return { from, to, label }
  })
}

export interface Point {
  label: string
  value: number | null
}

export const average = (readings: Pick<Reading, "value">[]) =>
  readings.length ? readings.reduce((sum, reading) => sum + reading.value, 0) / readings.length : null

export interface Stats {
  min: number
  avg: number
  max: number
}

export function statsOf(values: number[]): Stats | null {
  if (values.length === 0) return null
  return {
    min: Math.min(...values),
    avg: values.reduce((sum, value) => sum + value, 0) / values.length,
    max: Math.max(...values),
  }
}

// Points outside the limits, to mark them with a different shape (section 8).
export const outOfRange = (value: number, threshold: Pick<Threshold, "minValue" | "maxValue"> | undefined) =>
  threshold !== undefined && (value < threshold.minValue || value > threshold.maxValue)

// "há 3 h" style age of an alert, from its start time.
export function since(value: string | null | undefined) {
  return value ? `Desde as ${formatClock(value)}` : null
}
