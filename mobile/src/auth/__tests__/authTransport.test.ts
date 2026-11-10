import { createAuthTransport } from "../authTransport"

const ok = () => new Response("{}", { status: 200 })
const unauthorized = () => new Response("", { status: 401 })

function setup(responses: (() => Response)[], refreshed: string | null = "new-token") {
  const sent: Request[] = []
  const send = jest.fn(async (request: Request) => {
    sent.push(request)
    return (responses.shift() ?? ok)()
  })
  const refresh = jest.fn(async () => refreshed)
  const onUnauthorized = jest.fn()
  const transport = createAuthTransport({ getToken: () => "old-token", refresh, onUnauthorized, send })
  return { transport, sent, refresh, onUnauthorized }
}

describe("auth transport", () => {
  it("sends the bearer token", async () => {
    const { transport, sent } = setup([ok])
    await transport(new Request("http://gateway/api/orders/mine"))
    expect(sent[0]?.headers.get("Authorization")).toBe("Bearer old-token")
  })

  it("refreshes once on a 401 and repeats the call with the new token", async () => {
    const { transport, sent, refresh, onUnauthorized } = setup([unauthorized, ok])
    const response = await transport(new Request("http://gateway/api/orders", { method: "POST", body: '{"a":1}' }))

    expect(response.status).toBe(200)
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(sent[1]?.headers.get("Authorization")).toBe("Bearer new-token")
    expect(await sent[1]?.text()).toBe('{"a":1}')
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it("reports when there is no new token", async () => {
    const { transport, onUnauthorized } = setup([unauthorized], null)
    const response = await transport(new Request("http://gateway/api/orders/mine"))
    expect(response.status).toBe(401)
    expect(onUnauthorized).toHaveBeenCalledWith("refresh-failed")
  })

  it("reports a deactivated account when even the new token is refused", async () => {
    const { transport, onUnauthorized } = setup([unauthorized, unauthorized])
    await transport(new Request("http://gateway/api/orders/mine"))
    expect(onUnauthorized).toHaveBeenCalledWith("rejected")
  })
})
