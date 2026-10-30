# gRPC contracts

The services talk gRPC over HTTP/2 (h2c inside the network). The contracts are the `.proto` files in each service's
`Protos` folder; a client service compiles the producer's file directly, so there is a single source for each
contract.

Authorization column:

- **JWT** — the call is made on behalf of a user; the gateway forwards the user's bearer token and the service
  checks the role and ownership itself.
- **Admin** — JWT with the `Admin` role.
- **Internal** — service-to-service only, with the internal API key in `x-api-key`; user tokens are refused.
- **Anonymous** — no credentials.

## Auth.Service — [`auth.proto`](../Services/Auth.Service/Protos/auth.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| AuthService | `Register`, `Login` | Anonymous | Gateway |
| AuthService | `ValidateToken` | Anonymous (the token is the input) | Gateway, to refuse deactivated accounts |
| UserAccountsService | `GetUser` | JWT (own account, or admin) | Gateway |
| UserAccountsService | `CreateUser`, `ListUsers`, `UpdateUser`, `SetUserActive` | Admin | Gateway |
| UserDirectoryService | `GetUserContact` | Internal | Notifications (email address) |

## Users.Service — [`users.proto`](../Services/Users.Service/Protos/users.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| UsersService | `GetUserProfile`, `CreateUserProfile`, `UpdateUserProfile` | JWT (own profile, or admin) | Gateway |

## Farms.Service — [`farms.proto`](../Services/Farms.Service/Protos/farms.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| FarmsService | `GetFarm`, `ListFarms` | Anonymous for active farms; JWT to see inactive ones | Gateway |
| FarmsService | `CreateFarm`, `ListMyFarms`, `UpdateFarm`, `DeleteFarm` | JWT (manager, own farm) | Gateway |
| FarmsService | `SetFarmActive` | Admin | Gateway |
| PickupSchedulesService | `GetPickupSchedule`, `ListPickupSchedules` | Anonymous | Gateway |
| PickupSchedulesService | `CreatePickupSchedule`, `UpdatePickupSchedule`, `DeletePickupSchedule` | JWT (manager, own farm) | Gateway |
| ProductsService | `GetProduct`, `ListProducts` | Anonymous for the catalog; JWT for hidden products and surprise contents | Gateway |
| ProductsService | `CreateProduct`, `UpdateProduct`, `AdjustStock`, `DeleteProduct` | JWT (manager, own farm) | Gateway |
| StockService | `ReserveStock`, `ConfirmStock`, `ReleaseStock` | Internal | Orders |
| FarmDirectoryService | `GetFarmOwnership` | Internal | Sensors, Notifications |

`ReserveStock` is idempotent per order: a retry with the same lines returns the existing reservation, different
lines are a conflict. It returns each line's unit price, farm and farm owner, so Orders never reads the Farms
database (section 2.6).

## Orders.Service — [`orders.proto`](../Services/Orders.Service/Protos/orders.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| OrdersService | `CreateOrder`, `ListMyOrders` | JWT (client) | Gateway |
| OrdersService | `ListOrders`, `UpdateOrderStatus` | JWT (manager of the farm, or admin) | Gateway |
| OrdersService | `GetOrder`, `CancelOrder` | JWT (customer, farm owner or admin) | Gateway |
| ReviewsService | `CreateReview` | JWT (the customer, after pickup) | Gateway |
| ReviewsService | `GetReviewByOrder` | JWT (customer, farm owner or admin) | Gateway |
| ReviewsService | `ListFarmReviews` | Anonymous | Gateway |

## Sensors.Service — [`sensors.proto`](../Services/Sensors.Service/Protos/sensors.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| ThresholdsService | `ListThresholds` | JWT (admin or manager) | Gateway |
| ThresholdsService | `UpdateThreshold` | Admin | Gateway |
| SensorReadingsService | `ListReadings`, `GetLatestReadings` | JWT (manager of the farm only, RF18) | Gateway |
| SensorReadingsService | `RecordReading` | Internal | Tests and tools; sensors normally publish to RabbitMQ |
| AlertsService | `ListAlerts`, `AcknowledgeAlert` | JWT (manager of the farm only) | Gateway |

## Notifications.Service — [`notifications.proto`](../Services/Notifications.Service/Protos/notifications.proto)

| Service | RPC | Authorization | Called by |
|---|---|---|---|
| NotificationsService | `ListMyNotifications`, `MarkNotificationRead`, `MarkAllNotificationsRead` | JWT (own notifications only) | Gateway |

## Errors

Every service maps its domain errors to gRPC status codes in one interceptor, and the gateway maps those to HTTP:

| Domain error | gRPC status | HTTP |
|---|---|---|
| Invalid input | `InvalidArgument` | 400 |
| Not authenticated | `Unauthenticated` | 401 |
| Not allowed | `PermissionDenied` | 403 |
| Not found | `NotFound` | 404 |
| Already exists | `AlreadyExists` | 409 |
| Stale version | `Aborted` | 409 |
| Business rule (e.g. insufficient stock) | `FailedPrecondition` | 422 |
| Service down or too slow | `Unavailable`, `DeadlineExceeded` | 503 |
