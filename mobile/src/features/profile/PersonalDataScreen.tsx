import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Profile } from "@/api/types"
import { useSession } from "@/auth/AuthProvider"
import { Avatar } from "@/components/Avatar"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { required } from "@/lib/validation"
import type { ClientScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useMyProfile, useSaveProfile } from "./queries"

const phoneError = (value: string) =>
  value.trim() && !/^\+?[\d\s]{9,15}$/.test(value.trim()) ? "Indique um número com 9 dígitos (ex.: 912 345 678)." : null

function Form({ profile, onDone }: { profile: Profile | null; onDone: () => void }) {
  const session = useSession()
  const save = useSaveProfile()
  const snackbar = useSnackbar()
  const [firstName, setFirstName] = useState(profile?.firstName ?? "")
  const [lastName, setLastName] = useState(profile?.lastName ?? "")
  const [phone, setPhone] = useState(profile?.phoneNumber ?? "")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const changed =
    !profile || firstName !== profile.firstName || lastName !== profile.lastName || phone !== profile.phoneNumber

  function submit() {
    const next = {
      firstName: required(firstName, "Indique o nome."),
      lastName: required(lastName, "Indique o apelido."),
      phone: phoneError(phone),
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean)) return
    save.mutate(
      {
        exists: profile !== null,
        body: { firstName: firstName.trim(), lastName: lastName.trim(), phoneNumber: phone.trim() },
      },
      {
        onSuccess: () => {
          snackbar.show({ message: "Dados guardados." })
          onDone()
        },
        onError: (error) => setFormError(errorMessage(error)),
      },
    )
  }

  return (
    <Screen
      title="Dados pessoais"
      back="back"
      footer={
        <Button
          label="Guardar alterações"
          disabled={!changed}
          loading={save.isPending}
          onPress={submit}
          testID="save-profile"
        />
      }
    >
      <View style={styles.avatar}>
        <Avatar name={`${firstName} ${lastName}`} size={80} />
      </View>
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
        value={session.email}
        editable={false}
        hint="Para mudar o email, contacte o administrador."
      />
      <TextField
        label="Telemóvel (opcional)"
        icon="phone"
        value={phone}
        onChangeText={setPhone}
        error={errors.phone}
        keyboardType="phone-pad"
        hint="Só é partilhado com o produtor da encomenda."
      />
    </Screen>
  )
}

// C15 Dados pessoais: name and phone of the profile (the email belongs to the account).
export function PersonalDataScreen({ navigation }: ClientScreenProps<"PersonalData">) {
  const profile = useMyProfile()
  if (profile.isPending) {
    return (
      <Screen title="Dados pessoais" back="back">
        <LoadingState rows={3} height={64} />
      </Screen>
    )
  }
  if (profile.isError) {
    return (
      <Screen title="Dados pessoais" back="back">
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      </Screen>
    )
  }
  return <Form profile={profile.data} onDone={() => navigation.goBack()} />
}

const styles = StyleSheet.create({
  avatar: { alignItems: "center" },
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
