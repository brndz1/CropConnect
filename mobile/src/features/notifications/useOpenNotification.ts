import { useNavigation } from "@react-navigation/native"
import { useCallback } from "react"
import type { AppNotification } from "@/api/types"
import { useSession } from "@/auth/AuthProvider"

type Target = Pick<AppNotification, "type" | "relatedEntityId" | "farmId">

// Opens what a notification is about: the order, or the sensor alert for the farm's manager.
export function useOpenNotification() {
  const { role } = useSession()
  const navigation = useNavigation()

  return useCallback(
    (notification: Target) => {
      const navigate = navigation.navigate as (screen: string, params: object) => void
      if (notification.type === "SensorAlert") {
        if (role === "Manager")
          navigate("Alert", { farmId: notification.farmId, alertId: notification.relatedEntityId })
        return
      }
      if (role === "Client") navigate("Order", { orderId: notification.relatedEntityId })
      else if (role === "Manager") navigate("ManagerOrder", { orderId: notification.relatedEntityId })
    },
    [navigation, role],
  )
}
