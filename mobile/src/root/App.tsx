import { Lato_400Regular, Lato_700Bold } from "@expo-google-fonts/lato"
import { Poppins_400Regular, Poppins_500Medium, Poppins_600SemiBold, Poppins_700Bold } from "@expo-google-fonts/poppins"
import { DefaultTheme, NavigationContainer, type Theme } from "@react-navigation/native"
import { QueryClientProvider } from "@tanstack/react-query"
import { useFonts } from "expo-font"
import * as ExpoSplashScreen from "expo-splash-screen"
import { StatusBar } from "expo-status-bar"
import { useEffect, useState } from "react"
import { SafeAreaProvider } from "react-native-safe-area-context"
import { AuthProvider } from "@/auth/AuthProvider"
import { SnackbarProvider } from "@/components/Snackbar"
import { RootNavigator } from "@/navigation/RootNavigator"
import { colors, fonts } from "@/theme/tokens"
import { createQueryClient } from "./queryClient"

void ExpoSplashScreen.preventAutoHideAsync().catch(() => undefined)

const navigationTheme: Theme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    primary: colors.green700,
    background: colors.background,
    card: colors.background,
    text: colors.ink,
    border: colors.border,
    notification: colors.error,
  },
  fonts: {
    regular: { fontFamily: fonts.body, fontWeight: "400" },
    medium: { fontFamily: fonts.headingMedium, fontWeight: "500" },
    bold: { fontFamily: fonts.headingSemiBold, fontWeight: "600" },
    heavy: { fontFamily: fonts.heading, fontWeight: "700" },
  },
}

export default function App() {
  const [queryClient] = useState(createQueryClient)
  const [fontsLoaded, fontError] = useFonts({
    Poppins_400Regular,
    Poppins_500Medium,
    Poppins_600SemiBold,
    Poppins_700Bold,
    Lato_400Regular,
    Lato_700Bold,
  })
  const ready = fontsLoaded || fontError !== null

  useEffect(() => {
    if (ready) void ExpoSplashScreen.hideAsync().catch(() => undefined)
  }, [ready])

  if (!ready) return null

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <SnackbarProvider>
            <NavigationContainer
              theme={navigationTheme}
              documentTitle={{
                formatter: (options) => (options?.title ? `${options.title} · CropConnect` : "CropConnect"),
              }}
            >
              <StatusBar style="dark" />
              <RootNavigator />
            </NavigationContainer>
          </SnackbarProvider>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  )
}
