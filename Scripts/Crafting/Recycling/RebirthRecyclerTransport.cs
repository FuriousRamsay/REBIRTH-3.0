using System;

// Placed workstations retain the game's trusted native local-owner edit/sync
// path. Mobile stations use their existing acknowledged camper command channel.
internal static class RebirthRecyclerTransport
{
    private static EntityPlayerLocal scopedActor;
    private static TileEntityWorkstation scopedStation;
    private static bool ownerChanged;
    internal static void OwnerChanged(EntityPlayer actor, TileEntityWorkstation station)
    { if (IsNativeOwner(station, actor)) ownerChanged = true; }
    private sealed class Clock { internal int Frame = -1; internal float Elapsed; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TileEntityWorkstation, Clock> clocks
        = new System.Runtime.CompilerServices.ConditionalWeakTable<TileEntityWorkstation, Clock>();

    internal static void TryAdvanceOwned(EntityPlayerLocal player, TileEntityWorkstation station, float dt)
    {
        if (!Current(player, station) || scopedActor != null || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
        var clock = clocks.GetValue(station, _ => new Clock());
        if (clock.Frame == UnityEngine.Time.frameCount) return;
        clock.Frame = UnityEngine.Time.frameCount;
        clock.Elapsed += Math.Min(dt, 1f);
        if (clock.Elapsed < .25f) return;
        float elapsed = clock.Elapsed; clock.Elapsed = 0;
        var beforeQueue = station.Queue; var beforeTools = station.Tools; var beforeOutput = station.Output;
        scopedActor = player; scopedStation = station; ownerChanged = false;
        try { RebirthRecyclerRuntime.Process(station, elapsed); }
        finally { scopedActor = null; scopedStation = null; }
        if (!ReferenceEquals(beforeQueue, station.Queue) || !ReferenceEquals(beforeTools, station.Tools)
            || !ReferenceEquals(beforeOutput, station.Output) || ownerChanged) Publish(player, station, ownerChanged);
    }

    internal static bool ExecuteEarnedOriginal(EntityPlayerLocal player,TileEntityWorkstation station,Action callback)
    {
        if(callback==null||scopedActor!=null||!Current(player,station))return false;
        scopedActor=player;scopedStation=station;ownerChanged=false;
        try{callback();return true;}
        finally{scopedActor=null;scopedStation=null;ownerChanged=false;}
    }

    internal static bool IsNativeOwner(TileEntityWorkstation station, EntityPlayer actor)
        => ThreadManager.IsMainThread() && ReferenceEquals(scopedActor, actor)
            && ReferenceEquals(scopedStation, station) && Current(scopedActor, station);

    private static bool Current(EntityPlayerLocal player, TileEntityWorkstation station)
    {
        if (!ThreadManager.IsMainThread() || player == null || player.IsDead() || !player.IsSpawned() || station == null
            || station is RebirthCamperStationState || player.world?.IsRemote() != true
            || !ReferenceEquals(GameManager.Instance?.World, player.world)
            || !ReferenceEquals(player.world.GetPrimaryPlayer(), player)
            || !ReferenceEquals(player.world.GetEntity(player.entityId), player)
            || !ReferenceEquals(player.world.GetTileEntity(station.ToWorldPos()), station)
            || LockManager.Instance?.IsLockedByLocalPlayer(station, 0) != true
            || (player.position - station.ToWorldPos().ToVector3()).sqrMagnitude > 64f) return false;
        var window = XUiC_RebirthStationWorkspace.ActiveInstance;
        return window?.windowGroup?.isShowing == true
            && ReferenceEquals(window.xui?.playerUI?.entityPlayer, player)
            && ReferenceEquals(window.WorkstationData?.TileEntity, station);
    }

    internal static bool Request(EntityPlayerLocal player, TileEntityWorkstation station,
        int slot, int units, Guid operation, bool cancel, out string reason)
    {
        reason = "xuiRebirthRecyclingAccessExpired";
        if (station is RebirthCamperStationState mobile)
            return RebirthCamperRemoteStationClient.RequestRecycler(player, mobile, slot, units, operation, cancel, out reason);
        if (operation == Guid.Empty || !Current(player, station) || scopedActor != null) return false;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection?.IsClient != true || connection.IsServer) return false;
        bool accepted;
        scopedActor = player; scopedStation = station; ownerChanged = false;
        try
        {
            accepted = cancel
                ? RebirthRecyclerRuntime.TryCancel(player, station, operation, () => Current(player, station), out reason)
                : RebirthRecyclerRuntime.TryQueue(player, station, slot, units, operation, () => Current(player, station), out reason);
        }
        finally { scopedActor = null; scopedStation = null; }
        if (!accepted) return false;
        // The retained placed original owns its authenticated before/after
        // publications. This caller must not emit an unscoped initial or retry
        // snapshot while that purpose is still preparing either native leg.
        return true;
    }

    private static void Publish(EntityPlayerLocal player, TileEntityWorkstation station, bool inventoryChanged)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!Current(player, station) || connection?.IsClient != true || connection.IsServer) return;
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(station, StreamModeWrite.ToServer));
        if (inventoryChanged)
            connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(player, true, true, false, false));
    }
}
