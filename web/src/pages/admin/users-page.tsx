import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Loader2, Plus, Search, UserCheck, UserX, Users } from "lucide-react"
import { useEffect, useState, type FormEvent } from "react"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { FieldError } from "@/components/field-error"
import { PageHeader } from "@/components/page-header"
import { EmptyState, ErrorState, LoadingList } from "@/components/states"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { api, errorMessage } from "@/lib/api"
import { useAuth } from "@/lib/auth"
import { roleLabel } from "@/lib/format"
import type { Paged, Role, UserAccount } from "@/lib/types"

const roles: Role[] = ["Client", "Manager", "Admin"]

function CreateUserDialog() {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState({ username: "", email: "", password: "", role: "Client" as Role })
  const [error, setError] = useState<string>()
  const create = useMutation({
    mutationFn: () =>
      api.post<UserAccount>("/api/users", { ...form, username: form.username.trim(), email: form.email.trim() }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users"] })
      toast.success("Utilizador criado.")
      setOpen(false)
    },
    onError: (e) => setError(errorMessage(e)),
  })

  function submit(event: FormEvent) {
    event.preventDefault()
    if (form.username.trim().length < 3) return setError("O nome de utilizador tem de ter pelo menos 3 caracteres.")
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) return setError("Introduza um email válido.")
    if (form.password.length < 8) return setError("A palavra-passe tem de ter pelo menos 8 caracteres.")
    setError(undefined)
    create.mutate()
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setForm({ username: "", email: "", password: "", role: "Client" })
          setError(undefined)
        }
      }}
    >
      <DialogTrigger asChild>
        <Button>
          <Plus /> Novo utilizador
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="font-heading text-brand-900">Novo utilizador</DialogTitle>
          <DialogDescription>A pessoa entra com este email e palavra-passe.</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} className="grid gap-4" noValidate>
          <div className="grid gap-2">
            <Label htmlFor="new-username">Nome de utilizador</Label>
            <Input
              id="new-username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="new-email">Email</Label>
            <Input
              id="new-email"
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="new-password">Palavra-passe</Label>
            <Input
              id="new-password"
              type="password"
              autoComplete="new-password"
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
            />
          </div>
          <div className="grid gap-2">
            <Label>Perfil</Label>
            <Select value={form.role} onValueChange={(value) => setForm({ ...form, role: value as Role })}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {roles.map((role) => (
                  <SelectItem key={role} value={role}>
                    {roleLabel[role]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <FieldError message={error} />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending && <Loader2 className="animate-spin" />}
              Criar
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function UserRow({ user, self }: { user: UserAccount; self: boolean }) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["users"] })
  const changeRole = useMutation({
    mutationFn: (role: Role) => api.patch<UserAccount>(`/api/users/${user.id}`, { role }),
    onSuccess: () => {
      refresh()
      toast.success("Perfil alterado.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })
  const setActive = useMutation({
    mutationFn: (isActive: boolean) => api.put<UserAccount>(`/api/users/${user.id}/active`, { isActive }),
    onSuccess: (updated) => {
      refresh()
      toast.success(updated.isActive ? "Conta reativada." : "Conta desativada.")
    },
    onError: (e) => toast.error(errorMessage(e)),
  })

  return (
    <li className="flex flex-wrap items-center gap-4 rounded-xl border p-4">
      <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-secondary font-heading font-semibold text-brand-900">
        {user.username.charAt(0).toUpperCase()}
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-heading font-semibold text-brand-900">
          {user.username} {self && <span className="text-sm font-normal text-muted-foreground">(você)</span>}
        </p>
        <p className="truncate text-sm text-muted-foreground">{user.email}</p>
      </div>
      {!user.isActive && (
        <span className="inline-flex items-center gap-1 rounded-full bg-destructive/10 px-3 py-1 font-heading text-xs font-semibold text-destructive">
          <UserX className="size-3.5" aria-hidden /> Desativada
        </span>
      )}
      <Select
        value={user.role}
        onValueChange={(value) => changeRole.mutate(value as Role)}
        disabled={self || changeRole.isPending}
      >
        <SelectTrigger className="w-40" size="sm" aria-label={`Perfil de ${user.username}`}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {roles.map((role) => (
            <SelectItem key={role} value={role}>
              {roleLabel[role]}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {!self &&
        (user.isActive ? (
          <ConfirmDialog
            trigger={
              <Button
                variant="outline"
                size="sm"
                className="border-destructive text-destructive hover:bg-destructive/10"
              >
                <UserX /> Desativar
              </Button>
            }
            title={`Desativar ${user.username}?`}
            description="A pessoa deixa de conseguir entrar e perde logo o acesso. Pode reativar a conta a qualquer momento."
            confirmLabel="Desativar"
            destructive
            onConfirm={() => setActive.mutate(false)}
          />
        ) : (
          <Button variant="outline" size="sm" onClick={() => setActive.mutate(true)} disabled={setActive.isPending}>
            <UserCheck /> Reativar
          </Button>
        ))}
    </li>
  )
}

// RF22: list, search, create, edit and deactivate users.
export function UsersPage() {
  const { session } = useAuth()
  const [search, setSearch] = useState("")
  const [query, setQuery] = useState("")
  const [role, setRole] = useState<Role | "all">("all")

  useEffect(() => {
    const timer = window.setTimeout(() => setQuery(search.trim()), 300)
    return () => window.clearTimeout(timer)
  }, [search])

  const users = useQuery({
    queryKey: ["users", query, role],
    queryFn: () =>
      api.get<Paged<UserAccount>>("/api/users", {
        search: query || undefined,
        role: role === "all" ? undefined : role,
        pageSize: 50,
      }),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <PageHeader title="Utilizadores" actions={<CreateUserDialog />} />
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="relative min-w-60 flex-1">
          <Search
            className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
          <Input
            type="search"
            placeholder="Procurar por nome ou email…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="rounded-full bg-muted pl-12"
            aria-label="Procurar utilizadores"
          />
        </div>
        <Select value={role} onValueChange={(value) => setRole(value as Role | "all")}>
          <SelectTrigger className="w-48" aria-label="Filtrar por perfil">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Todos os perfis</SelectItem>
            {roles.map((r) => (
              <SelectItem key={r} value={r}>
                {roleLabel[r]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      {users.isPending ? (
        <LoadingList />
      ) : users.isError ? (
        <ErrorState error={users.error} onRetry={() => users.refetch()} />
      ) : users.data.items.length === 0 ? (
        <EmptyState icon={Users} title="Nenhum utilizador encontrado" />
      ) : (
        <ul className="grid gap-3">
          {users.data.items.map((user) => (
            <UserRow key={user.id} user={user} self={user.id === session?.userId} />
          ))}
        </ul>
      )}
    </>
  )
}
