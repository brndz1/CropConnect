import { StyleSheet, View } from "react-native"
import type { Reading, SensorType, Threshold } from "@/api/types"
import { spacing } from "@/theme/tokens"
import { SensorCard } from "./SensorCard"

interface SensorGridProps {
  readings: Reading[]
  thresholds: Threshold[]
  onOpen: (type: SensorType, sensorId: string) => void
}

// Sensor cards two by two (G01, G15).
export function SensorGrid({ readings, thresholds, onOpen }: SensorGridProps) {
  const rows: Reading[][] = []
  for (let index = 0; index < readings.length; index += 2) rows.push(readings.slice(index, index + 2))
  return (
    <View style={styles.grid}>
      {rows.map((row) => (
        <View key={row.map((reading) => reading.id).join()} style={styles.row}>
          {row.map((reading) => (
            <SensorCard
              key={reading.id}
              reading={reading}
              threshold={thresholds.find((threshold) => threshold.type === reading.type)}
              onPress={() => onOpen(reading.type, reading.sensorId)}
            />
          ))}
          {row.length === 1 ? <View style={styles.spacer} /> : null}
        </View>
      ))}
    </View>
  )
}

const styles = StyleSheet.create({
  grid: { gap: spacing.md },
  row: { flexDirection: "row", gap: spacing.md },
  spacer: { flex: 1 },
})
