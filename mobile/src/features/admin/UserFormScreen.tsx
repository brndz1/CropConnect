import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage, isApiError } from "@/api/errors"
import type { Profile, Role, UserAccount } from "@/api/types"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { ChipGroup } from "@/components/Chip"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { AppText } from "@/components/AppText"
import { emailError, required } from "@/lib/validation"
import type { AdminScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useInviteUser, useSaveUserProfile, useUpdateUser, useUser, useUserProfile } from "./queries"

const roles: { value: Role; label: string }[] = [
  { value: "Client", label: "Cliente" },
  { value: "Manager", label: "Gestor" },
  { value: "Admin", label: "Administrador" },
]

function Form({
  account,
  profile,
  onDone,
}: {
  account: UserAccount | null
  profile: Profile | null
  onDone: () => void
}) {
  const invite = useInviteUser()
  const update = useUpdateUser(account?.id ?? "")
  const saveProfile = useSaveUserProfile(account?.id ?? "")
  const snackbar = useSnackbar()
  const [firstName, setFirstName] = useState(profile?.firstName ?? "")
  const [lastName, setLastName] = useState(profile?.lastName ?? "")
  const [email, setEmail] = useState(account?.email ?? "")
  const [phone, setPhone] = useState(profile?.phoneNumber ?? "")
  const [role, setRole] = useState<Role>("Manager")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const busy = invite.isPending || update.isPending || saveProfile.isPending

  async function submit() {
    const next = {
      firstName: required(firstName, "Indique o nome."),
      lastName: required(lastName, "Indique o apelido."),
      email: emailError(email),
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean)) return
    try {
      if (account) {
        if (email.trim().toLowerCase() !== account.email) await update.mutateAsync({ email: email.trim() })
        await saveProfile.mutateAsync({
          exists: profile !== null,
          body: { firstName: firstName.trim(), lastName: lastName.trim(), phoneNumber: phone.trim() },
        })
        snackbar.show({ message: "Dados guardados." })
      } else {
        await invite.mutateAsync({ firstName: firstName.trim(), lastName: lastName.trim(), email, role })
        snackbar.show({ message: `Convite enviado para ${email.trim().toLowerCase()}.` })
      }
      onDone()
    } catch (error) {
      setFormError(isApiError(error, 409) ? "Já existe uma conta com este email." : errorMessage(error))
    }
  }

  return (
    <Screen
      title={account ? "Editar utilizador" : "Novo utilizador"}
      back="back"
      footer={
        <Button
          label={account ? "Guardar alterações" : "Enviar convite"}
          loading={busy}
          onPress={submit}
          testID="save-user"
        />
      }
    >
      {formError ? <Banner tone="error" message={formError} /> : null}
      <View style={styles.row}>
        <View style={styles.flex}>
          <TextField
            label="Nome"
            icon="person"
            value={firstName}
            onChangeText={setFirstName}
            error={errors.firstName}
          />
        </View>
        <View style={styles.flex}>
          <TextField label="Apelido" value={lastName} onChangeText={setLastName} error={errors.lastName} />
        </View>
      </View>
      <TextField
        label="Email"
        icon="email"
        value={email}
        onChangeText={setEmail}
        error={errors.email}
        autoCapitalize="none"
        keyboardType="email-address"
        placeholder="nome@exemplo.pt"
      />
      {account ? (
        <TextField
          label="Telemóvel (opcional)"
          icon="phone"
          value={phone}
          onChangeText={setPhone}
          keyboardType="phone-pad"
        />
      ) : (
        <>
          <AppText variant="label">Perfil</AppText>
          <ChipGroup label="Perfil" scroll={false} options={roles} value={role} onChange={setRole} />
          <Banner
            tone="info"
            title="Convite por email"
            message="O utilizador define a palavra-passe no primeiro acesso."
          />
        </>
      )}
    </Screen>
  )
}

// A05 Novo utilizador (an invitation by email) and A03 Editar utilizador (RF22).
export function UserFormScreen({ navigation, route }: AdminScreenProps<"UserForm">) {
  const userId = route.params?.userId
  const user = useUser(userId ?? "")
  const profile = useUserProfile(userId)

  if (userId && (user.isPending || profile.isPending)) {
    return (
      <Screen title="Editar utilizador" back="back">
        <LoadingState rows={3} height={64} />
      </Screen>
    )
  }
  if (userId && user.isError) {
    return (
      <Screen title="Editar utilizador" back="back">
        <ErrorState error={user.error} onRetry={() => void user.refetch()} />
      </Screen>
    )
  }
  return (
    <Form
      account={userId ? (user.data ?? null) : null}
      profile={userId ? (profile.data ?? null) : null}
      onDone={() => navigation.goBack()}
    />
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
