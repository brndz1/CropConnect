import { useMutation, useQueryClient } from "@tanstack/react-query"
import { ChevronRight, Loader2, MapPin, Plus, Tractor } from "lucide-react"
import { useState, type FormEvent } from "react"
import { Link, useNavigate } from "react-router-dom"
import { toast } from "sonner"
import { FieldError } from "@/components/field-error"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { api, errorMessage } from "@/lib/api"
import { useMyFarms } from "@/lib/queries"
import type { Farm } from "@/lib/types"
import { FarmFields, emptyFarmForm, farmBody, validateFarm } from "./farm-fields"

function CreateFarmDialog({ trigger }: { trigger: React.ReactNode }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState(emptyFarmForm)
  const [error, setError] = useState<string>()
  const create = useMutation({
    mutationFn: () => api.post<Farm>("/api/farms", farmBody(form)),
    onSuccess: (farm) => {
      queryClient.invalidateQueries({ queryKey: ["farms"] })
      toast.success("Quinta criada. Junte agora os horários de levantamento.")
      setOpen(false)
      navigate(`/gestor/quintas/${farm.id}`)
    },
    onError: (e) => setError(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    const problem = validateFarm(form)
    if (problem) return setError(problem)
    setError(undefined)
    create.mutate()
  }

  return (
    <Dialog open={open} onOpenChange={(next) => (setOpen(next), next && (setForm(emptyFarmForm), setError(undefined)))}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="max-h-[90svh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="font-heading text-brand-900">Nova quinta</DialogTitle>
          <DialogDescription>Os clientes veem o nome, a localização e a descrição.</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="grid gap-4" noValidate>
          <FarmFields form={form} onChange={setForm} />
          <FieldError message={error} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="animate-spin" />}
              Criar quinta
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function ManagerFarmsPage() {
  const farms = useMyFarms()
  const createButton = (
    <Button>
      <Plus /> Nova quinta
    </Button>
  )
  return (
    <>
      <PageHeader title="As minhas quintas" actions={<CreateFarmDialog trigger={createButton} />} />
      {farms.isPending ? (
        <LoadingList rows={2} />
      ) : farms.isError ? (
        <ErrorState error={farms.error} onRetry={() => farms.refetch()} />
      ) : farms.data.items.length === 0 ? (
        <EmptyState
          icon={Tractor}
          title="Registe a sua quinta"
          description="Depois pode juntar produtos, cabazes e os horários em que recebe levantamentos."
          action={<CreateFarmDialog trigger={createButton} />}
        />
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          {farms.data.items.map((farm) => (
            <Link
              key={farm.id}
              to={`/gestor/quintas/${farm.id}`}
              className="flex items-center gap-4 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md"
            >
              <span className="flex size-12 items-center justify-center rounded-full bg-secondary text-primary">
                <Tractor className="size-6" aria-hidden />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block font-heading font-semibold text-brand-900">{farm.name}</span>
                <span className="flex items-center gap-1 text-sm text-muted-foreground">
                  <MapPin className="size-4" aria-hidden /> {farm.location}
                </span>
                {!farm.isActive && (
                  <span className="text-sm font-semibold text-destructive">Desativada pelo administrador</span>
                )}
              </span>
              <ChevronRight className="size-5 text-muted-foreground" aria-hidden />
            </Link>
          ))}
        </div>
      )}
    </>
  )
}
