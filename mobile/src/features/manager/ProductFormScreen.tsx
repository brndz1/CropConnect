import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Product } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Banner } from "@/components/Banner"
import { Button } from "@/components/Button"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Illustration } from "@/components/Illustration"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { TextField } from "@/components/TextField"
import { categoryOf, illustrationFor } from "@/lib/catalog"
import { parseDecimal, parseInteger, required } from "@/lib/validation"
import type { ManagerScreenProps } from "@/navigation/types"
import { spacing } from "@/theme/tokens"
import { useProduct } from "../catalog/queries"
import { useAdjustStock, useCreateProduct, useUpdateProduct } from "./farmQueries"
import { useMyFarm } from "./queries"

interface FormProps {
  product: Product | null
  farmId: string
  onDone: () => void
}

function ProductForm({ product, farmId, onDone }: FormProps) {
  const create = useCreateProduct(farmId)
  const update = useUpdateProduct()
  const adjust = useAdjustStock()
  const snackbar = useSnackbar()
  const [name, setName] = useState(product?.name ?? "")
  const [description, setDescription] = useState(product?.description ?? "")
  const [price, setPrice] = useState(product ? product.price.toFixed(2).replace(".", ",") : "")
  const [unit, setUnit] = useState(product?.unit ?? "kg")
  const [stock, setStock] = useState(product ? String(product.stockQuantity) : "")
  const [errors, setErrors] = useState<Record<string, string | null>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const busy = create.isPending || update.isPending || adjust.isPending

  const preview = { name: name || "Produto", type: "Product" as const }
  const changed =
    !product ||
    name !== product.name ||
    description !== product.description ||
    parseDecimal(price) !== product.price ||
    unit !== product.unit ||
    parseInteger(stock) !== product.stockQuantity

  async function save() {
    const priceValue = parseDecimal(price)
    const stockValue = parseInteger(stock)
    const next = {
      name: required(name, "Indique o nome do produto."),
      price: priceValue === null || priceValue <= 0 ? "Indique o preço (ex.: 1,60)." : null,
      unit: required(unit, "Indique a unidade (ex.: kg, unidade, dúzia)."),
      stock: stockValue === null ? "Indique o stock disponível (número inteiro)." : null,
    }
    setErrors(next)
    setFormError(null)
    if (Object.values(next).some(Boolean) || priceValue === null || stockValue === null) return
    try {
      if (product) {
        await update.mutateAsync({
          product,
          changes: { name: name.trim(), description: description.trim(), unit: unit.trim(), price: priceValue },
        })
        if (stockValue !== product.stockQuantity) {
          await adjust.mutateAsync({ productId: product.id, delta: stockValue - product.stockQuantity })
        }
        snackbar.show({ message: "Alterações guardadas." })
      } else {
        await create.mutateAsync({
          name: name.trim(),
          description: description.trim() || null,
          unit: unit.trim(),
          price: priceValue,
          stockQuantity: stockValue,
          type: "Product",
          availableUntil: null,
          contents: null,
        })
        snackbar.show({ message: "Produto publicado no catálogo." })
      }
      onDone()
    } catch (error) {
      setFormError(errorMessage(error))
    }
  }

  const toggleActive = () => {
    if (!product) return
    update.mutate(
      { product, changes: { isAvailable: !product.isAvailable } },
      {
        onSuccess: () => {
          setConfirming(false)
          snackbar.show({ message: product.isAvailable ? "Produto desativado." : "Produto reativado." })
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
        label={product ? "Guardar alterações" : "Publicar produto"}
        loading={busy}
        disabled={!changed}
        onPress={save}
        testID="save-product"
      />
      {product ? (
        <Button
          label={product.isAvailable ? "Desativar produto" : "Reativar produto"}
          variant={product.isAvailable ? "destructiveText" : "text"}
          onPress={() => (product.isAvailable ? setConfirming(true) : toggleActive())}
        />
      ) : null}
    </>
  )

  return (
    <Screen title={product ? "Editar produto" : "Novo produto"} back="back" footer={footer}>
      {formError ? <Banner tone="error" message={formError} /> : null}
      {product && !product.isAvailable ? (
        <Banner tone="warning" message="Este produto está desativado e não aparece no catálogo." />
      ) : null}
      <View style={styles.preview}>
        <Illustration name={illustrationFor(preview)} size={56} background="green" style={styles.thumb} />
        <AppText variant="caption" style={styles.flex}>
          A ilustração e a categoria ({categoryOf(preview)}) são escolhidas pelo nome do produto.
        </AppText>
      </View>
      <TextField
        label="Nome"
        icon="eco"
        value={name}
        onChangeText={setName}
        error={errors.name}
        testID="product-name"
      />
      <TextField label="Descrição (opcional)" icon="edit" value={description} onChangeText={setDescription} multiline />
      <View style={styles.row}>
        <View style={styles.flex}>
          <TextField
            label="Preço"
            icon="euro"
            value={price}
            onChangeText={setPrice}
            error={errors.price}
            keyboardType="decimal-pad"
            placeholder="1,60"
            testID="product-price"
          />
        </View>
        <View style={styles.flex}>
          <TextField label="Unidade" icon="inventory-2" value={unit} onChangeText={setUnit} error={errors.unit} />
        </View>
      </View>
      <TextField
        label="Stock disponível"
        icon="inventory-2"
        value={stock}
        onChangeText={setStock}
        error={errors.stock}
        keyboardType="number-pad"
        suffix={unit}
        hint="Quando chega a 0, o produto aparece como «Esgotado»."
        testID="product-stock"
      />

      <ConfirmDialog
        visible={confirming}
        destructive
        title={`Desativar ${product?.name ?? "o produto"}?`}
        message="Sai do catálogo e deixa de poder ser reservado. As encomendas já feitas mantêm-se. Pode reativá-lo depois."
        confirmLabel="Desativar produto"
        cancelLabel="Manter"
        loading={update.isPending}
        onConfirm={toggleActive}
        onCancel={() => setConfirming(false)}
      />
    </Screen>
  )
}

// G18 Novo produto and G19 Editar produto, with deactivation behind a confirmation (DG-desprod, RF06).
export function ProductFormScreen({ navigation, route }: ManagerScreenProps<"ProductForm">) {
  const productId = route.params?.productId
  const { farm } = useMyFarm()
  const product = useProduct(productId ?? "")

  if (productId && product.isPending) {
    return (
      <Screen title="Editar produto" back="back">
        <LoadingState rows={4} height={64} />
      </Screen>
    )
  }
  if (productId && product.isError) {
    return (
      <Screen title="Editar produto" back="back">
        <ErrorState error={product.error} onRetry={() => void product.refetch()} />
      </Screen>
    )
  }
  return (
    <ProductForm
      key={product.data?.version ?? "new"}
      product={productId ? (product.data ?? null) : null}
      farmId={farm?.id ?? product.data?.farmId ?? ""}
      onDone={() => navigation.goBack()}
    />
  )
}

const styles = StyleSheet.create({
  preview: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  thumb: { width: 72, height: 72 },
  row: { flexDirection: "row", gap: spacing.md },
  flex: { flex: 1 },
})
