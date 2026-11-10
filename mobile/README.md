# CropConnect mobile app

The mobile client of CropConnect, built from the high-fidelity prototype of the UI/UX course (192 screens, iOS and
Android): Expo SDK 58, React Native 0.88 and TypeScript in strict mode, with React Navigation, TanStack Query and a
client typed from the gateway's OpenAPI document. One app serves the three profiles; the role in the JWT picks the
navigation (RF03).

| Profile       | Bottom navigation                      | Screens of the prototype                                                                                                                                                                                                                                         |
| ------------- | -------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Client        | Início, Explorar, Encomendas, Perfil   | C00-C16: splash, sign in, register, recover access, home, catalogue with search and filters, product, basket and farm, cart by farm, reservation review with the stock error, confirmation, orders, order detail, notifications, profile, personal data, privacy |
| Manager       | Painel, Encomendas, Produtos, Sensores | G01-G22: dashboard with the open alert, orders by state with the status dialogs, products with stock adjustment, product and basket forms, sensors, alert with the forecast, 24 h / 7 d / 30 d history, A minha quinta, farm details, pickup hours               |
| Administrator | Utilizadores, Quintas, Limites, Perfil | A01-A30: users with search and filter, user record, edit, change profile, deactivate and reactivate, invitation, farms, farm record, sensor thresholds                                                                                                           |

## Running it

Start the platform from the repository root, with the sensor simulator so the sensors have readings:

```bash
docker compose --profile simulation up -d --build
```

Optionally fill it with the farms, products and people of the prototype (Quinta do Vale, Horta da Serra, Quinta da
Ribeira, Marta Ferreira, Albano Moreira...). The accounts get the password in `DEMO_PASSWORD`, or a generated one that
the script prints:

```bash
cd mobile
npm install
DEMO_PASSWORD=<choose one> npm run seed:demo
```

Then start the app:

```bash
npm run web
```

- **Web preview** (http://localhost:8081): the Expo dev server forwards `/api` to the gateway on
  `http://localhost:5000` (`CROPCONNECT_GATEWAY_URL` changes it), like nginx does for the web app.
- **Phone with Expo Go**: `npm start` and scan the QR code. The app calls port 5000 on the computer that runs Metro;
  the gateway must be reachable on the local network. Set `EXPO_PUBLIC_API_URL` (see `.env.example`) to use another
  gateway.
- **Android emulator / iOS simulator**: `npm run android` or `npm run ios` (the emulator reaches the host as
  `10.0.2.2`).

The development administrator is `admin@cropconnect.test`, with the password of `ADMIN_PASSWORD` in `.env`.

## Scripts

| Script                                                  | What it does                                                                               |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| `npm run check`                                         | Formatting, lint, type-check and unit tests, as CI runs them                               |
| `npm test`                                              | Unit tests (Jest with jest-expo and React Native Testing Library)                          |
| `npm run lint` / `npm run typecheck` / `npm run format` | ESLint (Expo config), `tsc --noEmit`, Prettier                                             |
| `npm run generate:api`                                  | Regenerates `src/api/schema.d.ts` from `docs/openapi.json` (CI fails if it is out of date) |
| `npm run generate:illustrations`                        | Turns `assets/illustrations/*.svg` into `src/components/illustrations.ts`                  |
| `npm run export:web`                                    | Production web bundle in `dist/`                                                           |
| `npm run seed:demo`                                     | Demo data through the REST API (keeps what exists)                                         |

## How it is built

```
src/
  api/          typed openapi-fetch client, problem details → ApiError, query keys
  auth/         SecureStore tokens, JWT session, refresh on 401 (one refresh at a time), AuthProvider
  cart/         cart grouped by farm, stored per user (AsyncStorage)
  components/   design system: buttons, fields, chips, badges, cards, banners, stepper, dialog, snackbar, states
  features/     screens and hooks by area: auth, catalog, cart, orders, manager, sensors, notifications, profile, admin
  lib/          pure rules (formats, catalogue, schedules, orders, sensors, notifications, validation), unit tested
  navigation/   one stack and bottom tab bar per profile
  theme/        design tokens of section 7 of the report
```

- **Design tokens.** The palette (Verde Horta `#16722F`, Terracota, Olive, the neutrals and the semantic colours), the
  Poppins and Lato type scale in sp, the 8-point spacing, the radii and the 48 dp touch targets come from section 7
  of the report. States always pair an icon and a word; the charts dash the limits and draw out-of-range points as
  triangles (section 8).
- **Server state.** TanStack Query, with no retries on 4xx; orders, notifications and readings refresh on an interval
  so changes made by the other profile show up.
- **Accessibility.** Labels and roles on every control, live regions for errors and snackbars, text that follows the
  system font size up to 150 % (RNF03).
- **Session.** The JWT and the refresh token live in the Keychain or Keystore (localStorage only in the web preview);
  the app renews the JWT a minute before it expires and shows the "sessão expirou" dialog when the session ends.

## Where it differs from the prototype

The app talks to the real backend, so where the prototype shows something the API does not offer, it does the
closest thing:

| Prototype                                          | App                                                                  | Why                                                                 |
| -------------------------------------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Product photos, categories (Legumes, Frutas, Ovos) | Illustrations and categories chosen from the product name            | The API has no image or category field                              |
| Pickup slot chosen at checkout                     | The slot and the client's name go in the order notes                 | Orders have no slot field, and managers cannot read client profiles |
| Manager registers the pickup (G06, DG3)            | The client confirms the pickup in the app; the farm sees it waiting  | `POST /orders/{id}/pickup` is for the customer only                 |
| Order numbers such as #2042                        | The first 8 characters of the order id (#70E88F97)                   | Orders are identified by GUIDs, as in the web app and the emails    |
| Distance to farms, "perto de si"                   | Not shown                                                            | No user location                                                    |
| Product ratings                                    | The farm's rating                                                    | The API rates farms (reviews per order)                             |
| 3-day weather forecast with temperatures           | Rain forecast of the next 24 h, for low-humidity alerts              | That is what the Sensors service gets from Open-Meteo               |
| Humidade do solo / do ar                           | Humidade and Temperatura                                             | The sensors have two types                                          |
| Strikethrough "valor 10,00 €" on baskets           | Only the price                                                       | No original value in the API                                        |
| Consents, data copy and self-deactivation (C16)    | What the platform keeps, and deactivation asked to the administrator | No such endpoints                                                   |
| Associar gestor (A12)                              | Opens the manager's account                                          | Farms cannot change owner through the API                           |
| Notification failure with Tentar de novo (G04F)    | Tentar de novo when the status change itself fails                   | Notifications are sent asynchronously                               |
| Review after pickup (RF11, not designed)           | Stars and comment on the picked-up order                             | The API supports it                                                 |

The notifications, alerts and forecast arrive from the backend in English; the app recognises each template and
shows it in European Portuguese.
