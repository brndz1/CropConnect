import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import { AppText } from "@/components/AppText"
import { Badge } from "@/components/Badge"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { formatDateTime, formatNumber, formatRelative } from "@/lib/format"
import { alertTitle, isBelow, parseWeatherContext, rangeLabel, sensorOfAlert, since } from "@/lib/sensors"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { LineChart } from "./LineChart"
import { useAcknowledgeAlert, useAlerts, useRecentReadings } from "./queries"

// G11 Alerta de sensor: the reading against the limit and the rain forecast as context (RF17), or "previsão
// indisponível" without blocking the alert (G11W, RNF05); marking it resolved asks first (DR).
export function AlertScreen({ navigation, route }: ManagerScreenProps<"Alert">) {
  const { farmId, alertId } = route.params
  const alerts = useAlerts(farmId)
  const alert = alerts.data?.find((item) => item.id === alertId)
  const recent = useRecentReadings(farmId, alert?.type ?? "Humidity")
  const acknowledge = useAcknowledgeAlert(farmId)
  const snackbar = useSnackbar()
  const [confirming, setConfirming] = useState(false)

  if (alerts.isPending) {
    return (
      <Screen title="Alerta de sensor" back="back">
        <LoadingState rows={2} height={180} />
      </Screen>
    )
  }
  if (alerts.isError) {
    return (
      <Screen title="Alerta de sensor" back="back">
        <ErrorState error={alerts.error} onRetry={() => void alerts.refetch()} />
      </Screen>
    )
  }
  if (!alert) {
    return (
      <Screen title="Alerta de sensor" back="back">
        <EmptyState title="Alerta não encontrado" message="Pode já ter sido resolvido." />
      </Screen>
    )
  }

  const resolved = alert.acknowledgedAt !== null
  const weather = parseWeatherContext(alert.weatherContext)
  const lowHumidity = alert.type === "Humidity" && isBelow(alert)
  const threshold = { minValue: alert.minValue, maxValue: alert.maxValue, unit: alert.unit }
  const sensor = sensorOfAlert(alert)

  const resolve = () =>
    acknowledge.mutate(alert.id, {
      onSuccess: () => {
        setConfirming(false)
        snackbar.show({ message: "Alerta marcado como resolvido." })
        navigation.goBack()
      },
      onError: (error) => {
        setConfirming(false)
        snackbar.show({ tone: "error", message: errorMessage(error) })
      },
    })

  const footer = (
    <>
      {resolved ? null : (
        <Button label="Marcar como resolvido" icon="check" onPress={() => setConfirming(true)} testID="resolve-alert" />
      )}
      <Button
        label="Ver histórico"
        icon="history"
        variant="secondary"
        onPress={() =>
          navigation.navigate("SensorHistory", { farmId, type: alert.type, sensorId: sensor ?? undefined })
        }
      />
    </>
  )

  return (
    <Screen title="Alerta de sensor" back="back" footer={footer} testID="alert-screen">
      <View style={styles.status}>
        {resolved ? (
          <Badge label="Resolvido" tone="successSoft" icon="check" />
        ) : (
          <Badge label="Fora do limite" tone="error" icon="warning" />
        )}
        <AppText variant="caption">
          {resolved
            ? `Resolvido a ${formatDateTime(alert.acknowledgedAt ?? "")}`
            : `${since(alert.createdAt) ?? ""} - ${formatRelative(alert.createdAt ?? new Date())}`}
        </AppText>
      </View>
      <AppText variant="h2" accessibilityRole="header">
        {alertTitle(alert)}
      </AppText>
      {sensor ? <AppText color="textSecondary">Sensor {sensor}</AppText> : null}

      <Card title="Leitura vs limite">
        <View style={styles.reading}>
          <AppText variant="h1" color="error">
            {formatNumber(alert.value)}
          </AppText>
          <AppText color="textSecondary">
            {alert.unit}{" "}
            {isBelow(alert)
              ? `limite mínimo ${formatNumber(alert.minValue)} ${alert.unit}`
              : `limite máximo ${formatNumber(alert.maxValue)} ${alert.unit}`}
          </AppText>
        </View>
        <LineChart
          compact
          height={72}
          points={(recent.data ?? []).map((reading) => ({ label: "", value: reading.value }))}
          threshold={threshold}
          accessibilityLabel="Leituras recentes face ao limite"
        />
        <AppText variant="caption">Intervalo normal: {rangeLabel(threshold)} (definido pelo Administrador)</AppText>
      </Card>

      {lowHumidity ? (
        weather ? (
          <Card title="Previsão meteorológica · Open-Meteo">
            <View style={styles.weather}>
              <Icon
                name={weather.rain ? "water-drop" : "wb-sunny"}
                size={32}
                color={weather.rain ? "info" : "terracotta600"}
              />
              <View style={styles.flex}>
                <AppText variant="bodyBold">
                  {weather.rain ? "Chuva prevista" : "Sem chuva prevista"} nas próximas {weather.hours} h
                </AppText>
                <AppText variant="caption">
                  {formatNumber(weather.millimetres)} mm
                  {weather.chance !== null ? ` · até ${weather.chance}% de probabilidade` : ""}
                </AppText>
              </View>
            </View>
            <AppText>{weather.advice}</AppText>
          </Card>
        ) : (
          <Banner
            tone="warning"
            title="Previsão indisponível"
            message="Não foi possível obter a previsão meteorológica. Pode resolver o alerta na mesma."
          />
        )
      ) : null}

      <ConfirmDialog
        visible={confirming}
        icon="check-circle"
        title="Marcar o alerta como resolvido?"
        message="Confirme depois de agir no terreno. Se a leitura voltar a sair do limite, recebe um novo alerta."
        confirmLabel="Marcar como resolvido"
        loading={acknowledge.isPending}
        onConfirm={resolve}
        onCancel={() => setConfirming(false)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  status: { flexDirection: "row", alignItems: "center", gap: spacing.sm, flexWrap: "wrap" },
  reading: { flexDirection: "row", alignItems: "baseline", gap: spacing.xs, flexWrap: "wrap" },
  weather: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1 },
})
