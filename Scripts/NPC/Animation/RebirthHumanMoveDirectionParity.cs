using System;
using HarmonyLib;

// In 2.6, EntityMoveHelper called SetMoveForwardWithModifiers before
// MakeMotionMoveToward, leaving the path vector as the final movement command.
// In 3.x the call order is reversed: SetMoveForwardWithModifiers overwrites
// non-root-motion humans' absolute path vector with local straight-ahead (0,1).
// Capture the vector the native helper itself chose and restore it afterward.
internal static class RebirthHumanMoveDirectionParity
{
    [ThreadStatic] private static EntityMoveHelper activeHelper;
    [ThreadStatic] private static bool captured;
    [ThreadStatic] private static float pathX;
    [ThreadStatic] private static float pathZ;

    internal static void Install()
    {
        var harmony = new Harmony("rebirth.npc.human-move-direction-parity");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(CaptureNativePathDirection));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(ApplyNativePathDirection));
    }

    [HarmonyPatch(typeof(EntityMoveHelper), nameof(EntityMoveHelper.UpdateMoveHelper))]
    private static class ApplyNativePathDirection
    {
        [HarmonyPrefix]
        private static void Begin(EntityMoveHelper __instance)
        {
            activeHelper = __instance?.entity is EntityRebirthHumanoidNPC npc && !npc.RootMotion
                ? __instance : null;
            captured = false;
        }

        [HarmonyPostfix]
        private static void End(EntityMoveHelper __instance)
        {
            if (captured && ReferenceEquals(activeHelper, __instance) &&
                __instance.IsActive && __instance.entity is EntityRebirthHumanoidNPC npc &&
                !npc.RootMotion)
            {
                npc.moveDirection.x = pathX;
                npc.moveDirection.z = pathZ;
                npc.isMoveDirAbsolute = true;
            }
            activeHelper = null;
            captured = false;
        }

        [HarmonyFinalizer]
        private static Exception ClearAfterFailure(Exception __exception)
        {
            activeHelper = null;
            captured = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.MakeMotionMoveToward))]
    private static class CaptureNativePathDirection
    {
        [HarmonyPrefix]
        private static void Capture(EntityAlive __instance, float x, float z)
        {
            if (activeHelper == null || !ReferenceEquals(activeHelper.entity, __instance))
                return;
            pathX = x;
            pathZ = z;
            captured = true;
        }
    }
}
