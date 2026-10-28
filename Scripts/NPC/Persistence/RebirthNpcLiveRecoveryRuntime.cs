using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

// Main-thread transaction driver. Retry metadata/typed continuation only; never
// invoke a source action, Reset(false), backup replay after install, or native
// reconstruction. A server-local success requires complete projection receipts.
internal static partial class RebirthNpcLiveRecoveryRuntime
{
    private static RebirthNpcLiveRecoverySession current;
    private static World readyWorld,context;
    private static string directory,lastDetail="not checked";
    private static double retry;
    private static Guid lastCompleted;private static string lastCompletedTarget;private static ulong lastCompletedGeneration;
    private static bool installed,pumping,loadedAfterRecovery;
    internal static bool HasUnretiredForeignScope
    {
        get
        {
            if(!ThreadManager.IsMainThread())return true;
            var world=GameManager.Instance?.World;
            if((context!=null||current!=null)&&(!ReferenceEquals(context,world)||directory!=RebirthNpcSaveScope.CurrentDirectory()))return true;
            foreach(var pair in deferred)if(!ReferenceEquals(pair.Value,world))return true;
            return false;
        }
    }
    private static Dictionary<EntityRebirthNPC,World> deferred=new Dictionary<EntityRebirthNPC,World>();
    private static HashSet<EntityRebirthNPC> deferredProjected=new HashSet<EntityRebirthNPC>();
    internal static void DeferNativeActivation(EntityRebirthNPC actor)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||actor==null||!ReferenceEquals(world.GetEntity(actor.entityId),actor))throw new InvalidOperationException("Deferred activation requires exact current native body");
        if(!deferred.ContainsKey(actor)&&deferred.Count>=16384)throw new InvalidOperationException("Deferred activation capacity retained");
        deferred[actor]=world;
    }
    internal static bool IsNativeActivationDeferred(EntityRebirthNPC actor)=>actor!=null&&deferred.TryGetValue(actor,out var world)&&ReferenceEquals(world,GameManager.Instance?.World);
    internal static void PrimeDeferredIdentities()
    {
        var world=GameManager.Instance?.World;if(world?.IsRemote()!=false)throw new InvalidOperationException("Authoritative native identity required");
        string directory=GameIO.GetSaveGameDir();string path=Path.Combine(directory,"RebirthNpcStableIdentities.xml");
        var group=RebirthNpcLiveRecoveryGroup.Read(directory);
        if(group!=null&&group.Target=="RebirthNpcStableIdentities.xml"&&group.Phase<2)
        {
            var original=RebirthNpcLiveRecoveryIntent.Read(directory,group.Target);
            path=original.BeforeHash!="absent"&&RebirthNpcLiveRecoveryIntent.Hash(original.Prior)==original.BeforeHash?original.Prior:null;
        }
        System.Xml.XmlDocument primary=null;
        foreach(var pair in deferred)
        {
            if(!ReferenceEquals(pair.Value,world)||!ReferenceEquals(world.GetEntity(pair.Key.entityId),pair.Key))continue;
            if(!pair.Key.TryCompleteDeferredDomainRegistration())throw new InvalidOperationException("Original world domain remains retained before identity binding");
            if(pair.Key.BindCurrentRecoveryIdentity(null))continue;
            if(primary==null&&path!=null&&File.Exists(path))primary=RebirthNpcRecoveryCurrentBacking.Read(path);
            if(!pair.Key.BindCurrentRecoveryIdentity(primary))throw new InvalidDataException("Existing native body has no independent exact current identity binding");
        }
    }
    internal static void PrimeDeferredSemantics(string target)
    {
        if(target==RebirthNpcAggregatePersistenceStore.FileName)return;
        var world=GameManager.Instance?.World;
        foreach(var pair in deferred)
        {
            var actor=pair.Key;
            if(!ReferenceEquals(pair.Value,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||deferredProjected.Contains(actor))continue;
            if(!RebirthNpcAggregatePersistenceStore.TryGet(actor.RebirthRuntimeState.StableId,out var record)||
                !RebirthNpcRecoveryRuntimeProjection.TryPrepareCurrentNative(actor,record,out var projection,out _)||!projection.Install())throw new InvalidDataException("Current deferred native semantic identity is unavailable");
            deferredProjected.Add(actor);
        }
    }
    private static void ResumeDeferred()
    {
        if(deferred.Count==0)return;
        if(current!=null&&!current.Installed)return;
        if(current==null&&RebirthNpcPersistenceCoordinator.IsRecoveryInstalling)return;
        var world=GameManager.Instance?.World;
        foreach(var pair in new List<KeyValuePair<EntityRebirthNPC,World>>(deferred))
        {
            if(!ReferenceEquals(pair.Value,world))continue;
            var actor=pair.Key;if(!ReferenceEquals(world.GetEntity(actor.entityId),actor)){deferred.Remove(actor);deferredProjected.Remove(actor);continue;}
            if(RebirthNpcRuntimeNativeAdoption.HasIncomingActivationHistory(actor))
            {var native=RebirthNpcRuntimeNativeAdoption.NativeReentry.Capture(actor);if(native!=null&&!RebirthNpcRuntimeNativeAdoption.IsIncomingHeld(actor))TryResumeAdmittedNativeActivation(native);continue;}
            if(!actor.TryCompleteDeferredDomainRegistration())throw new InvalidOperationException("Original world domain remains retained before native activation");
            if(!actor.NativeIdentityReadable)throw new InvalidDataException("Native identity decoding failed; exact body retained without projection");
            if(!deferredProjected.Contains(actor))
            {
                bool recoveredExecution=current?.Target=="RebirthNpcExecutionState.xml"&&RebirthNpcExecutionLeaseRegistry.TryGetActive(actor.entityId,out _);
                var executionOrder=actor.RebirthRuntimeState.Order;var executionGuard=actor.RebirthRuntimeState.GuardPosition;bool hasExecutionGuard=actor.RebirthRuntimeState.HasGuardPosition;
                if(!RebirthNpcAggregatePersistenceStore.TryGet(actor.RebirthRuntimeState.StableId,out var record))throw new InvalidDataException("Current native identity has no qualified installed aggregate");
                if(!RebirthNpcRecoveryRuntimeProjection.TryPrepareCurrentNative(actor,record,out var projection,out var error)||!projection.Install())throw new InvalidOperationException(error??"Deferred semantic projection changed");
                if(recoveredExecution){actor.RebirthRuntimeState.Order=executionOrder;actor.RebirthRuntimeState.GuardPosition=executionGuard;actor.RebirthRuntimeState.HasGuardPosition=hasExecutionGuard;actor.RebirthRuntimeState.MarkPersisted();}
                deferredProjected.Add(actor);
            }
            actor.ResumeRecoveryNativeActivation();
            if(RebirthNpcExecutionPersistenceStore.HasUnrestoredOrdinary(actor.RebirthRuntimeState.StableId,actor.entityId))throw new InvalidOperationException("Current saved execution original still awaits exact native acceptance");
            deferred.Remove(actor);deferredProjected.Remove(actor);
        }
    }
    internal static void Install()
    {
        if(installed)return;RebirthNpcNativeStableIdentityCodec.InstallNativeGuard();RebirthNpcRecoveryProjectionNetwork.Install();
        ModEvents.CreateWorldDone.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SCreateWorldDoneData>(OnWorldCreated));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));installed=true;
    }
    private static void OnWorldCreated(ref ModEvents.SCreateWorldDoneData data)
    {readyWorld=GameManager.Instance?.World;retry=0;Pump();}
    private static void OnUpdate(ref ModEvents.SGameUpdateData data){Pump();}
    internal static bool BeforeOrdinaryLoad()
    {
        var world=GameManager.Instance?.World;if(world?.IsRemote()!=false)return true;
        if(pumping)return current==null?!RebirthNpcPersistenceCoordinator.IsRecoveryInstalling:current.Installed&&!RebirthNpcPersistenceCoordinator.IsRecoveryInstalling;
        Pump();return !RebirthNpcPersistenceCoordinator.IsRecoveryInstalling;
    }
    internal static bool Request(string file,out string detail)
    {
        detail=string.Empty;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)
        {detail="Current authoritative game thread required";return false;}
        // Refuse before installing/staging anything in B while A still owns
        // the driver or native registry. Pump's refusal alone must not fall
        // through into a fresh B transaction.
        if(HasUnretiredForeignScope||RebirthNpcRuntimeRegistry.HasForeignDomain)
        {detail="Original NPC world requires typed retirement before recovery admission";return false;}
        Install();
        // An administrator can only invoke this after the authoritative world
        // exists; CreateWorldDone remains the automatic cold admission boundary.
        if(!ReferenceEquals(readyWorld,world)){detail="Actual CreateWorldDone boundary has not completed";return false;}
        Guid priorCompleted=lastCompleted;Pump();
        if(lastCompleted!=priorCompleted&&string.Equals(lastCompletedTarget,file,StringComparison.OrdinalIgnoreCase)){detail=lastDetail;return true;}
        if(current!=null)
        {
            if(!string.Equals(current.Target,file,StringComparison.OrdinalIgnoreCase)){detail="Another original recovery remains pending: "+current.Operation;return false;}
            retry=0;bool done=Advance();detail=lastDetail;return done;
        }
        if(!string.IsNullOrEmpty(GameIO.GetSaveGameDir())&&RebirthNpcLiveRecoveryGroup.Read(GameIO.GetSaveGameDir())!=null)
        {retry=0;Pump();detail=lastDetail;return false;}
        bool staged=RebirthNpcLiveRecoverySession.TryStage(file,out current,out string error);
        if(!staged&&current==null){detail="Recovery refused; original evidence retained: "+error;lastDetail=detail;return false;}
        loadedAfterRecovery=false;retry=0;bool completed=Advance();detail=lastDetail;return completed;
    }
    internal static void Pump()
    {
        if(pumping||!ThreadManager.IsMainThread())return;var world=GameManager.Instance?.World;
        if(world?.IsRemote()==true)
        {
            if(RebirthNpcRuntimeRegistry.HasForeignDomain||deferred.Count==0)return;
            foreach(var pair in new List<KeyValuePair<EntityRebirthNPC,World>>(deferred))
            {
                if(!ReferenceEquals(pair.Value,world)||!ReferenceEquals(world.GetEntity(pair.Key.entityId),pair.Key))continue;
                try{pair.Key.ResumeRemoteDomainActivation();deferred.Remove(pair.Key);deferredProjected.Remove(pair.Key);}
                catch(Exception ex){lastDetail="Remote native registration retained: "+ex.GetType().Name;}
            }
            return;
        }
        if(world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)return;
        string save=RebirthNpcSaveScope.CurrentDirectory();if(string.IsNullOrEmpty(save)||!ReferenceEquals(world,readyWorld))return;
        RebirthNpcSaveQuarantineBundle.RetryServerNativeAdmission(world,save);
        if(!ReferenceEquals(context,world)||directory!=save)
        {
            if(context!=null||current!=null)
            {lastDetail="Original recovery driver requires atomic typed original-scope retirement";return;}
            foreach(var pair in deferred)if(!ReferenceEquals(pair.Value,world))
            {lastDetail="Original deferred native observation requires typed retirement";return;}
            context=world;directory=save;retry=0;loadedAfterRecovery=false;lastCompleted=Guid.Empty;lastCompletedTarget=null;lastCompletedGeneration=0;
        }
        if(Time.realtimeSinceStartup<retry)return;retry=Time.realtimeSinceStartup+.25;
        pumping=true;
        try
        {
            if(current==null)
            {
                var group=RebirthNpcLiveRecoveryGroup.Read(save);if(group==null){ResumeDeferred();return;}
                if(!RebirthNpcLiveRecoverySession.TryOpenPending(out current,out string error))
                {lastDetail="Recovery retained pending: "+error;return;}
            }
            Advance();
        }
        catch(Exception ex){lastDetail="Recovery retained pending: "+ex.GetType().Name+": "+ex.Message;}
        finally{pumping=false;}
    }
    private static bool Advance()
    {
        bool previous=pumping;pumping=true;
        try{return AdvanceCore();}finally{pumping=previous;}
    }
    private static bool AdvanceCore()
    {
        if(current==null)return false;Guid operation=current.Operation;
        if(RebirthNpcRecoveryFaults.IsPaused(operation)){lastDetail="Recovery fixture paused at actual durable boundary operation="+operation.ToString("N")+" generation="+current.Generation+" localInstalled="+current.Installed;return false;}
        if(current.Installed)ResumeDeferred();
        bool complete=current.TryProgress(out string error);
        if(current.Installed&&!loadedAfterRecovery)
        {
            // Populate unaffected normal stores only AFTER exact installed
            // generation adoption. Recursive entry cannot repeat the recovery.
            bool prior=pumping;pumping=true;
            try{RebirthNpcPersistenceCoordinator.EnsureLoaded();loadedAfterRecovery=true;}
            catch(Exception ex){lastDetail="Installed recovery; unrelated load pending: "+ex.GetType().Name;}
            finally{pumping=prior;}
        }
        if(complete&&current.Completed)
        {
            lastDetail="Recovery converged operation="+operation.ToString("N")+" generation="+current.Generation;
            lastCompleted=operation;lastCompletedTarget=current.Target;lastCompletedGeneration=current.Generation;
            RebirthNpcAdministrationSupportService.RecordConvergedRecovery(operation,lastDetail);
            current.Dispose();current=null;loadedAfterRecovery=false;return true;
        }
        lastDetail="Recovery pending operation="+operation.ToString("N")+" generation="+current.Generation+" localInstalled="+current.Installed+": "+error;return false;
    }
    internal static object CaptureAuditState()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null)return new {available=false};
        if(world.IsRemote())return new {available=true,authoritative=false,projection=RebirthNpcRecoveryProjectionNetwork.CaptureAuditState()};
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))return new {available=false};
        var group=RebirthNpcLiveRecoveryGroup.Read(save);var rows=new System.Collections.Generic.List<object>();
        if(group!=null)foreach(var member in group.Members)
        {
            string path=Path.Combine(group.Directory,member.File);
            rows.Add(new {file=member.File,candidateHash=member.Hash,beforeHash=member.BeforeHash,terminalHash=member.TerminalHash,terminalSequence=member.TerminalSequence,
                intentPresent=File.Exists(RebirthNpcPersistenceFile.RecoveryIntentPath(path)),publication=RebirthNpcRecoveryPublications.CaptureAuditState(path)});
        }
        var actors=new System.Collections.Generic.List<object>();var states=RebirthNpcRuntimeRegistry.GetSnapshot();
        foreach(var state in states)
        {
            if(actors.Count>=256)break;if(!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out int entity)||world.GetEntity(entity) is not EntityRebirthNPC npc)continue;
            actors.Add(new {stable=state.StableId.ToString(),entity,revision=state.Revision,presence=state.Presence.ToString(),order=state.Order.ToString(),
                ownership=state.OwnershipKind.ToString(),name=npc.EntityName,health=npc.Health,deferred=IsNativeActivationDeferred(npc),nativeStable=npc.NativeStableIdentity.ToString(),equipment=RebirthNpcNativeEquipmentBridge.CaptureAuditState(state.StableId)});
        }
        bool same=ReferenceEquals(context,world)&&directory==save;
        return new {available=true,authoritative=true,status=lastDetail,operation=group?.Operation.ToString("N")??(same&&lastCompleted!=Guid.Empty?lastCompleted.ToString("N"):null),
            generation=group?.Generation??(same?lastCompletedGeneration:0),phase=group==null?(byte?)null:group.Phase,
            pending=group!=null,localInstalled=current?.Installed==true&&current.IsCurrent,completed=group==null&&same&&lastCompleted!=Guid.Empty,
            installing=RebirthNpcPersistenceCoordinator.IsRecoveryInstalling,stores=rows.ToArray(),actors=actors.ToArray(),actorCount=states.Length,actorLimit=256,
            primitiveCaches=current?.PrimitiveCacheStatus,
            fault=RebirthNpcRecoveryFaults.Capture(),
            projection=RebirthNpcRecoveryProjectionNetwork.CaptureAuditState()};
    }    internal static string GetReport()=>"[REBIRTH NPC Recovery] "+lastDetail;
}
