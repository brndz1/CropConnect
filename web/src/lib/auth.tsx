import { useQueryClient } from "@tanstack/react-query"
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import { toast } from "sonner"
import {
  api,
  refreshSession,
  SESSION_EXPIRED_EVENT,
  SESSION_REFRESHED_EVENT,
  tokenStore,
  type TokenPair,
} from "@/lib/api"
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
  // Replaces the tokens, e.g. with the ones returned after changing the password.
  applyTokens: (pair: TokenPair) => Session
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// How long before the JWT expires the app gets the next one with the refresh token.
const REFRESH_AHEAD_MS = 60_000

// The JWT is only read here to choose the navigation; the gateway checks its signature on every call.
// An expired one still counts while there is a refresh token to replace it.
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
    return session.expiresAt > Date.now() || tokenStore.getRefresh() ? session : null
  } catch {
    return null
  }
}

function storedSession() {
  const token = tokenStore.get()
  return token ? decode(token) : null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = useState<Session | null>(storedSession)

  const clear = useCallback(() => {
    tokenStore.clear()
    setSession(null)
    queryClient.clear()
  }, [queryClient])

  // Ends the session on the server too, so its refresh token cannot be used again.
  const logout = useCallback(() => {
    const refreshToken = tokenStore.getRefresh()
    if (refreshToken) {
      void fetch("/api/auth/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      }).catch(() => undefined)
    }
    clear()
  }, [clear])

  const applyTokens = useCallback((pair: TokenPair) => {
    const next = decode(pair.token)
    if (!next) throw new Error("A sessão recebida não é válida.")
    tokenStore.set(pair)
    setSession(next)
    return next
  }, [])

  const login = useCallback(
    async (email: string, password: string) =>
      applyTokens(await api.post<TokenPair>("/api/auth/login", { email, password })),
    [applyTokens],
  )

  // Shortly before the JWT expires a new one is fetched with the refresh token; the session only ends when that is
  // refused (expired or revoked session, deactivated account) or the gateway refuses the token (RNF06).
  useEffect(() => {
    if (!session) return
    const expire = () => {
      clear()
      toast.error("A sessão expirou. Inicie sessão de novo.")
    }
    const refresh = async () => {
      const token = await refreshSession()
      if (!token && !tokenStore.getRefresh()) expire()
    }
    const onRefreshed = (event: Event) => setSession(decode((event as CustomEvent<string>).detail))
    // Another tab logged in, out or refreshed.
    const onStorage = () => setSession(storedSession())

    const timer = window.setTimeout(refresh, Math.max(0, session.expiresAt - Date.now() - REFRESH_AHEAD_MS))
    window.addEventListener(SESSION_EXPIRED_EVENT, expire)
    window.addEventListener(SESSION_REFRESHED_EVENT, onRefreshed)
    window.addEventListener("storage", onStorage)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener(SESSION_EXPIRED_EVENT, expire)
      window.removeEventListener(SESSION_REFRESHED_EVENT, onRefreshed)
      window.removeEventListener("storage", onStorage)
    }
  }, [session, clear])

  const value = useMemo(() => ({ session, login, applyTokens, logout }), [session, login, applyTokens, logout])
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
