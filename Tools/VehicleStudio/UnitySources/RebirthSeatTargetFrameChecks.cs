using System;
using System.IO;
using Rebirth.Vehicles.Authoring;
using UnityEngine;

public static class RebirthSeatTargetFrameChecks
{
    static void Require(bool value, string label) { if (!value) throw new Exception(label); }
    public static void Run()
    {
        const string output = "E:/_Haven/shared/REBIRTHTemp/SeatTargetFrameChecks_20261015";
        Directory.CreateDirectory(output);
        var model = new GameObject("Vehicle");
        var foreign = new GameObject("Foreign");
        try
        {
            model.transform.SetPositionAndRotation(new Vector3(800, 12, -420), Quaternion.Euler(8, 112, 3));
            model.transform.localScale = Vector3.one * 1.25f;
            var steering = new GameObject("Steering").transform; steering.SetParent(model.transform, false);
            steering.localPosition = new Vector3(.2f, .8f, 1.1f); steering.localRotation = Quaternion.Euler(15, 0, 12);
            var hand = new GameObject("HandTarget").transform; hand.SetParent(model.transform, false);
            Vector3 authored = new Vector3(.4f, .95f, 1.2f), authoredRotation = new Vector3(12, 40, 80);
            string path = SeatPoseTargetFrames.Path(model.transform, steering);
            Require(path == "Steering" && SeatPoseTargetFrames.Parent(model.transform, path) == steering, "native parent identity");
            Require(SeatPoseTargetFrames.Path(model.transform, foreign.transform) == null, "foreign hierarchy excluded");
            Require(SeatPoseTargetFrames.Path(model.transform, model.transform) == "", "model root binding");
            SeatPoseTargetFrames.Capture(model.transform, steering, authored, authoredRotation, out var offset, out var angle);
            SeatPoseTargetFrames.Resolve(model.transform, steering, offset, angle, out var resolved, out var resolvedAngle);
            Require(Vector3.Distance(resolved, authored) < .0001f && Quaternion.Angle(Quaternion.Euler(resolvedAngle), Quaternion.Euler(authoredRotation)) < .05f, "rotated scaled model roundtrip");
            float maxPositionError = 0, maxRotationError = 0;
            foreach (float turn in new[] { -30f, -15f, 0f, 15f, 30f })
            {
                steering.localRotation = Quaternion.Euler(15, turn, 12);
                // Entry while turned uses persisted parent-space values.
                SeatPoseTargetFrames.Apply(hand, SeatPoseTargetFrames.Parent(model.transform, path), offset, angle);
                float positionError = Vector3.Distance(hand.position, steering.TransformPoint(offset));
                float rotationError = Quaternion.Angle(hand.rotation, steering.rotation * Quaternion.Euler(angle));
                maxPositionError = Mathf.Max(maxPositionError, positionError); maxRotationError = Mathf.Max(maxRotationError, rotationError);
                Require(positionError < .0001f && rotationError < .05f, "turned entry " + turn);
                SeatPoseTargetFrames.Resolve(model.transform, steering, offset, angle, out resolved, out resolvedAngle);
                SeatPoseTargetFrames.Capture(model.transform, steering, resolved, resolvedAngle, out var recaptured, out var recapturedAngle);
                Require(Vector3.Distance(recaptured, offset) < .0001f && Quaternion.Angle(Quaternion.Euler(recapturedAngle), Quaternion.Euler(angle)) < .05f, "editor reopen retains offset " + turn);
            }
            bool rejected = false; try { SeatPoseTargetFrames.Parent(model.transform, "Missing"); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "missing parent rejected");
            var duplicate = new GameObject("Steering").transform; duplicate.SetParent(model.transform, false);
            rejected = false; try { SeatPoseTargetFrames.Path(model.transform, duplicate); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "ambiguous parent rejected");
            File.WriteAllText(output + "/passed.txt", "PASS: native/root/foreign identity, rotated scaled frame roundtrip, five steering entry angles, five editor recaptures, missing/ambiguous-parent rejection. maxPositionError=" + maxPositionError.ToString("R") + " maxRotationError=" + maxRotationError.ToString("R") + ". Synthetic Unity transforms only; no native animation, constraint evaluation, gameplay, physics or multiplayer proof.");
        }
        finally { UnityEngine.Object.DestroyImmediate(model); UnityEngine.Object.DestroyImmediate(foreign); }
    }
}
