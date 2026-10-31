import { AlertTriangle, CloudRain, Droplets, Thermometer } from "lucide-react"
import type { ReactNode } from "react"
import { formatDateTime, formatNumber, sensorTypeLabel } from "@/lib/format"
import type { Alert } from "@/lib/types"

// A sensor out of its limits: icon, short sentence and an action; it stays until resolved (report section 6.5).
export function AlertBanner({ alert, farmName, action }: { alert: Alert; farmName?: string; action?: ReactNode }) {
  const Icon = alert.type === "Humidity" ? Droplets : Thermometer
  const below = alert.value < alert.minValue
  return (
    <div role="alert" className="flex flex-wrap items-start gap-3 rounded-xl bg-warning p-4 text-foreground">
      <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-white/60">
        <Icon className="size-5" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="flex items-center gap-1.5 font-heading font-semibold">
          <AlertTriangle className="size-4" aria-hidden />
          {sensorTypeLabel[alert.type]} {below ? "abaixo" : "acima"} do limite
          {farmName && <span className="font-normal"> · {farmName}</span>}
        </p>
        <p className="text-sm">
          {formatNumber(alert.value)} {alert.unit} (normal entre {formatNumber(alert.minValue)} e{" "}
          {formatNumber(alert.maxValue)} {alert.unit}) · {formatDateTime(alert.createdAt)}
        </p>
        {alert.weatherContext ? (
          <p className="mt-1 flex items-start gap-1.5 text-sm">
            <CloudRain className="mt-0.5 size-4 shrink-0" aria-hidden /> {alert.weatherContext}
          </p>
        ) : (
          alert.type === "Humidity" && below && <p className="mt-1 text-sm">Previsão meteorológica indisponível.</p>
        )}
      </div>
      {action}
    </div>
  )
}
