# Node-RED with the AMQP nodes and the sensor flow already installed. Build context: Simulation/node-red.
FROM nodered/node-red:4.1

RUN npm install --no-fund --no-audit --omit=dev @meowwolf/node-red-contrib-amqp@2.4.2

COPY --chown=node-red:node-red settings.js flows.json /data/

ENV RABBITMQ_HOST=rabbitmq \
    RABBITMQ_PORT=5672 \
    RABBITMQ_USER=guest \
    RABBITMQ_PASSWORD=guest \
    GATEWAY_URL=http://api-gateway:8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=5 \
    CMD node -e "fetch('http://localhost:1880/').then(r => process.exit(r.ok ? 0 : 1), () => process.exit(1))"
