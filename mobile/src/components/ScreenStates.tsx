import { useEffect, useState } from "react"
import { Animated, StyleSheet, View, type DimensionValue } from "react-native"
import { ApiError, errorMessage } from "@/api/errors"
import { colors, radii, spacing } from "@/theme/tokens"
import { AppText } from "./AppText"
import { Button } from "./Button"
import { Illustration } from "./Illustration"
import type { IllustrationName } from "./illustrations"

// Loading skeleton: grey blocks in the shape of the cards, gently pulsing.
export function Skeleton({ height, width = "100%" }: { height: number; width?: DimensionValue }) {
  const [opacity] = useState(() => new Animated.Value(0.5))
  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 1, duration: 700, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 0.5, duration: 700, useNativeDriver: true }),
      ]),
    )
    loop.start()
    return () => loop.stop()
  }, [opacity])
  return <Animated.View style={[styles.skeleton, { height, width, opacity }]} />
}

export function LoadingState({ rows = 3, height = 96 }: { rows?: number; height?: number }) {
  return (
    <View style={styles.list} accessibilityRole="progressbar" accessibilityLabel="A carregar">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} height={height} />
      ))}
    </View>
  )
}

interface EmptyStateProps {
  title: string
  message?: string
  illustration?: IllustrationName
  action?: { label: string; onPress: () => void }
}

// The empty state suggests the next action (table 12).
export function EmptyState({ title, message, illustration = "semResultados", action }: EmptyStateProps) {
  return (
    <View style={styles.centered}>
      <Illustration name={illustration} size={120} />
      <AppText variant="h3" align="center" accessibilityRole="header">
        {title}
      </AppText>
      {message ? (
        <AppText color="textSecondary" align="center">
          {message}
        </AppText>
      ) : null}
      {action ? <Button label={action.label} onPress={action.onPress} variant="secondary" /> : null}
    </View>
  )
}

// A failed load always offers "Tentar de novo"; without a connection it shows the no-network illustration.
export function ErrorState({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const offline = error instanceof ApiError && error.isNetwork
  return (
    <View style={styles.centered} accessibilityRole="alert">
      <Illustration name={offline ? "semRede" : "semResultados"} size={120} />
      <AppText variant="h3" align="center">
        {offline ? "Sem ligação à internet" : "Não foi possível carregar"}
      </AppText>
      <AppText color="textSecondary" align="center">
        {errorMessage(error)}
      </AppText>
      <Button label="Tentar de novo" icon="refresh" onPress={onRetry} variant="secondary" />
    </View>
  )
}

const styles = StyleSheet.create({
  skeleton: { backgroundColor: colors.surface, borderRadius: radii.card },
  list: { gap: spacing.md },
  centered: { alignItems: "center", gap: spacing.md, paddingVertical: spacing.xl, paddingHorizontal: spacing.md },
})
