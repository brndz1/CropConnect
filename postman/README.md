# Postman collection

`CropConnect.postman_collection.json` tests the REST API from the outside, in the order of the BPMN processes: it
registers a new manager and client on every run, then goes through the catalog, an order from placement to pickup,
a cancellation, the sensor thresholds and readings, the administrator's actions and the error responses. Each
request checks its status code and body; the requests that wait for a notification poll for up to 15 seconds,
because notifications arrive asynchronously through RabbitMQ.

| Folder | What it checks |
|---|---|
| 00 Preparação | Registration that waits for email confirmation (login 403, resend, invalid links), password recovery that reveals no accounts, the administrator creating a manager and a client (already verified), login of the three roles, duplicate registration and wrong password |
| 01 Segurança e perfis de acesso | 401 without or with a tampered token, 403 for the wrong role, renewing the JWT with a refresh token that only works once, logout, and the 429 after too many login attempts |
| 02 Gestão do catálogo | Farm, pickup schedule, product and surprise basket; public catalog |
| 03 Encomenda e levantamento | Stock reservation, insufficient stock, status transitions, notifications, review |
| 04 Cancelamento | The stock is returned and the manager is notified |
| 05 Monitorização de sensores | Thresholds (admin only), optimistic concurrency, readings only for the farm's manager |
| 06 Administração | Deactivating a farm and an account |
| 07 Erros e documentação | Problem details for 400 and 404, the OpenAPI document and the health check |

## Running it

In Postman, import the collection and the two environments, pick **CropConnect Local** (Docker Compose) or
**CropConnect Azure**, fill in `adminPassword` (and `adminEmail` if it is not the default) and run the collection
with the Collection Runner.

From the command line, with Newman:

```bash
npx newman run postman/CropConnect.postman_collection.json -e postman/Local.postman_environment.json --env-var "adminPassword=<password>"
```

The local administrator's password is `ADMIN_PASSWORD` in `.env` (or `.env.example`); on Azure it is in
`deploy/k8s/overlays/azure/secrets.env`.
