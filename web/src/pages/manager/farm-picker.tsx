import { Tractor } from "lucide-react"
import { Link, useSearchParams } from "react-router-dom"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useMyFarms } from "@/lib/queries"
import type { Farm } from "@/lib/types"

// The manager's farm the screen is about, kept in ?quinta= so links and reloads keep it.
export function useSelectedFarm() {
  const farms = useMyFarms()
  const [params, setParams] = useSearchParams()
  const items = farms.data?.items ?? []
  const selected = items.find((farm) => farm.id === params.get("quinta")) ?? items[0]
  const select = (id: string) => {
    const next = new URLSearchParams(params)
    next.set("quinta", id)
    setParams(next, { replace: true })
  }
  return { farms, items, selected, select }
}

export function FarmPicker({
  items,
  selected,
  onSelect,
}: {
  items: Farm[]
  selected?: Farm
  onSelect: (id: string) => void
}) {
  if (items.length < 2 || !selected) return null
  return (
    <Select value={selected.id} onValueChange={onSelect}>
      <SelectTrigger className="w-full sm:w-64" aria-label="Quinta">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {items.map((farm) => (
          <SelectItem key={farm.id} value={farm.id}>
            {farm.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

// Loading, error and "no farm yet" states shared by the manager's screens.
export function FarmGate({ farms, children }: { farms: ReturnType<typeof useMyFarms>; children: React.ReactNode }) {
  if (farms.isPending) return <LoadingList />
  if (farms.isError) return <ErrorState error={farms.error} onRetry={() => farms.refetch()} />
  if (farms.data.items.length === 0) {
    return (
      <EmptyState
        icon={Tractor}
        title="Registe a sua quinta"
        description="Comece por criar a quinta; depois pode juntar produtos, cabazes e horários de levantamento."
        action={
          <Button asChild>
            <Link to="/gestor/quintas">Criar quinta</Link>
          </Button>
        }
      />
    )
  }
  return <>{children}</>
}
