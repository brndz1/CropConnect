# Databricks notebook source
# MAGIC %md
# MAGIC # 01 · Bronze: copy the MongoDB raw zone into Delta tables
# MAGIC
# MAGIC The raw zone (`cropconnect_raw` in MongoDB Atlas) keeps every sensor reading and domain event as it arrived,
# MAGIC plus a daily snapshot of the catalogue. Bronze copies those documents into Delta tables **without changing
# MAGIC them**: each row keeps the whole document as JSON, so nothing is lost if Silver later needs a field it ignored.
# MAGIC
# MAGIC | MongoDB collection | Bronze table | One row per | Watermark |
# MAGIC |---|---|---|---|
# MAGIC | `sensor_readings` | `bronze.sensor_readings` | reading | `receivedAt` |
# MAGIC | `domain_events` | `bronze.domain_events` | event | `receivedAt` |
# MAGIC | `catalog_snapshots` | `bronze.catalog_snapshots` | farm or product of a snapshot | `snapshotAt` |
# MAGIC
# MAGIC **Incremental:** each run reads only the documents newer than the latest one already in Bronze (the
# MAGIC watermark), and a `MERGE` on the document's id means running it twice never duplicates rows.

# COMMAND ----------

# MAGIC %pip install pymongo==4.10.1

# COMMAND ----------

# MAGIC %md
# MAGIC ## Settings
# MAGIC
# MAGIC The connection string is a secret: it comes from the secret scope `cropconnect` (key `mongo-uri`) when it
# MAGIC exists, otherwise from the `mongo_uri` widget at the top of the notebook. It is never written in the code.

# COMMAND ----------

dbutils.widgets.text("catalog", "cropconnect")
dbutils.widgets.text("mongo_database", "cropconnect_raw")
dbutils.widgets.text("mongo_uri", "")

CATALOG = dbutils.widgets.get("catalog")
MONGO_DATABASE = dbutils.widgets.get("mongo_database")


def mongo_uri() -> str:
    try:
        return dbutils.secrets.get(scope="cropconnect", key="mongo-uri")
    except Exception:
        uri = dbutils.widgets.get("mongo_uri").strip()
        if not uri:
            raise ValueError("Set the mongo_uri widget or the secret cropconnect/mongo-uri.")
        return uri


spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.bronze")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Reading from MongoDB
# MAGIC
# MAGIC `bson.json_util` turns each document into Extended JSON, which keeps the MongoDB types that plain JSON
# MAGIC loses (dates as `{"$date": …}`, ObjectIds as `{"$oid": …}`). Silver reads them back with those markers.
# MAGIC The documents are read in batches so a large collection never has to fit in memory at once.

# COMMAND ----------

from datetime import datetime, timezone

from bson import json_util
from pymongo import MongoClient
from pyspark.sql import functions as F
from pyspark.sql.types import BooleanType, StringType, StructField, StructType, TimestampType

BRONZE_SCHEMA = StructType(
    [
        StructField("source_id", StringType(), False),  # id of the document in the raw zone
        StructField("event_time", TimestampType(), True),  # receivedAt or snapshotAt: the watermark
        StructField("kind", StringType(), True),  # routing key (readings, events) or entity (snapshots)
        StructField("simulated", BooleanType(), False),  # written by the backfill, not by the platform
        StructField("document", StringType(), False),  # the whole document as Extended JSON
    ]
)

# How each collection names its id, its watermark and its kind.
SOURCES = {
    "sensor_readings": {
        "time_field": "receivedAt",
        "source_id": lambda d: d.get("messageId") or str(d["_id"]),
        "kind": lambda d: d.get("routingKey"),
    },
    "domain_events": {
        "time_field": "receivedAt",
        "source_id": lambda d: str(d["_id"]),  # the AMQP MessageId
        "kind": lambda d: d.get("routingKey"),
    },
    "catalog_snapshots": {
        "time_field": "snapshotAt",
        "source_id": lambda d: f"{d.get('snapshotId')}:{d.get('entity')}:{d.get('entityId')}",
        "kind": lambda d: d.get("entity"),
    },
}


def as_utc(value):
    """MongoDB returns naive datetimes in UTC; Spark needs them as such."""
    if isinstance(value, datetime):
        return value if value.tzinfo else value.replace(tzinfo=timezone.utc)
    return None


def to_row(document: dict, source: dict) -> tuple:
    return (
        source["source_id"](document),
        as_utc(document.get(source["time_field"])),
        source["kind"](document),
        bool(document.get("simulated", False)),
        json_util.dumps(document),
    )


# COMMAND ----------

# MAGIC %md
# MAGIC ## Writing to Delta
# MAGIC
# MAGIC - The **watermark** of each collection is kept in `bronze._ingestion_state`: the latest `event_time` of the
# MAGIC   last run that **finished**. The query asks MongoDB for documents at or after it (`$gte`, so a document
# MAGIC   that arrived in the same millisecond is not missed).
# MAGIC - The documents are read **unsorted**: sorting a time-series collection by `receivedAt` (not its time
# MAGIC   field) needs more than the 32 MB of memory a free Atlas cluster allows. That is safe because the watermark
# MAGIC   only moves when a collection was read to the end: if a run fails halfway, the next one starts again from
# MAGIC   the previous watermark.
# MAGIC - The **MERGE** inserts only the ids not yet in Bronze, so whatever is read again is never duplicated.
# MAGIC - If a Bronze table **does not exist** (first run, or someone dropped it), its watermark is ignored and the whole
# MAGIC   collection is read again. Without this, a dropped table would come back with only the newest documents.
# MAGIC - `_ingested_at` records when Bronze received each row, which helps to explain the data in the defence.

# COMMAND ----------

STATE_TABLE = f"{CATALOG}.bronze._ingestion_state"
spark.sql(f"CREATE TABLE IF NOT EXISTS {STATE_TABLE} (collection STRING, watermark TIMESTAMP, updated_at TIMESTAMP) USING DELTA")


def watermark(collection_name: str):
    row = spark.table(STATE_TABLE).filter(F.col("collection") == collection_name).select("watermark").first()
    return row[0] if row else None


def save_watermark(collection_name: str, value) -> None:
    if value is None:
        return
    spark.createDataFrame([(collection_name, value)], "collection string, watermark timestamp").createOrReplaceTempView("new_state")
    spark.sql(
        f"""
        MERGE INTO {STATE_TABLE} AS target
        USING new_state AS source
        ON target.collection = source.collection
        WHEN MATCHED THEN UPDATE SET watermark = source.watermark, updated_at = current_timestamp()
        WHEN NOT MATCHED THEN INSERT (collection, watermark, updated_at) VALUES (source.collection, source.watermark, current_timestamp())
        """
    )


def merge_batch(rows: list, table: str) -> int:
    batch = spark.createDataFrame(rows, BRONZE_SCHEMA).withColumn("_ingested_at", F.current_timestamp())
    if not spark.catalog.tableExists(table):
        batch.write.format("delta").saveAsTable(table)
        return len(rows)
    batch.createOrReplaceTempView("bronze_batch")
    spark.sql(
        f"""
        MERGE INTO {table} AS target
        USING bronze_batch AS source
        ON target.source_id = source.source_id
        WHEN NOT MATCHED THEN INSERT *
        """
    )
    return len(rows)


def ingest(collection_name: str, batch_size: int = 20_000) -> int:
    source = SOURCES[collection_name]
    table = f"{CATALOG}.bronze.{collection_name}"
    if spark.catalog.tableExists(table):
        since = as_utc(watermark(collection_name))  # Spark returns it without a time zone; it is UTC
    else:
        since = None  # first run, or the table was dropped: its watermark no longer applies, read everything
    query = {source["time_field"]: {"$gte": since}} if since else {}

    newest = since
    with MongoClient(mongo_uri(), tz_aware=False) as client:
        cursor = client[MONGO_DATABASE][collection_name].find(query).batch_size(batch_size)
        rows, read = [], 0
        for document in cursor:
            row = to_row(document, source)
            rows.append(row)
            if row[1] is not None and (newest is None or row[1] > newest):
                newest = row[1]
            if len(rows) >= batch_size:
                read += merge_batch(rows, table)
                rows = []
        if rows:
            read += merge_batch(rows, table)

    # Only now, with the whole collection read, does the watermark move forward.
    save_watermark(collection_name, newest)
    print(f"{collection_name}: read {read} documents since {since or 'the beginning'}")
    return read


# COMMAND ----------

# MAGIC %md
# MAGIC ## Run

# COMMAND ----------

for name in SOURCES:
    ingest(name)

# COMMAND ----------

# MAGIC %md
# MAGIC ## Check
# MAGIC
# MAGIC How many rows each table has, how many are simulated, and the time span they cover.

# COMMAND ----------

for name in SOURCES:
    table = f"{CATALOG}.bronze.{name}"
    display(
        spark.table(table).agg(
            F.lit(name).alias("table"),
            F.count("*").alias("rows"),
            F.sum(F.col("simulated").cast("int")).alias("simulated_rows"),
            F.min("event_time").alias("from"),
            F.max("event_time").alias("to"),
        )
    )

# COMMAND ----------

# MAGIC %md
# MAGIC What a reading and an event look like in Bronze: the document is untouched, Silver will parse it.

# COMMAND ----------

display(spark.table(f"{CATALOG}.bronze.domain_events").groupBy("kind").count().orderBy(F.desc("count")))
display(spark.table(f"{CATALOG}.bronze.sensor_readings").select("source_id", "event_time", "kind", "document").limit(5))
