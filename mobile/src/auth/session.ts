import type { Role } from "@/api/types"

export interface Session {
  token: string
  userId: string
  email: string
  role: Role
  // Milliseconds since the epoch.
  expiresAt: number
}

const ROLES: readonly Role[] = ["Client", "Manager", "Admin"]
const ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

// Base64url without relying on atob, which not every JavaScript engine of React Native provides.
export function decodeBase64Url(input: string): string {
  const base64 = input.replace(/-/g, "+").replace(/_/g, "/")
  let bits = 0
  let buffer = 0
  const bytes: number[] = []
  for (const char of base64) {
    if (char === "=") break
    const value = ALPHABET.indexOf(char)
    if (value < 0) throw new Error("Invalid base64")
    buffer = (buffer << 6) | value
    bits += 6
    if (bits >= 8) {
      bits -= 8
      bytes.push((buffer >> bits) & 0xff)
    }
  }
  // UTF-8 bytes to a string.
  return decodeURIComponent(bytes.map((byte) => `%${byte.toString(16).padStart(2, "0")}`).join(""))
}

// Reads the claims the app needs to choose the navigation (RF03). The gateway checks the signature on every call,
// so nothing here is trusted for access control.
export function decodeSession(token: string): Session | null {
  try {
    const payload = JSON.parse(decodeBase64Url(token.split(".")[1] ?? "")) as Record<string, unknown>
    const role = ROLES.find((candidate) => candidate === payload.role)
    const userId = typeof payload.sub === "string" ? payload.sub : null
    const exp = typeof payload.exp === "number" ? payload.exp : null
    if (!role || !userId || exp === null) return null
    return {
      token,
      userId,
      email: typeof payload.email === "string" ? payload.email : "",
      role,
      expiresAt: exp * 1000,
    }
  } catch {
    return null
  }
}

// How long before the JWT expires the app gets the next one with the refresh token.
export const REFRESH_AHEAD_MS = 60_000

export function refreshDelay(session: Session, now = Date.now()) {
  return Math.max(0, session.expiresAt - now - REFRESH_AHEAD_MS)
}
