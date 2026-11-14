# Databricks notebook source
# MAGIC %md
# MAGIC # 00 · Landing: extract the new MongoDB documents to files
# MAGIC
# MAGIC Auto Loader ingests **files**, and the raw zone is a database. This notebook is the bridge: it reads the
# MAGIC documents that arrived in MongoDB Atlas since the last run and writes them as JSON files to a Unity Catalog
# MAGIC **volume**, one folder per collection. Bronze then picks the new files up with Auto Loader.
# MAGIC
# MAGIC ```
# MAGIC MongoDB Atlas ──PyMongo──► /Volumes/<catalog>/landing/raw/<collection>/*.json ──Auto Loader──► Bronze
# MAGIC ```
# MAGIC
# MAGIC Each line of a file is one document, already in the shape of a Bronze row: its id, its time, its kind, whether
# MAGIC it is simulated, and the whole document as Extended JSON. The files are kept, so the landing zone is also an
# MAGIC archive Bronze can always be rebuilt from.

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
LANDING = f"/Volumes/{CATALOG}/landing/raw"


def mongo_uri() -> str:
    try:
        return dbutils.secrets.get(scope="cropconnect", key="mongo-uri")
    except Exception:
        uri = dbutils.widgets.get("mongo_uri").strip()
        if not uri:
            raise ValueError("Set the mongo_uri widget or the secret cropconnect/mongo-uri.")
        return uri


spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.landing")
spark.sql(f"CREATE VOLUME IF NOT EXISTS {CATALOG}.landing.raw")
spark.sql(f"CREATE VOLUME IF NOT EXISTS {CATALOG}.landing.checkpoints")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Extracting
# MAGIC
# MAGIC - The **watermark** of each collection (`receivedAt` or `snapshotAt` of the newest document extracted) is kept
# MAGIC   in `landing._extract_state`. A run asks MongoDB only for documents at or after it (`$gte`, so a document that
# MAGIC   arrived in the same millisecond is not missed; the few read twice are dropped by Bronze's `MERGE`).
# MAGIC - The documents are read **unsorted**: sorting a time-series collection by `receivedAt` (not its time field)
# MAGIC   needs more than the 32 MB of memory a free Atlas cluster allows. The watermark only moves once a collection
# MAGIC   was read to the end, so a run that fails halfway is simply repeated by the next one.
# MAGIC - If a collection's folder is **missing or empty** (first run, or someone deleted the files), the watermark is
# MAGIC   ignored and the whole collection is extracted again.

# COMMAND ----------

import json
import os
from datetime import datetime, timezone

from bson import json_util
from pymongo import MongoClient
from pyspark.sql import functions as F

# How each collection names its id, its time and its kind.
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

STATE_TABLE = f"{CATALOG}.landing._extract_state"
spark.sql(f"CREATE TABLE IF NOT EXISTS {STATE_TABLE} (collection STRING, watermark TIMESTAMP, updated_at TIMESTAMP) USING DELTA")


def as_utc(value):
    """MongoDB and Spark give naive datetimes; they are UTC."""
    if isinstance(value, datetime):
        return value if value.tzinfo else value.replace(tzinfo=timezone.utc)
    return None


def watermark(collection_name: str):
    row = spark.table(STATE_TABLE).filter(F.col("collection") == collection_name).select("watermark").first()
    return as_utc(row[0]) if row else None


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


def to_line(document: dict, source: dict) -> str:
    event_time = as_utc(document.get(source["time_field"]))
    return json.dumps(
        {
            "source_id": source["source_id"](document),
            "event_time": event_time.isoformat(timespec="milliseconds") if event_time else None,
            "kind": source["kind"](document),
            "simulated": bool(document.get("simulated", False)),
            "document": json_util.dumps(document),
        }
    )


def extract(collection_name: str, batch_size: int = 20_000) -> int:
    source = SOURCES[collection_name]
    folder = f"{LANDING}/{collection_name}"
    os.makedirs(folder, exist_ok=True)
    has_files = any(name.endswith(".json") for name in os.listdir(folder))
    since = watermark(collection_name) if has_files else None
    query = {source["time_field"]: {"$gte": since}} if since else {}

    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
    newest, written, part, lines = since, 0, 0, []

    def flush():
        nonlocal part, lines
        if lines:
            # The job runs Bronze only after this notebook ends, so Auto Loader never sees a half-written file.
            with open(f"{folder}/{run_id}_{part:04d}.json", "w", encoding="utf-8") as file:
                file.write("\n".join(lines) + "\n")
            part, lines = part + 1, []

    with MongoClient(mongo_uri(), tz_aware=False) as client:
        for document in client[MONGO_DATABASE][collection_name].find(query).batch_size(batch_size):
            lines.append(to_line(document, source))
            written += 1
            moment = as_utc(document.get(source["time_field"]))
            if moment is not None and (newest is None or moment > newest):
                newest = moment
            if len(lines) >= batch_size:
                flush()
        flush()

    # Only now, with the whole collection read, does the watermark move forward.
    save_watermark(collection_name, newest)
    print(f"{collection_name}: {written} documents written to {part} file(s) since {since or 'the beginning'}")
    return written


# COMMAND ----------

# MAGIC %md
# MAGIC ## Run

# COMMAND ----------

for name in SOURCES:
    extract(name)

# COMMAND ----------

# MAGIC %md
# MAGIC ## Check
# MAGIC
# MAGIC The files in the landing volume, per collection.

# COMMAND ----------

display(
    spark.createDataFrame(
        [
            (name, len([f for f in os.listdir(f"{LANDING}/{name}") if f.endswith(".json")]))
            for name in SOURCES
        ],
        "collection string, files long",
    )
)
display(spark.table(STATE_TABLE))
