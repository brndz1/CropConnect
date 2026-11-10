// Learn more https://docs.expo.dev/guides/customizing-metro
const http = require("node:http")
const { getDefaultConfig } = require("expo/metro-config")

const config = getDefaultConfig(__dirname)

// On the web the app calls /api on its own origin, like the web app behind nginx, so the browser never needs CORS.
// The dev server forwards those calls to the API gateway (docker compose publishes it on port 5000).
const gateway = new URL(process.env.CROPCONNECT_GATEWAY_URL ?? "http://localhost:5000")

function forwardApi(req, res, next) {
  if (!req.url || !req.url.startsWith("/api/")) return next()
  const upstream = http.request(
    {
      hostname: gateway.hostname,
      port: gateway.port,
      path: req.url,
      method: req.method,
      headers: { ...req.headers, host: gateway.host },
    },
    (response) => {
      res.writeHead(response.statusCode ?? 502, response.headers)
      response.pipe(res)
    },
  )
  upstream.on("error", () => {
    res.writeHead(502, { "Content-Type": "application/problem+json" })
    res.end(JSON.stringify({ status: 502, title: "The API gateway is not reachable", detail: gateway.href }))
  })
  req.pipe(upstream)
}

config.server = {
  ...config.server,
  enhanceMiddleware: (middleware) => (req, res, next) => forwardApi(req, res, () => middleware(req, res, next)),
}

module.exports = config
