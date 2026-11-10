import { useQueries, useQuery } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"
import type { Farm, Product, Review } from "@/api/types"

const PAGE = 100

interface ProductFilters {
  search?: string
  farmId?: string
  type?: Product["type"]
}

// The catalogue of every active farm (RF04). The page holds up to 100 items, enough for the farms of a region.
export function useProducts(filters: ProductFilters = {}) {
  return useQuery({
    queryKey: queryKeys.products(filters),
    queryFn: () =>
      call(
        client.GET("/api/products", {
          params: {
            query: {
              page: 1,
              pageSize: PAGE,
              search: filters.search || undefined,
              farmId: filters.farmId,
              type: filters.type,
            },
          },
        }),
      ),
    select: (page) => page.items,
  })
}

export function useProduct(productId: string) {
  return useQuery({
    queryKey: queryKeys.product(productId),
    queryFn: () => call(client.GET("/api/products/{id}", { params: { path: { id: productId } } })),
    enabled: Boolean(productId),
  })
}

export function useFarms(search?: string) {
  return useQuery({
    queryKey: queryKeys.farms({ search }),
    queryFn: () =>
      call(client.GET("/api/farms", { params: { query: { page: 1, pageSize: PAGE, search: search || undefined } } })),
    select: (page) => page.items,
  })
}

export function useFarm(farmId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.farm(farmId ?? ""),
    queryFn: () => call(client.GET("/api/farms/{id}", { params: { path: { id: farmId ?? "" } } })),
    enabled: Boolean(farmId),
    staleTime: 5 * 60_000,
  })
}

export function useFarmReviews(farmId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.farmReviews(farmId ?? ""),
    queryFn: () =>
      call(
        client.GET("/api/farms/{farmId}/reviews", {
          params: { path: { farmId: farmId ?? "" }, query: { page: 1, pageSize: PAGE } },
        }),
      ),
    enabled: Boolean(farmId),
    staleTime: 5 * 60_000,
  })
}

export interface Rating {
  average: number
  count: number
}

export function ratingOf(reviews: Pick<Review, "rating">[], total = reviews.length): Rating | null {
  if (reviews.length === 0) return null
  const average = reviews.reduce((sum, review) => sum + review.rating, 0) / reviews.length
  return { average: Math.round(average * 10) / 10, count: total }
}

// Ratings of several farms at once, for the cards of the catalogue (the API rates farms, not products).
export function useFarmRatings(farmIds: string[]) {
  const unique = [...new Set(farmIds)]
  const results = useQueries({
    queries: unique.map((farmId) => ({
      queryKey: queryKeys.farmReviews(farmId),
      queryFn: () =>
        call(
          client.GET("/api/farms/{farmId}/reviews", {
            params: { path: { farmId }, query: { page: 1, pageSize: PAGE } },
          }),
        ),
      staleTime: 5 * 60_000,
    })),
  })
  const ratings = new Map<string, Rating | null>()
  unique.forEach((farmId, index) => {
    const page = results[index]?.data
    if (page) ratings.set(farmId, ratingOf(page.items, page.totalCount))
  })
  return ratings
}

// Farms of the products shown, with their pickup schedules, keyed by id.
export function useFarmsById(farmIds: string[]) {
  const unique = [...new Set(farmIds)]
  const results = useQueries({
    queries: unique.map((farmId) => ({
      queryKey: queryKeys.farm(farmId),
      queryFn: () => call(client.GET("/api/farms/{id}", { params: { path: { id: farmId } } })),
      staleTime: 5 * 60_000,
    })),
  })
  const farms = new Map<string, Farm>()
  unique.forEach((farmId, index) => {
    const farm = results[index]?.data
    if (farm) farms.set(farmId, farm)
  })
  return farms
}
