import { Image, StyleSheet, View } from "react-native"
import { useAuth, useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { roleLabel } from "@/lib/format"
import { summarizeSchedules } from "@/lib/schedule"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { InfoRow } from "../catalog/InfoRow"
import { ratingOf, useFarmReviews } from "../catalog/queries"
import { RatingText } from "../catalog/RatingText"
import { displayName, useMyProfile } from "../profile/queries"
import { useMyFarm } from "./queries"

// G20 A minha quinta: the farm's details and pickup hours, each with Editar, and signing out (RF05, RF07).
export function MyFarmScreen({ navigation }: ManagerScreenProps<"MyFarm">) {
  const session = useSession()
  const { logout } = useAuth()
  const profile = useMyProfile()
  const { farm, ...farms } = useMyFarm()
  const reviews = useFarmReviews(farm?.id)

  let content
  if (farms.isPending) content = <LoadingState rows={3} height={100} />
  else if (farms.isError) content = <ErrorState error={farms.error} onRetry={() => void farms.refetch()} />
  else if (!farm) {
    content = (
      <EmptyState
        title="Ainda não registou a quinta"
        action={{ label: "Registar quinta", onPress: () => navigation.navigate("FarmForm") }}
      />
    )
  } else {
    content = (
      <>
        <View style={styles.header}>
          <Image
            source={require("../../../assets/emblema_256.png")}
            style={styles.emblem}
            accessibilityIgnoresInvertColors
          />
          <View style={styles.flex}>
            <AppText variant="h3">{farm.name}</AppText>
            <AppText variant="caption">
              {displayName(profile.data, session.email)} - {roleLabel.Manager}
            </AppText>
            <RatingText
              rating={reviews.data ? ratingOf(reviews.data.items, reviews.data.totalCount) : null}
              suffix="avaliações"
            />
          </View>
        </View>
        {!farm.isActive ? (
          <AppText color="error">A quinta foi desativada pelo administrador e não aparece no catálogo.</AppText>
        ) : null}
        <Card
          title="Dados da quinta"
          action={{ label: "Editar", onPress: () => navigation.navigate("FarmForm", { farmId: farm.id }) }}
        >
          <InfoRow icon="place" title={farm.location} subtitle="Localização no mapa" />
          <InfoRow icon="storefront" title={farm.name} subtitle="Nome apresentado no catálogo" />
          {farm.description ? <InfoRow icon="notes" title={farm.description} subtitle="Descrição" /> : null}
        </Card>
        <Card
          title="Horários de levantamento"
          action={{ label: "Editar", onPress: () => navigation.navigate("PickupSchedules", { farmId: farm.id }) }}
        >
          <InfoRow
            icon="event"
            title={summarizeSchedules(farm.pickupSchedules)}
            subtitle="Os clientes escolhem um destes horários ao reservar"
          />
        </Card>
      </>
    )
  }

  return (
    <Screen title="A minha quinta" back="back" testID="my-farm-screen">
      {content}
      <Button
        label="Alterar palavra-passe"
        icon="lock"
        variant="text"
        onPress={() => navigation.navigate("ChangePassword")}
      />
      <Button label="Terminar sessão" icon="logout" variant="secondary" onPress={logout} />
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  emblem: { width: 56, height: 56 },
  flex: { flex: 1 },
})
