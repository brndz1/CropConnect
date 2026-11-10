import * as SecureStore from "expo-secure-store"
import { Platform } from "react-native"

// The JWT and the refresh token live in the Keychain (iOS) or the Keystore (Android) through SecureStore. The web
// preview has no secure storage, so it falls back to localStorage, like the web app.

const TOKEN_KEY = "cropconnect.token"
const REFRESH_TOKEN_KEY = "cropconnect.refreshToken"

export interface StoredTokens {
  token: string
  refreshToken: string
}

const web = Platform.OS === "web"

async function read(key: string) {
  if (web) return globalThis.localStorage?.getItem(key) ?? null
  return SecureStore.getItemAsync(key)
}

async function write(key: string, value: string) {
  if (web) return globalThis.localStorage?.setItem(key, value)
  return SecureStore.setItemAsync(key, value, { keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY })
}

async function remove(key: string) {
  if (web) return globalThis.localStorage?.removeItem(key)
  return SecureStore.deleteItemAsync(key)
}

export const tokenStorage = {
  async load(): Promise<StoredTokens | null> {
    try {
      const [token, refreshToken] = await Promise.all([read(TOKEN_KEY), read(REFRESH_TOKEN_KEY)])
      return token && refreshToken ? { token, refreshToken } : null
    } catch {
      return null
    }
  },
  async save(tokens: StoredTokens) {
    await Promise.all([write(TOKEN_KEY, tokens.token), write(REFRESH_TOKEN_KEY, tokens.refreshToken)])
  },
  async clear() {
    await Promise.all([remove(TOKEN_KEY), remove(REFRESH_TOKEN_KEY)]).catch(() => undefined)
  },
}
