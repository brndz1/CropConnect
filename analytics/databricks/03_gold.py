# Databricks notebook source
# MAGIC %md
# MAGIC # 03 · Gold: answers for the business
# MAGIC
# MAGIC Silver has clean facts; Gold aggregates them into the tables the dashboard reads, each answering a question
# MAGIC the services alone cannot (they keep only the current state):
# MAGIC
# MAGIC | Gold table | Question |
# MAGIC |---|---|
# MAGIC | `farm_daily_sensors` | How did each farm's humidity and temperature evolve, and how much time was out of range? |
# MAGIC | `order_lifecycle` | How long does an order wait in each state, and how does it end? |
# MAGIC | `farm_daily_sales` | How much does each farm sell per day, and how many orders are cancelled? |
# MAGIC | `product_monthly_sales` | Which products sell most, month by month? |
# MAGIC | `customer_growth` | How many new clients join, and how many buy, each month? |
# MAGIC | `drought_vs_sales` | Do sales change on dry days (low soil humidity)? |
# MAGIC | `alert_forecasts` | How do low-humidity alerts split between dry and rainy forecasts, per farm and month? |
# MAGIC | `farm_scorecard` | One line per farm: sales, cancellations, rating, response time, alerts, time out of range. |
# MAGIC | `fact_order_lines` + `dim_farm`, `dim_product`, `dim_date` | Star schema of the sales, for questions not foreseen above. |
# MAGIC
# MAGIC Gold is rebuilt on every run: its tables are small aggregates, and a dropped table comes back on the next run. Divisions use `try_divide`: the serverless runtime is in ANSI
# MAGIC mode, where dividing by zero (a day whose orders were all cancelled) is an error instead of null.

# COMMAND ----------

# MAGIC %md
# MAGIC ## Settings
# MAGIC
# MAGIC - The **normal ranges** are the administrator's global thresholds (A20 in the app). The raw zone has no copy of
# MAGIC   them, so they are widgets here; the defaults are the ones the simulated history used.
# MAGIC - The platform's **tests** create farms (`Quinta E2E …` by the end-to-end tests, `Quinta Postman …` by the
# MAGIC   Postman collection, `Quinta da Pesquisa` by the search tests); they are test data, not farms, so the names
# MAGIC   starting with these comma-separated prefixes are left out (empty keeps everything).

# COMMAND ----------

dbutils.widgets.text("catalog", "cropconnect")
dbutils.widgets.text("humidity_min", "30")
dbutils.widgets.text("humidity_max", "80")
dbutils.widgets.text("temperature_min", "5")
dbutils.widgets.text("temperature_max", "35")
dbutils.widgets.text("exclude_farm_prefixes", "Quinta E2E,Quinta Postman,Quinta da Pesquisa")

CATALOG = dbutils.widgets.get("catalog")
LIMITS = {
    "humidity": (float(dbutils.widgets.get("humidity_min")), float(dbutils.widgets.get("humidity_max"))),
    "temperature": (float(dbutils.widgets.get("temperature_min")), float(dbutils.widgets.get("temperature_max"))),
}
EXCLUDE_PREFIXES = [p.strip() for p in dbutils.widgets.get("exclude_farm_prefixes").split(",") if p.strip()]

spark.sql(f"CREATE SCHEMA IF NOT EXISTS {CATALOG}.gold")

# COMMAND ----------

from pyspark.sql import DataFrame, Window
from pyspark.sql import functions as F


def silver(name: str) -> DataFrame:
    return spark.table(f"{CATALOG}.silver.{name}")


def save(df: DataFrame, name: str) -> None:
    (
        df.withColumn("_built_at", F.current_timestamp())
        .write.format("delta")
        .mode("overwrite")
        .option("overwriteSchema", "true")
        .saveAsTable(f"{CATALOG}.gold.{name}")
    )
    print(f"gold.{name}: {spark.table(f'{CATALOG}.gold.{name}').count()} rows")


# The farms to analyse, with their names for the dashboard.
farms = silver("farms").select("farm_id", "farm_name", "location", "latitude", "longitude")
for prefix in EXCLUDE_PREFIXES:
    farms = farms.filter(~F.col("farm_name").startswith(prefix))
farms = farms.cache()


def with_farm(df: DataFrame) -> DataFrame:
    """Keeps only the analysed farms and adds their name."""
    return df.join(farms.select("farm_id", "farm_name"), "farm_id")


# COMMAND ----------

# MAGIC %md
# MAGIC ## Sensors per farm and day
# MAGIC
# MAGIC Average, minimum and maximum of each sensor type per day, and the share of readings outside the normal range
# MAGIC (a reading every 15 minutes, so 10 % out of range is about 2,4 hours that day).

# COMMAND ----------

limit_min = F.when(F.col("sensor_type") == "humidity", LIMITS["humidity"][0]).otherwise(LIMITS["temperature"][0])
limit_max = F.when(F.col("sensor_type") == "humidity", LIMITS["humidity"][1]).otherwise(LIMITS["temperature"][1])

farm_daily_sensors = (
    with_farm(silver("sensor_readings"))
    .withColumn("date", F.to_date("reading_time"))
    .withColumn("below", (F.col("value") < limit_min).cast("int"))
    .withColumn("above", (F.col("value") > limit_max).cast("int"))
    .groupBy("farm_id", "farm_name", "date", "sensor_type")
    .agg(
        F.count("*").alias("readings"),
        F.round(F.avg("value"), 2).alias("avg_value"),
        F.min("value").alias("min_value"),
        F.max("value").alias("max_value"),
        F.sum("below").alias("readings_below"),
        F.sum("above").alias("readings_above"),
    )
    .withColumn("share_out_of_range", F.round(F.try_divide(F.col("readings_below") + F.col("readings_above"), F.col("readings")), 4))
    .withColumn("limit_min", limit_min)
    .withColumn("limit_max", limit_max)
)
save(farm_daily_sensors, "farm_daily_sensors")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Order life cycle
# MAGIC
# MAGIC One row per order with the time it reached each state, read from its status changes:
# MAGIC Pendente → Confirmada → Pronta a levantar → Levantada, or Cancelada. The waits are in hours.

# COMMAND ----------

changes = silver("order_status_changes")
reached = changes.groupBy("order_id").agg(
    F.min(F.when(F.col("new_status") == "confirmed", F.col("changed_at"))).alias("confirmed_at"),
    F.min(F.when(F.col("new_status") == "readyforpickup", F.col("changed_at"))).alias("ready_at"),
    F.min(F.when(F.col("new_status") == "pickedup", F.col("changed_at"))).alias("picked_up_at"),
)
cancelled = silver("order_cancellations").select("order_id", "cancelled_at", "cancelled_by", F.col("previous_status").alias("cancelled_from"))


def hours(start: str, end: str):
    return F.round(F.try_divide(F.col(end).cast("long") - F.col(start).cast("long"), F.lit(3600.0)), 2)


order_lifecycle = (
    with_farm(silver("orders").select("order_id", "farm_id", "customer_id", "total_amount", "item_count", "created_at", "simulated"))
    .join(reached, "order_id", "left")
    .join(cancelled, "order_id", "left")
    .withColumn(
        "final_status",
        F.when(F.col("cancelled_at").isNotNull(), "cancelled")
        .when(F.col("picked_up_at").isNotNull(), "picked_up")
        .when(F.col("ready_at").isNotNull(), "ready_for_pickup")
        .when(F.col("confirmed_at").isNotNull(), "confirmed")
        .otherwise("pending"),
    )
    .withColumn("hours_to_confirm", hours("created_at", "confirmed_at"))
    .withColumn("hours_confirmed_to_ready", hours("confirmed_at", "ready_at"))
    .withColumn("hours_ready_to_pickup", hours("ready_at", "picked_up_at"))
    .withColumn("hours_total", hours("created_at", "picked_up_at"))
)
save(order_lifecycle, "order_lifecycle")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Sales per farm and day
# MAGIC
# MAGIC Revenue counts the orders that were not cancelled (in CropConnect the client pays at pickup, so a cancelled
# MAGIC order brings nothing).

# COMMAND ----------

lifecycle = spark.table(f"{CATALOG}.gold.order_lifecycle")
farm_daily_sales = (
    lifecycle.withColumn("date", F.to_date("created_at"))
    .withColumn("is_cancelled", (F.col("final_status") == "cancelled").cast("int"))
    .groupBy("farm_id", "farm_name", "date")
    .agg(
        F.count("*").alias("orders"),
        F.sum("is_cancelled").alias("cancelled_orders"),
        F.sum(F.when(F.col("is_cancelled") == 0, F.col("total_amount")).otherwise(0)).cast("decimal(12,2)").alias("revenue"),
        F.sum(F.when(F.col("is_cancelled") == 0, F.col("item_count")).otherwise(0)).alias("items"),
        F.countDistinct("customer_id").alias("customers"),
    )
    .withColumn("avg_order_value", F.round(F.try_divide(F.col("revenue"), F.col("orders") - F.col("cancelled_orders")), 2))
    .withColumn("cancellation_rate", F.round(F.try_divide(F.col("cancelled_orders"), F.col("orders")), 4))
)
save(farm_daily_sales, "farm_daily_sales")

# COMMAND ----------

not_cancelled = lifecycle.filter("final_status <> 'cancelled'").select("order_id")
product_monthly_sales = (
    with_farm(silver("order_items"))
    .join(not_cancelled, "order_id")
    .withColumn("month", F.date_trunc("month", "created_at").cast("date"))
    .groupBy("month", "farm_id", "farm_name", "product_name")
    .agg(
        F.sum("quantity").alias("units_sold"),
        F.sum("line_total").cast("decimal(12,2)").alias("revenue"),
        F.countDistinct("order_id").alias("orders"),
        F.round(F.avg("unit_price"), 2).alias("avg_unit_price"),
    )
)
save(product_monthly_sales, "product_monthly_sales")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Clients
# MAGIC
# MAGIC A new client is a client account whose email was confirmed that month (the accounts are pseudonymised, so they
# MAGIC are counted by id, never by email). An active client placed at least one order that month.

# COMMAND ----------

new_clients = (
    silver("account_events")
    .filter((F.col("event") == "email.verified") & (F.col("role") == "Client"))
    .groupBy(F.date_trunc("month", "occurred_at").cast("date").alias("month"))
    .agg(F.countDistinct("user_id").alias("new_clients"))
)
active_clients = (
    lifecycle.groupBy(F.date_trunc("month", "created_at").cast("date").alias("month"))
    .agg(F.countDistinct("customer_id").alias("active_clients"), F.count("*").alias("orders"))
)
customer_growth = (
    new_clients.join(active_clients, "month", "full")
    .fillna(0, ["new_clients", "active_clients", "orders"])
    # Running total of the clients who joined up to each month (one partition: there are only a few months).
    .withColumn("total_clients", F.sum("new_clients").over(Window.orderBy("month").rowsBetween(Window.unboundedPreceding, 0)))
    .orderBy("month")
)
save(customer_growth, "customer_growth")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Drought and sales
# MAGIC
# MAGIC A **dry day** for a farm is a day whose average soil humidity was below the minimum. Joining the daily
# MAGIC humidity with the daily sales of the same farm shows whether dry days sell less (fewer products to offer) or
# MAGIC more. `drought_vs_sales` keeps one row per farm and day; the summary cell compares the averages.

# COMMAND ----------

humidity_days = (
    spark.table(f"{CATALOG}.gold.farm_daily_sensors")
    .filter("sensor_type = 'humidity'")
    .select("farm_id", "farm_name", "date", F.col("avg_value").alias("avg_humidity"), "share_out_of_range")
    .withColumn("is_dry_day", F.col("avg_humidity") < LIMITS["humidity"][0])
)
drought_vs_sales = (
    humidity_days.join(
        spark.table(f"{CATALOG}.gold.farm_daily_sales").select("farm_id", "date", "orders", "revenue", "items"),
        ["farm_id", "date"],
        "left",
    )
    .fillna(0, ["orders", "items"])
    .withColumn("revenue", F.coalesce("revenue", F.lit(0)).cast("decimal(12,2)"))
)
save(drought_vs_sales, "drought_vs_sales")

display(
    spark.table(f"{CATALOG}.gold.drought_vs_sales")
    .groupBy("is_dry_day")
    .agg(
        F.count("*").alias("farm_days"),
        F.round(F.avg("avg_humidity"), 1).alias("avg_humidity"),
        F.round(F.avg("orders"), 2).alias("avg_orders_per_day"),
        F.round(F.avg("revenue"), 2).alias("avg_revenue_per_day"),
    )
)

# COMMAND ----------

alert_forecasts = (
    with_farm(silver("sensor_alerts").drop("farm_name"))
    .withColumn("month", F.date_trunc("month", "raised_at").cast("date"))
    .withColumn(
        "forecast",
        F.when(F.col("rain_expected"), "rain expected")
        .when(~F.col("rain_expected"), "no rain")
        .otherwise("no forecast"),
    )
    .groupBy("month", "farm_id", "farm_name", "sensor_type", "direction", "forecast")
    .agg(F.count("*").alias("alerts"), F.round(F.avg("value"), 1).alias("avg_value"), F.round(F.avg("rain_mm"), 1).alias("avg_rain_mm"))
)
save(alert_forecasts, "alert_forecasts")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Farm scorecard
# MAGIC
# MAGIC One line per farm with the indicators a manager or the platform would look at first.

# COMMAND ----------

sales = lifecycle.groupBy("farm_id").agg(
    F.count("*").alias("orders"),
    F.sum(F.when(F.col("final_status") != "cancelled", F.col("total_amount")).otherwise(0)).cast("decimal(12,2)").alias("revenue"),
    F.round(F.avg((F.col("final_status") == "cancelled").cast("int")), 4).alias("cancellation_rate"),
    F.round(F.avg("hours_to_confirm"), 2).alias("avg_hours_to_confirm"),
    F.round(F.avg("hours_total"), 2).alias("avg_hours_to_pickup"),
)
ratings = silver("reviews").groupBy("farm_id").agg(F.round(F.avg("rating"), 2).alias("avg_rating"), F.count("*").alias("reviews"))
alerts = silver("sensor_alerts").groupBy("farm_id").agg(F.count("*").alias("alerts"))
out_of_range = (
    spark.table(f"{CATALOG}.gold.farm_daily_sensors")
    .groupBy("farm_id")
    .agg(F.round(F.try_divide(F.sum(F.col("readings_below") + F.col("readings_above")), F.sum("readings")), 4).alias("share_readings_out_of_range"))
)
farm_scorecard = (
    farms.join(sales, "farm_id", "left")
    .join(ratings, "farm_id", "left")
    .join(alerts, "farm_id", "left")
    .join(out_of_range, "farm_id", "left")
    .fillna(0, ["orders", "reviews", "alerts"])
    .orderBy(F.desc("revenue"))
)
save(farm_scorecard, "farm_scorecard")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Star schema: sales
# MAGIC
# MAGIC The tables above are aggregates built for the dashboard. Underneath them, the sales are also kept as a
# MAGIC **dimensional model** (Kimball), so any new question can be answered with joins and a `GROUP BY`, without a new
# MAGIC table:
# MAGIC
# MAGIC | Table | Kind | One row per |
# MAGIC |---|---|---|
# MAGIC | `fact_order_lines` | **fact table** (transaction grain) | line of an order |
# MAGIC | `dim_farm` | dimension | farm (latest snapshot) |
# MAGIC | `dim_product` | dimension | product (latest snapshot, current price) |
# MAGIC | `dim_date` | dimension | calendar day |
# MAGIC
# MAGIC The facts are the measures (quantity, unit price, line total) and the keys to the dimensions (`farm_id`,
# MAGIC `product_id`, `date_key`). `order_lifecycle` is a fact table too, of the kind Kimball calls an *accumulating
# MAGIC snapshot*: one row per order, with a date column for each step.

# COMMAND ----------

dim_farm = farms.join(silver("farms").select("farm_id", "owner_id", "is_active", "last_seen_at"), "farm_id")
save(dim_farm, "dim_farm")

# Each product as in its latest snapshot; a product that was sold but never photographed still gets a row,
# so every fact has a product to join to.
latest_product = Window.partitionBy("product_id").orderBy(F.col("snapshot_at").desc())
photographed = (
    silver("product_snapshots")
    .withColumn("_n", F.row_number().over(latest_product))
    .filter("_n = 1")
    .select(
        "product_id", "product_name", "farm_id", "unit", "product_type",
        F.col("price").alias("current_price"), "is_available", F.col("snapshot_at").alias("last_seen_at"),
    )
)
only_sold = (
    silver("order_items")
    .groupBy("product_id")
    .agg(F.first("product_name").alias("product_name"), F.first("farm_id").alias("farm_id"))
    .join(photographed.select("product_id"), "product_id", "left_anti")
)
dim_product = photographed.unionByName(only_sold, allowMissingColumns=True).join(farms.select("farm_id"), "farm_id")
save(dim_product, "dim_product")

# One row per day between the first and the last order or reading.
bounds = (
    silver("orders").select(F.to_date("created_at").alias("d"))
    .unionByName(silver("sensor_readings").select(F.to_date("reading_time").alias("d")))
    .agg(F.min("d").alias("first"), F.max("d").alias("last"))
    .first()
)
iso_weekday = ((F.dayofweek("date") + 5) % 7) + 1  # Monday = 1 … Sunday = 7
dim_date = (
    spark.sql(f"SELECT explode(sequence(DATE'{bounds['first']}', DATE'{bounds['last']}', INTERVAL 1 DAY)) AS date")
    .select(
        F.date_format("date", "yyyyMMdd").cast("int").alias("date_key"),
        "date",
        F.year("date").alias("year"),
        F.quarter("date").alias("quarter"),
        F.month("date").alias("month"),
        F.date_format("date", "MMMM").alias("month_name"),
        F.dayofmonth("date").alias("day"),
        iso_weekday.alias("weekday"),
        F.date_format("date", "EEEE").alias("weekday_name"),
        iso_weekday.isin(6, 7).alias("is_weekend"),
    )
)
save(dim_date, "dim_date")

fact_order_lines = (
    silver("order_items")
    .join(spark.table(f"{CATALOG}.gold.order_lifecycle").select("order_id", "customer_id", "final_status"), "order_id")
    .select(
        "order_id",
        F.date_format("created_at", "yyyyMMdd").cast("int").alias("date_key"),
        "farm_id",
        "product_id",
        "customer_id",
        "quantity",
        "unit_price",
        "line_total",
        (F.col("final_status") == "cancelled").alias("is_cancelled"),
        "final_status",
        "created_at",
        "simulated",
    )
)
save(fact_order_lines, "fact_order_lines")

# COMMAND ----------

# MAGIC %md
# MAGIC ## Checks

# COMMAND ----------

gold_tables = [
    "farm_daily_sensors", "order_lifecycle", "farm_daily_sales", "product_monthly_sales", "customer_growth",
    "drought_vs_sales", "alert_forecasts", "farm_scorecard", "fact_order_lines", "dim_farm", "dim_product", "dim_date",
]
display(spark.createDataFrame([(t, spark.table(f"{CATALOG}.gold.{t}").count()) for t in gold_tables], "table string, rows long"))

# Every fact must find its farm, product and day (all three counts must be 0).
fact = spark.table(f"{CATALOG}.gold.fact_order_lines")
display(
    spark.createDataFrame(
        [
            ("farm", fact.join(spark.table(f"{CATALOG}.gold.dim_farm"), "farm_id", "left_anti").count()),
            ("product", fact.join(spark.table(f"{CATALOG}.gold.dim_product"), "product_id", "left_anti").count()),
            ("date", fact.join(spark.table(f"{CATALOG}.gold.dim_date"), "date_key", "left_anti").count()),
        ],
        "dimension string, facts_without_a_match long",
    )
)

# The star schema at work: revenue by weekday, weekends first.
display(
    spark.sql(
        f"""
        SELECT d.weekday_name, d.is_weekend, round(sum(f.line_total), 2) AS revenue, count(DISTINCT f.order_id) AS orders
        FROM {CATALOG}.gold.fact_order_lines f
        JOIN {CATALOG}.gold.dim_date d ON f.date_key = d.date_key
        WHERE NOT f.is_cancelled
        GROUP BY d.weekday, d.weekday_name, d.is_weekend
        ORDER BY d.weekday
        """
    )
)
display(spark.table(f"{CATALOG}.gold.order_lifecycle").groupBy("final_status").count().orderBy(F.desc("count")))
display(spark.table(f"{CATALOG}.gold.farm_scorecard"))
