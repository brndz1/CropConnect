import { useQuery } from "@tanstack/react-query"
import { call, client } from "@/api/client"
import { queryKeys } from "@/api/queryKeys"

// The farms the manager runs. The prototype has one farm per manager, so the screens use the first.
export function useMyFarms() {
  return useQuery({
    queryKey: queryKeys.myFarms(),
    queryFn: () => call(client.GET("/api/farms/mine", { params: { query: { page: 1, pageSize: 20 } } })),
    select: (page) => page.items,
  })
}

export function useMyFarm() {
  const farms = useMyFarms()
  return { ...farms, farm: farms.data?.[0] ?? null }
}
