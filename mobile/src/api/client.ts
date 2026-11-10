// Typed client for the API gateway, generated from docs/openapi.json (schema.d.ts) with openapi-fetch.
import createClient from "openapi-fetch"
import { apiBaseUrl } from "./config"
import { ApiError, NETWORK_MESSAGE, toApiError } from "./errors"
import type { paths } from "./schema"
import { transport } from "./transport"

export const client = createClient<paths>({ baseUrl: apiBaseUrl, fetch: (request) => transport(request) })

interface FetchResult<T> {
  data?: T
  error?: unknown
  response: Response
}

// Awaits an openapi-fetch call and returns its data, or throws an ApiError with the message for the user.
export async function call<T>(request: Promise<FetchResult<T>>): Promise<T> {
  let result: FetchResult<T>
  try {
    result = await request
  } catch (error) {
    if (error instanceof ApiError) throw error
    throw new ApiError(0, NETWORK_MESSAGE)
  }
  if (!result.response.ok) throw toApiError(result.response.status, result.error)
  return result.data as T
}
