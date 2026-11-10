import { fireEvent, render, screen } from "@testing-library/react-native"
import { QuantityStepper } from "../QuantityStepper"

describe("QuantityStepper", () => {
  it("shows the value with its unit and changes it", async () => {
    const onChange = jest.fn()
    await render(<QuantityStepper value={1} max={2} unit="kg" label="Tomate-cherry" onChange={onChange} />)

    expect(screen.getByTestId("stepper-value")).toHaveTextContent("1 kg")
    await fireEvent.press(screen.getByLabelText("Aumentar"))
    expect(onChange).toHaveBeenCalledWith(2)
  })

  it("disables − at the minimum and + at the maximum", async () => {
    const onChange = jest.fn()
    await render(<QuantityStepper value={2} min={2} max={2} label="Cabaz" onChange={onChange} />)

    expect(screen.getByLabelText("Diminuir")).toBeDisabled()
    expect(screen.getByLabelText("Aumentar")).toBeDisabled()
    await fireEvent.press(screen.getByLabelText("Aumentar"))
    expect(onChange).not.toHaveBeenCalled()
  })
})
