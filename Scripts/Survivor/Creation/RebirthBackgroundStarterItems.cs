using System;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// The server authorizes the immutable background kit. The owning client delivers through
/// native inventory, whose save includes both the items and persistent CVar receipts.
/// Never grants into the server's stale copy of a remote player's bag.
/// </summary>
public static class RebirthBackgroundStarterItems
{
    private static World pendingWorld;
    private static int pendingPlayerId;
    private static string creationId, backgroundId, definitionHash;
    private static float nextAttempt;
    private static bool spaceNoticeShown;

    public static void Offer(EntityPlayer player)
    {
        if (player == null || !RebirthWorldCharacterRepository.IsServerAuthority
            || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Origin == null) return;
        string hash = RebirthSurvivorDefinitionRegistry.SemanticHash;
        if (player is EntityPlayerLocal)
            Receive(player.world, player.entityId, record.Origin.CreationId, record.Origin.BackgroundId, hash);
        else
        {
            ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (connection == null || !connection.IsServer) return;
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackgroundStarterItems));
                var offer = NetPackageManager.GetPackage<NetPackageRebirthBackgroundStarterItems>();
                if (offer == null) return;
                connection.SendPackage(offer.Setup(player.entityId, record.Origin.CreationId,
                    record.Origin.BackgroundId, hash), _attachedToEntityId: player.entityId);
            }
            catch (Exception)
            {
                // Spawn/creation owner-state publication must still proceed. The owner
                // recovers the immutable offer from its matching authoritative projection.
            }
        }
    }

    public static void Receive(World world, int playerId, string origin, string background, string hash)
    {
        string parsed;
        if (world == null || !RebirthSurvivorRequestScope.TryNormalize(origin, out parsed) || string.IsNullOrEmpty(background)) return;
        if (!ReferenceEquals(pendingWorld, world) || pendingPlayerId != playerId
            || creationId != parsed || backgroundId != background)
            spaceNoticeShown = false;
        pendingWorld = world;
        pendingPlayerId = playerId;
        creationId = parsed;
        backgroundId = background;
        definitionHash = hash;
        nextAttempt = 0f;
    }

    public static void Reset()
    {
        pendingWorld = null;
        creationId = backgroundId = definitionHash = null;
        pendingPlayerId = 0;
        nextAttempt = 0f;
        spaceNoticeShown = false;
    }

    private static bool IsCurrentCharacter(EntityPlayerLocal player, string origin, string background)
    {
        if (player == null || player.world == null) return false;
        if (player.world.IsRemote())
        {
            var snapshot = RebirthSurvivorClientState.GetOwnerStateSnapshot();
            return snapshot != null && snapshot.RebirthModeEnabled && snapshot.HasCharacter
                && RebirthSurvivorRequestScope.Matches(origin, RebirthSurvivorClientState.GetProjectedCreationId(player))
                && RebirthSurvivorRequestScope.Matches(origin, snapshot.CreationId)
                && string.Equals(background, snapshot.BackgroundId, StringComparison.Ordinal);
        }
        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player, out record) && record != null && record.IsComplete
            && record.Origin != null && RebirthSurvivorRequestScope.Matches(origin, record.Origin.CreationId)
            && string.Equals(background, record.Origin.BackgroundId, StringComparison.Ordinal);
    }

    private static bool TryReadDelivered(float receipt, int required, out int delivered)
    {
        delivered=0;
        if(required<0||float.IsNaN(receipt)||float.IsInfinity(receipt)||receipt<0f||
            Math.Floor((double)receipt)!=receipt)return false;
        // A kit reduced by later authoring remains paid; never cast an unbounded CVar to int.
        delivered=(int)Math.Min((double)receipt,required);
        return true;
    }

    private static void RecoverOwnerOffer()
    {
        World world=GameManager.Instance!=null?GameManager.Instance.World:null;
        EntityPlayerLocal player=world!=null?world.GetPrimaryPlayer():null;
        if(player==null||player.Buffs==null||!player.IsSpawned()||player.IsDead()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        string origin,background,hash;
        if(world.IsRemote())
        {
            origin=RebirthSurvivorClientState.GetProjectedCreationId(player);
            string ownerCreation;
            if(!RebirthSurvivorRequestScope.TryNormalize(origin,out ownerCreation))return;
            origin=ownerCreation;
            if(player.Buffs.GetCustomVar("rbStarter_"+origin)==1f)return;
            var snapshot=RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if(snapshot==null||!snapshot.RebirthModeEnabled||!snapshot.HasCharacter||
                !RebirthSurvivorRequestScope.Matches(origin,snapshot.CreationId))return;
            background=snapshot.BackgroundId;hash=snapshot.ServerDefinitionHash;
        }
        else
        {
            RebirthWorldCharacterRecord record;
            if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null||!record.IsComplete||record.Origin==null)return;
            origin=record.Origin.CreationId;background=record.Origin.BackgroundId;hash=RebirthSurvivorDefinitionRegistry.SemanticHash;
            string hostCreation;
            if(!RebirthSurvivorRequestScope.TryNormalize(origin,out hostCreation))return;
            origin=hostCreation;
            if(player.Buffs.GetCustomVar("rbStarter_"+origin)==1f)return;
        }
        Receive(world,player.entityId,origin,background,hash);
    }
    public static void Tick()
    {
        if (Time.realtimeSinceStartup < nextAttempt) return;
        if(pendingWorld==null)RecoverOwnerOffer();
        nextAttempt = Time.realtimeSinceStartup + 2f;
        if(pendingWorld==null)return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!ReferenceEquals(world, pendingWorld)) { Reset(); return; }
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player == null || !player.IsSpawned() || player.IsDead()
            || player.Buffs == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        // A late stale offer must not permanently pin the pending slot. Recover only
        // from the current authoritative owner projection; never grant this rejected offer.
        if (player.entityId != pendingPlayerId
            || !string.Equals(definitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.OrdinalIgnoreCase)
            || !IsCurrentCharacter(player, creationId, backgroundId))
        {
            RecoverOwnerOffer();
            // Receive resets the timer; preserve throttling for an incompatible projection.
            nextAttempt = Time.realtimeSinceStartup + 2f;
            return;
        }
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(player);
        if (ui == null || ui.xui == null || ui.xui.PlayerInventory == null || player.bag == null
            || RebirthBackpackLibraryReservation.IsHeld(player)) return;
        string prefix = "rbStarter_" + creationId;
        float completedReceipt=player.Buffs.GetCustomVar(prefix);
        if(float.IsNaN(completedReceipt)||float.IsInfinity(completedReceipt)||
            (completedReceipt!=0f&&completedReceipt!=1f))return;
        if (completedReceipt == 1f) { Reset(); return; }
        RebirthBackgroundDefinition background;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(backgroundId, out background) || background == null) return;

        bool complete = true, needsSpace = false;
        for (int i = 0; i < background.StartingItems.Count; i++)
        {
            RebirthStartingItemDefinition entry = background.StartingItems[i];
            // Item identity instead of list index preserves receipts if authoring reorders a kit.
            string receipt = prefix + "_" + entry.ItemId;
            int delivered;
            if(!TryReadDelivered(player.Buffs.GetCustomVar(receipt),entry.Count,out delivered))
            {complete=false;continue;}
            if (delivered >= entry.Count) continue;
            ItemValue value = entry.HasQuality ? ItemClass.CreateItemValue(entry.ItemId, entry.Quality)
                : ItemClass.GetItem(entry.ItemId);
            if (value == null || value.type == 0 || value.ItemClass == null) { complete = false; continue; }
            int count = Math.Min(entry.Count - delivered, Math.Max(1, value.ItemClass.MaxCount));
            // Reduce to a batch that fits. No partial grant/drop: unclaimed items stay pending.
            while (count > 0)
            {
                ItemStack transfer = new ItemStack(value, count);
                // Profile kits belong in the backpack, never the native toolbelt fallback.
                // Native Bag.AddItem adopts the input stack into an empty slot without
                // reducing its count; success therefore means the full batch was delivered.
                if (!player.bag.CanStack(transfer)) { count /= 2; continue; }
                try
                {
                    // AddItem changes slots before notifying listeners. Keep an uncertain
                    // receipt until its result is known; an exception must not grant again.
                    player.Buffs.SetCustomVar(receipt, -1f, true);
                    bool inserted = player.bag.AddItem(transfer);
                    int moved = inserted ? count : Math.Max(0, Math.Min(count, count - transfer.count));
                    delivered += moved;
                    player.Buffs.SetCustomVar(receipt, delivered, true);
                    if (moved > 0) break;
                }
                catch (Exception)
                {
                    // A negative receipt blocks replay for this entry without guessing a
                    // refund or delivered amount. Other independent kit entries may proceed.
                    complete = false;
                    break;
                }                count /= 2;
            }
            if (count == 0) needsSpace = true;
            if (delivered < entry.Count) complete = false;
        }
        if (complete)
        {
            player.Buffs.SetCustomVar(prefix, 1f, true);
            Reset();
        }
        else if (needsSpace && !spaceNoticeShown)
        {
            spaceNoticeShown = true;
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthStarterItemsNeedSpace"), true, false, 5f);
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthBackgroundStarterItems : NetPackage
{
    private int playerId;
    private string origin = "", background = "", hash = "";
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthBackgroundStarterItems Setup(int id, string creation, string backgroundId, string definitionHash)
    { playerId = id; origin = creation; background = backgroundId; hash = definitionHash; return this; }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader b = (BinaryReader)reader;
        playerId = b.ReadInt32();
        origin = RebirthSurvivorNetworkCodec.ReadString(b, 96);
        background = RebirthSurvivorNetworkCodec.ReadString(b, 128);
        hash = RebirthSurvivorNetworkCodec.ReadString(b, 128);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter b = (BinaryWriter)writer;
        b.Write(playerId);
        RebirthSurvivorNetworkCodec.WriteString(b, origin, 96);
        RebirthSurvivorNetworkCodec.WriteString(b, background, 128);
        RebirthSurvivorNetworkCodec.WriteString(b, hash, 128);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    { RebirthBackgroundStarterItems.Receive(world, playerId, origin, background, hash); }
    public int GetLength()
    { return 12 + RebirthSurvivorNetworkCodec.EstimateString(origin, 96)
        + RebirthSurvivorNetworkCodec.EstimateString(background, 128) + RebirthSurvivorNetworkCodec.EstimateString(hash, 128); }
}
