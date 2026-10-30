# REST API

The API gateway is the only entry point for clients (section 5.1). It translates each REST call to gRPC on the
owning service. The full contract, with every request and response schema, is the [OpenAPI document](openapi.json);
the running gateway serves it at `/openapi/v1.json` and an interactive reference at `/scalar`.

## Authentication

1. `POST /api/auth/register` (role `Client` or `Manager`) and `POST /api/auth/login` return a JWT valid for 60 minutes.
2. Send it as `Authorization: Bearer <token>`. The gateway rejects a missing, invalid or expired token, and the token
   of a deactivated account within 15 seconds; the owning service then checks the role and ownership.

Roles: **Client** buys, **Manager** runs farms, **Admin** manages users, farms and thresholds. The development
administrator is `admin@cropconnect.test`.

## Conventions

- JSON in camelCase; enums as strings (`"ReadyForPickup"`); times in UTC (ISO 8601); money as decimals.
- Lists take `page` (from 1) and `pageSize` (default 20, at most 100) and return
  `{ "items": [...], "totalCount", "page", "pageSize" }`.
- Updates of farms, schedules, products, orders and thresholds carry an `expectedVersion` (it may be `null` to skip
  the check); a stale one is a 409.
- Errors are RFC 9457 problem details (`application/problem+json`):

  ```json
  { "status": 422, "title": "Business rule violated", "detail": "Insufficient stock for product 'Batata'." }
  ```

  | Status | Meaning |
  |---|---|
  | 400 | Invalid input |
  | 401 | Missing, invalid or expired token, or deactivated account |
  | 403 | The role or ownership does not allow it |
  | 404 | Not found (also used for someone else's notification, so it is not revealed) |
  | 409 | Already exists, or modified by someone else (stale version) |
  | 422 | Business rule (insufficient stock, order not in the right status, expired basket...) |
  | 503 | A downstream service is unavailable or too slow; the detail is not exposed |

## Endpoints

The access rules each service enforces are in the [gRPC contracts](grpc.md). Token: **Public** needs none,
**Optional** works without one but shows more to the owner or an administrator (inactive farms, hidden products,
surprise basket contents), **Required** needs one.


### Auth

| Method | Path | Description | Token |
|---|---|---|---|
| POST | `/api/auth/register` | Register as a client or farm manager (RF01, RF02) | Public |
| POST | `/api/auth/login` | Log in and get a JWT for the other endpoints (RF01) | Public |

### Users

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/users` | List user accounts (administrator, RF03) | Required |
| POST | `/api/users` | Create a user account (administrator, RF03) | Required |
| GET | `/api/users/{id}` | Get a user account (own account, or any as administrator) | Required |
| PATCH | `/api/users/{id}` | Edit a user account and its role (administrator, RF03) | Required |
| PUT | `/api/users/{id}/active` | Activate or deactivate a user account (administrator, RF03) | Required |

### Profiles

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/users/{id}/profile` | Get a user's profile | Required |
| POST | `/api/users/{id}/profile` | Create the profile of your own user | Required |
| PUT | `/api/users/{id}/profile` | Replace the profile of your own user | Required |

### Farms

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/farms` | Browse the active farms (RF09); administrators can include inactive ones (RF04) | Optional |
| POST | `/api/farms` | Register a farm (manager, RF05) | Required |
| GET | `/api/farms/mine` | List the farms you manage (RF05) | Required |
| GET | `/api/farms/{id}` | Get a farm with its pickup schedules (RF09) | Optional |
| PATCH | `/api/farms/{id}` | Edit your farm (manager, RF05) | Required |
| DELETE | `/api/farms/{id}` | Delete your farm (manager, RF05) | Required |
| PUT | `/api/farms/{id}/active` | Activate or deactivate a farm (administrator, RF04) | Required |

### Pickup schedules

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/farms/{farmId}/pickup-schedules` | List a farm's pickup days and hours (RF09) | Public |
| POST | `/api/farms/{farmId}/pickup-schedules` | Add a pickup day and hours to your farm (manager, RF07) | Required |
| GET | `/api/pickup-schedules/{id}` | Get a pickup schedule | Public |
| PATCH | `/api/pickup-schedules/{id}` | Edit a pickup schedule (manager, RF07) | Required |
| DELETE | `/api/pickup-schedules/{id}` | Remove a pickup schedule (manager, RF07) | Required |

### Products

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/products` | Browse the catalog of products and baskets of every farm (RF09) | Optional |
| POST | `/api/products` | Add a product or seasonal basket to your farm (manager, RF06, RF08) | Required |
| GET | `/api/products/{id}` | Get a product or basket (RF09) | Optional |
| PATCH | `/api/products/{id}` | Edit a product or basket (manager, RF06, RF08) | Required |
| DELETE | `/api/products/{id}` | Remove a product or basket (manager) | Required |
| POST | `/api/products/{id}/stock-adjustments` | Add or remove stock (manager, RF06) | Required |

### Orders

| Method | Path | Description | Token |
|---|---|---|---|
| POST | `/api/orders` | Place an order, reserving its stock (client, RF10, RF11) | Required |
| GET | `/api/orders` | Orders received by your farms (manager, RF12) or by any farm (administrator) | Required |
| GET | `/api/orders/mine` | Your order history and status (client, RF13) | Required |
| GET | `/api/orders/{id}` | Get an order you placed or received | Required |
| PUT | `/api/orders/{id}/status` | Move an order to Confirmed, ReadyForPickup or PickedUp (manager, RF12) | Required |
| POST | `/api/orders/{id}/cancel` | Cancel an order and release its stock (client before ReadyForPickup, or manager, RF14) | Required |

### Reviews

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/farms/{farmId}/reviews` | A farm's reviews (RF15) | Public |
| POST | `/api/orders/{orderId}/review` | Review a picked-up order (client, RF15) | Required |
| GET | `/api/orders/{orderId}/review` | Get the review of an order | Required |

### Sensor thresholds

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/sensor-thresholds` | The global alert thresholds (administrator or manager) | Required |
| PUT | `/api/sensor-thresholds/{type}` | Change a global alert threshold (administrator, RF04) | Required |

### Sensors

| Method | Path | Description | Token |
|---|---|---|---|
| POST | `/api/alerts/{id}/acknowledge` | Mark an alert as handled (manager) | Required |
| GET | `/api/farms/{farmId}/readings` | Your farm's reading history (manager, RF18) | Required |
| GET | `/api/farms/{farmId}/readings/latest` | The latest reading of each sensor type on your farm (manager, RF18) | Required |
| GET | `/api/farms/{farmId}/alerts` | Your farm's sensor alerts (manager, RF17, RF18) | Required |

### Notifications

| Method | Path | Description | Token |
|---|---|---|---|
| GET | `/api/notifications` | Your notifications, newest first, with the unread count | Required |
| POST | `/api/notifications/{id}/read` | Mark one of your notifications as read | Required |
| POST | `/api/notifications/read-all` | Mark all your notifications as read | Required |
