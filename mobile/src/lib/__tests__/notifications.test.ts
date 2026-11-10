import { groupByDay, localizeNotification } from "../notifications"

const notification = (title: string, message: string, type: "OrderCreated" | "SensorAlert" = "OrderCreated") => ({
  type,
  title,
  message,
})

describe("notification texts", () => {
  it("translates the order notifications", () => {
    expect(
      localizeNotification(
        notification(
          "Order #70E88F97 is ready for pickup",
          "Your order #70E88F97 is ready for pickup at Quinta do Vale. You pay when you pick it up, then confirm the pickup in the app.",
        ),
      ),
    ).toEqual({
      title: "Encomenda #70E88F97 pronta a levantar",
      message: "Pode levantá-la em Quinta do Vale e pagar em mão. Depois confirme o levantamento na app.",
      icon: "shopping-bag",
    })
    expect(
      localizeNotification(notification("New order #7A38FAE3 at Horta da Serra", "A customer placed…")).title,
    ).toBe("Nova encomenda #7A38FAE3")
    expect(
      localizeNotification(
        notification("Order #7A38FAE3 cancelled", "Horta da Serra cancelled your order #7A38FAE3. Reason: Sem stock."),
      ).message,
    ).toBe("Horta da Serra cancelou a sua encomenda. Motivo: Sem stock.")
  })

  it("translates the sensor alerts", () => {
    const localized = localizeNotification(
      notification(
        "Humidity alert at Quinta do Vale",
        "Humidity of 22.4% from sensor sim-d771a8db is below the minimum of 35%. Rain forecast in the next 24h (23.2 mm, up to 100% chance); irrigation may not be needed.",
        "SensorAlert",
      ),
    )
    expect(localized.title).toBe("Alerta de humidade na Quinta do Vale")
    expect(localized.message).toBe(
      "Humidade de 22,4 % no sensor sim-d771a8db, abaixo do mínimo de 35 %. Chuva prevista nas próximas 24 h.",
    )
  })

  it("keeps unknown texts as they came", () => {
    expect(localizeNotification(notification("Something new", "Body")).title).toBe("Something new")
  })

  it("groups today's notifications apart", () => {
    const now = new Date(2026, 10, 10, 12)
    const { today, earlier } = groupByDay(
      [{ createdAt: new Date(2026, 10, 10, 9).toISOString() }, { createdAt: new Date(2026, 10, 9, 9).toISOString() }],
      now,
    )
    expect(today).toHaveLength(1)
    expect(earlier).toHaveLength(1)
  })
})
