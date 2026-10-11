using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

/// <summary>
/// Blocks a selected biome zombie before native spawn counts or asynchronous creation change.
/// The director's group indexes, enabled flags, animal pools and POI director remain native.
/// </summary>
[HarmonyPatch(typeof(SpawnManagerBiomes), "SpawnUpdate",
    new[] { typeof(string), typeof(bool), typeof(ChunkAreaBiomeSpawnData) })]
public static class RebirthPurgeBiomeZombieSpawnPatch
{
    public static bool ShouldSuppress(int entityClassId, SpawnManagerBiomes manager)
    {
        if (!RebirthPurgeReleasePolicy.Enabled ||
            !RebirthSandboxOptionManager.Current.IsPurge ||
            manager == null || !RebirthSpawnCompositionRuntimeIntegration.IsAuthoritative(manager.world))
            return false;

        EntityClass definition = EntityClass.GetEntityClass(entityClassId);
        // Animal subclasses retain normal biome behavior, including hostile wildlife.
        return definition != null && !definition.bIsAnimalEntity &&
            definition.classname != null && typeof(EntityZombie).IsAssignableFrom(definition.classname);
    }

    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var nativeSelection = AccessTools.Method(typeof(EntityGroups),
            nameof(EntityGroups.GetRandomFromGroup),
            new[] { typeof(string), typeof(int).MakeByRefType(), typeof(GameRandom) });
        var predicate = AccessTools.Method(typeof(RebirthPurgeBiomeZombieSpawnPatch),
            nameof(ShouldSuppress));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(nativeSelection)) continue;
            matches++;
            Label proceed = generator.DefineLabel();
            // Preserve the real selected ID for the native path. Never manufacture the
            // native "none" ID: that branch permanently decrements the group's allowance.
            yield return new CodeInstruction(OpCodes.Dup);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, predicate);
            yield return new CodeInstruction(OpCodes.Brfalse, proceed);
            yield return new CodeInstruction(OpCodes.Pop);
            yield return new CodeInstruction(OpCodes.Ret);
            var continuation = new CodeInstruction(OpCodes.Nop);
            continuation.labels.Add(proceed);
            yield return continuation;
        }
        if (matches != 1)
            throw new InvalidOperationException("Purge biome suppression expected one native entity selection, found " + matches);
    }
}