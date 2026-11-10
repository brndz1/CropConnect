import { ApiError } from "@/api/errors"
import { shouldRetry } from "../queryClient"

describe("query retries", () => {
  it("does not retry client errors", () => {
    expect(shouldRetry(0, new ApiError(404, "not found"))).toBe(false)
    expect(shouldRetry(0, new ApiError(422, "rule"))).toBe(false)
  })

  it("retries network and server failures twice", () => {
    expect(shouldRetry(0, new ApiError(0, "offline"))).toBe(true)
    expect(shouldRetry(1, new ApiError(503, "down"))).toBe(true)
    expect(shouldRetry(2, new ApiError(503, "down"))).toBe(false)
  })
})
