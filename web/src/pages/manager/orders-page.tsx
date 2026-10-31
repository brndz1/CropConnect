import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { ClipboardList } from "lucide-react"
import { useSearchParams } from "react-router-dom"
import { OrderCard } from "@/components/order-card"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { api } from "@/lib/api"
import { orderStatusLabel } from "@/lib/format"
import type { Order, OrderStatus, Paged } from "@/lib/types"
import { FarmGate, FarmPicker, useSelectedFarm } from "./farm-picker"

const filters: OrderStatus[] = ["Pending", "Confirmed", "ReadyForPickup", "PickedUp", "Cancelled"]

// RF17: the orders the farm received, organised by state.
export function ManagerOrdersPage() {
  const { farms, items, selected, select } = useSelectedFarm()
  const [params, setParams] = useSearchParams()
  const status = (params.get("estado") ?? "Pending") as OrderStatus
  const orders = useQuery({
    queryKey: ["orders", "farm", selected?.id, status],
    queryFn: () => api.get<Paged<Order>>("/api/orders", { farmId: selected!.id, status, pageSize: 50 }),
    enabled: !!selected,
    placeholderData: keepPreviousData,
    refetchInterval: 30_000,
  })

  return (
    <>
      <PageHeader title="Encomendas" description={selected?.name} />
      <FarmGate farms={farms}>
        <div className="mb-4 flex flex-wrap items-center gap-4">
          <FarmPicker items={items} selected={selected} onSelect={select} />
        </div>
        <Tabs
          value={status}
          onValueChange={(value) => {
            const next = new URLSearchParams(params)
            next.set("estado", value)
            setParams(next, { replace: true })
          }}
        >
          <div className="-mx-4 mb-4 overflow-x-auto overflow-y-hidden px-4 pb-1">
            <TabsList>
              {filters.map((value) => (
                <TabsTrigger key={value} value={value}>
                  {orderStatusLabel[value]}
                </TabsTrigger>
              ))}
            </TabsList>
          </div>
        </Tabs>
        {orders.isPending ? (
          <LoadingList />
        ) : orders.isError ? (
          <ErrorState error={orders.error} onRetry={() => orders.refetch()} />
        ) : orders.data.items.length === 0 ? (
          <EmptyState icon={ClipboardList} title={`Sem encomendas ${orderStatusLabel[status].toLowerCase()}s`} />
        ) : (
          <div className="grid gap-3">
            {orders.data.items.map((order) => (
              <OrderCard key={order.id} order={order} farmName={selected?.name} to={`/gestor/encomendas/${order.id}`} />
            ))}
          </div>
        )}
      </FarmGate>
    </>
  )
}
