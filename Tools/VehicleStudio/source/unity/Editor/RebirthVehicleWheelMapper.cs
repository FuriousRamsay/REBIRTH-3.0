// REBIRTH Vehicle Studio v0.4 — conservative name-based visual mesh proposals.
// Absolutely NO guesswork using left/right axis or wheel0 index ordering.
// Matching proposed visual ↔ XML pivot names is not a guarantee of correct
// wheel collider, steering axis, physics or game rig support.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RebirthVehicleInspector.UnityTools
{
    public static class RebirthVehicleWheelMapper
    {
        [Serializable]
        public sealed class Proposal
        {
            public string xmlWheelClass, wheelCorner, tirePivotPath, sourceMeshPath;
            public string outcome, explanation;
            public Transform selectedMesh;
        }

        private static readonly HashSet<string> VisualTokens = new HashSet<string> {
            "wheel", "wheels", "tire", "tyre", "rim", "tires", "tyres"
        };

        // A proposal requires a clearly named corner in both the captured XML
        // transform path and the source FBX visual. No bounding-box inference.
        public static string DetectCorner(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string separate = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
            string lower = separate.ToLowerInvariant();
            string[] tokens = Regex.Split(lower, "[^a-z0-9]+", RegexOptions.CultureInvariant);
            var words = new HashSet<string>(tokens, StringComparer.Ordinal);
            string packed = Regex.Replace(lower, "[^a-z0-9]", "");
            bool front = words.Contains("front") || words.Contains("fwd") || words.Contains("forward") || packed.Contains("front");
            bool rear = words.Contains("back") || words.Contains("rear") || packed.Contains("rear") || packed.Contains("back");
            bool left = words.Contains("left") || packed.Contains("left");
            bool right = words.Contains("right") || packed.Contains("right");
            if (front && !rear && left && !right) return "front-left";
            if (front && !rear && right && !left) return "front-right";
            if (rear && !front && left && !right) return "rear-left";
            if (rear && !front && right && !left) return "rear-right";
            if (front || rear || left || right) return ""; // conflicting or incomplete labels
            // Explicit short tags only. Do NOT classify LF elsewhere in a random word.
            foreach (string token in tokens)
            {
                switch (token)
                {
                    case "fl": case "lf": return "front-left";
                    case "fr": case "rf": return "front-right";
                    case "rl": case "lr": case "bl": case "lb": return "rear-left";
                    case "rr": case "br": case "rb": return "rear-right";
                }
            }
            return "";
        }

        public static bool LooksLikeWheelVisual(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string separate = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
            foreach (string token in Regex.Split(separate, "[^a-z0-9]+", RegexOptions.CultureInvariant))
                if (VisualTokens.Contains(token)) return true;
            // Common glued-name FBX nodes, e.g. frontLeftWheel or leftfronttire.
            return separate.Contains("wheel") || separate.Contains("tire") || separate.Contains("tyre");
        }

        public static List<Proposal> Discover(RebirthVehicleRigWorkspace workspace)
        {
            if (workspace == null || workspace.profile == null || workspace.importedVisuals == null)
                throw new ArgumentException("A scene workspace, rig profile and an imported FBX scene instance are required.");
            var visuals = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
            foreach (Renderer renderer in workspace.importedVisuals.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !LooksLikeWheelVisual(renderer.transform.name)) continue;
                string corner = DetectCorner(renderer.transform.name);
                if (string.IsNullOrEmpty(corner)) continue;
                if (!visuals.ContainsKey(corner)) visuals[corner] = new List<Transform>();
                if (!visuals[corner].Contains(renderer.transform)) visuals[corner].Add(renderer.transform);
            }
            var targetCounts = new Dictionary<string, int>();
            foreach (VehicleWheelLink wheel in workspace.profile.wheels)
            {
                if (wheel == null) continue;
                string corner = DetectCorner(wheel.tireTransformPath);
                if (string.IsNullOrEmpty(corner)) continue;
                if (!targetCounts.ContainsKey(corner)) targetCounts[corner] = 0;
                targetCounts[corner]++;
            }
            var proposals = new List<Proposal>();
            foreach (VehicleWheelLink wheel in workspace.profile.wheels)
            {
                if (wheel == null) continue;
                string corner = DetectCorner(wheel.tireTransformPath);
                var proposal = new Proposal {
                    xmlWheelClass = wheel.partClass,
                    wheelCorner = corner,
                    tirePivotPath = wheel.tireTransformPath,
                    sourceMeshPath = "",
                    outcome = "no-proposal",
                    explanation = "No explicit unambiguous corner label on the XML tireTransform; review manually."
                };
                if (!string.IsNullOrEmpty(corner))
                {
                    List<Transform> matches;
                    if (targetCounts[corner] != 1)
                        proposal.explanation = "Multiple XML wheels claim " + corner + "; no automatic mapping.";
                    else if (!visuals.TryGetValue(corner, out matches) || matches.Count == 0)
                        proposal.explanation = "No individually renderable FBX wheel labeled " + corner + ".";
                    else if (matches.Count > 1)
                        proposal.explanation = "Multiple FBX wheel meshes are labeled " + corner + "; choose a parent/group manually.";
                    else if (string.IsNullOrEmpty(wheel.tireTransformPath) ||
                             RebirthVehicleRigBuilder.FindPath(workspace.transform, wheel.tireTransformPath) == null)
                        proposal.explanation = "Referenced XML tire pivot not present in the authored rig.";
                    else
                    {
                        proposal.selectedMesh = matches[0];
                        proposal.sourceMeshPath = HierarchyPath(workspace.transform, matches[0]);
                        proposal.outcome = "candidate";
                        proposal.explanation = "Unique matching explicit XML/FBX wheel corner name. Requires visual approval, pivot calibration and in-game test.";
                    }
                }
                proposals.Add(proposal);
            }
            return proposals;
        }

        private static string HierarchyPath(Transform root, Transform leaf)
        {
            var names = new List<string>();
            for (Transform p = leaf; p != null && p != root; p = p.parent)
                names.Add(p.name);
            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
