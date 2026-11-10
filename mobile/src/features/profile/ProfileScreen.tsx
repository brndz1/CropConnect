import { StyleSheet, View } from "react-native"
import { useAuth, useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Button } from "@/components/Button"
import { ListRow } from "@/components/ListRow"
import { Screen } from "@/components/Screen"
import { roleLabel } from "@/lib/format"
import type { ClientTabProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { displayName, useMyProfile } from "./queries"

// C14 Perfil of the client.
export function ProfileScreen({ navigation }: ClientTabProps<"Profile">) {
  const session = useSession()
  const { logout } = useAuth()
  const profile = useMyProfile()
  const name = displayName(profile.data, session.email)

  return (
    <Screen title="Perfil" testID="profile-screen">
      <View style={styles.header}>
        <Avatar name={name} size={64} />
        <View style={styles.flex}>
          <AppText variant="h3">{name}</AppText>
          <AppText variant="caption">{session.email}</AppText>
          <AppText variant="caption">{roleLabel[session.role]}</AppText>
        </View>
      </View>
      <View>
        <ListRow icon="person" label="Dados pessoais" onPress={() => navigation.navigate("PersonalData")} />
        <ListRow icon="receipt-long" label="As minhas encomendas" onPress={() => navigation.navigate("Orders")} />
        <ListRow icon="notifications" label="Notificações" onPress={() => navigation.navigate("Notifications")} />
        <ListRow icon="lock" label="Alterar palavra-passe" onPress={() => navigation.navigate("ChangePassword")} />
        <ListRow icon="shield" label="Privacidade e dados (RGPD)" onPress={() => navigation.navigate("Privacy")} />
      </View>
      <Button label="Terminar sessão" icon="logout" variant="secondary" onPress={logout} testID="logout" />
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1 },
})
