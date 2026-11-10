import { useQuery } from "@tanstack/react-query"
import { useState } from "react"
import { StyleSheet, View } from "react-native"
import { call, client } from "@/api/client"
import { errorMessage } from "@/api/errors"
import { queryKeys } from "@/api/queryKeys"
import { AppText } from "@/components/AppText"
import { Badge } from "@/components/Badge"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { Screen } from "@/components/Screen"
import { ErrorState, LoadingState } from "@/components/ScreenStates"
import { useSnackbar } from "@/components/Snackbar"
import { summarizeSchedules } from "@/lib/schedule"
import type { AdminScreenProps } from "@/navigation/types"
import { colors, spacing } from "@/theme/tokens"
import { useFarm } from "../catalog/queries"
import { personName, useSetFarmActive, useUser, useUserProfile } from "./queries"

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row} accessible accessibilityLabel={`${label}: ${value}`}>
      <AppText color="textSecondary">{label}</AppText>
      <AppText style={styles.value}>{value}</AppText>
    </View>
  )
}

// A11 Quinta: details, manager and state; deactivating asks first (DA-vale) and can be undone (RF23). The API has no
// way to hand a farm to another manager, so "Associar gestor" opens the manager's account instead.
export function AdminFarmScreen({ navigation, route }: AdminScreenProps<"AdminFarm">) {
  const farm = useFarm(route.params.farmId)
  const owner = useUser(farm.data?.ownerId ?? "")
  const ownerProfile = useUserProfile(farm.data?.ownerId)
  const products = useQuery({
    queryKey: queryKeys.products({ farmId: route.params.farmId, count: true }),
    queryFn: () =>
      call(client.GET("/api/products", { params: { query: { farmId: route.params.farmId, page: 1, pageSize: 1 } } })),
  })
  const setActive = useSetFarmActive()
  const snackbar = useSnackbar()
  const [confirming, setConfirming] = useState(false)

  if (farm.isPending) {
    return (
      <Screen title="Quinta" back="back">
        <LoadingState rows={2} height={120} />
      </Screen>
    )
  }
  if (farm.isError) {
    return (
      <Screen title="Quinta" back="back">
        <ErrorState error={farm.error} onRetry={() => void farm.refetch()} />
      </Screen>
    )
  }

  const data = farm.data
  const toggle = (isActive: boolean) =>
    setActive.mutate(
      { farm: data, isActive },
      {
        onSuccess: () => {
          setConfirming(false)
          snackbar.show({
            message: isActive ? `${data.name} reativada.` : `${data.name} desativada.`,
            action: isActive ? undefined : { label: "Anular", onPress: () => toggle(true) },
          })
        },
        onError: (error) => {
          setConfirming(false)
          snackbar.show({ tone: "error", message: errorMessage(error) })
        },
      },
    )

  const footer = (
    <>
      {owner.data ? (
        <Button
          label="Ver gestor"
          icon="person"
          variant="secondary"
          onPress={() => navigation.navigate("User", { userId: data.ownerId })}
        />
      ) : null}
      {data.isActive ? (
        <Button
          label="Desativar quinta"
          variant="destructiveText"
          onPress={() => setConfirming(true)}
          testID="deactivate-farm"
        />
      ) : (
        <Button label="Reativar quinta" variant="text" loading={setActive.isPending} onPress={() => toggle(true)} />
      )}
    </>
  )

  return (
    <Screen title={data.name} back="back" footer={footer} testID="admin-farm-screen">
      {data.isActive ? (
        <Badge label="Quinta ativa" tone="successSoft" icon="check" />
      ) : (
        <Badge label="Quinta desativada" tone="error" icon="block" />
      )}
      <Card title="Dados da quinta">
        <Row label="Localidade" value={data.location} />
        <Row label="Produtos ativos" value={products.data ? String(products.data.totalCount) : "…"} />
        <Row label="Gestor" value={owner.data ? personName(owner.data, ownerProfile.data) : "…"} />
        <Row label="Levantamentos" value={summarizeSchedules(data.pickupSchedules)} />
      </Card>
      {!data.isActive ? (
        <AppText color="textSecondary">
          Uma quinta desativada não aparece no catálogo e os sensores deixam de gerar alertas.
        </AppText>
      ) : null}

      <ConfirmDialog
        visible={confirming}
        destructive
        title={`Desativar ${data.name}?`}
        message="A quinta sai do catálogo e deixa de receber encomendas. As encomendas em curso mantêm-se. Pode reativá-la depois."
        confirmLabel="Desativar quinta"
        cancelLabel="Manter"
        loading={setActive.isPending}
        onConfirm={() => toggle(false)}
        onCancel={() => setConfirming(false)}
      />
    </Screen>
  )
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    justifyContent: "space-between",
    gap: spacing.md,
    paddingVertical: spacing.xxs,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  value: { flexShrink: 1, textAlign: "right" },
})
