import { emailError, newPasswordError, parseDate, parseDecimal, parseInteger } from "../validation"

describe("validation", () => {
  it("checks the email format", () => {
    expect(emailError("")).toBe("Introduza o email.")
    expect(emailError("marta.ferreira@")).toBe("Introduza um email válido (ex.: nome@exemplo.pt).")
    expect(emailError(" marta@mail.pt ")).toBeNull()
  })

  it("asks for 8 characters with a number", () => {
    expect(newPasswordError("curta1")).not.toBeNull()
    expect(newPasswordError("semnumeros")).not.toBeNull()
    expect(newPasswordError("tomate2026")).toBeNull()
  })

  it("parses Portuguese numbers and dates", () => {
    expect(parseDecimal("2,40")).toBe(2.4)
    expect(parseDecimal("5,00 €")).toBe(5)
    expect(parseDecimal("abc")).toBeNull()
    expect(parseInteger("12")).toBe(12)
    expect(parseInteger("1.5")).toBeNull()
    expect(parseDate("31/10/2026")?.getDate()).toBe(31)
    expect(parseDate("31/02/2026")).toBeNull()
  })
})
