import { StyleSheet, View } from "react-native"
import type { Reading, Threshold } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Badge } from "@/components/Badge"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { formatNumber } from "@/lib/format"
import { rangeLabel, sensorIcon, sensorLabel, sensorState, type SensorState } from "@/lib/sensors"
import { colors, spacing } from "@/theme/tokens"
import { LineChart } from "./LineChart"
import { useRecentReadings } from "./queries"

export function SensorStateBadge({ state }: { state: SensorState }) {
  if (state === "out") return <Badge label="Fora do limite" tone="error" icon="warning" />
  if (state === "near") return <Badge label="Perto do limite" tone="warning" icon="error" />
  return <Badge label="Normal" tone="successSoft" icon="check" />
}

interface SensorCardProps {
  reading: Reading
  threshold: Threshold | undefined
  onPress: () => void
}

// Sensor card (table 12): current value in H1, unit, state, normal range and a small chart with the dashed limit.
export function SensorCard({ reading, threshold, onPress }: SensorCardProps) {
  const recent = useRecentReadings(reading.farmId, reading.type)
  const state = sensorState(reading.value, threshold)
  const points = (recent.data ?? []).map((item) => ({ label: "", value: item.value }))

  return (
    <Card
      onPress={onPress}
      highlight={state === "out" ? "error" : undefined}
      accessibilityLabel={`${sensorLabel[reading.type]}, ${formatNumber(reading.value)} ${reading.unit}, ${state === "out" ? "fora do limite" : state === "near" ? "perto do limite" : "normal"}`}
      style={styles.card}
      testID={`sensor-${reading.type}`}
    >
      <View style={styles.title}>
        <View style={[styles.icon, { backgroundColor: state === "out" ? colors.error100 : colors.green100 }]}>
          <Icon name={sensorIcon[reading.type]} size={18} color={state === "out" ? "error" : "green700"} />
        </View>
        <AppText variant="label" style={styles.flex}>
          {sensorLabel[reading.type]}
        </AppText>
      </View>
      <AppText variant="caption">Sensor {reading.sensorId}</AppText>
      <AppText variant="h1" color={state === "out" ? "error" : "ink"}>
        {formatNumber(reading.value)}
        <AppText variant="body" color="textSecondary">
          {" "}
          {reading.unit}
        </AppText>
      </AppText>
      <SensorStateBadge state={state} />
      {threshold ? <AppText variant="caption">Normal: {rangeLabel(threshold)}</AppText> : null}
      <LineChart
        compact
        height={44}
        points={points}
        threshold={threshold}
        accessibilityLabel={`Últimas leituras de ${sensorLabel[reading.type].toLowerCase()}`}
      />
    </Card>
  )
}

const styles = StyleSheet.create({
  card: { flex: 1, gap: spacing.xxs },
  title: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
  icon: { width: 32, height: 32, borderRadius: 16, alignItems: "center", justifyContent: "center" },
  flex: { flex: 1 },
})
