import { CheckCircle2, Loader2, MailCheck, XCircle } from "lucide-react"
import { useEffect, useRef, useState, type FormEvent } from "react"
import { Link, useSearchParams } from "react-router-dom"
import { toast } from "sonner"
import { FieldError } from "@/components/field-error"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { api, ApiError, errorMessage } from "@/lib/api"
import { AuthLayout } from "./auth-layout"

const linkClass = "font-semibold text-primary underline-offset-4 hover:underline"

const validEmail = (email: string) => /^\S+@\S+\.\S+$/.test(email.trim())

// The API answers the same whether the email has an account or not, so the message never says which.
export function ResendVerificationButton({ email }: { email: string }) {
  const [busy, setBusy] = useState(false)

  async function resend() {
    setBusy(true)
    try {
      await api.post("/api/auth/resend-verification", { email: email.trim() })
      toast.success("Se a conta existir e ainda não estiver confirmada, enviámos um novo email.")
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Button type="button" variant="outline" className="w-full" disabled={busy || !validEmail(email)} onClick={resend}>
      {busy && <Loader2 className="animate-spin" />}
      Reenviar email de confirmação
    </Button>
  )
}

// After registering: the account only works once the link in the email is opened.
export function CheckInbox({ email }: { email: string }) {
  return (
    <div className="grid gap-4">
      <div className="flex items-start gap-3 rounded-xl bg-secondary p-4">
        <MailCheck className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
        <p className="text-sm">
          Enviámos um email para <strong>{email}</strong>. Abra o link que lá está para ativar a conta; depois já pode
          entrar. Se não o encontrar, veja também a pasta de spam.
        </p>
      </div>
      <ResendVerificationButton email={email} />
      <Button asChild className="w-full">
        <Link to="/entrar">Ir para Entrar</Link>
      </Button>
    </div>
  )
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("")
  const [error, setError] = useState<string>()
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!validEmail(email)) {
      setError("Introduza um email válido.")
      return
    }
    setBusy(true)
    setError(undefined)
    try {
      await api.post("/api/auth/forgot-password", { email: email.trim() })
      setSent(true)
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Recuperar palavra-passe" description="Enviamos-lhe um link para escolher uma nova.">
      {sent ? (
        <div className="grid gap-4">
          <div className="flex items-start gap-3 rounded-xl bg-secondary p-4">
            <MailCheck className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
            <p className="text-sm">
              Se existir uma conta com <strong>{email.trim()}</strong>, receberá um email com um link válido durante 1
              hora. Veja também a pasta de spam.
            </p>
          </div>
          <Button asChild className="w-full">
            <Link to="/entrar">Voltar a Entrar</Link>
          </Button>
        </div>
      ) : (
        <form onSubmit={submit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="email">Email da conta</Label>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              aria-invalid={!!error}
            />
          </div>
          <FieldError message={error} />
          <Button type="submit" disabled={busy || !email} className="mt-2 w-full">
            {busy && <Loader2 className="animate-spin" />}
            Enviar link
          </Button>
        </form>
      )}
      <p className="mt-6 text-center text-sm text-muted-foreground">
        Lembrou-se?{" "}
        <Link to="/entrar" className={linkClass}>
          Entrar
        </Link>
      </p>
    </AuthLayout>
  )
}

// Opened from the reset email and from the invitation an administrator sends.
export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get("token") ?? ""
  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [error, setError] = useState<string>()
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (password.length < 8) return setError("A palavra-passe tem de ter pelo menos 8 caracteres.")
    if (password !== confirm) return setError("As palavras-passe não coincidem.")
    setBusy(true)
    setError(undefined)
    try {
      await api.post("/api/auth/reset-password", { token, newPassword: password })
      setDone(true)
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 400 && e.message.includes("link")
          ? "Este link é inválido ou expirou. Peça um novo."
          : errorMessage(e),
      )
    } finally {
      setBusy(false)
    }
  }

  if (!token) {
    return (
      <AuthLayout title="Link incompleto" description="Abra o link exatamente como está no email.">
        <Button asChild className="w-full">
          <Link to="/recuperar-palavra-passe">Pedir um novo link</Link>
        </Button>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Nova palavra-passe" description="Escolha a palavra-passe com que vai entrar.">
      {done ? (
        <div className="grid gap-4">
          <div className="flex items-start gap-3 rounded-xl bg-secondary p-4">
            <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
            <p className="text-sm">Palavra-passe definida. As sessões abertas com a anterior foram terminadas.</p>
          </div>
          <Button asChild className="w-full">
            <Link to="/entrar">Entrar</Link>
          </Button>
        </div>
      ) : (
        <form onSubmit={submit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="password">Nova palavra-passe</Label>
            <Input
              id="password"
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
            <p className="text-sm text-muted-foreground">Pelo menos 8 caracteres.</p>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="confirm">Repetir palavra-passe</Label>
            <Input
              id="confirm"
              type="password"
              autoComplete="new-password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              aria-invalid={!!error}
            />
          </div>
          <FieldError message={error} />
          <Button type="submit" disabled={busy || !password || !confirm} className="mt-2 w-full">
            {busy && <Loader2 className="animate-spin" />}
            Guardar palavra-passe
          </Button>
          {error?.includes("expirou") && (
            <Link to="/recuperar-palavra-passe" className={`text-center text-sm ${linkClass}`}>
              Pedir um novo link
            </Link>
          )}
        </form>
      )}
    </AuthLayout>
  )
}

// Opened from the verification email: confirms the address as soon as the page loads.
export function VerifyEmailPage() {
  const [params] = useSearchParams()
  const token = params.get("token") ?? ""
  const [state, setState] = useState<"working" | "done" | "failed">(token ? "working" : "failed")
  const [email, setEmail] = useState("")
  const started = useRef(false)

  useEffect(() => {
    // React's development mode runs effects twice; the token works only once.
    if (!token || started.current) return
    started.current = true
    api
      .post("/api/auth/verify-email", { token })
      .then(() => setState("done"))
      .catch(() => setState("failed"))
  }, [token])

  if (state === "working") {
    return (
      <AuthLayout title="A confirmar o email" description="Só um momento.">
        <Loader2 className="mx-auto size-8 animate-spin text-primary" aria-label="A confirmar" />
      </AuthLayout>
    )
  }

  if (state === "done") {
    return (
      <AuthLayout title="Email confirmado" description="A sua conta está ativa.">
        <div className="grid gap-4">
          <div className="flex items-start gap-3 rounded-xl bg-secondary p-4">
            <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-primary" aria-hidden />
            <p className="text-sm">Obrigado! Já pode entrar na CropConnect.</p>
          </div>
          <Button asChild className="w-full">
            <Link to="/entrar">Entrar</Link>
          </Button>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Link inválido ou expirado" description="Peça um novo email de confirmação.">
      <div className="grid gap-4">
        <div className="flex items-start gap-3 rounded-xl bg-destructive/10 p-4">
          <XCircle className="mt-0.5 size-5 shrink-0 text-destructive" aria-hidden />
          <p className="text-sm">
            O link já foi usado ou passou o prazo de 24 horas. Se a conta já estiver confirmada, basta entrar.
          </p>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="email">Email da conta</Label>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        <ResendVerificationButton email={email} />
        <Button asChild variant="ghost" className="w-full">
          <Link to="/entrar">Ir para Entrar</Link>
        </Button>
      </div>
    </AuthLayout>
  )
}
