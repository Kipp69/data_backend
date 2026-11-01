# RehabLens MongoDB Prototype

## Recommended structure

Use:
- `sessions`: one document per rehab exercise/session
- `hand_frames`: high-frequency tracking data

For `hand_frames`, use one document **per frame** and store all joints for that frame in a `joints` array.

Why this is better than one MongoDB document per CSV row:
- fewer writes;
- all joints for one frame stay together;
- easier to batch-upload from Quest/Unity;
- natural fit for MongoDB time-series storage.

Lost coordinates are stored as `null`, not `NaN`.

## Architecture

Recommended:

`Quest 3 / Unity -> HTTP API -> MongoDB Atlas`

Do not store Atlas credentials directly in the Quest application.

## Setup

1. Create a MongoDB Atlas Free cluster.
2. Create a database user.
3. Add your current IP address to the Atlas IP access list.
4. Copy the Atlas Python connection string.
5. In this folder:

```bash
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
```

6. Put your real URI in `.env`.

## Create the database

```bash
python setup_db.py
```

Creates:
- `rehablens.sessions`
- `rehablens.hand_frames`

`hand_frames` is a MongoDB time-series collection using:
- `timestamp` as the time field
- `meta` as stable metadata
- `seconds` granularity

## Load the mock data

```bash
python seed_mock_data.py
```

## Query it

```bash
python query_demo.py
```

This prints the frames and a tracking-status summary.

## Optional prototype backend

```bash
uvicorn api:app --reload
```

Then open:
- `http://127.0.0.1:8000/docs`
- `http://127.0.0.1:8000/health`

The API has:
- `POST /frames/batch`
- `GET /sessions/{session_id}/frames`

## What should be stored?

### sessions
Example:
- sessionId
- pseudonymous participantId
- activity (`tea_pouring`)
- device (`Meta Quest 3`)
- startedAt / endedAt
- difficulty
- summary metrics

### hand_frames
Example:
- timestamp
- frameIndex
- left/right hand
- all joint positions
- confidence score
- tracking status

## Next step

Record a short Quest session, batch frames in Unity, send them to the backend, and calculate:
- percentage of `Fully_Tracked`, `Estimated`, and `Lost`;
- average confidence;
- movement trajectory;
- completion time;
- later: smoothness or reach accuracy.
