using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.Hands;

public class RehabLensSessionRecorder : MonoBehaviour
{
    [Header("Prototype identity")]
    public string clientId = "C001";
    public string activity = "tea_pouring";
    public string device = "Meta Quest 3";

    [Header("Backend")]
    // Unity Editor + FastAPI running on the SAME Windows PC:
    public string apiUrl = "http://127.0.0.1:8000/sessions/complete";

    [Header("Recording")]
    [Tooltip("0.05 = 20 samples/second.")]
    public float sampleIntervalSeconds = 0.05f;

    private XRHandSubsystem handSubsystem;
    private bool subscribed = false;
    private bool isRecording = false;

    private float lastSampleTime = -999f;
    private int frameIndex = 0;

    private string currentSessionId;
    private DateTime sessionStartedUtc;

    private string csvPath;
    private StreamWriter csvWriter;

    // Stores all frames for the current session before upload.
    private readonly List<HandFramePayload> recordedFrames =
        new List<HandFramePayload>();

    // Stores the previous position of each joint so we can calculate movement delta.
    // Example key: "left:Wrist" or "right:IndexTip"
    private readonly Dictionary<string, Vector3> previousJointPositions =
        new Dictionary<string, Vector3>();

    void Update()
    {
        if (!subscribed)
        {
            TryConnectHandSubsystem();
        }
    }

    void TryConnectHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            if (subsystem.running)
            {
                handSubsystem = subsystem;
                handSubsystem.updatedHands += OnHandsUpdated;

                subscribed = true;

                Debug.Log("RehabLens: XR Hands connected.");
                return;
            }
        }
    }

    // Called by the START button.
    public void StartSession()
    {
        if (isRecording)
        {
            Debug.LogWarning("A session is already recording.");
            return;
        }

        currentSessionId = Guid.NewGuid().ToString();
        sessionStartedUtc = DateTime.UtcNow;

        frameIndex = 0;

        // Clear data from the previous session.
        recordedFrames.Clear();
        previousJointPositions.Clear();

        // Makes sure the first sample can happen immediately.
        lastSampleTime = -999f;

        string folder = Path.Combine(
            Application.persistentDataPath,
            "RehabLensData",
            clientId
        );

        Directory.CreateDirectory(folder);

        csvPath = Path.Combine(
            folder,
            $"session_{currentSessionId}.csv"
        );

        csvWriter = new StreamWriter(
            csvPath,
            false,
            Encoding.UTF8
        );

        // Cleaned CSV structure.
        csvWriter.WriteLine(
            "ClientId,SessionId,Timestamp,FrameIndex,Hand,Joint," +
            "Position_X,Position_Y,Position_Z," +
            "Rotation_X,Rotation_Y,Rotation_Z,Rotation_W," +
            "Delta_X,Delta_Y,Delta_Z," +
            "HasDelta,HighFidelity"
        );

        csvWriter.Flush();

        isRecording = true;

        Debug.Log(
            $"RehabLens SESSION STARTED | client={clientId} | " +
            $"session={currentSessionId}"
        );

        Debug.Log($"CSV: {csvPath}");
    }

    // Called by the FINISH button.
    public void FinishSession()
    {
        if (!isRecording)
        {
            Debug.LogWarning("No session is recording.");
            return;
        }

        isRecording = false;

        if (csvWriter != null)
        {
            csvWriter.Flush();
            csvWriter.Close();
            csvWriter = null;
        }

        DateTime endedUtc = DateTime.UtcNow;

        CompletedSessionPayload payload = new CompletedSessionPayload
        {
            clientId = clientId,
            sessionId = currentSessionId,
            activity = activity,
            device = device,
            startedAt = sessionStartedUtc.ToString("o"),
            endedAt = endedUtc.ToString("o"),
            frames = new List<HandFramePayload>(recordedFrames)
        };

        Debug.Log(
            $"RehabLens SESSION FINISHED | " +
            $"{recordedFrames.Count} hand frames | " +
            "uploading to MongoDB backend..."
        );

        StartCoroutine(
            UploadCompletedSession(payload)
        );
    }

    void OnHandsUpdated(
        XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (!isRecording)
            return;

        // We only record Dynamic updates.
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;

        // Limit recording to the chosen sampling rate.
        if (Time.unscaledTime - lastSampleTime < sampleIntervalSeconds)
            return;

        lastSampleTime = Time.unscaledTime;

        frameIndex++;

        CaptureHand(
            subsystem.leftHand,
            "left"
        );

        CaptureHand(
            subsystem.rightHand,
            "right"
        );
    }

    void CaptureHand(
        XRHand hand,
        string handName)
    {
        // Ignore the hand completely if it is not currently tracked.
        if (!hand.isTracked)
            return;

        HandFramePayload frame = new HandFramePayload
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            frameIndex = frameIndex,
            hand = handName,
            joints = new List<JointPayload>()
        };

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID jointId =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint =
                hand.GetJoint(jointId);

            // If Unity cannot provide a pose,
            // do not store this joint.
            if (!joint.TryGetPose(out Pose pose))
                continue;

            // Unique key for this exact hand + joint.
            string jointKey =
                handName + ":" + jointId.ToString();

            // Try to find where this joint was
            // during the previous sample.
            bool hasDelta =
                previousJointPositions.TryGetValue(
                    jointKey,
                    out Vector3 previousPosition
                );

            Vector3 deltaPosition =
                hasDelta
                    ? pose.position - previousPosition
                    : Vector3.zero;

            // Save current position so it becomes
            // the previous position next sample.
            previousJointPositions[jointKey] =
                pose.position;

            // Instead of storing the entire trackingState string,
            // we keep one useful quality indicator.
            bool highFidelity =
                (joint.trackingState &
                 XRHandJointTrackingState.HighFidelityPose) != 0;

            JointPayload jointPayload =
                new JointPayload
                {
                    name = jointId.ToString(),

                    position = new PositionPayload
                    {
                        x = pose.position.x,
                        y = pose.position.y,
                        z = pose.position.z
                    },

                    rotation = new RotationPayload
                    {
                        x = pose.rotation.x,
                        y = pose.rotation.y,
                        z = pose.rotation.z,
                        w = pose.rotation.w
                    },

                    deltaPosition =
                        new PositionPayload
                        {
                            x = deltaPosition.x,
                            y = deltaPosition.y,
                            z = deltaPosition.z
                        },

                    hasDelta = hasDelta,

                    highFidelity = highFidelity
                };

            frame.joints.Add(jointPayload);

            WriteCsvRow(
                frame,
                jointPayload
            );
        }

        // Only store a hand frame if at least
        // one valid joint was recorded.
        if (frame.joints.Count > 0)
        {
            recordedFrames.Add(frame);
        }
    }

    void WriteCsvRow(
        HandFramePayload frame,
        JointPayload joint)
    {
        if (csvWriter == null)
            return;

        CultureInfo c =
            CultureInfo.InvariantCulture;

        csvWriter.WriteLine(
            $"{clientId}," +
            $"{currentSessionId}," +
            $"{frame.timestamp}," +
            $"{frame.frameIndex}," +
            $"{frame.hand}," +
            $"{joint.name}," +

            $"{joint.position.x.ToString(c)}," +
            $"{joint.position.y.ToString(c)}," +
            $"{joint.position.z.ToString(c)}," +

            $"{joint.rotation.x.ToString(c)}," +
            $"{joint.rotation.y.ToString(c)}," +
            $"{joint.rotation.z.ToString(c)}," +
            $"{joint.rotation.w.ToString(c)}," +

            $"{joint.deltaPosition.x.ToString(c)}," +
            $"{joint.deltaPosition.y.ToString(c)}," +
            $"{joint.deltaPosition.z.ToString(c)}," +

            $"{joint.hasDelta}," +
            $"{joint.highFidelity}"
        );
    }

    IEnumerator UploadCompletedSession(
        CompletedSessionPayload payload)
    {
        string json =
            JsonUtility.ToJson(payload);

        using UnityWebRequest request =
            new UnityWebRequest(
                apiUrl,
                "POST"
            );

        byte[] body =
            Encoding.UTF8.GetBytes(json);

        request.uploadHandler =
            new UploadHandlerRaw(body);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        yield return request.SendWebRequest();

        if (
            request.result !=
            UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"UPLOAD FAILED: " +
                $"{request.responseCode} " +
                $"{request.error}\n" +
                request.downloadHandler.text +
                $"\nCSV was still saved locally at: {csvPath}"
            );
        }
        else
        {
            Debug.Log(
                "UPLOAD SUCCESS. MongoDB backend response:\n" +
                request.downloadHandler.text
            );
        }
    }

    void OnDestroy()
    {
        if (
            handSubsystem != null &&
            subscribed)
        {
            handSubsystem.updatedHands -=
                OnHandsUpdated;
        }

        if (csvWriter != null)
        {
            csvWriter.Flush();
            csvWriter.Close();
            csvWriter = null;
        }
    }
}


// ======================================================
// DATA STRUCTURES SENT TO FASTAPI / MONGODB
// ======================================================

[Serializable]
public class CompletedSessionPayload
{
    public string clientId;
    public string sessionId;
    public string activity;
    public string device;
    public string startedAt;
    public string endedAt;

    public List<HandFramePayload> frames;
}


[Serializable]
public class HandFramePayload
{
    public string timestamp;
    public int frameIndex;
    public string hand;

    public List<JointPayload> joints;
}


[Serializable]
public class JointPayload
{
    public string name;

    // Absolute XR position.
    public PositionPayload position;

    // Absolute XR rotation.
    public RotationPayload rotation;

    // Movement since this joint's previous sample.
    public PositionPayload deltaPosition;

    // False for the first measurement because
    // there is no previous measurement yet.
    public bool hasDelta;

    // Simple hand-tracking quality indicator.
    public bool highFidelity;
}


[Serializable]
public class PositionPayload
{
    public float x;
    public float y;
    public float z;
}


[Serializable]
public class RotationPayload
{
    public float x;
    public float y;
    public float z;
    public float w;
}