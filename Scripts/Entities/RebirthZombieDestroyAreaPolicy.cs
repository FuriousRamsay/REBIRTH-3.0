using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Runtime policy for the REBIRTH "Zombies Destroy Areas" option.
///
/// Off suppresses EAIDestroyArea, which is the broad fallback that can select
/// unrelated destruction positions when a zombie considers its target difficult
/// to reach. This does not disable the normal block-breaking AI used when a block
/// directly obstructs the zombie's route.
/// </summary>
public static class RebirthZombieDestroyAreaRuntimePolicy
{
    private static bool enabled;

    public static bool Enabled { get { return enabled; } }

    public static void SetEnabled(bool value)
    {
        enabled = value;
    }

    internal static void ClearDestroyAreaState(EAIDestroyArea task, bool clearNavigatorPath)
    {
        if (task == null)
            return;

        EntityAlive entity = task.theEntity;
        EntityMoveHelper moveHelper = entity != null ? entity.moveHelper : null;
        if (moveHelper != null)
        {
            moveHelper.IsDestroyAreaTryUnreachable = false;
            moveHelper.IsDestroyArea = false;
            moveHelper.destroyRefreshTicks = 0;
            moveHelper.destroyPosition.y = 0f;
        }

        task.delayTime = 0f;

        if (clearNavigatorPath && entity != null && entity.navigator != null)
            entity.navigator.clearPath();
    }
}

[HarmonyPatch(typeof(EAIDestroyArea), nameof(EAIDestroyArea.CanExecute))]
public static class RebirthZombieDestroyAreaCanExecutePatch
{
    private static bool Prefix(EAIDestroyArea __instance, ref bool __result)
    {
        if (RebirthZombieDestroyAreaRuntimePolicy.Enabled)
            return true;

        RebirthZombieDestroyAreaRuntimePolicy.ClearDestroyAreaState(__instance, false);
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EAIDestroyArea), nameof(EAIDestroyArea.Continue))]
public static class RebirthZombieDestroyAreaContinuePatch
{
    private static bool Prefix(EAIDestroyArea __instance, ref bool __result)
    {
        if (RebirthZombieDestroyAreaRuntimePolicy.Enabled)
            return true;

        // Continue is called only for an active DestroyArea task. Clearing this
        // task's path lets the normal target-pursuit AI request a fresh route.
        RebirthZombieDestroyAreaRuntimePolicy.ClearDestroyAreaState(__instance, true);
        __result = false;
        return false;
    }
}

[Preserve]
public sealed class RebirthZombieDestroyAreaModApi : IModApi
{
    private static bool initialized;

    public void InitMod(Mod modInstance)
    {
        if (initialized)
            return;

        initialized = true;
        Harmony harmony = new Harmony("rebirth.zombie.destroyarea.option");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthZombieDestroyAreaCanExecutePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthZombieDestroyAreaContinuePatch));
    }
}
