import { formatDate, formatPrice, formatRelative, formatTime, initials, plural, shortId } from "../format"

describe("format", () => {
  it("formats prices in euros, European Portuguese style", () => {
    expect(formatPrice(2.4).replace(/\s/g, " ")).toBe("2,40 €")
  })

  it("formats dates as dd/mm/aaaa", () => {
    expect(formatDate(new Date(2026, 9, 31, 9, 0))).toBe("31/10/2026")
  })

  it("drops the seconds of a time", () => {
    expect(formatTime("09:00:00")).toBe("09:00")
  })

  it("shows the first characters of an order id", () => {
    expect(shortId("3f2a9c1b-0000-4000-8000-000000000000")).toBe("#3F2A9C1B")
  })

  it("describes how long ago something happened", () => {
    const now = new Date(2026, 9, 31, 12, 0)
    expect(formatRelative(new Date(2026, 9, 31, 11, 59, 40), now)).toBe("agora")
    expect(formatRelative(new Date(2026, 9, 31, 11, 45), now)).toBe("há 15 min")
    expect(formatRelative(new Date(2026, 9, 31, 9, 0), now)).toBe("há 3 h")
    expect(formatRelative(new Date(2026, 9, 30, 18, 0), now)).toBe("ontem")
    expect(formatRelative(new Date(2026, 9, 20, 18, 0), now)).toBe("20/10/2026")
  })

  it("pluralises and builds initials", () => {
    expect(plural(1, "artigo", "artigos")).toBe("1 artigo")
    expect(plural(3, "artigo", "artigos")).toBe("3 artigos")
    expect(initials("Marta Ferreira")).toBe("MF")
    expect(initials("albano")).toBe("A")
    expect(initials("")).toBe("?")
  })
})
