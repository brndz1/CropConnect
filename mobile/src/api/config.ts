import Constants from "expo-constants"
import { Platform } from "react-native"

const GATEWAY_PORT = 5000

// Where the API gateway is:
// - EXPO_PUBLIC_API_URL when set (a deployed gateway, or a fixed address on the local network);
// - on the web, the app's own origin: the Expo dev server forwards /api to the gateway (metro.config.js), like nginx
//   does for the web app, so the browser never needs CORS;
// - on a phone with Expo Go, port 5000 on the machine that runs Metro (the Android emulator reaches it as 10.0.2.2).
export function resolveApiBaseUrl(): string {
  const configured = process.env.EXPO_PUBLIC_API_URL
  if (configured) return configured.replace(/\/+$/, "")
  if (Platform.OS === "web") return ""

  const host = Constants.expoConfig?.hostUri?.split(":")[0]
  if (host && host !== "localhost" && host !== "127.0.0.1") return `http://${host}:${GATEWAY_PORT}`
  return Platform.OS === "android" ? `http://10.0.2.2:${GATEWAY_PORT}` : `http://localhost:${GATEWAY_PORT}`
}

export const apiBaseUrl = resolveApiBaseUrl()
