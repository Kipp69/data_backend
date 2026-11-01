from pprint import pprint
from db import ensure_schema, FRAME_COLLECTION

SESSION_ID = "demo-session-001"
client, db = ensure_schema()
frames = db[FRAME_COLLECTION]

print("\n--- Frames ---")
for frame in frames.find({"meta.sessionId": SESSION_ID}, {"_id": 0}).sort("timestamp", 1):
    pprint(frame)

print("\n--- Tracking summary ---")
pipeline = [
    {"$match": {"meta.sessionId": SESSION_ID}},
    {"$unwind": "$joints"},
    {"$group": {
        "_id": "$joints.trackingStatus",
        "samples": {"$sum": 1},
        "avgConfidence": {"$avg": "$joints.confidenceScore"}
    }},
    {"$sort": {"_id": 1}}
]
for row in frames.aggregate(pipeline):
    pprint(row)

client.close()
