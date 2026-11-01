import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Loader2, LogOut, Pencil } from "lucide-react"
import { useState, type FormEvent } from "react"
import { useNavigate } from "react-router-dom"
import { toast } from "sonner"
import { PageHeader } from "@/components/page-header"
import { ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { api, ApiError, errorMessage } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { passwordError } from "@/lib/passwords"
import { roleLabel } from "@/lib/format"
import { cn } from "@/lib/utils"
import type { Profile, UserAccount } from "@/lib/types"

// Read-only until "Editar"; "Guardar" only works once something changed. Accounts without a profile yet
// (created before registration asked for the names) start in edit mode.
function ProfileForm({ userId, profile }: { userId: string; profile: Profile | null }) {
  const queryClient = useQueryClient()
  const saved = {
    firstName: profile?.firstName ?? "",
    lastName: profile?.lastName ?? "",
    phoneNumber: profile?.phoneNumber ?? "",
  }
  const [form, setForm] = useState(saved)
  const [editing, setEditing] = useState(!profile)
  const changed =
    form.firstName.trim() !== saved.firstName ||
    form.lastName.trim() !== saved.lastName ||
    form.phoneNumber.trim() !== saved.phoneNumber

  const save = useMutation({
    mutationFn: () => {
      const body = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        phoneNumber: form.phoneNumber.trim(),
      }
      return profile
        ? api.put<Profile>(`/api/users/${userId}/profile`, body)
        : api.post<Profile>(`/api/users/${userId}/profile`, body)
    },
    onSuccess: (next) => {
      queryClient.setQueryData(["profile", userId], next)
      setForm({ firstName: next.firstName, lastName: next.lastName, phoneNumber: next.phoneNumber })
      setEditing(false)
      toast.success("Dados guardados.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!changed) return
    if (form.phoneNumber && !/^\+?[0-9 ]{6,19}$/.test(form.phoneNumber.trim())) {
      toast.error("O número de telefone não é válido.")
      return
    }
    save.mutate()
  }

  function cancel() {
    setForm(saved)
    setEditing(false)
  }

  const canSave = editing && changed && !save.isPending && !!form.firstName.trim() && !!form.lastName.trim()

  return (
    <form onSubmit={submit} className="grid gap-4 rounded-xl border p-4 sm:p-6">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-lg font-semibold">Dados pessoais</h2>
        {!editing && (
          <Button type="button" variant="outline" size="sm" onClick={() => setEditing(true)}>
            <Pencil /> Editar
          </Button>
        )}
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="firstName">Nome</Label>
          <Input
            id="firstName"
            autoComplete="given-name"
            maxLength={100}
            value={form.firstName}
            onChange={(e) => setForm({ ...form, firstName: e.target.value })}
            readOnly={!editing}
            className={cn(!editing && "bg-muted text-muted-foreground")}
            required
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="lastName">Apelido</Label>
          <Input
            id="lastName"
            autoComplete="family-name"
            maxLength={100}
            value={form.lastName}
            onChange={(e) => setForm({ ...form, lastName: e.target.value })}
            readOnly={!editing}
            className={cn(!editing && "bg-muted text-muted-foreground")}
            required
          />
        </div>
      </div>
      <div className="grid gap-2">
        <Label htmlFor="phone">Telefone</Label>
        <Input
          id="phone"
          type="tel"
          autoComplete="tel"
          placeholder={editing ? "+351 912 345 678" : undefined}
          value={form.phoneNumber}
          onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
          readOnly={!editing}
          className={cn(!editing && "bg-muted text-muted-foreground")}
        />
      </div>
      {editing && (
        <div className="flex gap-2">
          <Button
            type="submit"
            disabled={!canSave}
            className={cn(!canSave && "bg-muted text-muted-foreground disabled:opacity-100")}
          >
            {save.isPending && <Loader2 className="animate-spin" />}
            Guardar
          </Button>
          {profile && (
            <Button type="button" variant="ghost" onClick={cancel} disabled={save.isPending}>
              Cancelar
            </Button>
          )}
        </div>
      )}
    </form>
  )
}

// The old password is asked again; the API returns a new token because the current one stops working.
function ChangePasswordForm() {
  const { applyToken } = useAuth()
  const [current, setCurrent] = useState("")
  const [next, setNext] = useState("")
  const [confirm, setConfirm] = useState("")
  const change = useMutation({
    mutationFn: () =>
      api.post<{ token: string }>("/api/auth/change-password", { currentPassword: current, newPassword: next }),
    onSuccess: ({ token }) => {
      applyToken(token)
      setCurrent("")
      setNext("")
      setConfirm("")
      toast.success("Palavra-passe alterada. Enviámos-lhe um email a confirmar.")
    },
    onError: (e) => toast.error(passwordError(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (next.length < 8) return toast.error("A nova palavra-passe tem de ter pelo menos 8 caracteres.")
    if (next === current) return toast.error("A nova palavra-passe tem de ser diferente da atual.")
    if (next !== confirm) return toast.error("As palavras-passe não coincidem.")
    change.mutate()
  }

  return (
    <form onSubmit={submit} className="grid gap-4 rounded-xl border p-4" noValidate>
      <h2 className="font-heading text-lg font-semibold text-brand-900">Alterar palavra-passe</h2>
      <div className="grid gap-2">
        <Label htmlFor="current-password">Palavra-passe atual</Label>
        <Input
          id="current-password"
          type="password"
          autoComplete="current-password"
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
        />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="new-password">Nova palavra-passe</Label>
          <Input
            id="new-password"
            type="password"
            autoComplete="new-password"
            value={next}
            onChange={(e) => setNext(e.target.value)}
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="confirm-password">Repetir nova palavra-passe</Label>
          <Input
            id="confirm-password"
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
          />
        </div>
      </div>
      <Button type="submit" className="w-fit" disabled={change.isPending || !current || !next || !confirm}>
        {change.isPending && <Loader2 className="animate-spin" />}
        Alterar palavra-passe
      </Button>
    </form>
  )
}

export function ProfilePage() {
  const { session, logout } = useAuth()
  const navigate = useNavigate()
  const userId = session!.userId
  const account = useQuery({
    queryKey: ["account", userId],
    queryFn: () => api.get<UserAccount>(`/api/users/${userId}`),
  })
  const profile = useQuery({
    queryKey: ["profile", userId],
    queryFn: async () => {
      try {
        return await api.get<Profile>(`/api/users/${userId}/profile`)
      } catch (e) {
        if (e instanceof ApiError && e.status === 404) return null
        throw e
      }
    },
  })

  if (account.isPending || profile.isPending) return <LoadingList rows={2} />
  if (account.isError) return <ErrorState error={account.error} onRetry={() => account.refetch()} />

  return (
    <>
      <PageHeader title="Perfil" />
      <div className="grid max-w-2xl gap-6">
        <div className="flex items-center gap-4 rounded-xl bg-muted p-4">
          <span className="flex size-14 items-center justify-center rounded-full bg-primary font-heading text-xl font-semibold text-primary-foreground">
            {account.data.username.charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="font-heading text-lg font-semibold text-brand-900">{account.data.username}</p>
            <p className="truncate text-sm text-muted-foreground">
              {account.data.email} · {roleLabel[account.data.role]}
            </p>
          </div>
        </div>

        <ProfileForm userId={userId} profile={profile.data ?? null} />

        <ChangePasswordForm />

        <Button
          variant="outline"
          className="w-fit"
          onClick={() => {
            logout()
            navigate("/entrar")
          }}
        >
          <LogOut /> Terminar sessão
        </Button>
      </div>
    </>
  )
}
