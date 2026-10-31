import { useQueryClient } from "@tanstack/react-query"
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import { toast } from "sonner"
import { api, SESSION_EXPIRED_EVENT, tokenStore } from "@/lib/api"
import type { Role } from "@/lib/types"

export interface Session {
  token: string
  userId: string
  email: string
  role: Role
  expiresAt: number
}

interface AuthContextValue {
  session: Session | null
  login: (email: string, password: string) => Promise<Session>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// The JWT is only read here to choose the navigation; the gateway checks its signature on every call.
function decode(token: string): Session | null {
  try {
    const payload = JSON.parse(atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")))
    const session = {
      token,
      userId: payload.sub as string,
      email: payload.email as string,
      role: payload.role as Role,
      expiresAt: (payload.exp as number) * 1000,
    }
    return session.expiresAt > Date.now() ? session : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(() => {
    const token = tokenStore.get()
    return token ? decode(token) : null
  })

  const logout = useCallback(() => {
    tokenStore.clear()
    setSession(null)
    queryClient.clear()
  }, [queryClient])

  const login = useCallback(async (email: string, password: string) => {
    const { token } = await api.post<{ token: string }>("/api/auth/login", { email, password })
    const next = decode(token)
    if (!next) throw new Error("A sessão recebida não é válida.")
    tokenStore.set(token)
    setSession(next)
    return next
  }, [])

  // The session ends when the token expires or the gateway refuses it (RNF06).
  useEffect(() => {
    if (!session) return
    const expire = () => {
      logout()
      toast.error("A sessão expirou. Inicie sessão de novo.")
    }
    const timer = window.setTimeout(expire, Math.max(0, session.expiresAt - Date.now()))
    window.addEventListener(SESSION_EXPIRED_EVENT, expire)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener(SESSION_EXPIRED_EVENT, expire)
    }
  }, [session, logout])

  const value = useMemo(() => ({ session, login, logout }), [session, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error("useAuth must be used inside AuthProvider")
  return context
}

// Home screen of each profile (RF03).
export function homeFor(role: Role) {
  return role === "Manager" ? "/gestor" : role === "Admin" ? "/admin" : "/"
}
