using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Platform;
using UnityEngine;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Deliberately reuses passenger slot 1. It does not change seat capacity,
    // shared vehicle properties, saved XML, avatar bones or the production DLL.
    internal sealed class CamperSeatLayout
    {
        private static readonly ConditionalWeakTable<Vehicle, CamperSeatLayout> layouts =
            new ConditionalWeakTable<Vehicle, CamperSeatLayout>();
        internal EntityVehicle Owner;
        internal Transform Model;
        internal Vector3 Position;
        internal float Yaw;
        internal int Pose;
        internal Transform LeftFoot;
        internal Transform RightFoot;

        internal static bool TryGet(Vehicle vehicle, out CamperSeatLayout layout)
        {
            layout = null;
            if (vehicle == null || GameManager.IsDedicatedServer
                || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest"
                || GameManager.Instance.World == null || GameManager.Instance.World.IsRemote()) return false;
            if (!layouts.TryGetValue(vehicle, out layout)) return false;
            if (layout.Owner == null || layout.Model == null || layout.Owner.ModelTransform != layout.Model
                || GameManager.Instance.World.GetEntity(layout.Owner.entityId) != layout.Owner)
            {
                layout = null;
                return false;
            }
            return true;
        }

        internal static void Clear(EntityVehicle camper)
        {
            CamperSeatLayout previous;
            if (!layouts.TryGetValue(camper.vehicle, out previous)) return;
            layouts.Remove(camper.vehicle);
            if (previous.LeftFoot != null) UnityEngine.Object.Destroy(previous.LeftFoot.gameObject);
            if (previous.RightFoot != null) UnityEngine.Object.Destroy(previous.RightFoot.gameObject);
        }

        internal static void Apply(EntityVehicle camper, int pose, Vector3 position, float yaw,
            Vector3? leftFoot, Vector3? rightFoot, Vector3 leftRotation, Vector3 rightRotation)
        {
            var candidate = new CamperSeatLayout {
                Owner = camper, Model = camper.ModelTransform, Pose = pose, Position = position, Yaw = yaw
            };
            try
            {
                if (leftFoot.HasValue)
                {
                    candidate.LeftFoot = MakeTarget(candidate.Model, "CamperSeatFixtureFootL", leftFoot.Value, leftRotation);
                    candidate.RightFoot = MakeTarget(candidate.Model, "CamperSeatFixtureFootR", rightFoot.Value, rightRotation);
                }
            }
            catch
            {
                if (candidate.LeftFoot != null) UnityEngine.Object.Destroy(candidate.LeftFoot.gameObject);
                if (candidate.RightFoot != null) UnityEngine.Object.Destroy(candidate.RightFoot.gameObject);
                throw;
            }
            Clear(camper);
            layouts.Add(camper.vehicle, candidate);
        }

        private static Transform MakeTarget(Transform model, string name, Vector3 position, Vector3 rotation)
        {
            var target = new GameObject(name).transform;
            target.SetParent(model, false);
            target.localPosition = position;
            target.localRotation = Quaternion.Euler(rotation);
            return target;
        }
    }

    [HarmonyPatch(typeof(Vehicle), "GetSeatPose")]
    internal static class CamperSeatLayoutPose
    {
        private static void Postfix(Vehicle __instance, int __0, ref int __result)
        {
            CamperSeatLayout layout;
            if (__0 == 1 && CamperSeatLayout.TryGet(__instance, out layout)) __result = layout.Pose;
        }
    }

    [HarmonyPatch(typeof(EntityVehicle), "GetAttachedToInfo")]
    internal static class CamperSeatLayoutAttachment
    {
        private static void Postfix(EntityVehicle __instance, int __0, AttachedToEntitySlotInfo __result)
        {
            CamperSeatLayout layout;
            if (__0 != 1 || __result == null || !CamperSeatLayout.TryGet(__instance.vehicle, out layout)) return;
            __result.enterPosition = __instance.transform.InverseTransformPoint(layout.Model.TransformPoint(layout.Position));
            __result.enterRotation = (Quaternion.Inverse(__instance.transform.rotation) * layout.Model.rotation
                * Quaternion.Euler(0, layout.Yaw, 0)).eulerAngles;
        }
    }

    [HarmonyPatch(typeof(Vehicle), "GetIKTargets")]
    internal static class CamperSeatLayoutFeet
    {
        private static void Postfix(Vehicle __instance, int __0, ref List<IKController.Target> __result)
        {
            CamperSeatLayout layout;
            if (__0 != 1 || !CamperSeatLayout.TryGet(__instance, out layout) || layout.LeftFoot == null) return;
            var targets = __result == null ? new List<IKController.Target>() : new List<IKController.Target>(__result);
            targets.RemoveAll(t => t.avatarGoal == AvatarIKGoal.LeftFoot || t.avatarGoal == AvatarIKGoal.RightFoot);
            targets.Add(new IKController.Target { avatarGoal = AvatarIKGoal.LeftFoot, transform = layout.LeftFoot });
            targets.Add(new IKController.Target { avatarGoal = AvatarIKGoal.RightFoot, transform = layout.RightFoot });
            __result = targets;
        }
    }

    public sealed class ConsoleCmdCamperSeatLayout : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperseatlayout" };
        public override string getDescription() => "Temporary native passenger pose/foot-IK calibration in CodexTest.";
        public override string getHelp() => "camperseatlayout ENTITY clear | ENTITY POSE X Y Z YAW [LX LY LZ RX RY RZ LRX LRY LRZ RRX RRY RRZ]: model-local attachment root and optional ankle targets with explicit rotations; unoccupied stopped camper only. Does not attach or save.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (!ThreadManager.IsMainThread() || GameManager.IsDedicatedServer
                    || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest"
                    || (args.Count != 2 && args.Count != 6 && args.Count != 18))
                    throw new InvalidOperationException(getHelp());
                var world = GameManager.Instance.World;
                if (world == null || world.IsRemote()) throw new InvalidOperationException("Local authoritative disposable world required.");
                var player = world.GetPrimaryPlayer();
                var camper = world.GetEntity(int.Parse(args[0], CultureInfo.InvariantCulture)) as EntityVehicle;
                if (player == null || camper == null || camper.isEntityRemote || camper.vehicle == null || camper.ModelTransform == null
                    || !EntityClass.list[camper.entityClass].entityClassName.Equals("rebirthCamper80s", StringComparison.OrdinalIgnoreCase)
                    || camper.vehicleRB == null || camper.vehicleRB.velocity.magnitude > .1f
                    || Vector3.Distance(player.GetPosition(), camper.GetPosition()) > 20)
                    throw new InvalidOperationException("A nearby, stopped, loaded exact camper and local player are required.");
                for (int slot = 0; slot < camper.GetAttachMaxCount(); slot++)
                    if (camper.GetAttached(slot) != null) throw new InvalidOperationException("Dismount all occupants before changing the test layout.");
                if (args.Count == 2)
                {
                    if (args[1] != "clear") throw new ArgumentException(getHelp());
                    CamperSeatLayout.Clear(camper);
                    SdtdConsole.Instance.Output("[CamperSeatLayout] CLEARED; original native passenger configuration restored.");
                    return;
                }
                int pose = int.Parse(args[1], CultureInfo.InvariantCulture);
                if (pose < 41 || pose > 43) throw new ArgumentException("Use native passenger pose 41, 42 or 43.");
                Vector3 position = Parse(args, 2);
                float yaw = Number(args[5]);
                if (Math.Abs(position.x) > .9f || position.y < -.4f || position.y > 1
                    || position.z < -2.9f || position.z > .1f || Math.Abs(yaw) > 180)
                    throw new ArgumentException("Attachment guide is outside the measured test cabin envelope.");
                Vector3? left = null, right = null;
                Vector3 leftRotation = Vector3.zero, rightRotation = Vector3.zero;
                if (args.Count == 18)
                {
                    left = Parse(args, 6); right = Parse(args, 9);
                    leftRotation = Parse(args, 12); rightRotation = Parse(args, 15);
                    foreach (Vector3 foot in new[] { left.Value, right.Value })
                        if (Math.Abs(foot.x) > .9f || foot.y < .65f || foot.y > 1.3f || foot.z < -2.9f || foot.z > -.6f)
                            throw new ArgumentException("Ankle target is outside the measured cabin floor envelope.");
                }
                CamperSeatLayout.Apply(camper, pose, position, yaw, left, right, leftRotation, rightRotation);
                SdtdConsole.Instance.Output("[CamperSeatLayout] APPLIED entity=" + camper.entityId + "; slot=1; pose=" + pose
                    + "; modelAttachment=" + position + "; yaw=" + yaw + "; footIK=" + left.HasValue
                    + "; temporary native calibration only; capacity/visual fit NOT accepted. Use camperseat, camperdiag and inspected screenshots; dismount and clear afterward.");
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperSeatLayout] " + error.Message); }
        }

        private static Vector3 Parse(List<string> args, int start)
        {
            return new Vector3(Number(args[start]), Number(args[start + 1]), Number(args[start + 2]));
        }
        private static float Number(string text)
        {
            float value = float.Parse(text, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Finite coordinates are required.");
            return value;
        }
    }
}
