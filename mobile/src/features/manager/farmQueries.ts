import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"
import type { DayOfWeek, Farm, Product, ProductType } from "@/api/types"

// The farm's own products, hidden ones included (RF06).
export function useFarmProducts(farmId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.products({ farmId, includeUnavailable: true }),
    queryFn: () =>
      call(
        client.GET("/api/products", {
          params: { query: { farmId, includeUnavailable: true, page: 1, pageSize: 100 } },
        }),
      ),
    enabled: Boolean(farmId),
    select: (page) => page.items,
  })
}

function useInvalidateCatalog() {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: ["products"] })
    void queryClient.invalidateQueries({ queryKey: ["product"] })
  }
}

export interface ProductInput {
  name: string
  description: string | null
  unit: string
  price: number
  stockQuantity: number
  type: ProductType
  availableUntil: string | null
  contents: string | null
}

export function useCreateProduct(farmId: string) {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: (input: ProductInput) => call(client.POST("/api/products", { body: { ...input, farmId } })),
    onSuccess: invalidate,
  })
}

export function useUpdateProduct() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: ({
      product,
      changes,
    }: {
      product: Pick<Product, "id" | "version">
      changes: Partial<
        Pick<Product, "name" | "description" | "unit" | "price" | "isAvailable" | "availableUntil" | "contents">
      >
    }) =>
      call(
        client.PATCH("/api/products/{id}", {
          params: { path: { id: product.id } },
          body: {
            name: changes.name ?? null,
            description: changes.description ?? null,
            unit: changes.unit ?? null,
            price: changes.price ?? null,
            isAvailable: changes.isAvailable ?? null,
            availableUntil: changes.availableUntil ?? null,
            contents: changes.contents ?? null,
            expectedVersion: product.version,
          },
        }),
      ),
    onSuccess: invalidate,
  })
}

// Stock is changed by adjustments, so two people can add and remove at the same time (RF06, G16).
export function useAdjustStock() {
  const invalidate = useInvalidateCatalog()
  return useMutation({
    mutationFn: ({ productId, delta }: { productId: string; delta: number }) =>
      call(
        client.POST("/api/products/{id}/stock-adjustments", { params: { path: { id: productId } }, body: { delta } }),
      ),
    onSuccess: invalidate,
  })
}

function useInvalidateFarms() {
  const queryClient = useQueryClient()
  return (farm?: Farm) => {
    if (farm) queryClient.setQueryData(queryKeys.farm(farm.id), farm)
    void queryClient.invalidateQueries({ queryKey: ["farms"] })
    void queryClient.invalidateQueries({ queryKey: ["farm"] })
  }
}

export interface FarmInput {
  name: string
  description: string
  location: string
  latitude: number | null
  longitude: number | null
}

export function useCreateFarm() {
  const invalidate = useInvalidateFarms()
  return useMutation({
    mutationFn: (input: FarmInput) => call(client.POST("/api/farms", { body: input })),
    onSuccess: invalidate,
  })
}

export function useUpdateFarm() {
  const invalidate = useInvalidateFarms()
  return useMutation({
    mutationFn: ({ farm, input }: { farm: Pick<Farm, "id" | "version">; input: FarmInput }) =>
      call(
        client.PATCH("/api/farms/{id}", {
          params: { path: { id: farm.id } },
          body: { ...input, expectedVersion: farm.version },
        }),
      ),
    onSuccess: invalidate,
  })
}

export interface ScheduleChange {
  create: { dayOfWeek: DayOfWeek; openTime: string; closeTime: string }[]
  update: { id: string; version: number; dayOfWeek: DayOfWeek; openTime: string; closeTime: string }[]
  remove: string[]
}

// Applies the pickup days and hours of G21 as creates, updates and removals of the farm's schedules (RF07).
export function useSaveSchedules(farmId: string) {
  const invalidate = useInvalidateFarms()
  return useMutation({
    mutationFn: async (change: ScheduleChange) => {
      for (const id of change.remove) {
        await call(client.DELETE("/api/pickup-schedules/{id}", { params: { path: { id } } }))
      }
      for (const item of change.update) {
        await call(
          client.PATCH("/api/pickup-schedules/{id}", {
            params: { path: { id: item.id } },
            body: {
              dayOfWeek: item.dayOfWeek,
              openTime: item.openTime,
              closeTime: item.closeTime,
              expectedVersion: item.version,
            },
          }),
        )
      }
      for (const item of change.create) {
        await call(client.POST("/api/farms/{farmId}/pickup-schedules", { params: { path: { farmId } }, body: item }))
      }
    },
    onSuccess: () => invalidate(),
  })
}
