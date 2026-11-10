// Test helpers: renders a screen with the providers it needs.
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render } from "@testing-library/react-native"
import type { ReactElement } from "react"
import { SafeAreaProvider } from "react-native-safe-area-context"
import { SnackbarProvider } from "@/components/Snackbar"

const metrics = {
  frame: { x: 0, y: 0, width: 390, height: 844 },
  insets: { top: 0, left: 0, right: 0, bottom: 0 },
}

export function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  })
}

export async function renderWithProviders(element: ReactElement, queryClient = createTestQueryClient()) {
  const result = await render(
    <SafeAreaProvider initialMetrics={metrics}>
      <QueryClientProvider client={queryClient}>
        <SnackbarProvider>{element}</SnackbarProvider>
      </QueryClientProvider>
    </SafeAreaProvider>,
  )
  return { ...result, queryClient }
}

// Navigation and route props for screens rendered outside a navigator.
export function screenProps<P extends object | undefined>(name: string, params?: P) {
  const navigation = {
    navigate: jest.fn(),
    goBack: jest.fn(),
    replace: jest.fn(),
    popToTop: jest.fn(),
    setParams: jest.fn(),
    reset: jest.fn(),
  }

  return { navigation: navigation as any, route: { key: name, name, params } as any, mocks: navigation }
}
