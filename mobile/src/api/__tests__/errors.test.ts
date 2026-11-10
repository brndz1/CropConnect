import { ApiError, errorMessage, problemMessage, toApiError, UNAVAILABLE_MESSAGE } from "../errors"

describe("problem details", () => {
  it("prefers the first field error of a validation problem", () => {
    const body = { title: "One or more validation errors occurred.", errors: { Email: ["O email não é válido."] } }
    expect(problemMessage(400, body)).toBe("O email não é válido.")
  })

  it("uses the detail of a business rule", () => {
    const body = { status: 422, title: "Business rule violated", detail: "Insufficient stock for product 'Batata'." }
    const error = toApiError(422, body)
    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(422)
    expect(error.message).toBe("Insufficient stock for product 'Batata'.")
  })

  it("never shows the detail of a server failure", () => {
    expect(problemMessage(503, { detail: "connection refused" })).toBe(UNAVAILABLE_MESSAGE)
  })

  it("explains the rate limit", () => {
    expect(problemMessage(429, null)).toMatch(/Demasiados pedidos/)
  })

  it("marks network failures", () => {
    expect(new ApiError(0, "offline").isNetwork).toBe(true)
    expect(errorMessage("not an error")).toBe("Ocorreu um erro inesperado.")
  })
})
