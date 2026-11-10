import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Threshold } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { formatNumber } from "@/lib/format"
import { sensorIcon, sensorLabel } from "@/lib/sensors"
import { spacing } from "@/theme/tokens"
import { useThresholds } from "../sensors/queries"
import { useUpdateThreshold } from "./queries"

const toNumber = (value: string) => {
  const text = value
    .trim()
    .replace(",", ".")
    .replace(/[^\d.-]/g, "")
  return text && Number.isFinite(Number(text)) ? Number(text) : null
}

function Form({ thresholds }: { thresholds: Threshold[] }) {
  const update = useUpdateThreshold()
  const snackbar = useSnackbar()
  const [values, setValues] = useState(() =>
    Object.fromEntries(
      thresholds.map((item) => [item.type, { min: formatNumber(item.minValue), max: formatNumber(item.maxValue) }]),
    ),
  )
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const changes = thresholds
    .map((item) => {
      const entry = values[item.type]
      return { item, min: toNumber(entry?.min ?? ""), max: toNumber(entry?.max ?? "") }
    })
    .filter(({ item, min, max }) => min !== item.minValue || max !== item.maxValue)

  async function save() {
    const next: Record<string, string | null> = {}
    for (const { item, min, max } of changes) {
      next[item.type] =
        min === null || max === null
          ? "Indique números (ex.: 30)."
          : min >= max
            ? "O mínimo tem de ser menor do que o máximo."
            : null
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean)) return
    setBusy(true)
    try {
      for (const { item, min, max } of changes) {
        await update.mutateAsync({ threshold: item, minValue: min ?? item.minValue, maxValue: max ?? item.maxValue })
      }
      snackbar.show({ message: "Limites guardados. Aplicam-se às próximas leituras de todas as quintas." })
    } catch (error) {
      setFormError(errorMessage(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen
      title="Limites dos sensores"
      footer={
        <Button
          label="Guardar limites"
          disabled={changes.length === 0}
          loading={busy}
          onPress={save}
          testID="save-thresholds"
        />
      }
      testID="thresholds-screen"
    >
      <AppText color="textSecondary">Valores normais usados para gerar alertas em todas as quintas.</AppText>
      {formError ? <Banner tone="error" message={formError} /> : null}
      {thresholds.map((item) => (
        <Card key={item.type}>
          <View style={styles.title}>
            <Icon name={sensorIcon[item.type]} color="green700" />
            <AppText variant="bodyBold">
              {sensorLabel[item.type]} ({item.unit})
            </AppText>
          </View>
          <View style={styles.row}>
            <View style={styles.flex}>
              <TextField
                label="Mínimo"
                value={values[item.type]?.min ?? ""}
                onChangeText={(min) =>
                  setValues((current) => ({ ...current, [item.type]: { max: current[item.type]?.max ?? "", min } }))
                }
                keyboardType="numbers-and-punctuation"
                suffix={item.unit}
              />
            </View>
            <View style={styles.flex}>
              <TextField
                label="Máximo"
                value={values[item.type]?.max ?? ""}
                onChangeText={(max) =>
                  setValues((current) => ({ ...current, [item.type]: { min: current[item.type]?.min ?? "", max } }))
                }
                keyboardType="numbers-and-punctuation"
                suffix={item.unit}
              />
            </View>
          </View>
          {errors[item.type] ? (
            <AppText variant="caption" color="error">
              {errors[item.type]}
            </AppText>
          ) : null}
        </Card>
      ))}
    </Screen>
  )
}

// A20 Limites dos sensores: the global normal ranges that raise alerts (RF24). The API has humidity and temperature.
export function ThresholdsScreen() {
  const thresholds = useThresholds()
  if (thresholds.isPending) {
    return (
      <Screen title="Limites dos sensores">
        <LoadingState rows={2} height={140} />
      </Screen>
    )
  }
  if (thresholds.isError) {
    return (
      <Screen title="Limites dos sensores">
        <ErrorState error={thresholds.error} onRetry={() => void thresholds.refetch()} />
      </Screen>
    )
  }
  return <Form key={thresholds.data.map((item) => item.version).join()} thresholds={thresholds.data} />
}

const styles = StyleSheet.create({
  title: { flexDirection: "row", alignItems: "center", gap: spacing.xs },
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
