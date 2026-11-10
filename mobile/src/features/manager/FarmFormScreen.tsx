import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Farm } from "@/api/types"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { Screen } from "@/components/Screen"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { required } from "@/lib/validation"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useCreateFarm, useUpdateFarm } from "./farmQueries"
import { useMyFarm } from "./queries"

const coordinate = (value: string) => {
  const text = value.trim().replace(",", ".")
  if (!text) return null
  const number = Number(text)
  return Number.isFinite(number) ? number : Number.NaN
}

function FarmForm({ farm, onDone }: { farm: Farm | null; onDone: () => void }) {
  const create = useCreateFarm()
  const update = useUpdateFarm()
  const snackbar = useSnackbar()
  const [name, setName] = useState(farm?.name ?? "")
  const [location, setLocation] = useState(farm?.location ?? "")
  const [description, setDescription] = useState(farm?.description ?? "")
  const [latitude, setLatitude] = useState(farm?.latitude?.toString() ?? "")
  const [longitude, setLongitude] = useState(farm?.longitude?.toString() ?? "")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)

  const changed =
    !farm ||
    name !== farm.name ||
    location !== farm.location ||
    description !== farm.description ||
    latitude !== (farm.latitude?.toString() ?? "") ||
    longitude !== (farm.longitude?.toString() ?? "")

  async function save() {
    const lat = coordinate(latitude)
    const lon = coordinate(longitude)
    const next = {
      name: required(name, "Indique o nome da quinta."),
      location: required(location, "Indique a morada para o levantamento."),
      latitude:
        lat !== null && (Number.isNaN(lat) || Math.abs(lat) > 90) ? "Latitude entre -90 e 90 (ex.: 41,53)." : null,
      longitude:
        lon !== null && (Number.isNaN(lon) || Math.abs(lon) > 180) ? "Longitude entre -180 e 180 (ex.: -8,61)." : null,
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean)) return
    const input = {
      name: name.trim(),
      location: location.trim(),
      description: description.trim(),
      latitude: lat,
      longitude: lon,
    }
    try {
      if (farm) await update.mutateAsync({ farm, input })
      else await create.mutateAsync(input)
      snackbar.show({ message: farm ? "Alterações guardadas." : "Quinta registada." })
      onDone()
    } catch (error) {
      setFormError(errorMessage(error))
    }
  }

  return (
    <Screen
      title={farm ? "Dados da quinta" : "Registar quinta"}
      back="back"
      footer={
        <Button
          label={farm ? "Guardar alterações" : "Registar quinta"}
          disabled={!changed}
          loading={create.isPending || update.isPending}
          onPress={save}
          testID="save-farm"
        />
      }
    >
      {formError ? <Banner tone="error" message={formError} /> : null}
      <TextField label="Nome da quinta" icon="storefront" value={name} onChangeText={setName} error={errors.name} />
      <TextField
        label="Morada de levantamento"
        icon="place"
        value={location}
        onChangeText={setLocation}
        error={errors.location}
        placeholder="Rua da Igreja 120, Barcelos"
      />
      <TextField label="Descrição" icon="notes" value={description} onChangeText={setDescription} multiline />
      <View style={styles.row}>
        <View style={styles.flex}>
          <TextField
            label="Latitude"
            value={latitude}
            onChangeText={setLatitude}
            error={errors.latitude}
            keyboardType="numbers-and-punctuation"
          />
        </View>
        <View style={styles.flex}>
          <TextField
            label="Longitude"
            value={longitude}
            onChangeText={setLongitude}
            error={errors.longitude}
            keyboardType="numbers-and-punctuation"
          />
        </View>
      </View>
      <Banner
        tone="info"
        message="As coordenadas mostram a quinta no mapa e permitem juntar a previsão meteorológica aos alertas dos sensores."
      />
    </Screen>
  )
}

// G22 Dados da quinta (and registering the farm when the manager has none yet, RF05).
export function FarmFormScreen({ navigation, route }: ManagerScreenProps<"FarmForm">) {
  const { farm } = useMyFarm()
  const editing = route.params?.farmId ? farm : null
  return <FarmForm key={editing?.version ?? "new"} farm={editing} onDone={() => navigation.goBack()} />
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
