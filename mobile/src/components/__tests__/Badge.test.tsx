import { render, screen } from "@testing-library/react-native"
import { StatusBadge } from "../Badge"

describe("StatusBadge", () => {
  it.each([
    ["Pending", "Pendente"],
    ["Confirmed", "Confirmada"],
    ["ReadyForPickup", "Pronta a levantar"],
    ["PickedUp", "Levantada"],
    ["Cancelled", "Cancelada"],
  ] as const)("labels %s in Portuguese, with text and not only colour", async (status, label) => {
    await render(<StatusBadge status={status} />)
    expect(screen.getByText(label)).toBeTruthy()
  })
})
