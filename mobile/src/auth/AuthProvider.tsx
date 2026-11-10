import { useQueryClient } from "@tanstack/react-query"
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react"
import { AppState } from "react-native"
import { call, client } from "@/api/client"
import { apiBaseUrl } from "@/api/config"
import { setTransport } from "@/api/transport"
import type { TokenPair } from "@/api/types"
import { createAuthTransport } from "./authTransport"
import { decodeSession, refreshDelay, type Session } from "./session"
import { tokenStorage, type StoredTokens } from "./tokenStorage"

export type AuthStatus = "restoring" | "signedOut" | "signedIn"

export interface RegisterInput {
  name: string
  email: string
  password: string
}

interface AuthContextValue {
  status: AuthStatus
  session: Session | null
  // True after the session ended on its own; the app shows the "A sessão expirou" dialog (D-sessao, RNF06).
  sessionExpired: boolean
  login: (email: string, password: string) => Promise<Session>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
  // Replaces the tokens, e.g. with the ones returned after changing the password.
  applyTokens: (pair: TokenPair) => Promise<Session>
  dismissSessionExpired: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

// Splits "Marta Ferreira" into the first and last names the API asks for.
export function splitName(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  const firstName = parts[0] ?? ""
  return { firstName, lastName: parts.slice(1).join(" ") || firstName }
}

// The prototype asks only for the name; the API also needs a unique username, so it comes from the email.
export function usernameFromEmail(email: string) {
  const normalized = email.trim().toLowerCase()
  if (normalized.length <= 50) return normalized
  return `${normalized.split("@")[0]?.slice(0, 40) ?? "cliente"}${Date.now() % 100000}`
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>("restoring")
  const [session, setSession] = useState<Session | null>(null)
  const [sessionExpired, setSessionExpired] = useState(false)
  const tokens = useRef<StoredTokens | null>(null)

  const signOutLocally = useCallback(async () => {
    tokens.current = null
    await tokenStorage.clear()
    setSession(null)
    setStatus("signedOut")
    queryClient.clear()
  }, [queryClient])

  const expire = useCallback(async () => {
    if (!tokens.current && status !== "signedIn") return
    await signOutLocally()
    setSessionExpired(true)
  }, [signOutLocally, status])

  const store = useCallback(async (pair: StoredTokens) => {
    const next = decodeSession(pair.token)
    if (!next) throw new Error("A sessão recebida não é válida.")
    tokens.current = { token: pair.token, refreshToken: pair.refreshToken }
    await tokenStorage.save(tokens.current)
    setSession(next)
    setStatus("signedIn")
    return next
  }, [])

  // Exchanges the refresh token for a new pair. Only a refusal ends the session; with the server unreachable the
  // next call tries again.
  const exchange = useCallback(async (): Promise<string | null> => {
    const current = tokens.current
    if (!current) return null
    try {
      const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: current.refreshToken }),
      })
      if (!response.ok) {
        if (response.status === 400 || response.status === 401) {
          tokens.current = null
          await tokenStorage.clear()
        }
        return null
      }
      const pair = (await response.json()) as TokenPair
      await store(pair)
      return pair.token
    } catch {
      return null
    }
  }, [store])

  // One refresh at a time: a refresh token works only once (rotation), so concurrent calls share the result.
  const pendingRefresh = useRef<Promise<string | null> | null>(null)
  const refresh = useCallback(() => {
    pendingRefresh.current ??= exchange().finally(() => {
      pendingRefresh.current = null
    })
    return pendingRefresh.current
  }, [exchange])

  // Every API call goes through the transport that adds the token and refreshes it on a 401.
  useEffect(() => {
    setTransport(
      createAuthTransport({
        getToken: () => tokens.current?.token ?? null,
        refresh,
        onUnauthorized: (reason) => {
          if (reason === "rejected" || !tokens.current) void expire()
        },
      }),
    )
  }, [refresh, expire])

  // Restores the session saved on the device (an expired JWT still counts while the refresh token works).
  useEffect(() => {
    let active = true
    void (async () => {
      const saved = await tokenStorage.load()
      if (!active) return
      if (!saved || !decodeSession(saved.token)) {
        setStatus("signedOut")
        return
      }
      tokens.current = saved
      const restored = decodeSession(saved.token)
      if (restored && restored.expiresAt > Date.now()) {
        setSession(restored)
        setStatus("signedIn")
        return
      }
      const token = await refresh()
      if (!active) return
      if (token) return
      if (tokens.current) {
        // Offline: keep the session and let the screens show the network error.
        setSession(restored)
        setStatus("signedIn")
      } else {
        setStatus("signedOut")
      }
    })()
    return () => {
      active = false
    }
  }, [refresh])

  // Shortly before the JWT expires a new one is fetched, and again when the app comes back to the foreground.
  useEffect(() => {
    if (!session) return
    const run = async () => {
      const token = await refresh()
      if (!token && !tokens.current) void expire()
    }
    const timer = setTimeout(run, refreshDelay(session))
    const subscription = AppState.addEventListener("change", (state) => {
      if (state === "active" && refreshDelay(session) === 0) void run()
    })
    return () => {
      clearTimeout(timer)
      subscription.remove()
    }
  }, [session, refresh, expire])

  const login = useCallback(
    async (email: string, password: string) => {
      const pair = await call(client.POST("/api/auth/login", { body: { email: email.trim().toLowerCase(), password } }))
      setSessionExpired(false)
      return store(pair)
    },
    [store],
  )

  const register = useCallback(async ({ name, email, password }: RegisterInput) => {
    const { firstName, lastName } = splitName(name)
    await call(
      client.POST("/api/auth/register", {
        body: {
          username: usernameFromEmail(email),
          email: email.trim().toLowerCase(),
          password,
          role: "Client",
          firstName,
          lastName,
        },
      }),
    )
  }, [])

  // Ends the session on the server too, so its refresh token cannot be used again.
  const logout = useCallback(async () => {
    const refreshToken = tokens.current?.refreshToken
    if (refreshToken) {
      void client.POST("/api/auth/logout", { body: { refreshToken } }).catch(() => undefined)
    }
    await signOutLocally()
  }, [signOutLocally])

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      session,
      sessionExpired,
      login,
      register,
      logout,
      applyTokens: store,
      dismissSessionExpired: () => setSessionExpired(false),
    }),
    [status, session, sessionExpired, login, register, logout, store],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error("useAuth must be used inside AuthProvider")
  return context
}

// The signed-in session; only for screens behind the login.
export function useSession() {
  const { session } = useAuth()
  if (!session) throw new Error("useSession needs a signed-in user")
  return session
}
