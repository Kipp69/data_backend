from db import get_client, DB_NAME, FRAME_COLLECTION, SESSION_COLLECTION, ensure_schema

client = get_client()
db = client[DB_NAME]

print("Before:", db.list_collection_names())

# Drop only the prototype telemetry collections.
# Dropping a time-series collection also removes its internal system.buckets collection.
db.drop_collection(FRAME_COLLECTION)
db.drop_collection(SESSION_COLLECTION)

client.close()

client, db = ensure_schema()
print("After reset:", db.list_collection_names())
print("RehabLens prototype data cleared.")
client.close()
