# CropConnect documentation

The technical documents that go with the project report, describing the system as built.

| Document | Contents |
|---|---|
| [Architecture](architecture.md) | Services, how they talk to each other, and how the system keeps working when a part fails |
| [Data model](data-model.md) | Entity-relationship diagram of each service's database |
| [Messaging](messaging.md) | RabbitMQ exchanges, queues, routing keys and the payload of every event |
| [gRPC contracts](grpc.md) | The internal services, their RPCs and who may call them |
| [REST API](api.md) | The gateway's endpoints, roles, errors and the [OpenAPI document](openapi.json) |
| [Analytics platform](analytics.md) | The raw zone in MongoDB that the analytics ingestor fills, the simulated history, and the Databricks lakehouse built from it (landing with Auto Loader, Bronze, Silver, Gold and a star schema) |

How to run the system and its tests is in the [repository README](../README.md).
