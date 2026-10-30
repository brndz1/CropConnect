# Simulated farm sensors (Node-RED)

`flows.json` simulates the humidity and temperature sensors of each farm. Every minute it loads the
active farms from the API gateway (`GET /api/farms`, first 100), and every 30 seconds it publishes one
reading per farm and sensor type to RabbitMQ, which `Sensors.Service` consumes (RF16).

| Item | Value |
|---|---|
| Exchange | `sensors.readings` (topic, durable) |
| Routing key | `farm.{farmId}.{type}` |
| Body | `{ "messageId", "farmId", "sensorId", "type": "humidity" \| "temperature", "value", "timestamp" }` |

## Run

The image in this folder has the AMQP nodes, the flow and its settings built in, so nothing is installed
or imported by hand.

- **Docker Compose:** from the repository root, `docker compose --profile simulation up -d --build`. The
  simulator is behind a profile so the end-to-end tests never see its random alerts. Open the flow at
  http://localhost:1881 (`SIMULATOR_PORT` in `.env`).
- **Aspire:** `dotnet run --project CropConnect.AppHost` starts it as the `sensor-simulator` resource; the
  dashboard links to its editor.

Click **Force dry spell** to make the next humidity readings of every farm fall below the threshold and raise
an alert (RF17, RF21). Farms appear in the simulation within a minute of being created.

## Configuration

| Variable | Default | Use |
|---|---|---|
| `RABBITMQ_HOST`, `RABBITMQ_PORT` | `rabbitmq`, `5672` | Broker the readings are published to |
| `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | `guest`, `guest` | Broker credentials (read by `settings.js`, never stored in the flow) |
| `GATEWAY_URL` | `http://api-gateway:8080` | Where the farms are loaded from |

The broker node's vhost field is `?frameMax=131072`: the node appends that field to the connection URL, and
RabbitMQ 4.1+ refuses the client's default frame size of 4096 bytes (`negotiated frame_max = 4096 is
lower than the minimum allowed value (8192)`). The empty vhost before `?` means the default `/`.

Changes made in the editor last until the container is recreated; to keep them, export the flow over
`flows.json` and rebuild.
