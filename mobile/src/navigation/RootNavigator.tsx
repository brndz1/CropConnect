import { createNativeStackNavigator } from "@react-navigation/native-stack"
import { useAuth } from "@/auth/AuthProvider"
import { ConfirmDialog } from "@/components/ConfirmDialog"
import { CheckEmailScreen } from "@/features/auth/CheckEmailScreen"
import { ForgotPasswordScreen } from "@/features/auth/ForgotPasswordScreen"
import { LoginScreen } from "@/features/auth/LoginScreen"
import { RegisterScreen } from "@/features/auth/RegisterScreen"
import { SplashScreen } from "@/features/auth/SplashScreen"
import { AdminNavigator } from "./AdminNavigator"
import { ClientNavigator } from "./ClientNavigator"
import { ManagerNavigator } from "./ManagerNavigator"
import type { AuthStackParamList } from "./types"

const AuthStack = createNativeStackNavigator<AuthStackParamList>()

function AuthNavigator() {
  return (
    <AuthStack.Navigator screenOptions={{ headerShown: false, animation: "slide_from_right" }}>
      <AuthStack.Screen name="Login" component={LoginScreen} />
      <AuthStack.Screen name="Register" component={RegisterScreen} />
      <AuthStack.Screen name="ForgotPassword" component={ForgotPasswordScreen} />
      <AuthStack.Screen name="CheckEmail" component={CheckEmailScreen} />
    </AuthStack.Navigator>
  )
}

// One app for the three profiles: the role in the token decides the navigation (RF03, report section 2.1).
export function RootNavigator() {
  const { status, session, sessionExpired, dismissSessionExpired } = useAuth()

  let content
  if (status === "restoring") content = <SplashScreen />
  else if (!session) content = <AuthNavigator />
  else if (session.role === "Manager") content = <ManagerNavigator />
  else if (session.role === "Admin") content = <AdminNavigator />
  else content = <ClientNavigator />

  return (
    <>
      {content}
      <ConfirmDialog
        visible={sessionExpired}
        icon="lock"
        title="A sessão expirou"
        message="Por segurança, a sua sessão terminou. Inicie sessão de novo para continuar."
        confirmLabel="Iniciar sessão"
        cancelLabel="Fechar"
        onConfirm={dismissSessionExpired}
        onCancel={dismissSessionExpired}
      />
    </>
  )
}
