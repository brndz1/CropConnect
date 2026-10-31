# The web app: built with Node, served by nginx, which also forwards /api to the gateway, so the browser talks to
# one origin and the gateway needs no CORS.
#   docker build -t cropconnect/web web

FROM node:24-alpine AS build
WORKDIR /app

# Install first, so the packages layer is reused while only the code changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:1.29-alpine AS runtime

# Where /api goes; the official image fills it into the template when the container starts.
ENV GATEWAY_URL=http://api-gateway:8080
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
