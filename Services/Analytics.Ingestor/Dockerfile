# Build from the repository root, since the service references CropConnect.ServiceDefaults
# and compiles the Farms contract as a client:
#   docker build -f Services/Analytics.Ingestor/Dockerfile -t cropconnect/analytics-ingestor .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages layer is reused while only the code changes.
COPY CropConnect.ServiceDefaults/CropConnect.ServiceDefaults.csproj CropConnect.ServiceDefaults/
COPY Services/Analytics.Ingestor/Analytics.Ingestor.csproj Services/Analytics.Ingestor/
RUN dotnet restore Services/Analytics.Ingestor/Analytics.Ingestor.csproj

COPY CropConnect.ServiceDefaults/ CropConnect.ServiceDefaults/
COPY Services/Farms.Service/Protos/farms.proto Services/Farms.Service/Protos/
COPY Services/Analytics.Ingestor/ Services/Analytics.Ingestor/
RUN dotnet publish Services/Analytics.Ingestor/Analytics.Ingestor.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl runs the health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# No gRPC server here, only the health endpoints over plain HTTP.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Analytics.Ingestor.dll"]
