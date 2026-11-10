import { Image, Pressable, StyleSheet, View } from "react-native"
import { useSession } from "@/auth/AuthProvider"
import { AppText } from "@/components/AppText"
import { ChipGroup } from "@/components/Chip"
import { Icon } from "@/components/Icon"
import { IconButton } from "@/components/IconButton"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { filterLabels, isBasket, isExpired, sortForCatalog, stockLevel, type CatalogFilter } from "@/lib/catalog"
import { plural } from "@/lib/format"
import type { ClientTabProps } from "@/navigation/types"
import { colors, radii, sizes, spacing } from "@/theme/tokens"
import { displayName, useMyProfile } from "../profile/queries"
import { BasketCard } from "./BasketCard"
import { CartButton } from "./CartButton"
import { ProductGrid } from "./ProductGrid"
import { useFarms, useProducts } from "./queries"

const filters = (Object.keys(filterLabels) as CatalogFilter[]).map((value) => ({ value, label: filterLabels[value] }))

// C02 Início: greeting, search, filters, the baskets of the week and products to discover.
export function HomeScreen({ navigation }: ClientTabProps<"Home">) {
  const session = useSession()
  const profile = useMyProfile()
  const products = useProducts()
  const farms = useFarms()

  const firstName = displayName(profile.data, session.email).split(" ")[0]
  const visible = sortForCatalog((products.data ?? []).filter((product) => !isExpired(product)))
  const baskets = visible.filter((product) => isBasket(product) && stockLevel(product) !== "soldOut")
  const highlights = visible.filter((product) => !isBasket(product)).slice(0, 6)

  const header = (
    <View style={styles.header}>
      <Image
        source={require("../../../assets/emblema_256.png")}
        style={styles.emblem}
        resizeMode="contain"
        accessibilityIgnoresInvertColors
      />
      <View style={styles.greeting}>
        <AppText variant="h3" color="green900" accessibilityRole="header">
          Olá, {firstName}
        </AppText>
        {farms.data ? (
          <AppText variant="caption">
            {plural(farms.data.length, "quinta na CropConnect", "quintas na CropConnect")}
          </AppText>
        ) : null}
      </View>
      <IconButton icon="notifications" label="Notificações" onPress={() => navigation.navigate("Notifications")} />
      <CartButton />
    </View>
  )

  return (
    <Screen
      header={header}
      refreshing={products.isRefetching}
      onRefresh={() => void products.refetch()}
      testID="home-screen"
    >
      <Pressable
        onPress={() => navigation.navigate("Explore", { query: "" })}
        accessibilityRole="search"
        accessibilityLabel="Procurar produtos ou quintas"
        style={styles.search}
        testID="home-search"
      >
        <Icon name="search" color="textSecondary" />
        <AppText color="textSecondary">Procurar produtos ou quintas</AppText>
      </Pressable>

      <ChipGroup
        label="Filtros"
        options={filters}
        value="all"
        onChange={(filter) => navigation.navigate("Explore", { filter })}
      />

      {products.isPending ? (
        <LoadingState rows={3} height={140} />
      ) : products.isError ? (
        <ErrorState error={products.error} onRetry={() => void products.refetch()} />
      ) : (
        <>
          <AppText variant="h3" accessibilityRole="header">
            Cabazes da semana
          </AppText>
          {baskets.length > 0 ? (
            baskets.map((basket) => (
              <BasketCard
                key={basket.id}
                product={basket}
                onPress={() => navigation.navigate("Product", { productId: basket.id })}
              />
            ))
          ) : (
            <AppText color="textSecondary">Esta semana ainda não há cabazes. Volte mais tarde.</AppText>
          )}

          <AppText variant="h3" accessibilityRole="header">
            Da quinta para si
          </AppText>
          {highlights.length > 0 ? (
            <ProductGrid products={highlights} />
          ) : (
            <EmptyState title="Ainda não há produtos" message="As quintas ainda não publicaram produtos." />
          )}
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  header: {
    flexDirection: "row",
    alignItems: "center",
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xs,
    minHeight: sizes.topBar,
    gap: spacing.sm,
  },
  emblem: { width: 44, height: 44 },
  greeting: { flex: 1 },
  search: {
    minHeight: sizes.touch + 4,
    borderRadius: radii.field,
    borderWidth: 1,
    borderColor: colors.outline,
    backgroundColor: colors.surface,
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
    paddingHorizontal: spacing.md,
  },
})
