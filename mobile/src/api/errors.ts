// Errors of the gateway are RFC 9457 problem details; validation problems list the fields in `errors` (docs/api.md).

export class ApiError extends Error {
  readonly status: number
  readonly detail: string | undefined

  constructor(status: number, message: string, detail?: string) {
    super(message)
    this.name = "ApiError"
    this.status = status
    this.detail = detail
  }

  get isNetwork() {
    return this.status === 0
  }
}

interface ProblemDetails {
  status?: number
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export const NETWORK_MESSAGE = "Sem ligação à internet. Verifique a ligação e tente de novo."
export const UNAVAILABLE_MESSAGE = "O serviço não está disponível. Tente de novo dentro de momentos."
export const UNEXPECTED_MESSAGE = "Ocorreu um erro inesperado."

// The message shown to the user for a failed call.
export function problemMessage(status: number, body: unknown): string {
  if (status === 429) return "Demasiados pedidos. Aguarde um pouco e tente de novo."
  if (status >= 500 && status !== 501) return UNAVAILABLE_MESSAGE
  const problem = (body && typeof body === "object" ? body : {}) as ProblemDetails
  const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : []
  return fieldErrors[0] ?? problem.detail ?? problem.title ?? UNEXPECTED_MESSAGE
}

export function toApiError(status: number, body: unknown): ApiError {
  const problem = (body && typeof body === "object" ? body : {}) as ProblemDetails
  return new ApiError(status, problemMessage(status, body), problem.detail)
}

export function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message) return error.message
  return UNEXPECTED_MESSAGE
}

export const isApiError = (error: unknown, status?: number): error is ApiError =>
  error instanceof ApiError && (status === undefined || error.status === status)
