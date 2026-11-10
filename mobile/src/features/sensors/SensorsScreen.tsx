import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { plural } from "@/lib/format"
import { alertTitle, sensorState } from "@/lib/sensors"
import type { ManagerTabProps } from "@/navigation/types"
import { useMyFarm } from "../manager/queries"
import { useAlerts, useLatestReadings, useThresholds } from "./queries"
import { SensorGrid } from "./SensorGrid"

// G15 Sensores: the current reading of each sensor of the farm, with what is out of range on top (RF16, RF18).
export function SensorsScreen({ navigation }: ManagerTabProps<"Sensors">) {
  const { farm, ...farms } = useMyFarm()
  const readings = useLatestReadings(farm?.id)
  const thresholds = useThresholds()
  const alerts = useAlerts(farm?.id, { onlyOpen: true })

  const list = readings.data ?? []
  const out = list.filter(
    (reading) =>
      sensorState(
        reading.value,
        thresholds.data?.find((item) => item.type === reading.type),
      ) === "out",
  )
  const firstAlert = alerts.data?.[0]

  return (
    <Screen
      title="Sensores"
      refreshing={readings.isRefetching}
      onRefresh={() => void readings.refetch()}
      testID="sensors-screen"
    >
      {farms.isPending || (farm && readings.isPending) ? (
        <LoadingState rows={2} height={200} />
      ) : farms.isError || readings.isError ? (
        <ErrorState
          error={farms.error ?? readings.error}
          onRetry={() => {
            void farms.refetch()
            void readings.refetch()
          }}
        />
      ) : !farm ? (
        <EmptyState
          title="Ainda não tem uma quinta"
          message="Registe a quinta em A minha quinta para ver os sensores."
        />
      ) : (
        <>
          {firstAlert ? (
            <Banner
              tone="error"
              title={`${plural(alerts.data?.length ?? 0, "alerta por resolver", "alertas por resolver")}`}
              message={alertTitle(firstAlert)}
              action={{
                label: "Ver",
                onPress: () => navigation.navigate("Alert", { farmId: farm.id, alertId: firstAlert.id }),
              }}
            />
          ) : out.length > 0 ? (
            <Banner
              tone="error"
              message={`${plural(out.length, "sensor fora do limite", "sensores fora do limite")}`}
            />
          ) : null}
          <AppText variant="h3" accessibilityRole="header">
            {farm.name}
          </AppText>
          {list.length > 0 ? (
            <SensorGrid
              readings={list}
              thresholds={thresholds.data ?? []}
              onOpen={(type, sensorId) => navigation.navigate("SensorHistory", { farmId: farm.id, type, sensorId })}
            />
          ) : (
            <EmptyState title="Sem leituras" message="Os sensores ainda não enviaram leituras desta quinta." />
          )}
        </>
      )}
    </Screen>
  )
}
