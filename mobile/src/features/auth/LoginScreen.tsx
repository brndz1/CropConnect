import type { NativeStackScreenProps } from "@react-navigation/native-stack"
import { useRef, useState, type ComponentRef } from "react"
import { Image, Pressable, StyleSheet, TextInput, View } from "react-native"
import { isApiError } from "@/api/errors"
import { useAuth } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { TextField } from "@/components/TextField"
import { emailError } from "@/lib/validation"
import type { AuthStackParamList } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"

type Props = NativeStackScreenProps<AuthStackParamList, "Login">

// C01 Iniciar sessão, with the validation (C01e), filled (C01f) and signing in (C01L) states. Every profile signs in
// here; the role in the token decides the navigation (RF03).
export function LoginScreen({ navigation }: Props) {
  const { login } = useAuth()
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [errors, setErrors] = useState<{ email?: string | null; password?: string | null }>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const passwordRef = useRef<ComponentRef<typeof TextInput>>(null)

  async function submit() {
    const next = { email: emailError(email), password: password ? null : "Introduza a palavra-passe." }
    setErrors(next)
    setFormError(null)
    if (next.email || next.password) return
    setBusy(true)
    try {
      await login(email, password)
    } catch (error) {
      if (isApiError(error, 401)) setFormError("Email ou palavra-passe incorretos.")
      else if (isApiError(error, 403)) setFormError("Confirme o seu email antes de entrar: enviámos-lhe um link.")
      else setFormError(error instanceof Error ? error.message : "Não foi possível iniciar sessão.")
    } finally {
      setBusy(false)
    }
  }

  const invalid = Boolean(errors.email || errors.password)

  return (
    <Screen testID="login-screen">
      <View style={styles.brand}>
        <Image
          source={require("../../../assets/logo_cropconnect.png")}
          style={styles.logo}
          resizeMode="contain"
          accessibilityLabel="CropConnect"
        />
        <AppText variant="h2" align="center" accessibilityRole="header">
          Iniciar sessão
        </AppText>
        <AppText color="textSecondary" align="center">
          Encomende diretamente a quem cultiva.
        </AppText>
      </View>

      {formError ? <Banner tone="error" message={formError} testID="login-error" /> : null}

      <TextField
        label="Email"
        icon="email"
        value={email}
        onChangeText={(value) => {
          setEmail(value)
          if (errors.email) setErrors((current) => ({ ...current, email: null }))
        }}
        error={errors.email}
        placeholder="nome@exemplo.pt"
        autoCapitalize="none"
        autoComplete="email"
        keyboardType="email-address"
        textContentType="username"
        returnKeyType="next"
        onSubmitEditing={() => passwordRef.current?.focus()}
        testID="login-email"
      />
      <TextField
        ref={passwordRef}
        label="Palavra-passe"
        icon="lock"
        secure
        value={password}
        onChangeText={(value) => {
          setPassword(value)
          if (errors.password) setErrors((current) => ({ ...current, password: null }))
        }}
        error={errors.password}
        placeholder="Palavra-passe"
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="go"
        onSubmitEditing={submit}
        testID="login-password"
      />

      <Pressable
        onPress={() => navigation.navigate("ForgotPassword", { email })}
        accessibilityRole="link"
        style={styles.forgot}
      >
        <AppText variant="button" color="green700">
          Esqueci-me da palavra-passe
        </AppText>
      </Pressable>

      <Button
        label="Entrar"
        loadingLabel="A entrar..."
        loading={busy}
        disabled={invalid}
        onPress={submit}
        testID="login-submit"
      />
      <View style={styles.divider} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
        <View style={styles.line} />
        <AppText color="textSecondary">ou</AppText>
        <View style={styles.line} />
      </View>
      <Button label="Criar conta" variant="secondary" onPress={() => navigation.navigate("Register")} />
    </Screen>
  )
}

const styles = StyleSheet.create({
  brand: { alignItems: "center", gap: spacing.xs, marginTop: spacing.lg, marginBottom: spacing.xs },
  logo: { width: 120, height: 104, marginBottom: spacing.md },
  forgot: { alignSelf: "flex-end", minHeight: 48, justifyContent: "center" },
  divider: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  line: { flex: 1, height: 1, backgroundColor: colors.border },
})
