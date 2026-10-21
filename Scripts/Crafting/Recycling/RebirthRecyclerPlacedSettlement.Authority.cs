using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class AuthorityOriginal:IDisposable
    {
        internal Original Source;internal StationPublication Station;internal OwnerPublication Owner;internal AccessHold Access;
        internal LoadedStation Reentry;
        internal LoadedStation PendingReentry;
        internal bool UnusedAdmission;
        internal byte[] Terminal;internal string TerminalPath;internal IDisposable TerminalAllocation;
        internal bool FinancialReleased=>Terminal!=null&&Station?.MarkerRemoved==true;
        public void Dispose(){Terminal=null;Owner?.Dispose();Station?.Dispose();Source?.Dispose();Owner=null;Station=null;Source=null;
            if(!ReferenceEquals(PendingReentry,Reentry))PendingReentry?.Dispose();PendingReentry=null;
            Reentry?.Dispose();Reentry=null;TerminalAllocation?.Dispose();TerminalAllocation=null;}
    }
    private sealed class AuthorityReceipt
    {
        internal Guid Operation;internal string Owner,Creation,Purpose,Birth,Path,Digest;internal long Length;
        internal WeakReference<World> World;internal WeakReference<TileEntityWorkstation> Station;
    }
    private static readonly List<AuthorityOriginal> authorityOriginals=new List<AuthorityOriginal>();
    private static readonly Dictionary<Guid,AuthorityReceipt> authorityReceipts=new Dictionary<Guid,AuthorityReceipt>();
    [ThreadStatic]private static AuthorityOriginal startingAuthorityOwner;
    private static bool authorityInstalled;
    internal sealed class AccessHold
    {
        private IStationOriginal source;private string birth;private bool pairSaved,terminal,unused;
        private readonly World world;private TileEntityWorkstation station;private readonly object provider;private readonly string save;
        private AccessHold(IStationOriginal source,string birth)
        {this.source=source;this.birth=birth;world=source.World;station=source.Station;provider=world.ChunkCache?.ChunkProvider;save=Path.GetFullPath(GameIO.GetSaveGameDir());}
        internal static AccessHold CapturePending(IStationOriginal source)=>source?.Current()==true?new AccessHold(source,null):null;
        internal bool RebindClosedAuthority(ClosedAuthorityCompletion previous,RemoteHostOriginal next)
        {
            if(!ReferenceEquals(source,previous)||birth!=null||pairSaved||terminal||unused||!ReferenceEquals(previous.Attached,next)||
                !ReferenceEquals(next.ClosedAuthority,previous)||!previous.Current()||!next.Current()||!ReferenceEquals(next.World,world)||!ReferenceEquals(next.Station,station))return false;
            source=next;return true;
        }
        internal bool RollbackClosedAuthority(RemoteHostOriginal previous,ClosedAuthorityCompletion next)
        {
            if(!ReferenceEquals(source,previous)||birth!=null||pairSaved||terminal||unused||!ReferenceEquals(next.Attached,previous)||
                previous.PlanSaved&&next.Pending?.CanReturnUnenteredAttachment(previous)!=true||
                previous.Publication!=null||previous.OwnerCheckpoint!=null||!previous.NativeLineageCurrent()||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(previous.Station,previous.BeforeNative,previous.BeforeOffset))return false;
            source=next;return true;
        }
        internal bool Bind(StationPublication publication)
        {if(birth!=null||unused||!ReferenceEquals(publication?.Original,source)||source.Current()!=true)return false;birth=publication.Birth;return birth!=null;}
        internal bool RebindPendingClosedLoad(ClosedAuthorityCompletion original,LoadedStation loaded)
        {
            if(!ReferenceEquals(source,original)||birth!=null||pairSaved||terminal||unused||original.Attached!=null||
                !original.Current()||original.Pending?.IsLoaded(loaded)!=true||!ReferenceEquals(original.Station,loaded.Station)||
                !ReferenceEquals(original.World,world)||!loaded.MatchesOriginal(original.Image))return false;
            station=loaded.Station;return true;
        }
        internal static AccessHold Capture(Original source,StationPublication publication)=>source?.Current()==true&&ReferenceEquals(publication?.Original,source)?new AccessHold(source,publication.Birth):null;
        internal static AccessHold CaptureRemote(RemoteHostOriginal source,StationPublication publication)=>source?.Current()==true&&ReferenceEquals(publication?.Original,source)?new AccessHold(source,publication.Birth):null;
        internal bool Ready()=>unused?ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&
            ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save:
            terminal&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&
            ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&Path.GetFullPath(GameIO.GetSaveGameDir())==save&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out string current)&&current==birth;
        internal bool ReadyFor(ClientInfo peer,INetConnection connection)=>Ready()||source is RemoteHostOriginal remote&&
            ReferenceEquals(remote.Peer,peer)&&ReferenceEquals(remote.Connection,connection)&&remote.OwnerCurrent()&&remote.Current()&&
            !remote.AdmissionUnknown&&remote.Publication!=null&&
            (remote.AcceptedStation?remote.Publication.SavedAfter():remote.Publication.SavedBefore());
        internal bool RebindActualSource(RemoteHostOriginal original,LoadedStation loaded,StationPublication publication)
        {
            if(!ReferenceEquals(source,original)||!ReferenceEquals(original.PendingReentry,loaded)||loaded?.Current()!=true||
                !ReferenceEquals(publication?.Original,source)||!loaded.MatchesOriginal(source.Image)||!source.Current())return false;
            birth=publication.Birth;station=original.Station;pairSaved=terminal=false;return birth!=null;
        }
        // Only the retained controller's positive never-entered outcome can
        // release a pending ordinary native lock request. This grants no setter.
        internal bool ObserveUnused(Original candidate)
        {
            var original=authorityOriginals.SingleOrDefault(value=>ReferenceEquals(value.Source,candidate));
            if(!ReferenceEquals(candidate,source)||original?.UnusedAdmission!=true||original.Station!=null||original.Owner!=null||birth!=null||!source.Current())return false;
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+".unused-admission.xml");
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var outcome=record?.Element("recyclerUnusedAdmission");
            if((bool?)outcome?.Attribute("nativeStationEntered")!=false||(bool?)outcome?.Attribute("nativeOwnerEntered")!=false||
                (string)record.Attribute("operation")!=source.Operation.ToString("N")||(string)record.Attribute("world")!=source.WorldKey||
                !XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),XElement.Parse(Encoding.UTF8.GetString(source.Image))))return false;
            unused=true;return true;
        }
        internal void ObserveTerminal(OwnerPublication owner,StationPublication station)
        {if(ReferenceEquals(owner?.Original,source)&&ReferenceEquals(station?.Original,source)&&owner.OwnerSaved&&station.SavedAfter())pairSaved=true;}
        internal void ObserveCleanup(StationPublication station)
        {if(pairSaved&&ReferenceEquals(station?.Original,source)&&station.CleanupSaved())terminal=true;}
        internal void ObserveRemoteTerminal(RemoteOwnerCheckpoint owner,StationPublication station)
        {if(source is RemoteHostOriginal remote&&ReferenceEquals(owner?.Source,remote)&&ReferenceEquals(station?.Original,source)&&owner.Phase==3&&
            owner.Completed==remote.Slots.Length&&owner.CurrentSaved()&&station.SavedAfter())pairSaved=true;}
        internal bool ObserveRemoteUnused(RemoteHostOriginal candidate)
        {
            if(!ReferenceEquals(source,candidate)||!candidate.UnusedRequested||!candidate.NativeLineageCurrent()||candidate.AcceptedStation||
                candidate.Publication!=null&&!candidate.Publication.CleanupSaved()||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(candidate.Station,candidate.BeforeNative,candidate.BeforeOffset))return false;
            unused=true;return true;
        }
    }
    internal static AccessHold[] CaptureAccessHolds(World world,IEnumerable<TileEntityWorkstation> targets)=>authorityOriginals.Where(value=>
        ReferenceEquals(value.Source?.World,world)&&targets.Any(target=>ReferenceEquals(target,value.Source.Station))).Select(value=>value.Access)
        .Concat(remoteRecyclerOriginals.Values.Where(value=>ReferenceEquals(value.World,world)&&targets.Any(target=>ReferenceEquals(target,value.Station))).Select(value=>value.Access))
        .Concat(closedAuthorityCompletions.Where(value=>ReferenceEquals(value.World,world)&&targets.Any(target=>ReferenceEquals(target,value.Station))).Select(value=>value.Access)).Where(value=>value!=null).ToArray();
    internal static void InstallAuthorityController(Harmony harmony)
    {
        if(authorityInstalled)return;
        RebirthProductiveEarnedNativeBody.Install();RebirthRemoteResourceNativePublication.Install();RebirthLogisticsEndpointIdentity.Install();
        RebirthCookingOutputDelivery.Install();RebirthProductiveEarnedUiOwnerFile.Install();InstallOwnerFile(harmony);
        RebirthDogLifecycleService.InstallRecyclerNativeOwnerLoadObservers();
        RebirthRemoteResourcePlacedStationOriginal.InstallCompletionCachedNativeLoadObservers();
        RebirthProductiveEarnedRemoteHost.Install();RebirthProductiveEarnedCancellation.InstallBeforeFlushObserver();
        RebirthProductiveEarnedUiDeferredTransport.InstallRecipientWindowObserver();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();RebirthProductiveEarnedUiDeferredTransport.InstallAuthorityNativeReceiptObserver(harmony);
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperStationGridGesture));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperStationGridDirectInsert));
        ModEvents.GameUpdate.RegisterHandler(PumpAuthority);authorityInstalled=true;
    }
    private static bool AuthorityOwnerHeld(EntityPlayerLocal actor)=>authorityOriginals.Any(value=>!value.FinancialReleased&&!ReferenceEquals(value,startingAuthorityOwner)&&
        ReferenceEquals(value.Source?.Actor,actor)&&value.Source.OwnerLineageCurrent());
    private static bool AuthorityStationHeld(TileEntityWorkstation station)=>authorityOriginals.Any(value=>!value.FinancialReleased&&ReferenceEquals(value.Source?.Station,station)&&
        ReferenceEquals(value.Source.World,GameManager.Instance?.World)&&ReferenceEquals(value.Source.World.GetTileEntity(station.ToWorldPos()),station));
    internal static bool CanProcessNativeUpload(NetPackageTileEntity packet,World world)=>packet==null||world==null||world.IsRemote()||
        !ClosedAuthorityStationHeld(world.GetTileEntity(packet.teWorldPos) as TileEntityWorkstation)&&!HasNativeOriginalMarker(world.GetTileEntity(packet.teWorldPos) as TileEntityWorkstation)&&!authorityOriginals.Any(value=>!value.FinancialReleased&&ReferenceEquals(value.Source?.World,world)&&value.Source.Station.ToWorldPos()==packet.teWorldPos&&
            ReferenceEquals(world.GetTileEntity(packet.teWorldPos),value.Source.Station));
    // Consumes the already frozen actual native action only on true. This is
    // authority-local; an authenticated remote issuer is a separate source.
    internal static bool TryRetainAuthority(Original original)
    {
        if(!authorityInstalled||original?.Current()!=true||original.World.IsRemote()||authorityOriginals.Count>=4||HoldsStation(original.Station)||
            RebirthCookingOutputDelivery.IsHeld(original.Actor)||RecyclerOwnerConflict(original.Actor)||
            !original.MatchesOwnerCheckpoint(original.Actor.bag.ItemGrid.items,original.Actor.inventory.ItemGrid.items,0))return false;
        if(original.Purpose=="complete")
        {if(!RebirthRecyclerRuntime.IsProcessing(original.Station)&&!IsViewedLocalAdmission(original))return false;}
        else
        {
            var window=XUiC_RebirthStationWorkspace.ActiveInstance;
            if(window?.windowGroup?.isShowing!=true||!ReferenceEquals(window.WorkstationData?.TileEntity,original.Station)||
                !ReferenceEquals(window.xui?.playerUI?.entityPlayer,original.Actor)||LockManager.Instance?.IsLockedByLocalPlayer(original.Station,0)!=true||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out _))return false;
        }
        var plan=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(original.Image))));
        string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".plan.xml");
        string previousTerminal=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".terminal.xml");
        // Existing terminal identity may be acknowledged, never re-admitted as
        // a new payment even when its current inventory happens to look alike.
        if(ArtifactNodeExists(previousTerminal)||ArtifactNodeExists(previousTerminal+".tmp")||ArtifactNodeExists(previousTerminal+".bak")||
            ArtifactNodeExists(path)||ArtifactNodeExists(path+".tmp")||ArtifactNodeExists(path+".bak"))return false;
        // Once this actual action starts writing its original, retain that
        // source even on an unsuccessful admission. A later identical XML
        // document cannot stand in for this never-entered native action.
        var pendingAccess=AccessHold.CapturePending(original);if(pendingAccess==null)return false;
        var retained=new AuthorityOriginal{Source=original,UnusedAdmission=true,Access=pendingAccess};authorityOriginals.Add(retained);
        try
        {
        if(!RebirthWorkstationCompletionPublication.Write(path,plan)||!XNode.DeepEquals(plan,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
        if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,original.Station,out _))
        {
            using(var birth=RebirthLogisticsEndpointIdentity.Begin(original.World,original.Station,()=>original.Current()&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,original.BeforeNative,original.BeforeOffset)))
                if(birth==null)return true;
        }
        retained.UnusedAdmission=false;
        var station=StationPublication.BeginBefore(original);if(station==null){retained.UnusedAdmission=true;return true;}
        retained.Station=station;retained.UnusedAdmission=false;
        // A failure after creating the actual marker remains the same unknown
        // original. It cannot abandon that marker and adopt it by equality.
        retained.Access.Bind(station);return true;
        }
        catch
        {
            // The controller already owns this source. In particular a native
            // marker callback may have thrown after mutation; retain unknown.
            return true;
        }
    }
    private static void PumpAuthority(ref ModEvents.SGameUpdateData data)
    {
        var world=GameManager.Instance?.World;
        PumpRemoteRecyclerOwner(world);
        PumpClosedAuthorityCompletions(world);
        PumpPlacedProtocol(world);
        PumpRecyclerClients(world);
        PumpViewedCompletions(world);
        SweepLoadedRecyclerOwners(world);
        PumpJoinedRecyclerTerminals(world);
        PumpJoinedRecyclerUnused(world);
        SweepLoadedRecyclerStations(world);
        PumpLoadedRecyclerCleanup(world);
        RecoverAuthorityHot(world);
        RecoverRemoteRecyclerHot(world);
        RecoverRemoteRecyclerReconnect(world);
        RecoverAuthorityCold(world);
        RecoverRemoteRecyclerCold(world);
        foreach(var item in authorityOriginals.ToArray())
        {
            try
            {
                if(!ReferenceEquals(item.Source.World,world)){authorityOriginals.Remove(item);item.Dispose();continue;}
                if(item.UnusedAdmission){RetireUnusedAdmission(item);continue;}
                if(item.PendingReentry!=null)continue;
                if(item.Access==null)continue;
                if(item.Terminal!=null){FinishAuthorityTerminal(item,world);continue;}
                if(item.Source.Current()!=true)continue;
                if(item.Owner==null)
                {
                    if(!item.Station.SavedBefore())continue;
                    startingAuthorityOwner=item;
                    try{item.Owner=OwnerPublication.Begin(item.Source);}finally{startingAuthorityOwner=null;}
                    continue;
                }
                if(!item.Station.AfterApplied){if(item.Owner.TrySaveBefore())item.Station.ApplyLocalAfter(item.Owner);continue;}
                if(!item.Owner.StationPaid){item.Owner.ArmPaidStation(item.Station);continue;}
                item.Owner.AdvanceOwner();if(!item.Owner.OwnerSaved)continue;
                var station=item.Station.CopySavedAfter();if(station==null)continue;
                var owner=item.Owner.CopySavedEvidence();if(owner==null)continue;
                var terminal=new XElement("workstationCompletionIntent",new XAttribute("operation",item.Source.Operation.ToString("N")),new XAttribute("world",item.Source.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(item.Source.Image))),new XElement("recyclerPlacedTerminal",
                        new XElement("actualOwnerSaved",owner),new XElement("actualStationSaved",station)));
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),item.Source.Operation.ToString("N")+".terminal.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,terminal)||!XNode.DeepEquals(terminal,RebirthWorkstationCompletionPublication.ReadPrimary(path)))continue;
                long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var terminalAllocation))continue;
                byte[] bytes;
                try{bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null||bytes.LongLength!=length){terminalAllocation.Dispose();continue;}}
                catch{terminalAllocation.Dispose();throw;}
                item.Access.ObserveTerminal(item.Owner,item.Station);item.Terminal=bytes;item.TerminalPath=path;item.TerminalAllocation=terminalAllocation;
                FinishAuthorityTerminal(item,world);
            }
            catch{/* Retain the same actual source. Never repeat a slot/native setter after an unknown attempt. */}
        }
        foreach(var old in authorityReceipts.Where(pair=>!pair.Value.World.TryGetTarget(out var saved)||!ReferenceEquals(saved,world)).Select(pair=>pair.Key).ToArray())authorityReceipts.Remove(old);
        RebirthProductiveEarnedUiDeferredTransport.PumpSelectedNativeAccessRequests(world);
    }
    private static void FinishAuthorityTerminal(AuthorityOriginal item,World world)
    {
        bool saved=item.Station.RetireTerminalMarker(item.TerminalPath,item.Terminal);
        // Both financial legs and their full pair terminal were saved before
        // this actual metadata Remove. Later native user actions are independent
        // originals; the cleanup snapshot grants them no permission.
        if(item.Station.MarkerRemoved&&item.Owner!=null){item.Owner.Dispose();item.Owner=null;}
        if(!saved)return;
        item.Access.ObserveCleanup(item.Station);if(!item.Access.Ready())return;
        var cleanup=item.Station.CopyCleanupSaved();if(cleanup==null)return;
        cleanup.Add(new XAttribute("terminal",RebirthStationPublicationRecord.Digest(item.Terminal)),new XAttribute("terminalPath",item.TerminalPath));
        var cleanupRecord=new XElement("workstationCompletionIntent",new XAttribute("operation",item.Source.Operation.ToString("N")),new XAttribute("world",item.Source.WorldKey),
            new XElement("before",XElement.Parse(Encoding.UTF8.GetString(item.Source.Image))),cleanup);
        string cleanupPath=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),item.Source.Operation.ToString("N")+".cleanup-saved.xml");
        if(!RebirthWorkstationCompletionPublication.Write(cleanupPath,cleanupRecord)||!XNode.DeepEquals(cleanupRecord,RebirthWorkstationCompletionPublication.ReadPrimary(cleanupPath))||!item.Station.CleanupSaved())return;
        // Zeroing old selector stamps is resource metadata only. The immutable
        // full terminal remains the replay identity; absence authorizes no row.
        if(item.Source.OwnerLineageCurrent())
        {item.Source.Actor.Buffs.SetCustomVar(item.Source.Receipt,0f);item.Source.Actor.Buffs.SetCustomVar(item.Source.Checkpoint,0f);}
        using(var hash=SHA256.Create())authorityReceipts[item.Source.Operation]=new AuthorityReceipt{Operation=item.Source.Operation,
            Owner=item.Source.Owner,Creation=item.Source.Creation,Purpose=item.Source.Purpose,Birth=item.Station.Birth,Path=item.TerminalPath,Length=item.Terminal.Length,
            Digest=Convert.ToBase64String(hash.ComputeHash(item.Terminal)),World=new WeakReference<World>(world),Station=new WeakReference<TileEntityWorkstation>(item.Source.Station)};
        var actor=item.Source.Actor;var native=item.Source.Station;
        authorityOriginals.Remove(item);item.Dispose();RebirthRecyclerRuntime.Refresh(actor,native);
    }
    internal static bool HasAuthorityTerminal(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string purpose)
    {
        if(actor==null||!authorityReceipts.TryGetValue(operation,out var proof)||proof.Purpose!=purpose||proof.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            !proof.World.TryGetTarget(out var world)||!ReferenceEquals(world,GameManager.Instance?.World)||!proof.Station.TryGetTarget(out var old)||!ReferenceEquals(old,station)||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=proof.Owner||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out string birth)||birth!=proof.Birth)return false;
        try
        {
            // Historical metadata acknowledgement only. No setters or source
            // restoration follow this immutable terminal identity check.
            for(string part=System.IO.Path.GetFullPath(proof.Path);!string.IsNullOrEmpty(part);part=System.IO.Path.GetDirectoryName(part))
                if((File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)return false;
            using(var file=new FileStream(proof.Path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())
                return file.Length==proof.Length&&Convert.ToBase64String(hash.ComputeHash(file))==proof.Digest;
        }
        catch{return false;}
    }
    internal static bool CanUseNativeEntry(BaseItemActionEntry entry)
    {
        var controller=entry?.ItemController;if(controller==null)return true;
        if(HoldsStation(controller.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity))return false;
        var active=XUiC_RebirthStationWorkspace.ActiveInstance;
        return active?.windowGroup?.isShowing!=true||!ReferenceEquals(active.xui?.playerUI?.entityPlayer,controller.xui?.playerUI?.entityPlayer)||
            !HoldsStation(active.WorkstationData?.TileEntity);
    }
}
