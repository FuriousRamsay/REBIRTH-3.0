using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Station-only original custody. These predicates never grant owner setters,
// source effects or a queue publication. Nothing calls this producer yet.
internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private sealed class StationHold
    {
        internal World World;internal string Directory;internal EntityPlayer Actor;
        internal RebirthCamperStationState Station;internal object Lease;
        internal RebirthRemoteResourceFetchedStationPlan.Plan Plan;internal bool Uninterrupted,CapacityMeasured;
    }
    private static readonly Dictionary<RebirthCamperStationState,StationHold> StationHolds=new Dictionary<RebirthCamperStationState,StationHold>();
    internal static bool BlocksStation(RebirthCamperStationState station)
    {
        // Lost leases/disconnect do not release the frozen queue. Only the
        // later named native Pending handoff or positive terminal can do so.
        return ThreadManager.IsMainThread()&&station!=null&&StationHolds.TryGetValue(station,out var hold)&&
            ReferenceEquals(hold.World,GameManager.Instance?.World)&&hold.Directory==GameIO.GetSaveGameDir();
    }
    internal static bool MatchesFrozenBefore(RebirthCamperStationState station)=>BlocksStation(station)&&StationHolds.TryGetValue(station,out var hold)&&SameFrozenStation(station,hold.Plan);
    private static bool SameFrozenStation(RebirthCamperStationState station,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        try
        {
            if(station==null||!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||station.InstallationId!=address.Installation||
                !RebirthCamperModuleRuntime.TryGetExisting(station.BoundVehicle,out var runtime)||runtime.Identity!=address.Vehicle||
                !ReferenceEquals(runtime.Stations.Find(address.Installation)?.State,station)||
                RebirthNativeItemCodec.Encode(runtime.Stations.Find(address.Installation).InstalledItem.itemValue)!=plan.InstalledDefinition)return false;
            var clone=RebirthCamperStationSnapshot.Restore(address.Installation,runtime.Stations.Find(address.Installation).InstalledItem,
                RebirthCamperStationSnapshot.Capture(address.Installation,station));
            clone.Tools=ItemStack.Clone(station.Tools);clone.Fuel=ItemStack.Clone(station.Fuel);clone.Input=ItemStack.Clone(station.Input);clone.Output=ItemStack.Clone(station.Output);
            clone.lastInput=ItemStack.Clone(station.lastInput);clone.CraftCompleteList=station.CraftCompleteList==null?null:new List<CraftCompleteData>(station.CraftCompleteList);
            clone.Queue=(RecipeQueueItem[])station.Queue.Clone();clone.lastTickTime=0;
            return RebirthCamperStationSnapshot.Capture(address.Installation,clone).SequenceEqual(plan.CopyBeforeStation());
        }
        catch{return false;}
    }
    private static bool CurrentStationOwner(EntityPlayer actor,RebirthRemoteResourceFetchedStationPlan.Plan plan,out RebirthCamperStationState station,out object lease)
    {
        station=null;lease=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!actor.IsSpawned()||actor.IsDead()||actor.Died!=plan.Deaths||
                actor.world.worldState.Guid!=plan.World||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=plan.OwnerKey||owner.CanonicalId!=plan.CanonicalOwner||
                !RebirthOriginalActionScope.Matches(actor,plan.OwnerKey,plan.Creation)||!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||
                !RebirthStationLiveAccess.TryGetMobileLease(actor,address,plan.Creation,out station,out lease)||RebirthCamperModuleRuntime.For(station.BoundVehicle).TransferPending)return false;
            return SameFrozenStation(station,plan);
        }
        catch{station=null;lease=null;return false;}
    }
    private static bool BindStationHold(EntityPlayer actor,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        if(!CurrentStationOwner(actor,plan,out var station,out var lease))return false;
        foreach(var old in StationHolds.Where(pair=>!ReferenceEquals(pair.Value.World,GameManager.Instance?.World)||pair.Value.Directory!=GameIO.GetSaveGameDir()).Select(pair=>pair.Key).ToArray())
            StationHolds.Remove(old); // Transient native references only; original artifacts remain intact.
        if(StationHolds.TryGetValue(station,out var retained))
        {
            if(retained.Plan.Source!=plan.Source||retained.Plan.Job!=plan.Job||retained.Plan.Binding!=plan.Binding||retained.Directory!=GameIO.GetSaveGameDir()||!ReferenceEquals(retained.World,actor.world))return false;
            // Reconnection changes only the authenticated current lease, never
            // the immutable original images or original lifetime.
            retained.Actor=actor;retained.Lease=lease;return retained.Uninterrupted;
        }
        if(StationHolds.Count>=32||RebirthCamperStationNativeActorPayment.IsHeld(station)||RebirthCamperStationGridCustody.IsHeld(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station))return false;
        if(!RebirthRemoteResourceDurableJournal.TryRead(plan.OwnerKey,plan.Source,out var root,out bool unknown)||unknown||root==null||root.Binding!=plan.Binding)return false;
        bool beforeEffects=root.Phase<=1&&root.Copy().Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0);
        StationHolds.Add(station,new StationHold{World=actor.world,Directory=GameIO.GetSaveGameDir(),Actor=actor,Station=station,Lease=lease,Plan=plan,Uninterrupted=beforeEffects});
        // A cold attempted original can retain station blocking custody, but
        // matching current images do not recreate an interrupted proof chain.
        return beforeEffects;
    }
    internal static bool CanPublishPlannedOwnerAction(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(!TryReadAdmission(root,out var original)||!Scope(actor,original))return false;
            if(TryReadPlan(root,out var retained))
            {
                if(!CurrentStationOwner(actor,retained,out var retainedStation,out _))return false;
                bool measured=StationHolds.TryGetValue(retainedStation,out var prior)&&prior.CapacityMeasured&&prior.Uninterrupted;
                if(!measured&&!RebirthRemoteResourceFetchedStationCapacity.TryMeasure(retainedStation.BoundVehicle,root,retained,out _))return false;
                if(!BindStationHold(actor,retained))return false;StationHolds[retainedStation].CapacityMeasured=true;return true;
            }
            // No replacement plan may overwrite an interrupted/corrupt old
            // artifact. A sole temporary candidate can finish publication only
            // after the same exact freshly rederived preeffect body compares
            // byte-for-byte in PublishPlan; disagreement remains unknown.
            string directory=PlanDirectory();
            string candidate=Path.Combine(directory,root.Operation.ToString("N")+".plan.tmp");
            if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory,root.Operation.ToString("N")+"*").Any(file=>file!=candidate))return false;
            if(!ValidateInput(original,true,out var address,out var recipe,out int count,out int tier,out _,out _,out _)||
                !RebirthRemoteResourceFetchedStationPlan.TryCreatePlanned(actor,address,original.Creation,root.Operation,original.Operation,recipe,count,tier,out var plan)||
                !CurrentStationOwner(actor,plan,out var station,out _)||
                !RebirthCamperStationNativeActorPayment.TryPreflightFetchedFrames(actor,station,address,original.Creation,plan,out _,out _)||
                !RebirthRemoteResourceFetchedStationCapacity.TryMeasure(station.BoundVehicle,root,plan,out _)||!PublishPlan(root,plan))return false;
            if(!BindStationHold(actor,plan))return false;StationHolds[station].CapacityMeasured=true;return true;
        }
        catch{return false;}
    }
    // Pending and unknown are deliberately not a no-payment disposition.
    internal enum PreparationStatus{Unknown,Waiting,Ready}
    internal static PreparationStatus PrepareBeforeSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(!TryReadAdmission(root,out var original)||!Scope(actor,original))return PreparationStatus.Unknown;
            if(!TryReadPlan(root,out var plan))
            {if(!CanPublishPlannedOwnerAction(actor,root)||!TryReadPlan(root,out plan))return PreparationStatus.Unknown;}
            if(!CurrentStationOwner(actor,plan,out var station,out _)||!RebirthCamperStationAddress.TryParse(plan.Address,out var address))return PreparationStatus.Unknown;
            if(!RebirthCamperStationNativeActorPayment.MatchesPendingOperation(station,plan.Job))
            {
                RebirthCamperStationNativeActorPayment.RestorePending(station.BoundVehicle);
                if(!RebirthCamperStationNativeActorPayment.MatchesPendingOperation(station,plan.Job)&&
                    (!BindStationHold(actor,plan)||!RebirthCamperStationNativeActorPayment.PrepareFetchedOriginal(actor,station,address,plan.Creation,plan.Job,plan.Source,plan.Binding)))return PreparationStatus.Unknown;
            }
            if(!RebirthCamperStationNativeActorPayment.TryResume(actor,station,address,plan.Creation))return PreparationStatus.Unknown;
            if(!RebirthCamperStationNativeActorPayment.TryGetFetchedSavedBefore(actor,station,plan.Job,out var before,out var after))return PreparationStatus.Waiting;
            if(!MatchesNativeFrames(actor,station,address,plan,before,after))return PreparationStatus.Unknown;
            // Native Pending now freezes the whole vehicle. Include planned
            // source cargo and mandatory effect metadata before any source effect.
            if(root.Copy().Element("sources").Elements("source").All(node=>(int)node.Attribute("attempted")==0)&&
                !RebirthRemoteResourceFetchedStationCapacity.TryMeasure(station.BoundVehicle,root,plan,out _))return PreparationStatus.Unknown;
            return PreparationStatus.Ready;
        }
        catch{return PreparationStatus.Unknown;}
    }
    private static bool MatchesNativeFrames(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,
        RebirthRemoteResourceFetchedStationPlan.Plan plan,byte[] before,byte[] after)=>
        MatchesNativeFrameImages(actor,station,address,plan,before,after)&&
        RebirthRemoteResourceStationPaymentWitness.TryReadSavedBefore(actor.world,plan.Job,plan.Address,plan.OwnerKey,plan.Creation,before,after,out _,out var definition)&&definition==plan.InstalledDefinition;
    private static bool MatchesNativeFrameImages(EntityPlayer actor,RebirthCamperStationState station,RebirthCamperStationAddress address,
        RebirthRemoteResourceFetchedStationPlan.Plan plan,byte[] before,byte[] after)=>
        RebirthCamperStationNativeActorPayment.TryPreflightFetchedFrames(actor,station,address,plan.Creation,plan,out var expectedBefore,out var expectedAfter)&&
        before.SequenceEqual(expectedBefore)&&after.SequenceEqual(expectedAfter);
    internal static bool TryReadForPrepare(EntityPlayer actor,RebirthCamperStationState expected,RebirthCamperStationAddress address,string creation,
        Guid job,Guid source,string binding,out RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        plan=null;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||root.Phase>1||root.Binding!=binding||
                root.Copy().Element("ownerAction")!=null||(int?)root.Copy().Attribute("ownerRejecting")==1||root.Copy().Element("sources").Elements("source").Any(node=>(int)node.Attribute("attempted")!=0)||
                !TryReadPlan(root,out var retained)||retained.Job!=job||retained.Address!=address.ToString()||retained.Creation!=creation||
                !CurrentStationOwner(actor,retained,out var station,out _)||!ReferenceEquals(station,expected)||
                !StationHolds.TryGetValue(station,out var hold)||!hold.Uninterrupted||hold.Plan.Source!=source||hold.Plan.Binding!=binding||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=retained.Deaths||!retained.CopyBeforeActor().Matches(before)||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(job),out float phase,out bool present)||!present||phase!=10f)return false;
            plan=retained;return true;
        }
        catch{return false;}
    }
    internal static bool MayAdvancePrepared(EntityPlayer actor,RebirthCamperStationState station,Guid job,byte[] before,byte[] after)
    {
        try
        {
            var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId)?.InstalledItem;
            if(installed==null)return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new System.Text.UTF8Encoding(false,true).GetString(after),8*1024*1024);
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed,Convert.FromBase64String(frame.Element("stationImage").Value));
            var row=model.Queue[(int)frame.Attribute("queueSlot")];
            if(row?.Recipe?.ingredients==null)return false;
            var marker=row.Recipe.ingredients.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true);
            if(marker==null||!marker.itemValue.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string sourceText)||!Guid.TryParseExact(sourceText,"N",out var source)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=2||
                !TryReadPlan(root,out var plan)||plan.Job!=job||!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||
                !MatchesNativeFrames(actor,station,address,plan,before,after)||!PrepareNativePermission(actor,station,address,plan.Creation,job,source,root.Binding))return false;
            return MatchesNativePermission(root,plan)||RebirthRemoteResourceDurableJournal.TryRead(root.Owner,source,out var updated,out unknown)&&!unknown&&MatchesNativePermission(updated,plan);
        }
        catch{return false;}
    }
    internal static bool TryReadForBegin(EntityPlayer actor,RebirthCamperStationState expected,RebirthCamperStationAddress address,string creation,
        Guid job,Guid sourceOperation,string binding,out RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        plan=null;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,sourceOperation,out var root,out bool unknown)||unknown||root?.Phase!=2||root.Binding!=binding||
                (int)root.Copy().Attribute("outcome")!=1||(int?)root.Copy().Attribute("ownerRejecting")==1||
                root.Copy().Element("ownerRefund")!=null||root.Copy().Attribute("ownerNoUseProof")!=null||!TryReadPlan(root,out var retained)||
                retained.Job!=job||retained.Address!=address.ToString()||retained.Creation!=creation||!CurrentStationOwner(actor,retained,out var actual,out var lease)||
                !ReferenceEquals(actual,expected)||!HasOriginalStationCustody(actor,actual,lease,retained)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var before,out var saved)||saved.id!=actor.entityId||saved.deaths!=retained.Deaths||
                !retained.CopyBeforeActor().Matches(before)||!RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(job),out float phase,out bool present)||
                !present||phase!=10f||root.Copy().Element("ownerAction")!=null&&!MatchesNativePermission(root,retained))return false;
            var actualPaid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Copy().Element("removed").Value));
            var originalPaid=root.Copy().Element("sources").Elements("source").SelectMany(source=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid").Value))).ToArray();
            if(!RebirthCamperInventorySnapshot.Capture(actualPaid).SequenceEqual(RebirthCamperInventorySnapshot.Capture(originalPaid)))return false;
            plan=retained;return true;
        }
        catch{plan=null;return false;}
    }
    private static bool HasOriginalStationCustody(EntityPlayer actor,RebirthCamperStationState station,object lease,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        if(StationHolds.TryGetValue(station,out var hold)&&hold.Plan.Source==plan.Source&&hold.Plan.Binding==plan.Binding&&hold.Uninterrupted&&
            ReferenceEquals(hold.Actor,actor)&&ReferenceEquals(hold.Lease,lease)&&hold.Directory==GameIO.GetSaveGameDir())return true;
        return RebirthCamperStationNativeActorPayment.TryGetFetchedSavedBefore(actor,station,plan.Job,out var before,out var after)&&
            RebirthCamperStationAddress.TryParse(plan.Address,out var address)&&MatchesNativeFrames(actor,station,address,plan,before,after);
    }
    internal static bool CompleteNativeHandoff(RebirthCamperStationState station,Guid job,Guid source,string binding)
    {
        if(!ThreadManager.IsMainThread()||station==null||!StationHolds.TryGetValue(station,out var hold)||hold.Plan.Job!=job||hold.Plan.Source!=source||hold.Plan.Binding!=binding||
            !RebirthCamperStationNativeActorPayment.MatchesPendingOperation(station,job))return false;
        // Existing native Pending already owns the same station here. Never
        // remove this hold on a failed Begin or merely an Applied-looking row.
        StationHolds.Remove(station);return true;
    }
    private static bool ReleaseNoPaymentStationHold(RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(root==null||!RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var saved,out bool unknown)||unknown||saved?.Phase!=4||saved.Binding!=root.Binding)return false;
            var body=saved.Copy();if((int)body.Attribute("outcome")!=2||(string)body.Attribute("disposition")!="unused-no-payment"||body.Element("ownerAction")!=null||
                (string)body.Attribute("ownerProof")!=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+saved.Owner+"|"+saved.Binding)||
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("removed").Value)).Length!=0)return false;
            // Positive terminal was published only after the named native
            // no-effect/restoration verifier. This retires station blocking
            // custody, not any original owner receipt or inventory image.
            foreach(var pair in StationHolds.Where(pair=>pair.Value.Plan.Source==saved.Operation&&pair.Value.Plan.Binding==saved.Binding&&pair.Value.Plan.OwnerKey==saved.Owner&&
                pair.Value.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(pair.Value.World,GameManager.Instance?.World)).ToArray())StationHolds.Remove(pair.Key);
            return true;
        }
        catch{return false;}
    }
}
