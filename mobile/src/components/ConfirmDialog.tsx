import { Modal, Pressable, StyleSheet, View } from "react-native"
import { colors, elevation, radii, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Button } from "./Button"
import { Icon, type IconName } from "./Icon"

interface ConfirmDialogProps {
  visible: boolean
  title: string
  message: string
  confirmLabel: string
  // "Manter" or "Cancelar": a destructive action always has a way out next to it (table 12).
  cancelLabel?: string
  destructive?: boolean
  icon?: IconName
  loading?: boolean
  onConfirm: () => void
  onCancel: () => void
}

// Confirmation dialog over a dimmed screen, used for state changes and every destructive action (RNF06).
export function ConfirmDialog({
  visible,
  title,
  message,
  confirmLabel,
  cancelLabel = "Cancelar",
  destructive = false,
  icon,
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const iconName = icon ?? (destructive ? "warning" : "check-circle")
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onCancel} statusBarTranslucent>
      <View style={styles.scrim}>
        <Pressable style={StyleSheet.absoluteFill} onPress={onCancel} accessibilityLabel={cancelLabel} />
        <View style={styles.dialog} accessibilityViewIsModal accessibilityRole="alert">
          <View style={[styles.icon, { backgroundColor: destructive ? colors.error100 : colors.green100 }]}>
            <Icon name={iconName} color={destructive ? "error" : "green700"} />
          </View>
          <AppText variant="h3" accessibilityRole="header">
            {title}
          </AppText>
          <AppText color="textSecondary">{message}</AppText>
          <View style={styles.actions}>
            <Button
              label={confirmLabel}
              variant={destructive ? "destructive" : "primary"}
              onPress={onConfirm}
              loading={loading}
              testID="dialog-confirm"
            />
            <Button label={cancelLabel} variant="text" onPress={onCancel} disabled={loading} testID="dialog-cancel" />
          </View>
        </View>
      </View>
    </Modal>
  )
}

const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    backgroundColor: colors.scrim,
    justifyContent: "center",
    padding: spacing.xl,
  },
  dialog: {
    backgroundColor: colors.background,
    borderRadius: radii.sheet,
    padding: spacing.lg,
    gap: spacing.sm,
    maxWidth: 420,
    width: "100%",
    alignSelf: "center",
    ...elevation.level1,
  },
  icon: { width: 48, height: 48, borderRadius: 24, alignItems: "center", justifyContent: "center" },
  actions: { gap: spacing.xxs, marginTop: spacing.xs },
})
