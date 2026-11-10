import { Image, StyleSheet, View } from "react-native"
import { AppText } from "@/components/AppText"
import { colors, spacing } from "@/theme/tokens"

// C00 Splash: negative logo on Verde Horta while the app restores the session.
export function SplashScreen() {
  return (
    <View style={styles.container} accessibilityLabel="CropConnect, a abrir" testID="splash">
      <Image
        source={require("../../../assets/logo_branco_480.png")}
        style={styles.logo}
        resizeMode="contain"
        accessibilityIgnoresInvertColors
      />
      <AppText color="white" style={styles.tagline}>
        Do campo à tua mesa
      </AppText>
    </View>
  )
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.green700, alignItems: "center", justifyContent: "center" },
  logo: { width: 200, height: 212 },
  tagline: { marginTop: spacing.lg, fontSize: 18 },
})
