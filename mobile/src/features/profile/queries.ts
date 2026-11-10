import { useQuery } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { isApiError } from "@/api/errors"
import { queryKeys } from "@/api/queryKeys"
import type { Profile } from "@/api/types"
import { useSession } from "@/auth/AuthProvider"

// The signed-in user's profile; null when the account has none yet (the API answers 404).
export function useMyProfile() {
  const { userId } = useSession()
  return useQuery({
    queryKey: queryKeys.profile(userId),
    queryFn: async (): Promise<Profile | null> => {
      try {
        return await call(client.GET("/api/users/{id}/profile", { params: { path: { id: userId } } }))
      } catch (error) {
        if (isApiError(error, 404)) return null
        throw error
      }
    },
    staleTime: 5 * 60_000,
  })
}

// "Marta Ferreira", or the start of the email while there is no profile.
export function displayName(profile: Profile | null | undefined, email: string) {
  const name = profile ? `${profile.firstName} ${profile.lastName}`.trim() : ""
  return name || email.split("@")[0] || email
}
