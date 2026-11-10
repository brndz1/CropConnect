import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"
import type { Order, OrderStatus } from "@/api/types"

const PAGE = 100

// The client's orders (RF13), refreshed every 30 s so the status changes of the farm show up.
export function useMyOrders() {
  return useQuery({
    queryKey: queryKeys.myOrders(),
    queryFn: () => call(client.GET("/api/orders/mine", { params: { query: { page: 1, pageSize: PAGE } } })),
    select: (page) => page.items,
    refetchInterval: 30_000,
  })
}

// Orders received by the manager's farms (RF12).
export function useFarmOrders(status?: OrderStatus) {
  return useQuery({
    queryKey: queryKeys.farmOrders({ status }),
    queryFn: () => call(client.GET("/api/orders", { params: { query: { page: 1, pageSize: PAGE, status } } })),
    select: (page) => page.items,
    refetchInterval: 30_000,
  })
}

export function useOrder(orderId: string) {
  return useQuery({
    queryKey: queryKeys.order(orderId),
    queryFn: () => call(client.GET("/api/orders/{id}", { params: { path: { id: orderId } } })),
    refetchInterval: 15_000,
  })
}

// After a change, the order and the lists that show it are refreshed.
function useOrderUpdated() {
  const queryClient = useQueryClient()
  return (order: Order) => {
    queryClient.setQueryData(queryKeys.order(order.id), order)
    void queryClient.invalidateQueries({ queryKey: ["orders"] })
    void queryClient.invalidateQueries({ queryKey: ["notifications"] })
  }
}

export function useCreateOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { items: { productId: string; quantity: number }[]; notes: string | null }) =>
      call(client.POST("/api/orders", { body })),
    onSuccess: (order) => {
      queryClient.setQueryData(queryKeys.order(order.id), order)
      void queryClient.invalidateQueries({ queryKey: ["orders"] })
      void queryClient.invalidateQueries({ queryKey: ["products"] })
    },
  })
}

export function useCancelOrder() {
  const updated = useOrderUpdated()
  return useMutation({
    mutationFn: (order: Pick<Order, "id" | "version"> & { reason?: string | null }) =>
      call(
        client.POST("/api/orders/{id}/cancel", {
          params: { path: { id: order.id } },
          body: { reason: order.reason ?? null, expectedVersion: order.version },
        }),
      ),
    onSuccess: updated,
  })
}

export function useConfirmPickup() {
  const updated = useOrderUpdated()
  return useMutation({
    mutationFn: (order: Pick<Order, "id" | "version">) =>
      call(
        client.POST("/api/orders/{id}/pickup", {
          params: { path: { id: order.id } },
          body: { expectedVersion: order.version },
        }),
      ),
    onSuccess: updated,
  })
}

export function useUpdateOrderStatus() {
  const updated = useOrderUpdated()
  return useMutation({
    mutationFn: ({ order, status }: { order: Pick<Order, "id" | "version">; status: OrderStatus }) =>
      call(
        client.PUT("/api/orders/{id}/status", {
          params: { path: { id: order.id } },
          body: { status, expectedVersion: order.version },
        }),
      ),
    onSuccess: updated,
  })
}

export function useOrderReview(orderId: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.orderReview(orderId),
    queryFn: async () => {
      const result = await client.GET("/api/orders/{orderId}/review", { params: { path: { orderId } } })
      if (result.response.status === 404) return null
      return call(Promise.resolve(result))
    },
    enabled,
  })
}

export function useCreateReview(orderId: string, farmId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { rating: number; comment: string | null }) =>
      call(client.POST("/api/orders/{orderId}/review", { params: { path: { orderId } }, body })),
    onSuccess: (review) => {
      queryClient.setQueryData(queryKeys.orderReview(orderId), review)
      void queryClient.invalidateQueries({ queryKey: queryKeys.farmReviews(farmId) })
    },
  })
}
