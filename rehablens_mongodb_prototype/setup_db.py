from db import ensure_schema

client, db = ensure_schema()
print("Connected.")
print("Database:", db.name)
print("Collections:", db.list_collection_names())
client.close()
