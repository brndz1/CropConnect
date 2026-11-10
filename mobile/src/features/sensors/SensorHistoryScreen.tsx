import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { Card } from "@/components/Card"
import { ChipGroup } from "@/components/Chip"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { formatDateTime, formatNumber, plural } from "@/lib/format"
import { alertTitle, outOfRange, periodWindows, sensorLabel, statsOf, type Period } from "@/lib/sensors"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useFarm } from "../catalog/queries"
import { LineChart } from "./LineChart"
import { useAlerts, useReadingHistory, useThresholds } from "./queries"

const periods: { value: Period; label: string }[] = [
  { value: "24h", label: "24 h" },
  { value: "7d", label: "7 dias" },
  { value: "30d", label: "30 dias" },
]

// G12 Histórico of a sensor in 24 h, 7 days or 30 days, with the limits dashed, minimum, average and maximum, and the
// alerts of the period (RF18).
export function SensorHistoryScreen({ navigation, route }: ManagerScreenProps<"SensorHistory">) {
  const { farmId, type } = route.params
  const [period, setPeriod] = useState<Period>("7d")
  const farm = useFarm(farmId)
  const thresholds = useThresholds()
  const threshold = thresholds.data?.find((item) => item.type === type)
  const history = useReadingHistory(farmId, type, period)
  const from = periodWindows(period)[0]?.from.toISOString().slice(0, 13)
  const alerts = useAlerts(farmId, { from: from ? `${from}:00:00Z` : undefined })

  const values = history.points.map((point) => point.value).filter((value): value is number => value !== null)
  const stats = statsOf(values)
  const unit = threshold?.unit ?? ""
  const outside = history.points.filter((point) => point.value !== null && outOfRange(point.value, threshold)).length
  const periodAlerts = (alerts.data ?? []).filter((alert) => alert.type === type)

  return (
    <Screen
      title="Histórico"
      subtitle={`${sensorLabel[type]}${farm.data ? ` - ${farm.data.name}` : ""}`}
      back="back"
      testID="history-screen"
    >
      <ChipGroup label="Período" scroll={false} options={periods} value={period} onChange={setPeriod} />

      {history.error ? (
        <ErrorState error={history.error} onRetry={history.refetch} />
      ) : history.isPending ? (
        <LoadingState rows={1} height={240} />
      ) : (
        <>
          <Card title={`${sensorLabel[type]} (${unit})`}>
            <LineChart
              points={history.points}
              threshold={threshold}
              height={220}
              accessibilityLabel={`Gráfico de ${sensorLabel[type].toLowerCase()} ${periods.find((item) => item.value === period)?.label}`}
            />
            <View style={styles.legend}>
              <AppText variant="caption">— Leitura</AppText>
              <AppText variant="caption">- - Limites</AppText>
              <View style={styles.legendItem}>
                <Icon name="change-history" size={14} color="error" />
                <AppText variant="caption">Fora do limite</AppText>
              </View>
            </View>
          </Card>

          {stats ? (
            <View style={styles.stats}>
              {(
                [
                  ["Mínimo", stats.min],
                  ["Média", stats.avg],
                  ["Máximo", stats.max],
                ] as const
              ).map(([label, value]) => (
                <Card key={label} style={styles.stat}>
                  <AppText variant="caption">{label}</AppText>
                  <AppText variant="number" color={outOfRange(value, threshold) ? "error" : "ink"}>
                    {formatNumber(value)} {unit}
                  </AppText>
                </Card>
              ))}
            </View>
          ) : (
            <AppText color="textSecondary">Sem leituras neste período.</AppText>
          )}
          {outside > 0 ? (
            <AppText variant="caption">
              {plural(outside, "ponto fora do limite", "pontos fora do limite")} neste período (média de cada
              intervalo).
            </AppText>
          ) : null}

          <Card title="Registo de alertas">
            {periodAlerts.length > 0 ? (
              periodAlerts.map((alert) => (
                <View key={alert.id} style={styles.alert}>
                  <Icon
                    name={alert.acknowledgedAt ? "check-circle-outline" : "warning"}
                    color={alert.acknowledgedAt ? "success" : "error"}
                  />
                  <View style={styles.flex}>
                    <AppText
                      onPress={() => navigation.navigate("Alert", { farmId, alertId: alert.id })}
                      accessibilityRole="link"
                    >
                      {alert.acknowledgedAt ? "Resolvido - " : ""}
                      {alertTitle(alert)} ({formatNumber(alert.value)} {alert.unit})
                    </AppText>
                    <AppText variant="caption">
                      {formatDateTime(alert.createdAt ?? "")}
                      {alert.acknowledgedAt ? "" : " - por resolver"}
                    </AppText>
                  </View>
                </View>
              ))
            ) : (
              <AppText color="textSecondary">Sem alertas neste período.</AppText>
            )}
          </Card>
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  legend: { flexDirection: "row", gap: spacing.md, flexWrap: "wrap" },
  legendItem: { flexDirection: "row", alignItems: "center", gap: 2 },
  stats: { flexDirection: "row", gap: spacing.sm },
  stat: { flex: 1, padding: spacing.sm, gap: 2 },
  alert: { flexDirection: "row", gap: spacing.sm, alignItems: "flex-start" },
  flex: { flex: 1 },
})
