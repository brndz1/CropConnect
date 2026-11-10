import { Pressable, StyleSheet, View } from "react-native"
import { colors, radii, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Icon } from "./Icon"

interface QuantityStepperProps {
  value: number
  onChange: (value: number) => void
  min?: number
  max: number
  unit?: string
  // What the item is called, for screen readers ("Tomate-cherry").
  label: string
}

// − and + of 48; each is disabled at the minimum or the maximum (stock or limit per client), table 12.
export function QuantityStepper({ value, onChange, min = 1, max, unit, label }: QuantityStepperProps) {
  const atMin = value <= min
  const atMax = value >= max
  const text = unit ? `${value} ${unit}` : String(value)

  return (
    <View
      style={styles.row}
      accessible
      accessibilityRole="adjustable"
      accessibilityLabel={`Quantidade de ${label}`}
      accessibilityValue={{ text }}
      accessibilityActions={[{ name: "increment" }, { name: "decrement" }]}
      onAccessibilityAction={(event) => {
        if (event.nativeEvent.actionName === "increment" && !atMax) onChange(value + 1)
        if (event.nativeEvent.actionName === "decrement" && !atMin) onChange(value - 1)
      }}
    >
      <Pressable
        testID="stepper-decrement"
        onPress={() => onChange(value - 1)}
        disabled={atMin}
        accessibilityRole="button"
        accessibilityLabel="Diminuir"
        accessibilityState={{ disabled: atMin }}
        style={[styles.button, styles.minus, atMin && styles.minusDisabled]}
      >
        <Icon name="remove" color={atMin ? "border" : "ink"} />
      </Pressable>
      <AppText variant="number" style={styles.value} testID="stepper-value">
        {text}
      </AppText>
      <Pressable
        testID="stepper-increment"
        onPress={() => onChange(value + 1)}
        disabled={atMax}
        accessibilityRole="button"
        accessibilityLabel="Aumentar"
        accessibilityState={{ disabled: atMax }}
        style={[styles.button, styles.plus, atMax && styles.plusDisabled]}
      >
        <Icon name="add" color={atMax ? "border" : "green700"} />
      </Pressable>
    </View>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
  button: {
    width: sizes.touch,
    height: sizes.touch,
    borderRadius: radii.round,
    alignItems: "center",
    justifyContent: "center",
  },
  minus: { borderWidth: 1, borderColor: colors.outline },
  minusDisabled: { borderColor: colors.border },
  plus: { backgroundColor: colors.green100 },
  plusDisabled: { backgroundColor: colors.surface },
  value: { minWidth: 56, textAlign: "center", fontSize: 18 },
})
