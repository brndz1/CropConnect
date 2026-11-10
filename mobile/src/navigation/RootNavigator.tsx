import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { SplashScreen } from "@/features/auth/SplashScreen"

export type RootStackParamList = {
  Splash: undefined
}

const Stack = createNativeStackNavigator<RootStackParamList>()

// Chooses the navigation of each profile (RF03). Filled in by the authentication flow.
export function RootNavigator() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="Splash" component={SplashScreen} />
    </Stack.Navigator>
  )
}
