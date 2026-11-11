using System;
using System.IO;
using System.Runtime.CompilerServices;

// A remote owner writes the selected native cell. The authority publishes the
// Sip slot or serving only after that write succeeds.
internal static class RebirthMetabolismLiveAction
{
    internal enum Kind : byte { Legacy, Serve, Equip, Return, Applied, Refused }
    private const int MaxItemBytes = 1024 * 1024;

    private sealed class OwnerAction
    {
        internal EntityPlayerLocal Player;
        internal INetConnection Connection;
        internal string Creation;
        internal int Deaths, Slot;
        internal Guid Id;
        internal Kind Action;
        internal RebirthMetabolismSourceKind Source;
        internal ItemStack Before;
        internal bool GrantReceived;

        internal bool Current()
        {
            var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return manager?.IsServer == false && manager.connectionToServer?.Length > 0 &&
                ReferenceEquals(manager.connectionToServer[0], Connection) && !Connection.IsDisconnected() &&
                ReferenceEquals(Player.world.GetPrimaryPlayer(), Player) && Player.Died == Deaths &&
                RebirthConsumptionOwnerPublication.TryCaptureCreation(Player, out var creation) && creation == Creation;
        }
    }

    private sealed class AuthorityAction
    {
        internal Guid Id;
        internal INetConnection Connection;
        internal RebirthRemoteResourceNetworkScope Scope;
        internal string Creation;
        internal int Deaths;
        internal Kind Action;
        internal Func<RebirthConsumeResult> Complete;
    }

    private static OwnerAction ownerAction;
    private static readonly ConditionalWeakTable<EntityPlayer, AuthorityAction> authorityActions = new();

    internal static bool Begin(EntityPlayerLocal player, Kind action, RebirthMetabolismSourceKind source, int slot, ItemStack selected = null)
    {
        var manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player?.world?.IsRemote() != true || manager?.IsServer != false || manager.connectionToServer?.Length < 1 ||
            manager.connectionToServer?[0] is not INetConnection connection || connection.IsDisconnected() ||
            !ReferenceEquals(player.world.GetPrimaryPlayer(), player) ||
            !RebirthConsumptionSourceIdentity.TryGetWorldKey(player.world, out _) ||
            !RebirthConsumptionOwnerPublication.TryCaptureCreation(player, out var creation) ||
            RebirthBackpackLibraryReservation.BlocksResourceUse(player) || ownerAction?.Current() == true) return false;

        if (action == Kind.Return) slot = FreeBagSlot(player);
        var before = Cell(player, source, slot);
        if (before == null || (action == Kind.Return ? !before.IsEmpty() : before.IsEmpty()) ||
            (action != Kind.Serve && source == RebirthMetabolismSourceKind.Backpack && Locked(player, slot)) ||
            (selected != null && !Same(before, selected))) return false;

        var pending = new OwnerAction
        {
            Player = player, Connection = connection, Creation = creation, Deaths = player.Died, Id = Guid.NewGuid(),
            Action = action, Source = source, Slot = slot, Before = before.Clone()
        };
        ownerAction = pending;
        try
        {
            manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeRequest>()
                .SetupLive(player, pending.Id, action, source, slot, pending.Before, creation));
            return true;
        }
        catch (Exception error)
        {
            ownerAction = null;
            RebirthDebug.Log("metabolism", "[REBIRTH Metabolism] Live request send failed: " + error.Message);
            return false;
        }
    }

    private static AuthorityAction CurrentAction(EntityPlayer player)
    {
        if (player == null || !authorityActions.TryGetValue(player, out var action)) return null;
        if (action.Scope.Current && player.Died == action.Deaths &&
            RebirthConsumptionOwnerPublication.TryCaptureCreation(player, out var creation) && creation == action.Creation)
            return action;
        authorityActions.Remove(player);
        return null;
    }

    internal static bool HoldsServing(EntityPlayer player) => CurrentAction(player)?.Action == Kind.Serve;
    internal static bool HoldsSipSlot(EntityPlayer player)
    {
        var action = CurrentAction(player);
        return action != null && action.Action != Kind.Serve;
    }

    internal static bool BlocksSupportGear(EntityPlayer player) => HoldsSipSlot(player) ||
        RebirthMetabolismStateRepository.TryGet(player, out var state) && state?.HydrationSlotItem?.IsEmpty() == false;

    internal static void ReceiveRequest(EntityPlayer player, ClientInfo peer, INetConnection connection, Guid id, Kind action,
        RebirthMetabolismSourceKind source, int slot, ItemStack before, string creation)
    {
        void Reply(RebirthConsumeResult result, ItemStack replacement = null, ItemStack siblings = null) =>
            peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>()
                .Setup(result).WithLiveAction(id, replacement, siblings));

        var pending = CurrentAction(player);
        if (action == Kind.Applied || action == Kind.Refused)
        {
            if (pending == null || pending.Id != id || !ReferenceEquals(pending.Connection, connection)) return;
            // Consume the acknowledgement before publishing effects, which can reenter game code.
            authorityActions.Remove(player);
            Reply(action == Kind.Applied ? pending.Complete() : Changed());
            return;
        }

        if (pending != null)
        {
            if (pending.Id != id) Reply(Changed());
            return;
        }
        var scope = RebirthRemoteResourceNetworkScope.Capture(player);
        if (scope == null || RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        {
            Reply(Changed());
            return;
        }

        ItemStack after, siblings = null;
        Func<RebirthConsumeResult> complete;
        RebirthConsumeResult planned;
        if (action == Kind.Serve)
            planned = RebirthMetabolismService.PrepareLiveServing(player, source, slot, before, out after, out siblings, out complete);
        else
            planned = PlanMove(player, source, slot, action == Kind.Return, before, out after, out complete);
        if (!planned.Success || after == null || complete == null)
        {
            Reply(planned.Success ? Changed() : planned);
            return;
        }
        authorityActions.Add(player, new AuthorityAction
        {
            Id = id, Connection = connection, Scope = scope, Creation = creation, Deaths = player.Died,
            Action = action, Complete = complete
        });
        try { Reply(planned, after, siblings); }
        catch
        {
            authorityActions.Remove(player);
            throw;
        }
    }

    internal static void ReceiveResult(EntityPlayerLocal player, INetConnection connection, Guid id,
        ItemStack replacement, ItemStack siblings, RebirthConsumeResult result)
    {
        var pending = ownerAction;
        if (pending == null || pending.Id != id || !ReferenceEquals(pending.Player, player) ||
            !ReferenceEquals(pending.Connection, connection) || !pending.Current()) return;
        if (replacement == null)
        {
            ownerAction = null;
            RebirthMetabolismUiFeedback.Receive(result);
            return;
        }
        if (pending.GrantReceived) return;
        pending.GrantReceived = true;
        var inventory = player.PlayerUI?.xui?.PlayerInventory;
        bool applied;
        bool hasSiblings = siblings?.IsEmpty() == false;
        try
        {
            applied = result.Success && (!hasSiblings || inventory != null) &&
                !RebirthBackpackLibraryReservation.BlocksResourceUse(player) &&
                WriteCell(player, pending.Source, pending.Slot, pending.Before, replacement, pending.Action != Kind.Serve);
        }
        catch (Exception error)
        {
            var observed = Cell(player, pending.Source, pending.Slot);
            RebirthDebug.Log("metabolism", "[REBIRTH Metabolism] Live cell write interrupted: " + error.Message);
            if (Same(observed, pending.Before)) applied = false;
            else if (!hasSiblings && Same(observed, replacement)) applied = true;
            else return;
        }
        if (applied && siblings != null && !siblings.IsEmpty() && !inventory.AddItem(siblings, true) && !siblings.IsEmpty())
            inventory.DropItem(siblings);
        if (pending.Current())
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeRequest>()
                .SetupLive(player, id, applied ? Kind.Applied : Kind.Refused, pending.Source, pending.Slot, null, pending.Creation));
    }

    internal static bool MoveLocal(EntityPlayerLocal player, bool bag, int slot, bool returning)
    {
        if (!RebirthConsumptionOwnerPublication.TryCaptureCreation(player, out _) ||
            !ReferenceEquals(player.world.GetPrimaryPlayer(), player)) return false;
        if (returning) slot = FreeBagSlot(player);
        var source = bag ? RebirthMetabolismSourceKind.Backpack : RebirthMetabolismSourceKind.HeldToolbelt;
        var before = Cell(player, source, slot);
        var result = PlanMove(player, source, slot, returning, before, out var after, out var complete);
        return result.Success && WriteCell(player, source, slot, before, after, true) && complete().Success;
    }

    private static RebirthConsumeResult PlanMove(EntityPlayer player, RebirthMetabolismSourceKind source, int slot,
        bool returning, ItemStack before, out ItemStack after, out Func<RebirthConsumeResult> complete)
    {
        after = null;
        complete = null;
        if (before == null || slot < 0 || RebirthHydrationOwnerHold.HoldsMetabolismOwner(player as EntityPlayerLocal) ||
            RebirthHydrationServerSourceHold.HoldsPlayer(player) || RebirthConsumptionDurableDelivery.HoldsInventoryOriginal(player) ||
            RebirthNpcMetabolismCustody.HoldsPlayer(player) || RebirthSurvivorMetabolismCreationGate.ShouldDefer(player)) return Changed();
        var state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (state == null) return Changed();
        var equipped = state.HydrationSlotItem;
        ItemStack next;
        if (returning)
        {
            if (source != RebirthMetabolismSourceKind.Backpack || !before.IsEmpty() || equipped == null || equipped.IsEmpty()) return Changed();
            after = equipped.Clone();
            next = ItemStack.Empty.Clone();
        }
        else
        {
            if (before.IsEmpty() || (equipped != null && !equipped.IsEmpty()) ||
                !RebirthConsumableResolver.TryResolve(before.itemValue, out var definition) || !definition.IsDrink || !definition.HydrationEquippable)
                return Changed();
            if (RebirthSurvivorMode.IsEnabledForCurrentWorld() &&
                (!RebirthWorldCharacterService.TryGet(player, out var character) || character?.Support == null ||
                 character.Support.PendingGearTransfer?.SlotId == RebirthSurvivorGearService.SupportSlotId ||
                 character.Support.EquippedGearBySlot.TryGetValue(RebirthSurvivorGearService.SupportSlotId, out var support) && !string.IsNullOrEmpty(support)))
                return Changed();
            after = before.count == 1 ? ItemStack.Empty.Clone() : new ItemStack(before.itemValue.Clone(), before.count - 1);
            next = new ItemStack(before.itemValue.Clone(), 1);
        }
        complete = () =>
        {
            if (!ReferenceEquals(RebirthMetabolismStateRepository.GetOrCreate(player), state)) return Changed();
            state.HydrationSlotItem = next;
            state.Touch();
            RebirthMetabolismService.SendSnapshotToOwner(player, true);
            return new RebirthConsumeResult
            {
                Success = true,
                Message = Localization.Get(returning ? "xuiRebirthWaterSupportReturned" : "xuiRebirthWaterSupportEquipped")
            };
        };
        return new RebirthConsumeResult { Success = true };
    }

    private static bool WriteCell(EntityPlayerLocal player, RebirthMetabolismSourceKind source, int slot,
        ItemStack before, ItemStack after, bool respectLocks)
    {
        if (respectLocks && source == RebirthMetabolismSourceKind.Backpack && Locked(player, slot) ||
            !Same(Cell(player, source, slot), before)) return false;
        if (source == RebirthMetabolismSourceKind.Backpack) player.bag.SetSlot(slot, after.Clone());
        else player.inventory.SetItem(slot, after.Clone());
        return Same(Cell(player, source, slot), after);
    }

    private static ItemStack Cell(EntityPlayerLocal player, RebirthMetabolismSourceKind source, int slot)
    {
        var slots = source == RebirthMetabolismSourceKind.Backpack ? player.bag?.ItemGrid?.items : player.inventory?.ItemGrid?.items;
        if (slots == null || slot < 0 || slot >= slots.Length || source == RebirthMetabolismSourceKind.HeldToolbelt &&
            slot >= RebirthToolbeltCapacity.GetOwnedSlotCount(player, slots.Length)) return null;
        return slots[slot] ?? ItemStack.Empty;
    }

    private static bool Locked(EntityPlayerLocal player, int slot)
    {
        var locks = player.bag?.LockedSlots;
        return slot >= 0 && locks != null && slot < locks.Length && locks[slot];
    }

    private static int FreeBagSlot(EntityPlayerLocal player)
    {
        var slots = player.bag?.ItemGrid?.items;
        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
                if ((slots[i] == null || slots[i].IsEmpty()) && !Locked(player, i)) return i;
        return -1;
    }

    private static bool Same(ItemStack a, ItemStack b) => a != null && b != null &&
        (a.IsEmpty() && b.IsEmpty() || RebirthStationGridIngredients.IsSameStackSnapshot(a, b));

    internal static RebirthConsumeResult Changed() => new RebirthConsumeResult
    {
        Failure = RebirthMetabolismConsumeFailure.ItemChanged,
        Message = "The selected item is no longer available."
    };

    internal static ItemStack ReadStack(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count == 0) return ItemStack.Empty.Clone();
        if (count < 0 || count > ushort.MaxValue ||
            !RebirthNativeItemCodec.TryDecode(RebirthSurvivorNetworkCodec.ReadBoundedString(reader, MaxItemBytes), out var item))
            throw new InvalidDataException("Invalid live metabolism item.");
        return new ItemStack(item, count);
    }

    internal static void WriteStack(BinaryWriter writer, ItemStack stack)
    {
        int count = stack == null || stack.IsEmpty() ? 0 : stack.count;
        writer.Write(count);
        if (count != 0)
            RebirthSurvivorNetworkCodec.WriteString(writer, RebirthNativeItemCodec.Encode(stack.itemValue), MaxItemBytes);
    }

    internal static int StackWireLength(ItemStack stack) => 8 +
        (stack == null || stack.IsEmpty() ? 0 : RebirthNativeItemCodec.Encode(stack.itemValue).Length);
}
