import { Loader2 } from "lucide-react"
import { useState, type FormEvent } from "react"
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { ApiError, errorMessage } from "@/lib/api"
import { homeFor, useAuth } from "@/lib/auth"
import { FieldError } from "@/components/field-error"
import { ResendVerificationButton } from "./account-pages"
import { AuthLayout } from "./auth-layout"

// Auth answers 403 with this detail when the password is right but the email was never confirmed.
const unverified = (e: unknown) =>
  e instanceof ApiError && e.status === 403 && e.message.startsWith("Confirm your email")

export function LoginPage() {
  const { session, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [error, setError] = useState<string>()
  const [needsVerification, setNeedsVerification] = useState(false)
  const [busy, setBusy] = useState(false)

  if (session) return <Navigate to={homeFor(session.role)} replace />

  async function submit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(undefined)
    setNeedsVerification(false)
    try {
      const next = await login(email.trim(), password)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from ?? homeFor(next.role), { replace: true })
    } catch (e) {
      setNeedsVerification(unverified(e))
      setError(
        e instanceof ApiError && e.status === 401
          ? "Email ou palavra-passe incorretos."
          : unverified(e)
            ? "Confirme o seu email antes de entrar: abra o link que lhe enviámos."
            : errorMessage(e),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Entrar" description="Inicie sessão para encomendar ou gerir a sua quinta.">
      <form onSubmit={submit} className="grid gap-4" noValidate>
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            placeholder="joao@exemplo.pt"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div className="grid gap-2">
          <div className="flex items-center justify-between">
            <Label htmlFor="password">Palavra-passe</Label>
            <Link
              to="/recuperar-palavra-passe"
              className="text-sm font-semibold text-primary underline-offset-4 hover:underline"
            >
              Esqueci-me da palavra-passe
            </Link>
          </div>
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-invalid={!!error}
            required
          />
        </div>
        <FieldError message={error} />
        <Button type="submit" disabled={busy || !email || !password} className="mt-2 w-full">
          {busy && <Loader2 className="animate-spin" />}
          Entrar
        </Button>
        {needsVerification && <ResendVerificationButton email={email} />}
      </form>
      <p className="mt-6 text-center text-sm text-muted-foreground">
        Ainda não tem conta?{" "}
        <Link to="/registar" className="font-semibold text-primary underline-offset-4 hover:underline">
          Criar conta
        </Link>
      </p>
    </AuthLayout>
  )
}
