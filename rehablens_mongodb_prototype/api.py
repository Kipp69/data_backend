from datetime import datetime
from typing import Literal
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from db import ensure_schema, FRAME_COLLECTION, SESSION_COLLECTION

app = FastAPI(title="RehabLens Live Session Prototype")

client, db = ensure_schema()
frames_collection = db[FRAME_COLLECTION]
sessions_collection = db[SESSION_COLLECTION]

class Position(BaseModel):
    x: float
    y: float
    z: float

class Rotation(BaseModel):
    x: float
    y: float
    z: float
    w: float

class Joint(BaseModel):
    name: str
    position: Position
    rotation: Rotation
    deltaPosition: Position
    hasDelta: bool
    highFidelity: bool

class HandFrame(BaseModel):
    timestamp: datetime
    frameIndex: int
    hand: Literal["left", "right"]
    joints: list[Joint]

class CompletedSession(BaseModel):
    clientId: str
    sessionId: str
    activity: str = "tea_pouring"
    device: str = "Meta Quest 3"
    startedAt: datetime
    endedAt: datetime
    frames: list[HandFrame]

@app.get("/health")
def health():
    client.admin.command("ping")
    return {"ok": True, "database": db.name}

@app.post("/sessions/complete")
def complete_session(payload: CompletedSession):
    if sessions_collection.find_one({"sessionId": payload.sessionId}):
        raise HTTPException(status_code=409, detail="sessionId already exists")

    session_doc = {
        "sessionId": payload.sessionId,
        "clientId": payload.clientId,
        "activity": payload.activity,
        "device": payload.device,
        "startedAt": payload.startedAt,
        "endedAt": payload.endedAt,
        "status": "completed",
        "frameCount": len(payload.frames),
    }

    frame_docs = []
    for frame in payload.frames:
        f = frame.model_dump()
        frame_docs.append({
            "timestamp": f["timestamp"],
            "meta": {
                "clientId": payload.clientId,
                "sessionId": payload.sessionId,
                "activity": payload.activity,
                "device": payload.device,
                "hand": f["hand"],
            },
            "frameIndex": f["frameIndex"],
            "joints": f["joints"],
        })

    sessions_collection.insert_one(session_doc)

    try:
        if frame_docs:
            frames_collection.insert_many(frame_docs)
    except Exception:
        # Avoid leaving a session record behind if frame insertion fails.
        sessions_collection.delete_one({"sessionId": payload.sessionId})
        raise

    return {
        "ok": True,
        "clientId": payload.clientId,
        "sessionId": payload.sessionId,
        "insertedFrames": len(frame_docs),
    }

@app.get("/clients/{client_id}/sessions")
def get_client_sessions(client_id: str):
    docs = list(
        sessions_collection.find(
            {"clientId": client_id},
            {"_id": 0}
        ).sort("startedAt", -1)
    )
    return docs

@app.get("/sessions/{session_id}/frames")
def get_session_frames(session_id: str, limit: int = 200):
    limit = max(1, min(limit, 5000))
    docs = list(
        frames_collection.find(
            {"meta.sessionId": session_id},
            {"_id": 0}
        ).sort("timestamp", 1).limit(limit)
    )
    return docs
