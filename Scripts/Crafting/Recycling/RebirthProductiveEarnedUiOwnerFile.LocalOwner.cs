using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal sealed class LocalOwnerScope:IDisposable
    {
        internal Guid Operation;internal World World;internal TileEntityWorkstation Station;internal EntityPlayerLocal Actor;
        internal XUiC_WorkstationWindowGroup Window;internal long WindowSession;internal string Owner,Creation,Root,Name,Path;internal int Deaths;
        internal DeferredWitness[] Members;internal XElement BeforeFrame,AfterFrame;
        internal readonly List<IDisposable> MemberRetentions=new List<IDisposable>();
        internal byte[] NativeBefore,NativeAfter,BeforeFile;internal int BeforeOffset,AfterOffset;internal string BeforeFilePath;
        internal bool BeforeSaved,BeforePublished,Attempted,Invoking,Consumed,OwnerSettled,Unknown,Closed;
        private int workers;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal LocalOwnerTerminalPublication Terminal;internal bool TerminalAccepted,HandoffAccepted,NativeCloseObserved;
        internal Guid CachedOperation;
        internal object CachedPending;
        private readonly object resourceGate=new object();private IDisposable allocation;private int references=1;
        private long beforeBytes,afterBytes,nativeSize,publicationBytes;
        internal LocalOwnerScope(IDisposable budget){allocation=budget;}
        private long Required(long before,long after,long native,long publication)=>checked(48L*1024*1024+65536+4*(before+after)+16*native+publication);
        internal bool ReserveFrame(long utf8,bool after)
        {
            if(utf8<1||utf8>Maximum)return false;
            lock(resourceGate){long first=Math.Max(beforeBytes,after?0:utf8),later=Math.Max(afterBytes,after?utf8:0);
                if(Closed||allocation==null||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,Required(first,later,nativeSize,publicationBytes)))return false;
                beforeBytes=first;afterBytes=later;return true;}
        }
        internal bool ReserveNative(long bytes)
        {
            if(bytes<1||bytes>Maximum)return false;
            lock(resourceGate){long measured=Math.Max(nativeSize,bytes);
                if(Closed||allocation==null||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,Required(beforeBytes,afterBytes,measured,publicationBytes)))return false;
                nativeSize=measured;return true;}
        }
        internal IDisposable EnterPublication(long bytes)
        {
            if(bytes<1)return null;lock(resourceGate)
            {long enlarged=checked(publicationBytes+16*bytes);if(Closed||allocation==null||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,Required(beforeBytes,afterBytes,nativeSize,enlarged)))return null;
                publicationBytes=enlarged;return new PublicationLease(this,16*bytes);}
        }
        private sealed class PublicationLease:IDisposable
        {private LocalOwnerScope source;private readonly long bytes;internal PublicationLease(LocalOwnerScope value,long bytes){source=value;this.bytes=bytes;}
         public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);if(value==null)return;lock(value.resourceGate){value.publicationBytes-=bytes;
            if(!value.Closed&&value.allocation!=null)RebirthProductiveEarnedObservationBudget.TryResizeDetached(value.allocation,value.Required(value.beforeBytes,value.afterBytes,value.nativeSize,value.publicationBytes));}}}
        internal IDisposable Retain(){lock(resourceGate){if(Closed||allocation==null)return null;references++;return new Lease(this);}}
        internal IDisposable RetainWorker(){lock(resourceGate){if(Closed||allocation==null||workers>=2)return null;workers++;references++;return new WorkerLease(this);}}
        private sealed class WorkerLease:IDisposable
        {private LocalOwnerScope source;internal WorkerLease(LocalOwnerScope value){source=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);if(value==null)return;lock(value.resourceGate){value.workers--;value.Release();}}}
        private void Release(){lock(resourceGate){if(--references!=0)return;allocation?.Dispose();allocation=null;}}
        private sealed class Lease:IDisposable
        {private LocalOwnerScope source;internal Lease(LocalOwnerScope value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        internal bool Current()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
            !Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==Name&&
            Root==System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir())&&Members.All(value=>!value.Closed&&value.Receipt?.CurrentSourceWithoutRow()==true);
        internal bool CurrentWindow()=>Current()&&ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Actor)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out long session)&&session==WindowSession&&
            LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true;
        internal bool BodyCurrent(bool after)
        {
            if(!Current())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return decoder!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,after?NativeAfter:NativeBefore,after?AfterOffset:BeforeOffset);
        }
        public void Dispose()
        {
            if(Closed)return;Closed=true;Watch?.Dispose();Watch=null;BeforeFrame=AfterFrame=null;BeforeFile=NativeBefore=NativeAfter=null;
            CachedPending=null;Terminal=null;Members=null;Window=null;Actor=null;World=null;Station=null;
            foreach(var lease in MemberRetentions)lease.Dispose();MemberRetentions.Clear();Release();
        }
    }
    private static readonly Dictionary<Guid,LocalOwnerScope> localOwners=new Dictionary<Guid,LocalOwnerScope>();
    internal static void SweepLocalOwnerWorld(World current)
    {
        if(!ThreadManager.IsMainThread())return;
        lock(gate)
        {
            foreach(var pair in localOwners.Where(value=>!ReferenceEquals(value.Value.World,current)).ToArray())
            {foreach(var member in pair.Value.Members){var receipt=member.Receipt;member.Dispose();receipt?.Dispose();}
             localOwners.Remove(pair.Key);pair.Value.Dispose();}
            foreach(var member in deferred.Where(value=>value.HostLocalOriginal!=null&&!ReferenceEquals(value.World,current)).ToArray())member.Dispose();
        }
    }
    [ThreadStatic]private static TileEntityWorkstation openingLocalOwner;
    internal static bool HasLocalOwnerAttempt(TileEntityWorkstation station)=>ThreadManager.IsMainThread()&&
        localOwners.Values.Any(value=>!value.Closed&&ReferenceEquals(value.Station,station));
    internal static bool DropUnusedHostLocalRead(RebirthProductiveEarnedUiDeferredTransport.HostLocalReceipt original)
    {
        if(!ThreadManager.IsMainThread()||HasLocalOwnerAttempt(original.Original.Station))return false;
        lock(gate)
        {
            var members=deferred.Where(value=>ReferenceEquals(value.HostLocalOriginal?.Source,original)).ToArray();
            if(members.Any(value=>value.Consumed||value.Before!=null||value.NativeAllocation!=null))return false;
            foreach(var member in members)member.Dispose();
            return true;
        }
    }
    internal static bool HoldsLocalOwner(TileEntityWorkstation station)=>ThreadManager.IsMainThread()&&localOwners.Values.Any(value=>
        !value.Closed&&!value.OwnerSettled&&!value.HandoffAccepted&&ReferenceEquals(value.Station,station)&&ReferenceEquals(value.World,GameManager.Instance?.World))||
        ThreadManager.IsMainThread()&&ReferenceEquals(openingLocalOwner,station)&&station!=null;
    internal static object BeginLocalOwnerWindowOpen(XUiC_WorkstationWindowGroup window,bool run)
    {
        var previous=openingLocalOwner;
        if(!run||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||
            window.xui?.playerUI?.entityPlayer is not EntityPlayerLocal actor||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return previous;
        var station=window.WorkstationData?.TileEntity;
        if(station!=null)RebirthProductiveEarnedUiDeferredTransport.ObserveLoadedLocalOwnerReceipt(GameManager.Instance.World,station,actor);
        lock(gate)if(station!=null&&deferred.Any(value=>!value.Closed&&value.ImportedOriginal==null&&ReferenceEquals(value.Station,station)&&value.EntityId==actor.entityId&&
            value.Owner==identity.StorageKey&&value.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&value.Receipt?.CurrentAggregateMembership()==true))openingLocalOwner=station;
        return previous;
    }
    internal static void EndLocalOwnerWindowOpen(object previous){openingLocalOwner=previous as TileEntityWorkstation;}
    internal static void ObserveLocalOwnerWindowClosed(XUiC_WorkstationWindowGroup window,long previousSession)
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var scope in localOwners.Values.Where(value=>!value.Closed&&ReferenceEquals(value.Window,window)&&value.WindowSession==previousSession).ToArray())
        {
            try
            {
                if(!scope.Current()||scope.Invoking||scope.Unknown||!ReferenceEquals(window.WorkstationData?.TileEntity,scope.Station)||
                    !ReferenceEquals(window.xui?.playerUI?.entityPlayer,scope.Actor))continue;
                if(scope.Consumed&&scope.BodyCurrent(true))
                {
                    if(SaveLocalStage(scope,"closedConsumed",new XElement("localNativeOwnerClose",new XAttribute("window",previousSession),
                        new XAttribute("consumed",true),new XAttribute("nativeAfter",Hash(scope.NativeAfter)))))scope.NativeCloseObserved=true;
                }
                else if(!scope.Attempted&&!scope.Consumed&&scope.BodyCurrent(false)&&scope.Members.All(value=>
                    !value.Consumed&&value.Before==null&&value.Receipt.CurrentAggregateMembership()))
                {
                    // The exact known Check gate never admitted this scope.
                    // Actual final-run close + original unchanged membership
                    // proves unused; receipt absence supplies no permission.
                    if(!SaveLocalStage(scope,"closedUnused",new XElement("localNativeOwnerClose",new XAttribute("window",previousSession),
                        new XAttribute("consumed",false),new XAttribute("nativeBefore",Hash(scope.NativeBefore)))))continue;
                    localOwners.Remove(scope.Operation);scope.Dispose();
                }
            }
            catch{/* Uncertain close retains custody and cannot rearm an attempted Check. */}
        }
    }
    internal static bool CanUseLocalKnownNativeRewards(TileEntityWorkstation station,EntityPlayerLocal player)
    {
        if(!ThreadManager.IsMainThread()||station==null||player==null)return true;
        lock(gate)
        {
            var known=deferred.Where(value=>!value.Closed&&value.ImportedOriginal==null&&ReferenceEquals(value.Station,station)&&value.Receipt!=null&&
                station.CraftCompleteList?.Any(row=>ReferenceEquals(row,value.Receipt.Receipt))==true).ToArray();
            if(known.Length==0)return true;
            var affected=known.Where(value=>value.EntityId==player.entityId).ToArray();if(affected.Length==0)return true;
            var scope=localOwners.Values.SingleOrDefault(value=>!value.Closed&&ReferenceEquals(value.Station,station));
            return scope!=null&&scope.Invoking&&scope.Attempted&&scope.BeforeSaved&&scope.BeforePublished&&scope.CurrentWindow()&&
                ReferenceEquals(scope.Actor,player)&&affected.Length==scope.Members.Length&&affected.All(value=>scope.Members.Contains(value));
        }
    }
    internal static void ObserveLocalOwnerWindowOpened(XUiC_WorkstationWindowGroup window,long session)
    {
        if(!ThreadManager.IsMainThread()||window?.WorkstationData?.TileEntity is not TileEntityWorkstation station||GameManager.Instance?.World?.IsRemote()!=false||
            window.xui?.playerUI?.entityPlayer is not EntityPlayerLocal actor||LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true)return;
        LocalOwnerScope scope=null;IDisposable allocation=null;
        try{lock(gate)
        {
            if(localOwners.Values.Any(value=>!value.Closed&&ReferenceEquals(value.Station,station))||localOwners.Count>=4||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return;
            var members=deferred.Where(value=>!value.Closed&&!value.Consumed&&value.ImportedOriginal==null&&ReferenceEquals(value.Station,station)&&value.EntityId==actor.entityId&&value.Receipt!=null).ToArray();
            if(members.Length<1||members.Length>4||members.Any(value=>!value.Current()||value.Owner!=identity.StorageKey||
                value.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||value.Receipt.aggregate.Recipient.Deaths!=actor.Died||!value.Receipt.CurrentAggregateMembership()))return;
            foreach(var group in members.GroupBy(value=>value.Receipt.Receipt))
                if(group.First().Receipt.AllContributions.Length!=group.Count())return;
            if(station.CraftCompleteList?.Count(value=>value.CrafterEntityID==actor.entityId)!=members.Select(value=>value.Receipt.Receipt).Distinct().Count())return;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(48L*1024*1024+65536,out allocation))return;
            scope=new LocalOwnerScope(allocation){Operation=Guid.NewGuid(),World=actor.world,Station=station,Actor=actor,Window=window,WindowSession=session,
                Owner=identity.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Name=identity.CanonicalId,
                Root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),Members=members};allocation=null;
            foreach(var member in members){var lease=member.RetainLocalOwnerObservation();if(lease==null)return;scope.MemberRetentions.Add(lease);}
            scope.Path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"local-native-owner",scope.Operation.ToString("N")+".xml");
            var recipes=members.Select(value=>value.Receipt.Receipt.RecipeName).Distinct(StringComparer.Ordinal).ToArray();
            if(!TryMeasureDeferredLiveFrame(actor,recipes,out int bytes)||!scope.ReserveFrame(bytes,false))return;
            scope.BeforeFrame=CaptureDeferredLiveFrame(actor,recipes);
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureCurrent(station,out scope.NativeBefore,out scope.BeforeOffset))return;
            if(!SaveLocalStage(scope,"original",new XElement("localNativeOwnerOriginal",new XAttribute("window",session),new XElement("membership",members.Select(value=>
                new XElement("member",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("recipient",value.Owner),new XAttribute("creation",value.Creation)))))))return;
            foreach(var member in members){member.Actor=actor;member.Deaths=actor.Died;member.PlayerRoot=scope.Root;member.PlayerName=scope.Name;}
            localOwners.Add(scope.Operation,scope);scope=null;
        }}catch{/* Only an actual successful native OnOpen can bind a local owner. */}finally{scope?.Dispose();allocation?.Dispose();}
    }
    private static bool SaveLocalStage(LocalOwnerScope scope,string stage,XElement evidence)
    {
        long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(evidence)+65536;
        using(var workspace=scope.EnterPublication(size))return workspace!=null&&RebirthWorkstationCompletionPublication.Write(scope.Path+"."+stage,
            new XElement("workstationCompletionIntent",new XAttribute("operation",scope.Operation.ToString("N")),
            new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(scope.World)),new XElement("before",evidence)));
    }
}
