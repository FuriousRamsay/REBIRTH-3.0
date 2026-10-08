using System;

// Captures one original authority incarnation. Producers cannot acquire actuation by constructing this.
internal sealed class RebirthNpcEmbodiedWorkIntent
{
    internal EntityRebirthNPC Actor { get; }
    internal World OriginalWorld { get; }
    internal int NativeId { get; }
    internal RebirthNpcStableId StableId { get; }
    internal string ProfileId { get; }
    internal uint Generation { get; }
    internal string SaveFingerprint { get; }
    internal object OriginalRuntime { get; }
    internal object OriginalHoldingData { get; }
    internal object OriginalHeldValue { get; }
    internal string Path { get; }
    internal ulong PrimaryId { get; }
    internal ulong SecondaryId { get; }
    internal uint Revision { get; }
    internal string ExecutorId { get; }
    internal long ExecutorGeneration { get; }
    private readonly Func<bool> current;
    private readonly Action begin,tick;
    // Reference binding is not full item/action semantics, native cancellation or outcome proof.
    internal bool HasQualifiedActuator => false;
    private RebirthNpcEmbodiedWorkIntent(EntityRebirthNPC actor,World world,string save,uint generation,
        string path,ulong primary,ulong secondary,uint revision,string executor,long executorGeneration,
        Func<bool> isCurrent,Action onBegin,Action onTick)
    {
        Actor=actor;OriginalWorld=world;NativeId=actor.entityId;StableId=actor.RebirthRuntimeState.StableId;
        ProfileId=actor.RebirthRuntimeState.ProfileId;Generation=generation;SaveFingerprint=save;
        OriginalRuntime=actor.RebirthRuntimeState;OriginalHoldingData=actor.inventory.holdingItemData;
        OriginalHeldValue=actor.inventory.holdingItemItemValue;Path=path;PrimaryId=primary;SecondaryId=secondary;
        Revision=revision;ExecutorId=executor;ExecutorGeneration=executorGeneration;current=isCurrent;begin=onBegin;tick=onTick;
    }
    internal static bool TryCreate(EntityRebirthNPC actor,string path,ulong primaryId,ulong secondaryId,
        uint revision,string executorId,long executorGeneration,Func<bool> isCurrent,Action begin,Action tick,
        out RebirthNpcEmbodiedWorkIntent intent)
    {
        intent=null;
        try
        {
            var world=GameManager.Instance?.World;var runtime=actor?.RebirthRuntimeState;
            if(actor==null||world==null||world.IsRemote()||!ReferenceEquals(actor.world,world)||
                !ReferenceEquals(world.GetEntity(actor.entityId),actor)||actor.IsDead()||runtime==null||runtime.StableId.IsEmpty||
                runtime.Presence!=RebirthNpcPresenceState.Active||actor.IsPreparedRestorationPending||runtime.PreparedRestorationPending||
                !RebirthNpcStructuralOperationIdentity.Text(path,128)||primaryId==0||revision==0||
                !RebirthNpcStructuralOperationIdentity.Text(executorId,128)||executorGeneration<=0||
                isCurrent==null||begin==null||tick==null||actor.inventory?.holdingItemData==null||actor.inventory.holdingItemItemValue==null||
                !RebirthNpcAggregatePersistenceStore.TryGetView(runtime.StableId,out var person)||person?.Identity==null||
                person.Identity.StableNpcId!=runtime.StableId||person.Profile?.ProfileId!=runtime.ProfileId||
                person.Presence==null||person.Presence.EmbodimentGeneration==0)return false;
            var save=RebirthNpcSaveScope.ObserveCurrent();
            if(save?.Fingerprint==null||save.Fingerprint.Length!=64)return false;
            intent=new RebirthNpcEmbodiedWorkIntent(actor,world,save.Fingerprint,person.Presence.EmbodimentGeneration,
                path,primaryId,secondaryId,revision,executorId,executorGeneration,isCurrent,begin,tick);
            if(intent.MatchesOriginal())return true;
            intent=null;return false;
        }
        catch {intent=null;return false;}
    }
    internal bool SameKey(RebirthNpcEmbodiedWorkIntent other)=>other!=null&&ReferenceEquals(Actor,other.Actor)&&
        ReferenceEquals(OriginalWorld,other.OriginalWorld)&&NativeId==other.NativeId&&StableId==other.StableId&&
        ProfileId==other.ProfileId&&Generation==other.Generation&&SaveFingerprint==other.SaveFingerprint&&
        ReferenceEquals(OriginalRuntime,other.OriginalRuntime)&&ReferenceEquals(OriginalHoldingData,other.OriginalHoldingData)&&
        ReferenceEquals(OriginalHeldValue,other.OriginalHeldValue)&&Path==other.Path&&PrimaryId==other.PrimaryId&&
        SecondaryId==other.SecondaryId&&Revision==other.Revision&&ExecutorId==other.ExecutorId&&ExecutorGeneration==other.ExecutorGeneration;
    internal bool MatchesOriginal()
    {
        try
        {
            var runtime=Actor.RebirthRuntimeState;
            return ReferenceEquals(GameManager.Instance?.World,OriginalWorld)&&!OriginalWorld.IsRemote()&&
                ReferenceEquals(Actor.world,OriginalWorld)&&Actor.entityId==NativeId&&ReferenceEquals(OriginalWorld.GetEntity(NativeId),Actor)&&
                !Actor.IsDead()&&ReferenceEquals(runtime,OriginalRuntime)&&runtime.StableId==StableId&&runtime.ProfileId==ProfileId&&
                runtime.Presence==RebirthNpcPresenceState.Active&&!Actor.IsPreparedRestorationPending&&!runtime.PreparedRestorationPending&&
                ReferenceEquals(Actor.inventory?.holdingItemData,OriginalHoldingData)&&ReferenceEquals(Actor.inventory?.holdingItemItemValue,OriginalHeldValue)&&
                RebirthNpcSaveScope.ObserveCurrent()?.Fingerprint==SaveFingerprint&&
                RebirthNpcAggregatePersistenceStore.TryGetView(StableId,out var person)&&person?.Identity?.StableNpcId==StableId&&
                person.Profile?.ProfileId==ProfileId&&person.Presence?.EmbodimentGeneration==Generation;
        }
        catch{return false;}
    }
    internal bool ProducerIsCurrent()=>current();
    internal void BeginOwned(){if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(this))throw new InvalidOperationException("Original EAI work dispatch is not owned.");begin();}
    internal void TickOwned(){if(!RebirthNpcEmbodiedWorkOwner.IsDispatching(this))throw new InvalidOperationException("Original EAI work dispatch is not owned.");tick();}
}


