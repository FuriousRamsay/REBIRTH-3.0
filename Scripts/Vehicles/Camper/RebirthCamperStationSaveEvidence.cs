using System;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

// A requested save is not evidence. Only primary native-file readback mints a
// publication. This primitive grants no queue, refund or progression authority.
public static class RebirthCamperStationSaveEvidence
{
    private static readonly ConditionalWeakTable<RebirthCamperStationState,Request> pending
        = new ConditionalWeakTable<RebirthCamperStationState,Request>();
    private sealed class VehicleRequests
    {internal readonly System.Collections.Generic.HashSet<Request> Entries=new System.Collections.Generic.HashSet<Request>();}
    private static readonly ConditionalWeakTable<EntityVehicle,VehicleRequests> vehiclePending
        =new ConditionalWeakTable<EntityVehicle,VehicleRequests>();
    private static void RegisterVehiclePending(EntityVehicle vehicle,Request request)
    {
        vehiclePending.GetValue(vehicle,_=>new VehicleRequests()).Entries.Add(request);
    }
    internal static bool IsVehiclePending(EntityVehicle vehicle)
    {
        if(vehicle==null||!vehiclePending.TryGetValue(vehicle,out var group))return false;
        var entries=group.Entries;
        foreach(var request in entries)
            if(!request.Closed&&UnityEngine.Time.realtimeSinceStartup<request.Deadline)return true;
        foreach(var request in new System.Collections.Generic.List<Request>(entries))request.Dispose();
        return false;
    }
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
        internal IRebirthStationSnapshotByteBudget Budget;
        internal IDisposable SnapshotAllocation,ItemAllocation,ModuleAllocation;
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
            if(Vehicle!=null&&vehiclePending.TryGetValue(Vehicle,out var group))
            {group.Entries.Remove(this);if(group.Entries.Count==0)vehiclePending.Remove(Vehicle);}
            if(Budget!=null)
            {Snapshot=null;InstalledItem=null;Expected?.Dispose();Expected=null;SnapshotAllocation?.Dispose();ItemAllocation?.Dispose();ModuleAllocation?.Dispose();SnapshotAllocation=ItemAllocation=ModuleAllocation=null;}
        }
    }
    public sealed class Publication : IDisposable
    {
        private byte[] snapshot;
        private ItemStack installedItem;
        private IRebirthStationSnapshotByteBudget budget;
        private IDisposable snapshotAllocation,itemAllocation,moduleAllocation;
        public RebirthCamperStationAddress Address { get; private set; }
        public string StationDigest { get; private set; }
        public string VehicleEntityDigest { get; private set; }
        public string VehicleBagDigest { get; private set; }
        public string SaveDirectory { get; private set; }
        public int VehicleEntityId { get; private set; }
        public int VehicleClass { get; private set; }
        internal Publication(Request request)
        {
            budget=request.Budget;
            try
            {
                if(budget!=null&&!budget.TryReserveAllocation(checked(2L*request.Snapshot.Length+4096),out snapshotAllocation))throw new InvalidOperationException("Station publication allocation pressure.");
                snapshot=(byte[])request.Snapshot.Clone();
                installedItem=CloneInstalled(request.InstalledItem,budget,out itemAllocation);
            }
            catch{Dispose();throw;}
            Address=request.Address;StationDigest=Digest(request.Snapshot);
            VehicleEntityDigest=Digest(request.Expected.Entity);VehicleBagDigest=Digest(request.Expected.Bag);
            SaveDirectory=request.Expected.SaveDirectory;
            VehicleEntityId=request.Expected.Id;VehicleClass=request.Expected.Class;
            moduleAllocation=request.ModuleAllocation;request.ModuleAllocation=null;
        }
        // Decode into a detached object. Neither callers nor later native ticks
        // can rewrite the publication's saved inventory/queue observation.
        public RebirthCamperStationState RestoreStationSnapshot()
            => RebirthCamperStationSnapshot.Restore(Address.Installation,installedItem.Clone(),(byte[])snapshot.Clone());
        // Scope the detached readback graph and its clones under the original
        // optional observer budget. The predicate must not retain the graph.
        // Native lastTickTime can change during Request.Dispose, so a current
        // full serialized-image comparison is not a replacement for readback.
        internal bool ReadStationSnapshot(Func<RebirthCamperStationState,bool> predicate)
        {
            if(!ThreadManager.IsMainThread()||snapshot==null||installedItem==null||predicate==null)return false;
            IDisposable allocation=null,item=null;
            try
            {
                const long nativeGraph=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace/3;
                if(budget!=null&&!budget.TryReserveAllocation(checked(2*nativeGraph+2L*snapshot.Length+128*1024),out allocation))return false;
                var installed=CloneInstalled(installedItem,budget,out item);
                var restored=RebirthCamperStationSnapshot.Restore(Address.Installation,installed,(byte[])snapshot.Clone());
                return predicate(restored);
            }
            finally{item?.Dispose();allocation?.Dispose();}
        }
        public void Dispose(){snapshot=null;installedItem=null;snapshotAllocation?.Dispose();itemAllocation?.Dispose();moduleAllocation?.Dispose();snapshotAllocation=itemAllocation=moduleAllocation=null;budget=null;}
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
    internal static Request BeginProcessingRecovery(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current,IRebirthStationSnapshotByteBudget budget=null)
        =>BeginCore(state,address,current,budget);
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
    private static Request BeginCore(RebirthCamperStationState state,RebirthCamperStationAddress address,Func<bool> current,IRebirthStationSnapshotByteBudget budget=null)
    {
        if(!ThreadManager.IsMainThread()||state==null||!address.IsValid||current==null||!current())
            throw new InvalidOperationException("Original authenticated station observation required.");
        var vehicle=state.BoundVehicle;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        if(IsPending(state))throw new InvalidOperationException("Station already awaits native save confirmation.");
        var request=new Request {Budget=budget,Vehicle=vehicle,State=state,Address=address,
            Deadline=UnityEngine.Time.realtimeSinceStartup+30f,CurrentSession=current};
        // TickMobile pauses a pending station by setting this clock to zero.
        // Freeze it BEFORE either snapshot is captured, otherwise the first
        // vehicle tick invalidates our own exact readback and restarts saving.
        // Dispose already restarts the clock without accumulating paused time.
        state.lastTickTime=0;
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            if(runtime.Stations.GetSlot(slot)?.State is RebirthCamperStationState other)other.lastTickTime=0;
        pending.Add(state,request);
        RegisterVehiclePending(vehicle,request);
        try
        {
            if(budget==null)runtime.Persist(true);else runtime.PersistProcessingRecovery(budget,out request.ModuleAllocation);
            request.InstalledItem=CloneInstalled(runtime.Stations.Find(address.Installation).InstalledItem,budget,out request.ItemAllocation);
            request.Snapshot=RebirthCamperStationSnapshot.CaptureCharged(state.InstallationId,state,budget,out request.SnapshotAllocation);
            request.Expected=RebirthCamperSavedVehicle.RequestSave(vehicle,budget);
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
        if(!current){request.Dispose();request.ConfirmedPublication.Dispose();request.ConfirmedPublication=null;return false;}
        // A verified publication belongs to this original request. Retain it
        // if native dirty cleanup throws, then deliver once after cleanup.
        request.Dispose();
        try{current=request.CurrentSession();}catch{current=false;}
        if(!current){request.ConfirmedPublication.Dispose();request.ConfirmedPublication=null;return false;}
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
        IDisposable allocation=null;
        try
        {
            var current=RebirthCamperStationSnapshot.CaptureCharged(request.State.InstallationId,request.State,request.Budget,out allocation);
            if(current.Length!=request.Snapshot.Length)return false;
            for(int i=0;i<current.Length;i++)if(current[i]!=request.Snapshot[i])return false;
            return request.CurrentSession();
        }
        finally{allocation?.Dispose();}
    }
    internal static ItemStack CloneInstalled(ItemStack item,IRebirthStationSnapshotByteBudget budget,out IDisposable retained)
    {
        retained=null;if(budget==null)return item.Clone();
        const long native=196608;
        const long graph=512L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
            16L*(native/4)+384L*(native/5)+2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes;
        const long scratch=256L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
            128L*(native/5)+2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes+4*native+65536;
        if(!budget.TryReserveAllocation(checked(3*graph+scratch+4*native),out var work))throw new InvalidOperationException("Installed item preflight allocation pressure.");
        try
        {
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{item},262144,349528,out var text))throw new InvalidOperationException("Installed item preflight failed.");
            var frame=Convert.FromBase64String(text);long actual=160;
            if(frame.Length<4||frame[0]!=1||frame[1]!=1)throw new InvalidDataException();
            if((frame[2]|frame[3]<<8)!=0)
            {
                if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(frame,4,out var measured,out int size)||size!=frame.Length-4)throw new InvalidDataException();
                actual=checked(actual+measured.CloneAllocationBound);
            }
            else if(frame.Length!=4)throw new InvalidDataException();
            if(!budget.TryReserveAllocation(checked(3*actual+4096),out retained))throw new InvalidOperationException("Installed item allocation pressure.");
            return item.Clone();
        }
        catch{retained?.Dispose();retained=null;throw;}
        finally{work.Dispose();}
    }
    private static string Digest(byte[] bytes)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");}
}
