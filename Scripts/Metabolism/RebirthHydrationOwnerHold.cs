using System;
using System.Collections.Generic;
using System.IO;

// Native owner interaction custody. Acquisition freezes an exact original but
// does not authorize applying it. Terminal release needs the joint saved proof.
internal static partial class RebirthHydrationOwnerHold
{
    private sealed class Entry
    {
        internal EntityPlayerLocal Player;internal World World;internal GameManager Game;
        internal ConnectionManager Manager;internal object Peer;
        internal object Bag,Belt;
        internal string Save,PlayerData;internal RebirthHydrationTransferOriginal Original;
    }
    private static readonly Dictionary<EntityPlayerLocal,Entry> entries=new Dictionary<EntityPlayerLocal,Entry>();
    private static readonly List<Entry> reenteredRemoteEntries=new List<Entry>();
    internal static bool TryAcquire(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(player==null||original==null)return false;
            if(entries.TryGetValue(player,out var retained))return retained.Original.Digest==original.Digest&&Current(retained);
            if(entries.Count>=64||!Scope(player,original)||!CanAcquire(player)||
                !original.TryGetPlan(out var plan)||!Capture(player,original,out var snapshot)||
                !plan.MatchesBefore(snapshot.Bag,snapshot.Belt,plan.GearBefore,snapshot.Bag.Length,snapshot.Belt.Length))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peers=manager.connectionToServer;
            var entry=new Entry{Player=player,World=player.world,Game=GameManager.Instance,Manager=manager,
                Peer=player.world.IsRemote()?peers[0]:null,Save=DirectoryPath(GameIO.GetSaveGameDir()),
                PlayerData=DirectoryPath(GameIO.GetPlayerDataDir()),Original=original,Bag=player.bag,Belt=player.inventory};
            if(!Current(entry)||!CanAcquire(player)||!Capture(player,original,out snapshot)||
                !plan.MatchesBefore(snapshot.Bag,snapshot.Belt,plan.GearBefore,snapshot.Bag.Length,snapshot.Belt.Length))return false;
            entries.Add(player,entry);return true;
        }
        catch{return false;}
    }
    internal static bool Matches(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)=>
        player!=null&&original!=null&&entries.TryGetValue(player,out var entry)&&entry.Original.Digest==original.Digest&&Current(entry);
    // Loss of current permission never releases this actor's retained original.
    internal static bool IsHeld(EntityPlayerLocal player)=>player!=null&&entries.ContainsKey(player);
    // The account's persisted metabolism row outlives its native body. A new
    // body/creation must not consume or replace a row still held by the old
    // body's original. This does not adopt that body's native inventory handles.
    internal static bool HoldsMetabolismOwner(EntityPlayerLocal player)
    {
        if(player==null)return false;
        if(entries.ContainsKey(player))return true;
        bool identified=RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner!=null;
        foreach(var entry in entries.Values)
            if(ReferenceEquals(entry.World,player.world)&&(!identified||entry.Original.Owner==owner.StorageKey))return true;
        return false;
    }
    internal static bool HoldsWorld(World world)
    {
        if(world==null)return false;
        foreach(var entry in entries.Values)if(ReferenceEquals(entry.World,world))return true;
        foreach(var entry in reenteredRemoteEntries)if(ReferenceEquals(entry.World,world))return true;
        return false;
    }
    internal static bool BlocksInventory(object inventory,bool bag)
    {
        if(inventory==null)return false;
        foreach(var entry in entries.Values)
            if(ReferenceEquals(bag?entry.Bag:entry.Belt,inventory))return true;
        foreach(var entry in reenteredRemoteEntries)
            if(ReferenceEquals(bag?entry.Bag:entry.Belt,inventory))return true;
        return false;
    }
    private static bool Current(Entry entry)
    {
        try
        {
            if(!Scope(entry.Player,entry.Original)||!ReferenceEquals(GameManager.Instance,entry.Game)||
                !ReferenceEquals(entry.Player.world,entry.World)||
                !ReferenceEquals(entry.Player.bag,entry.Bag)||!ReferenceEquals(entry.Player.inventory,entry.Belt)||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,entry.Manager)||
                !SamePath(entry.Save,DirectoryPath(GameIO.GetSaveGameDir()))||
                !SamePath(entry.PlayerData,DirectoryPath(GameIO.GetPlayerDataDir()))||!LocksMatch(entry.Player,entry.Original))return false;
            if(!entry.World.IsRemote())return entry.Manager.IsServer&&entry.Peer==null;
            var peers=entry.Manager.connectionToServer;
            return !entry.Manager.IsServer&&peers!=null&&peers.Length>0&&ReferenceEquals(peers[0],entry.Peer)&&!peers[0].IsDisconnected();
        }
        catch{return false;}
    }
    private static bool Scope(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||player?.world==null||manager==null||player.IsDead()||!player.IsSpawned()||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||
            !RebirthHydrationTransferScope.TryCapture(player,out var creation)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner))return false;
        string guid=player.world.IsRemote()?GamePrefs.GetString(EnumGamePrefs.GameGuidClient):player.world.Guid;
        if(!Guid.TryParse(guid,out var world)||!original.Matches(world,owner.StorageKey,creation,player.Died))return false;
        if(!player.world.IsRemote())return manager.IsServer;
        var peers=manager.connectionToServer;
        return !manager.IsServer&&peers!=null&&peers.Length>0&&peers[0]!=null&&!peers[0].IsDisconnected();
    }
    private static bool CanAcquire(EntityPlayerLocal player)
    {
        var xui=player.PlayerUI?.xui;
        return !HoldsMetabolismOwner(player)&&RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)&&!RebirthDogLifecycleService.HasHireInventoryHold(player)&&
            !RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)&&player.inventory!=null&&
            !player.inventory.IsHoldingItemActionRunning()&&xui!=null&&!xui.IsUsingItemActionEntryUse&&
            xui.DragAndDropWindow?.CurrentStack!=null&&xui.DragAndDropWindow.CurrentStack.IsEmpty();
    }
    private static bool Capture(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthGearInventorySnapshot snapshot)
    {
        snapshot=null;var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;
        if(bag==null||belt==null||!LocksMatch(player,original))return false;
        return RebirthGearInventorySnapshot.TryCapture(bag,belt,original.OwnedBelt,out snapshot);
    }
    private static bool LocksMatch(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        var bag=player.bag?.ItemGrid?.items;var belt=player.inventory?.ItemGrid?.items;
        if(bag==null||belt==null||original.BagLocks.Length!=bag.Length||RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length)!=original.OwnedBelt)return false;
        var locks=player.bag.LockedSlots;
        for(int index=0;index<bag.Length;index++)
            if((locks!=null&&index<locks.Length&&locks[index])!=(original.BagLocks[index]=='1'))return false;
        return true;
    }
    private static string DirectoryPath(string value)=>string.IsNullOrWhiteSpace(value)?throw new InvalidDataException("Actual hydration native save scope required"):Path.GetFullPath(value);
    private static bool SamePath(string first,string second)=>string.Equals(first,second,Environment.OSVersion.Platform==PlatformID.Win32NT?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
}
