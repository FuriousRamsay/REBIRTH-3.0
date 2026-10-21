using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

// Typed original recovery transaction. File publication is not success: complete
// local installation and current eligible recipient native readbacks precede a
// joint durable closing proof. Installed same-generation actions are never reset.
internal sealed partial class RebirthNpcLiveRecoverySession : IDisposable
{
    private static readonly string[] Stores={"RebirthNpcStableIdentities.xml","rebirth_npc_inventory.xml","rebirth_npc_equipment.xml","RebirthNpcExecutionState.xml","rebirth_npc_settlement.xml","RebirthNpcWorkCheckpoints.xml","RebirthNpcWorkOutcomes.xml","RebirthNpcSocialState.xml","RebirthNpcCombatState.xml","RebirthNpcPersistentRecords.xml","RebirthNpcWorldIntegration.xml"};
    private readonly List<IDisposable> fences=new List<IDisposable>();
    internal static bool OwnsAllWriterFences(string directory,Guid operation)
    {foreach(string file in Stores)if(!RebirthNpcPersistenceFile.OwnsRecoveryWriterFence(Path.Combine(directory,file),operation))return false;return true;}
    private RebirthNpcPersistenceCoordinator.RecoveryLease coordinator;
    private RebirthNpcRecoveryCandidates.Candidate candidate;
    private readonly List<RebirthNpcLiveRecoveryIntent> intents=new List<RebirthNpcLiveRecoveryIntent>();
    private RebirthNpcLiveRecoveryGroup group;
    private RebirthNpcRecoveryPrimitiveCaches primitiveCaches;
    private World world;
    private void SetUnresolved(bool value)=>RebirthNpcPersistenceCoordinator.SetRecoveryUnresolved(value,world,group?.Directory);
    private void SetProjectionPending(bool value)=>RebirthNpcPersistenceCoordinator.SetRecoveryProjectionPending(value,world,group?.Directory);
    private bool mainCommitted,executionCommitted,runtimeCommitted,installed,disposed,coldInstalled,closing,completed;
    private readonly HashSet<string> relatedCommitted=new HashSet<string>(StringComparer.Ordinal);
    internal Guid Operation {get;private set;}
    internal ulong Generation=>group?.Generation??0;
    internal string Target=>group?.Target;
    internal bool Installed=>installed;
    internal object PrimitiveCacheStatus=>primitiveCaches?.CaptureStatus();
    internal bool Completed=>completed;
    internal bool IsCurrent{get{string directory=GameIO.GetSaveGameDir();return !disposed&&ReferenceEquals(world,GameManager.Instance?.World)&&group!=null&&!string.IsNullOrEmpty(directory)&&string.Equals(group.Directory,Path.GetFullPath(directory),StringComparison.OrdinalIgnoreCase);}}
    internal RebirthNpcStableId[] Affected=>candidate==null?new RebirthNpcStableId[0]:(RebirthNpcStableId[])candidate.Affected.Clone();
    private void AcquireLeases()
    {
        if(coordinator!=null){if(!coordinator.IsCurrent)throw new InvalidOperationException("Original recovery lease changed world/save");return;}
        if(!RebirthNpcPersistenceCoordinator.TryAcquireRecovery(out coordinator,out var error))throw new InvalidOperationException(error);
        try
        {
            string directory=GameIO.GetSaveGameDir();
            foreach(string file in Stores)
            {if(!RebirthNpcPersistenceFile.TryFenceRecovery(Path.Combine(directory,file),Operation,out var lease,out error))throw new InvalidOperationException(error);fences.Add(lease);}
        }
        catch{ReleaseLeases();throw;}
    }
    internal static bool TryStage(string file,out RebirthNpcLiveRecoverySession session,out string error)
    {
        session=null;error=string.Empty;var next=new RebirthNpcLiveRecoverySession{Operation=Guid.NewGuid(),world=GameManager.Instance?.World};
        try
        {
            string canonical=null;foreach(string allowed in Stores)if(string.Equals(allowed,file,StringComparison.OrdinalIgnoreCase)&&allowed!="RebirthNpcWorldIntegration.xml")canonical=allowed;
            if(canonical==null)throw new InvalidDataException("File has no standalone typed recovery adapter");
            if(!RebirthNpcFreshRecoveryCustody.Check(next.world,out error))throw new InvalidOperationException(error);
            next.AcquireLeases();string directory=GameIO.GetSaveGameDir();
            if(RebirthNpcLiveRecoveryGroup.Read(directory)!=null)throw new InvalidOperationException("An existing recovery must converge first");
            if(!RebirthNpcPersistenceTimelineService.CanAdoptRecovery(out error))throw new InvalidOperationException(error);
            ulong previous=RebirthNpcRecoveryGeneration.RequireCurrent();
            if(previous==ulong.MaxValue)throw new InvalidOperationException("Recovery generation exhausted");
            next.primitiveCaches=RebirthNpcRecoveryPrimitiveCaches.Capture(next.coordinator,next.Operation,previous,previous+1,false);
            RebirthNpcLiveRecoveryRuntime.PrimeDeferredIdentities();
            RebirthNpcRecoveryCurrentBacking.Prime(directory,next.Operation,canonical);
            RebirthNpcLiveRecoveryRuntime.PrimeDeferredSemantics(canonical);
            string frozen=Path.Combine(directory,canonical)+".recovery."+next.Operation.ToString("N")+".source-backup.xml";
            FreezeBackup(Path.Combine(directory,canonical)+".bak",frozen);
            if(!RebirthNpcRecoveryCandidates.TryPrepare(canonical,RebirthNpcRecoveryCurrentBacking.Read(frozen),out next.candidate,out error)||
                !RebirthNpcRecoveryPreflight.Check(next.world,next.candidate,out error,true,next.primitiveCaches))throw new InvalidOperationException(error);
            next.intents.Add(RebirthNpcLiveRecoveryIntent.Stage(directory,canonical,next.Operation,previous,next.candidate.Publication));
            foreach(var related in next.candidate.Related)
            {if(related.PublicationDocument==null)throw new InvalidDataException("Related adapter lacks a publication image");next.intents.Add(RebirthNpcLiveRecoveryIntent.Stage(directory,related.FileName,next.Operation,previous,related.PublicationDocument));}
            var members=new List<RebirthNpcLiveRecoveryGroup.Member>();foreach(var intent in next.intents)members.Add(new RebirthNpcLiveRecoveryGroup.Member{File=intent.File,Hash=intent.CandidateHash,BeforeHash=intent.BeforeHash});
            next.group=RebirthNpcLiveRecoveryGroup.Create(directory,canonical,next.Operation,previous,members);
            next.SetUnresolved(true);
            RebirthNpcRecoveryFaults.Checkpoint(next.Operation,canonical,"group-prepared");
            next.intents.Clear();foreach(var member in next.group.Members)
            {next.intents.Add(RebirthNpcLiveRecoveryIntent.PublishPreparedMember(next.group,member));RebirthNpcRecoveryFaults.Checkpoint(next.Operation,canonical,"member-prepared");}
            session=next;return true;
        }
        catch(Exception ex)
        {
            error=ex.GetType().Name+": "+ex.Message;
            if(next.group!=null){session=next;next.SetUnresolved(true);}else next.Dispose();
            return false;
        }
    }
    internal static bool TryOpenPending(out RebirthNpcLiveRecoverySession session,out string error)
    {
        session=null;error=string.Empty;var next=new RebirthNpcLiveRecoverySession{world=GameManager.Instance?.World};
        try
        {
            string directory=GameIO.GetSaveGameDir();next.group=RebirthNpcLiveRecoveryGroup.Read(directory);
            if(next.group==null)return false;
            next.Operation=next.group.Operation;next.AcquireLeases();
            var scope=RebirthNpcSaveScope.ObserveCurrent();
            if(scope==null||scope.Fingerprint!=next.group.Scope||!string.Equals(Path.GetFullPath(directory),next.group.Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Pending recovery original save scope differs");
            if(!RebirthNpcPersistenceTimelineService.CanAdoptRecovery(out error))throw new InvalidOperationException(error);
            next.coldInstalled=next.group.Phase==2;next.closing=next.group.Phase==3;
            next.SetUnresolved(true);
            // Preparation is retried on the retained instance, not discarded
            // after one adapter has already committed or an IO read has failed.
            session=next;return true;
        }
        catch(Exception ex)
        {error=ex.GetType().Name+": "+ex.Message;next.Dispose();return false;}
    }
    internal static bool TryAdoptInstalled(out RebirthNpcLiveRecoverySession session,out string error)
    {if(!TryOpenPending(out session,out error))return false;if(session.group.Phase!=2){session.Dispose();session=null;error="Installed recovery group required";return false;}return session.TryInstall(out error);}
    private void PreparePending()
    {
        if(candidate!=null)
        {
            // A retained detached candidate does not prove all member intents
            // finished publishing. Rebuild the COMPLETE durable group0 extent
            // after a real partial IO failure before advancing any primary.
            if(group.Phase==0)
            {
                AcquireLeases();intents.Clear();foreach(var member in group.Members)
                {intents.Add(RebirthNpcLiveRecoveryIntent.PublishPreparedMember(group,member));RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"member-prepared");}
            }
            return;
        }
        AcquireLeases();intents.Clear();
        if(group.Phase==0)foreach(var member in group.Members)
        {intents.Add(RebirthNpcLiveRecoveryIntent.PublishPreparedMember(group,member));RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"member-prepared");}
        else foreach(var member in group.Members)
        {
            var intent=RebirthNpcLiveRecoveryIntent.Read(group.Directory,member.File);
            if(intent.Operation!=Operation||intent.Generation!=Generation||intent.CandidateHash!=member.Hash||intent.BeforeHash!=member.BeforeHash)throw new InvalidDataException("Pending member differs from complete group");
            if(coldInstalled&&intent.Phase!=2)throw new InvalidDataException("Cold installed member lacks its exact install receipt");
            intents.Add(intent);
        }
        RebirthNpcLiveRecoveryRuntime.PrimeDeferredIdentities();
        RebirthNpcRecoveryCurrentBacking.Prime(group.Directory,Operation,group.Target,group);
        RebirthNpcLiveRecoveryRuntime.PrimeDeferredSemantics(group.Target);
        var documents=new Dictionary<string,XmlDocument>(StringComparer.Ordinal);
        foreach(var intent in intents)
        {
            if(coldInstalled)
            {
                if(RebirthNpcRecoveryPublications.IsInstalled(intent.Primary))throw new InvalidOperationException("Use retained live recovery; installed runtime must not be reinstalled");
                if(!RebirthNpcRecoveryPublications.TryResolveLatest(intent.Primary,Operation,Generation,out var latest,out _,out _,out var error))throw new InvalidDataException(error);
                documents.Add(intent.File,latest);
            }
            else documents.Add(intent.File,intent.ReadCandidate());
        }
        documents.TryGetValue("RebirthNpcWorldIntegration.xml",out var latestWorld);
        string prepareError;bool prepared=coldInstalled?RebirthNpcRecoveryCandidates.TryPrepareInstalled(group.Target,documents[group.Target],latestWorld,out candidate,out prepareError):
            RebirthNpcRecoveryCandidates.TryPrepare(group.Target,documents[group.Target],out candidate,out prepareError);
        if(!prepared)throw new InvalidDataException(prepareError);
        if(!coldInstalled)
        {
            foreach(var related in candidate.Related)
                if(!documents.TryGetValue(related.FileName,out var recorded)||related.PublicationDocument?.OuterXml!=recorded.OuterXml)
                    throw new InvalidDataException("Recaptured related adapter differs from the frozen original image");
            bool inventory=group.Target=="rebirth_npc_inventory.xml";
            if(inventory)
            {
                var intent=intents.Find(value=>value.File==group.Target);
                string custodyError=string.Empty;if(intent.BeforeHash=="absent"||RebirthNpcLiveRecoveryIntent.Hash(intent.Prior)!=intent.BeforeHash||
                    !RebirthNpcInventoryPersistenceStore.ProvesStagedRecoveryCustody(RebirthNpcRecoveryCurrentBacking.Read(intent.Prior),documents[group.Target],out custodyError))
                    throw new InvalidDataException("Interrupted initial physical custody is unproved: "+custodyError);
            }
            if(!RebirthNpcRecoveryPreflight.Check(world,candidate,out var error,!inventory,primitiveCaches))throw new InvalidDataException(error);
        }
    }
    private void CommitTyped()
    {
        if(!mainCommitted)
        {if(!candidate.Prepared.IsCurrent||!candidate.Prepared.Commit())throw new InvalidOperationException("Main recovery generation changed before install");mainCommitted=true;RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"main-installed");}
        foreach(var related in candidate.Related)if(!relatedCommitted.Contains(related.FileName))
        {if(!related.IsCurrent||!related.Commit())throw new InvalidOperationException("Related recovery generation changed before install");relatedCommitted.Add(related.FileName);RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"related-installed");}
        if(!executionCommitted&&candidate.ExecutionProjection!=null)
        {if(!candidate.ExecutionProjection.IsCurrent||!candidate.ExecutionProjection.Commit())throw new InvalidOperationException("Execution recovery native generation changed");executionCommitted=true;RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"execution-installed");}
        if(!runtimeCommitted&&candidate.Runtime!=null)
        {if(!candidate.Runtime.IsCurrent||!candidate.Runtime.Install())throw new InvalidOperationException("Recovery native identity/runtime generation changed");runtimeCommitted=true;RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"runtime-installed");}
    }
    internal bool TryInstall(out string error)
    {
        error=string.Empty;
        try
        {
            if(!IsCurrent)throw new InvalidOperationException("Original recovery world/save changed");
            if(installed)return RebirthNpcRecoveryGeneration.RequireCurrent()==Generation;
            RebirthNpcRecoveryGeneration.InvalidateObservedGeneration();
            if(group.Phase==3)return FinishClosing(out error);
            if(primitiveCaches==null){AcquireLeases();primitiveCaches=RebirthNpcRecoveryPrimitiveCaches.Capture(coordinator,Operation,group.PreviousGeneration,Generation,coldInstalled);}
            if(!primitiveCaches.Frozen)throw new InvalidOperationException("Frozen original primitive caches changed");
            PreparePending();
            bool publishOriginal=!coldInstalled&&group.Phase<2;
            if(publishOriginal)
            {
                // Validate all unconsumed candidates BEFORE any durable publication.
                if(!mainCommitted&&!candidate.Prepared.IsCurrent)throw new InvalidOperationException("Recovery candidate epoch changed");
                foreach(var related in candidate.Related)if(!relatedCommitted.Contains(related.FileName)&&!related.IsCurrent)throw new InvalidOperationException("Related recovery candidate epoch changed");
                if(!runtimeCommitted&&candidate.Runtime!=null&&!candidate.Runtime.IsCurrent)throw new InvalidOperationException("Native recovery projection changed");
                if(!executionCommitted&&candidate.ExecutionProjection!=null&&!candidate.ExecutionProjection.IsCurrent)throw new InvalidOperationException("Native execution projection changed");
                group.Advance(0,1);
                foreach(var intent in intents)
                {var saved=RebirthNpcLiveRecoveryIntent.Read(group.Directory,intent.File);if(saved.Phase<=1)intent.Publish();else if(saved.Phase!=2||RebirthNpcLiveRecoveryIntent.Hash(intent.Primary)!=intent.CandidateHash)throw new InvalidDataException("Partially installed primary differs");RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"primary-published");}
                if(!RebirthNpcRecoveryGeneration.TryAdvance(group.PreviousGeneration,Generation,Operation,out error))throw new InvalidOperationException(error);
                RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"generation-published");
            }
            else if(RebirthNpcRecoveryGeneration.RequireCurrent()!=Generation)throw new InvalidDataException("Cold installed generation differs");
            CommitTyped();
            foreach(var intent in intents)
            {
                if(publishOriginal)
                {intent.MarkInstalled();if(!RebirthNpcRecoveryPublications.TryInitialize(intent.Primary,Operation,Generation,intent.CandidateHash,out error))throw new InvalidDataException(error);}
                if(!RebirthNpcRecoveryPublications.IsInstalled(intent.Primary)&&!RebirthNpcRecoveryPublications.TryRegisterInstalled(intent.Primary,Operation,Generation,out error))throw new InvalidDataException(error);
                RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"publication-initialized");
            }
            if(publishOriginal)group.Advance(1,2);
            primitiveCaches.RetireAfterInstalled(group);
            foreach(var intent in intents)RebirthNpcPersistenceFile.VerifiedRead(intent.Primary);
            if(RebirthNpcRecoveryGeneration.RequireCurrent()!=Generation)throw new InvalidDataException("Installed recovery generation differs after runtime publication");
            installed=true;SetProjectionPending(true);SetUnresolved(false);ReleaseLeases();return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;SetUnresolved(true);return false;}
    }
    internal bool TryProgress(out string error)
    {
        error=string.Empty;if(completed)return true;
        try
        {
            if(!IsCurrent)throw new InvalidOperationException("Original recovery scope unavailable");
            group=RebirthNpcLiveRecoveryGroup.Read(group.Directory)??throw new InvalidDataException("Pending group vanished without terminal retirement");
            if(group.Phase==3){closing=true;return FinishClosing(out error);}
            if(closing&&group.Phase==2&&installed)
            {closing=false;SetUnresolved(false);ReleaseLeases();}
            if(!installed&&!TryInstall(out error))return false;
            RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"installed");
            if(completed)return true;
            if(!ConvergeNative(out error)||!RebirthNpcRecoveryProjectionNetwork.Poll(Operation,Generation,Affected,out error))return false;
            // Finish any exact previously recorded ordinary save before asking
            // the serializer for the latest legitimate installed runtime state.
            AcquireLeases();foreach(var intent in intents)
                if(!RebirthNpcRecoveryPublications.TryResolveLatest(intent.Primary,Operation,Generation,out _,out _,out _,out error))throw new InvalidDataException(error);
            ReleaseLeases();
            if(!RebirthNpcPersistenceCoordinator.TryFlushRecoveryMembers(Operation,out error))return false;
            AcquireLeases();
            if(!ConvergeNative(out error)||!RebirthNpcRecoveryProjectionNetwork.TryCaptureCompletion(Operation,Generation,Affected,out string proof,out error))
            {ReleaseLeases();return false;}
            group.BeginClosing(proof);closing=true;SetUnresolved(true);
            RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"closing-intent");
            return FinishClosing(out error);
        }
        catch(Exception ex)
        {error=ex.GetType().Name+": "+ex.Message;if(closing)SetUnresolved(true);else ReleaseLeases();return false;}
    }
    private bool FinishClosing(out string error)
    {
        error=string.Empty;
        try
        {
            AcquireLeases();group=RebirthNpcLiveRecoveryGroup.Read(group.Directory)??throw new InvalidDataException("Closing group missing");
            if(group.Phase!=3)throw new InvalidDataException("Complete joint closing proof unavailable");
            closing=true;SetUnresolved(true);
            foreach(var member in group.Members)
            {
                string primary=Path.Combine(group.Directory,member.File);
                if(File.Exists(RebirthNpcPersistenceFile.RecoveryIntentPath(primary)))
                {
                    var intent=RebirthNpcLiveRecoveryIntent.Read(group.Directory,member.File);intent.MarkConverged(member.TerminalSequence,member.TerminalHash);
                    if(!RebirthNpcRecoveryPublications.TryRetireConverged(primary,Operation,Generation,out error))throw new InvalidDataException(error);
                    RebirthNpcRecoveryFaults.Checkpoint(Operation,group.Target,"member-retired");
                }
                // Already retired members are revalidated by RetireAfterMembers.
            }
            group.RetireAfterMembers();completed=true;SetProjectionPending(false);SetUnresolved(false);ReleaseLeases();return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }
    internal bool ConvergeNative(out string error)
    {
        error=string.Empty;if(!installed||!IsCurrent||RebirthNpcRecoveryGeneration.RequireCurrent()!=Generation){error="Installed recovery native scope differs";return false;}
        if(candidate.Runtime!=null&&!candidate.Runtime.ConvergeNative(Generation,out error))return false;
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();if(!RebirthNpcEquipmentPersistenceStore.IsRecoveryProjectionLoaded){error="Current native equipment backing unavailable";return false;}
        var equipment=new Dictionary<RebirthNpcStableId,RebirthNpcEquipmentPersistentRecord>();foreach(var row in RebirthNpcEquipmentService.CapturePersistentRecords())equipment.Add(row.NpcId,row);
        if(!RebirthNpcNativeEquipmentBridge.ObserveRecoveryGeneration(Generation)){error="Native equipment generation floor differs";return false;}
        var ids=new HashSet<RebirthNpcStableId>(Affected);foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())ids.Add(state.StableId);
        foreach(var id in ids)
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity))continue;
            if(world.GetEntity(entity) is not EntityRebirthNPC actor||actor.RebirthRuntimeState?.StableId!=id){error="Native projection identity differs";return false;}
            if(actor is not EntityRebirthHumanoidNPC humanoid)continue;equipment.TryGetValue(id,out var current);
            if(RebirthNpcNativeEquipmentBridge.ApplyRecoverySnapshot(humanoid,id,Generation,current?.Revision??0,current?.Slots??new Dictionary<RebirthNpcEquipmentSlot,string>())!=RebirthNpcNativeEquipmentApplyResult.Applied)
            {error="Native equipment readback pending";return false;}
        }
        return IsCurrent&&RebirthNpcRecoveryGeneration.RequireCurrent()==Generation;
    }
    private static void FreezeBackup(string source,string target)
    {
        using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long remaining=input.Length;if(remaining<1||remaining>67108864)throw new InvalidDataException("Backup size unsupported");
            using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {var buffer=new byte[65536];while(remaining>0){int count=input.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));if(count==0)throw new EndOfStreamException();output.Write(buffer,0,count);remaining-=count;}if(input.ReadByte()!=-1)throw new InvalidDataException("Backup changed length");output.Flush(true);}
        }
        if(RebirthNpcLiveRecoveryIntent.Hash(source)!=RebirthNpcLiveRecoveryIntent.Hash(target))throw new InvalidDataException("Backup changed during frozen capture");
    }
    private void ReleaseLeases(){for(int i=fences.Count-1;i>=0;i--)fences[i].Dispose();fences.Clear();coordinator?.Dispose();coordinator=null;}
    public void Dispose(){if(disposed)return;disposed=true;ReleaseLeases();}
}
