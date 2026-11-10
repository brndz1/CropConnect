import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { DayOfWeek, Farm } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Chip } from "@/components/Chip"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { isPlanEmpty, normalizeTime, planSchedules } from "@/lib/farm"
import { dayOrder, dayShort, formatTime } from "@/lib/format"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useFarm } from "../catalog/queries"
import { useSaveSchedules } from "./farmQueries"

function SchedulesForm({ farm, onDone }: { farm: Farm; onDone: () => void }) {
  const save = useSaveSchedules(farm.id)
  const snackbar = useSnackbar()
  const first = farm.pickupSchedules[0]
  const [days, setDays] = useState<DayOfWeek[]>(farm.pickupSchedules.map((item) => item.dayOfWeek))
  const [open, setOpen] = useState(first ? formatTime(first.openTime) : "09:00")
  const [close, setClose] = useState(first ? formatTime(first.closeTime) : "13:00")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const mixedHours = new Set(farm.pickupSchedules.map((item) => `${item.openTime}-${item.closeTime}`)).size > 1

  const openTime = normalizeTime(open)
  const closeTime = normalizeTime(close)
  const plan = openTime && closeTime ? planSchedules(farm.pickupSchedules, days, openTime, closeTime) : null

  const toggle = (day: DayOfWeek) =>
    setDays((current) => (current.includes(day) ? current.filter((item) => item !== day) : [...current, day]))

  function submit() {
    const next = {
      open: openTime ? null : "Hora no formato 09:00.",
      close: !closeTime
        ? "Hora no formato 13:00."
        : openTime && closeTime <= openTime
          ? "Fecha depois de abrir."
          : null,
      days: days.length ? null : "Escolha pelo menos um dia.",
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean) || !plan) return
    save.mutate(plan, {
      onSuccess: () => {
        snackbar.show({ message: "Horários guardados." })
        onDone()
      },
      onError: (error) => setFormError(errorMessage(error)),
    })
  }

  return (
    <Screen
      title="Horários"
      back="back"
      footer={
        <Button
          label="Guardar horários"
          loading={save.isPending}
          disabled={plan !== null && isPlanEmpty(plan)}
          onPress={submit}
          testID="save-schedules"
        />
      }
    >
      {formError ? <Banner tone="error" message={formError} /> : null}
      <AppText variant="label">Dias de levantamento</AppText>
      <View style={styles.days}>
        {dayOrder.map((day) => (
          <Chip key={day} label={dayShort[day]} selected={days.includes(day)} onPress={() => toggle(day)} />
        ))}
      </View>
      {errors.days ? (
        <AppText variant="caption" color="error">
          {errors.days}
        </AppText>
      ) : null}
      <View style={styles.row}>
        <View style={styles.flex}>
          <TextField
            label="Abre às"
            icon="schedule"
            value={open}
            onChangeText={setOpen}
            error={errors.open}
            placeholder="09:00"
          />
        </View>
        <View style={styles.flex}>
          <TextField
            label="Fecha às"
            icon="schedule"
            value={close}
            onChangeText={setClose}
            error={errors.close}
            placeholder="13:00"
          />
        </View>
      </View>
      {mixedHours ? (
        <Banner tone="warning" message="Os dias tinham horas diferentes: ao guardar, todos ficam com estas horas." />
      ) : null}
      <Banner
        tone="info"
        title="Reservas já feitas"
        message="As encomendas já feitas mantêm o horário escolhido pelo cliente."
      />
    </Screen>
  )
}

// G21 Horários de levantamento: the days and hours when the farm receives pickups (RF07).
export function PickupSchedulesScreen({ navigation, route }: ManagerScreenProps<"PickupSchedules">) {
  const farm = useFarm(route.params.farmId)
  if (farm.isPending) {
    return (
      <Screen title="Horários" back="back">
        <LoadingState rows={2} height={80} />
      </Screen>
    )
  }
  if (farm.isError) {
    return (
      <Screen title="Horários" back="back">
        <ErrorState error={farm.error} onRetry={() => void farm.refetch()} />
      </Screen>
    )
  }
  return <SchedulesForm key={farm.data.version} farm={farm.data} onDone={() => navigation.goBack()} />
}

const styles = StyleSheet.create({
  days: { flexDirection: "row", flexWrap: "wrap", gap: spacing.xs },
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
