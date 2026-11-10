import { invitationPassword, personName } from "../queries"

jest.mock("expo-crypto", () => ({
  getRandomBytes: (count: number) => Uint8Array.from({ length: count }, (_, index) => (index * 37 + 11) % 256),
}))

describe("admin helpers", () => {
  it("makes an invitation password the API accepts (8+ characters with a number)", () => {
    const password = invitationPassword()
    expect(password.length).toBeGreaterThanOrEqual(8)
    expect(password).toMatch(/\d/)
  })

  it("names a person by the profile, else by the username", () => {
    const account = { username: "albano@cropconnect.test", email: "albano@cropconnect.test" }
    expect(personName(account, { id: "1", firstName: "Albano", lastName: "Moreira", phoneNumber: "" })).toBe(
      "Albano Moreira",
    )
    expect(personName(account, null)).toBe("albano@cropconnect.test")
  })
})
