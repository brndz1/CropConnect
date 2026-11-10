// Sends the requests of the API client. The authentication layer replaces it to add the token (src/auth).
export type Transport = (request: Request) => Promise<Response>

let current: Transport = (request) => fetch(request)

export const transport: Transport = (request) => current(request)

export function setTransport(next: Transport) {
  current = next
}
