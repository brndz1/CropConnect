import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import type { Farm } from "@/lib/types"

export interface FarmForm {
  name: string
  description: string
  location: string
  latitude: string
  longitude: string
}

export const emptyFarmForm: FarmForm = { name: "", description: "", location: "", latitude: "", longitude: "" }

export const farmToForm = (farm: Farm): FarmForm => ({
  name: farm.name,
  description: farm.description,
  location: farm.location,
  latitude: farm.latitude?.toString().replace(".", ",") ?? "",
  longitude: farm.longitude?.toString().replace(".", ",") ?? "",
})

const coordinate = (value: string) => (value.trim() ? Number(value.trim().replace(",", ".")) : null)

export const farmBody = (form: FarmForm) => ({
  name: form.name.trim(),
  description: form.description.trim(),
  location: form.location.trim(),
  latitude: coordinate(form.latitude),
  longitude: coordinate(form.longitude),
})

export function validateFarm(form: FarmForm) {
  if (!form.name.trim()) return "Indique o nome da quinta."
  if (!form.location.trim()) return "Indique a localização."
  const { latitude, longitude } = farmBody(form)
  if ((latitude === null) !== (longitude === null)) return "Indique a latitude e a longitude, ou nenhuma."
  if (latitude !== null && (!Number.isFinite(latitude) || latitude < -90 || latitude > 90))
    return "A latitude tem de estar entre -90 e 90."
  if (longitude !== null && (!Number.isFinite(longitude) || longitude < -180 || longitude > 180))
    return "A longitude tem de estar entre -180 e 180."
  return undefined
}

export function FarmFields({ form, onChange }: { form: FarmForm; onChange: (form: FarmForm) => void }) {
  return (
    <>
      <div className="grid gap-2">
        <Label htmlFor="farm-name">Nome da quinta</Label>
        <Input
          id="farm-name"
          placeholder="Ex.: Quinta do Vale"
          maxLength={120}
          value={form.name}
          onChange={(e) => onChange({ ...form, name: e.target.value })}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="farm-location">Localização</Label>
        <Input
          id="farm-location"
          placeholder="Braga, Portugal"
          maxLength={200}
          value={form.location}
          onChange={(e) => onChange({ ...form, location: e.target.value })}
        />
      </div>
      <div className="grid gap-2">
        <Label htmlFor="farm-description">Descrição e história</Label>
        <Textarea
          id="farm-description"
          rows={3}
          maxLength={1000}
          value={form.description}
          onChange={(e) => onChange({ ...form, description: e.target.value })}
        />
      </div>
      <div className="grid grid-cols-2 gap-4">
        <div className="grid gap-2">
          <Label htmlFor="farm-lat">Latitude</Label>
          <Input
            id="farm-lat"
            inputMode="decimal"
            placeholder="41,55"
            value={form.latitude}
            onChange={(e) => onChange({ ...form, latitude: e.target.value })}
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="farm-lon">Longitude</Label>
          <Input
            id="farm-lon"
            inputMode="decimal"
            placeholder="-8,42"
            value={form.longitude}
            onChange={(e) => onChange({ ...form, longitude: e.target.value })}
          />
        </div>
      </div>
      <p className="-mt-2 text-sm text-muted-foreground">
        As coordenadas são opcionais e servem para a previsão de chuva nos alertas.
      </p>
    </>
  )
}
