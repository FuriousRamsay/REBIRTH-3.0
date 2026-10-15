using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Native 3.3 b18 terrain recovery logs transform.parent.name in three branches.
// A root-level camper has no parent. Keep recovery intact and fix only its name lookup.
[HarmonyPatch(typeof(EntityVehicle), "CheckForOutOfWorld")]
internal static class RebirthCamperTerrainLog
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var parentGetter = AccessTools.PropertyGetter(typeof(Transform), "parent");
        var nameGetter = AccessTools.PropertyGetter(typeof(UnityEngine.Object), "name");
        var safeName = AccessTools.Method(typeof(RebirthCamperTerrainLog), nameof(ParentName));
        int matches = 0;
        for (int index = 1; index < code.Count; index++)
            if (code[index - 1].Calls(parentGetter) && code[index].Calls(nameGetter)) matches++;
        if (matches != 3)
            throw new InvalidOperationException("Camper terrain logging patch requires the three audited native parent-name calls.");
        for (int index = 0; index < code.Count; index++)
        {
            if (index > 0 && code[index - 1].Calls(parentGetter) && code[index].Calls(nameGetter))
            {
                var vehicle = new CodeInstruction(OpCodes.Ldarg_0);
                vehicle.labels.AddRange(code[index].labels);
                vehicle.blocks.AddRange(code[index].blocks);
                yield return vehicle;
                yield return new CodeInstruction(OpCodes.Call, safeName);
            }
            else yield return code[index];
        }
    }

    private static string ParentName(Transform parent, EntityVehicle vehicle)
    {
        if (parent != null) return parent.name;
        bool camper = vehicle != null && EntityClass.list[vehicle.entityClass].entityClassName == "rebirthCamper80s";
#if REBIRTH_CAMPER_DIAGNOSTICS
        camper &= GamePrefs.GetString(EnumGamePrefs.GameName) == "CodexTest";
#endif
        if (camper) return "rebirthCamper80s (root)";
        // Preserve other vehicles' original behavior rather than broadening this fix.
        return parent.name;
    }
}
