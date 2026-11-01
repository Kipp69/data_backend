import csv
from datetime import datetime
from pathlib import Path
from db import ensure_schema, FRAME_COLLECTION, SESSION_COLLECTION

SESSION_ID = "demo-session-001"
PARTICIPANT_ID = "P001"
HAND = "right"
ACTIVITY = "tea_pouring"
BASE_DATE = "2026-10-20"

def num(value):
    if value.strip().lower() == "nan":
        return None
    return float(value)

client, db = ensure_schema()
frames = db[FRAME_COLLECTION]
sessions = db[SESSION_COLLECTION]

frames.delete_many({"meta.sessionId": SESSION_ID})
sessions.delete_one({"sessionId": SESSION_ID})

rows = []
with Path(__file__).with_name("mock_hand_tracking.csv").open(newline="", encoding="utf-8") as f:
    for row in csv.DictReader(f):
        rows.append({
            "timestamp": datetime.fromisoformat(f"{BASE_DATE}T{row['Timestamp']}+00:00"),
            "meta": {
                "sessionId": SESSION_ID,
                "participantId": PARTICIPANT_ID,
                "hand": HAND,
                "device": "Meta Quest 3",
                "activity": ACTIVITY
            },
            "frameIndex": int(row["FrameIndex"]),
            "joints": [{
                "name": row["JointName"],
                "position": {
                    "x": num(row["Position_X"]),
                    "y": num(row["Position_Y"]),
                    "z": num(row["Position_Z"])
                },
                "confidenceScore": float(row["Confidence_Score"]),
                "trackingStatus": row["Tracking_Status"]
            }]
        })

sessions.insert_one({
    "sessionId": SESSION_ID,
    "participantId": PARTICIPANT_ID,
    "device": "Meta Quest 3",
    "activity": ACTIVITY,
    "startedAt": rows[0]["timestamp"],
    "notes": "Mock RehabLens prototype session"
})

result = frames.insert_many(rows)
print(f"Inserted {len(result.inserted_ids)} frame documents into {db.name}.{FRAME_COLLECTION}")
client.close()
