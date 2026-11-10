import type { Transport } from "@/api/transport"

interface AuthTransportOptions {
  getToken: () => string | null
  // Exchanges the refresh token for a new JWT; null when the session cannot go on.
  refresh: () => Promise<string | null>
  // "refresh-failed": no new token (refused, or the server unreachable); "rejected": the gateway refused even the
  // refreshed token (a deactivated account).
  onUnauthorized: (reason: "refresh-failed" | "rejected") => void
  send?: Transport
}

function withToken(request: Request, token: string | null) {
  const headers = new Headers(request.headers)
  if (token) headers.set("Authorization", `Bearer ${token}`)
  return new Request(request, { headers })
}

// Adds the JWT to every call. A 401 with a token is retried once with a refreshed token; if that fails too the
// session ends (RNF06).
export function createAuthTransport({ getToken, refresh, onUnauthorized, send = fetch }: AuthTransportOptions) {
  const transport: Transport = async (request) => {
    const token = getToken()
    const retry = token ? request.clone() : null
    const response = await send(withToken(request, token))
    if (response.status !== 401 || !retry) return response

    const refreshed = await refresh()
    if (!refreshed) {
      onUnauthorized("refresh-failed")
      return response
    }
    const second = await send(withToken(retry, refreshed))
    if (second.status === 401) onUnauthorized("rejected")
    return second
  }
  return transport
}
