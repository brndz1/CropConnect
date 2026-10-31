// Calls to the API gateway. The app is served from the same origin (nginx or the Vite proxy forwards /api), so
// requests are relative and the browser never needs CORS.

const TOKEN_KEY = "cropconnect.token"

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token: string) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

// Raised when the gateway answers 401 to a signed-in user: the session expired or the account was deactivated.
export const SESSION_EXPIRED_EVENT = "cropconnect:session-expired"

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
  return new ApiError(response.status, message)
}

async function request<T>(method: string, path: string, body?: unknown, query?: Query): Promise<T> {
  const token = tokenStore.get()
  const headers: Record<string, string> = {}
  if (body !== undefined) headers["Content-Type"] = "application/json"
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(withQuery(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, "Sem ligação ao servidor. Verifique a ligação e tente de novo.")
  }

  if (response.status === 401 && token) {
    window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
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
