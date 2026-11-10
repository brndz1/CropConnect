import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Product, ProductType } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { ChipGroup } from "@/components/Chip"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { formatDate } from "@/lib/format"
import { parseDate, parseDecimal, parseInteger, required } from "@/lib/validation"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useProduct } from "../catalog/queries"
import { useAdjustStock, useCreateProduct, useUpdateProduct } from "./farmQueries"
import { useMyFarm } from "./queries"

type BasketType = Exclude<ProductType, "Product">

function inDays(days: number) {
  const date = new Date()
  date.setDate(date.getDate() + days)
  return formatDate(date)
}

function BasketForm({ basket, farmId, onDone }: { basket: Product | null; farmId: string; onDone: () => void }) {
  const create = useCreateProduct(farmId)
  const update = useUpdateProduct()
  const adjust = useAdjustStock()
  const snackbar = useSnackbar()
  const [type, setType] = useState<BasketType>(
    basket?.type === "PreDefinedBasket" ? "PreDefinedBasket" : "SurpriseBasket",
  )
  const [name, setName] = useState(basket?.name ?? "")
  const [description, setDescription] = useState(basket?.description ?? "")
  const [contents, setContents] = useState(basket?.contents ?? "")
  const [price, setPrice] = useState(basket ? basket.price.toFixed(2).replace(".", ",") : "")
  const [quantity, setQuantity] = useState(basket ? String(basket.stockQuantity) : "")
  const [until, setUntil] = useState(basket?.availableUntil ? formatDate(basket.availableUntil) : inDays(5))
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const busy = create.isPending || update.isPending || adjust.isPending

  async function save() {
    const priceValue = parseDecimal(price)
    const quantityValue = parseInteger(quantity)
    const date = parseDate(until)
    const next = {
      name: required(name, "Indique o nome do cabaz."),
      description: required(description, "Descreva o cabaz ao cliente."),
      contents: type === "PreDefinedBasket" ? required(contents, "Indique o que leva o cabaz.") : null,
      price: priceValue === null || priceValue <= 0 ? "Indique o preço (ex.: 5,00)." : null,
      quantity: quantityValue === null || quantityValue < 1 ? "Indique quantos cabazes há." : null,
      until: !date ? "Indique a data no formato dd/mm/aaaa." : date < new Date() ? "A data limite já passou." : null,
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean) || priceValue === null || quantityValue === null || !date) return
    try {
      const body = {
        name: name.trim(),
        description: description.trim(),
        unit: "cabaz",
        price: priceValue,
        availableUntil: date.toISOString(),
        contents: contents.trim() || null,
      }
      if (basket) {
        await update.mutateAsync({ product: basket, changes: body })
        if (quantityValue !== basket.stockQuantity) {
          await adjust.mutateAsync({ productId: basket.id, delta: quantityValue - basket.stockQuantity })
        }
        snackbar.show({ message: "Alterações guardadas." })
      } else {
        await create.mutateAsync({ ...body, type, stockQuantity: quantityValue })
        snackbar.show({ message: "Cabaz publicado no catálogo." })
      }
      onDone()
    } catch (error) {
      setFormError(errorMessage(error))
    }
  }

  const deactivate = () => {
    if (!basket) return
    update.mutate(
      { product: basket, changes: { isAvailable: false } },
      {
        onSuccess: () => {
          setConfirming(false)
          snackbar.show({ message: "Cabaz retirado do catálogo." })
          onDone()
        },
        onError: (error) => {
          setConfirming(false)
          setFormError(errorMessage(error))
        },
      },
    )
  }

  const footer = (
    <>
      <Button
        label={basket ? "Guardar alterações" : "Publicar cabaz"}
        loading={busy}
        onPress={save}
        testID="save-basket"
      />
      {basket?.isAvailable ? (
        <Button label="Retirar do catálogo" variant="destructiveText" onPress={() => setConfirming(true)} />
      ) : null}
    </>
  )

  return (
    <Screen title={basket ? "Editar cabaz" : "Novo cabaz"} back="back" footer={footer}>
      {formError ? <Banner tone="error" message={formError} /> : null}
      <AppText variant="label">Tipo de cabaz</AppText>
      {basket ? (
        <AppText color="textSecondary">{type === "SurpriseBasket" ? "Surpresa" : "Pré-definido"}</AppText>
      ) : (
        <ChipGroup<BasketType>
          label="Tipo de cabaz"
          scroll={false}
          value={type}
          onChange={setType}
          options={[
            { value: "SurpriseBasket", label: "Surpresa" },
            { value: "PreDefinedBasket", label: "Pré-definido" },
          ]}
        />
      )}
      <TextField
        label="Nome"
        icon="redeem"
        value={name}
        onChangeText={setName}
        error={errors.name}
        testID="basket-name"
      />
      <TextField
        label="Descrição geral"
        icon="edit"
        value={description}
        onChangeText={setDescription}
        error={errors.description}
        multiline
        placeholder="Legumes da época perto do fim da validade"
      />
      {type === "PreDefinedBasket" ? (
        <TextField
          label="O que leva"
          icon="list"
          value={contents}
          onChangeText={setContents}
          error={errors.contents}
          multiline
          placeholder="Abóbora, cenoura, batata, alho-francês"
          hint="Separe os produtos por vírgulas."
        />
      ) : null}
      <View style={styles.row}>
        <View style={styles.flex}>
          <TextField
            label="Preço"
            icon="euro"
            value={price}
            onChangeText={setPrice}
            error={errors.price}
            keyboardType="decimal-pad"
            placeholder="5,00"
          />
        </View>
        <View style={styles.flex}>
          <TextField
            label="Quantidade"
            icon="inventory-2"
            value={quantity}
            onChangeText={setQuantity}
            error={errors.quantity}
            keyboardType="number-pad"
            suffix="cabazes"
          />
        </View>
      </View>
      <TextField
        label="Data limite de levantamento"
        icon="event"
        value={until}
        onChangeText={setUntil}
        error={errors.until}
        placeholder="dd/mm/aaaa"
        hint="O cabaz sai do catálogo depois desta data."
      />
      <Banner
        tone="info"
        title="O que o cliente vê"
        message={
          type === "SurpriseBasket"
            ? "Nos cabazes surpresa, o cliente vê só o preço, a descrição geral e a data limite. Cada cliente leva no máximo um."
            : "Nos cabazes pré-definidos, o cliente vê o preço, a descrição, o que leva e a data limite."
        }
      />

      <ConfirmDialog
        visible={confirming}
        destructive
        title={`Retirar ${basket?.name ?? "o cabaz"} do catálogo?`}
        message="Deixa de poder ser reservado. As encomendas já feitas mantêm-se."
        confirmLabel="Retirar do catálogo"
        cancelLabel="Manter"
        loading={update.isPending}
        onConfirm={deactivate}
        onCancel={() => setConfirming(false)}
      />
    </Screen>
  )
}

// G17 Novo cabaz surpresa and G17p cabaz pré-definido: price, quantity, description and pickup limit (RF08).
export function BasketFormScreen({ navigation, route }: ManagerScreenProps<"BasketForm">) {
  const productId = route.params?.productId
  const { farm } = useMyFarm()
  const basket = useProduct(productId ?? "")

  if (productId && basket.isPending) {
    return (
      <Screen title="Editar cabaz" back="back">
        <LoadingState rows={4} height={64} />
      </Screen>
    )
  }
  if (productId && basket.isError) {
    return (
      <Screen title="Editar cabaz" back="back">
        <ErrorState error={basket.error} onRetry={() => void basket.refetch()} />
      </Screen>
    )
  }
  return (
    <BasketForm
      key={basket.data?.version ?? "new"}
      basket={productId ? (basket.data ?? null) : null}
      farmId={farm?.id ?? basket.data?.farmId ?? ""}
      onDone={() => navigation.goBack()}
    />
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
