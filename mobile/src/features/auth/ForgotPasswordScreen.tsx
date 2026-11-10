import type { NativeStackScreenProps } from "@react-navigation/native-stack"
import { useState } from "react"
import { call, client } from "@/api/client"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { TextField } from "@/components/TextField"
import { emailError } from "@/lib/validation"
import type { AuthStackParamList } from "@/navigation/types"

type Props = NativeStackScreenProps<AuthStackParamList, "ForgotPassword">

// C01p Recuperar acesso: emails a link to choose a new password. The API always answers 202, so it never reveals
// which emails have an account.
export function ForgotPasswordScreen({ navigation, route }: Props) {
  const [email, setEmail] = useState(route.params?.email ?? "")
  const [error, setError] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit() {
    const problem = emailError(email)
    setError(problem)
    if (problem) return
    setBusy(true)
    setFormError(null)
    try {
      const normalized = email.trim().toLowerCase()
      await call(client.POST("/api/auth/forgot-password", { body: { email: normalized } }))
      navigation.replace("CheckEmail", { email: normalized, reason: "reset" })
    } catch (failure) {
      setFormError(failure instanceof Error ? failure.message : "Não foi possível enviar o link.")
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen title="Recuperar acesso" back="back">
      <AppText color="textSecondary">
        Indique o email da sua conta. Enviamos um link para escolher uma nova palavra-passe.
      </AppText>
      {formError ? <Banner tone="error" message={formError} /> : null}
      <TextField
        label="Email"
        icon="email"
        value={email}
        onChangeText={setEmail}
        error={error}
        placeholder="nome@exemplo.pt"
        autoCapitalize="none"
        autoComplete="email"
        keyboardType="email-address"
        returnKeyType="send"
        onSubmitEditing={submit}
      />
      <Button label="Enviar link" loadingLabel="A enviar..." loading={busy} onPress={submit} />
    </Screen>
  )
}
