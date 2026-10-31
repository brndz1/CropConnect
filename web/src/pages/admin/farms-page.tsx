import { useMutation, useQueryClient } from "@tanstack/react-query"
import { MapPin, Tractor } from "lucide-react"
import { Link } from "react-router-dom"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { api, errorMessage } from "@/lib/api"
import { useFarms } from "@/lib/queries"
import type { Farm } from "@/lib/types"

// RF23: every farm, including the deactivated ones; the administrator activates and deactivates them.
export function AdminFarmsPage() {
  const queryClient = useQueryClient()
  const farms = useFarms({ includeInactive: true })
  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      api.put<Farm>(`/api/farms/${id}/active`, { isActive }),
    onSuccess: (farm) => {
      queryClient.invalidateQueries({ queryKey: ["farms"] })
      toast.success(farm.isActive ? "Quinta reativada." : "Quinta desativada.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  return (
    <>
      <PageHeader title="Quintas" description="Uma quinta desativada sai do catálogo e deixa de receber encomendas." />
      {farms.isPending ? (
        <LoadingList />
      ) : farms.isError ? (
        <ErrorState error={farms.error} onRetry={() => farms.refetch()} />
      ) : farms.data.items.length === 0 ? (
        <EmptyState icon={Tractor} title="Ainda não há quintas" />
      ) : (
        <ul className="grid gap-3">
          {farms.data.items.map((farm) => (
            <li key={farm.id} className="flex flex-wrap items-center gap-4 rounded-xl border p-4">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-secondary text-primary">
                <Tractor className="size-5" aria-hidden />
              </span>
              <div className="min-w-0 flex-1">
                <Link to={`/quintas/${farm.id}`} className="font-heading font-semibold text-brand-900">
                  {farm.name}
                </Link>
                <p className="flex items-center gap-1 text-sm text-muted-foreground">
                  <MapPin className="size-4" aria-hidden /> {farm.location}
                </p>
              </div>
              <span
                className={
                  farm.isActive ? "text-sm font-semibold text-success" : "text-sm font-semibold text-destructive"
                }
              >
                {farm.isActive ? "Ativa" : "Desativada"}
              </span>
              {farm.isActive ? (
                <ConfirmDialog
                  trigger={
                    <Button
                      variant="outline"
                      size="sm"
                      className="border-destructive text-destructive hover:bg-destructive/10"
                    >
                      Desativar
                    </Button>
                  }
                  title={`Desativar ${farm.name}?`}
                  description="A quinta e os seus produtos deixam de aparecer no catálogo. Pode reativá-la a qualquer momento."
                  confirmLabel="Desativar"
                  destructive
                  onConfirm={() => setActive.mutate({ id: farm.id, isActive: false })}
                />
              ) : (
                <Button variant="outline" size="sm" onClick={() => setActive.mutate({ id: farm.id, isActive: true })}>
                  Reativar
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
