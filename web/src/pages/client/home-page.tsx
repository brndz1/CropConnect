import { useQuery } from "@tanstack/react-query"
import { ArrowRight, Gift, MapPin, Tractor } from "lucide-react"
import { Link } from "react-router-dom"
import { ProductCard, productGrid } from "@/components/product-card"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { api } from "@/lib/api"
import { useFarms } from "@/lib/queries"
import type { Paged, Product } from "@/lib/types"

function Section({ title, link, children }: { title: string; link?: string; children: React.ReactNode }) {
  return (
    <section className="mt-10">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-xl font-semibold sm:text-2xl">{title}</h2>
        {link && (
          <Button variant="link" asChild className="px-0">
            <Link to={link}>
              Ver mais <ArrowRight />
            </Link>
          </Button>
        )}
      </div>
      {children}
    </section>
  )
}

export function HomePage() {
  const baskets = useQuery({
    queryKey: ["products", "home-baskets"],
    queryFn: () => api.get<Paged<Product>>("/api/products", { type: "SurpriseBasket", onlyInStock: true, pageSize: 4 }),
  })
  const latest = useQuery({
    queryKey: ["products", "home-latest"],
    queryFn: () => api.get<Paged<Product>>("/api/products", { onlyInStock: true, pageSize: 8 }),
  })
  const farms = useFarms()

  return (
    <>
      <section className="overflow-hidden rounded-2xl bg-primary px-6 py-10 text-primary-foreground sm:px-10 sm:py-14">
        <p className="font-heading text-sm font-medium text-white/80">Produtores locais perto de ti</p>
        <h1 className="mt-2 max-w-xl text-[32px] leading-10 font-bold text-white">Do campo à tua mesa</h1>
        <p className="mt-3 max-w-lg text-white/90">
          Reserva produtos e cabazes das quintas da tua região e levanta-os na quinta, dentro do horário. Pagas no
          levantamento.
        </p>
        <div className="mt-6 flex flex-wrap gap-3">
          <Button asChild className="bg-white text-primary hover:bg-secondary">
            <Link to="/explorar">Explorar produtos</Link>
          </Button>
          <Button asChild variant="outline" className="border-white bg-transparent text-white hover:bg-white/10">
            <Link to="/explorar?tipo=SurpriseBasket">
              <Gift /> Cabazes surpresa
            </Link>
          </Button>
        </div>
      </section>

      {(baskets.data?.items.length ?? 0) > 0 && (
        <Section title="Cabazes surpresa a preço reduzido" link="/explorar?tipo=SurpriseBasket">
          <div className={productGrid}>
            {baskets.data!.items.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        </Section>
      )}

      <Section title="Quintas" link="/explorar">
        {farms.isPending ? (
          <LoadingList rows={3} className="grid gap-3 sm:grid-cols-3" />
        ) : farms.isError ? (
          <ErrorState error={farms.error} onRetry={() => farms.refetch()} />
        ) : farms.data.items.length === 0 ? (
          <EmptyState icon={Tractor} title="Ainda não há quintas" description="Volte em breve." />
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {farms.data.items.slice(0, 6).map((farm) => (
              <Link
                key={farm.id}
                to={`/quintas/${farm.id}`}
                className="flex items-center gap-4 rounded-xl border bg-card p-4 transition-shadow hover:shadow-md"
              >
                <span className="flex size-12 shrink-0 items-center justify-center rounded-full bg-secondary text-primary">
                  <Tractor className="size-6" aria-hidden />
                </span>
                <span className="min-w-0">
                  <span className="block truncate font-heading font-semibold text-brand-900">{farm.name}</span>
                  <span className="flex items-center gap-1 text-sm text-muted-foreground">
                    <MapPin className="size-4" aria-hidden /> {farm.location}
                  </span>
                </span>
              </Link>
            ))}
          </div>
        )}
      </Section>

      <Section title="Acabados de chegar" link="/explorar">
        {latest.isPending ? (
          <LoadingList rows={4} className={productGrid} />
        ) : latest.isError ? (
          <ErrorState error={latest.error} onRetry={() => latest.refetch()} />
        ) : latest.data.items.length === 0 ? (
          <EmptyState icon={Tractor} title="Ainda não há produtos à venda" />
        ) : (
          <div className={productGrid}>
            {latest.data.items.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        )}
      </Section>
    </>
  )
}
