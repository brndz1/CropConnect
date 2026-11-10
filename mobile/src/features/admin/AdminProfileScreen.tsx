import { StyleSheet, View } from "react-native"
import { useAuth, useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Button } from "@/components/Button"
import { ListRow } from "@/components/ListRow"
import { Screen } from "@/components/Screen"
import { roleLabel } from "@/lib/format"
import type { AdminTabProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { displayName, useMyProfile } from "../profile/queries"

// A30 Perfil of the administrator.
export function AdminProfileScreen({ navigation }: AdminTabProps<"AdminProfile">) {
  const session = useSession()
  const { logout } = useAuth()
  const profile = useMyProfile()
  const name = displayName(profile.data, session.email)

  return (
    <Screen title="Perfil" testID="admin-profile-screen">
      <View style={styles.header}>
        <Avatar name={name} size={64} />
        <View style={styles.flex}>
          <AppText variant="h3">{name}</AppText>
          <AppText variant="caption">{session.email}</AppText>
          <AppText variant="caption">{roleLabel.Admin}</AppText>
        </View>
      </View>
      <View>
        <ListRow icon="person-add" label="Convidar utilizador" onPress={() => navigation.navigate("UserForm")} />
        <ListRow icon="lock" label="Alterar palavra-passe" onPress={() => navigation.navigate("ChangePassword")} />
      </View>
      <Button label="Terminar sessão" icon="logout" variant="secondary" onPress={logout} testID="logout" />
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1 },
})
