import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react"
import { AccessibilityInfo, Pressable, StyleSheet, View } from "react-native"
import { useSafeAreaInsets } from "react-native-safe-area-context"
import { colors, elevation, radii, sizes, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import MaterialIcons from "@expo/vector-icons/MaterialIcons"

export interface SnackbarMessage {
  message: string
  tone?: "success" | "warning" | "error"
  // "Anular" when the action can be undone, "Ver carrinho", "Tentar de novo" when it failed (table 12).
  action?: { label: string; onPress: () => void }
  duration?: number
}

interface SnackbarContextValue {
  show: (message: SnackbarMessage) => void
  hide: () => void
}

const SnackbarContext = createContext<SnackbarContextValue | null>(null)

export function SnackbarProvider({ children }: { children: ReactNode }) {
  const [current, setCurrent] = useState<SnackbarMessage | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  const insets = useSafeAreaInsets()

  const hide = useCallback(() => {
    if (timer.current) clearTimeout(timer.current)
    setCurrent(null)
  }, [])

  const show = useCallback(
    (message: SnackbarMessage) => {
      if (timer.current) clearTimeout(timer.current)
      setCurrent(message)
      AccessibilityInfo.announceForAccessibility(message.message)
      timer.current = setTimeout(hide, message.duration ?? (message.action ? 6000 : 3500))
    },
    [hide],
  )

  useEffect(() => () => (timer.current ? clearTimeout(timer.current) : undefined), [])

  const value = useMemo(() => ({ show, hide }), [show, hide])
  const icon = current?.tone === "error" ? "error" : current?.tone === "warning" ? "warning" : "check-circle"
  const iconColor = current?.tone === "error" ? "#F2B8B5" : current?.tone === "warning" ? colors.warning : "#A5D6A7"

  return (
    <SnackbarContext.Provider value={value}>
      {children}
      {current ? (
        <View
          pointerEvents="box-none"
          style={[styles.wrapper, { bottom: insets.bottom + sizes.bottomBar + spacing.md }]}
        >
          <View style={styles.snackbar} accessibilityLiveRegion="polite" testID="snackbar">
            <MaterialIcons name={icon} size={20} color={iconColor} accessibilityElementsHidden />
            <AppText color="white" style={styles.text}>
              {current.message}
            </AppText>
            {current.action ? (
              <Pressable
                accessibilityRole="button"
                onPress={() => {
                  current.action?.onPress()
                  hide()
                }}
                style={styles.action}
              >
                <AppText variant="label" style={styles.actionText}>
                  {current.action.label}
                </AppText>
              </Pressable>
            ) : null}
          </View>
        </View>
      ) : null}
    </SnackbarContext.Provider>
  )
}

export function useSnackbar() {
  const context = useContext(SnackbarContext)
  if (!context) throw new Error("useSnackbar must be used inside SnackbarProvider")
  return context
}

const styles = StyleSheet.create({
  wrapper: { position: "absolute", left: spacing.md, right: spacing.md },
  snackbar: {
    minHeight: sizes.touch,
    borderRadius: radii.card,
    backgroundColor: colors.ink,
    flexDirection: "row",
    alignItems: "center",
    paddingLeft: spacing.md,
    paddingRight: spacing.xs,
    gap: spacing.sm,
    ...elevation.level1,
  },
  text: { flex: 1, paddingVertical: spacing.sm, fontSize: 15 },
  actionText: { color: "#A5D6A7" },
  action: { minHeight: sizes.touch, justifyContent: "center", paddingHorizontal: spacing.sm },
})
