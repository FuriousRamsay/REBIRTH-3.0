using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

internal static partial class RebirthRepairOriginalCorrection
{
    private sealed class PaidCancelOverflow
    {
        internal Client Client;
        internal XUiC_RecipeStack Card;
        internal PaidCancelOverflow Previous;
        internal XElement Drops;
        internal bool Invalid;
        internal int KitType, PaidKits, CarriedKitsBefore;
    }

    [ThreadStatic] private static PaidCancelOverflow activePaidCancel;

    private static PaidCancelOverflow BeginCancelOverflow(Client client, XUiC_RecipeStack card)
    {
        var attempt = new PaidCancelOverflow { Client = client, Card = card, Previous = activePaidCancel };
        var input = ReadItem(client.Receipt.Input);
        if (input.TryGetMetadata(PaidKitTypeKey, out attempt.KitType) &&
            input.TryGetMetadata(PaidKitCountKey, out attempt.PaidKits))
            attempt.CarriedKitsBefore = NativeKitCount(client.Player, attempt.KitType);
        else attempt.Invalid = true;
        activePaidCancel = attempt;
        return attempt;
    }

    private static bool ObserveCancelKitReturn(PaidCancelOverflow attempt)
    {
        if (attempt.Invalid) return false;
        var kits = attempt.Drops?.Element("kits");
        if (kits == null) return true;
        int carried = NativeKitCount(attempt.Client.Player, attempt.KitType) - attempt.CarriedKitsBefore;
        if (carried < 0 || carried + (int)kits.Attribute("count") != attempt.PaidKits) return false;
        kits.SetAttributeValue("carried", carried);
        return true;
    }

    internal static void EndCancelOverflow(object state)
    {
        if (state is PaidCancelOverflow attempt && ReferenceEquals(activePaidCancel, attempt))
            activePaidCancel = attempt.Previous;
    }

    // These are the two existing native overflow calls inside HandleOnPress.
    internal static void DropCancelTool(GameManager game, ItemStack stack, Vector3 position, Vector3 random,
        int owner, float lifetime, bool relative, XUiC_RecipeStack card)
        => DropCancelItem(game, stack, position, random, owner, lifetime, relative, card, "tool");

    internal static void DropCancelKits(GameManager game, ItemStack stack, Vector3 position, Vector3 random,
        int owner, float lifetime, bool relative, XUiC_RecipeStack card)
        => DropCancelItem(game, stack, position, random, owner, lifetime, relative, card, "kits");

    private static void DropCancelItem(GameManager game, ItemStack stack, Vector3 position, Vector3 random,
        int owner, float lifetime, bool relative, XUiC_RecipeStack card, string leg)
    {
        var attempt = activePaidCancel;
        if (!ReferenceEquals(attempt?.Card, card))
        {
            game.ItemDropServer(stack, position, random, owner, lifetime, relative);
            return;
        }

        // The paid receipt will send the exact native remainder with the
        // already existing Cancel result. There is no independent client drop.
        try
        {
            var client = attempt.Client;
            if (!Current(client) || owner != client.Player.entityId || stack == null || stack.IsEmpty() ||
                random != Vector3.zero || relative || lifetime != 120f)
            {
                attempt.Invalid = true;
                return;
            }
            attempt.Drops ??= new XElement("cancelOverflow", new XAttribute("x", position.x),
                new XAttribute("y", position.y), new XAttribute("z", position.z));
            if (attempt.Drops.Element(leg) != null || DropPosition(attempt.Drops) != position)
            {
                attempt.Invalid = true;
                return;
            }
            attempt.Drops.Add(new XElement(leg, new XAttribute("count", stack.count),
                new XAttribute("state", 0), Image(stack.itemValue)));
        }
        catch { attempt.Invalid = true; }
    }

    private static Vector3 DropPosition(XElement node) =>
        new Vector3((float)node.Attribute("x"), (float)node.Attribute("y"), (float)node.Attribute("z"));

    private static bool ValidCancelOverflow(Receipt receipt)
    {
        var overflow = receipt.CancelOverflow;
        if (!receipt.Cancelled || !receipt.CancelReturned || !HasIndependentPayment(receipt) ||
            receipt.CancelInput.Length == 0 || overflow.Attributes().Count() != 3 ||
            overflow.Elements().Count() is < 1 or > 2)
            return false;

        var position = DropPosition(overflow);
        if (float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude))
            return false;
        var input = ReadItem(receipt.Input);
        if (!input.TryGetMetadata(PaidKitTypeKey, out int kitType) ||
            !input.TryGetMetadata(PaidKitCountKey, out int paidKits))
            return false;
        var seen = new HashSet<XName>();
        foreach (var leg in overflow.Elements())
        {
            var carried = (int?)leg.Attribute("carried");
            if (!seen.Add(leg.Name) || leg.HasElements || leg.Attributes().Count() != (carried.HasValue ? 3 : 2) ||
                (int?)leg.Attribute("count") is not int count || count <= 0 ||
                (int?)leg.Attribute("state") is not int state || state < 0 || state > 2 ||
                receipt.CancelConfirmed && state != 2)
                return false;
            if (leg.Name == "tool")
            {
                if (carried.HasValue || count != 1 || leg.Value != receipt.CancelInput) return false;
            }
            else if (leg.Name == "kits")
            {
                var kit = ReadItem(leg.Value);
                var nativeKit = new ItemValue(kitType);
                nativeKit.Seed = kit.Seed;
                if (count > paidKits || carried.HasValue && (carried.Value < 0 || carried.Value != paidKits - count) ||
                    Image(nativeKit) != leg.Value) return false;
            }
            else return false;
        }
        return true;
    }

    private sealed class CancelDropBudget : IRebirthStationSnapshotByteBudget
    {
        public int MaximumSnapshotBytes => RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
        public bool TryReserveAllocation(long bytes, out IDisposable reservation)
            => RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes, out reservation);
    }

    private sealed class CancelDropWatch
    {
        internal EntityPlayer Owner;
        internal EntityItem Item;
        internal XName Leg;
        internal RebirthCookingSavedDropObservation Save;
    }

    private static readonly CancelDropBudget cancelDropBudget = new CancelDropBudget();
    private static readonly Dictionary<Guid, CancelDropWatch> cancelDropWatches = new Dictionary<Guid, CancelDropWatch>();

    private static void ClearCancelDropWatches()
    {
        foreach (var watch in cancelDropWatches.Values) watch.Save?.Dispose();
        cancelDropWatches.Clear();
    }

    private static void PruneCancelDropWatches(World world)
    {
        if (cancelDropWatches.Count == 0) return;
        foreach (var pair in cancelDropWatches.ToArray())
        {
            var watch = pair.Value;
            if (ReferenceEquals(world.GetEntity(watch.Owner.entityId), watch.Owner) && !watch.Owner.IsDead() &&
                ReferenceEquals(world.GetEntity(watch.Item.entityId), watch.Item)) continue;
            watch.Save?.Dispose();
            cancelDropWatches.Remove(pair.Key);
        }
    }

    private static bool SameCancelDrops(XElement original, XElement request)
    {
        if (original == null || request == null) return original == request;
        var copy = new XElement(original);
        foreach (var leg in copy.Elements()) leg.SetAttributeValue("state", 0);
        return XNode.DeepEquals(copy, request);
    }

    private static bool HasReturnedRepairTool(EntityPlayer owner, Receipt receipt)
    {
        if (!TryRepairIdentity(owner, out var identity) || !ReadSaved(identity, out var actor, out var saved))
            return true;
        var equipment = ReadEquipment(saved);
        return equipment == null || actor.Bag.Concat(actor.Belt).Concat(equipment).Append(actor.Cursor)
            .Any(stack => stack != null && !stack.IsEmpty() && SameOperation(stack.itemValue, receipt.Operation, OriginalKey));
    }

    private static bool SettleCancelOverflow(EntityPlayer owner, Receipt request, ref Receipt original)
    {
        if (original.CancelOverflow == null && request.CancelOverflow == null) return true;
        if (!HasIndependentPayment(original) || request.CancelOverflow == null ||
            request.CancelOverflow.Elements().Any(leg => (int)leg.Attribute("state") != 0) ||
            original.CancelInput != request.CancelInput || original.PaymentProof != request.PaymentProof ||
            original.NativeDelivered || original.OutputInvoking)
            return false;

        if (original.CancelOverflow == null)
        {
            if (request.CancelOverflow.Element("tool") != null && HasReturnedRepairTool(owner, original))
                return false;
            var prepared = Decode(Encode(original));
            prepared.Cancelled = prepared.CancelReturned = true;
            prepared.CancelOverflow = new XElement(request.CancelOverflow);
            if (!Save(prepared, true, Encode(original))) return false;
            original = prepared;
        }
        if (!SameCancelDrops(original.CancelOverflow, request.CancelOverflow)) return false;
        var leg = original.CancelOverflow.Elements().FirstOrDefault(value => (int)value.Attribute("state") != 2);
        if (leg == null) return true;

        var operation = original.Operation;
        var world = owner.world;
        int life = original.Deaths;
        string creation = original.Creation;
        bool Current() => ReferenceEquals(GameManager.Instance?.World, world) &&
            ReferenceEquals(world.GetEntity(owner.entityId), owner) && !owner.IsDead() &&
            owner.Died == life && RebirthCamperCharacterAccess.CurrentCreation(owner) == creation;
        var offer = new ItemStack(ReadItem(leg.Value), (int)leg.Attribute("count"));
        if ((int)leg.Attribute("state") == 0)
        {
            if (cancelDropWatches.Count >= RebirthStationSnapshotEvidence.MaximumRequests) return false;
            var issued = Decode(Encode(original));
            issued.CancelOverflow.Element(leg.Name).SetAttributeValue("state", 1);
            string before = Encode(original);
            bool saved = false;
            bool RememberInvocation() => saved = Save(issued, true, before);
            if (RebirthCookingRefundDropObservation.TryDrop(owner, offer, Current, out _, out var entity,
                RememberInvocation, DropPosition(original.CancelOverflow)))
                cancelDropWatches.Add(operation, new CancelDropWatch { Owner = owner, Item = entity, Leg = leg.Name });
            if (!saved) return false;
            original = issued;
        }
        if (!cancelDropWatches.TryGetValue(operation, out var watch) || !ReferenceEquals(watch.Owner, owner) ||
            watch.Leg != leg.Name) return false;
        if (!Current() || !ReferenceEquals(world.GetEntity(watch.Item.entityId), watch.Item))
        {
            watch.Save?.Dispose();
            cancelDropWatches.Remove(operation);
            return false;
        }
        watch.Save ??= RebirthCookingSavedDropObservation.Begin(owner, watch.Item, offer, Current, cancelDropBudget);
        if (watch.Save?.Saved(out _) != true) return false;
        var published = Decode(Encode(original));
        published.CancelOverflow.Element(leg.Name).SetAttributeValue("state", 2);
        if (!Save(published, true, Encode(original))) return false;
        watch.Save.Dispose();
        cancelDropWatches.Remove(operation);
        original = published;
        return original.CancelOverflow.Elements().All(value => (int)value.Attribute("state") == 2);
    }
}
