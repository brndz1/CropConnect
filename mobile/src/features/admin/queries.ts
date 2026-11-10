import { useInfiniteQuery, useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query"
import { getRandomBytes } from "expo-crypto"
import { call, client } from "@/api/client"
import { isApiError } from "@/api/errors"
import { queryKeys } from "@/api/queryKeys"
import type { Farm, Profile, Role, SensorType, Threshold, UserAccount } from "@/api/types"

const PAGE = 20

// Accounts, searched by name or email and filtered by role (RF22), 20 at a time.
export function useUsers(filters: { search?: string; role?: Role }) {
  return useInfiniteQuery({
    queryKey: queryKeys.users(filters),
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      call(
        client.GET("/api/users", {
          params: {
            query: { page: pageParam, pageSize: PAGE, search: filters.search || undefined, role: filters.role },
          },
        }),
      ),
    getNextPageParam: (last) => (last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined),
    select: (data) => ({
      users: data.pages.flatMap((page) => page.items) as UserAccount[],
      total: data.pages[0]?.totalCount ?? 0,
    }),
  })
}

export function useUser(userId: string) {
  return useQuery({
    queryKey: queryKeys.user(userId),
    queryFn: async () =>
      (await call(client.GET("/api/users/{id}", { params: { path: { id: userId } } }))) as UserAccount,
    enabled: Boolean(userId),
  })
}

async function fetchProfile(userId: string): Promise<Profile | null> {
  try {
    return await call(client.GET("/api/users/{id}/profile", { params: { path: { id: userId } } }))
  } catch (error) {
    if (isApiError(error, 404)) return null
    throw error
  }
}

export function useUserProfile(userId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.profile(userId ?? ""),
    queryFn: () => fetchProfile(userId ?? ""),
    enabled: Boolean(userId),
    staleTime: 5 * 60_000,
  })
}

// Profiles of the people listed, to show their names instead of the usernames.
export function useProfiles(userIds: string[]) {
  const unique = [...new Set(userIds)]
  const results = useQueries({
    queries: unique.map((id) => ({
      queryKey: queryKeys.profile(id),
      queryFn: () => fetchProfile(id),
      staleTime: 5 * 60_000,
    })),
  })
  const profiles = new Map<string, Profile | null>()
  unique.forEach((id, index) => {
    const result = results[index]
    if (result?.isSuccess) profiles.set(id, result.data)
  })
  return profiles
}

export const personName = (account: Pick<UserAccount, "username" | "email">, profile: Profile | null | undefined) =>
  (profile ? `${profile.firstName} ${profile.lastName}`.trim() : "") || account.username || account.email

function useInvalidateUsers() {
  const queryClient = useQueryClient()
  return (user?: UserAccount) => {
    if (user) queryClient.setQueryData(queryKeys.user(user.id), user)
    void queryClient.invalidateQueries({ queryKey: ["users"] })
  }
}

// A password nobody knows: the invitation email lets the person choose their own (A05).
export function invitationPassword() {
  const bytes = Array.from(getRandomBytes(18), (byte) => byte.toString(16).padStart(2, "0")).join("")
  return `Inv-${bytes}-9a`
}

export function useInviteUser() {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: async (input: { firstName: string; lastName: string; email: string; role: Role }) => {
      const email = input.email.trim().toLowerCase()
      const user = (await call(
        client.POST("/api/users", {
          body: { username: email, email, password: invitationPassword(), role: input.role },
        }),
      )) as UserAccount
      // The name goes to the new person's profile; if it cannot be saved now, they fill it in themselves.
      await call(
        client.POST("/api/users/{id}/profile", {
          params: { path: { id: user.id } },
          body: { firstName: input.firstName, lastName: input.lastName, phoneNumber: "" },
        }),
      ).catch(() => undefined)
      return user
    },
    onSuccess: (user) => invalidate(user),
  })
}

export function useUpdateUser(userId: string) {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: async (changes: { email?: string; role?: Role }) =>
      (await call(
        client.PATCH("/api/users/{id}", {
          params: { path: { id: userId } },
          body: { username: null, email: changes.email ?? null, role: changes.role ?? null },
        }),
      )) as UserAccount,
    onSuccess: invalidate,
  })
}

export function useSetUserActive(userId: string) {
  const invalidate = useInvalidateUsers()
  return useMutation({
    mutationFn: async (isActive: boolean) =>
      (await call(
        client.PUT("/api/users/{id}/active", { params: { path: { id: userId } }, body: { isActive } }),
      )) as UserAccount,
    onSuccess: invalidate,
  })
}

export function useSaveUserProfile(userId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      exists,
      body,
    }: {
      exists: boolean
      body: { firstName: string; lastName: string; phoneNumber: string }
    }) =>
      call(
        exists
          ? client.PUT("/api/users/{id}/profile", { params: { path: { id: userId } }, body })
          : client.POST("/api/users/{id}/profile", { params: { path: { id: userId } }, body }),
      ),
    onSuccess: (profile) => queryClient.setQueryData(queryKeys.profile(userId), profile),
  })
}

// Every farm, inactive ones included (RF23).
export function useAdminFarms() {
  return useQuery({
    queryKey: queryKeys.farms({ includeInactive: true }),
    queryFn: () =>
      call(client.GET("/api/farms", { params: { query: { page: 1, pageSize: 100, includeInactive: true } } })),
    select: (page) => ({ farms: page.items, total: page.totalCount }),
  })
}

export function useSetFarmActive() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ farm, isActive }: { farm: Pick<Farm, "id">; isActive: boolean }) =>
      call(client.PUT("/api/farms/{id}/active", { params: { path: { id: farm.id } }, body: { isActive } })),
    onSuccess: (farm) => {
      queryClient.setQueryData(queryKeys.farm(farm.id), farm)
      void queryClient.invalidateQueries({ queryKey: ["farms"] })
    },
  })
}

export function useUpdateThreshold() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      threshold,
      minValue,
      maxValue,
    }: {
      threshold: Pick<Threshold, "type" | "version">
      minValue: number
      maxValue: number
    }) =>
      call(
        client.PUT("/api/sensor-thresholds/{type}", {
          params: { path: { type: threshold.type as SensorType } },
          body: { minValue, maxValue, expectedVersion: threshold.version },
        }),
      ),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.thresholds() }),
  })
}
