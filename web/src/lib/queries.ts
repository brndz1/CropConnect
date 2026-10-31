import { useQuery } from "@tanstack/react-query"
import { api } from "@/lib/api"
import type { Farm, NotificationPage, Paged } from "@/lib/types"

// Queries several screens share.

export function useFarms(options: { includeInactive?: boolean } = {}) {
  return useQuery({
    queryKey: ["farms", options],
    queryFn: () => api.get<Paged<Farm>>("/api/farms", { pageSize: 100, includeInactive: options.includeInactive }),
    staleTime: 60_000,
  })
}

// Farm names for orders, which only carry the farm's ID.
export function useFarmNames() {
  const { data } = useFarms()
  return new Map(data?.items.map((farm) => [farm.id, farm.name]))
}

export function useMyFarms(enabled = true) {
  return useQuery({
    queryKey: ["farms", "mine"],
    queryFn: () => api.get<Paged<Farm>>("/api/farms/mine", { pageSize: 100 }),
    enabled,
  })
}

// Polled, so the badges follow new orders and alerts without a reload.
export function useUnreadNotifications(enabled: boolean) {
  return useQuery({
    queryKey: ["notifications", "unread"],
    queryFn: () => api.get<NotificationPage>("/api/notifications", { pageSize: 1 }),
    select: (page) => page.unreadCount,
    refetchInterval: 30_000,
    enabled,
  })
}
