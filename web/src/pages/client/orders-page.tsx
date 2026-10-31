import { keepPreviousData, useQuery } from "@tanstack/react-query"
import { ClipboardList } from "lucide-react"
import { Link, useSearchParams } from "react-router-dom"
import { OrderCard } from "@/components/order-card"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { api } from "@/lib/api"
import { orderStatusLabel } from "@/lib/format"
import { useFarmNames } from "@/lib/queries"
import type { Order, OrderStatus, Paged } from "@/lib/types"

const filters: (OrderStatus | "all")[] = ["all", "Pending", "Confirmed", "ReadyForPickup", "PickedUp", "Cancelled"]

// RF08: the client's order history and status.
export function OrdersPage() {
  const [params, setParams] = useSearchParams()
  const status = (params.get("estado") ?? "all") as OrderStatus | "all"
  const farmNames = useFarmNames()
  const orders = useQuery({
    queryKey: ["orders", "mine", status],
    queryFn: () =>
      api.get<Paged<Order>>("/api/orders/mine", { status: status === "all" ? undefined : status, pageSize: 50 }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader title="As minhas encomendas" />
      <Tabs
        value={status}
        onValueChange={(value) => setParams(value === "all" ? {} : { estado: value }, { replace: true })}
      >
        <div className="-mx-4 mb-4 overflow-x-auto overflow-y-hidden px-4 pb-1">
          <TabsList>
            {filters.map((value) => (
              <TabsTrigger key={value} value={value}>
                {value === "all" ? "Todas" : orderStatusLabel[value]}
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
        <EmptyState
          icon={ClipboardList}
          title={status === "all" ? "Ainda não fez encomendas" : "Nenhuma encomenda neste estado"}
          description="As reservas que fizer aparecem aqui, com o estado de cada uma."
          action={
            <Button asChild>
              <Link to="/explorar">Explorar produtos</Link>
            </Button>
          }
        />
      ) : (
        <div className="grid gap-3">
          {orders.data.items.map((order) => (
            <OrderCard
              key={order.id}
              order={order}
              farmName={farmNames.get(order.farmId)}
              to={`/encomendas/${order.id}`}
            />
          ))}
        </div>
      )}
    </>
  )
}
