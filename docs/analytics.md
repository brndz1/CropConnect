# Analytics platform

The services' databases serve the application. Some keep history (`sensors_db` has every reading and alert), others
only the current state: an order holds only its latest status, and a price is updated in place. The data is also
split per service, and analytical queries would compete with the application. The full history the business would
want to analyse (how a farm's humidity evolved, how long orders wait in each state, how sales react to a drought) is
in the messages that cross RabbitMQ, and it was lost once they were consumed. The analytics platform keeps it, apart
from the operational databases.

```mermaid
flowchart LR
    nodered([Node-RED]) --> mq{{RabbitMQ}}
    orders[Orders] & sensors[Sensors] & auth[Auth] --> mq
    mq -- own queues --> ingestor[Analytics.Ingestor]
    ingestor -- daily snapshot, gRPC --> farms[Farms]
    ingestor --> mongo[(MongoDB<br/>cropconnect_raw)]
    backfill([backfill.py<br/>simulated history]) -.-> mongo
    mongo -- mongodump / mongorestore --> atlas[(MongoDB Atlas)]
    atlas -- PyMongo --> bronze[Bronze] --> silver[Silver] --> gold[Gold] --> dashboard([AI/BI dashboard])
    subgraph databricks [Databricks]
        bronze
        silver
        gold
        dashboard
    end
```

## Raw zone (MongoDB)

[Analytics.Ingestor](../Services/Analytics.Ingestor) has its own queues on the two exchanges, so it gets a copy of
every reading and domain event without changing the services that already consume them. It stores each one as it
arrived in MongoDB (database `cropconnect_raw`), and once a day it also stores a snapshot of the public catalog read
from Farms. The collections are described in the [data model](data-model.md#analyticsingestor-mongodb-cropconnect_raw).

Why a document database for this zone:

- **The payloads differ.** Each of the routing keys has its own fields, and new events or fields appear over time.
  A document keeps each one as it came, with no table per event type and no migration when a field is added.
- **The readings are a time series.** They arrive all the time and are always read by sensor and time, which is what
  MongoDB's time-series collections are made for.
- **It keeps the analytics away from the operational databases.** The PostgreSQL databases keep serving the
  application, and the analytics never query them.

## Running it

Docker Compose and the Aspire AppHost start MongoDB (published on `127.0.0.1:27018`) and the ingestor with the rest
of the platform. To have months of data to analyse, write a simulated history (marked `"simulated": true`):

```bash
python -m pip install -r analytics/backfill/requirements.txt
```

```bash
python analytics/backfill/backfill.py --replace
```

By default it writes 6 months for 24 farms (about 725,000 readings, 76,000 events and 32,000 snapshot documents);
`--help` lists the options. `--replace` deletes the simulated documents first, and the same `--seed` gives the same
history.

## Lakehouse (Databricks)

The notebooks in [analytics/databricks](../analytics/databricks) build a medallion lakehouse of Delta tables in
Unity Catalog (catalog `cropconnect`, one schema per layer). Databricks runs in the cloud and cannot reach the local
MongoDB, so the raw zone is copied to a MongoDB Atlas cluster with `mongodump`/`mongorestore`, and Bronze reads it
from there.

| Notebook | Layer | Writes |
|---|---|---|
| [01_bronze.py](../analytics/databricks/01_bronze.py) | Bronze | One table per collection, each document kept whole as Extended JSON. **Incremental.** |
| [02_silver.py](../analytics/databricks/02_silver.py) | Silver | Typed tables per business concept, invalid readings in quarantine. Readings **incremental**, the rest rebuilt. |
| [03_gold.py](../analytics/databricks/03_gold.py) | Gold | Aggregates for the dashboard and a star schema of the sales. Rebuilt on every run. |

A job runs the three in order (Silver only after Bronze succeeds, Gold only after Silver), and the AI/BI dashboard
reads only Gold.

### Incremental writes

- **Bronze** keeps a watermark per collection (`receivedAt` or `snapshotAt`) in `bronze._ingestion_state` and asks
  MongoDB only for newer documents (`$gte`). Rows are `MERGE`d on the document id, so reading one again changes
  nothing. The watermark moves only once a collection was read to the end. Documents are read unsorted: sorting a
  time-series collection by a field other than its time field goes over the 32 MB sort limit of a free Atlas cluster.
- **Silver** does the same for the sensor readings, with the Bronze `_ingested_at` as the watermark
  (`silver._processing_state`) and a `MERGE` on `reading_id`. The `full_refresh` widget rebuilds them, for example
  after a quality rule changes. The other Silver tables are small and rebuilt on every run.
- **A dropped table comes back.** If a Bronze or Silver incremental table does not exist, its watermark is ignored and
  everything is read again (the `MERGE` keeps it free of duplicates). Every other table is rebuilt on the next run.

### Star schema

`gold.fact_order_lines` is the fact table, at the grain of an order line (quantity, unit price, line total, whether
the order was cancelled), with keys to `gold.dim_farm`, `gold.dim_product` and `gold.dim_date` (`date_key` as
`yyyyMMdd`). The notebook checks that every fact finds its farm, product and day. `gold.order_lifecycle` is a fact
table too, an accumulating snapshot with one date column per step of the order.

### Running it in Databricks

1. Import the three notebooks into the workspace.
2. Store the Atlas connection string as the secret `cropconnect`/`mongo-uri` (or fill the `mongo_uri` widget of
   `01_bronze` for a one-off run). It never goes into the code.
3. Create a job with the three notebooks as dependent tasks, on serverless compute.
