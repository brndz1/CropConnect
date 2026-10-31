import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Loader2, LogOut } from "lucide-react"
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
import { roleLabel } from "@/lib/format"
import type { Profile, UserAccount } from "@/lib/types"

function ProfileForm({ userId, profile }: { userId: string; profile: Profile | null }) {
  const queryClient = useQueryClient()
  const [form, setForm] = useState({
    firstName: profile?.firstName ?? "",
    lastName: profile?.lastName ?? "",
    phoneNumber: profile?.phoneNumber ?? "",
  })

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
    onSuccess: (saved) => {
      queryClient.setQueryData(["profile", userId], saved)
      toast.success("Dados guardados.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (form.phoneNumber && !/^\+?[0-9 ]{6,19}$/.test(form.phoneNumber.trim())) {
      toast.error("O número de telefone não é válido.")
      return
    }
    save.mutate()
  }

  return (
    <form onSubmit={submit} className="grid gap-4 rounded-xl border p-4 sm:p-6">
      <h2 className="text-lg font-semibold">Dados pessoais</h2>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="grid gap-2">
          <Label htmlFor="firstName">Nome</Label>
          <Input
            id="firstName"
            autoComplete="given-name"
            maxLength={100}
            value={form.firstName}
            onChange={(e) => setForm({ ...form, firstName: e.target.value })}
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
          placeholder="+351 912 345 678"
          value={form.phoneNumber}
          onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
        />
      </div>
      <Button
        type="submit"
        className="w-fit"
        disabled={save.isPending || !form.firstName.trim() || !form.lastName.trim()}
      >
        {save.isPending && <Loader2 className="animate-spin" />}
        Guardar
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
