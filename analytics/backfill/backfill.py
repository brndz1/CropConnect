"""Fills the raw zone (MongoDB, cropconnect_raw) with months of simulated history for the analytics platform.

The platform only has the data it saw since the analytics ingestor started, which is too little for the Gold layer
to show anything. This script writes what the ingestor would have stored over the last months: sensor readings,
alerts, orders and their status changes, reviews, account events and the daily catalog snapshots, with the same
document shapes. Every document it writes has "simulated": true, so the simulated history can always be told apart
from the real data, and --replace deletes it before writing it again.

    python -m pip install -r analytics/backfill/requirements.txt
    python analytics/backfill/backfill.py --replace

The history is built to have a story to find: some farms are drier than others and their droughts raise
alerts, orders drop on the days a farm is in a drought, Saturdays sell more, a few readings are invalid (for the
Silver layer's quarantine), prices change now and then, and some farms join or leave the platform along the way.
"""

import argparse
import hashlib
import hmac
import math
import random
import uuid
from datetime import datetime, timedelta, timezone

from pymongo import MongoClient
from pymongo.errors import CollectionInvalid

READINGS = "sensor_readings"
EVENTS = "domain_events"
SNAPSHOTS = "catalog_snapshots"

# The thresholds Sensors seeds (humidity 30-80 %, temperature 5-35 °C) and its 30-minute alert cooldown.
THRESHOLDS = {"humidity": (30.0, 80.0, "%"), "temperature": (5.0, 35.0, "°C")}
ALERT_COOLDOWN = timedelta(minutes=30)

# Mean temperature per month in mainland Portugal, roughly.
MONTHLY_TEMPERATURE = {1: 10, 2: 11, 3: 13, 4: 15, 5: 18, 6: 22, 7: 25, 8: 26, 9: 23, 10: 18, 11: 13, 12: 11}

FARM_NAMES = [
    "Quinta do Vale", "Horta da Ribeira", "Quinta das Oliveiras", "Herdade do Sobreiro", "Quinta da Fonte",
    "Horta do Moinho", "Quinta do Pinhal", "Herdade da Planície", "Quinta da Serra", "Horta Nova",
    "Quinta do Rio", "Quinta dos Castanheiros", "Horta da Avó", "Quinta da Ponte", "Herdade do Monte",
    "Quinta das Laranjeiras", "Horta do Lago", "Quinta da Encosta", "Quinta do Freixo", "Horta Biológica do Norte",
    "Quinta da Lezíria", "Herdade das Vinhas", "Quinta do Souto", "Horta da Aldeia", "Quinta das Amendoeiras",
    "Quinta do Carvalhal", "Horta Verde", "Quinta da Azenha",
]

LOCATIONS = [
    ("Braga", 41.55, -8.42), ("Viseu", 40.66, -7.91), ("Coimbra", 40.21, -8.43), ("Leiria", 39.74, -8.81),
    ("Santarém", 39.24, -8.69), ("Évora", 38.57, -7.91), ("Beja", 38.02, -7.86), ("Faro", 37.02, -7.93),
    ("Vila Real", 41.30, -7.74), ("Castelo Branco", 39.82, -7.49), ("Guarda", 40.54, -7.27), ("Setúbal", 38.52, -8.89),
    ("Portalegre", 39.29, -7.43), ("Aveiro", 40.64, -8.65), ("Bragança", 41.81, -6.76), ("Torres Vedras", 39.09, -9.26),
]

# (name, unit, base price in euros)
PRODUCTS = [
    ("Alface", "un", 0.9), ("Tomate", "kg", 2.4), ("Cenoura", "kg", 1.3), ("Batata", "kg", 1.1),
    ("Cebola", "kg", 1.2), ("Couve", "un", 1.5), ("Pimento", "kg", 3.2), ("Abóbora", "kg", 1.6),
    ("Morango", "kg", 6.5), ("Maçã", "kg", 1.9), ("Laranja", "kg", 1.4), ("Pêra", "kg", 2.1),
    ("Feijão-verde", "kg", 3.8), ("Curgete", "kg", 1.8), ("Espinafre", "kg", 4.2), ("Ovos", "dúzia", 3.5),
    ("Mel", "frasco", 7.0), ("Azeite", "litro", 9.5),
]

REVIEW_COMMENTS = [
    "Produtos muito frescos.", "Tudo impecável, recomendo.", "Bom, mas o levantamento atrasou um pouco.",
    "Alface com muito sabor.", "Voltarei a encomendar.", "Alguns produtos vieram pisados.",
    "Atendimento simpático.", "Preço justo para a qualidade.", None, None, None,
]

CANCEL_REASONS = ["Mudei de ideias", "Já não consigo levantar", "Encomendei por engano", "Sem stock suficiente", None]

WEATHER_CONTEXTS = [
    "No significant rain forecast in the next 24h ({mm} mm, up to {pct}% chance); consider irrigating.",
    "Rain is forecast in the next 24h ({mm} mm, {pct}% chance); irrigation may not be needed.",
]


class History:
    def __init__(self, rng, start, end, args):
        self.rng = rng
        self.start = start
        self.end = end
        self.args = args
        self.readings = []
        self.events = []
        self.snapshots = []
        self.farms = []
        self.customers = []

    # ---- helpers -------------------------------------------------------------------------------------------

    def new_id(self):
        return str(uuid.UUID(int=self.rng.getrandbits(128), version=4))

    def email_hash(self, email):
        key = self.args.pseudonymization_key.encode()
        return hmac.new(key, email.strip().lower().encode(), hashlib.sha256).hexdigest()

    @staticmethod
    def iso(moment):
        return moment.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"

    def add_event(self, routing_key, event_type, occurred_at, payload):
        if occurred_at >= self.end:
            return  # still in the future when the history ends, like an order not picked up yet
        payload["occurredAt"] = self.iso(occurred_at)
        self.events.append({
            "_id": self.new_id(),
            "routingKey": routing_key,
            "eventType": event_type,
            "publishedAt": occurred_at.replace(microsecond=0),
            "receivedAt": occurred_at + timedelta(milliseconds=self.rng.randint(20, 400)),
            "traceParent": None,
            "payload": payload,
            "simulated": True,
        })

    # ---- people ----------------------------------------------------------------------------------------------

    def register(self, role, at, verified_probability=0.92):
        user_id = self.new_id()
        email = f"{role.lower()}-{user_id[:8]}@example.com"
        base = {"userId": user_id, "emailHash": self.email_hash(email), "role": role}

        if role == "Manager":
            # An administrator creates the manager's account, and the manager sets the password from the link.
            self.add_event("account.invited", "AccountTokenMessage", at,
                           {**base, "expiresAt": self.iso(at + timedelta(days=3))})
            self.add_event("account.password.changed", "AccountMessage", at + timedelta(hours=self.rng.uniform(0.2, 30)), dict(base))
        else:
            self.add_event("account.email-verification.requested", "AccountTokenMessage", at,
                           {**base, "expiresAt": self.iso(at + timedelta(days=1))})
            if self.rng.random() < verified_probability:
                self.add_event("account.email.verified", "AccountMessage", at + timedelta(minutes=self.rng.uniform(1, 600)), dict(base))
        return base

    # ---- catalog ---------------------------------------------------------------------------------------------

    def build_farms(self):
        span = (self.end - self.start).total_seconds()
        names = self.rng.sample(FARM_NAMES, self.args.farms)

        for index, name in enumerate(names):
            # Most farms are there from the start; the others join along the way.
            joined = self.start - timedelta(days=self.rng.randint(30, 400)) if index < self.args.farms * 0.75 \
                else self.start + timedelta(seconds=self.rng.uniform(0.1, 0.8) * span)
            # Two farms leave the platform in the last two months.
            left = self.end - timedelta(days=self.rng.uniform(5, 60)) if index in (3, 11) else None
            city, lat, lon = self.rng.choice(LOCATIONS)
            owner = self.register("Manager", joined - timedelta(days=1))["userId"]

            products = []
            for product_name, unit, base in self.rng.sample(PRODUCTS, self.rng.randint(5, 10)):
                price = round(base * self.rng.uniform(0.85, 1.25), 2)
                # A few price changes over the period, each one kept from its day on.
                changes = sorted((self.start + timedelta(seconds=self.rng.uniform(0, span)),
                                  round(price * self.rng.uniform(0.85, 1.18), 2))
                                 for _ in range(self.rng.randint(0, 3)))
                products.append({
                    "id": self.new_id(), "name": product_name, "unit": unit, "price": price, "changes": changes,
                    "createdAt": max(joined, self.start - timedelta(days=20)) + timedelta(hours=self.rng.uniform(1, 48)),
                    "stock": self.rng.randint(5, 80),
                })

            self.farms.append({
                "id": self.new_id(), "ownerId": owner, "name": name, "location": city,
                "latitude": round(lat + self.rng.uniform(-0.15, 0.15), 4),
                "longitude": round(lon + self.rng.uniform(-0.15, 0.15), 4),
                "joined": joined, "left": left, "products": products, "sensorId": f"sim-{index + 1:02d}",
                # The story: dry farms have more droughts; popular ones sell more; quality drives the ratings.
                "dryness": self.rng.uniform(0, 1), "popularity": self.rng.uniform(0.5, 2.0),
                "quality": self.rng.uniform(3.4, 4.8), "droughts": [],
            })

    @staticmethod
    def active(farm, at):
        return farm["joined"] <= at and (farm["left"] is None or at < farm["left"])

    @staticmethod
    def price_at(product, at):
        price = product["price"]
        for changed_at, new_price in product["changes"]:
            if changed_at <= at:
                price = new_price
        return price

    @staticmethod
    def money(value):
        units = int(math.floor(value))
        return {"units": str(units), "nanos": int(round((value - units) * 1e9))}

    def take_snapshots(self):
        day = self.start.replace(hour=3, minute=0, second=0, microsecond=0)
        while day < self.end:
            snapshot_id = self.new_id()
            for farm in self.farms:
                if not self.active(farm, day):
                    continue
                self.snapshot(snapshot_id, day, "farm", farm["id"], {
                    "id": farm["id"], "ownerId": farm["ownerId"], "name": farm["name"],
                    "description": f"Produtos frescos de {farm['location']}.", "location": farm["location"],
                    "isActive": True, "createdAt": self.iso(farm["joined"]), "updatedAt": self.iso(farm["joined"]),
                    "version": 1, "pickupSchedules": [], "latitude": farm["latitude"], "longitude": farm["longitude"],
                })
                for product in farm["products"]:
                    if product["createdAt"] > day:
                        continue
                    product["stock"] = max(0, product["stock"] + self.rng.randint(-12, 15))
                    self.snapshot(snapshot_id, day, "product", product["id"], {
                        "id": product["id"], "farmId": farm["id"], "farmName": farm["name"], "name": product["name"],
                        "description": "", "unit": product["unit"], "price": self.money(self.price_at(product, day)),
                        "stockQuantity": product["stock"], "isAvailable": True,
                        "createdAt": self.iso(product["createdAt"]), "updatedAt": self.iso(product["createdAt"]),
                        "version": 1, "type": "PRODUCT_TYPE_PRODUCT",
                    })
            day += timedelta(days=1)

    def snapshot(self, snapshot_id, at, entity, entity_id, data):
        self.snapshots.append({"snapshotId": snapshot_id, "snapshotAt": at, "entity": entity,
                               "entityId": entity_id, "data": data, "simulated": True})

    # ---- sensors ---------------------------------------------------------------------------------------------

    def plan_droughts(self):
        for farm in self.farms:
            day = self.start
            while day < self.end:
                if self.rng.random() < 0.01 + 0.05 * farm["dryness"]:
                    length = timedelta(hours=self.rng.uniform(12, 60))
                    farm["droughts"].append((day + timedelta(hours=self.rng.uniform(0, 24)), length))
                    day += length
                day += timedelta(days=1)

    @staticmethod
    def in_drought(farm, at):
        return any(begin <= at < begin + length for begin, length in farm["droughts"])

    def simulate_sensors(self):
        step = timedelta(minutes=self.args.reading_interval)
        for farm in self.farms:
            humidity = 58 - 12 * farm["dryness"]
            last_alert = {"humidity": None, "temperature": None}
            at = max(self.start, farm["joined"])
            while at < self.end and (farm["left"] is None or at < farm["left"]):
                target = (self.rng.uniform(19, 27) if self.in_drought(farm, at)
                          else 58 - 12 * farm["dryness"] - 6 * (at.month in (7, 8)))
                humidity += (target - humidity) * 0.25 + self.rng.gauss(0, 1.6)
                humidity = min(100.0, max(0.0, humidity))

                hour = at.hour + at.minute / 60
                temperature = (MONTHLY_TEMPERATURE[at.month] + 1.5 * farm["dryness"]
                               + 5 * math.sin((hour - 9) / 24 * 2 * math.pi) + self.rng.gauss(0, 1.2))

                for sensor_type, value in (("humidity", humidity), ("temperature", temperature)):
                    self.reading(farm, sensor_type, round(value, 1), at, last_alert)
                at += step

    def reading(self, farm, sensor_type, value, at, last_alert):
        message_id = self.new_id()
        timestamp = self.iso(at)
        timestamp_source = "payload"

        # A few broken readings, for the Silver layer to quarantine.
        roll = self.rng.random()
        if roll < 0.0008:
            value = self.rng.choice([150.0, -999.0, 1000.0])
        elif roll < 0.0012:
            timestamp, timestamp_source = "not-a-date", "receivedAt"

        received_at = at + timedelta(milliseconds=self.rng.randint(5, 250))
        self.readings.append({
            "timestamp": at if timestamp_source == "payload" else received_at,
            "timestampSource": timestamp_source,
            "meta": {"farmId": farm["id"], "sensorId": farm["sensorId"], "type": sensor_type},
            "messageId": message_id,
            "routingKey": f"farm.{farm['id']}.{sensor_type}",
            "receivedAt": received_at,
            "traceParent": None,
            "payload": {"messageId": message_id, "farmId": farm["id"], "sensorId": farm["sensorId"],
                        "type": sensor_type, "value": value, "timestamp": timestamp},
            "simulated": True,
        })

        low, high, unit = THRESHOLDS[sensor_type]
        if low <= value <= high or value in (150.0, -999.0, 1000.0):
            return
        if last_alert[sensor_type] is not None and at - last_alert[sensor_type] < ALERT_COOLDOWN:
            return
        last_alert[sensor_type] = at

        side, limit = ("below the minimum", low) if value < low else ("above the maximum", high)
        weather = None
        if sensor_type == "humidity" and value < low:
            template = self.rng.choice(WEATHER_CONTEXTS)
            weather = template.format(mm=round(self.rng.uniform(0, 6), 1), pct=self.rng.randint(5, 90))
        self.add_event("sensor.alert.raised", "SensorAlertRaised", at + timedelta(milliseconds=300), {
            "alertId": self.new_id(), "farmId": farm["id"], "farmOwnerId": farm["ownerId"], "farmName": farm["name"],
            "type": sensor_type, "value": value, "minValue": low, "maxValue": high, "unit": unit,
            "description": f"{sensor_type.capitalize()} of {value}{unit} from sensor {farm['sensorId']} is {side} of {limit}{unit}.",
            "weatherContext": weather,
        })

    # ---- orders ----------------------------------------------------------------------------------------------

    def simulate_customers(self):
        # Customers who were already there (their registration is older than the period), then a few new ones a day.
        for _ in range(self.args.initial_customers):
            self.customers.append((self.register("Client", self.start - timedelta(days=self.rng.uniform(1, 300))),
                                   self.start - timedelta(days=1)))
        day = self.start
        while day < self.end:
            for _ in range(self.poisson(self.args.new_customers_per_day)):
                at = day + timedelta(hours=self.rng.uniform(7, 23))
                self.customers.append((self.register("Client", at), at))
            day += timedelta(days=1)

    def poisson(self, mean):
        # Knuth's method; the means here are small.
        limit, count, product = math.exp(-mean), 0, self.rng.random()
        while product > limit:
            count += 1
            product *= self.rng.random()
        return count

    def simulate_orders(self):
        day = self.start.replace(hour=0, minute=0, second=0, microsecond=0)
        total_days = (self.end - self.start).days or 1
        while day < self.end:
            weekend = {5: 1.5, 6: 1.15}.get(day.weekday(), 1.0)
            growth = 1 + 0.6 * (day - self.start).days / total_days
            for farm in self.farms:
                noon = day + timedelta(hours=12)
                if not self.active(farm, noon):
                    continue
                drought = 0.55 if self.in_drought(farm, noon) else 1.0
                mean = self.args.orders_per_farm_per_day * farm["popularity"] * weekend * growth * drought
                for _ in range(self.poisson(mean)):
                    self.order(farm, day + timedelta(hours=self.rng.uniform(8, 22)))
            day += timedelta(days=1)

    def order(self, farm, at):
        known = [customer for customer, since in self.customers if since <= at]
        products = [p for p in farm["products"] if p["createdAt"] <= at]
        if not known or not products:
            return

        customer = self.rng.choice(known)["userId"]
        order_id = self.new_id()
        items = [{"productId": p["id"], "productName": p["name"], "quantity": self.rng.randint(1, 5),
                  "unitPrice": self.price_at(p, at)}
                 for p in self.rng.sample(products, min(len(products), self.rng.randint(1, 4)))]
        total = round(sum(i["quantity"] * i["unitPrice"] for i in items), 2)
        base = {"orderId": order_id, "customerId": customer, "farmId": farm["id"], "farmOwnerId": farm["ownerId"]}

        self.add_event("order.created", "OrderCreated", at,
                       {**base, "totalAmount": total, "itemCount": len(items), "items": items})

        # The state model: Pending -> Confirmed -> ReadyForPickup -> PickedUp, or Cancelled before pickup.
        steps = [("confirmed", self.rng.uniform(0.2, 4)), ("readyForPickup", self.rng.uniform(5, 30)),
                 ("pickedUp", self.rng.uniform(2, 48))]
        cancel_at = self.rng.choice([0, 0, 1]) if self.rng.random() < 0.08 else None
        status, moment = "pending", at

        for index, (new_status, hours) in enumerate(steps):
            moment += timedelta(hours=hours)
            if cancel_at == index:
                by_customer = self.rng.random() < 0.7
                self.add_event("order.cancelled", "OrderCancelled", moment, {
                    **base, "previousStatus": status, "reason": self.rng.choice(CANCEL_REASONS),
                    "cancelledByCustomer": by_customer})
                return
            self.add_event("order.status.changed", "OrderStatusChanged", moment,
                           {**base, "previousStatus": status, "newStatus": new_status})
            status = new_status

        if self.rng.random() < 0.45:
            rating = max(1, min(5, round(self.rng.gauss(farm["quality"], 0.8))))
            self.add_event("review.created", "ReviewCreated", moment + timedelta(hours=self.rng.uniform(1, 72)), {
                "reviewId": self.new_id(), **base, "rating": rating, "comment": self.rng.choice(REVIEW_COMMENTS)})

    def deactivate_some_accounts(self):
        for customer, since in self.rng.sample(self.customers, max(1, len(self.customers) // 60)):
            at = max(since, self.start) + timedelta(days=self.rng.uniform(5, 120))
            self.add_event("account.deactivated", "AccountMessage", at, dict(customer))


def ensure_collections(database, retention_days):
    existing = database.list_collection_names()
    if READINGS not in existing:
        options = {"timeseries": {"timeField": "timestamp", "metaField": "meta", "granularity": "seconds"}}
        if retention_days:
            options["expireAfterSeconds"] = retention_days * 86400
        try:
            database.create_collection(READINGS, **options)
        except CollectionInvalid:
            pass
    database[READINGS].create_index("messageId")
    database[READINGS].create_index("receivedAt")
    database[EVENTS].create_index("receivedAt")
    database[EVENTS].create_index([("routingKey", 1), ("receivedAt", 1)])
    database[SNAPSHOTS].create_index("snapshotAt")
    database[SNAPSHOTS].create_index([("entity", 1), ("entityId", 1)])


def insert(collection, documents, batch=5000):
    for offset in range(0, len(documents), batch):
        collection.insert_many(documents[offset:offset + batch], ordered=False)
        print(f"  {collection.name}: {min(offset + batch, len(documents))}/{len(documents)}", end="\r")
    print()


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--mongo-uri", default="mongodb://mongo:mongo@localhost:27018/?authSource=admin",
                        help="MongoDB of Docker Compose by default")
    parser.add_argument("--database", default="cropconnect_raw")
    parser.add_argument("--months", type=int, default=6)
    parser.add_argument("--farms", type=int, default=24)
    parser.add_argument("--reading-interval", type=int, default=15, help="minutes between readings of a sensor")
    parser.add_argument("--orders-per-farm-per-day", type=float, default=2.0)
    parser.add_argument("--initial-customers", type=int, default=150)
    parser.add_argument("--new-customers-per-day", type=float, default=2.0)
    parser.add_argument("--pseudonymization-key", default="dev-pseudonymization-key-change-me-0123456789",
                        help="the ingestor's Pseudonymization:Key, so the email hashes match")
    parser.add_argument("--seed", type=int, default=2026)
    parser.add_argument("--replace", action="store_true", help="delete the simulated documents first")
    parser.add_argument("--dry-run", action="store_true", help="generate and count, write nothing")
    args = parser.parse_args()

    if not 1 <= args.farms <= len(FARM_NAMES):
        parser.error(f"--farms must be between 1 and {len(FARM_NAMES)}")

    # The history ends where the live data begins: the start of the current hour.
    end = datetime.now(timezone.utc).replace(minute=0, second=0, microsecond=0)
    start = end - timedelta(days=round(args.months * 30.44))

    history = History(random.Random(args.seed), start, end, args)
    print(f"Simulating {start:%Y-%m-%d} to {end:%Y-%m-%d %H:%M} UTC for {args.farms} farms...")
    history.build_farms()
    history.plan_droughts()
    history.take_snapshots()
    history.simulate_sensors()
    history.simulate_customers()
    history.simulate_orders()
    history.deactivate_some_accounts()
    history.events.sort(key=lambda e: e["receivedAt"])

    by_key = {}
    for event in history.events:
        by_key[event["routingKey"]] = by_key.get(event["routingKey"], 0) + 1
    print(f"{len(history.readings)} readings, {len(history.snapshots)} snapshot documents, {len(history.events)} events:")
    for key, count in sorted(by_key.items()):
        print(f"  {key}: {count}")

    if args.dry_run:
        return

    database = MongoClient(args.mongo_uri)[args.database]
    ensure_collections(database, retention_days=None)

    if args.replace:
        for name in (READINGS, EVENTS, SNAPSHOTS):
            deleted = database[name].delete_many({"simulated": True}).deleted_count
            print(f"Deleted {deleted} simulated documents from {name}")

    insert(database[SNAPSHOTS], history.snapshots)
    insert(database[EVENTS], history.events)
    insert(database[READINGS], history.readings)
    print("Done.")


if __name__ == "__main__":
    main()
