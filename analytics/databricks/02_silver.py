# Databricks notebook source
# MAGIC %md
# MAGIC # 02 · Silver: parse, type, clean and split the raw documents
# MAGIC
# MAGIC Bronze keeps each document as JSON text. Silver turns it into **typed tables, one per business concept**,
# MAGIC ready to be joined and aggregated:
# MAGIC
# MAGIC | Silver table | From | One row per |
# MAGIC |---|---|---|
# MAGIC | `sensor_readings` | `bronze.sensor_readings` | valid reading |
# MAGIC | `sensor_readings_rejected` | `bronze.sensor_readings` | reading that failed a quality rule, with the reason |
# MAGIC | `sensor_alerts` | `sensor.alert.raised` | alert, with the rain forecast it came with |
# MAGIC | `orders` | `order.created` | order |
# MAGIC | `order_items` | `order.created` (its `items` array) | line of an order |
# MAGIC | `order_status_changes` | `order.status.changed` | status change |
# MAGIC | `order_cancellations` | `order.cancelled` | cancellation |
# MAGIC | `reviews` | `review.created` | review |
# MAGIC | `account_events` | `account.*` | account event (pseudonymised: no email, only its hash) |
# MAGIC | `farm_snapshots` | `bronze.catalog_snapshots` | farm on a snapshot day |
# MAGIC | `product_snapshots` | `bronze.catalog_snapshots` | product on a snapshot day (price, stock) |
# MAGIC | `farms` | `farm_snapshots` | farm, as in its latest snapshot |
# MAGIC
# MAGIC **Two ways of writing:**
# MAGIC
# MAGIC - The **sensor readings**, by far the largest table, are written **incrementally**: each run parses only the
# MAGIC   Bronze rows that arrived since the previous run and `MERGE`s them in, so nothing is reprocessed or duplicated.
# MAGIC - The other tables are **rebuilt from Bronze on every run** (overwrite). They are small, and a rebuild keeps them
# MAGIC   simple and idempotent: changing a rule here and running again fixes the whole history.
# MAGIC
# MAGIC The `full_refresh` widget rebuilds the readings from scratch too (after changing a quality rule, for example).

# COMMAND ----------

dbutils.widgets.text("catalog", "cropconnect")
dbutils.widgets.dropdown("full_refresh", "false", ["false", "true"])
CATALOG = dbutils.widgets.get("catalog")
FULL_REFRESH = dbutils.widgets.get("full_refresh") == "true"
spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.silver")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Helpers
# MAGIC
# MAGIC - **Times.** The services are .NET and write times with 7 decimal places (`2026-11-10T00:46:40.8725754Z`);
# MAGIC   Spark keeps microseconds (6), so the 7th digit is dropped before parsing. `try_to_timestamp` gives null
# MAGIC   instead of failing on a bad value (the serverless runtime is in ANSI mode, where a bad cast is an error).
# MAGIC - **MongoDB dates.** In Bronze they are Extended JSON: `{"$date": "2026-05-09T17:00:00Z"}`.
# MAGIC - **Money.** The catalogue snapshots come from a gRPC API, where a price is `{"units": "2", "nanos": 500000000}`
# MAGIC   (2,50 €).

# COMMAND ----------

from pyspark.sql import DataFrame, Window
from pyspark.sql import functions as F
from pyspark.sql.types import (
    ArrayType,
    BooleanType,
    DoubleType,
    IntegerType,
    LongType,
    StringType,
    StructField,
    StructType,
)


def to_ts(column):
    """ISO 8601 text (up to 7 decimals, with Z or an offset) to a timestamp; null when it is not a date."""
    trimmed = F.regexp_replace(column, r"(\.\d{6})\d+", "$1")
    return F.call_function("try_to_timestamp", trimmed)


def mongo_date(column):
    """Extended JSON date ({"$date": "…"}) to a timestamp."""
    return to_ts(column.getField("$date"))


def money(column):
    """Protobuf Money ({"units": "2", "nanos": 500000000}) to a decimal number of euros."""
    units = F.coalesce(column.getField("units").cast("double"), F.lit(0.0))
    nanos = F.coalesce(column.getField("nanos").cast("double"), F.lit(0.0))
    return (units + nanos / 1e9).cast("decimal(10,2)")


MONGO_DATE = StructType([StructField("$date", StringType())])


def bronze(name: str) -> DataFrame:
    return spark.table(f"{CATALOG}.bronze.{name}")


def save(df: DataFrame, name: str) -> None:
    (
        df.withColumn("_processed_at", F.current_timestamp())
        .write.format("delta")
        .mode("overwrite")
        .option("overwriteSchema", "true")
        .saveAsTable(f"{CATALOG}.silver.{name}")
    )
    print(f"silver.{name}: {spark.table(f'{CATALOG}.silver.{name}').count()} rows")


# COMMAND ----------

# MAGIC %md
# MAGIC ## Sensor readings
# MAGIC
# MAGIC The reading's time is the `timestamp` the ingestor stored in MongoDB: the sensor's own time when it was valid,
# MAGIC otherwise the time it arrived (`timestampSource` says which). Quality rules, each with a reason, so rejected
# MAGIC readings can be counted and explained instead of silently disappearing:
# MAGIC
# MAGIC | Rule | Reason |
# MAGIC |---|---|
# MAGIC | value present and numeric | `missing_value` |
# MAGIC | humidity between 0 and 100 % | `humidity_out_of_range` |
# MAGIC | temperature between −50 and 70 °C (the limits the simulator and the sensors use) | `temperature_out_of_range` |
# MAGIC | known sensor type | `unknown_type` |
# MAGIC | a time | `missing_time` |

# COMMAND ----------

READING_SCHEMA = StructType(
    [
        StructField("timestamp", MONGO_DATE),
        StructField("timestampSource", StringType()),
        StructField("meta", StructType([StructField("farmId", StringType()), StructField("sensorId", StringType()), StructField("type", StringType())])),
        StructField("receivedAt", MONGO_DATE),
        StructField("payload", StructType([StructField("value", DoubleType())])),
    ]
)


def parse_readings(raw: DataFrame) -> DataFrame:
    doc = F.from_json("document", READING_SCHEMA)
    return (
        raw.select("source_id", "simulated", doc.alias("d"))
        .select(
            F.col("source_id").alias("reading_id"),
            F.col("d.meta.farmId").alias("farm_id"),
            F.col("d.meta.sensorId").alias("sensor_id"),
            F.lower("d.meta.type").alias("sensor_type"),
            F.col("d.payload.value").alias("value"),
            mongo_date(F.col("d.timestamp")).alias("reading_time"),
            (F.coalesce(F.col("d.timestampSource"), F.lit("payload")) == "payload").alias("time_from_sensor"),
            mongo_date(F.col("d.receivedAt")).alias("received_at"),
            "simulated",
        )
        .withColumn("unit", F.when(F.col("sensor_type") == "humidity", "%").when(F.col("sensor_type") == "temperature", "°C"))
        .withColumn("delay_seconds", F.col("received_at").cast("long") - F.col("reading_time").cast("long"))
        .withColumn(
            "rejection_reason",
            F.when(F.col("value").isNull(), "missing_value")
            .when(~F.col("sensor_type").isin("humidity", "temperature"), "unknown_type")
            .when((F.col("sensor_type") == "humidity") & ~F.col("value").between(0, 100), "humidity_out_of_range")
            .when((F.col("sensor_type") == "temperature") & ~F.col("value").between(-50, 70), "temperature_out_of_range")
            .when(F.col("reading_time").isNull(), "missing_time"),
        )
        .dropDuplicates(["reading_id"])
    )


# COMMAND ----------

# MAGIC %md
# MAGIC ### Incremental write
# MAGIC
# MAGIC - The **watermark** is the latest Bronze `_ingested_at` already processed, kept in `silver._processing_state`.
# MAGIC   A run reads only the Bronze rows ingested after it.
# MAGIC - The new readings are **merged** on `reading_id`: a reading already in Silver is never inserted twice, so running
# MAGIC   the notebook again, or reading a row twice, changes nothing.
# MAGIC - The watermark only moves after both tables were written. If a run fails halfway, the next one repeats the
# MAGIC   same rows and the `MERGE` skips the ones already there.
# MAGIC - If either table **does not exist** (first run, or someone dropped it) or `full_refresh` is `true`, the watermark
# MAGIC   is ignored and every Bronze reading is processed again.

# COMMAND ----------

READINGS_TABLE = f"{CATALOG}.silver.sensor_readings"
REJECTED_TABLE = f"{CATALOG}.silver.sensor_readings_rejected"
STATE_TABLE = f"{CATALOG}.silver._processing_state"
STATE_KEY = "bronze.sensor_readings"
spark.sql(f"CREATE TABLE IF NOT EXISTS {STATE_TABLE} (source STRING, watermark TIMESTAMP, updated_at TIMESTAMP) USING DELTA")


def processed_until(source: str):
    row = spark.table(STATE_TABLE).filter(F.col("source") == source).select("watermark").first()
    return row[0] if row else None


def save_processed_until(source: str, value) -> None:
    if value is None:
        return
    spark.createDataFrame([(source, value)], "source string, watermark timestamp").createOrReplaceTempView("new_state")
    spark.sql(
        f"""
        MERGE INTO {STATE_TABLE} AS target
        USING new_state AS source
        ON target.source = source.source
        WHEN MATCHED THEN UPDATE SET watermark = source.watermark, updated_at = current_timestamp()
        WHEN NOT MATCHED THEN INSERT (source, watermark, updated_at) VALUES (source.source, source.watermark, current_timestamp())
        """
    )


def merge_into(df: DataFrame, table: str, key: str) -> None:
    """Inserts the rows whose key is not in the table yet; creates the table on the first run."""
    df = df.withColumn("_processed_at", F.current_timestamp())
    if not spark.catalog.tableExists(table):
        df.write.format("delta").saveAsTable(table)
        return
    df.createOrReplaceTempView("silver_batch")
    spark.sql(f"MERGE INTO {table} AS target USING silver_batch AS source ON target.{key} = source.{key} WHEN NOT MATCHED THEN INSERT *")


if FULL_REFRESH:
    spark.sql(f"DROP TABLE IF EXISTS {READINGS_TABLE}")
    spark.sql(f"DROP TABLE IF EXISTS {REJECTED_TABLE}")

tables_exist = spark.catalog.tableExists(READINGS_TABLE) and spark.catalog.tableExists(REJECTED_TABLE)
since = processed_until(STATE_KEY) if tables_exist else None

new_bronze = bronze("sensor_readings")
if since is not None:
    new_bronze = new_bronze.filter(F.col("_ingested_at") > F.lit(since))
newest = new_bronze.agg(F.max("_ingested_at")).first()[0]

readings = parse_readings(new_bronze).cache()
new_rows = readings.count()
if new_rows:
    merge_into(readings.filter(F.col("rejection_reason").isNull()).drop("rejection_reason"), READINGS_TABLE, "reading_id")
    merge_into(readings.filter(F.col("rejection_reason").isNotNull()), REJECTED_TABLE, "reading_id")
readings.unpersist()
save_processed_until(STATE_KEY, newest)

print(f"sensor readings: {new_rows} new Bronze rows processed since {since or 'the beginning'}")
print(f"silver.sensor_readings: {spark.table(READINGS_TABLE).count()} rows, "
      f"silver.sensor_readings_rejected: {spark.table(REJECTED_TABLE).count()} rows")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Domain events
# MAGIC
# MAGIC Every event has a `payload` with its own fields. One schema per event type reads just what that table
# MAGIC needs; `occurredAt` is when it happened in the service (the business time), `received_at` when the raw zone
# MAGIC got it.

# COMMAND ----------

EVENT_ENVELOPE = StructType([StructField("routingKey", StringType()), StructField("payload", StringType())])


def events(routing_key_pattern: str, payload_schema: StructType) -> DataFrame:
    """Events whose routing key matches (SQL LIKE), with the payload parsed into the column `p`."""
    raw = bronze("domain_events").filter(F.col("kind").like(routing_key_pattern))
    envelope = raw.select(
        F.col("source_id").alias("event_id"),
        F.col("kind").alias("routing_key"),
        F.col("event_time").alias("received_at"),
        "simulated",
        F.get_json_object("document", "$.payload").alias("payload_json"),
    )
    return envelope.withColumn("p", F.from_json("payload_json", payload_schema)).drop("payload_json")


def first_per(df: DataFrame, key: str, order: str) -> DataFrame:
    """Keeps one row per business key (the first by `order`): a replayed event never counts twice."""
    window = Window.partitionBy(key).orderBy(F.col(order).asc_nulls_last())
    return df.withColumn("_n", F.row_number().over(window)).filter("_n = 1").drop("_n")


# COMMAND ----------

ORDER_KEYS = [
    StructField("orderId", StringType()),
    StructField("customerId", StringType()),
    StructField("farmId", StringType()),
    StructField("farmOwnerId", StringType()),
    StructField("occurredAt", StringType()),
]
ITEM = StructType(
    [
        StructField("productId", StringType()),
        StructField("productName", StringType()),
        StructField("quantity", IntegerType()),
        StructField("unitPrice", DoubleType()),
    ]
)

created = events("order.created", StructType(ORDER_KEYS + [
    StructField("totalAmount", DoubleType()),
    StructField("itemCount", IntegerType()),
    StructField("items", ArrayType(ITEM)),
]))
created = first_per(created.withColumn("created_at", to_ts(F.col("p.occurredAt"))), "p.orderId", "created_at")

save(
    created.select(
        F.col("p.orderId").alias("order_id"),
        F.col("p.farmId").alias("farm_id"),
        F.col("p.customerId").alias("customer_id"),
        F.col("p.farmOwnerId").alias("farm_owner_id"),
        F.col("p.totalAmount").cast("decimal(10,2)").alias("total_amount"),
        F.col("p.itemCount").alias("item_count"),
        "created_at",
        "received_at",
        "simulated",
    ),
    "orders",
)

save(
    created.select(F.col("p.orderId").alias("order_id"), F.col("p.farmId").alias("farm_id"), "created_at", "simulated", F.explode("p.items").alias("i"))
    .select(
        "order_id",
        "farm_id",
        F.col("i.productId").alias("product_id"),
        F.col("i.productName").alias("product_name"),
        F.col("i.quantity").alias("quantity"),
        F.col("i.unitPrice").cast("decimal(10,2)").alias("unit_price"),
        (F.col("i.quantity") * F.col("i.unitPrice")).cast("decimal(10,2)").alias("line_total"),
        "created_at",
        "simulated",
    ),
    "order_items",
)

# COMMAND ----------

changes = events("order.status.changed", StructType(ORDER_KEYS + [
    StructField("previousStatus", StringType()),
    StructField("newStatus", StringType()),
]))
save(
    first_per(changes, "event_id", "received_at").select(
        "event_id",
        F.col("p.orderId").alias("order_id"),
        F.col("p.farmId").alias("farm_id"),
        F.lower("p.previousStatus").alias("previous_status"),
        F.lower("p.newStatus").alias("new_status"),
        to_ts(F.col("p.occurredAt")).alias("changed_at"),
        "simulated",
    ),
    "order_status_changes",
)

cancellations = events("order.cancelled", StructType(ORDER_KEYS + [
    StructField("previousStatus", StringType()),
    StructField("reason", StringType()),
    StructField("cancelledByCustomer", BooleanType()),
]))
save(
    first_per(cancellations.withColumn("cancelled_at", to_ts(F.col("p.occurredAt"))), "p.orderId", "cancelled_at").select(
        F.col("p.orderId").alias("order_id"),
        F.col("p.farmId").alias("farm_id"),
        F.col("p.customerId").alias("customer_id"),
        F.lower("p.previousStatus").alias("previous_status"),
        F.when(F.col("p.cancelledByCustomer"), "customer").otherwise("farm").alias("cancelled_by"),
        F.nullif(F.trim("p.reason"), F.lit("")).alias("reason"),
        "cancelled_at",
        "simulated",
    ),
    "order_cancellations",
)

# COMMAND ----------

reviews = events("review.created", StructType([
    StructField("reviewId", StringType()),
    StructField("orderId", StringType()),
    StructField("customerId", StringType()),
    StructField("farmId", StringType()),
    StructField("rating", IntegerType()),
    StructField("comment", StringType()),
    StructField("occurredAt", StringType()),
]))
save(
    first_per(reviews.withColumn("reviewed_at", to_ts(F.col("p.occurredAt"))), "p.reviewId", "reviewed_at")
    .filter(F.col("p.rating").between(1, 5))
    .select(
        F.col("p.reviewId").alias("review_id"),
        F.col("p.orderId").alias("order_id"),
        F.col("p.farmId").alias("farm_id"),
        F.col("p.customerId").alias("customer_id"),
        F.col("p.rating").alias("rating"),
        F.nullif(F.trim("p.comment"), F.lit("")).alias("comment"),
        "reviewed_at",
        "simulated",
    ),
    "reviews",
)

# COMMAND ----------

# MAGIC %md
# MAGIC ### Sensor alerts
# MAGIC
# MAGIC The Sensors service adds Open-Meteo's rain forecast to low-humidity alerts as a sentence. Silver turns it into
# MAGIC columns (rain expected, millimetres, probability), so Gold can ask whether alerts with dry forecasts differ
# MAGIC from the others. Two wordings exist ("Rain forecast…, up to 100% chance" from the service and "Rain is
# MAGIC forecast…, 10% chance" from the backfill); both are read.

# COMMAND ----------

alerts = events("sensor.alert.raised", StructType([
    StructField("alertId", StringType()),
    StructField("farmId", StringType()),
    StructField("farmName", StringType()),
    StructField("type", StringType()),
    StructField("value", DoubleType()),
    StructField("minValue", DoubleType()),
    StructField("maxValue", DoubleType()),
    StructField("unit", StringType()),
    StructField("weatherContext", StringType()),
    StructField("occurredAt", StringType()),
]))
weather = F.col("p.weatherContext")
save(
    first_per(alerts.withColumn("raised_at", to_ts(F.col("p.occurredAt"))), "p.alertId", "raised_at").select(
        F.col("p.alertId").alias("alert_id"),
        F.col("p.farmId").alias("farm_id"),
        F.col("p.farmName").alias("farm_name"),
        F.lower("p.type").alias("sensor_type"),
        F.col("p.value").alias("value"),
        F.col("p.minValue").alias("min_value"),
        F.col("p.maxValue").alias("max_value"),
        F.col("p.unit").alias("unit"),
        F.when(F.col("p.value") < F.col("p.minValue"), "below").otherwise("above").alias("direction"),
        F.when(weather.startswith("No significant rain"), False).when(weather.startswith("Rain"), True).alias("rain_expected"),
        F.regexp_extract(weather, r"\(([\d.]+) mm", 1).cast("double").alias("rain_mm"),
        F.nullif(F.regexp_extract(weather, r"(\d+)% chance", 1), F.lit("")).cast("int").alias("rain_probability"),
        "raised_at",
        "simulated",
    ),
    "sensor_alerts",
)

# COMMAND ----------

accounts = events("account.%", StructType([
    StructField("userId", StringType()),
    StructField("emailHash", StringType()),
    StructField("role", StringType()),
    StructField("occurredAt", StringType()),
]))
save(
    first_per(accounts, "event_id", "received_at").select(
        "event_id",
        F.col("p.userId").alias("user_id"),
        F.col("p.emailHash").alias("email_hash"),
        F.col("p.role").alias("role"),
        F.regexp_replace("routing_key", r"^account\.", "").alias("event"),
        to_ts(F.col("p.occurredAt")).alias("occurred_at"),
        "simulated",
    ),
    "account_events",
)

# COMMAND ----------

# MAGIC %md
# MAGIC ## Catalogue snapshots
# MAGIC
# MAGIC Once a day the ingestor stores every active farm and product. Kept per day, they show how prices, stock and
# MAGIC availability change over time (a slowly changing dimension); `farms` keeps each farm as in its latest snapshot,
# MAGIC to give Gold the farm names, locations and coordinates.

# COMMAND ----------

SNAPSHOT_ENVELOPE = StructType([
    StructField("snapshotId", StringType()),
    StructField("snapshotAt", MONGO_DATE),
    StructField("entity", StringType()),
    StructField("entityId", StringType()),
    StructField("data", StringType()),
])
snapshots = (
    bronze("catalog_snapshots")
    .select("simulated", F.from_json("document", SNAPSHOT_ENVELOPE).alias("s"))
    .select(
        "simulated",
        F.col("s.snapshotId").alias("snapshot_id"),
        mongo_date(F.col("s.snapshotAt")).alias("snapshot_at"),
        F.col("s.entity").alias("entity"),
        F.col("s.entityId").alias("entity_id"),
        F.col("s.data").alias("data"),
    )
    .withColumn("snapshot_date", F.to_date("snapshot_at"))
)

FARM = StructType([
    StructField("name", StringType()),
    StructField("location", StringType()),
    StructField("ownerId", StringType()),
    StructField("isActive", BooleanType()),
    StructField("latitude", DoubleType()),
    StructField("longitude", DoubleType()),
])
farm_snapshots = first_per(
    snapshots.filter("entity = 'farm'").withColumn("f", F.from_json("data", FARM)).withColumn("_key", F.concat_ws(":", "entity_id", "snapshot_date")),
    "_key",
    "snapshot_at",
).select(
    F.col("entity_id").alias("farm_id"),
    "snapshot_date",
    "snapshot_at",
    F.col("f.name").alias("farm_name"),
    F.col("f.location").alias("location"),
    F.col("f.ownerId").alias("owner_id"),
    F.coalesce(F.col("f.isActive"), F.lit(True)).alias("is_active"),
    F.col("f.latitude").alias("latitude"),
    F.col("f.longitude").alias("longitude"),
    "simulated",
)
save(farm_snapshots, "farm_snapshots")

latest = Window.partitionBy("farm_id").orderBy(F.col("snapshot_at").desc())
save(
    spark.table(f"{CATALOG}.silver.farm_snapshots").withColumn("_n", F.row_number().over(latest)).filter("_n = 1").drop("_n", "_processed_at", "snapshot_date").withColumnRenamed("snapshot_at", "last_seen_at"),
    "farms",
)

PRODUCT = StructType([
    StructField("farmId", StringType()),
    StructField("name", StringType()),
    StructField("unit", StringType()),
    StructField("price", StructType([StructField("units", StringType()), StructField("nanos", LongType())])),
    StructField("stockQuantity", IntegerType()),
    StructField("isAvailable", BooleanType()),
    StructField("type", StringType()),
])
product_snapshots = first_per(
    snapshots.filter("entity = 'product'").withColumn("p", F.from_json("data", PRODUCT)).withColumn("_key", F.concat_ws(":", "entity_id", "snapshot_date")),
    "_key",
    "snapshot_at",
).select(
    F.col("entity_id").alias("product_id"),
    F.col("p.farmId").alias("farm_id"),
    "snapshot_date",
    "snapshot_at",
    F.col("p.name").alias("product_name"),
    F.col("p.unit").alias("unit"),
    money(F.col("p.price")).alias("price"),
    F.coalesce(F.col("p.stockQuantity"), F.lit(0)).alias("stock_quantity"),
    F.coalesce(F.col("p.isAvailable"), F.lit(True)).alias("is_available"),
    F.lower(F.regexp_replace("p.type", r"^PRODUCT_TYPE_", "")).alias("product_type"),
    "simulated",
)
save(product_snapshots, "product_snapshots")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Checks
# MAGIC
# MAGIC Quick evidence for the defence: rows per table, the readings rejected and why, and that every order line
# MAGIC adds up to its order's total.

# COMMAND ----------

tables = [
    "sensor_readings", "sensor_readings_rejected", "sensor_alerts", "orders", "order_items", "order_status_changes",
    "order_cancellations", "reviews", "account_events", "farm_snapshots", "product_snapshots", "farms",
]
display(spark.createDataFrame([(t, spark.table(f"{CATALOG}.silver.{t}").count()) for t in tables], "table string, rows long"))

display(spark.table(f"{CATALOG}.silver.sensor_readings_rejected").groupBy("rejection_reason").count())

items_total = spark.table(f"{CATALOG}.silver.order_items").groupBy("order_id").agg(F.sum("line_total").alias("items_total"))
display(
    spark.table(f"{CATALOG}.silver.orders")
    .join(items_total, "order_id")
    .agg(
        F.count("*").alias("orders"),
        F.sum((F.abs(F.col("total_amount") - F.col("items_total")) > 0.01).cast("int")).alias("orders_whose_lines_do_not_add_up"),
    )
)
