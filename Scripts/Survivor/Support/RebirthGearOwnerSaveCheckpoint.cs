using System;
using System.IO;
using System.Threading;

// Called by the authenticated owner adapter while its inventory interaction hold
// is active. isCurrent must include that hold, connection and character binding.
internal static class RebirthGearOwnerSaveCheckpoint
{
    internal static bool TryPersist(EntityPlayerLocal player, RebirthGearTransferState offer,
        RebirthGearOwnerReceipt stage, Func<bool> isCurrent)
    {
        if (isCurrent == null || player == null || offer == null ||
            (stage != RebirthGearOwnerReceipt.Applying && stage != RebirthGearOwnerReceipt.Applied && stage != RebirthGearOwnerReceipt.Rejected) ||
            !offer.TryGetPlan(out var plan) ||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)) return false;
        var game = GameManager.Instance;
        var world = player.world;
        int thread = Thread.CurrentThread.ManagedThreadId;
        string root;
        try { root = Path.GetFullPath(GameIO.GetPlayerDataDir()); }
        catch { return false; }
        Func<bool> current = () => Thread.CurrentThread.ManagedThreadId == thread &&
            isCurrent() && game != null && ReferenceEquals(GameManager.Instance, game) &&
            world != null && ReferenceEquals(game.World, world) && ReferenceEquals(player.world, world) &&
            ReferenceEquals(world.GetPrimaryPlayer(), player) && ReferenceEquals(world.GetEntity(player.entityId), player) &&
            player.IsSpawned() && !player.IsDead() && player.Buffs != null &&
            !RebirthCharacterCreationHoldService.IsHeld(player) && RebirthSurvivorMode.IsEnabledForCurrentWorld() &&
            game.getPersistentPlayerID(null)?.CombinedString == identity.CanonicalId &&
            Path.GetFullPath(GameIO.GetPlayerDataDir()) == root;
        try
        {
            if (!current()) return false;
            string receipt = "rbGear_" + offer.TransactionId;
            float previous = player.Buffs.GetCustomVar(receipt);
            // Never downgrade applied, resurrect rejection, or infer a malformed marker.
            if (previous == 1f)
            {
                if (stage != RebirthGearOwnerReceipt.Applied) return false;
                if (RebirthGearPlayerFileWitness.HasApplied(identity, offer)) return current();
                // The mutation finished but its save may have failed: revalidate
                // the live postimage and retry only saving, never inventory writes.
            }
            if (stage == RebirthGearOwnerReceipt.Rejected)
            {
                if (previous != 0f && previous != -1f) return false;
            }
            else if (previous != 0f && previous != 2f && previous != 1f) return false;
            if (stage == RebirthGearOwnerReceipt.Applied && previous != 2f && previous != 1f) return false;
            if (!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,
                player.inventory?.ItemGrid?.items, 4, out var snapshot)) return false;
            if (stage == RebirthGearOwnerReceipt.Applied)
            {
                if (!plan.MatchesAppliedInventory(snapshot.Bag, snapshot.Belt)) return false;
            }
            else if ((previous == 0f || stage == RebirthGearOwnerReceipt.Rejected) && !plan.MatchesBefore(snapshot.Bag, snapshot.Belt, plan.GearBefore,
                snapshot.Bag.Length, snapshot.Belt.Length)) return false;
            if (!current()) return false;
            if (stage == RebirthGearOwnerReceipt.Rejected && previous == -1f &&
                RebirthGearPlayerFileWitness.HasRejected(identity, offer)) return current();
            player.Buffs.SetCustomVar(receipt, (float)stage, true);
            if (!current()) return false;
            // Native Save catches errors internally. Only exact final-file readback
            // can prove durability. Retain uncertain markers, never guess rollback.
            game.SaveLocalPlayerData();
            if (!current()) return false;
            bool saved = stage == RebirthGearOwnerReceipt.Rejected
                ? RebirthGearPlayerFileWitness.HasRejected(identity, offer)
                : stage == RebirthGearOwnerReceipt.Applying
                ? RebirthGearPlayerFileWitness.HasApplying(identity, offer)
                : RebirthGearPlayerFileWitness.HasApplied(identity, offer);
            return saved && current();
        }
        catch { return false; }
    }
}