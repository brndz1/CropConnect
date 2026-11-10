import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"
import type { SensorType } from "@/api/types"
import { average, periodWindows, type Period, type Point } from "@/lib/sensors"

// The global limits that raise alerts (set by the administrator, RF24).
export function useThresholds() {
  return useQuery({
    queryKey: queryKeys.thresholds(),
    queryFn: () => call(client.GET("/api/sensor-thresholds")),
    staleTime: 5 * 60_000,
  })
}

// Latest reading of each sensor type; readings arrive every 30 s.
export function useLatestReadings(farmId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.latestReadings(farmId ?? ""),
    queryFn: () =>
      call(client.GET("/api/farms/{farmId}/readings/latest", { params: { path: { farmId: farmId ?? "" } } })),
    enabled: Boolean(farmId),
    refetchInterval: 30_000,
  })
}

// The most recent readings of one type, oldest first, for the small chart of a sensor card.
export function useRecentReadings(farmId: string | undefined, type: SensorType, count = 40) {
  return useQuery({
    queryKey: queryKeys.readings(farmId ?? "", { type, count }),
    queryFn: () =>
      call(
        client.GET("/api/farms/{farmId}/readings", {
          params: { path: { farmId: farmId ?? "" }, query: { type, page: 1, pageSize: count } },
        }),
      ),
    enabled: Boolean(farmId),
    refetchInterval: 60_000,
    select: (page) => [...page.items].reverse(),
  })
}

// History of a period (G12): one point per window, the average of up to 50 readings of that window.
export function useReadingHistory(farmId: string, type: SensorType, period: Period) {
  const windows = periodWindows(period)
  const results = useQueries({
    queries: windows.map((window) => ({
      queryKey: queryKeys.readings(farmId, { type, period, from: window.from.toISOString().slice(0, 13) }),
      queryFn: () =>
        call(
          client.GET("/api/farms/{farmId}/readings", {
            params: {
              path: { farmId },
              query: { type, from: window.from.toISOString(), to: window.to.toISOString(), page: 1, pageSize: 50 },
            },
          }),
        ),
      staleTime: 5 * 60_000,
    })),
  })
  const points: Point[] = windows.map((window, index) => ({
    label: window.label,
    value: average(results[index]?.data?.items ?? []),
  }))
  return {
    points,
    isPending: results.some((result) => result.isPending),
    error: results.find((result) => result.error)?.error ?? null,
    refetch: () => results.forEach((result) => void result.refetch()),
  }
}

export function useAlerts(farmId: string | undefined, options: { onlyOpen?: boolean; from?: string } = {}) {
  return useQuery({
    queryKey: queryKeys.alerts(farmId ?? "", options),
    queryFn: () =>
      call(
        client.GET("/api/farms/{farmId}/alerts", {
          params: {
            path: { farmId: farmId ?? "" },
            query: { onlyOpen: options.onlyOpen, from: options.from, page: 1, pageSize: 50 },
          },
        }),
      ),
    enabled: Boolean(farmId),
    refetchInterval: 60_000,
    select: (page) => page.items,
  })
}

export function useAcknowledgeAlert(farmId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (alertId: string) =>
      call(client.POST("/api/alerts/{id}/acknowledge", { params: { path: { id: alertId } } })),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["sensors", farmId] }),
  })
}
