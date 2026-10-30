# Data model

Each service owns one PostgreSQL database and no service reads another's. References across services (a farm's
`OwnerId`, an order's `ProductId`) are plain IDs, checked through gRPC rather than foreign keys. The schemas are
created by each service's EF Core migrations.

Common columns: entities that users edit carry a `Version` used for optimistic concurrency and soft-delete with
`IsDeleted`; times are UTC.

## Auth.Service (`auth_db`)

```mermaid
erDiagram
    Users {
        uuid Id PK
        varchar(50) Username UK
        varchar(254) Email UK "lower case"
        text PasswordHash "BCrypt"
        varchar(20) Role "Client | Manager | Admin"
        bool IsActive
    }
```

The administrator account is created on startup from the `BootstrapAdmin` settings; nobody can register as one.

## Users.Service (`users_db`)

```mermaid
erDiagram
    UserProfiles {
        uuid Id PK "same ID as the Auth user"
        varchar(100) FirstName
        varchar(100) LastName
        varchar(20) PhoneNumber
        timestamptz CreatedAt
        timestamptz UpdatedAt
    }
```

## Farms.Service (`farms_db`)

```mermaid
erDiagram
    Farms ||--o{ PickupSchedules : "opens on"
    Farms ||--o{ Products : sells
    Products ||--o{ StockReservations : "held by"

    Farms {
        uuid Id PK
        uuid OwnerId "Auth user (manager)"
        varchar Name
        varchar Description
        varchar Location
        float8 Latitude "optional, for the weather"
        float8 Longitude
        bool IsActive "admin can deactivate (RF04)"
        int Version
        bool IsDeleted
    }
    PickupSchedules {
        uuid Id PK
        uuid FarmId FK
        varchar(20) DayOfWeek
        time OpenTime
        time CloseTime "check: after OpenTime"
        int Version
    }
    Products {
        uuid Id PK
        uuid FarmId FK
        varchar Name
        varchar Description
        varchar Unit "default kg"
        numeric(18_2) Price "check: >= 0"
        int StockQuantity "check: >= 0"
        bool IsAvailable "manager can hide it"
        varchar(30) Type "Product | PreDefinedBasket | SurpriseBasket"
        timestamptz AvailableUntil "baskets only"
        varchar Contents "baskets only; hidden for surprise ones"
        int Version
        bool IsDeleted
    }
    StockReservations {
        uuid Id PK
        uuid OrderId "Orders.Service order; unique with ProductId"
        uuid ProductId FK
        int Quantity "check: > 0"
        numeric(18_2) UnitPrice "price when reserved"
        varchar Status "Reserved | Confirmed | Released | Expired"
        timestamptz ExpiresAt "15 min after creation"
        timestamptz ConfirmedAt
        timestamptz ReleasedAt
    }
```

Seasonal baskets are products with a basket `Type` (section 2.6). A product is in the catalog while it is available,
its farm is active and, for a basket, `AvailableUntil` has not passed; an expired basket is final.

## Orders.Service (`orders_db`)

```mermaid
erDiagram
    Orders ||--|{ OrderItems : contains
    Orders ||--o| Reviews : "reviewed in"

    Orders {
        uuid Id PK
        uuid CustomerId "Auth user (client)"
        uuid FarmId "one farm per order"
        uuid FarmOwnerId "copied from the reservation"
        varchar Status "Pending | Confirmed | ReadyForPickup | PickedUp | Cancelled"
        numeric(18_2) TotalAmount
        varchar(1000) Notes
        varchar(500) CancellationReason
        timestamptz StockReleasedAt "set once Farms released the stock"
        timestamptz CreatedAt
        int Version
    }
    OrderItems {
        uuid Id PK
        uuid OrderId FK
        uuid ProductId "Farms product"
        varchar(120) ProductName
        int Quantity
        numeric(18_2) UnitPrice "returned by Farms"
    }
    Reviews {
        uuid Id PK
        uuid OrderId FK "unique: one review per order"
        uuid CustomerId
        uuid FarmId
        int Rating "1-5"
        varchar(1000) Comment
        timestamptz CreatedAt
    }
    OutboxMessages {
        uuid Id PK "becomes the AMQP MessageId"
        varchar(100) EventType
        varchar(255) RoutingKey
        jsonb Payload
        timestamptz OccurredAt
        varchar(55) TraceParent
        timestamptz PublishedAt "null until published"
        int Attempts
        varchar(1000) LastError
    }
```

## Sensors.Service (`sensors_db`)

```mermaid
erDiagram
    SensorReadings ||--o| Alerts : raises

    SensorReadings {
        uuid Id PK
        uuid MessageId UK "from Node-RED; makes redelivery a no-op"
        uuid FarmId "Farms farm"
        varchar SensorId
        varchar Type "Humidity | Temperature"
        float8 Value
        timestamptz Timestamp "measured"
        timestamptz ReceivedAt
    }
    AlertThresholds {
        uuid Id PK
        varchar Type UK "one global threshold per type (RF04)"
        float8 MinValue "check: below MaxValue"
        float8 MaxValue
        int Version
    }
    Alerts {
        uuid Id PK
        uuid ReadingId FK
        uuid FarmId
        uuid FarmOwnerId
        varchar Type
        float8 Value
        float8 MinValue "threshold at the time"
        float8 MaxValue
        varchar Description
        varchar WeatherContext "rain forecast, low humidity only"
        timestamptz CreatedAt
        timestamptz AcknowledgedAt
        uuid AcknowledgedBy
    }
    OutboxMessages {
        uuid Id PK
        varchar EventType
        varchar RoutingKey
        jsonb Payload
        timestamptz PublishedAt
    }
```

The thresholds are seeded by the migration (humidity 30-80 %, temperature 5-35 °C); the administrator only changes
them. Readings are indexed by farm, type and time for the history queries (RF18).

## Notifications.Service (`notifications_db`)

```mermaid
erDiagram
    Notifications {
        uuid Id PK
        uuid SourceEventId "AMQP MessageId; unique with RecipientId"
        uuid RecipientId "Auth user"
        varchar Type "OrderCreated | OrderStatusChanged | OrderCancelled | SensorAlert"
        varchar Title
        varchar Message
        uuid RelatedEntityId "order or alert"
        uuid FarmId
        timestamptz CreatedAt
        timestamptz ReadAt
        varchar(20) EmailStatus "Pending | Sent | Failed | Skipped"
        int EmailAttempts
        timestamptz EmailNextAttemptAt
        timestamptz EmailSentAt
        varchar EmailLastError
        varchar(55) TraceParent
    }
```

Each notification is listed in the app and sent by email in the background; `EmailNextAttemptAt` drives the retries.
