import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Clock, Loader2, Plus, Trash2 } from "lucide-react"
import { useState, type FormEvent } from "react"
import { useNavigate, useParams } from "react-router-dom"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { FieldError } from "@/components/field-error"
import { PageHeader } from "@/components/page-header"
import { ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { api, errorMessage } from "@/lib/api"
import { dayLabel, dayOrder, formatTime } from "@/lib/format"
import type { DayOfWeek, Farm, PickupSchedule } from "@/lib/types"
import { FarmFields, farmBody, farmToForm, validateFarm } from "./farm-fields"

function Schedules({ farm }: { farm: Farm }) {
  const queryClient = useQueryClient()
  const [day, setDay] = useState<DayOfWeek>("Saturday")
  const [open, setOpen] = useState("09:00")
  const [close, setClose] = useState("13:00")
  const [error, setError] = useState<string>()
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["farm", farm.id] })

  const add = useMutation({
    mutationFn: () =>
      api.post<PickupSchedule>(`/api/farms/${farm.id}/pickup-schedules`, {
        dayOfWeek: day,
        openTime: open,
        closeTime: close,
      }),
    onSuccess: () => {
      refresh()
      toast.success("Horário adicionado.")
    },
    onError: (e) => setError(errorMessage(e)),
  })
  const remove = useMutation({
    mutationFn: (id: string) => api.delete(`/api/pickup-schedules/${id}`),
    onSuccess: refresh,
    onError: (e) => toast.error(errorMessage(e)),
  })

  const sorted = [...farm.pickupSchedules].sort(
    (a, b) => dayOrder.indexOf(a.dayOfWeek) - dayOrder.indexOf(b.dayOfWeek) || a.openTime.localeCompare(b.openTime),
  )

  function submit(event: FormEvent) {
    event.preventDefault()
    if (close <= open) return setError("A hora de fecho tem de ser depois da abertura.")
    setError(undefined)
    add.mutate()
  }

  return (
    <section className="rounded-xl border p-4 sm:p-6">
      <h2 className="text-lg font-semibold">Horários de levantamento</h2>
      <p className="mb-4 text-sm text-muted-foreground">
        Os dias e horas em que os clientes podem levantar as encomendas.
      </p>
      {sorted.length === 0 ? (
        <p className="mb-4 rounded-lg bg-muted p-4 text-sm">
          Ainda sem horários: os clientes não sabem quando levantar.
        </p>
      ) : (
        <ul className="mb-4 grid gap-2">
          {sorted.map((schedule) => (
            <li key={schedule.id} className="flex items-center gap-3 rounded-lg bg-muted py-1 pr-1 pl-4">
              <Clock className="size-4 text-primary" aria-hidden />
              <span className="flex-1 font-medium">{dayLabel[schedule.dayOfWeek]}</span>
              <span className="font-heading text-sm font-semibold tabular-nums">
                {formatTime(schedule.openTime)} – {formatTime(schedule.closeTime)}
              </span>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => remove.mutate(schedule.id)}
                aria-label={`Remover horário de ${dayLabel[schedule.dayOfWeek]}`}
              >
                <Trash2 className="text-destructive" />
              </Button>
            </li>
          ))}
        </ul>
      )}
      <form onSubmit={submit} className="grid grid-cols-2 gap-3">
        <div className="col-span-2 grid gap-2">
          <Label htmlFor="schedule-day">Dia</Label>
          <Select value={day} onValueChange={(value) => setDay(value as DayOfWeek)}>
            <SelectTrigger id="schedule-day" className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {dayOrder.map((d) => (
                <SelectItem key={d} value={d}>
                  {dayLabel[d]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="open">Abre</Label>
          <Input id="open" type="time" value={open} onChange={(e) => setOpen(e.target.value)} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="close">Fecha</Label>
          <Input id="close" type="time" value={close} onChange={(e) => setClose(e.target.value)} />
        </div>
        <Button type="submit" variant="outline" disabled={add.isPending} className="col-span-2 w-fit">
          <Plus /> Adicionar
        </Button>
      </form>
      <div className="mt-2">
        <FieldError message={error} />
      </div>
    </section>
  )
}

function FarmDetailsForm({ farm }: { farm: Farm }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [form, setForm] = useState(() => farmToForm(farm))
  const [error, setError] = useState<string>()

  const save = useMutation({
    mutationFn: () => api.patch<Farm>(`/api/farms/${farm.id}`, { ...farmBody(form), expectedVersion: farm.version }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["farm", farm.id], updated)
      queryClient.invalidateQueries({ queryKey: ["farms"] })
      toast.success("Quinta atualizada.")
    },
    onError: (e) => setError(errorMessage(e)),
  })
  const remove = useMutation({
    mutationFn: () => api.delete(`/api/farms/${farm.id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["farms"] })
      toast.success("Quinta removida.")
      navigate("/gestor/quintas")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const problem = validateFarm(form)
    if (problem) return setError(problem)
    setError(undefined)
    save.mutate()
  }

  return (
    <form onSubmit={submit} className="grid h-fit gap-4 rounded-xl border p-4 sm:p-6" noValidate>
      <h2 className="text-lg font-semibold">Dados da quinta</h2>
      <FarmFields form={form} onChange={setForm} />
      <FieldError message={error} />
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={save.isPending}>
          {save.isPending && <Loader2 className="animate-spin" />}
          Guardar
        </Button>
        <ConfirmDialog
          trigger={
            <Button
              type="button"
              variant="outline"
              className="border-destructive text-destructive hover:bg-destructive/10"
            >
              Remover quinta
            </Button>
          }
          title={`Remover ${farm.name}?`}
          description="A quinta e os seus produtos saem do catálogo. As encomendas já feitas mantêm-se."
          confirmLabel="Remover quinta"
          destructive
          onConfirm={() => remove.mutate()}
        />
      </div>
    </form>
  )
}

export function FarmEditPage() {
  const { id } = useParams()
  const farm = useQuery({ queryKey: ["farm", id], queryFn: () => api.get<Farm>(`/api/farms/${id}`) })

  if (farm.isPending) return <LoadingList rows={3} />
  if (farm.isError) return <ErrorState error={farm.error} onRetry={() => farm.refetch()} />

  return (
    <>
      <PageHeader title={farm.data.name} back />
      <div className="grid gap-6 lg:grid-cols-2">
        <FarmDetailsForm key={farm.data.version} farm={farm.data} />
        <Schedules farm={farm.data} />
      </div>
    </>
  )
}
