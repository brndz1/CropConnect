import { useNavigation } from "@react-navigation/native"
import { IconButton } from "@/components/IconButton"
import { useNotifications } from "./queries"

// Bell on the top bar with the unread count; opens the notification centre (RF19).
export function NotificationsButton() {
  const navigation = useNavigation()
  const notifications = useNotifications()
  return (
    <IconButton
      icon="notifications"
      label="Notificações"
      count={notifications.data?.unreadCount}
      // Every profile's stack has a Notifications screen.
      onPress={() => navigation.navigate("Notifications" as never)}
      testID="notifications-button"
    />
  )
}
