import { useRoute } from "@react-navigation/native"
import { useAuth } from "@/auth/AuthProvider"
import { Button } from "@/components/Button"
import { EmptyState } from "@/components/ScreenStates"
import { Screen } from "@/components/Screen"

// Stands in for a destination that a later part of the app fills in.
export function PlaceholderScreen() {
  const route = useRoute()
  const { logout } = useAuth()
  return (
    <Screen title={route.name}>
      <EmptyState title="Em construção" message="Este ecrã chega numa das próximas versões." />
      <Button label="Terminar sessão" icon="logout" variant="secondary" onPress={logout} />
    </Screen>
  )
}
