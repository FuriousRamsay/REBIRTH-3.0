using System;
using HarmonyLib;

// Read-only local witness candidate. No installer, credit, effect, refund, save or remote authority.
internal static class RebirthExternalTreatmentConsumptionObservation
{
    internal enum Outcome { Refused, Indeterminate, ObservedConsumed, ObservedCellConsumptionDuringAction }
    internal sealed class Token
    {
        internal Token Previous;
        internal ItemActionUseOther Action;
        internal ItemActionData Data;
        internal ItemInventoryData Source;
        internal EntityPlayer Healer, Patient;
        internal Inventory Inventory;
        internal Hand Hand;
        internal ItemStack[] Cells;
        internal ItemStack Cell;
        internal ItemValue Value;
        internal int Slot, BeforeCount;
        internal string BeforeValue, ExpectedClear, MutationValue;
        internal ItemStack[] OriginalCells;
        internal string[] OriginalValues;
        internal int[] OriginalCounts;
        internal RebirthExternalTreatmentEvidenceScope Scope;
        internal bool InitialConsume;
        internal bool EligibleClear, Invalid, ActionReturned, ActionRan, DecrementReturned, DecrementRan, MutationSeen;
        internal int Decrements, Reconciles;
        internal Outcome Result = Outcome.Indeterminate;
    }
    [ThreadStatic] private static Token active;
    private const int EncodedLimit = 262144;

    private static string Encode(ItemValue value)
    {
        string encoded = RebirthNativeItemCodec.Encode(value);
        // Bound the retained payload, not the codec's preceding allocation.
        if (encoded == null || encoded.Length > EncodedLimit) throw new InvalidOperationException("Consumption observation payload unavailable.");
        return encoded;
    }
    internal static Token Begin(ItemActionUseOther action, ItemActionData data, bool released, bool ranOriginal)
    {
        var feed = data as ItemActionUseOther.FeedInventoryData;
        EntityPlayer healer = data?.invData?.holdingEntity as EntityPlayer;
        EntityPlayer patient = feed?.TargetEntity as EntityPlayer;
        var token = new Token { Previous = active, Action = action, Data = data, Source = data?.invData, Healer = healer, Patient = patient };
        active = token; // Every nested UseOther entry masks the prior token, including excluded calls.
        try
        {
        if (!released || !ranOriginal || action == null || healer == null || patient == null || ReferenceEquals(healer, patient) ||
            !RebirthExternalTreatmentTargetPolicy.IsExternalTreatment(data.invData.itemValue?.ItemClass?.GetItemName()))
        {
            token.Invalid = true;
            token.Result = Outcome.Refused;
            return token;
        }
        token.InitialConsume = action.Consume;
            token.Scope = RebirthExternalTreatmentEvidenceScope.Capture(healer, patient);
            token.Inventory = healer.inventory;
            token.Hand = healer.Hand;
            token.Slot = token.Source.toolbeltSlotIdx;
            token.Cells = token.Inventory?.ItemGrid?.items;
            token.Cell = token.Source.stack;
            token.Value = token.Cell?.itemValue;
            token.BeforeCount = token.Cell?.count ?? 0;
            if (token.Scope == null || !Bound(token) || token.Value?.ItemClass == null || token.BeforeCount < 1 ||
                float.IsNaN(token.Value.UseTimes) || float.IsInfinity(token.Value.UseTimes)) { token.Invalid = true; return token; }
            token.BeforeValue = Encode(token.Value);
            token.OriginalCells = (ItemStack[])token.Cells.Clone();
            token.OriginalValues = new string[token.Cells.Length];
            token.OriginalCounts = new int[token.Cells.Length];
            for (int i = 0; i < token.Cells.Length; i++)
            {
                if (token.Cells[i] == null) { token.Invalid = true; return token; }
                token.OriginalCounts[i] = token.Cells[i].count;
                token.OriginalValues[i] = Encode(token.Cells[i].itemValue);
            }
            // Candidate eligibility only: native Clear notifications can precede Reconcile.
            // Reusable degradation and nonempty reset paths grant no positive proof.
            token.EligibleClear = action.Consume && token.BeforeCount == 1 &&
                !(token.Value.MaxUseTimes > 0 && token.Value.UseTimes + 1f < token.Value.MaxUseTimes);
            if (token.EligibleClear)
            {
                ItemValue detached;
                if (!RebirthNativeItemCodec.TryDecode(token.BeforeValue, out detached)) { token.Invalid = true; return token; }
                detached.Clear();
                token.ExpectedClear = Encode(detached);
            }
        }
        catch (Exception) { token.Invalid = true; }
        return token;
    }
    private static bool Bound(Token token)
    {
        if (token == null || token.Scope == null || !token.Scope.IsCurrent(token.Healer, token.Patient) ||
            !ReferenceEquals(token.Data.invData, token.Source) || !ReferenceEquals(token.Healer.inventory, token.Inventory) ||
            !ReferenceEquals(token.Healer.Hand, token.Hand) || token.Hand == null || token.Hand.mode != Hand.HoldingMode.Current ||
            !ReferenceEquals(token.Hand.toolbelt, token.Inventory) || token.Inventory.SelectedSlot != token.Slot ||
            token.Cells == null || token.Slot < 0 || token.Slot >= token.Cells.Length ||
            !ReferenceEquals(token.Inventory.ItemGrid.items, token.Cells) || token.Hand.slotDatas == null ||
            token.Slot >= token.Hand.slotDatas.Length || !ReferenceEquals(token.Hand.slotDatas[token.Slot], token.Source) ||
            !ReferenceEquals(token.Source.stack, token.Cell) || !ReferenceEquals(token.Cells[token.Slot], token.Cell) ||
            !ReferenceEquals(token.Cell.itemValue, token.Value)) return false;
        return true;
    }
    private static bool OtherCellsUnchanged(Token token)
    {
        for (int i = 0; i < token.Cells.Length; i++)
        {
            if (i == token.Slot) continue;
            ItemStack cell = token.Cells[i];
            if (!ReferenceEquals(cell, token.OriginalCells[i]) || cell.count != token.OriginalCounts[i] ||
                Encode(cell.itemValue) != token.OriginalValues[i]) return false;
        }
        return true;
    }
    internal static Token DecrementEntry(Inventory inventory, int count, bool ranOriginal)
    {
        Token token = active;
        if (token == null || !ReferenceEquals(inventory, token.Inventory)) return null;
        try
        {
            token.Decrements++;
            if (!ranOriginal || count != 1 || token.Decrements != 1 || token.Invalid || !token.EligibleClear || !Bound(token) ||
                token.Cell.count != token.BeforeCount || Encode(token.Value) != token.BeforeValue || !OtherCellsUnchanged(token)) token.Invalid = true;
        }
        catch (Exception) { token.Invalid = true; }
        return token;
    }
    internal static void ReconcileEntry(Hand hand, bool ranOriginal)
    {
        Token token = active;
        if (token == null || !ReferenceEquals(hand, token.Hand)) return;
        try
        {
            token.Reconciles++;
            if (!ranOriginal || token.Invalid || !token.EligibleClear || token.Decrements != 1 || token.Reconciles != 1 ||
                !Bound(token) || token.Cell.count != 0 || Encode(token.Value) != token.ExpectedClear || !OtherCellsUnchanged(token)) { token.Invalid = true; return; }
            token.MutationValue = Encode(token.Value);
            token.MutationSeen = true;
            token.Result = Outcome.ObservedConsumed;
        }
        catch (Exception) { token.Invalid = true; }
    }
    internal static void DecrementReturn(Token token, bool ranOriginal, bool result)
    {
        if (token == null) return;
        token.DecrementReturned = true;
        token.DecrementRan = ranOriginal && result;
        if (!ranOriginal || !result) token.Invalid = true;
    }
    internal static void DecrementFault(Token token, Exception exception)
    {
        if (token != null && exception != null) token.Invalid = true;
    }
    internal static void ActionReturn(Token token, bool ranOriginal)
    {
        if (token == null) return;
        token.ActionReturned = true;
        token.ActionRan = ranOriginal;
        if (!ranOriginal) token.Invalid = true;
    }
    internal static void Finish(Token token, Exception originalException)
    {
        if (token == null) return;
        try
        {
            bool verified = originalException == null && ReferenceEquals(active, token) && !token.Invalid && token.EligibleClear &&
                token.ActionReturned && token.ActionRan && token.Action.Consume == token.InitialConsume && token.DecrementReturned && token.DecrementRan && token.MutationSeen &&
                Bound(token) && token.Cell.count == 0 && Encode(token.Value) == token.MutationValue && OtherCellsUnchanged(token);
            // Ambient observation does not authenticate the native consumption callsite.
            token.Result = verified ? Outcome.ObservedCellConsumptionDuringAction : Outcome.Indeterminate;
        }
        catch (Exception) { token.Invalid = true; token.Result = Outcome.Indeterminate; }
        finally
        {
            if (ReferenceEquals(active, token)) active = token.Previous;
            else { token.Invalid = true; token.Result = Outcome.Indeterminate; active = null; }
        }
    }
}

// All three optional hooks are uninstalled. Every observer failure preserves native execution.
[HarmonyPatch(typeof(ItemActionUseOther), nameof(ItemActionUseOther.ExecuteAction))]
internal static class RebirthExternalTreatmentConsumptionActionPatch
{
    private static void Prefix(ItemActionUseOther __instance, ItemActionData _actionData, bool _bReleased, bool __runOriginal,
        out RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        __state = null;
        try { __state = RebirthExternalTreatmentConsumptionObservation.Begin(__instance, _actionData, _bReleased, __runOriginal); }
        catch (Exception) { }
    }
    private static void Postfix(bool __runOriginal, RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        try { RebirthExternalTreatmentConsumptionObservation.ActionReturn(__state, __runOriginal); }
        catch (Exception) { if (__state != null) __state.Invalid = true; }
    }
    private static void Finalizer(Exception __exception, RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        try { RebirthExternalTreatmentConsumptionObservation.Finish(__state, __exception); }
        catch (Exception) { if (__state != null) __state.Invalid = true; }
    }
}
[HarmonyPatch(typeof(Inventory), nameof(Inventory.DecHoldingItem))]
internal static class RebirthExternalTreatmentConsumptionDecrementPatch
{
    private static void Prefix(Inventory __instance, int _count, bool __runOriginal,
        out RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        __state = null;
        try { __state = RebirthExternalTreatmentConsumptionObservation.DecrementEntry(__instance, _count, __runOriginal); }
        catch (Exception) { }
    }
    private static void Postfix(bool __runOriginal, bool __result, RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        try { RebirthExternalTreatmentConsumptionObservation.DecrementReturn(__state, __runOriginal, __result); }
        catch (Exception) { if (__state != null) __state.Invalid = true; }
    }
    private static void Finalizer(Exception __exception, RebirthExternalTreatmentConsumptionObservation.Token __state)
    {
        try { RebirthExternalTreatmentConsumptionObservation.DecrementFault(__state, __exception); }
        catch (Exception) { if (__state != null) __state.Invalid = true; }
    }
}
[HarmonyPatch(typeof(Hand), nameof(Hand.Reconcile))]
internal static class RebirthExternalTreatmentConsumptionReconcilePatch
{
    private static void Prefix(Hand __instance, bool __runOriginal)
    {
        try { RebirthExternalTreatmentConsumptionObservation.ReconcileEntry(__instance, __runOriginal); }
        catch (Exception) { }
    }
}



