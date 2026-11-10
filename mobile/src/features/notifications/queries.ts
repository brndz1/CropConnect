import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"

// The notification centre (RF19), newest first with the unread count, refreshed every 30 s.
export function useNotifications() {
  return useQuery({
    queryKey: queryKeys.notifications(),
    queryFn: () => call(client.GET("/api/notifications", { params: { query: { page: 1, pageSize: 50 } } })),
    refetchInterval: 30_000,
  })
}

export function useMarkRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => call(client.POST("/api/notifications/{id}/read", { params: { path: { id } } })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.notifications() }),
  })
}

export function useMarkAllRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => call(client.POST("/api/notifications/read-all")),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: queryKeys.notifications() }),
  })
}
