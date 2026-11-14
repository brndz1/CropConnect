# Databricks notebook source
# MAGIC %md
# MAGIC # 01 · Bronze: ingest the landing files with Auto Loader
# MAGIC
# MAGIC `00_landing` writes the new MongoDB documents as JSON files to the landing volume. Bronze ingests them into Delta
# MAGIC tables **without changing them**: each row keeps the whole document as Extended JSON, so nothing is lost if
# MAGIC Silver later needs a field it ignored.
# MAGIC
# MAGIC | Landing folder | Bronze table | One row per |
# MAGIC |---|---|---|
# MAGIC | `/Volumes/<catalog>/landing/raw/sensor_readings` | `bronze.sensor_readings` | reading |
# MAGIC | `/Volumes/<catalog>/landing/raw/domain_events` | `bronze.domain_events` | event |
# MAGIC | `/Volumes/<catalog>/landing/raw/catalog_snapshots` | `bronze.catalog_snapshots` | farm or product of a snapshot |
# MAGIC
# MAGIC **Incremental, in two steps:**
# MAGIC
# MAGIC - **Auto Loader** (`cloudFiles`) reads only the files it has not processed yet. It keeps that list in a
# MAGIC   checkpoint, one per table, in the `landing.checkpoints` volume. It runs with `availableNow`: it processes every
# MAGIC   new file and stops, which is how a scheduled job uses a stream.
# MAGIC - Each micro-batch is written with **`MERGE INTO`** on the document id, so a document that comes in two files
# MAGIC   (the landing reads the last millisecond again) or is processed twice is never duplicated.

# COMMAND ----------

dbutils.widgets.text("catalog", "cropconnect")
CATALOG = dbutils.widgets.get("catalog")
LANDING = f"/Volumes/{CATALOG}/landing/raw"
CHECKPOINTS = f"/Volumes/{CATALOG}/landing/checkpoints"

spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.bronze")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Tables
# MAGIC
# MAGIC The schema of the files is fixed (the landing writes it), so it is given to Auto Loader instead of inferred.
# MAGIC Bronze adds `_ingested_at` (when the row reached Bronze, the watermark Silver uses) and `_source_file` (which
# MAGIC landing file it came from, for lineage).
# MAGIC
# MAGIC If a Bronze table **does not exist** (first run, or someone dropped it), its checkpoint is deleted too:
# MAGIC otherwise Auto Loader would remember the files as already processed and the table would come back empty. With
# MAGIC the checkpoint gone, every landing file is read again, and the `MERGE` keeps the table free of duplicates.

# COMMAND ----------

from pyspark.sql import DataFrame
from pyspark.sql import functions as F
from pyspark.sql.types import BooleanType, StringType, StructField, StructType, TimestampType

LANDING_SCHEMA = StructType(
    [
        StructField("source_id", StringType()),  # id of the document in the raw zone
        StructField("event_time", TimestampType()),  # receivedAt or snapshotAt
        StructField("kind", StringType()),  # routing key (readings, events) or entity (snapshots)
        StructField("simulated", BooleanType()),  # written by the backfill, not by the platform
        StructField("document", StringType()),  # the whole document as Extended JSON
    ]
)
COLLECTIONS = ["sensor_readings", "domain_events", "catalog_snapshots"]


def prepare(table: str, checkpoint: str) -> None:
    if not spark.catalog.tableExists(table):
        dbutils.fs.rm(checkpoint, True)  # the table is new: every landing file must be read again
        spark.sql(
            f"""
            CREATE TABLE {table} (
                source_id STRING, event_time TIMESTAMP, kind STRING, simulated BOOLEAN, document STRING,
                _ingested_at TIMESTAMP, _source_file STRING
            ) USING DELTA
            """
        )
    elif "_source_file" not in spark.table(table).columns:
        spark.sql(f"ALTER TABLE {table} ADD COLUMNS (_source_file STRING)")  # tables from before Auto Loader


def merge_into(table: str):
    def merge_batch(batch: DataFrame, batch_id: int) -> None:
        rows = (
            batch.filter(F.col("source_id").isNotNull())
            .dropDuplicates(["source_id"])
            .withColumn("_ingested_at", F.current_timestamp())
        )
        rows.createOrReplaceTempView("bronze_batch")
        rows.sparkSession.sql(
            f"""
            MERGE INTO {table} AS target
            USING bronze_batch AS source
            ON target.source_id = source.source_id
            WHEN NOT MATCHED THEN INSERT *
            """
        )

    return merge_batch


def ingest(collection: str) -> None:
    table = f"{CATALOG}.bronze.{collection}"
    checkpoint = f"{CHECKPOINTS}/bronze_{collection}"
    prepare(table, checkpoint)
    before = spark.table(table).count()

    (
        spark.readStream.format("cloudFiles")
        .option("cloudFiles.format", "json")
        .option("pathGlobFilter", "*.json")
        .schema(LANDING_SCHEMA)
        .load(f"{LANDING}/{collection}")
        .withColumn("_source_file", F.col("_metadata.file_path"))
        .writeStream.foreachBatch(merge_into(table))
        .option("checkpointLocation", checkpoint)
        .trigger(availableNow=True)
        .start()
        .awaitTermination()
    )
    after = spark.table(table).count()
    print(f"{collection}: {after - before} new rows ({after} in total)")


# COMMAND ----------

# MAGIC %md
# MAGIC ## Run

# COMMAND ----------

for name in COLLECTIONS:
    ingest(name)

# COMMAND ----------

# MAGIC %md
# MAGIC ## Check
# MAGIC
# MAGIC Rows per table, how many are simulated, the time span, and how many landing files they came from.

# COMMAND ----------

for name in COLLECTIONS:
    display(
        spark.table(f"{CATALOG}.bronze.{name}").agg(
            F.lit(name).alias("table"),
            F.count("*").alias("rows"),
            F.countDistinct("source_id").alias("distinct_ids"),
            F.sum(F.col("simulated").cast("int")).alias("simulated_rows"),
            F.countDistinct("_source_file").alias("landing_files"),
            F.min("event_time").alias("from"),
            F.max("event_time").alias("to"),
        )
    )

# COMMAND ----------

# MAGIC %md
# MAGIC What a reading looks like in Bronze: the document is untouched, Silver will parse it.

# COMMAND ----------

display(spark.table(f"{CATALOG}.bronze.domain_events").groupBy("kind").count().orderBy(F.desc("count")))
display(spark.table(f"{CATALOG}.bronze.sensor_readings").select("source_id", "event_time", "kind", "_source_file", "document").limit(5))
