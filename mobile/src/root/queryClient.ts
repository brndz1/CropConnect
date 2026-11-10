import { QueryClient } from "@tanstack/react-query"
import { ApiError } from "@/api/errors"

// A client error (4xx) will not change on a retry; network failures and 5xx get two more tries.
export function shouldRetry(failureCount: number, error: unknown) {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
  return failureCount < 2
}

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: shouldRetry },
      mutations: { retry: false },
    },
  })
}
