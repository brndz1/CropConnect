import { Image, StyleSheet, View } from "react-native"
import { useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { IconButton } from "@/components/IconButton"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { roleLabel, shortId } from "@/lib/format"
import { parseOrderNotes } from "@/lib/orders"
import { nextPickupSlots } from "@/lib/schedule"
import { alertSummary, alertTitle } from "@/lib/sensors"
import type { ManagerTabProps } from "@/navigation/types"
import { sizes, spacing } from "@/theme/tokens"
import { NotificationsButton } from "../notifications/NotificationsButton"
import { useFarmOrders } from "../orders/queries"
import { displayName, useMyProfile } from "../profile/queries"
import { useAlerts, useLatestReadings, useThresholds } from "../sensors/queries"
import { SensorGrid } from "../sensors/SensorGrid"
import { useMyFarm } from "./queries"

// G01 Painel: the open alert stays on top until it is resolved, then new orders, pickups and the sensors (RF16-RF18).
export function DashboardScreen({ navigation }: ManagerTabProps<"Dashboard">) {
  const session = useSession()
  const profile = useMyProfile()
  const { farm, ...farms } = useMyFarm()
  const pending = useFarmOrders("Pending")
  const ready = useFarmOrders("ReadyForPickup")
  const readings = useLatestReadings(farm?.id)
  const thresholds = useThresholds()
  const alerts = useAlerts(farm?.id, { onlyOpen: true })

  const header = (
    <View style={styles.header}>
      <Image
        source={require("../../../assets/emblema_256.png")}
        style={styles.emblem}
        resizeMode="contain"
        accessibilityIgnoresInvertColors
      />
      <View style={styles.flex}>
        <AppText variant="h3" color="green900" accessibilityRole="header" numberOfLines={1}>
          {farm?.name ?? "CropConnect"}
        </AppText>
        <AppText variant="caption">
          {displayName(profile.data, session.email)} - {roleLabel.Manager}
        </AppText>
      </View>
      <IconButton icon="storefront" label="A minha quinta" onPress={() => navigation.navigate("MyFarm")} />
      <NotificationsButton />
    </View>
  )

  if (farms.isPending) {
    return (
      <Screen header={header}>
        <LoadingState rows={3} height={120} />
      </Screen>
    )
  }
  if (farms.isError) {
    return (
      <Screen header={header}>
        <ErrorState error={farms.error} onRetry={() => void farms.refetch()} />
      </Screen>
    )
  }
  if (!farm) {
    return (
      <Screen header={header}>
        <EmptyState
          title="Registe a sua quinta"
          message="Com a quinta registada, os clientes encontram os seus produtos e recebe as encomendas aqui."
          action={{ label: "Registar quinta", onPress: () => navigation.navigate("FarmForm") }}
        />
      </Screen>
    )
  }

  const alert = alerts.data?.[0]
  const firstPending = pending.data?.[0]
  const nextSlot = nextPickupSlots(farm.pickupSchedules)[0]

  return (
    <Screen
      header={header}
      refreshing={readings.isRefetching}
      onRefresh={() => {
        void readings.refetch()
        void pending.refetch()
        void alerts.refetch()
      }}
      testID="dashboard-screen"
    >
      {alert ? (
        <Banner
          tone="error"
          title={alertTitle(alert)}
          message={alertSummary(alert)}
          action={{ label: "Ver", onPress: () => navigation.navigate("Alert", { farmId: farm.id, alertId: alert.id }) }}
          testID="alert-banner"
        />
      ) : null}

      <View style={styles.row}>
        <Card
          style={styles.flex}
          onPress={() => navigation.navigate("ManagerOrders")}
          accessibilityLabel="Novas encomendas"
        >
          <View style={styles.cardTitle}>
            <Icon name="receipt-long" size={18} color="green700" />
            <AppText variant="label">Novas encomendas</AppText>
          </View>
          <AppText variant="h1">{pending.data?.length ?? "–"}</AppText>
          <AppText variant="caption" numberOfLines={1}>
            {firstPending
              ? `${shortId(firstPending.id)} - ${parseOrderNotes(firstPending.notes).customer ?? "Cliente"}`
              : "Nenhuma por confirmar"}
          </AppText>
        </Card>
        <Card
          style={styles.flex}
          onPress={() => navigation.navigate("ManagerOrders")}
          accessibilityLabel="Levantamentos"
        >
          <View style={styles.cardTitle}>
            <Icon name="shopping-bag" size={18} color="green700" />
            <AppText variant="label">Levantamentos</AppText>
          </View>
          <AppText variant="h1">{ready.data?.length ?? "–"}</AppText>
          <AppText variant="caption" numberOfLines={1}>
            {nextSlot ? nextSlot.label : "Sem horário definido"}
          </AppText>
        </Card>
      </View>

      <View style={styles.sectionTitle}>
        <AppText variant="h3" accessibilityRole="header">
          Sensores
        </AppText>
        <Button label="Ver todos" variant="text" onPress={() => navigation.navigate("Sensors")} />
      </View>
      {readings.data && readings.data.length > 0 ? (
        <SensorGrid
          readings={readings.data}
          thresholds={thresholds.data ?? []}
          onOpen={(type, sensorId) => navigation.navigate("SensorHistory", { farmId: farm.id, type, sensorId })}
        />
      ) : (
        <AppText color="textSecondary">Os sensores ainda não enviaram leituras.</AppText>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xs,
    minHeight: sizes.topBar,
    gap: spacing.sm,
  },
  emblem: { width: 44, height: 44 },
  flex: { flex: 1 },
  row: { flexDirection: "row", gap: spacing.md },
  cardTitle: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
  sectionTitle: { flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
})
