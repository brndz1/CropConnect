import { Pressable, StyleSheet, View } from "react-native"
import type { AppNotification } from "@/api/types"
import { AppText } from "@/components/AppText"
import { Button } from "@/components/Button"
import { Icon } from "@/components/Icon"
import { Screen } from "@/components/Screen"
import { EmptyState, ErrorState, LoadingState } from "@/components/ScreenStates"
import { formatRelative } from "@/lib/format"
import { groupByDay, localizeNotification } from "@/lib/notifications"
import { colors, radii, spacing } from "@/theme/tokens"
import { useMarkAllRead, useMarkRead, useNotifications } from "./queries"
import { useOpenNotification } from "./useOpenNotification"

function NotificationRow({ notification, onPress }: { notification: AppNotification; onPress: () => void }) {
  const { title, message, icon } = localizeNotification(notification)
  const unread = notification.readAt === null
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${unread ? "Não lida. " : ""}${title}. ${message}`}
      style={({ pressed }) => [styles.row, unread && styles.unread, pressed && { backgroundColor: colors.surface }]}
      testID={`notification-${notification.id}`}
    >
      <View style={[styles.icon, unread && { backgroundColor: colors.background }]}>
        <Icon
          name={icon}
          size={20}
          color={notification.type === "SensorAlert" ? "error" : icon === "cancel" ? "error" : "green700"}
        />
      </View>
      <View style={styles.text}>
        <AppText variant={unread ? "bodyBold" : "body"}>{title}</AppText>
        <AppText variant="caption" color="ink">
          {message}
        </AppText>
        <AppText variant="caption">{notification.createdAt ? formatRelative(notification.createdAt) : ""}</AppText>
      </View>
      {unread ? <View style={styles.dot} /> : null}
    </Pressable>
  )
}

// C12 Notificações: state changes of the orders, new orders and sensor alerts, newest first (RF19).
export function NotificationsScreen() {
  const notifications = useNotifications()
  const markRead = useMarkRead()
  const markAll = useMarkAllRead()
  const open = useOpenNotification()

  const items = notifications.data?.items ?? []
  const { today, earlier } = groupByDay(items)
  const press = (notification: AppNotification) => {
    if (notification.readAt === null) markRead.mutate(notification.id)
    open(notification)
  }
  const section = (title: string, list: AppNotification[]) =>
    list.length > 0 ? (
      <>
        <AppText variant="label" color="textSecondary" accessibilityRole="header">
          {title}
        </AppText>
        {list.map((notification) => (
          <NotificationRow key={notification.id} notification={notification} onPress={() => press(notification)} />
        ))}
      </>
    ) : null

  return (
    <Screen
      title="Notificações"
      back="back"
      refreshing={notifications.isRefetching}
      onRefresh={() => void notifications.refetch()}
      contentStyle={styles.content}
      testID="notifications-screen"
    >
      {notifications.isPending ? (
        <LoadingState rows={4} height={72} />
      ) : notifications.isError ? (
        <ErrorState error={notifications.error} onRetry={() => void notifications.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState title="Sem notificações" message="Os avisos das encomendas e dos sensores aparecem aqui." />
      ) : (
        <>
          {(notifications.data?.unreadCount ?? 0) > 0 ? (
            <Button
              label="Marcar todas como lidas"
              icon="done-all"
              variant="text"
              loading={markAll.isPending}
              onPress={() => markAll.mutate()}
              style={styles.markAll}
            />
          ) : null}
          {section("Hoje", today)}
          {section("Anteriores", earlier)}
        </>
      )}
    </Screen>
  )
}

const styles = StyleSheet.create({
  content: { gap: spacing.xs },
  markAll: { alignSelf: "flex-end" },
  row: {
    flexDirection: "row",
    gap: spacing.sm,
    padding: spacing.sm,
    borderRadius: radii.card,
    alignItems: "flex-start",
  },
  unread: { backgroundColor: colors.green100 },
  icon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.surface,
    alignItems: "center",
    justifyContent: "center",
  },
  text: { flex: 1, gap: 2 },
  dot: { width: 10, height: 10, borderRadius: 5, backgroundColor: colors.success, marginTop: spacing.xs },
})
