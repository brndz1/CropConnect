import { useNavigation } from "@react-navigation/native"
import { IconButton } from "@/components/IconButton"

// Bell on the top bar that opens the notification centre (RF19).
export function NotificationsButton() {
  const navigation = useNavigation()
  return (
    <IconButton
      icon="notifications"
      label="Notificações"
      // Every profile's stack has a Notifications screen.
      onPress={() => navigation.navigate("Notifications" as never)}
      testID="notifications-button"
    />
  )
}
