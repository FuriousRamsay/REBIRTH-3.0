using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using HarmonyLib;

// Standalone, optional, UNINSTALLED readonly prototype. Never install legacy decrement/reconcile observers alongside it.
internal static class RebirthExternalTreatmentConsumptionCallsite
{
    private const string NativeAssembly = "FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705";
    private const string NativeMvid = "1a9a4203-3d95-4c90-b094-8926dec1ee9c";
    private const string NativeIl = "70C237A6AEF6E8B451FADCD079F6CAA17DB5E25047E9CEC938B761154FC0AFF3";
    internal sealed class Frame
    {
        internal Frame Previous;
        internal ItemActionUseOther Action;
        internal ItemActionData Data;
        internal RebirthExternalTreatmentConsumptionObservation.Token Token;
        internal int Calls;
        internal bool Returned;
    }
    [ThreadStatic] private static Frame current;
    private static readonly MethodInfo Decrement = AccessTools.Method(typeof(Inventory), nameof(Inventory.DecHoldingItem), new[] { typeof(int) });
    private static readonly MethodInfo Shim = AccessTools.Method(typeof(RebirthExternalTreatmentConsumptionCallsite), nameof(Invoke));
    private static string Digest(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
    }
    internal static bool IsPinnedNative(MethodBase method)
    {
        try
        {
            if (method == null || method.DeclaringType != typeof(ItemActionUseOther) || method.Name != nameof(ItemActionUseOther.ExecuteAction) ||
                method.MetadataToken != 0x0600314C || method.Module.ModuleVersionId.ToString() != NativeMvid) return false;
            var parameters = method.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(ItemActionData) || parameters[1].ParameterType != typeof(bool)) return false;
            var body = method.GetMethodBody();
            byte[] il = body?.GetILAsByteArray();
            if (il == null || il.Length != 796 || body.ExceptionHandlingClauses.Count != 0 || Digest(il) != NativeIl) return false;
            using (var stream = File.OpenRead(method.Module.Assembly.Location))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty) == NativeAssembly;
        }
        catch (Exception) { return false; }
    }
    private static Dictionary<Label, int> Labels(List<CodeInstruction> instructions)
    {
        var targets = new Dictionary<Label, int>();
        for (int i = 0; i < instructions.Count; i++) foreach (Label label in instructions[i].labels) targets.Add(label, i);
        return targets;
    }
    private static string Operand(object operand, Dictionary<Label, int> labels)
    {
        if (operand == null) return "null";
        if (operand is Label) return "target:" + labels[(Label)operand].ToString(CultureInfo.InvariantCulture);
        if (operand is Label[])
        {
            var values = new List<string>();
            foreach (Label label in (Label[])operand) values.Add(labels[label].ToString(CultureInfo.InvariantCulture));
            return "targets:" + string.Join(",", values);
        }
        var member = operand as MemberInfo;
        if (member != null) return "member:" + member.Module.ModuleVersionId + ":" + member.MetadataToken.ToString(CultureInfo.InvariantCulture);
        var local = operand as LocalBuilder;
        if (local != null) return "local:" + local.LocalIndex + ":" + local.LocalType.AssemblyQualifiedName;
        if (operand is string) return "string:" + (string)operand;
        if (operand is float) return "floatbits:" + BitConverter.ToString(BitConverter.GetBytes((float)operand));
        if (operand is double) return "doublebits:" + BitConverter.ToString(BitConverter.GetBytes((double)operand));
        if (operand is byte || operand is sbyte || operand is short || operand is ushort || operand is int || operand is long)
            return operand.GetType().FullName + ":" + Convert.ToString(operand, CultureInfo.InvariantCulture);
        throw new InvalidOperationException("Unknown incoming native instruction operand.");
    }
    internal static bool SameIncoming(List<CodeInstruction> incoming, List<CodeInstruction> original)
    {
        try
        {
            if (incoming == null || original == null || incoming.Count != original.Count) return false;
            var left = Labels(incoming); var right = Labels(original);
            for (int i = 0; i < incoming.Count; i++)
            {
                if (incoming[i].opcode != original[i].opcode || incoming[i].labels.Count != original[i].labels.Count ||
                    incoming[i].blocks.Count != 0 || original[i].blocks.Count != 0 ||
                    Operand(incoming[i].operand, left) != Operand(original[i].operand, right)) return false;
            }
            return true;
        }
        catch (Exception) { return false; }
    }
    // Pure rewrite core is fixture-accessible; production entry additionally requires the pinned installed method.
    internal static IEnumerable<CodeInstruction> RewriteUnchangedOrExact(List<CodeInstruction> incoming, List<CodeInstruction> original)
    {
        try
        {
            if (!SameIncoming(incoming, original)) return incoming;
            int index = -1;
            for (int i = 0; i < incoming.Count; i++)
            {
                if (!Equals(incoming[i].operand, Decrement)) continue;
                if (index >= 0 || incoming[i].opcode != OpCodes.Callvirt) return incoming;
                index = i;
            }
            if (index < 3 || incoming[index].labels.Count != 0 || incoming[index].blocks.Count != 0 ||
                incoming[index - 1].opcode != OpCodes.Ldc_I4_1 || incoming[index - 2].opcode != OpCodes.Ldfld ||
                !Equals(incoming[index - 2].operand, AccessTools.Field(typeof(EntityAlive), nameof(EntityAlive.inventory))) ||
                incoming[index - 3].opcode != OpCodes.Ldloc_0 || index + 1 >= incoming.Count || incoming[index + 1].opcode != OpCodes.Pop ||
                Decrement.ReturnType != typeof(bool)) return incoming;
            var output = new List<CodeInstruction>(incoming.Count + 2);
            for (int i = 0; i < incoming.Count; i++)
            {
                if (i == index)
                {
                    output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    output.Add(new CodeInstruction(OpCodes.Ldarg_1));
                    output.Add(new CodeInstruction(OpCodes.Call, Shim));
                }
                else output.Add(new CodeInstruction(incoming[i]));
            }
            return output;
        }
        catch (Exception) { return incoming; }
    }
    internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase originalMethod, ILGenerator generator)
    {
        var incoming = new List<CodeInstruction>(instructions);
        try
        {
            if (!IsPinnedNative(originalMethod)) return incoming;
            return RewriteUnchangedOrExact(incoming, PatchProcessor.GetOriginalInstructions(originalMethod, out var baselineGenerator));
        }
        catch (Exception) { return incoming; }
    }
    internal static Frame Begin(ItemActionUseOther action, ItemActionData data, bool released, bool ranOriginal)
    {
        var frame = new Frame { Previous = current, Action = action, Data = data };
        current = frame;
        try { frame.Token = RebirthExternalTreatmentConsumptionObservation.Begin(action, data, released, ranOriginal); }
        catch (Exception) { }
        return frame;
    }
    private static bool AfterMatches(RebirthExternalTreatmentConsumptionObservation.Token token, Inventory inventory)
    {
        if (token == null || token.Invalid || !token.EligibleClear || !ReferenceEquals(inventory, token.Inventory) ||
            token.Scope == null || !token.Scope.IsCurrent(token.Healer, token.Patient) ||
            !ReferenceEquals(token.Data.invData, token.Source) || !ReferenceEquals(token.Healer.inventory, token.Inventory) ||
            !ReferenceEquals(token.Healer.Hand, token.Hand) || token.Hand.mode != Hand.HoldingMode.Current ||
            !ReferenceEquals(token.Hand.toolbelt, inventory) || inventory.SelectedSlot != token.Slot ||
            !ReferenceEquals(inventory.ItemGrid.items, token.Cells) || !ReferenceEquals(token.Cells[token.Slot], token.Cell) ||
            !ReferenceEquals(token.Source.stack, token.Cell) || !ReferenceEquals(token.Cell.itemValue, token.Value) ||
            !ReferenceEquals(token.Hand.slotDatas[token.Slot], token.Source) || token.Cell.count != 0) return false;
        string encoded = RebirthNativeItemCodec.Encode(token.Value);
        if (encoded.Length > 262144 || encoded != token.ExpectedClear) return false;
        for (int i = 0; i < token.Cells.Length; i++)
        {
            if (i == token.Slot) continue;
            if (!ReferenceEquals(token.Cells[i], token.OriginalCells[i]) || token.Cells[i].count != token.OriginalCounts[i] ||
                RebirthNativeItemCodec.Encode(token.Cells[i].itemValue) != token.OriginalValues[i]) return false;
        }
        token.MutationValue = encoded;
        return true;
    }
    internal static bool Invoke(Inventory inventory, int count, ItemActionUseOther action, ItemActionData data)
    {
        Frame frame = current;
        RebirthExternalTreatmentConsumptionObservation.Token token = null;
        try
        {
            if (frame != null && ReferenceEquals(frame.Action, action) && ReferenceEquals(frame.Data, data))
            {
                frame.Calls++;
                token = RebirthExternalTreatmentConsumptionObservation.DecrementEntry(inventory, count, true);
                if (!ReferenceEquals(token, frame.Token) || frame.Calls != 1) { if (frame.Token != null) frame.Token.Invalid = true; token = null; }
            }
        }
        catch (Exception) { if (frame?.Token != null) frame.Token.Invalid = true; }
        // Exactly the native call, outside all observer catches. Null/count/return/exception semantics unchanged.
        bool result = inventory.DecHoldingItem(count);
        try
        {
            if (frame != null) frame.Returned = true;
            if (token != null)
            {
                RebirthExternalTreatmentConsumptionObservation.DecrementReturn(token, true, result);
                token.MutationSeen = result && AfterMatches(token, inventory);
                if (!token.MutationSeen) token.Invalid = true;
            }
        }
        catch (Exception) { if (frame?.Token != null) frame.Token.Invalid = true; }
        return result;
    }
    internal static void Returned(Frame frame, bool ranOriginal)
    {
        if (frame?.Token != null) RebirthExternalTreatmentConsumptionObservation.ActionReturn(frame.Token, ranOriginal);
    }
    internal static void Finish(Frame frame, Exception exception)
    {
        if (frame == null) return;
        try
        {
            if (frame.Token != null)
            {
                if (!ReferenceEquals(current, frame) || frame.Calls != 1 || !frame.Returned) frame.Token.Invalid = true;
                RebirthExternalTreatmentConsumptionObservation.Finish(frame.Token, exception);
            }
        }
        catch (Exception) { if (frame.Token != null) frame.Token.Invalid = true; }
        finally { if (ReferenceEquals(current, frame)) current = frame.Previous; else current = null; }
    }
}
[HarmonyPatch(typeof(ItemActionUseOther), nameof(ItemActionUseOther.ExecuteAction))]
internal static class RebirthExternalTreatmentConsumptionCallsitePatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod, ILGenerator generator)
        => RebirthExternalTreatmentConsumptionCallsite.Transpile(instructions, __originalMethod, generator);
    private static void Prefix(ItemActionUseOther __instance, ItemActionData _actionData, bool _bReleased, bool __runOriginal,
        out RebirthExternalTreatmentConsumptionCallsite.Frame __state)
    {
        __state = null;
        try { __state = RebirthExternalTreatmentConsumptionCallsite.Begin(__instance, _actionData, _bReleased, __runOriginal); }
        catch (Exception) { }
    }
    private static void Postfix(bool __runOriginal, RebirthExternalTreatmentConsumptionCallsite.Frame __state)
    {
        try { RebirthExternalTreatmentConsumptionCallsite.Returned(__state, __runOriginal); }
        catch (Exception) { if (__state?.Token != null) __state.Token.Invalid = true; }
    }
    private static void Finalizer(Exception __exception, RebirthExternalTreatmentConsumptionCallsite.Frame __state)
    {
        try { RebirthExternalTreatmentConsumptionCallsite.Finish(__state, __exception); }
        catch (Exception) { if (__state?.Token != null) __state.Token.Invalid = true; }
    }
}
