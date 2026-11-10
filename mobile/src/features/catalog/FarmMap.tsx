import { Linking, Platform, Pressable, StyleSheet, View } from "react-native"
import Svg, { Circle, Path, Rect } from "react-native-svg"
import { AppText } from "@/components/AppText"
import { Icon } from "@/components/Icon"
import { colors, spacing } from "@/theme/tokens"

// Opens the farm in the phone's maps app, by coordinates when the farm has them, else by its address.
export function openInMaps(location: string, latitude: number | null, longitude: number | null) {
  const query = latitude !== null && longitude !== null ? `${latitude},${longitude}` : encodeURIComponent(location)
  const url =
    Platform.OS === "ios"
      ? `https://maps.apple.com/?q=${query}`
      : `https://www.google.com/maps/search/?api=1&query=${query}`
  void Linking.openURL(url).catch(() => undefined)
}

// Illustrated map band of the farm detail (C08), in the colours of the visual system; a tap opens the maps app.
export function FarmMap({
  location,
  latitude,
  longitude,
}: {
  location: string
  latitude: number | null
  longitude: number | null
}) {
  return (
    <Pressable
      onPress={() => openInMaps(location, latitude, longitude)}
      accessibilityRole="link"
      accessibilityLabel={`Ver ${location} no mapa`}
      style={styles.band}
    >
      <Svg width="100%" height="100%" viewBox="0 0 390 160" preserveAspectRatio="xMidYMid slice">
        <Rect width="390" height="160" fill={colors.green100} />
        <Path d="M40 0 L120 160" stroke={colors.white} strokeWidth="10" />
        <Path d="M260 0 L210 160" stroke={colors.white} strokeWidth="10" />
        <Path d="M0 120 C 90 90, 160 110, 230 95 S 340 70, 390 75" stroke={colors.border} strokeWidth="9" fill="none" />
        <Circle cx="195" cy="70" r="26" fill={colors.green700} opacity={0.18} />
      </Svg>
      <View style={styles.pin} pointerEvents="none">
        <Icon name="place" size={36} color="green700" />
      </View>
      <View style={styles.hint} pointerEvents="none">
        <AppText variant="caption" color="green900">
          Ver no mapa
        </AppText>
      </View>
    </Pressable>
  )
}

const styles = StyleSheet.create({
  band: { height: 160, backgroundColor: colors.green100, overflow: "hidden" },
  pin: { position: "absolute", top: 48, left: 0, right: 0, alignItems: "center" },
  hint: {
    position: "absolute",
    right: spacing.sm,
    bottom: spacing.sm,
    backgroundColor: colors.background,
    borderRadius: 12,
    paddingHorizontal: spacing.xs,
    paddingVertical: 2,
  },
})
