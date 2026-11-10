import { useEffect, useRef } from "react"
import { useSnackbar } from "@/components/Snackbar"
import { localizeNotification } from "@/lib/notifications"
import { useNotifications } from "./queries"
import { useOpenNotification } from "./useOpenNotification"

// While the app is open, a notification that arrives shows at the bottom with "Ver", which opens it (C11N). The
// ones already there when the app started, or already shown, are not announced again.
export function useNotificationToasts() {
  const notifications = useNotifications()
  const snackbar = useSnackbar()
  const open = useOpenNotification()
  const seen = useRef<Set<string> | null>(null)

  useEffect(() => {
    const items = notifications.data?.items
    if (!items) return
    if (seen.current === null) {
      seen.current = new Set(items.map((item) => item.id))
      return
    }
    const known = seen.current
    const arrived = items.filter((item) => !known.has(item.id))
    arrived.forEach((item) => known.add(item.id))
    const newest = arrived.find((item) => item.readAt === null)
    if (newest) {
      snackbar.show({
        message: localizeNotification(newest).title,
        action: { label: "Ver", onPress: () => open(newest) },
        duration: 6000,
      })
    }
  }, [notifications.data, snackbar, open])
}
