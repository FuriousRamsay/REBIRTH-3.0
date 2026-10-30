using System;
using System.Collections.Generic;
using UnityEngine;

internal static class RebirthPurgePlayerWraps
{
    private const string PlanKey = "RebirthPurgeStarterWrapItems";
    private const string ArmedKey = "rbPurgeStarterWrapsArmed";
    private static World lastWorld;
    private static float nextAttempt;
    private static bool spaceNoticeShown;
    private static string Receipt(string item) => "rbPurgeStarterWrap_" + item;

    private static bool ValidItem(string name)
    {
        if (RebirthCamperWrapCatalog.FromItem(name) == null) return false;
        var item = ItemClass.GetItem(name, false);
        return item != null && !item.IsEmpty() && item.ItemClass != null;
    }

    // Called only for a newly created authoritative Purge starter, before publication.
    internal static void Prepare(EntityVehicle truck, string owner)
    {
        if (!RebirthPurgeTruckStart.Enabled || truck?.world == null || truck.world.IsRemote()) return;
        var item = truck.vehicle?.GetUpdatedItemValue();
        if (item == null || item.CosmeticModCount != 1) return;
        if (item.TryGetMetadata(PlanKey, out string existing) && !string.IsNullOrEmpty(existing)) return;
        string[] plan = RebirthPurgePlayerWrapAssignments.Select(
            RebirthPurgePlayerWrapAssignments.ForPlayer(owner), ValidItem, new System.Random(Guid.NewGuid().GetHashCode()));
        if (plan.Length == 0) return; // Keep the existing random dye.
        item.SetCosmeticMod(0, ItemClass.GetItem(plan[0], false));
        item.SetMetadata(PlanKey, string.Join(";", plan));
        truck.vehicle.SetItemValue(item);
    }

    // Like background starter items, backpack mutations run on the owning client.
    // Native item metadata carries the fixed server selection; native player CVars
    // retain pending/completed delivery across ordinary saves and reconnections.
    internal static void Tick(bool immediate = false)
    {
        if (!RebirthPurgeTruckStart.Enabled || GameManager.IsDedicatedServer) return;
        var manager = GameManager.Instance;
        var world = manager?.World;
        if (!ReferenceEquals(lastWorld, world))
        { lastWorld = world; nextAttempt = 0; spaceNoticeShown = false; }
        if (world == null || !immediate && Time.realtimeSinceStartup < nextAttempt) return;
        nextAttempt = Time.realtimeSinceStartup + 2f;
        var player = world.GetPrimaryPlayer();
        if (player == null || !player.IsSpawned() || player.IsDead() || player.Buffs == null || player.bag == null
            || RebirthCharacterCreationHoldService.IsHeld(player) || RebirthBackpackLibraryReservation.IsHeld(player)) return;
        string owner = manager.GetPersistentLocalPlayer()?.PrimaryId?.CombinedString
            ?? manager.getPersistentPlayerID(null)?.CombinedString;
        string[] assignments = RebirthPurgePlayerWrapAssignments.ForPlayer(owner);
        if (assignments.Length == 0) return;

        float armed = player.Buffs.GetCustomVar(ArmedKey);
        if (armed == 0)
        {
            var truck = player.AttachedToEntity as EntityVehicle;
            var item = truck?.vehicle?.GetUpdatedItemValue();
            if (item == null || !item.TryGetMetadata("RebirthPurgeStarterOwner", out string truckOwner)
                || !string.Equals(owner, truckOwner, StringComparison.Ordinal)
                || !item.TryGetMetadata(PlanKey, out string text) || string.IsNullOrEmpty(text)) return;
            string[] plan = text.Split(';');
            var allowed = new HashSet<string>(assignments, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in plan) if (!allowed.Contains(name) || !seen.Add(name)) return;
            // Arm extras once, without resetting completed or uncertain receipts.
            for (int i = 1; i < plan.Length; i++)
                if (player.Buffs.GetCustomVar(Receipt(plan[i])) == 0)
                    player.Buffs.SetCustomVar(Receipt(plan[i]), 1f, true);
            player.Buffs.SetCustomVar(ArmedKey, 1f, true);
            armed = 1;
        }
        if (armed != 1f) return;
        bool needsSpace = false;
        foreach (string name in assignments)
        {
            string receipt = Receipt(name);
            if (player.Buffs.GetCustomVar(receipt) != 1f || !ValidItem(name)) continue;
            var transfer = new ItemStack(ItemClass.GetItem(name, false), 1);
            if (!player.bag.CanStack(transfer)) { needsSpace = true; continue; }
            try
            {
                // Listener exceptions may occur after native mutation. An uncertain
                // receipt deliberately blocks replay instead of duplicating an item.
                player.Buffs.SetCustomVar(receipt, -1f, true);
                bool inserted = player.bag.AddItem(transfer);
                player.Buffs.SetCustomVar(receipt, inserted || transfer.count == 0 ? 2f : 1f, true);
            }
            catch (Exception e) { Log.Warning("[PurgeStarterWraps] Delivery requires review: " + name + ": " + e.Message); }
        }
        if (needsSpace && !spaceNoticeShown)
        {
            spaceNoticeShown = true;
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthStarterItemsNeedSpace"), true, false, 5f);
        }
    }
}
