using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class WorldApplication
    {
        internal EntityPlayer Actor;internal RebirthRemoteResourceDurableJournal.Record Root;
        internal RebirthRemoteResourcePlacedStationCustody.Claim Claim;
        internal RebirthRemoteResourcePlacedStationArtifactReader.Image Artifact;
        internal RebirthRemoteResourcePlacedStationReference.Image Reference;
        internal RebirthRemoteResourcePlacedStationAllocation.Admission Allocation;
        internal byte[] Command,After;internal bool Invoked,Completed;
        internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal string OwnerActor;internal byte[] OwnerBuffs;
    }
    private sealed class StationPermit
    {internal WorldApplication Original;internal string Method;internal object Value;internal bool Consumed;}
    [ThreadStatic]private static StationPermit stationPermit;
    private static readonly Dictionary<Guid,WorldApplication> worldApplications=new Dictionary<Guid,WorldApplication>();
    private static World applicationWorld;private static string applicationSave;
    private const int MaximumApplicationCommand=8192;
    private const int MaximumApplicationFrame=2*MaximumApplicationCommand+8192; // Existing rcj framing/headroom grammar.
    private enum SmallJournalDomain{WorldApplication,ClientUsed,Retirement,ClientRetired,ClientBeforeFlush,FinalAck}
    private static RebirthRemoteResourceClientOriginalJournal ApplicationJournal(Guid operation)=>SmallPlacedJournal(operation,SmallJournalDomain.WorldApplication);
    private static RebirthRemoteResourceClientOriginalJournal SmallPlacedJournal(Guid operation,SmallJournalDomain domain)
    {
        if(operation==Guid.Empty)throw new InvalidDataException();
        string name=domain==SmallJournalDomain.WorldApplication?"NativePlacedWorldApplications":domain==SmallJournalDomain.ClientUsed?"NativePlacedClientUsed":
            domain==SmallJournalDomain.Retirement?"NativePlacedRetirements":domain==SmallJournalDomain.ClientRetired?"NativePlacedClientRetired":domain==SmallJournalDomain.ClientBeforeFlush?"NativePlacedBeforeFlush":domain==SmallJournalDomain.FinalAck?"NativePlacedFinalAcks":throw new InvalidDataException();
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name),prefix=operation.ToString("N");
        CompletionAncestors(directory);int count=0;
        foreach(string folder in new[]{directory,Path.Combine(directory,"completed",prefix),Path.Combine(directory,"failedCandidates")})
        {
            if(!Directory.Exists(folder))continue;CompletionAncestors(folder);
            foreach(string path in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
                if(++count>32||Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||
                    !new[]{prefix+".0.rcj",prefix+".1.rcj",prefix+".2.rcj",prefix+".3.rcj"}.Contains(Path.GetFileName(path))||
                    new FileInfo(path).Length>MaximumApplicationFrame||folder==Path.Combine(directory,"failedCandidates"))throw new InvalidDataException();
        }
        return new RebirthRemoteResourceClientOriginalJournal(directory);
    }
    private static void BindApplicationWorld()
    {
        var world=GameManager.Instance?.World;string save=bindingSaveDirectory.Get();
        if(ReferenceEquals(world,applicationWorld)&&save==applicationSave)return;
        foreach(var original in worldApplications.Values)original.Watch?.Dispose();
        worldApplications.Clear();applicationWorld=world;applicationSave=save;
    }
    private static bool ApplicationScope(WorldApplication original)
        =>original!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,applicationWorld)&&
        applicationSave==GameIO.GetSaveGameDir()&&ReferenceEquals(original.Actor?.world,applicationWorld)&&
        ServerScope(original.Actor,original.Artifact.Original)&&original.Claim.TargetCurrent()&&
        RebirthRemoteResourcePlacedStationCustody.TryReadGuardClaim(original.Claim.Station,out var claim)&&ReferenceEquals(claim,original.Claim)&&
        RebirthRemoteResourceDurableJournal.TryRead(original.Root.Owner,original.Root.Operation,out var root,out bool unknown)&&!unknown&&root?.Phase==2&&
        root.Binding==original.Root.Binding&&(int)root.Copy().Attribute("outcome")==1&&(int?)root.Copy().Attribute("ownerRejecting")!=1&&
        root.Copy().Element("ownerRefund")==null&&root.Copy().Attribute("ownerNoUseProof")==null&&
        XNode.DeepEquals(root.Copy().Element("ownerAction"),OwnerPermission(root,original.Artifact,original.Reference));
    private static bool ApplicationAfter(WorldApplication original)
        =>ApplicationScope(original)&&original.Completed&&RebirthRemoteResourcePlacedStationCandidate.TrySerialize(original.Claim.Station,
            original.Claim.Station.Input,original.Claim.Station.Queue,original.Claim.Station.IsBurning,out var actual,out _)&&actual.SequenceEqual(original.After);
    // Only this exact original's direct native setter call can consume the
    // permit. A target hold is never itself mutation permission.
    internal static bool ConsumeStationSetter(TileEntityWorkstation station,MethodBase method,object[] arguments)
    {
        var once=stationPermit;
        if(once==null||once.Consumed||!once.Original.Invoked||!ReferenceEquals(once.Original.Claim.Station,station)||
            !ApplicationScope(once.Original)||method?.DeclaringType!=typeof(TileEntityWorkstation)||method.Name!=once.Method)return false;
        if(once.Method==nameof(TileEntityWorkstation.ResetTickTime))
        {if(arguments==null||arguments.Length!=0)return false;}
        else if(arguments==null||arguments.Length!=1||
            (once.Value is bool burning?arguments[0] is not bool actual||actual!=burning:!ReferenceEquals(arguments[0],once.Value)))return false;
        once.Consumed=true;return true;
    }
    private static bool NativeSet(WorldApplication original,string method,object value,Action invoke)
    {
        if(stationPermit!=null||!ApplicationScope(original))return false;
        var once=new StationPermit{Original=original,Method=method,Value=value};stationPermit=once;
        try{invoke();return once.Consumed&&ApplicationScope(original);}finally{stationPermit=null;}
    }
    private static byte[] ApplicationCommand(RebirthRemoteResourceDurableJournal.Record root,
        RebirthRemoteResourcePlacedStationArtifactReader.Image full,RebirthRemoteResourcePlacedStationReference.Image reference)
        =>Encoding.UTF8.GetBytes(new XElement("placedWorldOriginal",new XAttribute("version",1),new XAttribute("source",root.Operation.ToString("N")),
            new XAttribute("job",full.Original.Operation.ToString("N")),new XAttribute("binding",root.Binding),new XAttribute("original",full.Original.Digest),
            new XElement("reference",reference.Encode())).ToString(SaveOptions.DisableFormatting));
    private static bool TrySavedPaidOwner(EntityPlayer actor,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        out string actorImage,out byte[] buffs)
    {
        actorImage=null;buffs=null;
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
            !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=artifact.Original.Deaths||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(artifact.Original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
        string expected=artifact.Copy().Element("plan").Element("actorAfter").Value;
        if(!actual.Matches(RebirthRemoteResourceClientOriginal.ReadActor(expected))||saved.buffData==null||saved.buffData.Length>1024*1024)return false;
        actorImage=expected;buffs=saved.buffData.ToArray();return true;
    }
    private static bool TryBeginWorldApplication(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out WorldApplication original)
    {
        original=null;BindApplicationWorld();
        if(worldApplications.TryGetValue(root.Operation,out var existing))
        {if(!ReferenceEquals(existing.Actor,actor)||existing.Root.Binding!=root.Binding||!ApplicationScope(existing))return false;original=existing;return true;}
        if(worldApplications.Count>=32||!TryPaidContext(actor,root,out var intent,out var claim,out var full,out var reference)||
            !XNode.DeepEquals(root.Copy().Element("ownerAction"),OwnerPermission(root,full,reference))||
            !RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out _,out var actualClaim,out var allocation,out _)||!ReferenceEquals(actualClaim,claim)||
            !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.WorldApply,out var scratch))return false;
        using(scratch)
        {
        if(!TrySavedPaidOwner(actor,full,out string ownerActor,out var ownerBuffs))return false;
        byte[] command=ApplicationCommand(root,full,reference);if(command.Length>MaximumApplicationCommand)return false;
        var record=ApplicationJournal(intent.Operation).Prepare(intent.Operation,root.Operation,intent.Creation,0,command,command);
        // A cold Uncertain command cannot replay native setters merely because
        // a later station image coincides. Positive full saved evidence must
        // resolve that command; absent evidence stays retained unknown.
        if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||!record.Before.SequenceEqual(command)||!record.After.SequenceEqual(command))return false;
        var value=new WorldApplication{Actor=actor,Root=root,Claim=claim,Artifact=full,Reference=reference,Allocation=allocation,
            Command=command,After=Convert.FromBase64String(full.Copy().Element("plan").Element("afterTile").Value),OwnerActor=ownerActor,OwnerBuffs=ownerBuffs};
        worldApplications.Add(root.Operation,value);original=value;return true;
        }
    }
    private static bool ApplyWorldOriginal(WorldApplication original)
    {
        if(original?.Allocation==null||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Allocation,
            RebirthRemoteResourcePlacedStationAllocation.ReadPhase.WorldApply,out var scratch))return false;
        using(scratch)return ApplyWorldOriginalCore(original);
    }
    private static bool ApplyWorldOriginalCore(WorldApplication original)
    {
        if(!ApplicationScope(original)||original.Invoked||!original.Claim.ExactBefore()||
            !TrySavedPaidOwner(original.Actor,original.Artifact,out string actor,out var buffs)||actor!=original.OwnerActor)return false;
        var record=ApplicationJournal(original.Artifact.Original.Operation).Read(original.Artifact.Original.Operation);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Prepared||!record.Before.SequenceEqual(original.Command)||!record.After.SequenceEqual(original.Command)||
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(original.After,out var after)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(original.Artifact.Copy().Element("plan").Element("queue").Value,out var authored))return false;
        int slot=(int)original.Artifact.Copy().Element("plan").Attribute("slot");var station=original.Claim.Station;
        if(slot<0||slot>=station.Queue.Length||authored.Length!=station.Queue.Length)return false;
        // Preserve every unrelated native row object, including native/runtime
        // fields omitted by serialization. Only the original admitted row moves.
        var queue=(RecipeQueueItem[])station.Queue.Clone();queue[slot]=authored[slot];
        if(RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(after.Queue))return false;
        record=ApplicationJournal(original.Artifact.Original.Operation).Advance(original.Artifact.Original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain||!ApplicationScope(original)||!original.Claim.ExactBefore())return false;
        original.Invoked=true;original.OwnerBuffs=buffs;
        if(!RebirthCamperInventorySnapshot.Capture(station.Input).SequenceEqual(RebirthCamperInventorySnapshot.Capture(after.Input))&&
            !NativeSet(original,"set_Input",after.Input,()=>station.Input=after.Input))return false;
        if(!NativeSet(original,"set_Queue",queue,()=>station.Queue=queue))return false;
        if(station.IsBurning!=after.Burning&&!NativeSet(original,"set_IsBurning",after.Burning,()=>station.IsBurning=after.Burning))return false;
        if(!NativeSet(original,nameof(TileEntityWorkstation.ResetTickTime),null,station.ResetTickTime))return false;
        if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var observed,out _)||!observed.SequenceEqual(original.After))return false;
        original.Completed=true;return ApplicationAfter(original);
    }
    private static bool AdvanceWorldApplication(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(!TryBeginWorldApplication(actor,root,out var original))return false;
            if(TryReadCompletion(root,original.Artifact,original.Reference,original.Allocation,out string completedProof))
            {original.Watch?.Dispose();original.Watch=null;return SettleWorldApplication(original,root,completedProof);}
            if(!original.Invoked&&!ApplyWorldOriginal(original))return false;
            if(!ApplicationAfter(original))return false; // Partial/throw is retained, never replayed.
            if(original.Watch==null||!original.Watch.Current())
            {
                original.Watch?.Dispose();original.Watch=null;var position=original.Claim.Station.ToWorldPos();
                if(!RebirthRemoteResourceNativeBinding.TryResolve(actor.world,RemoteResourceIdentity.Workstation(position),RemoteResourceSourceKind.WorkstationOutput,
                    original.Claim.Birth,out var target))return false;
                original.Watch=RebirthRemoteResourceNativePublication.BeginPlacedPaidTarget(target,root,()=>ApplicationAfter(original));
                return false; // Native snapshot pressure/async completion is waiting, not refusal.
            }
            if(!PublishCompletion(original)||!TryReadCompletion(root,original.Artifact,original.Reference,original.Allocation,out string proof))return false;
            original.Watch.Dispose();original.Watch=null;
            return SettleWorldApplication(original,root,proof);
        }
        catch{return false;}
    }
    private static bool SettleWorldApplication(WorldApplication original,RebirthRemoteResourceDurableJournal.Record expected,string proof)
    {
        bool Current()
        {
            if(!ThreadManager.IsMainThread()||!ServerScope(original.Actor,original.Artifact.Original)||!original.Claim.TargetCurrent()||
                !RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var actual,out bool unknown)||unknown||actual==null||actual.Binding!=expected.Binding||
                !TryReadCompletion(actual,original.Artifact,original.Reference,original.Allocation,out string observed)||observed!=proof)return false;
            if(actual.Phase==2)return (int)actual.Copy().Attribute("outcome")==1&&(int?)actual.Copy().Attribute("ownerRejecting")!=1&&actual.Copy().Element("ownerRefund")==null;
            return actual.Phase==4&&(string)actual.Copy().Attribute("disposition")=="used"&&(string)actual.Copy().Attribute("ownerProof")==proof;
        }
        if(!Current()||!RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var current,out bool bad)||bad||current==null)return false;
        if(current.Phase==4)return Current();
        if(!PublishSavedEarnedOwner(original,current,proof))return false;
        return RebirthRemoteResourceDurableJournal.TryAdvance(current,image=>{image.SetAttributeValue("phase",4);image.SetAttributeValue("disposition","used");image.SetAttributeValue("ownerProof",proof);},Current,out _);
    }
}
