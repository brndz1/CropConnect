import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import { useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Badge } from "@/components/Badge"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { roleLabel } from "@/lib/format"
import type { AdminScreenProps } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"
import { personName, useSetUserActive, useUser, useUserProfile } from "./queries"

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row} accessible accessibilityLabel={`${label}: ${value}`}>
      <AppText color="textSecondary">{label}</AppText>
      <AppText style={styles.value}>{value}</AppText>
    </View>
  )
}

// A02 Ficha do utilizador: data, profile and account state; edit, change the profile, or deactivate and reactivate
// with a confirmation (DA, RF22).
export function UserDetailScreen({ navigation, route }: AdminScreenProps<"User">) {
  const { userId } = route.params
  const session = useSession()
  const user = useUser(userId)
  const profile = useUserProfile(userId)
  const setActive = useSetUserActive(userId)
  const snackbar = useSnackbar()
  const [confirming, setConfirming] = useState(false)

  if (user.isPending) {
    return (
      <Screen title="Utilizador" back="back">
        <LoadingState rows={2} height={120} />
      </Screen>
    )
  }
  if (user.isError) {
    return (
      <Screen title="Utilizador" back="back">
        <ErrorState error={user.error} onRetry={() => void user.refetch()} />
      </Screen>
    )
  }

  const account = user.data
  const name = personName(account, profile.data)
  const self = account.id === session.userId

  const toggle = (isActive: boolean) =>
    setActive.mutate(isActive, {
      onSuccess: () => {
        setConfirming(false)
        snackbar.show({
          message: isActive ? `Conta de ${name} reativada.` : `Conta de ${name} desativada.`,
          action: isActive ? undefined : { label: "Anular", onPress: () => toggle(true) },
        })
      },
      onError: (error) => {
        setConfirming(false)
        snackbar.show({ tone: "error", message: errorMessage(error) })
      },
    })

  const footer = (
    <>
      <Button
        label="Editar dados"
        icon="edit"
        variant="secondary"
        onPress={() => navigation.navigate("UserForm", { userId })}
      />
      <Button
        label="Alterar perfil"
        icon="person"
        variant="secondary"
        disabled={self}
        onPress={() => navigation.navigate("ChangeRole", { userId })}
      />
      {account.isActive ? (
        <Button
          label="Desativar conta"
          variant="destructiveText"
          disabled={self}
          onPress={() => setConfirming(true)}
          testID="deactivate-user"
        />
      ) : (
        <Button label="Reativar conta" variant="text" loading={setActive.isPending} onPress={() => toggle(true)} />
      )}
    </>
  )

  return (
    <Screen title="Utilizador" back="back" footer={footer} testID="user-screen">
      <View style={styles.header}>
        <Avatar name={name} size={72} />
        <View style={styles.flex}>
          <AppText variant="h3">{name}</AppText>
          <AppText variant="caption">{account.email}</AppText>
          {account.isActive ? (
            <Badge label="Conta ativa" tone="successSoft" icon="check" />
          ) : (
            <Badge label="Conta desativada" tone="error" icon="block" />
          )}
        </View>
      </View>
      {self ? (
        <Banner
          tone="info"
          message="É a sua conta: o perfil e o estado só podem ser mudados por outro administrador."
        />
      ) : null}
      <Card title="Dados da conta">
        <Row label="Perfil" value={roleLabel[account.role]} />
        <Row label="Nome de utilizador" value={account.username} />
        <Row label="Email confirmado" value={account.emailVerified ? "Sim" : "Ainda não"} />
        <Row label="Telemóvel" value={profile.data?.phoneNumber || "—"} />
      </Card>

      <ConfirmDialog
        visible={confirming}
        destructive
        title={`Desativar a conta de ${name}?`}
        message="Deixa de poder entrar e as sessões abertas terminam. Pode reativar a conta mais tarde."
        confirmLabel="Desativar conta"
        cancelLabel="Manter"
        loading={setActive.isPending}
        onConfirm={() => toggle(false)}
        onCancel={() => setConfirming(false)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1, gap: spacing.xxs },
  row: {
    flexDirection: "row",
    justifyContent: "space-between",
    gap: spacing.md,
    paddingVertical: spacing.xxs,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  value: { flexShrink: 1, textAlign: "right" },
})
