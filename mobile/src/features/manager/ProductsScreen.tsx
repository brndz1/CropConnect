import { Pressable, StyleSheet, View } from "react-native"
import { errorMessage } from "@/api/errors"
import type { Product } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Badge } from "@/components/Badge"
import { Button } from "@/components/Button"
import { Icon } from "@/components/Icon"
import { IconButton } from "@/components/IconButton"
import { Illustration } from "@/components/Illustration"
import { QuantityStepper } from "@/components/QuantityStepper"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { formatQuantity, illustrationFor, isBasket, isExpired, stockLevel } from "@/lib/catalog"
import { formatDate, formatPrice } from "@/lib/format"
import type { ManagerTabProps } from "@/navigation/types"
import { colors, radii, spacing } from "@/theme/tokens"
import { useAdjustStock, useFarmProducts } from "./farmQueries"
import { useMyFarm } from "./queries"

function ProductStatus({ product }: { product: Product }) {
  if (!product.isAvailable) return <Badge label="Inativo" tone="neutral" icon="visibility-off" />
  if (isExpired(product)) return <Badge label="Expirado" tone="neutral" icon="history" />
  const level = stockLevel(product)
  if (level === "soldOut") return <Badge label="Esgotado" tone="error" icon="warning" />
  if (level === "low") return <Badge label="Pouco stock" tone="warning" icon="error" />
  return <Badge label="Disponível" tone="successSoft" icon="check" />
}

// G16 Produtos: baskets, and the stock of each product adjusted with − and + right in the list (RF06, RF08).
export function ProductsScreen({ navigation }: ManagerTabProps<"Products">) {
  const { farm, ...farms } = useMyFarm()
  const products = useFarmProducts(farm?.id)
  const adjust = useAdjustStock()
  const snackbar = useSnackbar()

  const items = [...(products.data ?? [])].sort((a, b) => a.name.localeCompare(b.name, "pt"))
  const baskets = items.filter(isBasket)
  const single = items.filter((product) => !isBasket(product))

  const change = (product: Product, quantity: number) =>
    adjust.mutate(
      { productId: product.id, delta: quantity - product.stockQuantity },
      { onError: (error) => snackbar.show({ tone: "error", message: errorMessage(error) }) },
    )

  return (
    <Screen
      title="Produtos"
      right={
        farm ? <IconButton icon="add" label="Novo produto" onPress={() => navigation.navigate("ProductForm")} /> : null
      }
      refreshing={products.isRefetching}
      onRefresh={() => void products.refetch()}
      testID="products-screen"
    >
      {farms.isPending || (farm && products.isPending) ? (
        <LoadingState rows={4} height={88} />
      ) : farms.isError || products.isError ? (
        <ErrorState error={farms.error ?? products.error} onRetry={() => void products.refetch()} />
      ) : !farm ? (
        <EmptyState
          title="Registe primeiro a quinta"
          action={{ label: "Registar quinta", onPress: () => navigation.navigate("FarmForm") }}
        />
      ) : (
        <>
          <Button
            label="Criar cabaz"
            icon="redeem"
            variant="secondary"
            onPress={() => navigation.navigate("BasketForm")}
          />
          <AppText variant="label" color="textSecondary" accessibilityRole="header">
            Cabazes
          </AppText>
          {baskets.length === 0 ? <AppText color="textSecondary">Ainda não criou cabazes.</AppText> : null}
          {baskets.map((basket) => (
            <Pressable
              key={basket.id}
              onPress={() => navigation.navigate("BasketForm", { productId: basket.id })}
              accessibilityRole="button"
              accessibilityLabel={`Editar ${basket.name}`}
              style={styles.row}
            >
              <Illustration name={illustrationFor(basket)} size={40} background="terracotta" style={styles.thumb} />
              <View style={styles.flex}>
                <AppText variant="bodyBold">{basket.name}</AppText>
                <AppText variant="caption">
                  {isExpired(basket) && basket.availableUntil
                    ? `Terminou a ${formatDate(basket.availableUntil)} - fora do catálogo`
                    : `${formatPrice(basket.price)} - ${formatQuantity(basket.stockQuantity, "cabaz")}${basket.availableUntil ? ` - até ${formatDate(basket.availableUntil)}` : ""}`}
                </AppText>
                <ProductStatus product={basket} />
              </View>
              <Icon name="edit" color="textSecondary" />
            </Pressable>
          ))}

          <AppText variant="label" color="textSecondary" accessibilityRole="header">
            Stock (toque em − e + para ajustar)
          </AppText>
          {single.length === 0 ? (
            <EmptyState
              title="Ainda sem produtos"
              message="Publique os produtos da quinta para os clientes os reservarem."
              action={{ label: "Novo produto", onPress: () => navigation.navigate("ProductForm") }}
            />
          ) : null}
          {single.map((product) => (
            <View key={product.id} style={styles.row} testID={`stock-${product.id}`}>
              <Illustration name={illustrationFor(product)} size={40} background="green" style={styles.thumb} />
              <View style={styles.flex}>
                <AppText variant="bodyBold">{product.name}</AppText>
                <AppText variant="caption">
                  {formatPrice(product.price)} / {product.unit}
                </AppText>
                <ProductStatus product={product} />
                <Pressable
                  onPress={() => navigation.navigate("ProductForm", { productId: product.id })}
                  accessibilityRole="button"
                  accessibilityLabel={`Editar ${product.name}`}
                  style={styles.edit}
                >
                  <Icon name="edit" size={18} color="green700" />
                  <AppText variant="label" color="green700">
                    Editar
                  </AppText>
                </Pressable>
              </View>
              <QuantityStepper
                value={product.stockQuantity}
                min={0}
                max={9999}
                unit={product.unit.length <= 3 ? product.unit : undefined}
                label={product.name}
                onChange={(quantity) => change(product, quantity)}
              />
            </View>
          ))}
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  thumb: { width: 56, height: 56, borderRadius: radii.card },
  flex: { flex: 1, gap: 2 },
  edit: { flexDirection: "row", alignItems: "center", gap: spacing.xxs, minHeight: 40 },
})
