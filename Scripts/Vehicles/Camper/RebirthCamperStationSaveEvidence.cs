using System;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

// A requested save is not evidence. Only primary native-file readback mints a
// publication. This primitive grants no queue, refund or progression authority.
public static class RebirthCamperStationSaveEvidence
{
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Request> pending
        = new ConditionalWeakTable<RebirthCamperStationState,Request>();
    public sealed class Request : IDisposable
    {
        internal EntityVehicle Vehicle;
        internal RebirthCamperStationState State;
        internal RebirthCamperStationAddress Address;
        internal RebirthCamperSavedVehicle.Expected Expected;
        internal byte[] Snapshot;
        internal ItemStack InstalledItem;
        internal Func<bool> CurrentSession;
        internal bool Issued;
        internal Publication ConfirmedPublication;
        internal bool Closed;
        internal float Deadline;
        internal Request() { }
        public void Dispose()
        {
            if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Release station save on game thread.");
            if(Closed)return;
            if(State!=null && pending.TryGetValue(State,out var active) && ReferenceEquals(active,this))
            {
                State.lastTickTime=0;
                State.setModified();
                pending.Remove(State);
            }
            // A failed native dirty notification must remain retryable. Do not
            // discard the pending owner or report closure before it succeeds.
            Closed=true;
        }
    }
    public sealed class Publication
    {
        private readonly byte[] snapshot;
        private readonly ItemStack installedItem;
        public RebirthCamperStationAddress Address { get; private set; }
        public string StationDigest { get; private set; }
        public string VehicleEntityDigest { get; private set; }
        public string VehicleBagDigest { get; private set; }
        public string SaveDirectory { get; private set; }
        public int VehicleEntityId { get; private set; }
        public int VehicleClass { get; private set; }
        internal Publication(Request request)
        {
            snapshot=(byte[])request.Snapshot.Clone();
            installedItem=request.InstalledItem.Clone();
            Address=request.Address;StationDigest=Digest(request.Snapshot);
            VehicleEntityDigest=Digest(request.Expected.Entity);VehicleBagDigest=Digest(request.Expected.Bag);
            SaveDirectory=request.Expected.SaveDirectory;
            VehicleEntityId=request.Expected.Id;VehicleClass=request.Expected.Class;
        }
        // Decode into a detached object. Neither callers nor later native ticks
        // can rewrite the publication's saved inventory/queue observation.
        public RebirthCamperStationState RestoreStationSnapshot()
            => RebirthCamperStationSnapshot.Restore(Address.Installation,installedItem.Clone(),(byte[])snapshot.Clone());
        internal string RecoveryDigest()
            =>RebirthCamperStationRecoveryImage.Digest(RestoreStationSnapshot(),installedItem.Clone());
    }
    public static Request Begin(EntityPlayer player, Func<bool> authenticatedSession)
    {
        if(!ThreadManager.IsMainThread() || authenticatedSession==null || !authenticatedSession()
            || !RebirthCamperStationWindowSession.TryGetCurrent(player,out var state,out var address))
            throw new InvalidOperationException("Current authenticated mobile station required.");
        return BeginCore(state,address,()=>authenticatedSession()
            && RebirthCamperStationWindowSession.TryGetCurrent(player,out var current,out var currentAddress)
            && ReferenceEquals(current,state) && currentAddress.Equals(address));
    }
#if !REBIRTH_CAMPER_DIAGNOSTICS
    internal static Request BeginRetirement(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current)
        =>BeginCore(state,address,current);
    // Fresh primary native observation for a separately authenticated persisted
    // owner/job scope. It grants processing proof only, never owner effects.
    internal static Request BeginProcessingRecovery(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current)
        =>BeginCore(state,address,current);
    internal static Request BeginRecovery(EntityPlayer player,RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> authenticatedOwner)
    {
        if(!RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Origin==null||
            !RebirthStationLiveAccess.TryGetMobileLease(player,address,owner.Origin.CreationId,out var original,out var lease)||
            !ReferenceEquals(original,state))throw new InvalidOperationException("Current authenticated mobile recovery lease required.");
        string creation=owner.Origin.CreationId;
        Func<bool> current=()=>authenticatedOwner!=null&&authenticatedOwner()&&
            RebirthStationLiveAccess.TryGetMobileLease(player,address,creation,out var live,out var currentLease)&&
            ReferenceEquals(live,state)&&ReferenceEquals(lease,currentLease);
        return BeginCore(state,address,current);
    }
    internal static Request BeginCompletion(RebirthCamperStationCompletionCapture.SuccessfulOutput completed)
    {
        if(completed==null)throw new ArgumentNullException(nameof(completed));
        var binding=completed.Binding;
        Func<bool> current=()=>RebirthCamperStationCompletionCallsite.TryGetSuccessfulOutput(binding.Station,binding.Admission.JobId,out var retained)
            && ReferenceEquals(retained,completed) && completed.MatchesLiveOutput();
        return BeginCore(binding.Station,binding.Address,current);
    }
#endif
    private static Request BeginCore(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||state==null||!address.IsValid||current==null||!current())
            throw new InvalidOperationException("Original authenticated station observation required.");
        var vehicle=state.BoundVehicle;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        if(IsPending(state))throw new InvalidOperationException("Station already awaits native save confirmation.");
        var request=new Request {Vehicle=vehicle,State=state,Address=address,
            Deadline=UnityEngine.Time.realtimeSinceStartup+30f,CurrentSession=current};
        pending.Add(state,request);
        try
        {
            runtime.Persist(true);
            request.InstalledItem=runtime.Stations.Find(address.Installation).InstalledItem.Clone();
            request.Snapshot=RebirthCamperStationSnapshot.Capture(state.InstallationId,state);
            request.Expected=RebirthCamperSavedVehicle.RequestSave(vehicle);
            if(!Current(request))throw new InvalidOperationException("Station changed during native save request.");
            return request;
        }
        catch{request.Dispose();throw;}
    }
    public static bool TryObserve(Request request,out Publication publication)
    {
        publication=null;
        if(!ThreadManager.IsMainThread() || request==null)return false;
        if(request.Issued)return CompleteObservation(request,out publication);
        if(request.Closed)return false;
        if(!Current(request)){request.Dispose();return false;}
        if(!RebirthCamperSavedVehicle.Verify(request.Vehicle,request.Expected,()=>Current(request)))return false;
        if(!Current(request)){request.Dispose();return false;}
        request.ConfirmedPublication=new Publication(request);
        request.Issued=true;
        return CompleteObservation(request,out publication);
    }
    private static bool CompleteObservation(Request request,out Publication publication)
    {
        publication=null;
        if(request.ConfirmedPublication==null)return false;
        bool current;
        try{current=request.CurrentSession();}catch{current=false;}
        if(!current){request.Dispose();request.ConfirmedPublication=null;return false;}
        // A verified publication belongs to this original request. Retain it
        // if native dirty cleanup throws, then deliver once after cleanup.
        request.Dispose();
        try{current=request.CurrentSession();}catch{current=false;}
        if(!current){request.ConfirmedPublication=null;return false;}
        publication=request.ConfirmedPublication;request.ConfirmedPublication=null;return true;
    }
    internal static bool IsPending(RebirthCamperStationState state)
    {
        if(!pending.TryGetValue(state,out var request))return false;
        bool admitted;
        try{admitted=!request.Closed && UnityEngine.Time.realtimeSinceStartup<request.Deadline && request.CurrentSession();}
        catch{admitted=false;}
        if(!admitted){request.Dispose();return false;}
        return true;
    }
    private static bool Current(Request request)
    {
        try{return CurrentCore(request);}
        catch{return false;}
    }
    private static bool CurrentCore(Request request)
    {
        if(request==null || request.Closed || UnityEngine.Time.realtimeSinceStartup>=request.Deadline
            || !request.CurrentSession() || request.Vehicle==null
            || request.State==null || request.Vehicle.world==null || request.Vehicle.world.IsRemote())return false;
        var runtime=RebirthCamperModuleRuntime.For(request.Vehicle);
        if(runtime.TransferPending || runtime.Identity!=request.Address.Vehicle
            || !ReferenceEquals(runtime.Stations.Find(request.Address.Installation)?.State,request.State))return false;
        var current=RebirthCamperStationSnapshot.Capture(request.State.InstallationId,request.State);
        if(current.Length!=request.Snapshot.Length)return false;
        for(int i=0;i<current.Length;i++)if(current[i]!=request.Snapshot[i])return false;
        return request.CurrentSession();
    }
    private static string Digest(byte[] bytes)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");}
}
