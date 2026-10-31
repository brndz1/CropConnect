import { useQueries, useQuery } from "@tanstack/react-query"
import { ArrowRight, ClipboardList, Package, ShoppingBag, ThermometerSun } from "lucide-react"
import { Link } from "react-router-dom"
import { AlertBanner } from "@/components/alert-banner"
import { OrderCard } from "@/components/order-card"
import { PageHeader } from "@/components/page-header"
import { Button } from "@/components/ui/button"
import { api } from "@/lib/api"
import type { Alert, Order, Paged, Product } from "@/lib/types"
import { FarmGate, useSelectedFarm } from "./farm-picker"

function Stat({ icon: Icon, label, value, to }: { icon: typeof Package; label: string; value?: number; to: string }) {
  return (
    <Link to={to} className="flex items-center gap-4 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md">
      <span className="flex size-12 items-center justify-center rounded-full bg-secondary text-primary">
        <Icon className="size-6" aria-hidden />
      </span>
      <span>
        <span className="block font-heading text-[28px] leading-8 font-bold text-brand-900 tabular-nums">
          {value ?? "–"}
        </span>
        <span className="text-sm text-muted-foreground">{label}</span>
      </span>
    </Link>
  )
}

function useFarmOrders(farmIds: string[], status: string) {
  return useQueries({
    queries: farmIds.map((id) => ({
      queryKey: ["orders", "farm", id, status],
      queryFn: () => api.get<Paged<Order>>("/api/orders", { farmId: id, status, pageSize: 20 }),
      refetchInterval: 30_000,
    })),
  })
}

// The manager's home (Painel): new orders, open alerts and low stock in one place (RF18, RF20).
export function DashboardPage() {
  const { farms, items } = useSelectedFarm()
  const farmIds = items.map((farm) => farm.id)
  const names = new Map(items.map((farm) => [farm.id, farm.name]))

  const pending = useFarmOrders(farmIds, "Pending")
  const ready = useFarmOrders(farmIds, "ReadyForPickup")
  const alerts = useQueries({
    queries: farmIds.map((id) => ({
      queryKey: ["alerts", id, "open"],
      queryFn: () => api.get<Paged<Alert>>(`/api/farms/${id}/alerts`, { onlyOpen: true, pageSize: 20 }),
      refetchInterval: 30_000,
    })),
  })
  const lowStock = useQuery({
    queryKey: ["products", "low-stock", farmIds],
    queryFn: async () => {
      const pages = await Promise.all(
        farmIds.map((id) =>
          api.get<Paged<Product>>("/api/products", { farmId: id, onlyInStock: false, pageSize: 100 }),
        ),
      )
      return pages.flatMap((page) => page.items).filter((product) => product.stockQuantity <= 5)
    },
    enabled: farmIds.length > 0,
  })

  const sum = (results: { data?: { totalCount: number } }[]) =>
    results.every((r) => r.data) ? results.reduce((total, r) => total + r.data!.totalCount, 0) : undefined
  const openAlerts = alerts.flatMap((r) => r.data?.items ?? [])
  const newOrders = pending
    .flatMap((r) => r.data?.items ?? [])
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
    .slice(0, 5)

  return (
    <>
      <PageHeader title="Painel" description="O que precisa da sua atenção hoje." />
      <FarmGate farms={farms}>
        {openAlerts.length > 0 && (
          <div className="mb-6 grid gap-3">
            {openAlerts.map((alert) => (
              <AlertBanner
                key={alert.id}
                alert={alert}
                farmName={farmIds.length > 1 ? names.get(alert.farmId) : undefined}
                action={
                  <Button
                    size="sm"
                    variant="outline"
                    asChild
                    className="border-foreground bg-transparent text-foreground hover:bg-white/40"
                  >
                    <Link to={`/gestor/sensores?quinta=${alert.farmId}`}>Ver</Link>
                  </Button>
                }
              />
            ))}
          </div>
        )}

        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat
            icon={ClipboardList}
            label="Encomendas novas"
            value={sum(pending)}
            to="/gestor/encomendas?estado=Pending"
          />
          <Stat
            icon={ShoppingBag}
            label="Prontas a levantar"
            value={sum(ready)}
            to="/gestor/encomendas?estado=ReadyForPickup"
          />
          <Stat icon={ThermometerSun} label="Alertas por resolver" value={sum(alerts)} to="/gestor/sensores" />
          <Stat icon={Package} label="Produtos com pouco stock" value={lowStock.data?.length} to="/gestor/produtos" />
        </div>

        <section className="mt-8">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-xl font-semibold">Encomendas novas</h2>
            <Button variant="link" asChild className="px-0">
              <Link to="/gestor/encomendas">
                Ver todas <ArrowRight />
              </Link>
            </Button>
          </div>
          {newOrders.length === 0 ? (
            <p className="rounded-xl border border-dashed p-6 text-center text-muted-foreground">
              Não há encomendas à espera de confirmação.
            </p>
          ) : (
            <div className="grid gap-3">
              {newOrders.map((order) => (
                <OrderCard
                  key={order.id}
                  order={order}
                  farmName={names.get(order.farmId)}
                  to={`/gestor/encomendas/${order.id}`}
                />
              ))}
            </div>
          )}
        </section>

        {(lowStock.data?.length ?? 0) > 0 && (
          <section className="mt-8">
            <h2 className="mb-3 text-xl font-semibold">Pouco stock</h2>
            <ul className="grid gap-2 sm:grid-cols-2">
              {lowStock.data!.map((product) => (
                <li key={product.id} className="flex items-center justify-between rounded-xl border px-4 py-3">
                  <span className="font-medium">{product.name}</span>
                  <span
                    className={
                      product.stockQuantity === 0 ? "font-semibold text-destructive" : "font-semibold text-terracotta"
                    }
                  >
                    {product.stockQuantity === 0 ? "Esgotado" : `Restam ${product.stockQuantity}`}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </FarmGate>
    </>
  )
}
