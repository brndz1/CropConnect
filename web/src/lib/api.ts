// Calls to the API gateway. The app is served from the same origin (nginx or the Vite proxy forwards /api), so
// requests are relative and the browser never needs CORS.

const TOKEN_KEY = "cropconnect.token"
const REFRESH_TOKEN_KEY = "cropconnect.refreshToken"

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// What the login, the refresh and the password change return: a short-lived JWT and the refresh token for the next.
export interface TokenPair {
  token: string
  refreshToken: string
  expiresIn: number
}

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  getRefresh: () => localStorage.getItem(REFRESH_TOKEN_KEY),
  set: (pair: Pick<TokenPair, "token" | "refreshToken">) => {
    localStorage.setItem(TOKEN_KEY, pair.token)
    localStorage.setItem(REFRESH_TOKEN_KEY, pair.refreshToken)
  },
  clear: () => {
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(REFRESH_TOKEN_KEY)
  },
}

// Raised when the session cannot go on: the refresh token expired or was revoked, or the account was deactivated.
export const SESSION_EXPIRED_EVENT = "cropconnect:session-expired"
// Raised with the new JWT after a refresh, so the signed-in state follows it.
export const SESSION_REFRESHED_EVENT = "cropconnect:session-refreshed"

type Query = Record<string, string | number | boolean | undefined | null>

function withQuery(path: string, query?: Query) {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== "") params.set(key, String(value))
  }
  const text = params.toString()
  return text ? `${path}?${text}` : path
}

// Problem details (RFC 9457) carry the reason in detail; validation problems list the fields in errors.
async function readError(response: Response): Promise<ApiError> {
  let message = "Ocorreu um erro inesperado."
  try {
    const body = await response.json()
    const fieldErrors = body.errors ? Object.values(body.errors as Record<string, string[]>).flat() : []
    message = fieldErrors[0] ?? body.detail ?? body.title ?? message
  } catch {
    if (response.status >= 500) message = "O serviço não está disponível. Tente de novo dentro de momentos."
  }
  if (response.status === 429) message = "Demasiados pedidos. Aguarde um pouco e tente de novo."
  return new ApiError(response.status, message)
}

let refreshing: Promise<string | null> | null = null

// Exchanges the refresh token for a new pair. A refresh token works only once, so the tabs of the browser take turns
// (Web Locks): a tab that waited finds the pair another tab already got and uses it instead of refreshing again.
export function refreshSession(): Promise<string | null> {
  if (refreshing) return refreshing
  const used = tokenStore.getRefresh()
  if (!used) return Promise.resolve(null)

  const run = async () => {
    const current = tokenStore.getRefresh()
    if (!current) return null
    if (current !== used) return tokenStore.get()
    try {
      const response = await fetch("/api/auth/refresh", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: current }),
      })
      if (!response.ok) {
        // Only a refusal ends the session; with the server unreachable the next call tries again.
        if (response.status === 400 || response.status === 401) tokenStore.clear()
        return null
      }
      const pair = (await response.json()) as TokenPair
      tokenStore.set(pair)
      window.dispatchEvent(new CustomEvent(SESSION_REFRESHED_EVENT, { detail: pair.token }))
      return pair.token
    } catch {
      return null
    }
  }

  refreshing = (navigator.locks ? navigator.locks.request("cropconnect.refresh", run) : run()).finally(() => {
    refreshing = null
  })
  return refreshing
}

async function send(method: string, path: string, token: string | null, body?: unknown, query?: Query) {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers["Content-Type"] = "application/json"
  if (token) headers.Authorization = `Bearer ${token}`

  try {
    return await fetch(withQuery(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, "Sem ligação ao servidor. Verifique a ligação e tente de novo.")
  }
}

async function request<T>(method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const token = tokenStore.get()
  let response = await send(method, path, token, body, query)

  // An expired token is replaced once with the refresh token and the call is repeated.
  if (response.status === 401 && token) {
    const refreshed = await refreshSession()
    if (refreshed) {
      response = await send(method, path, refreshed, body, query)
    }
    if (response.status === 401) {
      window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
    }
  }
  if (!response.ok) throw await readError(response)
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, query?: Query) => request<T>("GET", path, undefined, query),
  post: <T>(path: string, body?: unknown) => request<T>("POST", path, body ?? {}),
  put: <T>(path: string, body?: unknown) => request<T>("PUT", path, body ?? {}),
  patch: <T>(path: string, body?: unknown) => request<T>("PATCH", path, body ?? {}),
  delete: <T = void>(path: string) => request<T>("DELETE", path),
}

export function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Ocorreu um erro inesperado."
}
