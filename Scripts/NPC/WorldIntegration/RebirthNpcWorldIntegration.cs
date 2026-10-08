using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using UnityEngine;

#nullable disable

public enum RebirthNpcModelPipeline : byte { HumanSdcs=1, HumanLegacy=2, Dog=3, Panther=4 }
public enum RebirthNpcPresentationEvent : byte { Idle=0, Work=1, Travel=2, Combat=3, Hurt=4, Death=5, Mission=6 }

public sealed class RebirthNpcProductionProfile
{
    public string ProfileId, SpawnGroup, EntityClassKey, LocalizationKey, AnimationSet, SoundSet;
    public RebirthNpcCategory Category;
    public RebirthNpcCapabilities Capabilities;
    public RebirthNpcModelPipeline ModelPipeline;
    public bool AmbientEligible, PromotionEligible, Persistent;
}

public static class RebirthNpcProductionProfileCatalogue
{
    private static readonly object Sync=new object();
    private static readonly Dictionary<string,RebirthNpcProductionProfile> Profiles=new Dictionary<string,RebirthNpcProductionProfile>(StringComparer.OrdinalIgnoreCase);
    private static bool initialized;
    public static void EnsureInitialized()
    {
        lock(Sync){if(initialized)return;
            Add("survivor.ambient","npcSurvivors","EntityRebirthSurvivor",RebirthNpcCategory.Survivor,RebirthNpcModelPipeline.HumanSdcs,
                RebirthNpcCapabilities.Dialogue|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.Equipment|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.RangedCombat,"npc_survivor","human.activity","human.voice",true,true,false);
            Add("survivor.persistent","npcSurvivorsPersistent","EntityRebirthSurvivor",RebirthNpcCategory.Survivor,RebirthNpcModelPipeline.HumanSdcs,
                RebirthNpcCapabilities.Ownership|RebirthNpcCapabilities.Hireable|RebirthNpcCapabilities.Contract|RebirthNpcCapabilities.Orders|RebirthNpcCapabilities.Travel|RebirthNpcCapabilities.Respawn|RebirthNpcCapabilities.Persistent|RebirthNpcCapabilities.Dialogue|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.Equipment|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.RangedCombat|RebirthNpcCapabilities.NavMarker|RebirthNpcCapabilities.Mission,"npc_survivor","human.activity","human.voice",false,false,true);
            Add("bandit.standard","npcBandits","EntityRebirthBandit",RebirthNpcCategory.Bandit,RebirthNpcModelPipeline.HumanSdcs,
                RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.Equipment|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.RangedCombat,"npc_bandit","human.activity","bandit.voice",true,false,false);
            Add("special.humanoid","npcSpecialHumanoids","EntityRebirthSpecialHumanoid",RebirthNpcCategory.SpecialHumanoid,RebirthNpcModelPipeline.HumanLegacy,
                RebirthNpcCapabilities.Dialogue|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.Equipment|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.RangedCombat|RebirthNpcCapabilities.Persistent,"npc_special_humanoid","human.activity","special.voice",true,true,true);
            Add("companion.dog","npcDogCompanions","EntityRebirthDogCompanion",RebirthNpcCategory.DogCompanion,RebirthNpcModelPipeline.Dog,
                RebirthNpcCapabilities.Ownership|RebirthNpcCapabilities.Orders|RebirthNpcCapabilities.Travel|RebirthNpcCapabilities.Respawn|RebirthNpcCapabilities.Persistent|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.NavMarker|RebirthNpcCapabilities.Alert,"npc_dog_companion","dog.activity","dog.voice",false,false,true);
            Add("companion.panther","npcPantherCompanions","EntityPantherCompanionRebirth",RebirthNpcCategory.PantherCompanion,RebirthNpcModelPipeline.Panther,
                RebirthNpcCapabilities.Ownership|RebirthNpcCapabilities.Orders|RebirthNpcCapabilities.Travel|RebirthNpcCapabilities.Respawn|RebirthNpcCapabilities.Persistent|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.NavMarker|RebirthNpcCapabilities.Pounce,"npc_panther_companion","panther.activity","panther.voice",false,false,true);
            // Authored profession profiles retain their stable identity; no ambient promotion
            // to a generic survivor profile, which would discard instructor expertise.
            foreach(var profession in new[]{"combat_instructor","firearms_instructor","wilderness_guide","farmer","technician","medic","cook","builder","chemist","scout","quartermaster","mystic"})
                Add("specialist."+profession,"npcSpecialist_"+profession,"EntityRebirthSurvivor",RebirthNpcCategory.Survivor,RebirthNpcModelPipeline.HumanSdcs,
                    RebirthNpcCapabilities.Dialogue|RebirthNpcCapabilities.Inventory|RebirthNpcCapabilities.Equipment|RebirthNpcCapabilities.MeleeCombat|RebirthNpcCapabilities.RangedCombat|RebirthNpcCapabilities.Persistent,
                    "npc_specialist_"+profession,"human.activity","human.voice",false,false,true);
            initialized=true;}
    }
    private static void Add(string id,string group,string cls,RebirthNpcCategory cat,RebirthNpcModelPipeline model,RebirthNpcCapabilities caps,string loc,string anim,string sound,bool ambient,bool promote,bool persistent)
    {
        if(string.IsNullOrWhiteSpace(group)||string.IsNullOrWhiteSpace(cls)||cat==RebirthNpcCategory.Unknown)throw new InvalidOperationException("Invalid production profile "+id);
        var d=new RebirthNpcProductionProfile{ProfileId=id,SpawnGroup=group,EntityClassKey=cls,Category=cat,ModelPipeline=model,Capabilities=caps,LocalizationKey=loc,AnimationSet=anim,SoundSet=sound,AmbientEligible=ambient,PromotionEligible=promote,Persistent=persistent};
        Profiles.Add(id,d); RebirthNpcProfileRegistry.Register(new RebirthNpcProfile(id,cat,caps),true);
    }
    public static bool TryGet(string id,out RebirthNpcProductionProfile value){EnsureInitialized();lock(Sync)return Profiles.TryGetValue(id??string.Empty,out value);}
    public static RebirthNpcProductionProfile[] Snapshot(){EnsureInitialized();lock(Sync){var a=new RebirthNpcProductionProfile[Profiles.Count];Profiles.Values.CopyTo(a,0);Array.Sort(a,(x,y)=>string.Compare(x.ProfileId,y.ProfileId,StringComparison.Ordinal));return a;}}
    public static string Validate(){EnsureInitialized();var seen=new HashSet<RebirthNpcCategory>();foreach(var p in Snapshot()){seen.Add(p.Category);if(string.IsNullOrWhiteSpace(p.AnimationSet)||string.IsNullOrWhiteSpace(p.SoundSet))return "FAIL missing presentation mapping "+p.ProfileId;}foreach(RebirthNpcCategory c in new[]{RebirthNpcCategory.Survivor,RebirthNpcCategory.Bandit,RebirthNpcCategory.SpecialHumanoid,RebirthNpcCategory.DogCompanion,RebirthNpcCategory.PantherCompanion})if(!seen.Contains(c))return "FAIL missing category "+c;return "PASS";}
}

public sealed class RebirthNpcWorldIdentityRecord
{
    public int AmbientEntityId; public RebirthNpcStableId StableId; public string ProfileId,DisplayName; public long PromotedUtcTicks; public bool Persistent;
}
public sealed class RebirthNpcMarkerProjection
{
    public string ViewerId; public RebirthNpcStableId StableId; public string DisplayName; public Vector3 Position; public bool Compass,Map; public uint Revision;
}

public static class RebirthNpcWorldIntegrationAdapters
{
    public delegate bool SpawnDelegate(RebirthNpcProductionProfile profile,Vector3 position,out int entityId,out string reason);
    public delegate bool AmbientDelegate(int entityId,out string profileId,out Vector3 position,out string reason);
    public delegate bool MarkerDelegate(RebirthNpcMarkerProjection marker,bool remove,out string reason);
    public delegate bool PresentationDelegate(RebirthNpcStableId stableId,string animationKey,string soundKey,out string reason);
    public delegate bool PartyDelegate(string viewerId,string ownerId);
    public static SpawnDelegate Spawn;
    public static AmbientDelegate ResolveAmbient;
    public static MarkerDelegate Marker;
    public static PresentationDelegate Presentation;
    public static PartyDelegate IsPartyMember;
}

public static class RebirthNpcWorldIntegrationService
{
    private const int SchemaVersion=2; private const string FileName="RebirthNpcWorldIntegration.xml";
    private static readonly object Sync=new object();
    private static Dictionary<int,RebirthNpcWorldIdentityRecord> ByAmbient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
    private static Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> ByStable=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>();
    private static readonly Dictionary<string,RebirthNpcMarkerProjection> Markers=new Dictionary<string,RebirthNpcMarkerProjection>(StringComparer.Ordinal);
    private static Dictionary<string,RebirthNpcPendingSpawn> PendingSpawns=new Dictionary<string,RebirthNpcPendingSpawn>(StringComparer.Ordinal);
    private static Dictionary<string,RebirthNpcSpawnCompletion> Completions=new Dictionary<string,RebirthNpcSpawnCompletion>(StringComparer.Ordinal);
    private static bool worldSnapshotPublicationUncertain;
    private static World loadedNativeWorld;
    private static string loadedNativeDirectory=string.Empty;
    private static readonly RebirthNpcSpawnAttemptGate SpawnAttempts=new RebirthNpcSpawnAttemptGate();
    private static HashSet<string> Replay=new HashSet<string>(StringComparer.Ordinal);
    private static bool initialized,loaded,dirty; private static long spawns,promotions,duplicatePromotions,warmActivations,warmDeactivations,markerUpdates,markerRemovals,presentationEvents;
    private static readonly string[] SurvivorNames={"Mara Voss","Elias Ward","Nora Hale","Jonas Reed","Tessa Vale","Cal Mercer"};
    private static readonly string[] BanditNames={"Rook","Sable","Knox","Viper","Mace","Ash"};
    private static readonly string[] SpecialNames={"The Warden","The Herald","The Seeker","The Exile"};
    private static readonly string[] DogNames={"Scout","Bruno","Ranger","Patch","Milo","Bear"};
    private static readonly string[] PantherNames={"Nyx","Shade","Onyx","Ember","Vanta","Echo"};

    public static void EnsureInitialized(){lock(Sync){if(initialized)return;RebirthNpcProductionProfileCatalogue.EnsureInitialized();LoadNoLock();initialized=true;}}
    // Existing-person preparation never manufactures an identity or changes a profile.
    internal static bool TryPrepareExistingPerson(string replayId,RebirthNpcStableId stable,string entityClass,
        Vector3 position,float yaw,out RebirthNpcPendingSpawn request,out string reason)
    {
        request=null;reason="Existing persistent humanoid person required.";
        if(!IsServer()||GameManager.Instance?.World==null||GameManager.Instance.World.IsRemote())return false;
        EnsureInitialized();EnsurePersistenceLoaded();
        if(!RebirthNpcAggregatePersistenceStore.TryGet(stable,out var person)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason)||
            !RebirthNpcPendingSpawn.TryCreate("spawn:"+replayId,stable,profile,entityClass,
                position.x,position.y,position.z,yaw,DateTime.UtcNow.Ticks,out var proposed))return false;
        if(!TryValidateSpawnPlacement(proposed)){reason="Loaded safe placement and exact physical/profile binding required.";return false;}
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||!IsPreparedContextCurrentNoLock())return false;
            if(Replay.Contains(proposed.ReplayKey)){reason="Request already completed; reconcile the original live embodiment.";return false;}
            if(PendingSpawns.TryGetValue(proposed.ReplayKey,out var previous))
            {
                if(previous.StableId!=proposed.StableId||previous.Profile!=proposed.Profile||previous.EntityClass!=proposed.EntityClass||
                    previous.X!=proposed.X||previous.Y!=proposed.Y||previous.Z!=proposed.Z||previous.Yaw!=proposed.Yaw)
                {reason="Replay key is already bound to a different original request.";return false;}
                request=previous;
                try{SaveIfDirty();return HasSavedPendingSpawn(previous);}catch{reason="Original intent requires durable save retry.";return false;}
            }
            foreach(var pending in PendingSpawns.Values)if(pending.StableId==proposed.StableId)
            {reason="Original person already has a different pending request.";return false;}
            if(PendingSpawns.Count>=1024||Replay.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords)return false;
            PendingSpawns.Add(proposed.ReplayKey,proposed);dirty=true;request=proposed;
            try{SaveIfDirty();if(!HasSavedPendingSpawn(proposed))return false;reason=string.Empty;return true;}
            catch{reason="Original intent requires durable save retry.";return false;}
        }
    }
    internal static bool TryGetCompletedBinding(string replayId,out RebirthNpcSpawnCompletion binding)
    {
        binding=null;if(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+replayId)||!IsServer())return false;
        EnsureInitialized();EnsurePersistenceLoaded();lock(Sync)return IsPreparedContextCurrentNoLock()&&Completions.TryGetValue("spawn:"+replayId,out binding);
    }    internal static bool TryGetPreparedRequest(string replayId,out RebirthNpcPendingSpawn request)
    {
        request=null;if(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+replayId)||!IsServer())return false;
        EnsureInitialized();EnsurePersistenceLoaded();lock(Sync)return IsPreparedContextCurrentNoLock()&&PendingSpawns.TryGetValue("spawn:"+replayId,out request);
    }
    internal static bool TryPrepareSpawn(string replayId,string profileId,string entityClassName,Vector3 position,float yaw,
        out RebirthNpcPendingSpawn request,out string reason)
    {
        request=null;reason="Spawn preparation is unavailable.";
        var world=GameManager.Instance?.World;
        if(!IsServer()||world==null||world.IsRemote())return false;
        EnsureInitialized();
        if(!RebirthNpcProductionProfileCatalogue.TryGet(profileId,out var profile)||
            !RebirthNpcPendingSpawn.TryCreate("spawn:"+replayId,RebirthNpcStableId.NewId(),profile.ProfileId,entityClassName,
                position.x,position.y,position.z,yaw,DateTime.UtcNow.Ticks,out var proposed))return false;
        if(!TryValidateSpawnPlacement(proposed)){reason="A current entity definition and loaded safe spawn location are required.";return false;}
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||PendingSpawns.ContainsKey(proposed.ReplayKey)||Replay.Contains(proposed.ReplayKey)||PendingSpawns.Count>=1024||Replay.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords)
            {reason="This spawn request is already reserved or the journal is full.";return false;}
            PendingSpawns.Add(proposed.ReplayKey,proposed);dirty=true;request=proposed;
            try
            {
                SaveIfDirty();
                if(!HasSavedPendingSpawn(proposed)){reason="Spawn preparation is waiting for durable confirmation.";return false;}
                reason=string.Empty;return true;
            }
            catch{reason="Spawn preparation is waiting to be saved.";return false;}
        }
    }
    // Rechecks current definitions and loaded terrain; never loads chunks or changes terrain.
    internal static bool TryValidateSpawnPlacement(RebirthNpcPendingSpawn request)
    {
        var world=GameManager.Instance?.World;
        if(request==null||!IsServer()||world==null||world.IsRemote()||
            !RebirthNpcProductionProfileCatalogue.TryGet(request.Profile,out var profile))return false;
        int classId=EntityClass.FromString(request.EntityClass);
        if(EntityClass.list==null||!EntityClass.list.TryGetValue(classId,out var definition)||definition?.Properties==null)return false;
        string runtime=definition.Properties.GetString("Class");
        if(runtime==null||runtime.Split(',').Length!=2||!string.Equals(runtime.Split(',')[1].Trim(),typeof(EntityRebirthNPC).Assembly.GetName().Name,StringComparison.Ordinal)||!string.Equals(runtime.Split(',')[0].Trim(),profile.EntityClassKey,StringComparison.Ordinal)||
            !string.Equals(definition.Properties.GetString("RebirthProfile"),profile.ProfileId,StringComparison.OrdinalIgnoreCase))return false;
        Type type=typeof(EntityRebirthNPC).Assembly.GetType(profile.EntityClassKey,false);
        if(type==null||type.IsAbstract||!typeof(EntityRebirthNPC).IsAssignableFrom(type))return false;
        int x=Utils.Fastfloor(request.X),y=Utils.Fastfloor(request.Y),z=Utils.Fastfloor(request.Z);
        var chunk=world.GetChunkFromWorldPos(x,z) as Chunk;
        if(chunk==null||!world.IsInPlayfield(chunk)||!chunk.CanMobsSpawnAtPos(World.toBlockXZ(x),y,World.toBlockXZ(z)))
        return false;
        return true;
    }
    internal static bool TryRetrySpawnPreparation(RebirthNpcPendingSpawn request)
    {
        var world=GameManager.Instance?.World;
        if(request==null||!IsServer()||world==null||world.IsRemote())return false;
        EnsureInitialized();
        lock(Sync)
        {
            if(!PendingSpawns.TryGetValue(request.ReplayKey,out var stored)||
                !System.Xml.Linq.XNode.DeepEquals(stored.Write(),request.Write()))return false;
            try{SaveIfDirty();return HasSavedPendingSpawn(stored);}catch{return false;}
        }
    }
    // This saved transition is required before a future dispatcher invokes native creation.
    internal static bool TryMarkSpawnCreationAttempt(RebirthNpcPendingSpawn request,out RebirthNpcPendingSpawn attempted)
    {
        attempted=null;var world=GameManager.Instance?.World;
        if(request==null||!IsServer()||world==null||world.IsRemote())return false;
        EnsureInitialized();
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||!PendingSpawns.TryGetValue(request.ReplayKey,out var stored)||stored.IsAttempted||Replay.Contains(request.ReplayKey)||
                !System.Xml.Linq.XNode.DeepEquals(stored.Write(),request.Write())||!HasSavedPendingSpawn(stored)||
                !ReferenceEquals(GameManager.Instance?.World,world)||!TryValidateSpawnPlacement(stored)||
                !stored.TryMarkAttempted(out var next))return false;
            PendingSpawns[request.ReplayKey]=next;dirty=true;attempted=next;
            try{SaveIfDirty();return HasSavedPendingSpawn(next);}catch{return false;}
        }
    }
    // Constructs once from a saved attempt. The caller owns the candidate even on validation failure.
    // World publication and durable native entity reconciliation are separate required steps.
    internal static bool TryConstructPreparedSpawn(RebirthNpcPendingSpawn request,out Entity candidate,out string reason)
    {
        candidate=null;reason="Prepared spawn construction is unavailable.";
        if(request==null||request.IsAttempted||!TryValidateSpawnPlacement(request)||
            !RebirthNpcStableId.TryParse(request.StableId,out var stable)||
            RebirthNpcRuntimeRegistry.TryGetEntityId(stable,out var existing))return false;
        int classId=EntityClass.FromString(request.EntityClass);
        // Enter before changing the saved phase: a conflicting scope has made no native attempt.
        var creation=EntityFactory.SetupEntityCreationData(classId,new Vector3(request.X,request.Y,request.Z),new Vector3(0f,request.Yaw,0f));
        if(!RebirthNpcPreparedCreationScope.TryEnter(classId,request.Profile,stable,creation.id,out var scope))return false;
        using(scope)
        {
            var originalWorld=GameManager.Instance?.World;string originalSave=GameIO.GetSaveGameDir();
            var boundIntent=request;
            if(RebirthNpcProductionProfileCatalogue.TryGet(request.Profile,out var production)&&
                (production.Category==RebirthNpcCategory.Survivor||production.Category==RebirthNpcCategory.Bandit||production.Category==RebirthNpcCategory.SpecialHumanoid))
            {
                if(!RebirthNpcPreparedEventGateInstaller.IsReady){reason="Original humanoid event guards are not installed.";return false;}
                if(EntityClass.list[classId].UseAIPackages){reason="Original humanoid preparation requires EAI authoring.";return false;}
                if(!RebirthNpcAggregatePersistenceStore.TryGet(stable,out var originalPerson)||
                    !RebirthNpcPreparedCreationScope.TryBindOriginalRestoration(()=>
                        TryValidateOriginalCreationBinding(boundIntent,originalPerson,originalWorld,originalSave)))
                {reason="Original saved reconstruction context could not be bound before native initialization.";return false;}
            }
            // Invalid known original context does not consume a native attempt. Only after the
            // validated planned intent is bound do we advance it durably and switch the proof.
            if(!TryMarkSpawnCreationAttempt(request,out var attempted))return false;
            boundIntent=attempted;
            try
            {
                candidate=EntityFactory.CreateEntity(creation);
                if(candidate==null||!(candidate is EntityRebirthNPC)||candidate.entityClass!=classId||candidate.entityId<=0||
                    !RebirthNpcRuntimeRegistry.TryGet(candidate.entityId,out var runtime)||runtime.StableId!=stable||
                    !string.Equals(runtime.ProfileId,attempted.Profile,StringComparison.OrdinalIgnoreCase)||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(stable,out var canonical)||canonical!=candidate.entityId)
                {reason="Constructed NPC requires identity reconciliation.";return false;}
                bool physicallyHeld=true;
                if(candidate is EntityRebirthHumanoidNPC held)
                    physicallyHeld=RebirthNpcAggregatePersistenceStore.TryGet(stable,out var savedPerson)&&RebirthNpcPreparedPhysicalHold.TryHold(held,savedPerson);
                lock(Sync)
                {
                    if(!PendingSpawns.TryGetValue(attempted.ReplayKey,out var stored)||
                        !System.Xml.Linq.XNode.DeepEquals(stored.Write(),attempted.Write())||
                        !attempted.TryMarkConstructed(candidate.entityId,out var constructed))
                    {reason="Constructed NPC requires journal reconciliation.";return false;}
                    PendingSpawns[attempted.ReplayKey]=constructed;dirty=true;
                    SaveIfDirty();
                    if(!HasSavedPendingSpawn(constructed)){reason="Constructed NPC is waiting for durable confirmation.";return false;}
                }
                if(!physicallyHeld){reason="Original constructed candidate is durably bound but requires physical suspension reconciliation.";return false;}
                reason=string.Empty;return true;
            }
            catch(Exception ex){reason="NPC construction result is uncertain: "+ex.GetType().Name;return false;}
        }
    }
    private static bool TryValidateOriginalCreationBinding(RebirthNpcPendingSpawn intent,
        RebirthNpcPersistentRecord original,World originalWorld,string originalSave)
    {
        if(originalWorld==null||originalWorld.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            string.IsNullOrEmpty(originalSave)||!string.Equals(GameIO.GetSaveGameDir(),originalSave,StringComparison.OrdinalIgnoreCase)||
            original?.NativeReconstruction==null||!original.NativeReconstruction.HasNativeHeader||!original.NativeReconstruction.HasNativeGeometry||!original.NativeReconstruction.HasNativeHand||!original.NativeReconstruction.HasNativeBodyPolicy||!original.NativeReconstruction.BodyPolicy.HasDynamics||!original.NativeReconstruction.Matches(original)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(original,out var profile,out _)||profile!=intent.Profile||
            original.Identity.StableNpcId.ToString()!=intent.StableId||original.Presence.EmbodimentGeneration==0)return false;
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain||!IsPreparedContextCurrentNoLock()||
                !PendingSpawns.TryGetValue(intent.ReplayKey,out var current)||
                !System.Xml.Linq.XNode.DeepEquals(current.Write(),intent.Write())||!HasSavedPendingSpawn(current)||
                !RebirthNpcAggregatePersistenceStore.TryGet(original.Identity.StableNpcId,out var saved))return false;
            return saved.AggregateRevision==original.AggregateRevision&&saved.AggregateChecksum==original.AggregateChecksum&&
                saved.Presence.EmbodimentGeneration==original.Presence.EmbodimentGeneration;
        }
    }
    // Save retry only: never constructs, publishes, retires or rebinds an entity.
    internal static bool TryConfirmConstructedSpawn(string replayId,Entity candidate,out RebirthNpcPendingSpawn request)
    {
        request=null;var world=GameManager.Instance?.World;
        if(!IsServer()||world==null||world.IsRemote()||!(candidate is EntityRebirthNPC npc)||
            !ReferenceEquals(candidate.world,world)||candidate.entityId<=0||npc.IsDead()||npc.IsMarkedForUnload())return false;
        EnsureInitialized();
        lock(Sync)
        {
            string key="spawn:"+replayId;
            if(worldSnapshotPublicationUncertain||!PendingSpawns.TryGetValue(key,out var stored)||!stored.IsConstructed||Replay.Contains(key)||
                stored.NativeEntityId!=candidate.entityId||candidate.entityClass!=EntityClass.FromString(stored.EntityClass)||
                !RebirthNpcStableId.TryParse(stored.StableId,out var stable)||
                !RebirthNpcRuntimeRegistry.TryGet(candidate.entityId,out var runtime)||runtime.StableId!=stable||
                !string.Equals(runtime.ProfileId,stored.Profile,StringComparison.OrdinalIgnoreCase)||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(stable,out var canonical)||canonical!=candidate.entityId)return false;
            var present=world.GetEntity(candidate.entityId);
            if(present!=null&&!ReferenceEquals(present,candidate))return false;
            try
            {
                SaveIfDirty();
                if(!HasSavedPendingSpawn(stored))return false;
                request=stored;return true;
            }
            catch{return false;}
        }
    }
    // Journal the attempt before native publication. A saved publishing phase
    // with no exact live candidate is uncertain and must never be replayed.
    internal static bool TryPublishConstructedSpawn(string replayId,World originalWorld,Entity candidate,out string reason)
    {
        reason="NPC publication requires reconciliation.";
        if(candidate is EntityRebirthHumanoidNPC held && held.IsPreparedRestorationPending && !held.CanPublishPreparedRestoration())
        {reason="Prepared native reconstruction has not completed qualification.";return false;}
        if(originalWorld==null||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            !TryConfirmConstructedSpawn(replayId,candidate,out var request))return false;
        var present=originalWorld.GetEntity(candidate.entityId);
        if(ReferenceEquals(present,candidate))
            return TryObservePublishedSpawn(replayId,originalWorld,candidate,out _);
        if(present!=null||request.IsPublishing)return false;
        if(!SpawnAttempts.TryBegin(request.ReplayKey,out var lease))return false;
        bool attempted=false;
        try
        {
            lock(Sync)
            {
                if(worldSnapshotPublicationUncertain||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
                    !ReferenceEquals(candidate.world,originalWorld)||originalWorld.GetEntity(candidate.entityId)!=null||
                    !PendingSpawns.TryGetValue(request.ReplayKey,out var stored)||
                    !System.Xml.Linq.XNode.DeepEquals(stored.Write(),request.Write())||
                    !request.TryMarkPublishing(out var publishing))return false;
                // Do not roll back this phase after an uncertain save or native call.
                PendingSpawns[request.ReplayKey]=publishing;dirty=true;
                SaveIfDirty();
                if(!HasSavedPendingSpawn(publishing)||
                    !ReferenceEquals(GameManager.Instance?.World,originalWorld)||
                    !ReferenceEquals(candidate.world,originalWorld)||originalWorld.GetEntity(candidate.entityId)!=null)return false;
                attempted=true;
                originalWorld.SpawnEntityInWorld(candidate);
            }
            if(!TryObservePublishedSpawn(replayId,originalWorld,candidate,out _))return false;
            reason=string.Empty;return true;
        }
        catch(Exception ex){reason="NPC publication result is uncertain: "+ex.GetType().Name;return false;}
        finally{if(!attempted)SpawnAttempts.ReleaseKnown(request.ReplayKey,lease);}
    }
    // Construction alone does not prove publication in the original native world.
    internal static bool TryObservePublishedSpawn(string replayId,World originalWorld,Entity candidate,
        out RebirthNpcPendingSpawn request)
    {
        request=null;
        if(originalWorld==null||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            candidate==null||!ReferenceEquals(candidate.world,originalWorld)||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate))return false;
        if(!TryConfirmConstructedSpawn(replayId,candidate,out var confirmed)||
            !ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            !ReferenceEquals(candidate.world,originalWorld)||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate))return false;
        request=confirmed;return true;
    }
    // Reconcile only an exact observed live publication, then atomically acknowledge its original journal key.
    internal static bool TryCompletePublishedExistingPerson(string replayId,World originalWorld,Entity candidate,out string reason)
    {
        reason="Original published embodiment requires reconciliation.";
        if(!IsServer()||originalWorld==null||!ReferenceEquals(GameManager.Instance?.World,originalWorld)||
            !(candidate is EntityRebirthNPC npc)||!ReferenceEquals(candidate.world,originalWorld)||candidate.entityId<=0||
            !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)||npc.IsDead()||npc.IsMarkedForUnload())return false;
        var runtime=npc.RebirthRuntimeState;
        if(runtime==null||!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var canonical)||canonical!=candidate.entityId||
            !RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId,out var person)||
            !RebirthNpcPreparedPersonAdmission.TryValidate(person,out var profile,out reason,true)||
            !string.Equals(profile,runtime.ProfileId,StringComparison.OrdinalIgnoreCase)||
            person.HumanAppearance.HasValue&&(!runtime.HasHumanAppearance||!person.HumanAppearance.Value.Equals(runtime.HumanAppearance)))return false;
        string key="spawn:"+replayId;if(!RebirthNpcSpawnReplayCodec.ValidKey(key))return false;
        EnsureInitialized();EnsurePersistenceLoaded();
        lock(Sync)
        {
            if(!IsPreparedContextCurrentNoLock())return false;
            if(worldSnapshotPublicationUncertain)
            {
                if(!EntityClass.list.TryGetValue(candidate.entityClass,out var recoveryClass)||
                    !RebirthNpcCompletionSnapshot.TryLoad(Path.Combine(loadedNativeDirectory,FileName),key,runtime.StableId,
                        profile,recoveryClass.entityClassName,candidate.entityId,person.Presence.EmbodimentGeneration,out var recovered)||
                    !IsPreparedContextCurrentNoLock()||!ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)||npc.IsDead()||npc.IsMarkedForUnload()||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var recoveryCanonical)||recoveryCanonical!=candidate.entityId||
                    !RebirthNpcRuntimeRegistry.TryGet(candidate.entityId,out var recoveryRuntime)||!ReferenceEquals(recoveryRuntime,runtime)||
                    !RebirthNpcAggregatePersistenceStore.TryGet(runtime.StableId,out var currentPerson)||
                    !RebirthNpcPreparedPersonAdmission.TryValidate(currentPerson,out var recoveryProfile,out reason,true)||recoveryProfile!=profile||
                    currentPerson.Presence.EmbodimentGeneration!=person.Presence.EmbodimentGeneration||
                    currentPerson.HumanAppearance.HasValue&&(!runtime.HasHumanAppearance||!currentPerson.HumanAppearance.Value.Equals(runtime.HumanAppearance)))return false;
                var recoveredAmbient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
                foreach(var row in recovered.Identities.Values)if(row.AmbientEntityId>0)recoveredAmbient.Add(row.AmbientEntityId,row);
                ByStable=recovered.Identities;ByAmbient=recoveredAmbient;PendingSpawns=recovered.Pending;Replay=recovered.Replays;Completions=recovered.Completions;
                dirty=false;worldSnapshotPublicationUncertain=false;reason=string.Empty;return true;
            }
            if(Replay.Contains(key))
            {
                if(!Completions.TryGetValue(key,out var binding)||
                    !EntityClass.list.TryGetValue(candidate.entityClass,out var definition)||
                    !binding.Matches(runtime.StableId,profile,definition.entityClassName,candidate.entityId,person.Presence.EmbodimentGeneration)||
                    !HasSavedCompletedSpawn(key,runtime.StableId,candidate.entityId,profile,binding.EntityClass,binding.Generation))
                {reason="Completed key has no qualified original-person binding; automatic retry refused.";return false;}
                reason=string.Empty;return true;
            }
            if(!TryObservePublishedSpawn(replayId,originalWorld,candidate,out var request)||
                !request.IsPublishing||request.StableId!=runtime.StableId.ToString()||request.Profile!=profile)return false;
            if(ByAmbient.TryGetValue(candidate.entityId,out var collision)&&collision.StableId!=runtime.StableId)return false;
            var identities=new Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord>(ByStable);
            var pending=new Dictionary<string,RebirthNpcPendingSpawn>(PendingSpawns,StringComparer.Ordinal);
            var completed=new HashSet<string>(Replay,StringComparer.Ordinal);
            var bindings=new Dictionary<string,RebirthNpcSpawnCompletion>(Completions,StringComparer.Ordinal);
            if(!RebirthNpcSpawnCompletion.TryCreate(request,person.Presence.EmbodimentGeneration,DateTime.UtcNow.Ticks,out var provenance))return false;
            bindings.Add(key,provenance);
            if(completed.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords)return false;
            string display=person.Identity.GeneratedOrAssignedDisplayName;
            if(string.IsNullOrWhiteSpace(display))display=GenerateName(runtime.StableId,RebirthNpcProfileRegistry.ResolveRequired(profile).Category);
            var identity=new RebirthNpcWorldIdentityRecord{StableId=runtime.StableId,AmbientEntityId=candidate.entityId,
                ProfileId=profile,DisplayName=display,PromotedUtcTicks=DateTime.UtcNow.Ticks,Persistent=true};
            identities[runtime.StableId]=identity;pending.Remove(key);completed.Add(key);
            var ambient=new Dictionary<int,RebirthNpcWorldIdentityRecord>();
            foreach(var row in identities.Values)if(row.AmbientEntityId>0)
            {if(ambient.ContainsKey(row.AmbientEntityId))return false;ambient.Add(row.AmbientEntityId,row);}
            try
            {
                WriteSnapshotNoLock(identities,pending,completed,bindings);
                if(!HasSavedCompletedSpawn(key,runtime.StableId,candidate.entityId,profile,request.EntityClass,person.Presence.EmbodimentGeneration)||!IsPreparedContextCurrentNoLock()||
                    !ReferenceEquals(originalWorld.GetEntity(candidate.entityId),candidate)){worldSnapshotPublicationUncertain=true;return false;}
                ByStable=identities;ByAmbient=ambient;PendingSpawns=pending;Replay=completed;Completions=bindings;dirty=false;spawns++;
                reason=string.Empty;return true;
            }
            catch(Exception ex){worldSnapshotPublicationUncertain=!HasSavedPendingSpawn(request);reason="Terminal journal requires save reconciliation: "+ex.GetType().Name;return false;}
        }
    }
    private static bool HasSavedCompletedSpawn(string key,RebirthNpcStableId stable,int nativeId,string profile,string entityClass,uint generation)
    {
        return IsPreparedContextCurrentNoLock()&&RebirthNpcCompletionSnapshot.TryLoad(Path.Combine(loadedNativeDirectory,FileName),
            key,stable,profile,entityClass,nativeId,generation,out _);
    }    // Final-file intent witness only; native execution requires separate live revalidation.
    internal static bool HasSavedPendingSpawn(RebirthNpcPendingSpawn request)
    {
        if(request==null||!IsServer())return false;
        lock(Sync)
        {
            try
            {
                string directory=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(directory))return false;
                string path=Path.Combine(directory,FileName);
                if(!File.Exists(path)||new FileInfo(path).Length>64L*1024L*1024L)return false;
                var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64L*1024L*1024L};
                var document=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(path,settings))document.Load(reader);
                if(!RebirthNpcWorldIdentityLoad.TryRead(document.DocumentElement,out var identities,out var saved,out var completed)||
                    completed.Contains(request.ReplayKey)||!saved.TryGetValue(request.ReplayKey,out var stored))return false;
                return System.Xml.Linq.XNode.DeepEquals(stored.Write(),request.Write());
            }
            catch{return false;}
        }
    }
    public static bool TryComposeSpawn(string replayId,string profileId,Vector3 position,out RebirthNpcStableId stableId,out string reason)
    {
        EnsureInitialized();stableId=default(RebirthNpcStableId);reason="Spawn request is already pending or its result is uncertain.";
        if(!IsServer())return false;
        string key="spawn:"+replayId;
        lock(Sync)if(worldSnapshotPublicationUncertain){reason="World journal uncertainty requires original-person reconciliation.";return false;}
        if(!SpawnAttempts.TryBegin(key,out var lease))return false;
        bool attempted=false,completed=false;
        try{return completed=TryComposeSpawnCore(replayId,profileId,position,out stableId,out reason,out attempted);}
        finally{if(completed||!attempted)SpawnAttempts.ReleaseKnown(key,lease);}
    }
    private static bool TryComposeSpawnCore(string replayId,string profileId,Vector3 position,out RebirthNpcStableId stableId,out string reason,out bool attempted)
    {
        attempted=false; EnsureInitialized(); stableId=default(RebirthNpcStableId); if(!IsServer()){reason="Server authority required.";return false;}
        if(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+replayId)){reason="Valid bounded replay id required.";return false;} RebirthNpcProductionProfile profile;if(!RebirthNpcProductionProfileCatalogue.TryGet(profileId,out profile)){reason="Unknown production profile.";return false;}
        lock(Sync)if(PendingSpawns.ContainsKey("spawn:"+replayId)){reason="Spawn request requires recovery.";return false;}else if(Replay.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords){reason="Spawn replay journal is full.";return false;}else if(Replay.Contains("spawn:"+replayId)){reason="Duplicate spawn request.";return false;}
        if(RebirthNpcWorldIntegrationAdapters.Spawn==null){reason="Native spawn adapter unavailable.";return false;}int entityId;attempted=true;if(!RebirthNpcWorldIntegrationAdapters.Spawn(profile,position,out entityId,out reason)||entityId<=0)return false;
        if(!RebirthNpcSpawnIdentity.TryBind(entityId,profile.ProfileId,out stableId,out reason))return false;
        lock(Sync){Replay.Add("spawn:"+replayId);spawns++;if(profile.Persistent)RegisterIdentityNoLock(entityId,stableId,profile.ProfileId,true);dirty=true;} SaveIfDirty(); reason="Spawn composed.";return true;
    }
    public static bool TryPromoteAmbient(string replayId,int ambientEntityId,out RebirthNpcStableId stableId,out string reason)
    {
        EnsureInitialized();stableId=default(RebirthNpcStableId);if(!IsServer()){reason="Server authority required.";return false;}if(!RebirthNpcSpawnReplayCodec.ValidKey("promote:"+replayId)){reason="Valid bounded replay id required.";return false;}
        lock(Sync){if(worldSnapshotPublicationUncertain){reason="World journal uncertainty requires original-person reconciliation.";return false;}RebirthNpcWorldIdentityRecord existing;if(ByAmbient.TryGetValue(ambientEntityId,out existing)){stableId=existing.StableId;duplicatePromotions++;reason="Ambient entity already owns a durable identity.";return true;}if(Replay.Count>=RebirthNpcSpawnReplayCodec.MaximumRecords){reason="Spawn replay journal is full.";return false;}if(Replay.Contains("promote:"+replayId)){reason="Duplicate promotion request.";return false;}}
        if(RebirthNpcWorldIntegrationAdapters.ResolveAmbient==null){reason="Ambient resolver unavailable.";return false;}string sourceProfile;Vector3 pos;if(!RebirthNpcWorldIntegrationAdapters.ResolveAmbient(ambientEntityId,out sourceProfile,out pos,out reason))return false;
        RebirthNpcProductionProfile source;if(!RebirthNpcProductionProfileCatalogue.TryGet(sourceProfile,out source)||!source.PromotionEligible){reason="Ambient profile is not promotion eligible.";return false;}
        string target=source.Category==RebirthNpcCategory.Survivor?"survivor.persistent":source.ProfileId;RebirthNpcProductionProfile promoted;if(!RebirthNpcProductionProfileCatalogue.TryGet(target,out promoted)||!promoted.Persistent){reason="No persistent promotion profile.";return false;}
        stableId=RebirthNpcStableId.NewId();RebirthNpcRuntimeState runtime;if(RebirthNpcRuntimeRegistry.TryGet(ambientEntityId,out runtime)){if(!RebirthNpcRuntimeRegistry.TryRebindStableId(ambientEntityId,stableId)){reason="Stable identity collision.";return false;}if(!RebirthNpcRuntimeRegistry.TryChangeProfile(ambientEntityId,target)){reason="Persistent profile transition failed.";return false;}}else{try{RebirthNpcRuntimeRegistry.Register(ambientEntityId,target,stableId);}catch(Exception ex){reason=ex.Message;return false;}}
        lock(Sync){RegisterIdentityNoLock(ambientEntityId,stableId,target,true);Replay.Add("promote:"+replayId);promotions++;dirty=true;}SaveIfDirty();reason="Ambient NPC promoted exactly once.";return true;
    }
    private static void RegisterIdentityNoLock(int ambient,RebirthNpcStableId id,string profile,bool persistent){var r=new RebirthNpcWorldIdentityRecord{AmbientEntityId=ambient,StableId=id,ProfileId=profile,DisplayName=GenerateName(id,RebirthNpcProfileRegistry.ResolveRequired(profile).Category),PromotedUtcTicks=DateTime.UtcNow.Ticks,Persistent=persistent};ByAmbient[ambient]=r;ByStable[id]=r;}
    public static string GetDisplayName(RebirthNpcStableId id){EnsureInitialized();lock(Sync){RebirthNpcWorldIdentityRecord r;if(ByStable.TryGetValue(id,out r))return r.DisplayName;}RebirthNpcRuntimeState[] s=RebirthNpcRuntimeRegistry.GetSnapshot();for(int i=0;i<s.Length;i++)if(s[i].StableId==id)return GenerateName(id,RebirthNpcProfileRegistry.ResolveRequired(s[i].ProfileId).Category);return "Unknown NPC";}
    public static bool TrySetDisplayName(RebirthNpcStableId id,string displayName,out string reason)
    {
        EnsureInitialized(); reason=string.Empty;
        if(!IsServer()){reason="Server authority required.";return false;}
        string clean=(displayName??string.Empty).Trim();
        if(clean.Length<1||clean.Length>32){reason="Name must be between 1 and 32 characters.";return false;}
        for(int i=0;i<clean.Length;i++) if(char.IsControl(clean[i])){reason="Name contains invalid characters.";return false;}
        lock(Sync)
        {
            if(worldSnapshotPublicationUncertain){reason="World journal uncertainty requires original-person reconciliation.";return false;}
            RebirthNpcWorldIdentityRecord r;
            if(ByStable.TryGetValue(id,out r)){r.DisplayName=clean;dirty=true;}
            else
            {
                RebirthNpcRuntimeState runtime; int entityId;
                if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out entityId)||!RebirthNpcRuntimeRegistry.TryGet(entityId,out runtime)){reason="NPC identity is not active.";return false;}
                r=new RebirthNpcWorldIdentityRecord{AmbientEntityId=entityId,StableId=id,ProfileId=runtime.ProfileId,DisplayName=clean,PromotedUtcTicks=DateTime.UtcNow.Ticks,Persistent=true};
                ByStable[id]=r;ByAmbient[entityId]=r;dirty=true;
            }
        }
        SaveIfDirty(); reason="Name updated."; return true;
    }
    private static string GenerateName(RebirthNpcStableId id,RebirthNpcCategory c){string[] a=c==RebirthNpcCategory.Bandit?BanditNames:c==RebirthNpcCategory.SpecialHumanoid?SpecialNames:c==RebirthNpcCategory.DogCompanion?DogNames:c==RebirthNpcCategory.PantherCompanion?PantherNames:SurvivorNames;ulong h=id.High^(id.Low*11400714819323198485UL);return a[(int)(h%(ulong)a.Length)];}
    public static bool OnWarmActivated(int entityId,string profileId,RebirthNpcStableId stableId,out string reason){EnsureInitialized();if(stableId.IsEmpty){reason="Stable identity required for warming.";return false;}int existing;if(RebirthNpcRuntimeRegistry.TryGetEntityId(stableId,out existing)){if(existing==entityId){reason="Already warmed.";return true;}reason="Duplicate stable identity is already active.";return false;}try{RebirthNpcRuntimeRegistry.Register(entityId,profileId,stableId);Interlocked.Increment(ref warmActivations);reason="Persistent NPC warmed.";return true;}catch(Exception ex){reason=ex.Message;return false;}}
    public static void OnWarmDeactivated(int entityId){RebirthNpcRuntimeState s;if(RebirthNpcRuntimeRegistry.TryGet(entityId,out s)){RebirthNpcRuntimeRegistry.Unregister(entityId,false);Interlocked.Increment(ref warmDeactivations);}}
    public static bool UpdateMarker(string viewerId,RebirthNpcStableId id,Vector3 position,bool loaded,out string reason)
    {
        EnsureInitialized();RebirthNpcRuntimeState state=null;foreach(var s in RebirthNpcRuntimeRegistry.GetSnapshot())if(s.StableId==id){state=s;break;}if(state==null){reason="NPC not active.";return false;}
        bool authorized=!string.IsNullOrWhiteSpace(viewerId)&&state.ProfileId!=null&&RebirthNpcProfileRegistry.ResolveRequired(state.ProfileId).Has(RebirthNpcCapabilities.NavMarker)&&(string.Equals(viewerId,state.OwnerId,StringComparison.Ordinal)||state.OwnershipKind==RebirthNpcOwnershipKind.Party&&RebirthNpcWorldIntegrationAdapters.IsPartyMember!=null&&RebirthNpcWorldIntegrationAdapters.IsPartyMember(viewerId,state.OwnerId));
        string key=viewerId+":"+id;if(!authorized||!loaded||state.Presence==RebirthNpcPresenceState.Removed||state.Presence==RebirthNpcPresenceState.AwaitingRespawn){return RemoveMarker(key,out reason);}
        var m=new RebirthNpcMarkerProjection{ViewerId=viewerId,StableId=id,DisplayName=GetDisplayName(id),Position=position,Compass=true,Map=true,Revision=state.Revision};if(RebirthNpcWorldIntegrationAdapters.Marker==null){reason="Marker adapter unavailable.";return false;}if(!RebirthNpcWorldIntegrationAdapters.Marker(m,false,out reason))return false;lock(Sync)Markers[key]=m;Interlocked.Increment(ref markerUpdates);return true;
    }
    private static bool RemoveMarker(string key,out string reason){RebirthNpcMarkerProjection old;lock(Sync){if(!Markers.TryGetValue(key,out old)){reason="No marker.";return true;}}if(RebirthNpcWorldIntegrationAdapters.Marker!=null&&!RebirthNpcWorldIntegrationAdapters.Marker(old,true,out reason))return false;lock(Sync)Markers.Remove(key);Interlocked.Increment(ref markerRemovals);reason="Marker removed.";return true;}
    public static void RemoveMarkersForNpc(RebirthNpcStableId id){string[] keys;lock(Sync){var l=new List<string>();foreach(var kv in Markers)if(kv.Value.StableId==id)l.Add(kv.Key);keys=l.ToArray();}for(int i=0;i<keys.Length;i++){string r;RemoveMarker(keys[i],out r);}}
    public static bool PublishPresentation(RebirthNpcStableId id,RebirthNpcPresentationEvent ev,out string reason){RebirthNpcRuntimeState state=null;foreach(var s in RebirthNpcRuntimeRegistry.GetSnapshot())if(s.StableId==id){state=s;break;}if(state==null){reason="NPC not active.";return false;}RebirthNpcProductionProfile p;if(!RebirthNpcProductionProfileCatalogue.TryGet(state.ProfileId,out p)){reason="Profile mapping absent.";return false;}string token=ev.ToString().ToLowerInvariant();if(RebirthNpcWorldIntegrationAdapters.Presentation==null){reason="Presentation adapter unavailable.";return false;}bool ok=RebirthNpcWorldIntegrationAdapters.Presentation(id,p.AnimationSet+"."+token,p.SoundSet+"."+token,out reason);if(ok)Interlocked.Increment(ref presentationEvents);return ok;}
    public static void OnActivityTransition(int entityId,RebirthNpcOrderState order){RebirthNpcRuntimeState s;if(!RebirthNpcRuntimeRegistry.TryGet(entityId,out s))return;RebirthNpcPresentationEvent ev=order==RebirthNpcOrderState.Work?RebirthNpcPresentationEvent.Work:order==RebirthNpcOrderState.Travel?RebirthNpcPresentationEvent.Travel:order==RebirthNpcOrderState.Mission?RebirthNpcPresentationEvent.Mission:RebirthNpcPresentationEvent.Idle;string r;PublishPresentation(s.StableId,ev,out r);}
    public static void OnCombatTransition(RebirthNpcStableId id){string r;PublishPresentation(id,RebirthNpcPresentationEvent.Combat,out r);}
    public static string GetReport(){EnsureInitialized();lock(Sync)return "[REBIRTH NPC ACIP-08 World Integration] profiles="+RebirthNpcProductionProfileCatalogue.Snapshot().Length+" identities="+ByStable.Count+" markers="+Markers.Count+" spawns="+spawns+" promotions="+promotions+" duplicatePromotions="+duplicatePromotions+" warmIn="+warmActivations+" warmOut="+warmDeactivations+" markerUpdates="+markerUpdates+" markerRemovals="+markerRemovals+" presentationEvents="+presentationEvents;}
    public static string Qualify()
    {
        var report=new StringBuilder("[REBIRTH NPC ACIP-08 Qualification]\n");
        string catalogue=RebirthNpcProductionProfileCatalogue.Validate();
        report.AppendLine("production-profile-authoring="+catalogue);
        report.AppendLine("spawn-adapter="+(RebirthNpcWorldIntegrationAdapters.Spawn==null?"UNAVAILABLE":"REGISTERED_NOT_VERIFIED"));
        report.AppendLine("ambient-resolver="+(RebirthNpcWorldIntegrationAdapters.ResolveAmbient==null?"UNAVAILABLE":"REGISTERED_NOT_VERIFIED"));
        report.AppendLine("marker-adapter="+(RebirthNpcWorldIntegrationAdapters.Marker==null?"UNAVAILABLE":"REGISTERED_NOT_VERIFIED"));
        report.AppendLine("presentation-adapter="+(RebirthNpcWorldIntegrationAdapters.Presentation==null?"UNAVAILABLE":"REGISTERED_NOT_VERIFIED"));
        foreach(var profile in RebirthNpcProductionProfileCatalogue.Snapshot())
        {
            Type runtime=typeof(EntityRebirthNPC).Assembly.GetType(profile.EntityClassKey,false);
            string status=runtime==null||!typeof(EntityRebirthNPC).IsAssignableFrom(runtime)?"INVALID_RUNTIME_TYPE":
                runtime.IsAbstract?"ABSTRACT_AUTHORING_BASE":"RUNTIME_TYPE_FOUND_NOT_NATIVE_VERIFIED";
            report.AppendLine("runtime-class:"+profile.ProfileId+"="+status);
        }
        // Catalogue declarations and adapter registration do not execute acceptance tests.
        report.AppendLine("spawn-replay-save-recovery=NOT_VERIFIED");
        report.AppendLine("ambient-promotion-and-warming=NOT_VERIFIED");
        report.AppendLine("authorized-marker-lifecycle=NOT_VERIFIED");
        report.AppendLine("name-and-animation-sound-gameplay=NOT_VERIFIED");
        report.Append("result=NOT_VERIFIED");return report.ToString();
    }
    public static void EnsurePersistenceLoaded()
    {
        if (GameManager.Instance?.World == null || !IsServer()) return;
        lock (Sync) LoadNoLock();
    }
    public static void SaveIfDirty()
    {
        EnsurePersistenceLoaded();
        lock (Sync)
        {
            if (!dirty && !RebirthNpcPersistenceCoordinator.IsCheckpointWrite) return;
            RebirthNpcPersistenceFile.RequireCheckpointLoaded(loaded, Path.Combine(GameIO.GetSaveGameDir(), FileName));
            SaveNoLock(); dirty = false;
        }
    }
    private static void LoadNoLock()
    {
        if (loaded || GameManager.Instance?.World == null) return;
        string dir = GameIO.GetSaveGameDir(); if (string.IsNullOrEmpty(dir)) return;
        string path = Path.Combine(dir, FileName);
        XmlDocument d; string source, error;
        if (!RebirthNpcPersistenceFile.TryLoad(path, doc => doc.DocumentElement != null &&
            doc.DocumentElement.Name == "rebirthNpcWorldIntegration" &&
            (doc.DocumentElement.GetAttribute("version")=="1"||doc.DocumentElement.GetAttribute("version") == SchemaVersion.ToString(CultureInfo.InvariantCulture)),
            out d, out source, out error))
        { RebirthNpcPersistenceFile.AssertWritable(path); loaded = true; loadedNativeWorld=GameManager.Instance.World;loadedNativeDirectory=dir;worldSnapshotPublicationUncertain=false; return; }
        try
        {
            if(!RebirthNpcWorldIdentityLoad.TryRead(d.DocumentElement,out var identities,out var pending,out var replays,out var completions))
                throw new InvalidDataException("NPC world identity snapshot invalid.");
            // Validate the complete snapshot before publishing any of its records.
            ByStable.Clear();ByAmbient.Clear();PendingSpawns.Clear();Replay.Clear();Completions.Clear();
            foreach(var pair in identities){ByStable.Add(pair.Key,pair.Value);if(pair.Value.AmbientEntityId>0)ByAmbient.Add(pair.Value.AmbientEntityId,pair.Value);}
            foreach(var pair in pending)PendingSpawns.Add(pair.Key,pair.Value);
            foreach(var replay in replays)Replay.Add(replay);
            foreach(var completion in completions)Completions.Add(completion.Key,completion.Value);
            loaded=true;loadedNativeWorld=GameManager.Instance.World;loadedNativeDirectory=dir;worldSnapshotPublicationUncertain=false;
        }
        catch (Exception ex) { RebirthNpcPersistenceFile.BlockWrite(path, ex.Message); throw; }
    }
    private static void SaveNoLock()
    {
        if(worldSnapshotPublicationUncertain||!IsPreparedContextCurrentNoLock())throw new IOException("NPC world snapshot context changed; write refused.");
        WriteSnapshotNoLock(ByStable,PendingSpawns,Replay,Completions);
    }
    private static void WriteSnapshotNoLock(Dictionary<RebirthNpcStableId,RebirthNpcWorldIdentityRecord> identities,
        Dictionary<string,RebirthNpcPendingSpawn> pending,HashSet<string> replays,
        Dictionary<string,RebirthNpcSpawnCompletion> completions)
    {
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(directory))throw new IOException("NPC world identity save directory unavailable.");
        Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,FileName),temporary=path+".tmp",backup=path+".bak";
        RebirthNpcPersistenceFile.AssertWritable(path);
        var settings=new XmlWriterSettings{Indent=true,Encoding=new UTF8Encoding(false),CloseOutput=false};
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            using(var writer=XmlWriter.Create(stream,settings))
            {
                writer.WriteStartElement("rebirthNpcWorldIntegration");
                writer.WriteAttributeString("version",SchemaVersion.ToString(CultureInfo.InvariantCulture));
                foreach(var record in identities.Values)
                {
                    writer.WriteStartElement("identity");
                    writer.WriteAttributeString("stableId",record.StableId.ToString());
                    writer.WriteAttributeString("ambientEntityId",record.AmbientEntityId.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("profileId",record.ProfileId);
                    writer.WriteAttributeString("displayName",record.DisplayName);
                    writer.WriteAttributeString("promotedUtcTicks",record.PromotedUtcTicks.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                RebirthNpcSpawnReplayCodec.Write(writer,replays);
                RebirthNpcPendingSpawnPersistence.Write(writer,pending);
                RebirthNpcSpawnCompletion.WriteSection(writer,completions);
                writer.WriteEndElement();writer.Flush();
            }
            stream.Flush(true);
        }
        if(new FileInfo(temporary).Length>64L*1024L*1024L)throw new InvalidDataException("NPC world snapshot exceeds supported bound.");
        var verification=new XmlDocument{XmlResolver=null};verification.Load(temporary);
        if(!RebirthNpcWorldIdentityLoad.TryRead(verification.DocumentElement,out _,out _,out _))throw new InvalidDataException("NPC world identity temporary snapshot invalid.");
        // Never delete or copy over the current final file before the replacement succeeds.
        if(File.Exists(path))File.Replace(temporary,path,backup,true);else File.Move(temporary,path);
    }
    public static void ResetForWorldChange(){SaveIfDirty();lock(Sync){Markers.Clear();Replay.Clear();Completions.Clear();PendingSpawns.Clear();SpawnAttempts.Reset();initialized=false;loaded=false;loadedNativeWorld=null;loadedNativeDirectory=string.Empty;ByAmbient.Clear();ByStable.Clear();}}
    private static bool IsPreparedContextCurrentNoLock()=>loaded&&ReferenceEquals(loadedNativeWorld,GameManager.Instance?.World)&&!string.IsNullOrEmpty(loadedNativeDirectory)&&string.Equals(loadedNativeDirectory,GameIO.GetSaveGameDir(),StringComparison.OrdinalIgnoreCase);
    private static bool IsServer(){return ConnectionManager.Instance==null||ConnectionManager.Instance.IsServer;}
}
