import { useQueryClient } from "@tanstack/react-query"
import { AlertTriangle, CheckCircle2, Loader2, ShoppingCart, Trash2, Tractor } from "lucide-react"
import { useState } from "react"
import { Link, useNavigate } from "react-router-dom"
import { PageHeader } from "@/components/page-header"
import { ProductVisual } from "@/components/product-visual"
import { QuantityStepper } from "@/components/quantity-stepper"
import { EmptyState } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { api, ApiError, errorMessage } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { useCart, type CartGroup } from "@/lib/cart"
import { formatPrice, shortId } from "@/lib/format"
import type { Order, Product } from "@/lib/types"

// RF06, RF07: the cart grouped by farm; confirming creates one order per farm. Payment happens at pickup.
export function CartPage() {
  const { session } = useAuth()
  const cart = useCart()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [notes, setNotes] = useState<Record<string, string>>({})
  const [failures, setFailures] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [placed, setPlaced] = useState<Order[]>([])

  async function refreshStock(group: CartGroup) {
    // The reservation failed on stock: show what is left now (task model 4.2 → back to 3.1).
    for (const line of group.lines) {
      try {
        const product = await api.get<Product>(`/api/products/${line.productId}`)
        if (product.stockQuantity === 0) cart.remove(line.productId)
        else cart.add(product, 0)
      } catch {
        cart.remove(line.productId)
      }
    }
  }

  async function confirm() {
    setBusy(true)
    setFailures({})
    const created: Order[] = []
    const failed: Record<string, string> = {}
    for (const group of cart.groups) {
      try {
        const order = await api.post<Order>("/api/orders", {
          items: group.lines.map((line) => ({ productId: line.productId, quantity: line.quantity })),
          notes: notes[group.farmId]?.trim() || null,
        })
        created.push(order)
        cart.clearFarm(group.farmId)
      } catch (e) {
        if (e instanceof ApiError && e.status === 422) {
          failed[group.farmId] =
            "Alguns produtos já não têm stock suficiente. Atualizámos as quantidades disponíveis: reveja e confirme de novo."
          await refreshStock(group)
        } else {
          failed[group.farmId] = errorMessage(e)
        }
      }
    }
    setFailures(failed)
    setBusy(false)
    if (created.length) {
      queryClient.invalidateQueries({ queryKey: ["orders"] })
      queryClient.invalidateQueries({ queryKey: ["products"] })
      setPlaced(created)
    }
  }

  if (placed.length && cart.groups.length === 0) {
    return (
      <div className="mx-auto flex max-w-md flex-col items-center gap-4 py-12 text-center">
        <span className="flex size-16 items-center justify-center rounded-full bg-success/10 text-success">
          <CheckCircle2 className="size-9" aria-hidden />
        </span>
        <h1 className="text-2xl font-semibold">Reserva confirmada</h1>
        <p className="text-muted-foreground">
          {placed.length === 1 ? "Criámos a encomenda" : `Criámos ${placed.length} encomendas, uma por quinta:`}{" "}
          {placed.map((order) => shortId(order.id)).join(", ")}. Vai ser avisado quando estiver pronta a levantar. O
          pagamento é feito no levantamento.
        </p>
        <div className="flex flex-wrap justify-center gap-3">
          <Button onClick={() => navigate("/encomendas")}>Ver encomendas</Button>
          <Button variant="outline" onClick={() => navigate("/explorar")}>
            Continuar a comprar
          </Button>
        </div>
      </div>
    )
  }

  if (cart.groups.length === 0) {
    return (
      <>
        <PageHeader title="Carrinho" />
        <EmptyState
          icon={ShoppingCart}
          title="O carrinho está vazio"
          description="Explore os produtos das quintas e adicione o que quer reservar."
          action={
            <Button asChild>
              <Link to="/explorar">Explorar produtos</Link>
            </Button>
          }
        />
      </>
    )
  }

  const total = cart.groups.reduce((sum, group) => sum + group.total, 0)

  return (
    <>
      <PageHeader
        title="Carrinho"
        description={cart.groups.length > 1 ? `Vai criar ${cart.groups.length} encomendas, uma por quinta.` : undefined}
      />
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <div className="grid gap-4">
          {cart.groups.map((group) => (
            <section key={group.farmId} className="rounded-xl border" aria-label={group.farmName}>
              <header className="flex items-center gap-2 border-b px-4 py-3">
                <Tractor className="size-5 text-primary" aria-hidden />
                <Link to={`/quintas/${group.farmId}`} className="font-heading font-semibold text-brand-900">
                  {group.farmName}
                </Link>
              </header>
              {failures[group.farmId] && (
                <div role="alert" className="mx-4 mt-4 flex gap-3 rounded-lg bg-warning p-3 text-sm text-foreground">
                  <AlertTriangle className="size-5 shrink-0" aria-hidden />
                  {failures[group.farmId]}
                </div>
              )}
              <ul className="divide-y">
                {group.lines.map((line) => (
                  <li key={line.productId} className="flex flex-wrap items-center gap-4 p-4">
                    <ProductVisual type={line.type} className="size-16 shrink-0" />
                    <div className="min-w-0 flex-1">
                      <Link to={`/produtos/${line.productId}`} className="font-heading font-semibold">
                        {line.name}
                      </Link>
                      <p className="text-sm text-muted-foreground">
                        {formatPrice(line.price)} / {line.unit}
                      </p>
                    </div>
                    <QuantityStepper
                      value={line.quantity}
                      max={line.stock}
                      onChange={(value) => cart.setQuantity(line.productId, value)}
                      label={`Quantidade de ${line.name}`}
                    />
                    <p className="w-20 text-right font-heading font-semibold tabular-nums">
                      {formatPrice(line.price * line.quantity)}
                    </p>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => cart.remove(line.productId)}
                      aria-label={`Remover ${line.name}`}
                    >
                      <Trash2 className="text-destructive" />
                    </Button>
                  </li>
                ))}
              </ul>
              <div className="grid gap-2 border-t p-4">
                <label htmlFor={`notes-${group.farmId}`} className="text-sm font-medium">
                  Nota para o produtor (opcional)
                </label>
                <Textarea
                  id={`notes-${group.farmId}`}
                  rows={2}
                  maxLength={1000}
                  value={notes[group.farmId] ?? ""}
                  onChange={(e) => setNotes({ ...notes, [group.farmId]: e.target.value })}
                />
                <p className="text-right font-heading font-semibold text-brand-900">
                  Total da quinta: {formatPrice(group.total)}
                </p>
              </div>
            </section>
          ))}
        </div>

        <aside className="h-fit rounded-xl bg-muted p-5 lg:sticky lg:top-24">
          <h2 className="text-lg font-semibold">Resumo</h2>
          <dl className="mt-3 grid gap-2 text-sm">
            {cart.groups.map((group) => (
              <div key={group.farmId} className="flex justify-between gap-2">
                <dt className="truncate">{group.farmName}</dt>
                <dd className="tabular-nums">{formatPrice(group.total)}</dd>
              </div>
            ))}
          </dl>
          <div className="mt-3 flex justify-between border-t pt-3 font-heading text-lg font-semibold text-brand-900">
            <span>Total</span>
            <span className="tabular-nums">{formatPrice(total)}</span>
          </div>
          <p className="mt-2 text-sm text-muted-foreground">Paga diretamente ao produtor no levantamento.</p>
          {session?.role === "Client" ? (
            <Button className="mt-4 w-full" onClick={confirm} disabled={busy}>
              {busy && <Loader2 className="animate-spin" />}
              Confirmar reserva
            </Button>
          ) : (
            <Button className="mt-4 w-full" asChild>
              <Link to="/entrar" state={{ from: "/carrinho" }}>
                Entrar para reservar
              </Link>
            </Button>
          )}
        </aside>
      </div>
    </>
  )
}
