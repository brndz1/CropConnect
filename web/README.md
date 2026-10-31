# CropConnect web app

The front end of CropConnect: React 19, TypeScript, Vite, Tailwind CSS 4 and shadcn/ui (Radix), with TanStack Query
for the API calls, React Router and Recharts. It follows the visual system of the design report (section 6): Verde
Horta `#16722F` as the primary colour, Poppins for titles, buttons and numbers, Lato for body text, 48 px touch
targets, and status badges that always pair an icon with a word.

One app serves the three profiles; the JWT's role picks the navigation and the home screen (RF03):

| Profile | Screens |
|---|---|
| Visitor | Home, catalog with search and filters, product and farm details, cart |
| Client | The above, plus checkout (one order per farm), orders with their progress, cancelling, reviews, notifications, profile |
| Manager | Dashboard (new orders, open alerts, low stock), orders by state with the status changes, products and baskets with stock adjustment, farms and pickup schedules, sensors (current readings, history chart with the limits, alerts) |
| Administrator | Users (search, role, deactivate), farms (activate and deactivate), sensor thresholds |

The manager's and the administrator's screens and the charts are loaded only when needed (code splitting).

## Development

The dev server proxies `/api` to the gateway of Docker Compose (`http://localhost:5000`); set `GATEWAY_URL` to use
another one.

```bash
npm install
```

```bash
npm run dev
```

The app opens on http://localhost:5173. Other scripts: `npm run build` (type-check and production build),
`npm run lint` (oxlint), `npm run format` (Prettier, with Tailwind class sorting).

## Container

The [Dockerfile](Dockerfile) builds the app with `node:24-alpine` and serves it with `nginx:1.29-alpine`, which also
forwards `/api`, `/health`, `/openapi` and `/scalar` to the gateway (`GATEWAY_URL`), so the browser uses a single
origin. Docker Compose publishes it on http://localhost:3000; on Kubernetes it is the only public entry point.

## Not covered yet

What the design report lists but the API does not offer: password recovery, product photos and categories, favourites,
the farm map, and sales reports. Notification and alert texts come from the backend in English.
