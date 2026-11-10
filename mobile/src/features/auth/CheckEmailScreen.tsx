import type { NativeStackScreenProps } from "@react-navigation/native-stack"
import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { call, client } from "@/api/client"
import { AppText } from "@/components/AppText"
import { Button } from "@/components/Button"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { useSnackbar } from "@/components/Snackbar"
import type { AuthStackParamList } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"

type Props = NativeStackScreenProps<AuthStackParamList, "CheckEmail">

// C01pk Verifique o seu email (and C01pk2 after resending), for the reset link and for the confirmation of a new
// account.
export function CheckEmailScreen({ navigation, route }: Props) {
  const { email, reason } = route.params
  const snackbar = useSnackbar()
  const [busy, setBusy] = useState(false)

  async function resend() {
    setBusy(true)
    try {
      await call(
        reason === "reset"
          ? client.POST("/api/auth/forgot-password", { body: { email } })
          : client.POST("/api/auth/resend-verification", { body: { email } }),
      )
      snackbar.show({ message: "Email reenviado." })
    } catch (error) {
      snackbar.show({ tone: "error", message: error instanceof Error ? error.message : "Não foi possível reenviar." })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen back="back" contentStyle={styles.content}>
      <View style={styles.icon}>
        <Icon name="email" size={40} color="green900" />
      </View>
      <AppText variant="h2" align="center" accessibilityRole="header">
        Verifique o seu email
      </AppText>
      <AppText color="textSecondary" align="center">
        {reason === "reset"
          ? `Enviámos um link para ${email}. O link é válido durante 1 hora.`
          : `Enviámos um link para ${email}. Confirme o email para poder entrar.`}
      </AppText>
      <Button label="Voltar ao início de sessão" onPress={() => navigation.popToTop()} style={styles.wide} />
      <Button label="Reenviar email" variant="text" loading={busy} onPress={resend} style={styles.wide} />
    </Screen>
  )
}

const styles = StyleSheet.create({
  content: { flexGrow: 1, justifyContent: "center", alignItems: "center", gap: spacing.md },
  icon: {
    width: 96,
    height: 96,
    borderRadius: 48,
    backgroundColor: colors.green100,
    alignItems: "center",
    justifyContent: "center",
  },
  wide: { alignSelf: "stretch" },
})
