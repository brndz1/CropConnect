import { useEffect, useMemo, useRef, useState, type ComponentRef } from "react"
import { Pressable, StyleSheet, TextInput, View } from "react-native"
import { AppText } from "@/components/AppText"
import { ChipGroup } from "@/components/Chip"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { noFocusOutline } from "@/components/webStyles"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import {
  applyFilter,
  filterLabels,
  isExpired,
  normalize,
  sortForCatalog,
  stockLevel,
  type CatalogFilter,
} from "@/lib/catalog"
import { plural } from "@/lib/format"
import { useDebouncedValue } from "@/lib/useDebouncedValue"
import type { ClientTabProps } from "@/navigation/types"
import { colors, fonts, radii, sizes, spacing } from "@/theme/tokens"
import { CartButton } from "./CartButton"
import { FarmCard } from "./FarmCard"
import { ProductGrid } from "./ProductGrid"
import { useFarmRatings, useFarms, useProducts } from "./queries"

const filters = (Object.keys(filterLabels) as CatalogFilter[]).map((value) => ({ value, label: filterLabels[value] }))
const isFilter = (value: unknown): value is CatalogFilter => typeof value === "string" && value in filterLabels

// C03 Explorar: search by name, filter by category, availability or farm (RF04), with the loading (C03L),
// no-network (C03N) and empty states.
export function ExploreScreen({ navigation, route }: ClientTabProps<"Explore">) {
  const [query, setQuery] = useState(route.params?.query ?? "")
  const [filter, setFilter] = useState<CatalogFilter>(isFilter(route.params?.filter) ? route.params.filter : "all")
  const input = useRef<ComponentRef<typeof TextInput>>(null)
  const search = useDebouncedValue(query.trim())

  // Coming from Início: the chip or the search field chosen there.
  const [params, setParams] = useState(route.params)
  if (route.params !== params) {
    setParams(route.params)
    if (isFilter(route.params?.filter)) setFilter(route.params.filter)
    if (route.params?.query !== undefined) setQuery(route.params.query)
  }
  const wantsSearch = route.params?.query !== undefined
  useEffect(() => {
    if (wantsSearch) input.current?.focus()
  }, [wantsSearch, route.params])

  const products = useProducts({ search })
  const farms = useFarms()
  const showFarms = filter === "farms"

  const results = useMemo(
    () =>
      sortForCatalog(
        applyFilter(
          (products.data ?? []).filter((product) => !isExpired(product)),
          filter,
        ),
      ),
    [products.data, filter],
  )
  const matchingFarms = useMemo(() => {
    const text = normalize(search)
    return (farms.data ?? []).filter((farm) => !text || normalize(`${farm.name} ${farm.location}`).includes(text))
  }, [farms.data, search])
  const ratings = useFarmRatings(showFarms ? matchingFarms.map((farm) => farm.id) : [])
  const availableByFarm = useMemo(() => {
    const counts = new Map<string, number>()
    for (const product of products.data ?? []) {
      if (stockLevel(product) !== "soldOut") counts.set(product.farmId, (counts.get(product.farmId) ?? 0) + 1)
    }
    return counts
  }, [products.data])

  const active = showFarms ? farms : products
  const summary = showFarms
    ? plural(matchingFarms.length, "quinta", "quintas")
    : search
      ? `${plural(results.length, "resultado", "resultados")} para «${search}» - disponíveis hoje primeiro`
      : `${plural(results.length, "produto", "produtos")} - disponíveis hoje primeiro`

  return (
    <Screen
      title="Explorar"
      right={<CartButton />}
      refreshing={active.isRefetching}
      onRefresh={() => void active.refetch()}
      testID="explore-screen"
    >
      <View style={styles.search}>
        <Icon name="search" color="textSecondary" />
        <TextInput
          ref={input}
          value={query}
          onChangeText={setQuery}
          placeholder="Procurar produtos ou quintas"
          placeholderTextColor={colors.textSecondary}
          style={[styles.input, noFocusOutline]}
          returnKeyType="search"
          accessibilityLabel="Procurar produtos ou quintas"
          maxFontSizeMultiplier={1.5}
          testID="explore-search"
        />
        {query ? (
          <Pressable onPress={() => setQuery("")} accessibilityRole="button" accessibilityLabel="Limpar pesquisa">
            <Icon name="close" color="textSecondary" />
          </Pressable>
        ) : null}
      </View>

      <ChipGroup label="Filtros" options={filters} value={filter} onChange={setFilter} />

      {active.isPending ? (
        <LoadingState rows={3} height={180} />
      ) : active.isError ? (
        <ErrorState error={active.error} onRetry={() => void active.refetch()} />
      ) : (
        <>
          <AppText variant="caption" accessibilityLiveRegion="polite">
            {summary}
          </AppText>
          {showFarms ? (
            matchingFarms.length > 0 ? (
              matchingFarms.map((farm) => (
                <FarmCard
                  key={farm.id}
                  farm={farm}
                  rating={ratings.get(farm.id)}
                  availableProducts={availableByFarm.get(farm.id) ?? 0}
                  onPress={() => navigation.navigate("Farm", { farmId: farm.id })}
                />
              ))
            ) : (
              <EmptyState title="Nenhuma quinta encontrada" message="Experimente outro nome ou localidade." />
            )
          ) : results.length > 0 ? (
            <ProductGrid products={results} />
          ) : filter !== "all" && filter !== "available" ? (
            <EmptyState
              title={`Nada disponível em ${filterLabels[filter]} hoje`}
              message="As quintas atualizam o stock todos os dias."
              action={
                filter === "vegetables"
                  ? undefined
                  : { label: "Ver legumes disponíveis", onPress: () => setFilter("vegetables") }
              }
            />
          ) : (
            <EmptyState
              title="Sem resultados"
              message={search ? `Não encontrámos «${search}». Experimente outro nome.` : "Ainda não há produtos."}
            />
          )}
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
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
  input: { flex: 1, minHeight: sizes.touch, fontFamily: fonts.body, fontSize: 16, color: colors.ink },
})
