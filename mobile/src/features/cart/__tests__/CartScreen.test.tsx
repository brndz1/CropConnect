import AsyncStorage from "@react-native-async-storage/async-storage"
import { fireEvent, screen } from "@testing-library/react-native"
import { addLine } from "@/cart/cart"
import { CartProvider } from "@/cart/CartProvider"
import { product } from "@/test/fixtures"
import { renderWithProviders, screenProps } from "@/test/render"
import { CartScreen } from "../CartScreen"

jest.mock("@react-navigation/native", () => ({ useNavigation: () => ({ goBack: jest.fn() }) }))
jest.mock("@/features/catalog/queries", () => ({ useFarmsById: () => new Map() }))

const tomato = product({ id: "tomato", name: "Tomate-cherry", price: 2.4, stockQuantity: 2 })
const basket = product({
  id: "basket",
  farmId: "f2",
  farmName: "Horta da Serra",
  name: "Cabaz surpresa da semana",
  type: "SurpriseBasket",
  price: 6,
  stockQuantity: 3,
})

async function renderCart(lines = addLine(addLine([], basket), tomato, 2)) {
  await AsyncStorage.setItem("cropconnect.cart.u1", JSON.stringify(lines))
  const props = screenProps("Cart")
  await renderWithProviders(
    <CartProvider userId="u1">
      <CartScreen navigation={props.navigation} route={props.route} />
    </CartProvider>,
  )
  return props.mocks
}

describe("CartScreen", () => {
  beforeEach(() => AsyncStorage.clear())

  it("groups by farm, explains the limits and shows the total (C06)", async () => {
    await renderCart()
    expect(await screen.findByText("Horta da Serra")).toBeTruthy()
    expect(screen.getByText("Quinta do Vale")).toBeTruthy()
    expect(screen.getByText("Limite de 1 cabaz surpresa por cliente")).toBeTruthy()
    expect(screen.getByText("Stock disponível: 2 kg")).toBeTruthy()
    expect(screen.getByText("Total (2 encomendas)")).toBeTruthy()
  })

  it("removes after confirming and can undo it", async () => {
    await renderCart()
    await fireEvent.press(await screen.findByLabelText("Remover Tomate-cherry"))
    expect(screen.getByText("Remover Tomate-cherry?")).toBeTruthy()
    await fireEvent.press(screen.getByTestId("dialog-confirm"))

    expect(screen.queryByTestId("cart-line-tomato")).toBeNull()
    await fireEvent.press(screen.getByText("Anular"))
    expect(screen.getByTestId("cart-line-tomato")).toBeTruthy()
  })

  it("invites to explore when empty (C06V)", async () => {
    const navigation = await renderCart([])
    await fireEvent.press(await screen.findByText("Explorar produtos"))
    expect(navigation.navigate).toHaveBeenCalledWith("ClientTabs", { screen: "Explore" })
  })

  it("goes on to review the reservation", async () => {
    const navigation = await renderCart()
    await fireEvent.press(await screen.findByTestId("cart-review"))
    expect(navigation.navigate).toHaveBeenCalledWith("ReviewReservation")
  })
})
