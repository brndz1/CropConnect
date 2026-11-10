import { decodeBase64Url, decodeSession, refreshDelay, REFRESH_AHEAD_MS } from "../session"

// UTF-8 bytes of the JSON as a binary string, then base64url.
const encode = (value: object) =>
  btoa(unescape(encodeURIComponent(JSON.stringify(value))))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "")

const token = (payload: object) => `header.${encode(payload)}.signature`

describe("session", () => {
  it("decodes base64url with UTF-8 characters", () => {
    expect(decodeBase64Url(encode({ name: "João Gonçalves" }))).toBe('{"name":"João Gonçalves"}')
  })

  it("reads the user, the role and the expiry from the JWT", () => {
    const session = decodeSession(token({ sub: "u1", email: "marta@mail.pt", role: "Client", exp: 1_800_000_000 }))
    expect(session).toEqual({
      token: expect.any(String),
      userId: "u1",
      email: "marta@mail.pt",
      role: "Client",
      expiresAt: 1_800_000_000_000,
    })
  })

  it("refuses tokens without a known role or expiry", () => {
    expect(decodeSession(token({ sub: "u1", role: "Root", exp: 1 }))).toBeNull()
    expect(decodeSession(token({ sub: "u1", role: "Admin" }))).toBeNull()
    expect(decodeSession("not-a-jwt")).toBeNull()
  })

  it("refreshes a minute before the expiry, or right away", () => {
    const now = 1_000_000
    const session = { token: "t", userId: "u", email: "", role: "Manager" as const, expiresAt: now + 15 * 60_000 }
    expect(refreshDelay(session, now)).toBe(15 * 60_000 - REFRESH_AHEAD_MS)
    expect(refreshDelay({ ...session, expiresAt: now - 1 }, now)).toBe(0)
  })
})
