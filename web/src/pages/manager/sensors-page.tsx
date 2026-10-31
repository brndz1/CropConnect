import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query"
import { AlertTriangle, CheckCircle2, Droplets, Radio, Thermometer } from "lucide-react"
import { useMemo, useState } from "react"
import { CartesianGrid, Line, LineChart, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"
import { toast } from "sonner"
import { AlertBanner } from "@/components/alert-banner"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { api, errorMessage } from "@/lib/api"
import { formatDateTime, formatNumber, sensorTypeLabel } from "@/lib/format"
import type { Alert, Paged, Reading, SensorType, Threshold } from "@/lib/types"
import { cn } from "@/lib/utils"
import { FarmGate, FarmPicker, useSelectedFarm } from "./farm-picker"

const periods = { "24h": 24, "7d": 24 * 7, "30d": 24 * 30 } as const
type Period = keyof typeof periods
const BUCKETS = 24

const icons: Record<SensorType, typeof Droplets> = { Humidity: Droplets, Temperature: Thermometer }

function readingState(value: number, threshold?: Threshold) {
  if (!threshold) return "normal" as const
  if (value < threshold.minValue || value > threshold.maxValue) return "out" as const
  const margin = (threshold.maxValue - threshold.minValue) * 0.1
  if (value < threshold.minValue + margin || value > threshold.maxValue - margin) return "near" as const
  return "normal" as const
}

const stateStyle = {
  normal: { label: "Dentro do intervalo", icon: CheckCircle2, className: "text-success" },
  near: { label: "Perto do limite", icon: AlertTriangle, className: "text-terracotta" },
  out: { label: "Fora do limite", icon: AlertTriangle, className: "text-destructive" },
}

function SensorCard({ reading, threshold }: { reading: Reading; threshold?: Threshold }) {
  const Icon = icons[reading.type]
  const state = stateStyle[readingState(reading.value, threshold)]
  return (
    <div className="rounded-xl border p-4">
      <p className="flex items-center gap-2 font-heading font-semibold text-brand-900">
        <Icon className="size-5 text-primary" aria-hidden /> {sensorTypeLabel[reading.type]}
        <span className="ml-auto text-xs font-normal text-muted-foreground">{reading.sensorId}</span>
      </p>
      <p className="mt-2 font-heading text-[32px] leading-10 font-bold text-brand-900 tabular-nums">
        {formatNumber(reading.value)}
        <span className="text-lg font-semibold"> {reading.unit}</span>
      </p>
      {threshold && (
        <p className="text-sm text-muted-foreground">
          Normal entre {formatNumber(threshold.minValue)} e {formatNumber(threshold.maxValue)} {threshold.unit}
        </p>
      )}
      <p className={cn("mt-2 flex items-center gap-1.5 text-sm font-semibold", state.className)}>
        <state.icon className="size-4" aria-hidden /> {state.label}
      </p>
      <p className="mt-1 text-xs text-muted-foreground">{formatDateTime(reading.timestamp)}</p>
    </div>
  )
}

// Points outside the limits change shape as well as colour (report section 7).
function Dot(props: { cx?: number; cy?: number; payload?: { value: number | null; out: boolean } }) {
  const { cx, cy, payload } = props
  if (cx === undefined || cy === undefined || payload?.value == null) return null
  if (payload.out) {
    return <path d={`M${cx} ${cy - 6}L${cx + 6} ${cy + 5}L${cx - 6} ${cy + 5}Z`} fill="var(--destructive)" />
  }
  return <circle cx={cx} cy={cy} r={3} fill="var(--brand-700)" />
}

// RF19: history in a chart. The API pages readings (100 at most), so each period is split into 24 intervals and
// the chart shows the latest reading of each one.
function History({ farmId, threshold, type }: { farmId: string; threshold?: Threshold; type: SensorType }) {
  const [period, setPeriod] = useState<Period>("24h")
  const windows = useMemo(() => {
    const end = Date.now()
    const span = (periods[period] * 3_600_000) / BUCKETS
    return Array.from({ length: BUCKETS }, (_, i) => ({
      from: end - (BUCKETS - i) * span,
      to: end - (BUCKETS - i - 1) * span,
    }))
    // A new "now" each time the period or the type changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [period, type, farmId])

  const results = useQueries({
    queries: windows.map((w) => ({
      queryKey: ["readings", farmId, type, w.from, w.to],
      queryFn: () =>
        api.get<Paged<Reading>>(`/api/farms/${farmId}/readings`, {
          type,
          from: new Date(w.from).toISOString(),
          to: new Date(w.to).toISOString(),
          pageSize: 1,
        }),
      staleTime: 5 * 60_000,
    })),
  })

  const loading = results.some((r) => r.isPending)
  const failed = results.find((r) => r.isError)
  const data = results.map((r, i) => {
    const reading = r.data?.items[0]
    return {
      time: windows[i].to,
      value: reading ? reading.value : null,
      out: reading ? readingState(reading.value, threshold) === "out" : false,
    }
  })
  const hasData = data.some((point) => point.value !== null)
  const format = (time: number) =>
    new Intl.DateTimeFormat(
      "pt-PT",
      period === "24h" ? { hour: "2-digit", minute: "2-digit" } : { day: "2-digit", month: "2-digit" },
    ).format(time)

  return (
    <div className="rounded-xl border p-4 sm:p-6">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h3 className="text-lg font-semibold">Histórico de {sensorTypeLabel[type].toLowerCase()}</h3>
        <Tabs value={period} onValueChange={(value) => setPeriod(value as Period)}>
          <TabsList>
            <TabsTrigger value="24h">24 h</TabsTrigger>
            <TabsTrigger value="7d">7 dias</TabsTrigger>
            <TabsTrigger value="30d">30 dias</TabsTrigger>
          </TabsList>
        </Tabs>
      </div>
      {failed ? (
        <ErrorState error={failed.error} onRetry={() => results.forEach((r) => r.isError && r.refetch())} />
      ) : loading ? (
        <LoadingList rows={1} className="h-64" />
      ) : !hasData ? (
        <p className="py-16 text-center text-muted-foreground">Sem leituras neste período.</p>
      ) : (
        <div
          className="h-64"
          role="img"
          aria-label={`Gráfico de ${sensorTypeLabel[type].toLowerCase()} no período escolhido`}
        >
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data} margin={{ top: 16, right: 16, bottom: 0, left: -16 }}>
              <CartesianGrid stroke="var(--border)" strokeDasharray="3 3" vertical={false} />
              <XAxis
                dataKey="time"
                tickFormatter={format}
                tick={{ fontSize: 12, fill: "var(--muted-foreground)" }}
                minTickGap={24}
              />
              {/* Both limits always fit on the scale, so the dashed lines are visible. */}
              <YAxis
                tick={{ fontSize: 12, fill: "var(--muted-foreground)" }}
                domain={[
                  (low: number) => Math.floor(Math.min(low, threshold?.minValue ?? low) - 5),
                  (high: number) => Math.ceil(Math.max(high, threshold?.maxValue ?? high) + 5),
                ]}
              />
              <Tooltip
                labelFormatter={(time) => formatDateTime(new Date(time as number))}
                formatter={(value) => [
                  `${formatNumber(value as number)} ${threshold?.unit ?? ""}`,
                  sensorTypeLabel[type],
                ]}
              />
              {threshold && (
                <>
                  <ReferenceLine
                    y={threshold.minValue}
                    stroke="var(--destructive)"
                    strokeDasharray="6 4"
                    label={{ value: "mín.", position: "insideBottomRight", fontSize: 12, fill: "var(--destructive)" }}
                  />
                  <ReferenceLine
                    y={threshold.maxValue}
                    stroke="var(--destructive)"
                    strokeDasharray="6 4"
                    label={{ value: "máx.", position: "insideTopRight", fontSize: 12, fill: "var(--destructive)" }}
                  />
                </>
              )}
              <Line
                type="monotone"
                dataKey="value"
                stroke="var(--brand-700)"
                strokeWidth={2}
                dot={<Dot />}
                connectNulls
                isAnimationActive={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
      <p className="mt-2 text-xs text-muted-foreground">
        Uma leitura por intervalo. Os pontos fora dos limites aparecem como triângulos vermelhos.
      </p>
    </div>
  )
}

export function SensorsPage() {
  const { farms, items, selected, select } = useSelectedFarm()
  const queryClient = useQueryClient()
  const [type, setType] = useState<SensorType>("Humidity")
  const farmId = selected?.id

  const latest = useQuery({
    queryKey: ["readings", farmId, "latest"],
    queryFn: () => api.get<Reading[]>(`/api/farms/${farmId}/readings/latest`),
    enabled: !!farmId,
    refetchInterval: 30_000,
  })
  const thresholds = useQuery({
    queryKey: ["thresholds"],
    queryFn: () => api.get<Threshold[]>("/api/sensor-thresholds"),
  })
  const alerts = useQuery({
    queryKey: ["alerts", farmId, "all"],
    queryFn: () => api.get<Paged<Alert>>(`/api/farms/${farmId}/alerts`, { pageSize: 20 }),
    enabled: !!farmId,
    refetchInterval: 30_000,
  })
  // RF21: mark an alert as resolved.
  const acknowledge = useMutation({
    mutationFn: (id: string) => api.post<Alert>(`/api/alerts/${id}/acknowledge`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["alerts"] })
      toast.success("Alerta marcado como resolvido.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  const thresholdOf = (t: SensorType) => thresholds.data?.find((x) => x.type === t)
  const open = alerts.data?.items.filter((a) => !a.acknowledgedAt) ?? []
  const resolved = alerts.data?.items.filter((a) => a.acknowledgedAt) ?? []

  return (
    <>
      <PageHeader title="Sensores" description={selected?.name} />
      <FarmGate farms={farms}>
        <div className="mb-4">
          <FarmPicker items={items} selected={selected} onSelect={select} />
        </div>

        {open.length > 0 && (
          <div className="mb-6 grid gap-3">
            {open.map((alert) => (
              <AlertBanner
                key={alert.id}
                alert={alert}
                action={
                  <Button
                    size="sm"
                    variant="outline"
                    className="border-foreground bg-transparent text-foreground hover:bg-white/40"
                    disabled={acknowledge.isPending}
                    onClick={() => acknowledge.mutate(alert.id)}
                  >
                    Marcar resolvido
                  </Button>
                }
              />
            ))}
          </div>
        )}

        <h2 className="mb-3 text-xl font-semibold">Leituras atuais</h2>
        {latest.isPending ? (
          <LoadingList rows={2} className="grid gap-3 sm:grid-cols-2" />
        ) : latest.isError ? (
          <ErrorState error={latest.error} onRetry={() => latest.refetch()} />
        ) : latest.data.length === 0 ? (
          <EmptyState
            icon={Radio}
            title="Ainda sem leituras"
            description="Assim que os sensores da quinta enviarem dados, aparecem aqui."
          />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {latest.data.map((reading) => (
              <SensorCard key={reading.id} reading={reading} threshold={thresholdOf(reading.type)} />
            ))}
          </div>
        )}

        {farmId && (
          <section className="mt-8">
            <Tabs value={type} onValueChange={(value) => setType(value as SensorType)} className="mb-3">
              <TabsList>
                <TabsTrigger value="Humidity">Humidade</TabsTrigger>
                <TabsTrigger value="Temperature">Temperatura</TabsTrigger>
              </TabsList>
            </Tabs>
            <History farmId={farmId} type={type} threshold={thresholdOf(type)} />
          </section>
        )}

        {resolved.length > 0 && (
          <section className="mt-8">
            <h2 className="mb-3 text-xl font-semibold">Alertas resolvidos</h2>
            <ul className="grid gap-2">
              {resolved.map((alert) => (
                <li key={alert.id} className="flex flex-wrap items-center gap-3 rounded-xl border px-4 py-3 text-sm">
                  <CheckCircle2 className="size-4 text-success" aria-hidden />
                  <span className="flex-1">{alert.description}</span>
                  <span className="text-muted-foreground">{formatDateTime(alert.createdAt)}</span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </FarmGate>
    </>
  )
}
