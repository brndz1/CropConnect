import { fireEvent, render, screen } from "@testing-library/react-native"
import { Button } from "../Button"

describe("Button", () => {
  it("calls onPress", async () => {
    const onPress = jest.fn()
    await render(<Button label="Entrar" onPress={onPress} />)
    await fireEvent.press(screen.getByRole("button", { name: "Entrar" }))
    expect(onPress).toHaveBeenCalledTimes(1)
  })

  it("shows the loading label and ignores presses while busy", async () => {
    const onPress = jest.fn()
    await render(<Button label="Entrar" loadingLabel="A entrar..." loading onPress={onPress} />)
    expect(screen.getByText("A entrar...")).toBeTruthy()
    await fireEvent.press(screen.getByRole("button"))
    expect(onPress).not.toHaveBeenCalled()
  })
})
