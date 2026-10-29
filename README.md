# CropConnect

[![CI](https://github.com/brndz1/CropConnect/actions/workflows/ci.yml/badge.svg)](https://github.com/brndz1/CropConnect/actions/workflows/ci.yml)

CropConnect - Agricultural Management &amp; Marketplace Platform

## Running with .NET Aspire

For development, the Aspire AppHost starts every service as a project, with PostgreSQL (one database per
service), RabbitMQ and Mailpit in containers, and opens the Aspire dashboard with their logs, distributed
traces and metrics:

```bash
dotnet run --project CropConnect.AppHost
```

- The dashboard URL (with its login token) is printed on startup. The resource list links to the gateway,
  the RabbitMQ management UI and the Mailpit inbox.
- Traces follow a request from the gateway through the gRPC calls between services, their PostgreSQL
  commands and the RabbitMQ messages they publish and consume.
- The services use their `appsettings.Development.json` secrets; Aspire only supplies connection strings,
  the broker and the addresses of the other services.
- Stop Docker Compose first (`docker compose stop`): both publish the services on the same ports.

## Running with Docker

`compose.yaml` starts every service with PostgreSQL and RabbitMQ:

```bash
docker compose up -d --build
```

Clients use the REST API of the gateway, which translates each call to gRPC on the owning service:

| Entry point | Address |
|---|---|
| API Gateway (REST) | http://localhost:5000/api |
| API reference (Scalar) | http://localhost:5000/scalar |
| OpenAPI document | http://localhost:5000/openapi/v1.json |

Log in with `POST /api/auth/login` and send the token as `Authorization: Bearer <token>`. The services are
also published on their own ports for development:

| Service | gRPC endpoint (h2c) |
|---|---|
| Auth | `localhost:5298` |
| Users | `localhost:5001` |
| Farms | `localhost:5002` |
| Orders | `localhost:5003` |
| Sensors | `localhost:5004` |
| Notifications | `localhost:5005` |

| Infrastructure | Address |
|---|---|
| PostgreSQL | `localhost:5440` (one database per service) |
| RabbitMQ (AMQP) | `localhost:5673` |
| RabbitMQ management | http://localhost:15673 (`guest` / `guest`) |
| Mailpit (notification emails) | http://localhost:8025 |

- Each service applies its migrations on startup; Auth also creates the bootstrap administrator
  (`admin@cropconnect.test`).
- The defaults are for development only. Copy `.env.example` to `.env` to change secrets, credentials
  and ports.
- Data is kept in the `postgres-data` and `rabbitmq-data` volumes; `docker compose down -v` removes it.
- Notification emails are caught by Mailpit. To send them through Mailtrap instead, set `EMAIL_PROVIDER=Mailtrap`
  and `MAILTRAP_API_TOKEN` (plus `MAILTRAP_SANDBOX_INBOX_ID` to use a sandbox inbox) in `.env`.
- To simulate the farm sensors, see [Simulation/node-red](Simulation/node-red/README.md).

Each service can also be built on its own from the repository root, for example:

```bash
docker build -f Services/Auth.Service/Dockerfile -t cropconnect/auth-service .
```

## End-to-end tests

`tests/CropConnect.EndToEndTests` tests the running platform from the outside, the way clients use it: through
the gateway's REST API, RabbitMQ (as the sensors do) and Mailpit (the notification emails). Start the stack and
run them:

```bash
docker compose up -d --build --wait
dotnet test tests/CropConnect.EndToEndTests --filter "Category!=Resilience"
```

GitHub Actions (`.github/workflows/ci.yml`) builds the solution and runs both suites against Docker Compose
on every pull request and on `main`.

The resilience tests stop and restart containers to check that orders and emails survive an outage of
Notifications or of the mail server, so they run on their own:

```bash
dotnet test tests/CropConnect.EndToEndTests --filter "Category=Resilience"
```

To test another environment (e.g. the Aspire AppHost), set `CROPCONNECT_GATEWAY_URL`,
`CROPCONNECT_RABBITMQ_MANAGEMENT_URL` and `CROPCONNECT_MAILPIT_URL`.
