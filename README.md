# CropConnect
CropConnect - Agricultural Management &amp; Marketplace Platform

## Running with Docker

`compose.yaml` starts every service with PostgreSQL and RabbitMQ:

```bash
docker compose up -d --build
```

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
