# Build from the repository root, since the gateway references CropConnect.ServiceDefaults
# and compiles every service's contract as a client:
#   docker build -f CropConnect.ApiGateway/Dockerfile -t cropconnect/api-gateway .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages layer is reused while only the code changes.
COPY CropConnect.ServiceDefaults/CropConnect.ServiceDefaults.csproj CropConnect.ServiceDefaults/
COPY CropConnect.ApiGateway/CropConnect.ApiGateway.csproj CropConnect.ApiGateway/
RUN dotnet restore CropConnect.ApiGateway/CropConnect.ApiGateway.csproj

COPY CropConnect.ServiceDefaults/ CropConnect.ServiceDefaults/
COPY Services/Auth.Service/Protos/auth.proto Services/Auth.Service/Protos/
COPY Services/Users.Service/Protos/users.proto Services/Users.Service/Protos/
COPY Services/Farms.Service/Protos/farms.proto Services/Farms.Service/Protos/
COPY Services/Orders.Service/Protos/orders.proto Services/Orders.Service/Protos/
COPY Services/Sensors.Service/Protos/sensors.proto Services/Sensors.Service/Protos/
COPY Services/Notifications.Service/Protos/notifications.proto Services/Notifications.Service/Protos/
COPY CropConnect.ApiGateway/ CropConnect.ApiGateway/
RUN dotnet publish CropConnect.ApiGateway/CropConnect.ApiGateway.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl runs the health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# REST over plain HTTP; put TLS termination (a reverse proxy or the cloud load balancer) in front of it.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "CropConnect.ApiGateway.dll"]
