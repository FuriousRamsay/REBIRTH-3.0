using System;
using System.Collections.Generic;

// Server lease resolver shared with the existing preparation/refund/reward
// services. A lease is the exact native vehicle lock and connection, not a
// terrain coordinate or a client-created TileEntity.
public static partial class RebirthCamperRemoteStationTransport
{
    private sealed class Lease
    {
        internal EntityVehicle Vehicle;internal EntityPlayer Player;internal ClientInfo Sender;
        internal RebirthCamperStationAddress Address;internal string Creation;internal Guid Token;
    }
    private static readonly Dictionary<int,Lease> leases=new Dictionary<int,Lease>();
    private static Func<EntityPlayer,string> creationProvider;
    private static Func<EntityPlayer,RebirthCamperStationAddress,string,object,RebirthCamperRemoteStationCommand,bool> command;
    private static Action<EntityPlayer,RebirthCamperStationState> observeTerminal;
    public static bool IsRegistered=>creationProvider!=null && command!=null;

    public static void Register(Func<EntityPlayer,string> authenticatedCreation,
        Func<EntityPlayer,RebirthCamperStationAddress,string,object,RebirthCamperRemoteStationCommand,bool> handleCommand,
        Action<EntityPlayer,RebirthCamperStationState> observeTerminalJobs=null)
    {
        if(authenticatedCreation==null || handleCommand==null)throw new ArgumentNullException("Complete remote station adapters required.");
        if(IsRegistered)throw new InvalidOperationException("Remote station adapters already registered.");
        // Install adapters during ModAPI startup. The native connection lifecycle
        // discovers these package classes and assigns IDs after startup.
        creationProvider=authenticatedCreation;command=handleCommand;observeTerminal=observeTerminalJobs;
    }

    internal static object Admit(EntityVehicle vehicle,EntityPlayer player,ClientInfo sender,
        RebirthCamperStationAddress address,Guid token)
    {
        if(!ThreadManager.IsMainThread() || !IsRegistered || player==null || token==Guid.Empty || !address.IsValid)
            throw new InvalidOperationException("Authenticated native station lease required.");
        string creation=creationProvider(player);
        if(string.IsNullOrWhiteSpace(creation) || !RebirthCamperRemoteAdmission.CanAccess(vehicle,player,sender,()=>creationProvider(player)==creation))
            throw new InvalidOperationException("Native station access expired.");
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        var station=runtime.Stations.Find(address.Installation);
        if(runtime.TransferPending || runtime.Identity!=address.Vehicle || station==null)
            throw new InvalidOperationException("Current installed station required.");
        if(leases.TryGetValue(player.entityId,out var prior))
        {
            if(Current(prior))
            {
                if(prior.Token==token && prior.Address.Equals(address))return prior;
                throw new InvalidOperationException("Close the existing remote station first.");
            }
            leases.Remove(player.entityId);
        }
        if(RebirthCamperStationWindowSession.IsInUse(station.State))
            throw new InvalidOperationException("The installed station already has a user.");
        var lease=new Lease{Vehicle=vehicle,Player=player,Sender=sender,Address=address,Creation=creation,Token=token};
        leases.Add(player.entityId,lease);
        GameManager.Instance.StartCoroutine(Watch(lease));
        // There is no server UI to own native fuel/queue processing. Keep native
        // access security in LockManager; the native processing flag stays false.
        station.State.bUserAccessing=false;
        return lease;
    }

    public static bool TryResolve(EntityPlayer player,RebirthCamperStationAddress address,string creation,
        out RebirthCamperStationState state,out object leaseIdentity)
    {
        state=null;leaseIdentity=null;
        if(!ThreadManager.IsMainThread() || player==null || !leases.TryGetValue(player.entityId,out var lease)
            || !ReferenceEquals(lease.Player,player) || !lease.Address.Equals(address)
            || lease.Creation!=creation || !Current(lease))return false;
        state=RebirthCamperModuleRuntime.For(lease.Vehicle).Stations.Find(address.Installation)?.State;
        leaseIdentity=lease;return state!=null;
    }
    public static bool IsCurrent(EntityPlayer player,RebirthCamperStationAddress address,string creation,object identity)
        =>TryResolve(player,address,creation,out _,out var lease) && ReferenceEquals(lease,identity);

    public static bool TryGetCurrent(EntityPlayer player,out RebirthCamperStationState state,
        out RebirthCamperStationAddress address,out string creation,out object identity)
    {
        state=null;address=default;creation=null;identity=null;
        if(!ThreadManager.IsMainThread() || player==null || !leases.TryGetValue(player.entityId,out var lease) || !Current(lease))return false;
        address=lease.Address;creation=lease.Creation;
        return TryResolve(player,address,creation,out state,out identity);
    }

    public static bool HasServerLease(RebirthCamperStationState state)
    {
        if(!ThreadManager.IsMainThread() || state==null || state.BoundVehicle?.world==null || state.BoundVehicle.world.IsRemote())return false;
        foreach(var lease in leases.Values)
            if(ReferenceEquals(lease.Vehicle,state.BoundVehicle) && lease.Address.Installation==state.InstallationId && Current(lease))return true;
        return false;
    }
    internal static bool Execute(EntityPlayer player,Guid token,RebirthCamperRemoteStationCommand request)
    {
        if(!ThreadManager.IsMainThread() || request==null || player==null || !leases.TryGetValue(player.entityId,out var lease)
            || !ReferenceEquals(lease.Player,player) || lease.Token!=token || !Current(lease))return false;
        return command(player,lease.Address,lease.Creation,lease,request) && Current(lease);
    }
    internal static void Release(EntityPlayer player,Guid token)
    {
        if(!ThreadManager.IsMainThread() || player==null || !leases.TryGetValue(player.entityId,out var lease)
            || !ReferenceEquals(lease.Player,player) || lease.Token!=token)return;
        leases.Remove(player.entityId);
        // The local exact callback closes/unlocks native channel0. Server lease
        // cleanup must not independently unlock a replacement native session.
    }
    private static bool Current(Lease lease)
        =>NativeCurrent(lease) && !RebirthCamperModuleRuntime.For(lease.Vehicle).TransferPending;

    private static bool NativeCurrent(Lease lease)
    {
        if(!IsRegistered || lease==null || lease.Player==null || lease.Vehicle==null
            || !leases.TryGetValue(lease.Player.entityId,out var registered) || !ReferenceEquals(registered,lease)
            || !ReferenceEquals(GameManager.Instance?.World,lease.Vehicle.world)
            || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(lease.Player.entityId),lease.Sender)
            || !RebirthCamperRemoteAdmission.CanAccess(lease.Vehicle,lease.Player,lease.Sender,()=>creationProvider(lease.Player)==lease.Creation))return false;
        var runtime=RebirthCamperModuleRuntime.For(lease.Vehicle);
        return runtime.Identity==lease.Address.Vehicle && runtime.Stations.Find(lease.Address.Installation)!=null;
    }
}
