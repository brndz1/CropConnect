import { useState } from "react"
import { StyleSheet, TextInput, View } from "react-native"
import type { Role, UserAccount } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Avatar } from "@/components/Avatar"
import { Button } from "@/components/Button"
import { Card } from "@/components/Card"
import { ChipGroup } from "@/components/Chip"
import { Icon } from "@/components/Icon"
import { IconButton } from "@/components/IconButton"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { noFocusOutline } from "@/components/webStyles"
import { plural, roleLabel } from "@/lib/format"
import { useDebouncedValue } from "@/lib/useDebouncedValue"
import type { AdminTabProps } from "@/navigation/types"
import { colors, fonts, radii, sizes, spacing } from "@/theme/tokens"
import { personName, useProfiles, useUsers } from "./queries"

type Filter = "all" | Role

const filters: { value: Filter; label: string }[] = [
  { value: "all", label: "Todos" },
  { value: "Client", label: "Clientes" },
  { value: "Manager", label: "Gestores" },
  { value: "Admin", label: "Administradores" },
]

// A01 Utilizadores: search by name or email and filter by profile (RF22).
export function UsersScreen({ navigation }: AdminTabProps<"Users">) {
  const [query, setQuery] = useState("")
  const [filter, setFilter] = useState<Filter>("all")
  const search = useDebouncedValue(query.trim())
  const users = useUsers({ search, role: filter === "all" ? undefined : filter })
  const list = users.data?.users ?? []
  const profiles = useProfiles(list.map((user) => user.id))

  const row = (user: UserAccount) => {
    const name = personName(user, profiles.get(user.id))
    return (
      <Card
        key={user.id}
        onPress={() => navigation.navigate("User", { userId: user.id })}
        accessibilityLabel={`${name}, ${user.email}, ${roleLabel[user.role]}, conta ${user.isActive ? "ativa" : "desativada"}`}
        testID={`user-${user.id}`}
      >
        <View style={styles.row}>
          <Avatar name={name} />
          <View style={styles.flex}>
            <AppText variant="bodyBold" numberOfLines={1}>
              {name}
            </AppText>
            <AppText variant="caption" numberOfLines={1}>
              {user.email}
            </AppText>
            <AppText variant="label" color={user.isActive ? "ink" : "error"}>
              {roleLabel[user.role]} - conta {user.isActive ? "ativa" : "desativada"}
            </AppText>
          </View>
          <Icon name="chevron-right" color="textSecondary" />
        </View>
      </Card>
    )
  }

  return (
    <Screen
      title="Utilizadores"
      right={<IconButton icon="add" label="Novo utilizador" onPress={() => navigation.navigate("UserForm")} />}
      refreshing={users.isRefetching}
      onRefresh={() => void users.refetch()}
      testID="users-screen"
    >
      <View style={styles.search}>
        <Icon name="search" color="textSecondary" />
        <TextInput
          value={query}
          onChangeText={setQuery}
          placeholder="Procurar por nome ou email"
          placeholderTextColor={colors.textSecondary}
          style={[styles.input, noFocusOutline]}
          autoCapitalize="none"
          accessibilityLabel="Procurar por nome ou email"
          maxFontSizeMultiplier={1.5}
        />
      </View>
      <ChipGroup label="Perfil" options={filters} value={filter} onChange={setFilter} />
      {users.isPending ? (
        <LoadingState rows={4} height={88} />
      ) : users.isError ? (
        <ErrorState error={users.error} onRetry={() => void users.refetch()} />
      ) : list.length === 0 ? (
        <EmptyState title="Nenhum utilizador encontrado" message="Experimente outro nome, email ou perfil." />
      ) : (
        <>
          <AppText variant="caption">{plural(users.data?.total ?? 0, "utilizador", "utilizadores")}</AppText>
          {list.map(row)}
          {users.hasNextPage ? (
            <Button
              label="Mostrar mais"
              variant="secondary"
              loading={users.isFetchingNextPage}
              onPress={() => void users.fetchNextPage()}
            />
          ) : null}
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
  row: { flexDirection: "row", alignItems: "center", gap: spacing.md },
  flex: { flex: 1 },
})
