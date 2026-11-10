import type { NativeStackScreenProps } from "@react-navigation/native-stack"
import { useState } from "react"
import { Pressable, StyleSheet, View } from "react-native"
import { isApiError } from "@/api/errors"
import { useAuth } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { TextField } from "@/components/TextField"
import { emailError, newPasswordError, required } from "@/lib/validation"
import type { AuthStackParamList } from "@/navigation/types"
import { colors, radii, sizes, spacing } from "@/theme/tokens"

type Props = NativeStackScreenProps<AuthStackParamList, "Register">

interface Errors {
  name?: string | null
  email?: string | null
  password?: string | null
  consent?: string | null
}

// C01r Criar conta. Only clients register themselves; managers and administrators are invited (RF01, table 29).
export function RegisterScreen({ navigation }: Props) {
  const { register } = useAuth()
  const [name, setName] = useState("")
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [consent, setConsent] = useState(false)
  const [errors, setErrors] = useState<Errors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit() {
    const next: Errors = {
      name: required(name, "Indique o seu nome."),
      email: emailError(email),
      password: newPasswordError(password),
      consent: consent ? null : "Tem de aceitar os termos para criar a conta.",
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean)) return
    setBusy(true)
    try {
      await register({ name, email, password })
      navigation.replace("CheckEmail", { email: email.trim().toLowerCase(), reason: "verify" })
    } catch (error) {
      setFormError(
        isApiError(error, 409)
          ? "Já existe uma conta com este email."
          : error instanceof Error
            ? error.message
            : "Não foi possível criar a conta.",
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen title="Criar conta" back="back">
      <AppText color="textSecondary">Uma só conta para comprar às quintas da sua região.</AppText>
      {formError ? <Banner tone="error" message={formError} /> : null}
      <TextField
        label="Nome"
        icon="person"
        value={name}
        onChangeText={setName}
        error={errors.name}
        autoComplete="name"
        textContentType="name"
        testID="register-name"
      />
      <TextField
        label="Email"
        icon="email"
        value={email}
        onChangeText={setEmail}
        error={errors.email}
        placeholder="nome@exemplo.pt"
        autoCapitalize="none"
        autoComplete="email"
        keyboardType="email-address"
        testID="register-email"
      />
      <TextField
        label="Palavra-passe"
        icon="lock"
        secure
        value={password}
        onChangeText={setPassword}
        error={errors.password}
        hint="Mínimo de 8 caracteres, com pelo menos um número."
        autoComplete="new-password"
        textContentType="newPassword"
        testID="register-password"
      />

      <Pressable
        onPress={() => setConsent((value) => !value)}
        accessibilityRole="checkbox"
        accessibilityState={{ checked: consent }}
        accessibilityLabel="Aceito os Termos de Utilização e a Política de Privacidade (RGPD)."
        style={styles.consent}
        testID="register-consent"
      >
        <View style={[styles.checkbox, consent && styles.checked]}>
          {consent ? <Icon name="check" size={18} color="white" /> : null}
        </View>
        <AppText style={styles.consentText}>Aceito os Termos de Utilização e a Política de Privacidade (RGPD).</AppText>
      </Pressable>
      {errors.consent ? (
        <AppText variant="caption" color="error">
          {errors.consent}
        </AppText>
      ) : null}

      <Button label="Criar conta" loadingLabel="A criar conta..." loading={busy} onPress={submit} />
      <Button label="Já tem conta? Iniciar sessão" variant="text" onPress={() => navigation.goBack()} />
    </Screen>
  )
}

const styles = StyleSheet.create({
  consent: { flexDirection: "row", gap: spacing.md, alignItems: "flex-start", minHeight: sizes.touch },
  checkbox: {
    width: 24,
    height: 24,
    marginTop: 2,
    borderRadius: 4,
    borderWidth: 2,
    borderColor: colors.outline,
    alignItems: "center",
    justifyContent: "center",
  },
  checked: { backgroundColor: colors.green700, borderColor: colors.green700, borderRadius: radii.field / 2 },
  consentText: { flex: 1 },
})
