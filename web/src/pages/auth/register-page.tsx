import { Check, Loader2, ShoppingBasket, Tractor } from "lucide-react"
import { useState, type FormEvent } from "react"
import { Link, Navigate } from "react-router-dom"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { api, ApiError, errorMessage } from "@/lib/api"
import { homeFor, useAuth } from "@/lib/auth"
import { cn } from "@/lib/utils"
import { FieldError } from "@/components/field-error"
import { CheckInbox } from "./account-pages"
import { AuthLayout } from "./auth-layout"

type Kind = "Client" | "Manager"

const kinds: { value: Kind; title: string; description: string; icon: typeof Tractor }[] = [
  { value: "Client", title: "Cliente", description: "Compro produtos locais", icon: ShoppingBasket },
  { value: "Manager", title: "Produtor", description: "Vendo o que cultivo", icon: Tractor },
]

export function RegisterPage() {
  const { session } = useAuth()
  const [kind, setKind] = useState<Kind>("Client")
  const [firstName, setFirstName] = useState("")
  const [lastName, setLastName] = useState("")
  const [username, setUsername] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [consent, setConsent] = useState(false)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [registered, setRegistered] = useState<string>()

  if (session) return <Navigate to={homeFor(session.role)} replace />

  // The account works once the email is confirmed (link in the verification email).
  if (registered) {
    return (
      <AuthLayout title="Confirme o seu email" description="Falta só um passo para ativar a conta.">
        <CheckInbox email={registered} />
      </AuthLayout>
    )
  }

  function validate() {
    const next: Record<string, string> = {}
    if (!firstName.trim()) next.firstName = "Indique o nome."
    if (!lastName.trim()) next.lastName = "Indique o apelido."
    if (username.trim().length < 3) next.username = "O nome de utilizador tem de ter pelo menos 3 caracteres."
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) next.email = "Introduza um email válido."
    if (password.length < 8) next.password = "A palavra-passe tem de ter pelo menos 8 caracteres."
    else if (confirm !== password) next.confirm = "As palavras-passe não coincidem."
    if (!consent) next.consent = "Tem de aceitar o tratamento dos dados para criar a conta."
    setErrors(next)
    return Object.keys(next).length === 0
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!validate()) return
    setBusy(true)
    try {
      await api.post("/api/auth/register", {
        username: username.trim(),
        email: email.trim(),
        password,
        role: kind,
        firstName: firstName.trim(),
        lastName: lastName.trim(),
      })
      toast.success("Conta criada. Confirme o email para entrar.")
      setRegistered(email.trim())
    } catch (e) {
      setErrors({
        form:
          e instanceof ApiError && e.status === 409
            ? "Já existe uma conta com este email ou nome de utilizador."
            : errorMessage(e),
      })
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Criar conta" description="Escolha como vai usar a CropConnect.">
      <form onSubmit={submit} className="grid gap-4" noValidate>
        <div className="grid grid-cols-2 gap-3" role="radiogroup" aria-label="Tipo de conta">
          {kinds.map((option) => {
            const selected = kind === option.value
            return (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={selected}
                aria-label={`${option.title}: ${option.description}`}
                onClick={() => setKind(option.value)}
                className={cn(
                  "relative flex flex-col items-start gap-1 rounded-xl border p-4 text-left transition-colors",
                  selected ? "border-2 border-primary bg-secondary" : "hover:bg-muted",
                )}
              >
                {selected && <Check className="absolute top-3 right-3 size-4 text-primary" aria-hidden />}
                <option.icon className="size-6 text-primary" aria-hidden />
                <span className="font-heading font-semibold text-brand-900">{option.title}</span>
                <span className="text-xs text-muted-foreground">{option.description}</span>
              </button>
            )
          })}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="grid gap-2">
            <Label htmlFor="firstName">Nome</Label>
            <Input
              id="firstName"
              autoComplete="given-name"
              maxLength={100}
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
              aria-invalid={!!errors.firstName}
            />
            <FieldError message={errors.firstName} />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="lastName">Apelido</Label>
            <Input
              id="lastName"
              autoComplete="family-name"
              maxLength={100}
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
              aria-invalid={!!errors.lastName}
            />
            <FieldError message={errors.lastName} />
          </div>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="username">Nome de utilizador</Label>
          <Input
            id="username"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            aria-invalid={!!errors.username}
          />
          <FieldError message={errors.username} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="email">Email</Label>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-invalid={!!errors.email}
          />
          <FieldError message={errors.email} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="password">Palavra-passe</Label>
          <Input
            id="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-invalid={!!errors.password}
          />
          <p className="text-sm text-muted-foreground">Pelo menos 8 caracteres.</p>
          <FieldError message={errors.password} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="confirm">Confirmar palavra-passe</Label>
          <Input
            id="confirm"
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            aria-invalid={!!errors.confirm}
          />
          <FieldError message={errors.confirm} />
        </div>

        {/* RGPD consent (RNF10). */}
        <div className="grid gap-2">
          <div className="flex items-start gap-3">
            <Checkbox
              id="consent"
              checked={consent}
              onCheckedChange={(v) => setConsent(v === true)}
              className="mt-0.5 size-5"
            />
            <Label htmlFor="consent" className="text-sm leading-5 font-normal">
              Aceito que a CropConnect trate os meus dados para gerir a conta e as encomendas, nos termos do RGPD.
            </Label>
          </div>
          <FieldError message={errors.consent} />
        </div>

        <FieldError message={errors.form} />
        <Button type="submit" disabled={busy} className="mt-2 w-full">
          {busy && <Loader2 className="animate-spin" />}
          Criar conta
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-muted-foreground">
        Já tem conta?{" "}
        <Link to="/entrar" className="font-semibold text-primary underline-offset-4 hover:underline">
          Entrar
        </Link>
      </p>
    </AuthLayout>
  )
}
