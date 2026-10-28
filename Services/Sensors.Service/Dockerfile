# Build from the repository root, since the service references CropConnect.ServiceDefaults
# and compiles the Farms contract (farms.proto) as a client:
#   docker build -f Services/Sensors.Service/Dockerfile -t cropconnect/sensors-service .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages layer is reused while only the code changes.
COPY CropConnect.ServiceDefaults/CropConnect.ServiceDefaults.csproj CropConnect.ServiceDefaults/
COPY Services/Sensors.Service/Sensors.Service.csproj Services/Sensors.Service/
RUN dotnet restore Services/Sensors.Service/Sensors.Service.csproj

COPY CropConnect.ServiceDefaults/ CropConnect.ServiceDefaults/
COPY Services/Farms.Service/Protos/farms.proto Services/Farms.Service/Protos/
COPY Services/Sensors.Service/ Services/Sensors.Service/
RUN dotnet publish Services/Sensors.Service/Sensors.Service.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for Kerberos (GSSAPI) on every connection and logs an error when the library is missing.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# gRPC over plain HTTP/2 (h2c), as configured in appsettings.json; TLS ends at the gateway.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Sensors.Service.dll"]
