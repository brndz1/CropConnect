import { fireEvent, screen } from "@testing-library/react-native"
import { ApiError } from "@/api/errors"
import { renderWithProviders, screenProps } from "@/test/render"
import { LoginScreen } from "../LoginScreen"

const mockLogin = jest.fn()
jest.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ login: mockLogin }) }))
jest.mock("@react-navigation/native", () => ({ useNavigation: () => ({ goBack: jest.fn() }) }))

async function renderLogin() {
  const props = screenProps("Login")
  await renderWithProviders(<LoginScreen navigation={props.navigation} route={props.route} />)
  return props.mocks
}

async function fill(email: string, password: string) {
  await fireEvent.changeText(screen.getByTestId("login-email"), email)
  await fireEvent.changeText(screen.getByTestId("login-password"), password)
  await fireEvent.press(screen.getByTestId("login-submit"))
}

describe("LoginScreen", () => {
  beforeEach(() => jest.clearAllMocks())

  it("shows the validation errors next to the fields (C01e)", async () => {
    await renderLogin()
    await fill("marta.ferreira@", "")

    expect(screen.getByText("Introduza um email válido (ex.: nome@exemplo.pt).")).toBeTruthy()
    expect(screen.getByText("Introduza a palavra-passe.")).toBeTruthy()
    expect(mockLogin).not.toHaveBeenCalled()
  })

  it("signs in with the email and the password", async () => {
    mockLogin.mockResolvedValue({})
    await renderLogin()
    await fill("marta@mail.pt", "tomate2026")
    expect(mockLogin).toHaveBeenCalledWith("marta@mail.pt", "tomate2026")
  })

  it("explains wrong credentials", async () => {
    mockLogin.mockRejectedValue(new ApiError(401, "Unauthorized"))
    await renderLogin()
    await fill("marta@mail.pt", "errada123")
    expect(await screen.findByText("Email ou palavra-passe incorretos.")).toBeTruthy()
  })

  it("opens the password recovery with the typed email", async () => {
    const navigation = await renderLogin()
    await fireEvent.changeText(screen.getByTestId("login-email"), "marta@mail.pt")
    await fireEvent.press(screen.getByText("Esqueci-me da palavra-passe"))
    expect(navigation.navigate).toHaveBeenCalledWith("ForgotPassword", { email: "marta@mail.pt" })
  })
})
