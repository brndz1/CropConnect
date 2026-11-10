import { splitName, usernameFromEmail } from "../AuthProvider"

describe("registration names", () => {
  it("splits the name into first and last names", () => {
    expect(splitName(" Marta  Ferreira ")).toEqual({ firstName: "Marta", lastName: "Ferreira" })
    expect(splitName("Ana Maria Costa")).toEqual({ firstName: "Ana", lastName: "Maria Costa" })
    expect(splitName("Albano")).toEqual({ firstName: "Albano", lastName: "Albano" })
  })

  it("derives a unique username from the email", () => {
    expect(usernameFromEmail("Marta@Mail.pt")).toBe("marta@mail.pt")
    expect(usernameFromEmail(`${"a".repeat(60)}@mail.pt`).length).toBeLessThanOrEqual(50)
  })
})
