import os
from pymongo import MongoClient, ASCENDING, DESCENDING
from pymongo.server_api import ServerApi
from dotenv import load_dotenv

load_dotenv()

DB_NAME = "rehablens"
FRAME_COLLECTION = "hand_frames"
SESSION_COLLECTION = "sessions"

def get_client():
    uri = os.getenv("MONGODB_URI")
    if not uri:
        raise RuntimeError("Missing MONGODB_URI in .env")
    return MongoClient(uri, server_api=ServerApi("1"))

def ensure_schema():
    client = get_client()
    client.admin.command("ping")
    db = client[DB_NAME]
    names = db.list_collection_names()

    if SESSION_COLLECTION not in names:
        db.create_collection(SESSION_COLLECTION)

    # Index sessions by sessionId, and make client history fast to query.
    sessions = db[SESSION_COLLECTION]
    sessions.create_index([("sessionId", ASCENDING)], unique=True)
    sessions.create_index([("clientId", ASCENDING), ("startedAt", DESCENDING)])

    if FRAME_COLLECTION not in names:
        db.create_collection(
            FRAME_COLLECTION,
            timeseries={
                "timeField": "timestamp",
                "metaField": "meta",
                "granularity": "seconds",
            },
        )

    return client, db
