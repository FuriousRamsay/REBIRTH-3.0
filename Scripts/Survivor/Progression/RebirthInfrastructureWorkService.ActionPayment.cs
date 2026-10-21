using System;
using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthInfrastructureWorkService
{
    private sealed class ActionPreparation
    {
        internal readonly EntityPlayer Actor;internal readonly World World;internal readonly Vector3i Position;
        internal TileEntityPowered Target;internal RebirthElectricalServiceRecord Candidate;internal List<ItemStack> Requirements;
        internal ActionPreparation(EntityPlayer actor,Vector3i position){Actor=actor;World=actor.world;Position=position;}
    }
    [ThreadStatic]private static ActionPreparation preparingAction;
    // Price description only. The host still performs all existing knowledge,
    // ownership, meaningful-change and canonical admission checks. A remote
    // grant may never ask the clicking client to pay a different native price.
    internal static bool TryDescribeActionPrice(World world,TileEntityPowered target,Vector3i position,out ItemStack[] requirements)
    {
        requirements=null;
        if(!ThreadManager.IsMainThread()||world==null||target==null||!ReferenceEquals(world.GetTileEntity(position),target)||!target.IsPlayerPlaced)return false;
        var block=world.GetBlock(position);if(block.ischild||block.Block==null||!IsElectricalBlock(block.Block))return false;
        requirements=ItemStack.Clone(BuildRequirements(Classify(block.Block.GetBlockName(),target))).ToArray();return true;
    }
    private static bool CapturePreparedAction(EntityPlayer actor,TileEntityPowered target,RebirthElectricalServiceRecord candidate,List<ItemStack> requirements)
    {
        var scope=preparingAction;
        if(scope==null||!ReferenceEquals(scope.Actor,actor)||!ReferenceEquals(scope.World,actor.world)||scope.Position!=candidate.Position)return false;
        if(scope.Candidate!=null)throw new InvalidOperationException("Repeated electrical preparation boundary");
        scope.Target=target;scope.Candidate=Clone(candidate);scope.Requirements=new List<ItemStack>(ItemStack.Clone(requirements));return true;
    }
    internal static bool BeginOwnedService(EntityPlayer actor,int clr,Vector3i position,out RemoteResourceTransactions.ActionPayment original,out string message)
    {
        original=null;message="Electrical action original is unavailable.";
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||preparingAction!=null)return false;
        if(RemoteResourceTransactions.FindElectricalOriginal(actor,position,out original))
        {message="The same original electrical service remains in settlement.";return true;}
        var scope=new ActionPreparation(actor,position);preparingAction=scope;
        try{PrepareOwnedService(actor,clr,position,out message,out _);}
        finally{preparingAction=null;}
        if(scope.Candidate==null||scope.Target==null||scope.Requirements==null)return false;
        return RemoteResourceTransactions.TryBeginElectricalPayment(actor,scope.Target,scope.Candidate,scope.Requirements,out original,out message);
    }
    internal static bool ReservePaidAction(RemoteResourceTransactions.ActionPayment original)
    {
        if(original==null||!Ready||!ReferenceEquals(boundWorld,original.World)||!original.TargetCurrent()||original.AdmissionReserved)return false;
        if(!TryReserveAdmission(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position)))return false;
        original.AdmissionReserved=true;return true;
    }
    internal static XElement CapturePaidActionBefore(RemoteResourceTransactions.ActionPayment original)
    {
        if(original==null||!ThreadManager.IsMainThread()||!Ready||!ReferenceEquals(boundWorld,original.World)||!original.TargetCurrent()||
            original.State!=RemoteResourceTransactions.ActionPaymentState.Prepared||original.PublicationEntered)return null;
        bool present=records.TryGetValue(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position),out var current);
        return new XElement("serviceBefore",new XAttribute("present",present),present?ActionCandidateImage(current):null);
    }
    internal static bool RestoreReturnedPaidAction(RemoteResourceTransactions.ActionPayment original,XElement before)
    {
        if(original==null||before?.Name!="serviceBefore"||!ThreadManager.IsMainThread()||!Ready||!ReferenceEquals(boundWorld,original.World)||!original.TargetCurrent()||
            original.State!=RemoteResourceTransactions.ActionPaymentState.Paid||!original.AdmissionReserved||!original.PublicationEntered||!original.PublicationReturned||
            !original.Events.ContainsKey("publication-returned")||!original.OwnerAfterSaved||original.Legs.Any(leg=>!leg.AfterSaved))return false;
        string key=Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position);bool present=records.TryGetValue(key,out var current);
        bool already=present&&XNode.DeepEquals(ActionCandidateImage(current),ActionCandidateImage(original.ServiceCandidate));
        if(!already&&(present!=(bool?)before.Attribute("present")||present&&!XNode.DeepEquals(ActionCandidateImage(current),before.Element("configuration"))))return false;
        // Restore only the exact positively returned original result. No price,
        // service count, skill or source delta is recalculated or invoked.
        var deferred=RebirthElectricalSourceIdentity.ReadDeferred(original.World,original.ServiceCandidate.Position);
        bool exactDeferred=deferred!=null&&XNode.DeepEquals(ActionCandidateImage(deferred),ActionCandidateImage(original.ServiceCandidate));
        if(original.RestartTargetSource?.LoadedAfterPublication!=true&&!exactDeferred)
        {
            if(!RebirthElectricalSourceIdentity.Defer(original.World,original.ServiceCandidate))return false;
            if(!RemoteResourceTransactions.ObserveRestoredReturnedActionTargetMarker(original))return false;
        }
        if(!already){records[key]=Clone(original.ServiceCandidate);dirty=true;}
        original.ServiceTarget.SetModified();return SavePaidAction(original)!=null;
    }
    internal static void ReleasePaidAction(RemoteResourceTransactions.ActionPayment original)
    {
        if(original?.AdmissionReserved!=true)return;
        if(ReferenceEquals(boundWorld,original.World))admissions.Remove(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position));
        original.AdmissionReserved=false;
    }
    // This is the actual owning publication, separate from fetching or native
    // recipe refunds. Admission and every debit were sealed by this original.
    internal static bool PublishPaidAction(RemoteResourceTransactions.ActionPayment original)
    {
        if(original==null||!Ready||!ReferenceEquals(boundWorld,original.World)||!original.TargetCurrent()||
            original.State!=RemoteResourceTransactions.ActionPaymentState.Paid||!original.PublicationEntered||!original.AdmissionReserved)return false;
        string key=Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position);
        if(original.PublicationReturned)return true;
        // One invocation only: the caller writes its durable publication intent
        // before entering here. A throw after Defer leaves the original held.
        if(!RebirthElectricalSourceIdentity.Defer(original.World,original.ServiceCandidate))return false;
        if(!RemoteResourceTransactions.ObservePaidActionTargetMarker(original))return false;
        records[key]=Clone(original.ServiceCandidate);dirty=true;
        original.PublicationReturned=true;
        original.ServiceTarget.SetModified();return true;
    }
    internal static string SavePaidAction(RemoteResourceTransactions.ActionPayment original)
    {
        if(original?.PublicationReturned!=true||!original.TargetCurrent()||!ReferenceEquals(boundWorld,original.World)||!Ready||
            !records.TryGetValue(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position),out var current)||
            !XNode.DeepEquals(ActionCandidateImage(current),ActionCandidateImage(original.ServiceCandidate)))return null;
        Save();
        // Save catches its own native/disk errors. Actual full readback, rather
        // than its return, decides whether the owning outcome is durable.
        if(dirty||boundPath==null||!TryLoadFile(boundPath,out var saved,out _)||
            !saved.TryGetValue(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position),out var row)||
            !XNode.DeepEquals(ActionCandidateImage(row),ActionCandidateImage(original.ServiceCandidate)))return null;
        return boundPath;
    }
    internal static void NotifyPaidActionOutcome(RemoteResourceTransactions.ActionPayment original)
    {
        if(!ThreadManager.IsMainThread()||original==null||!original.Terminal||original.Retired||original.Actor==null||
            !ReferenceEquals(GameManager.Instance?.World,original.World)||!ReferenceEquals(original.World.GetEntity(original.Actor.entityId),original.Actor)||
            original.Actor.IsDead()||RebirthCamperCharacterAccess.CurrentCreation(original.Actor)!=original.Creation)return;
        bool used=original.State==RemoteResourceTransactions.ActionPaymentState.Used;
        if(!used&&original.State!=RemoteResourceTransactions.ActionPaymentState.Returned)return;
        string message="Electrical service payment returned; no service was applied.";
        if(used)
        {
            int nativePower=ReferenceEquals(original.World.GetTileEntity(original.ServiceCandidate.Position),original.ServiceTarget)?original.ServiceTarget.PowerUsed:0;
            int adjusted=nativePower>0?AdjustPowerUsed(original.ServiceTarget,nativePower):0;
            float efficiency=nativePower>0?(1f-(float)adjusted/nativePower)*100f:0f;
            message=FriendlyProcedure(original.ServiceCandidate.Kind)+
                " completed. Condition 100%, workmanship "+UnityEngine.Mathf.RoundToInt(original.ServiceCandidate.Workmanship*100f)+"%"+
                (nativePower>0?", current power efficiency "+efficiency.ToString("0.#")+"%.":".");
        }
        if(original.Actor is EntityPlayerLocal actor)
        {
            if(!ReferenceEquals(original.World.GetPrimaryPlayer(),actor))return;
            if(used)ReceiveClientSnapshot(original.ServiceCandidate);
            RebirthElectricalServiceUiFeedback.Receive(used,message);
        }
        else if(original.RemoteOwner==null)
        {
            // Legacy authenticated requests can own server-only source legs.
            // Their final result carries a description, never player inventory.
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(original.Actor.entityId);
            if(peer!=null&&RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)&&owner.CanonicalId==original.Account)
                peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthElectricalServiceResult>().Setup(used,message,used?original.ServiceCandidate:null));
        }
    }
    private static XElement ActionCandidateImage(RebirthElectricalServiceRecord r)=>new XElement("configuration",
        new XAttribute("clr",r.ClrIdx),new XAttribute("x",r.Position.x),new XAttribute("y",r.Position.y),new XAttribute("z",r.Position.z),
        new XAttribute("kind",(byte)r.Kind),new XAttribute("condition",r.Condition),new XAttribute("workmanship",r.Workmanship),
        new XAttribute("time",r.LastServiceWorldTime),new XAttribute("count",r.ServiceCount),new XAttribute("author",r.ConfiguredByStableId??""),
        new XAttribute("background",r.ConfiguredByBackgroundId??""),new XAttribute("bonus",r.ConfiguredByBonusId??""),new XAttribute("skill",r.ConfiguredSkillValue),
        new XAttribute("block",r.SourceBlock),new XAttribute("incarnation",r.SourceIncarnation));
    internal static bool ValidatePaidActionArtifact(RemoteResourceTransactions.ActionPayment original,string path)
    {
        return original?.PublicationReturned==true&&TryLoadFile(path,out var rows,out _)&&
            rows.TryGetValue(Key(original.ServiceCandidate.ClrIdx,original.ServiceCandidate.Position),out var row)&&
            XNode.DeepEquals(ActionCandidateImage(row),ActionCandidateImage(original.ServiceCandidate));
    }
}
