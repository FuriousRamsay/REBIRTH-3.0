using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

// Explosion.AttackBlocks bypasses Block.DamageBlock. Filter the native damage integer
// before native destruction hooks, loot side effects and the final block-change batch.
internal static class RebirthWorkstationExplosionProtection
{
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Explosion), nameof(Explosion.AttackBlocks)),
            transpiler: new HarmonyMethod(typeof(RebirthWorkstationExplosionProtection), nameof(Transpile)));
    }
    private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> source, MethodBase __originalMethod)
    {
        var code = source.ToList();
        var changeLocals = __originalMethod.GetMethodBody().LocalVariables
            .Where(v => v.LocalType == typeof(BlockChangeInfo)).ToArray();
        if (changeLocals.Length != 1) throw new InvalidOperationException("Explosion block-change local is not qualified.");
        int matches = 0;
        var blockValue = AccessTools.Field(typeof(BlockChangeInfo), nameof(BlockChangeInfo.blockValue));
        for (int i = 0; i + 5 < code.Count; i++)
        {
            // damage > 0, then damage + change.blockValue.damage: exact native mutation gate.
            if (!code[i].IsLdloc() || code[i+1].opcode != OpCodes.Ldc_I4_0 ||
                (code[i+2].opcode != OpCodes.Ble && code[i+2].opcode != OpCodes.Ble_S) ||
                code[i+3].opcode != code[i].opcode || !Equals(code[i+3].operand, code[i].operand) ||
                !code[i+4].IsLdloc() || code[i+5].opcode != OpCodes.Ldflda || !Equals(code[i+5].operand, blockValue)) continue;
            code.InsertRange(i+1, new[] {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc, changeLocals[0].LocalIndex),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RebirthWorkstationExplosionProtection), nameof(Filter))) });
            matches++; i += 3;
        }
        if (matches != 1) throw new InvalidOperationException("Explosion station damage gate is not qualified.");
        return code;
    }
    internal static int Filter(int damage, Explosion explosion, BlockChangeInfo change)
    {
        int ignored = 0;
        return RebirthWorkstationDamagePolicy.BeforeDamage(explosion.world, change.blockValueRef,
            change.blockValue, damage, explosion.entityId, ref ignored) ? damage : 0;
    }
}