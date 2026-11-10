import { StyleSheet, View } from "react-native"
import type { Farm } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Badge } from "@/components/Badge"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { plural } from "@/lib/format"
import type { AdminTabProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useAdminFarms, useProfiles } from "./queries"

// A10 Quintas: every farm with its manager and state (RF23); inactive ones last.
export function AdminFarmsScreen({ navigation }: AdminTabProps<"AdminFarms">) {
  const farms = useAdminFarms()
  const list = [...(farms.data?.farms ?? [])].sort(
    (a, b) => Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name, "pt"),
  )
  const profiles = useProfiles(list.slice(0, 30).map((farm) => farm.ownerId))

  const manager = (farm: Farm) => {
    const profile = profiles.get(farm.ownerId)
    return profile ? `${profile.firstName} ${profile.lastName}`.trim() : null
  }

  return (
    <Screen
      title="Quintas"
      refreshing={farms.isRefetching}
      onRefresh={() => void farms.refetch()}
      testID="admin-farms-screen"
    >
      {farms.isPending ? (
        <LoadingState rows={4} height={96} />
      ) : farms.isError ? (
        <ErrorState error={farms.error} onRetry={() => void farms.refetch()} />
      ) : list.length === 0 ? (
        <EmptyState title="Ainda não há quintas" message="Os gestores registam as quintas na app." />
      ) : (
        <>
          <AppText variant="caption">
            {plural(farms.data?.total ?? list.length, "quinta registada", "quintas registadas")}
            {(farms.data?.total ?? 0) > list.length ? ` - as primeiras ${list.length}` : ""}
          </AppText>
          {list.map((farm) => (
            <Card
              key={farm.id}
              onPress={() => navigation.navigate("AdminFarm", { farmId: farm.id })}
              accessibilityLabel={`${farm.name}, ${farm.location}, ${farm.isActive ? "ativa" : "desativada"}`}
              testID={`admin-farm-${farm.id}`}
            >
              <View style={styles.row}>
                <Avatar icon="storefront" />
                <View style={styles.flex}>
                  <AppText variant="bodyBold">{farm.name}</AppText>
                  <AppText variant="caption">{farm.location}</AppText>
                  {manager(farm) ? <AppText variant="label">Gestor: {manager(farm)}</AppText> : null}
                  {farm.isActive ? (
                    <Badge label="Ativa" tone="successSoft" icon="check" />
                  ) : (
                    <Badge label="Desativada" tone="error" icon="block" />
                  )}
                </View>
                <Icon name="chevron-right" color="textSecondary" />
              </View>
            </Card>
          ))}
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1, gap: 2 },
})
