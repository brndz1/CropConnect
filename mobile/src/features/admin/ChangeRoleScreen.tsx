import { useState } from "react"
import { Pressable, StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Role } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import type { AdminScreenProps } from "@/navigation/types"
import { colors, radii, spacing } from "@/theme/tokens"
import { personName, useUpdateUser, useUser, useUserProfile } from "./queries"

const options: { role: Role; title: string; description: string }[] = [
  { role: "Client", title: "Cliente", description: "Compra e reserva produtos." },
  { role: "Manager", title: "Gestor", description: "Gere uma quinta, os produtos e as encomendas." },
  { role: "Admin", title: "Administrador", description: "Gere utilizadores, quintas e limites." },
]

// A04 Alterar perfil: the profile decides the navigation the person sees (RF03, RF22).
export function ChangeRoleScreen({ navigation, route }: AdminScreenProps<"ChangeRole">) {
  const { userId } = route.params
  const user = useUser(userId)
  const profile = useUserProfile(userId)
  const update = useUpdateUser(userId)
  const snackbar = useSnackbar()
  const [chosen, setChosen] = useState<Role | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (!user.data) {
    return (
      <Screen title="Alterar perfil" back="back">
        <LoadingState rows={3} height={72} />
      </Screen>
    )
  }
  const current = chosen ?? user.data.role
  const name = personName(user.data, profile.data)

  const save = () =>
    update.mutate(
      { role: current },
      {
        onSuccess: () => {
          snackbar.show({ message: `Perfil de ${name} alterado.` })
          navigation.goBack()
        },
        onError: (failure) => setError(errorMessage(failure)),
      },
    )

  return (
    <Screen
      title="Alterar perfil"
      back="back"
      footer={
        <Button
          label="Guardar perfil"
          disabled={current === user.data.role}
          loading={update.isPending}
          onPress={save}
          testID="save-role"
        />
      }
    >
      <AppText color="textSecondary">
        Escolha o perfil de {name}. O perfil decide a navegação que o utilizador vê.
      </AppText>
      {error ? <Banner tone="error" message={error} /> : null}
      <View accessibilityRole="radiogroup" style={styles.list}>
        {options.map((option) => {
          const selected = option.role === current
          return (
            <Pressable
              key={option.role}
              onPress={() => setChosen(option.role)}
              accessibilityRole="radio"
              accessibilityState={{ checked: selected }}
              accessibilityLabel={`${option.title}. ${option.description}`}
              style={[styles.option, selected && styles.selected]}
            >
              <View style={[styles.radio, selected && styles.radioOn]}>
                {selected ? <View style={styles.dot} /> : null}
              </View>
              <View style={styles.flex}>
                <AppText variant="bodyBold">{option.title}</AppText>
                <AppText variant="caption">{option.description}</AppText>
              </View>
            </Pressable>
          )
        })}
      </View>
    </Screen>
  )
}

const styles = StyleSheet.create({
  list: { gap: spacing.sm },
  option: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.md,
    padding: spacing.md,
    borderRadius: radii.card,
    borderWidth: 1,
    borderColor: colors.border,
  },
  selected: { backgroundColor: colors.green100, borderColor: colors.green900, borderWidth: 2 },
  radio: {
    width: 22,
    height: 22,
    borderRadius: 11,
    borderWidth: 2,
    borderColor: colors.outline,
    alignItems: "center",
    justifyContent: "center",
  },
  radioOn: { borderColor: colors.green700 },
  dot: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.green700 },
  flex: { flex: 1 },
})
