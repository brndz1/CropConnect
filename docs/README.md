# CropConnect documentation

The documents promised in section 8 of the project report, describing the system as built.

| Document | Contents |
|---|---|
| [Architecture](architecture.md) | Services, how they talk to each other, and how the system keeps working when a part fails |
| [Data model](data-model.md) | Entity-relationship diagram of each service's database |
| [Messaging](messaging.md) | RabbitMQ exchanges, queues, routing keys and the payload of every event |
| [gRPC contracts](grpc.md) | The internal services, their RPCs and who may call them |
| [REST API](api.md) | The gateway's endpoints, roles, errors and the [OpenAPI document](openapi.json) |

How to run the system and its tests is in the [repository README](../README.md).
