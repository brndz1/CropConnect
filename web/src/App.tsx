import { lazy, Suspense, type ReactNode } from "react"
import { Navigate, Route, Routes, useLocation } from "react-router-dom"
import { AppShell } from "@/components/layout/app-shell"
import { LoadingList } from "@/components/states"
import { homeFor, useAuth } from "@/lib/auth"
import type { Role } from "@/lib/types"
import { LoginPage } from "@/pages/auth/login-page"
import { RegisterPage } from "@/pages/auth/register-page"
import { CartPage } from "@/pages/client/cart-page"
import { CatalogPage } from "@/pages/client/catalog-page"
import { FarmPage } from "@/pages/client/farm-page"
import { HomePage } from "@/pages/client/home-page"
import { OrdersPage } from "@/pages/client/orders-page"
import { ProductPage } from "@/pages/client/product-page"
import { NotFoundPage } from "@/pages/shared/not-found-page"
import { NotificationsPage } from "@/pages/shared/notifications-page"
import { OrderDetailPage } from "@/pages/shared/order-detail-page"
import { ProfilePage } from "@/pages/shared/profile-page"

// The manager's and the administrator's screens (and the charts) load only when one of them signs in.
const AdminFarmsPage = lazy(() => import("@/pages/admin/farms-page").then((m) => ({ default: m.AdminFarmsPage })))
const ThresholdsPage = lazy(() => import("@/pages/admin/thresholds-page").then((m) => ({ default: m.ThresholdsPage })))
const UsersPage = lazy(() => import("@/pages/admin/users-page").then((m) => ({ default: m.UsersPage })))
const DashboardPage = lazy(() => import("@/pages/manager/dashboard-page").then((m) => ({ default: m.DashboardPage })))
const FarmEditPage = lazy(() => import("@/pages/manager/farm-edit-page").then((m) => ({ default: m.FarmEditPage })))
const ManagerFarmsPage = lazy(() => import("@/pages/manager/farms-page").then((m) => ({ default: m.ManagerFarmsPage })))
const ManagerOrdersPage = lazy(() =>
  import("@/pages/manager/orders-page").then((m) => ({ default: m.ManagerOrdersPage })),
)
const ManagerProductsPage = lazy(() =>
  import("@/pages/manager/products-page").then((m) => ({ default: m.ManagerProductsPage })),
)
const SensorsPage = lazy(() => import("@/pages/manager/sensors-page").then((m) => ({ default: m.SensorsPage })))

// Signed-in users of the given profiles only; others go to their own home (RF03).
function RequireRole({ roles, children }: { roles?: Role[]; children: ReactNode }) {
  const { session } = useAuth()
  const location = useLocation()
  if (!session) return <Navigate to="/entrar" state={{ from: location.pathname + location.search }} replace />
  if (roles && !roles.includes(session.role)) return <Navigate to={homeFor(session.role)} replace />
  return <>{children}</>
}

// The catalog is public; managers and administrators start on their own screens.
function PublicHome() {
  const { session } = useAuth()
  if (session && session.role !== "Client") return <Navigate to={homeFor(session.role)} replace />
  return <HomePage />
}

export default function App() {
  return (
    <Suspense fallback={<LoadingList />}>
      <Routes>
        <Route path="/entrar" element={<LoginPage />} />
        <Route path="/registar" element={<RegisterPage />} />
        <Route element={<AppShell />}>
          <Route index element={<PublicHome />} />
          <Route path="explorar" element={<CatalogPage />} />
          <Route path="produtos/:id" element={<ProductPage />} />
          <Route path="quintas/:id" element={<FarmPage />} />
          <Route path="carrinho" element={<CartPage />} />

          <Route
            path="encomendas"
            element={
              <RequireRole roles={["Client"]}>
                <OrdersPage />
              </RequireRole>
            }
          />
          <Route
            path="encomendas/:id"
            element={
              <RequireRole roles={["Client"]}>
                <OrderDetailPage />
              </RequireRole>
            }
          />
          <Route
            path="notificacoes"
            element={
              <RequireRole>
                <NotificationsPage />
              </RequireRole>
            }
          />
          <Route
            path="perfil"
            element={
              <RequireRole>
                <ProfilePage />
              </RequireRole>
            }
          />

          <Route
            path="gestor"
            element={
              <RequireRole roles={["Manager"]}>
                <DashboardPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/encomendas"
            element={
              <RequireRole roles={["Manager"]}>
                <ManagerOrdersPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/encomendas/:id"
            element={
              <RequireRole roles={["Manager", "Admin"]}>
                <OrderDetailPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/produtos"
            element={
              <RequireRole roles={["Manager"]}>
                <ManagerProductsPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/sensores"
            element={
              <RequireRole roles={["Manager"]}>
                <SensorsPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/quintas"
            element={
              <RequireRole roles={["Manager"]}>
                <ManagerFarmsPage />
              </RequireRole>
            }
          />
          <Route
            path="gestor/quintas/:id"
            element={
              <RequireRole roles={["Manager"]}>
                <FarmEditPage />
              </RequireRole>
            }
          />

          <Route
            path="admin"
            element={
              <RequireRole roles={["Admin"]}>
                <UsersPage />
              </RequireRole>
            }
          />
          <Route
            path="admin/quintas"
            element={
              <RequireRole roles={["Admin"]}>
                <AdminFarmsPage />
              </RequireRole>
            }
          />
          <Route
            path="admin/limites"
            element={
              <RequireRole roles={["Admin"]}>
                <ThresholdsPage />
              </RequireRole>
            }
          />

          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
