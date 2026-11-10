import { useNavigation } from "@react-navigation/native"
import { useState } from "react"
import { call, client } from "@/api/client"
import { errorMessage } from "@/api/errors"
import { useAuth } from "@/auth/AuthProvider"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { newPasswordError } from "@/lib/validation"

// The API's answers about passwords, in the app's language.
export function passwordMessage(error: unknown) {
  const message = errorMessage(error)
  if (message.startsWith("The new password must be different"))
    return "A nova palavra-passe tem de ser diferente da atual."
  if (message.startsWith("The current password is not correct")) return "A palavra-passe atual não está correta."
  return message
}

// Alterar palavra-passe, for every profile. The API ends the other sessions and returns new tokens for this one.
export function ChangePasswordScreen() {
  const navigation = useNavigation()
  const { applyTokens } = useAuth()
  const snackbar = useSnackbar()
  const [current, setCurrent] = useState("")
  const [next, setNext] = useState("")
  const [confirm, setConfirm] = useState("")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit() {
    const problems = {
      current: current ? null : "Introduza a palavra-passe atual.",
      next: newPasswordError(next),
      confirm: confirm === next ? null : "As palavras-passe não coincidem.",
    }
    setErrors(problems)
    setFormError(null)
    if (Object.values(problems).some(Boolean)) return
    setBusy(true)
    try {
      const pair = await call(
        client.POST("/api/auth/change-password", { body: { currentPassword: current, newPassword: next } }),
      )
      await applyTokens(pair)
      snackbar.show({ message: "Palavra-passe alterada. As outras sessões foram terminadas." })
      navigation.goBack()
    } catch (error) {
      setFormError(passwordMessage(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen
      title="Alterar palavra-passe"
      back="back"
      footer={<Button label="Guardar palavra-passe" loading={busy} onPress={submit} testID="save-password" />}
    >
      {formError ? <Banner tone="error" message={formError} /> : null}
      <TextField
        label="Palavra-passe atual"
        icon="lock"
        secure
        value={current}
        onChangeText={setCurrent}
        error={errors.current}
        autoComplete="current-password"
      />
      <TextField
        label="Nova palavra-passe"
        icon="lock"
        secure
        value={next}
        onChangeText={setNext}
        error={errors.next}
        hint="Mínimo de 8 caracteres, com pelo menos um número."
        autoComplete="new-password"
      />
      <TextField
        label="Confirmar a nova palavra-passe"
        icon="lock"
        secure
        value={confirm}
        onChangeText={setConfirm}
        error={errors.confirm}
        autoComplete="new-password"
      />
    </Screen>
  )
}
