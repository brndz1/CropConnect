import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Droplets, Loader2, Thermometer } from "lucide-react"
import { useState, type FormEvent } from "react"
import { toast } from "sonner"
import { FieldError } from "@/components/field-error"
import { PageHeader } from "@/components/page-header"
import { ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { api, errorMessage } from "@/lib/api"
import { formatDateTime, sensorTypeLabel } from "@/lib/format"
import type { Threshold } from "@/lib/types"

function ThresholdForm({ threshold }: { threshold: Threshold }) {
  const queryClient = useQueryClient()
  const [min, setMin] = useState(String(threshold.minValue).replace(".", ","))
  const [max, setMax] = useState(String(threshold.maxValue).replace(".", ","))
  const [error, setError] = useState<string>()
  const Icon = threshold.type === "Humidity" ? Droplets : Thermometer

  const save = useMutation({
    mutationFn: () =>
      api.put<Threshold>(`/api/sensor-thresholds/${threshold.type}`, {
        minValue: Number(min.replace(",", ".")),
        maxValue: Number(max.replace(",", ".")),
        expectedVersion: threshold.version,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["thresholds"] })
      toast.success(`Limites de ${sensorTypeLabel[threshold.type].toLowerCase()} guardados.`)
    },
    onError: (e) => setError(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const lo = Number(min.replace(",", "."))
    const hi = Number(max.replace(",", "."))
    if (!Number.isFinite(lo) || !Number.isFinite(hi)) return setError("Indique valores numéricos.")
    if (lo >= hi) return setError("O mínimo tem de ser menor do que o máximo.")
    setError(undefined)
    save.mutate()
  }

  return (
    <form onSubmit={submit} className="grid gap-4 rounded-xl border p-4 sm:p-6" noValidate>
      <h2 className="flex items-center gap-2 text-lg font-semibold">
        <Icon className="size-5 text-primary" aria-hidden /> {sensorTypeLabel[threshold.type]}
      </h2>
      <div className="grid grid-cols-2 gap-4">
        <div className="grid gap-2">
          <Label htmlFor={`${threshold.type}-min`}>Mínimo ({threshold.unit})</Label>
          <Input
            id={`${threshold.type}-min`}
            inputMode="decimal"
            value={min}
            onChange={(e) => setMin(e.target.value)}
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor={`${threshold.type}-max`}>Máximo ({threshold.unit})</Label>
          <Input
            id={`${threshold.type}-max`}
            inputMode="decimal"
            value={max}
            onChange={(e) => setMax(e.target.value)}
          />
        </div>
      </div>
      <FieldError message={error} />
      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          Guardar
        </Button>
        {threshold.updatedAt && (
          <span className="text-sm text-muted-foreground">Alterado em {formatDateTime(threshold.updatedAt)}</span>
        )}
      </div>
    </form>
  )
}

// RF24: the normal humidity and temperature ranges every farm's alerts use.
export function ThresholdsPage() {
  const thresholds = useQuery({
    queryKey: ["thresholds"],
    queryFn: () => api.get<Threshold[]>("/api/sensor-thresholds"),
  })
  return (
    <>
      <PageHeader
        title="Limites dos sensores"
        description="Uma leitura fora destes intervalos gera um alerta para o produtor."
      />
      {thresholds.isPending ? (
        <LoadingList rows={2} />
      ) : thresholds.isError ? (
        <ErrorState error={thresholds.error} onRetry={() => thresholds.refetch()} />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {thresholds.data.map((threshold) => (
            <ThresholdForm key={`${threshold.type}-${threshold.version}`} threshold={threshold} />
          ))}
        </div>
      )}
    </>
  )
}
