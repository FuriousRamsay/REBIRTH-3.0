using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

[HarmonyPatch(typeof(XmlPatchConditionEvaluator), nameof(XmlPatchConditionEvaluator.xpath))]
internal static class RebirthXPathConditionLoggingPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        var log = ((Action<string>)Log.Out).Method;
        var messages = code.FindAll(instruction => instruction.Calls(log));
        if (messages.Count != 3) return code;

        // b10 logs the filename, match count, and a single match from this
        // evaluator. Keep the XPath evaluation and its error paths native.
        foreach (var message in messages)
        {
            message.opcode = OpCodes.Pop;
            message.operand = null;
        }
        return code;
    }
}
