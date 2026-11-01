using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class XRHandDataExplorer : MonoBehaviour
{
    private XRHandSubsystem handSubsystem;
    private int updateCounter = 0;

    void Update()
    {
        if (handSubsystem == null)
        {
            FindHandSubsystem();
        }
    }

    void FindHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            if (subsystem.running)
            {
                handSubsystem = subsystem;
                handSubsystem.updatedHands += OnHandsUpdated;

                Debug.Log("REHABLENS: XR Hand Subsystem connected!");
                return;
            }
        }
    }

    void OnHandsUpdated(
        XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        // XR Hands updates twice per frame.
        // We only inspect the normal game-logic update.
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
            return;

        updateCounter++;

        // Print roughly every 30 updates so Console stays readable.
        if (updateCounter % 30 != 0)
            return;

        PrintHand(subsystem.rightHand, "RIGHT");
        PrintHand(subsystem.leftHand, "LEFT");
    }

    void PrintHand(XRHand hand, string handName)
    {
        if (!hand.isTracked)
        {
            Debug.Log($"{handName} HAND: not tracked");
            return;
        }

        Debug.Log($"========== {handName} HAND ==========");

        for (int i = XRHandJointID.BeginMarker.ToIndex();
             i < XRHandJointID.EndMarker.ToIndex();
             i++)
        {
            XRHandJointID jointID =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint = hand.GetJoint(jointID);

            if (joint.TryGetPose(out Pose pose))
            {
                Debug.Log(
                    $"{jointID} | " +
                    $"Position: " +
                    $"X={pose.position.x:F3}, " +
                    $"Y={pose.position.y:F3}, " +
                    $"Z={pose.position.z:F3} | " +

                    $"Rotation: " +
                    $"X={pose.rotation.x:F3}, " +
                    $"Y={pose.rotation.y:F3}, " +
                    $"Z={pose.rotation.z:F3}, " +
                    $"W={pose.rotation.w:F3} | " +

                    $"TrackingState={joint.trackingState}"
                );
            }
            else
            {
                Debug.Log(
                    $"{jointID} | Pose unavailable | " +
                    $"TrackingState={joint.trackingState}"
                );
            }
        }
    }

    void OnDestroy()
    {
        if (handSubsystem != null)
        {
            handSubsystem.updatedHands -= OnHandsUpdated;
        }
    }
}