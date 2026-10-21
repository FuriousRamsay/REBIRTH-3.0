using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rebirth.Vehicles.Authoring
{
    internal static class SeatPoseVehicleTargets
    {
        internal static Dictionary<string, Vector3> Read(Transform model, int seat)
        {
            var result = new Dictionary<string, Vector3>();
            foreach (string limb in new[] { "Seat", "HandL", "HandR", "FootL", "FootR" })
            {
                string marker = "REBIRTH_Seat" + seat + "_" + limb;
                var matches = model.GetComponentsInChildren<Transform>(true).Where(t => t.name == marker).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Expected one Unity contact target: " + marker);
                result.Add(limb == "Seat" ? "Seat" : "IK" + limb, model.InverseTransformPoint(matches[0].position));
            }
            return result;
        }
    }
}
