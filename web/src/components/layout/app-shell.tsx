import {
  Bell,
  ClipboardList,
  Gauge,
  House,
  LogOut,
  Package,
  Search,
  Settings,
  ShoppingCart,
  ThermometerSun,
  Tractor,
  User,
  Users,
  type LucideIcon,
} from "lucide-react"
import { Link, NavLink, Outlet, useNavigate } from "react-router-dom"
import symbol from "@/assets/symbol.png"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { homeFor, useAuth } from "@/lib/auth"
import { useCart } from "@/lib/cart"
import { roleLabel } from "@/lib/format"
import { useUnreadNotifications } from "@/lib/queries"
import type { Role } from "@/lib/types"
import { cn } from "@/lib/utils"

interface Destination {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
  // Only in the top bar: the bottom navigation keeps four destinations; the account menu links the rest.
  desktopOnly?: boolean
}

// Four destinations per profile in the bottom navigation (report section 6.5); the top bar shows them on wider
// screens. Visitors browse the catalog without an account.
const destinations: Record<Role | "Guest", Destination[]> = {
  Guest: [
    { to: "/", label: "Início", icon: House, end: true },
    { to: "/explorar", label: "Explorar", icon: Search },
  ],
  Client: [
    { to: "/", label: "Início", icon: House, end: true },
    { to: "/explorar", label: "Explorar", icon: Search },
    { to: "/encomendas", label: "Encomendas", icon: ClipboardList },
    { to: "/perfil", label: "Perfil", icon: User },
  ],
  Manager: [
    { to: "/gestor", label: "Painel", icon: Gauge, end: true },
    { to: "/gestor/encomendas", label: "Encomendas", icon: ClipboardList },
    { to: "/gestor/produtos", label: "Produtos", icon: Package },
    { to: "/gestor/sensores", label: "Sensores", icon: ThermometerSun },
    { to: "/gestor/quintas", label: "Quintas", icon: Tractor, desktopOnly: true },
  ],
  Admin: [
    { to: "/admin", label: "Utilizadores", icon: Users, end: true },
    { to: "/admin/quintas", label: "Quintas", icon: Tractor },
    { to: "/admin/limites", label: "Limites", icon: Settings },
    { to: "/perfil", label: "Perfil", icon: User },
  ],
}

function CountBadge({ count }: { count: number | undefined }) {
  if (!count) return null
  return (
    <span className="absolute -top-1 -right-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-terracotta px-1 font-heading text-[11px] font-semibold text-white">
      {count > 99 ? "99+" : count}
    </span>
  )
}

export function AppShell() {
  const { session, logout } = useAuth()
  const { count: cartCount } = useCart()
  const navigate = useNavigate()
  const role = session?.role ?? "Guest"
  const { data: unread } = useUnreadNotifications(!!session)
  const items = destinations[role]

  return (
    <div className="flex min-h-svh flex-col bg-background">
      <header className="sticky top-0 z-40 border-b bg-background/95 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 lg:px-6">
          <Link to={session ? homeFor(session.role) : "/"} className="flex items-center gap-2" aria-label="CropConnect">
            <img src={symbol} alt="" className="size-9" />
            <span className="font-heading text-lg font-bold text-primary">CropConnect</span>
          </Link>

          <nav className="ml-6 hidden items-center gap-1 md:flex" aria-label="Navegação principal">
            {items.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    "rounded-full px-4 py-2 font-heading text-sm font-medium text-muted-foreground transition-colors hover:text-brand-900",
                    isActive && "bg-secondary text-brand-900",
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-1">
            {session && (
              <Button variant="ghost" size="icon" asChild className="relative">
                <Link to="/notificacoes" aria-label={`Notificações${unread ? `, ${unread} por ler` : ""}`}>
                  <Bell className="size-5" />
                  <CountBadge count={unread} />
                </Link>
              </Button>
            )}
            {(role === "Client" || role === "Guest") && (
              <Button variant="ghost" size="icon" asChild className="relative">
                <Link to="/carrinho" aria-label={`Carrinho${cartCount ? `, ${cartCount} artigos` : ""}`}>
                  <ShoppingCart className="size-5" />
                  <CountBadge count={cartCount} />
                </Link>
              </Button>
            )}
            {session ? (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="icon" aria-label="Conta">
                    <span className="flex size-9 items-center justify-center rounded-full bg-primary font-heading text-sm font-semibold text-primary-foreground">
                      {session.email.charAt(0).toUpperCase()}
                    </span>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-60">
                  <DropdownMenuLabel>
                    <p className="truncate font-heading text-sm text-brand-900">{session.email}</p>
                    <p className="text-xs font-normal text-muted-foreground">{roleLabel[session.role]}</p>
                  </DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onSelect={() => navigate("/perfil")}>
                    <User /> Perfil
                  </DropdownMenuItem>
                  {session.role === "Manager" && (
                    <DropdownMenuItem onSelect={() => navigate("/gestor/quintas")}>
                      <Tractor /> As minhas quintas
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuItem
                    onSelect={() => {
                      logout()
                      navigate("/entrar")
                    }}
                  >
                    <LogOut /> Terminar sessão
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            ) : (
              <Button size="sm" asChild className="ml-2">
                <Link to="/entrar">Entrar</Link>
              </Button>
            )}
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-6xl flex-1 px-4 pt-6 pb-28 md:pb-12 lg:px-6">
        <Outlet />
      </main>

      <nav
        className="fixed inset-x-0 bottom-0 z-40 border-t bg-background pb-[env(safe-area-inset-bottom)] md:hidden"
        aria-label="Navegação inferior"
      >
        <div className="mx-auto flex h-16 max-w-md items-stretch justify-around">
          {items
            .filter((item) => !item.desktopOnly)
            .map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    "flex min-w-16 flex-1 flex-col items-center justify-center gap-1 text-xs text-muted-foreground",
                    isActive && "text-brand-900",
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span className={cn("rounded-full px-4 py-1", isActive && "bg-secondary")}>
                      <item.icon className="size-5" aria-hidden />
                    </span>
                    <span className={cn("font-heading", isActive && "font-semibold")}>{item.label}</span>
                  </>
                )}
              </NavLink>
            ))}
        </div>
      </nav>
    </div>
  )
}
