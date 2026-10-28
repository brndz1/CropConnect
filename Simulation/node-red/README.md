# Simulated farm sensors (Node-RED)

`flows.json` simulates the humidity and temperature sensors of each farm. Every 30 seconds it
publishes one reading per farm and sensor type to RabbitMQ, which `Sensors.Service` consumes (RF16).

| Item | Value |
|---|---|
| Exchange | `sensors.readings` (topic, durable) |
| Routing key | `farm.{farmId}.{type}` |
| Body | `{ "messageId", "farmId", "sensorId", "type": "humidity" \| "temperature", "value", "timestamp" }` |

## Run

1. Start RabbitMQ and Node-RED (the ports avoid clashing with other local brokers):

   ```bash
   docker run -d --name cropconnect-rabbitmq -p 5673:5672 -p 15673:15672 rabbitmq:4-management
   docker run -d --name cropconnect-nodered -p 1881:1880 nodered/node-red
   ```

2. Install the AMQP nodes and restart Node-RED:

   ```bash
   docker exec -w /data cropconnect-nodered npm install @meowwolf/node-red-contrib-amqp
   docker restart cropconnect-nodered
   ```

3. Open http://localhost:1881, choose **Import**, paste `flows.json` and deploy.
4. In the **Farms to simulate** node, replace the example with the IDs of farms created in
   `Farms.Service` and deploy again. Readings of unknown farms go to the
   `sensors-service.readings.dead-letter` queue.
5. Click **Force dry spell** to make the next humidity readings fall below the threshold and raise an
   alert (RF17, RF21).

The broker node connects to `host.docker.internal:5673` with the default `guest` user; change it in
the **CropConnect RabbitMQ** configuration node if your broker runs elsewhere.

Its vhost field is `?frameMax=131072`: the node appends that field to the connection URL, and
RabbitMQ 4.1+ refuses the client's default frame size of 4096 bytes (`negotiated frame_max = 4096 is
lower than the minimum allowed value (8192)`). The empty vhost before `?` means the default `/`.
