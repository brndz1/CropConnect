// Fills a local CropConnect (docker compose up -d) with the farms, products and people of the UI/UX prototype, so the
// mobile app can be tried with realistic data. It only uses the public REST API and can run more than once: what
// already exists is kept.
//
//   DEMO_PASSWORD=<password for the demo accounts> npm run seed:demo
//
// GATEWAY_URL (default http://localhost:5000), ADMIN_EMAIL and ADMIN_PASSWORD (the defaults of compose.yaml) can be
// overridden. Without DEMO_PASSWORD a random one is generated and printed at the end.
import { Buffer } from "node:buffer"
import { randomBytes } from "node:crypto"

const gateway = (process.env.GATEWAY_URL ?? "http://localhost:5000").replace(/\/+$/, "")
const adminEmail = process.env.ADMIN_EMAIL ?? "admin@cropconnect.test"
const adminPassword = process.env.ADMIN_PASSWORD ?? "DevAdmin-ChangeMe-123"
const demoPassword = process.env.DEMO_PASSWORD ?? `Demo-${randomBytes(6).toString("hex")}1`

const inDays = (days, hour = 23) => {
  const date = new Date()
  date.setDate(date.getDate() + days)
  date.setHours(hour, 0, 0, 0)
  return date.toISOString()
}

const people = [
  {
    email: "marta.ferreira@cropconnect.test",
    role: "Client",
    firstName: "Marta",
    lastName: "Ferreira",
    phone: "912 345 678",
  },
  { email: "joao.pires@cropconnect.test", role: "Client", firstName: "João", lastName: "Pires", phone: "913 222 450" },
  { email: "albano@cropconnect.test", role: "Manager", firstName: "Albano", lastName: "Moreira", phone: "934 111 222" },
  { email: "rosa@cropconnect.test", role: "Manager", firstName: "Rosa", lastName: "Martins", phone: "936 333 444" },
  { email: "carlos@cropconnect.test", role: "Manager", firstName: "Carlos", lastName: "Lima", phone: "938 555 666" },
]

const farms = [
  {
    manager: "albano@cropconnect.test",
    name: "Quinta do Vale",
    description: "Hortícolas e uma pequena estufa, em modo de produção biológico.",
    location: "Rua da Igreja 120, Barcelos",
    latitude: 41.5388,
    longitude: -8.6151,
    schedules: [
      { dayOfWeek: "Saturday", openTime: "09:00", closeTime: "13:00" },
      { dayOfWeek: "Sunday", openTime: "09:00", closeTime: "13:00" },
    ],
    products: [
      {
        name: "Tomate-cherry",
        unit: "kg",
        price: 2.4,
        stockQuantity: 2,
        type: "Product",
        description: "Colhido na véspera.",
      },
      { name: "Cenoura", unit: "kg", price: 1.2, stockQuantity: 20, type: "Product", description: "" },
      { name: "Abóbora-menina", unit: "kg", price: 1.5, stockQuantity: 8, type: "Product", description: "" },
      { name: "Alface roxa", unit: "unidade", price: 0.9, stockQuantity: 12, type: "Product", description: "" },
      {
        name: "Cabaz de sopa",
        unit: "cabaz",
        price: 7.5,
        stockQuantity: 6,
        type: "PreDefinedBasket",
        description: "5 legumes da época para uma panela de sopa.",
        contents: "Abóbora, cenoura, batata, alho-francês, couve",
        availableUntil: inDays(4),
      },
    ],
  },
  {
    manager: "rosa@cropconnect.test",
    name: "Horta da Serra",
    description: "Pequena horta familiar na encosta da serra.",
    location: "Lugar da Serra, Braga",
    latitude: 41.5454,
    longitude: -8.4265,
    schedules: [
      { dayOfWeek: "Friday", openTime: "17:00", closeTime: "19:30" },
      { dayOfWeek: "Saturday", openTime: "09:00", closeTime: "13:00" },
    ],
    products: [
      {
        name: "Cabaz surpresa da semana",
        unit: "cabaz",
        price: 6,
        stockQuantity: 4,
        type: "SurpriseBasket",
        description: "Legumes da época perto do fim do prazo.",
        contents: "Couve, batata, cebola e o que a horta der",
        availableUntil: inDays(5),
      },
      { name: "Coração-de-boi", unit: "kg", price: 2.9, stockQuantity: 2, type: "Product", description: "" },
      {
        name: "Morangos",
        unit: "caixa",
        price: 3.5,
        stockQuantity: 6,
        type: "Product",
        description: "Caixa de 500 g.",
      },
      { name: "Ovos do campo", unit: "dúzia", price: 2.2, stockQuantity: 10, type: "Product", description: "" },
    ],
  },
  {
    manager: "carlos@cropconnect.test",
    name: "Quinta da Ribeira",
    description: "Pomar e colmeias junto ao rio Cávado.",
    location: "Rua da Ribeira 8, Esposende",
    latitude: 41.5326,
    longitude: -8.781,
    schedules: [{ dayOfWeek: "Saturday", openTime: "10:00", closeTime: "13:00" }],
    products: [
      { name: "Maçã Esmolfe", unit: "kg", price: 1.8, stockQuantity: 30, type: "Product", description: "" },
      { name: "Tomate de rama", unit: "kg", price: 2.2, stockQuantity: 0, type: "Product", description: "" },
      {
        name: "Mel de rosmaninho",
        unit: "frasco",
        price: 6.5,
        stockQuantity: 5,
        type: "Product",
        description: "500 g.",
      },
    ],
  },
]

async function api(method, path, { token, body, allow = [] } = {}) {
  const response = await fetch(`${gateway}${path}`, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  const data = text ? JSON.parse(text) : undefined
  if (!response.ok && !allow.includes(response.status)) {
    throw new Error(`${method} ${path} → ${response.status} ${data?.detail ?? data?.title ?? text}`)
  }
  return { status: response.status, data }
}

const login = async (email, password) =>
  (await api("POST", "/api/auth/login", { body: { email, password } })).data.token

const userId = (token) => JSON.parse(Buffer.from(token.split(".")[1], "base64url").toString()).sub

async function ensureUser(adminToken, person) {
  const found = await api("GET", `/api/users?search=${encodeURIComponent(person.email)}&pageSize=5`, {
    token: adminToken,
  })
  const existing = found.data.items.find((user) => user.email === person.email)
  if (existing) return false
  await api("POST", "/api/users", {
    token: adminToken,
    body: { username: person.email, email: person.email, password: demoPassword, role: person.role },
  })
  return true
}

async function ensureProfile(token, person) {
  const id = userId(token)
  const body = { firstName: person.firstName, lastName: person.lastName, phoneNumber: person.phone }
  const existing = await api("GET", `/api/users/${id}/profile`, { token, allow: [404] })
  if (existing.status === 404) await api("POST", `/api/users/${id}/profile`, { token, body })
}

async function ensureFarm(token, farm) {
  const mine = await api("GET", "/api/farms/mine?pageSize=100", { token })
  let current = mine.data.items.find((item) => item.name === farm.name)
  if (!current) {
    const { name, description, location, latitude, longitude } = farm
    current = (await api("POST", "/api/farms", { token, body: { name, description, location, latitude, longitude } }))
      .data
  }
  for (const schedule of farm.schedules) {
    const exists = current.pickupSchedules?.some((item) => item.dayOfWeek === schedule.dayOfWeek)
    if (!exists) await api("POST", `/api/farms/${current.id}/pickup-schedules`, { token, body: schedule })
  }
  const products = await api("GET", `/api/products?farmId=${current.id}&includeUnavailable=true&pageSize=100`, {
    token,
  })
  for (const product of farm.products) {
    if (products.data.items.some((item) => item.name === product.name)) continue
    await api("POST", "/api/products", {
      token,
      body: { availableUntil: null, contents: null, ...product, farmId: current.id },
    })
  }
  return current
}

const adminToken = await login(adminEmail, adminPassword)
const created = []
for (const person of people) {
  if (await ensureUser(adminToken, person)) created.push(person.email)
}

for (const person of people) {
  let token
  try {
    token = await login(person.email, demoPassword)
  } catch {
    console.warn(`Skipped ${person.email}: it already existed with another password.`)
    continue
  }
  await ensureProfile(token, person)
  const farm = farms.find((item) => item.manager === person.email)
  if (farm) {
    const saved = await ensureFarm(token, farm)
    console.warn(`${farm.name}: ${saved.id}`)
  }
}

console.warn(created.length ? `Created ${created.join(", ")}` : "The demo accounts already existed.")
if (!process.env.DEMO_PASSWORD) console.warn(`Password of the new demo accounts: ${demoPassword}`)
