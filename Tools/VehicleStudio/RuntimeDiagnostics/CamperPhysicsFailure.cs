using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Evidence only: never prevents native execution or suppresses its exception.
    [HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.PhysicsFixedUpdate))]
    internal static class CamperPhysicsFailure
    {
        private static int captures;
        private static Exception Finalizer(EntityVehicle __instance, Exception __exception)
        {
            if (__exception == null || captures >= 8 || GameManager.IsDedicatedServer
                || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") return __exception;
            captures++;
            try
            {
                var fields = new Dictionary<string, string>();
                for (Type type = __instance.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.FieldType.IsValueType) continue;
                        object value = field.GetValue(__instance);
                        var unity = value as UnityEngine.Object;
                        fields[type.Name + "." + field.Name] = value == null ? "null"
                            : value is UnityEngine.Object && unity == null ? "destroyed Unity object"
                            : value.GetType().FullName;
                    }
                var wheels = new List<object>();
                if (__instance.wheels != null)
                    for (int i = 0; i < __instance.wheels.Length; i++)
                    {
                        var wheel = __instance.wheels[i];
                        wheels.Add(new { index = i, collider = wheel.wheelC != null,
                            steerTarget = wheel.steerT != null, tireTarget = wheel.tireT != null });
                    }
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string file = Path.Combine(CamperDiagnosticsMod.ExportFolder, "physics_failure_" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(file, JsonConvert.SerializeObject(new {
                    kind = "NATIVE_VEHICLE_PHYSICS_EXCEPTION", utc = DateTime.UtcNow.ToString("o"),
                    entity = __instance.entityId, entityClass = EntityClass.list[__instance.entityClass].entityClassName,
                    position = new { x = __instance.GetPosition().x, y = __instance.GetPosition().y, z = __instance.GetPosition().z }, remote = __instance.isEntityRemote,
                    rigidbody = __instance.vehicleRB != null, physicsRoot = __instance.PhysicsTransform != null,
                    model = __instance.ModelTransform != null, vehicle = __instance.vehicle != null,
                    exception = __exception.ToString(), fields, wheels,
                    note = "Read-only failure evidence; original exception remains unsuppressed."
                }, Formatting.Indented));
                Log.Out("[CamperDiagnostics] PHYSICS_FAILURE_CAPTURE " + file);
            }
            catch (Exception captureError)
            {
                Log.Out("[CamperDiagnostics] Failure capture unavailable: " + captureError.Message);
            }
            return __exception;
        }
    }
}
