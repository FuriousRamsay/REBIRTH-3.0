using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthBeastmasterSpeciesDefinition
{
    public string CategoryId; public string SourceEntityClass; public string TamedEntityClass; public float SkillRequired; public int CapacityCost; public string KnowledgeId; public int TameInteractions;
}

/// <summary>Chunk-D server authority for Beastmaster initiation and natural-animal taming. PC002 exclusions are checked before every interaction and conversion.</summary>
public static class RebirthBeastmasterService
{
    private static readonly Dictionary<string,RebirthBeastmasterSpeciesDefinition> Species=new Dictionary<string,RebirthBeastmasterSpeciesDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TamedEntityClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool ready; private static float initiationSkill=30f,maxDistance=5f,cooldownSeconds=8f;private static int initiationInteractions=3,baitCount=1,capacityBase=2,capacityStep=15,capacityMax=6;private static string baitItem="foodRawMeat";
    public static bool IsReady=>ready;

    public static string Install(Harmony harmony)
    {
        string report=Load();
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthBeastmasterActivationPatch));
        return report+" patches=1";
    }

    public static string Load()
    {
        Species.Clear();TamedEntityClasses.Clear();ready=false;string path=RebirthAdvancedDisciplineRegistry.SourcePath;XDocument doc=XDocument.Load(path);XElement root=doc.Root, beast=root?.Element("beastmaster");if(beast==null)throw new InvalidDataException("advanced_disciplines.xml is missing beastmaster definitions");
        initiationSkill=F(beast,"initiation_skill",30f);initiationInteractions=Math.Max(2,I(beast,"initiation_interactions",3));cooldownSeconds=Math.Max(1f,F(beast,"interaction_cooldown_seconds",8f));maxDistance=Math.Max(2f,F(beast,"maximum_interaction_distance",5f));baitItem=A(beast,"bait_item");if(baitItem.Length==0)baitItem="foodRawMeat";baitCount=Math.Max(1,I(beast,"bait_count",1));
        XElement tun=root?.Element("tunables")?.Element("beastmaster_animal_capacity");if(tun!=null){capacityBase=Math.Max(1,I(tun,"base_at_skill_30",2));capacityStep=Math.Max(1,I(tun,"skill_step",15));capacityMax=Math.Max(capacityBase,I(tun,"max_capacity",6));}
        foreach(XElement c in beast.Elements("category"))
        {
            string cat=A(c,"id"),knowledge=A(c,"knowledge");float skill=F(c,"skill",0f);int cost=Math.Max(1,I(c,"capacity",1)),interactions=Math.Max(4,I(c,"tame_interactions",4));
            foreach(XElement source in c.Elements("source")){string cls=A(source,"class"),tamed=A(source,"tamed_class");if(cls.Length==0||tamed.Length==0)continue;if(Species.ContainsKey(cls))throw new InvalidDataException("Duplicate Beastmaster source class: "+cls);TamedEntityClasses.Add(tamed);Species[cls]=new RebirthBeastmasterSpeciesDefinition{CategoryId=cat,SourceEntityClass=cls,TamedEntityClass=tamed,SkillRequired=skill,CapacityCost=cost,KnowledgeId=knowledge,TameInteractions=interactions};}
        }
        List<string> errors=ValidateAuthoring();if(errors.Count>0)throw new InvalidDataException("Beastmaster authoring invalid: "+string.Join(" | ",errors.ToArray()));ready=true;return "beastmaster species="+Species.Count+" initiation="+initiationSkill.ToString("0.##",CultureInfo.InvariantCulture)+" animalCapacity="+capacityBase+".."+capacityMax;
    }

    // Presentation classification uses shared authoring, never client-side persistence.
    // Ownership and allowed actions remain validated by the server.
    public static bool IsTamedEntityClass(string entityClassName)
    {
        return ready && !string.IsNullOrEmpty(entityClassName) && TamedEntityClasses.Contains(entityClassName);
    }

    public static bool HasBeastmaster(EntityPlayer player)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||player==null)return false;RebirthWorldCharacterRecord r;if(player.world!=null&&!player.world.IsRemote()&&RebirthWorldCharacterService.TryGet(player,out r)&&r?.Progression!=null)return r.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBeastmaster);
        return RebirthSurvivorClientState.HasProjectedDiscipline(player,RebirthSurvivorIds.DisciplineBeastmaster);
    }

    public static int GetAnimalCapacity(EntityPlayer player)
    { float skill=RebirthAnimalHandlingService.GetPlayerSkill(player);if(!HasBeastmaster(player)||skill<initiationSkill)return 0;int extra=(int)Math.Floor(Math.Max(0f,skill-initiationSkill)/capacityStep);return Math.Min(capacityMax,capacityBase+extra); }

    public static bool TryGetAnimalCapacityUsage(EntityPlayer player,out int used,out int capacity,out int total,out string reason)
    {
        used=0;capacity=GetAnimalCapacity(player);total=0;reason=string.Empty;if(player==null){reason="player unavailable";return false;}string owner;if(!RebirthDogLifecycleService.TryResolveOwnerId(player,out owner)){reason="owner identity unavailable";return false;}
        foreach(RebirthNpcPersistentRecordView r in RebirthNpcAggregatePersistenceStore.SnapshotViews())
        { if(r?.Identity==null||r.Ownership==null||r.Dog==null||!r.Dog.IsTamedWild)continue;if(!string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId??string.Empty,owner,StringComparison.OrdinalIgnoreCase))continue;if(r.Lifecycle!=null&&r.Lifecycle.TombstoneState)continue;if(r.Presence!=null&&string.Equals(r.Presence.PresenceState,RebirthNpcPresenceState.Removed.ToString(),StringComparison.OrdinalIgnoreCase))continue;used+=Math.Max(1,r.Dog.AnimalCapacityCost);total++; }
        return true;
    }

    public static bool IsAdvancedDogTrainingAccomplished(EntityPlayer player)
    {
        string owner;if(player==null||!RebirthDogLifecycleService.TryResolveOwnerId(player,out owner))return false;
        foreach(RebirthNpcPersistentRecordView r in RebirthNpcAggregatePersistenceStore.SnapshotViews())
        { if(r?.Dog==null||r.Dog.IsTamedWild||r.Ownership==null)continue;if(!string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId??string.Empty,owner,StringComparison.OrdinalIgnoreCase))continue;RebirthDogPersistentRecordView d=r.Dog;if(d.Training+0.001f>=50f&&d.Bond+0.001f>=50f&&d.LearnedCommands.Contains(RebirthDogTrainingCommandIds.GuardArea)&&d.LearnedCommands.Contains(RebirthDogTrainingCommandIds.Hunting))return true; }
        return false;
    }

    public static bool CanInteract(EntityAlive animal,EntityPlayer player,out RebirthBeastmasterSpeciesDefinition species,out string reason)
    {
        reason=string.Empty;species=null;if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason="not Rebirth progression";return false;}if(animal==null||player==null||animal.IsDead()){reason="animal/player unavailable";return false;}if((animal.position-player.position).sqrMagnitude>maxDistance*maxDistance){reason="move closer";return false;}EntityClass ec=animal.EntityClass;if(ec==null){reason="entity class unavailable";return false;}string hard;if(RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(ec,out hard)){reason="PC002: "+hard;return false;}if(!Species.TryGetValue(ec.entityClassName??string.Empty,out species)){reason="species not authored for natural taming";return false;}return true;
    }

    public static bool TryInteract(World world,EntityPlayer player,int animalEntityId,out string reason)
    {
        reason=string.Empty;if(world==null||world.IsRemote()||player==null||!RebirthWorldCharacterRepository.IsServerAuthority){reason="server authority required";return false;}EntityAlive animal=world.GetEntity(animalEntityId) as EntityAlive;RebirthBeastmasterSpeciesDefinition species;if(!CanInteract(animal,player,out species,out reason))return false;
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record)){reason="Rebirth character unavailable";return false;}float skill=RebirthAnimalHandlingService.GetPlayerSkill(player);
        bool acquired=record.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBeastmaster);
        if(!acquired)
        {
            if(skill+0.001f<initiationSkill){reason="Animal Handling "+skill.ToString("0.##",CultureInfo.InvariantCulture)+" / "+initiationSkill.ToString("0",CultureInfo.InvariantCulture);return false;}
            if(!RebirthKnowledgeService.HasKnowledge(player,RebirthAnimalHandlingService.KnowledgeAnimalBehavior)){reason="Animal Behavior Knowledge required";return false;}
            if(!IsAdvancedDogTrainingAccomplished(player)){reason="train and bond an owned dog through Guard Area and Hunting first";return false;}
            if(!string.Equals(species.CategoryId,"coyote",StringComparison.OrdinalIgnoreCase)){reason="Beastmaster initiation must be completed with a living coyote";return false;}
            record.Progression.AccomplishmentIds.Add(RebirthSurvivorIds.AccomplishmentDogAdvancedTraining);
            return AdvanceSession(player,identity,record,animal,species,true,initiationInteractions,out reason);
        }
        if(skill+0.001f<species.SkillRequired){reason="Animal Handling "+skill.ToString("0.##",CultureInfo.InvariantCulture)+" / "+species.SkillRequired.ToString("0",CultureInfo.InvariantCulture);return false;}
        if(!string.IsNullOrEmpty(species.KnowledgeId)&&!RebirthKnowledgeService.HasKnowledge(player,species.KnowledgeId)){reason=RebirthKnowledgeService.GetDisplayName(species.KnowledgeId)+" required";return false;}
        return AdvanceSession(player,identity,record,animal,species,false,species.TameInteractions,out reason);
    }

    private static bool AdvanceSession(EntityPlayer player,RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord record,EntityAlive animal,RebirthBeastmasterSpeciesDefinition species,bool initiation,int interactionsNeeded,out string reason)
    {
        string id="entity:"+animal.entityId.ToString(CultureInfo.InvariantCulture);RebirthWildTamingRuntimeState session;long now=DateTime.UtcNow.Ticks;if(!record.Progression.WildTamingSessions.TryGetValue(id,out session)||session==null||!string.Equals(session.SourceEntityClass,species.SourceEntityClass,StringComparison.OrdinalIgnoreCase)||session.InitiationTrial!=initiation)
        {session=new RebirthWildTamingRuntimeState{SessionId=id,TargetEntityId=animal.entityId,SourceEntityClass=species.SourceEntityClass,SpeciesCategory=species.CategoryId,Stage="calm",SuccessfulInteractions=0,LastKnownPosition=animal.position,StartedUtcTicks=now,LastInteractionUtcTicks=0,InitiationTrial=initiation};record.Progression.WildTamingSessions[id]=session;}
        if(session.LastInteractionUtcTicks>0&&new TimeSpan(Math.Max(0L,now-session.LastInteractionUtcTicks)).TotalSeconds<cooldownSeconds){reason="animal needs time to settle before the next interaction";return false;}
        if(!initiation&&session.SuccessfulInteractions+1>=interactionsNeeded&&!CanCompleteTameCapacity(player,species,out reason))return false;
        if(!ConsumeBait(player,baitCount)){reason="requires "+baitCount+" raw meat";return false;}
        animal.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);session.LastKnownPosition=animal.position;session.LastInteractionUtcTicks=now;session.SuccessfulInteractions++;session.Stage=StageFor(session.SuccessfulInteractions,interactionsNeeded);record.Touch((initiation?"beastmaster-initiation":"wild-taming")+":"+species.CategoryId);RebirthWorldCharacterRepository.SaveIfDirty(identity,"beastmaster-interaction");
        if(session.SuccessfulInteractions<interactionsNeeded){reason=(initiation?"Beastmaster trial":"Taming")+" — "+session.Stage+" ("+session.SuccessfulInteractions+"/"+interactionsNeeded+")";RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"beastmaster-interaction");return true;}
        if(initiation)
        {
            string grantReason;if(!RebirthAdvancedDisciplineRegistry.CanAcquireFromRecord(RebirthSurvivorIds.DisciplineBeastmaster,record,RebirthSurvivorIds.TrialBeastmasterInitiation,out grantReason)){reason=grantReason;return false;}
            record.Progression.CompletedTrialIds.Add(RebirthSurvivorIds.TrialBeastmasterInitiation);record.Progression.AcquiredDisciplineIds.Add(RebirthSurvivorIds.DisciplineBeastmaster);record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeBeastmasterPredatorHandling);record.Progression.WildTamingSessions.Remove(id);record.Touch("beastmaster-acquired");RebirthWorldCharacterRepository.SaveIfDirty(identity,"beastmaster-acquired");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"beastmaster-acquired");reason="Beastmaster discipline acquired. Predator Handling learned.";return true;
        }
        return CompleteTame(player,identity,record,animal,species,id,out reason);
    }

    private static bool CanCompleteTameCapacity(EntityPlayer player,RebirthBeastmasterSpeciesDefinition species,out string reason)
    {
        int used,capacity,total;string capReason;if(!TryGetAnimalCapacityUsage(player,out used,out capacity,out total,out capReason)){reason=capReason;return false;}if(used+species.CapacityCost>capacity){reason="Animal Capacity "+used+"/"+capacity+"; this animal costs "+species.CapacityCost;return false;}
        int dogs,totalOwned,dogCap;if(!RebirthDogLifecycleService.TryGetOwnershipCounts(player,out dogs,out totalOwned,out dogCap)){reason="companion capacity unavailable";return false;}if(totalOwned>=RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit){reason=Localization.Get("xuiRebirthCompanionSafetyLimitReached");return false;}
        reason=string.Empty;return true;
    }

    private static bool CompleteTame(EntityPlayer player,RebirthStablePlayerIdentity identity,RebirthWorldCharacterRecord record,EntityAlive animal,RebirthBeastmasterSpeciesDefinition species,string sessionId,out string reason)
    {
        if(!CanCompleteTameCapacity(player,species,out reason))return false;
        int used,capacity,total;string capReason;TryGetAnimalCapacityUsage(player,out used,out capacity,out total,out capReason);
        int classId=EntityClass.FromString(species.TamedEntityClass);EntityClass cls;if(EntityClass.list==null||!EntityClass.list.TryGetValue(classId,out cls)||cls==null){reason="tamed entity class unavailable: "+species.TamedEntityClass;return false;}Vector3 position=animal.position;float yaw=animal.rotation.y;int sourceEntityId=animal.entityId;
        EntityRebirthDogCompanion tame=EntityFactory.CreateEntity(classId,position,new Vector3(0f,yaw,0f)) as EntityRebirthDogCompanion;if(tame==null){reason="failed to create persistent tamed companion";return false;}tame.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);tame.PrepareNewDogDeployment();player.world.SpawnEntityInWorld(tame);bool committed=false;
        try
        {
            string owner;if(!RebirthDogLifecycleService.TryResolveOwnerId(player,out owner)){reason="owner identity unavailable";return false;}RebirthNpcTransactionResult own=tame.SetRebirthOwner(RebirthNpcOwnershipKind.Player,owner);if(!own.Succeeded){reason=own.Error;return false;}RebirthNpcTransactionResult order=tame.SetRebirthOrder(RebirthNpcOrderState.Follow,Vector3.zero,false);if(!order.Succeeded){reason=order.Error;return false;}
            if (RebirthDogStateService.EnsureView(tame) == null)
            { reason = "persistent companion state unavailable"; return false; }
            if (!RebirthNpcAggregatePersistenceStore.Mutate(tame.RebirthRuntimeState.StableId,
                delegate(RebirthNpcPersistentRecord aggregate)
                {
                    RebirthDogPersistentRecord d = aggregate.Dog;
                    if (d == null) throw new InvalidOperationException("tamed state missing after initialization");
                    d.IsTamedWild = true;
                    d.SourceEntityClass = species.SourceEntityClass; d.TamedEntityClass = species.TamedEntityClass;
                    d.SpeciesCategory = species.CategoryId; d.AnimalCapacityCost = species.CapacityCost;
                    d.BreedId = "wild:" + species.CategoryId; d.Training = 10f; d.Bond = 30f;
                    d.LegacyCommandGrandfathered = false; d.TamedUtcTicks = DateTime.UtcNow.Ticks;
                    d.LastCareUtcTicks = d.TamedUtcTicks; d.Lifecycle = RebirthDogLifecycleKind.Active;
                    aggregate.Identity.Species = species.CategoryId;
                    unchecked { d.Revision++; }
                })) { reason = "persistent companion state unavailable"; return false; }
            RebirthDogRuntimeService.ApplyProgressionCvars(tame);RebirthDogStateService.CaptureRuntime(tame);RebirthNpcAggregatePersistenceStore.Save();RebirthNpcStableIdentityStore.Save();
            record.Progression.WildTamingSessions.Remove(sessionId);if(string.Equals(species.CategoryId,"coyote",StringComparison.OrdinalIgnoreCase))record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeBeastmasterPackBonding);if(string.Equals(species.CategoryId,"wolf",StringComparison.OrdinalIgnoreCase)||string.Equals(species.CategoryId,"advanced_wolf",StringComparison.OrdinalIgnoreCase))record.Progression.KnowledgeIds.Add(RebirthSurvivorIds.KnowledgeBeastmasterApexPredatorHandling);record.Touch("wild-tame-complete:"+species.CategoryId);RebirthWorldCharacterRepository.SaveIfDirty(identity,"wild-tame-complete");RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"wild-tame-complete");
            player.world.RemoveEntity(sourceEntityId,EnumRemoveEntityReason.Despawned);RebirthDogCapacitySyncService.NotifyOwner(player);committed=true;reason="Tamed "+species.CategoryId+". Animal Capacity "+(used+species.CapacityCost)+"/"+capacity+".";return true;
        }
        finally{if(!committed&&tame!=null)player.world.RemoveEntity(tame.entityId,EnumRemoveEntityReason.Despawned);}
    }

    public static bool TryCare(EntityPlayer player, EntityRebirthDogCompanion animal, out string reason)
    {
        reason = string.Empty;
        if (player == null || animal == null || player.world == null || player.world.IsRemote())
        { reason = "server authority required"; return false; }
        if ((animal.position - player.position).sqrMagnitude > maxDistance * maxDistance)
        { reason = "move closer"; return false; }
        RebirthNpcPersistentRecordView view; RebirthDogPersistentRecordView dog;
        if (animal.RebirthRuntimeState == null ||
            !RebirthDogStateService.TryGetView(animal.RebirthRuntimeState.StableId, out view, out dog) ||
            !dog.IsTamedWild)
        { reason = "not a tamed wild animal"; return false; }
        string owner;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(player, out owner) || view.Ownership == null ||
            !string.Equals(owner, view.Ownership.OwnerPlatformIdOrPersistentPlayerId, StringComparison.OrdinalIgnoreCase))
        { reason = "not your animal"; return false; }

        if (dog.Bond >= 100f)
        { reason = Localization.Get("xuiRebirthBeastmasterCareBondFull"); return false; }

        string failure = "animal state changed; retry care";
        float committedBond = 0f;
        bool committed = RebirthNpcAggregatePersistenceStore.TryMutateExisting(
            animal.RebirthRuntimeState.StableId, view.AggregateRevision, delegate(RebirthNpcPersistentRecord record)
            {
                // Recheck ownership/revision under store ownership before consuming bait.
                if (!RebirthDogStateService.IsTamedWild(record) || record.Ownership == null ||
                    !string.Equals(owner, record.Ownership.OwnerPlatformIdOrPersistentPlayerId, StringComparison.OrdinalIgnoreCase))
                { failure = "not your tamed animal"; return false; }
                if (record.Dog.Bond >= 100f)
                { failure = Localization.Get("xuiRebirthBeastmasterCareBondFull"); return false; }
                if (!ConsumeBait(player, 1)) { failure = "requires raw meat"; return false; }
                record.Dog.Bond = Mathf.Clamp(record.Dog.Bond + 1f, 0f, 100f);
                record.Dog.LastCareUtcTicks = DateTime.UtcNow.Ticks;
                unchecked { record.Dog.Revision++; }
                committedBond = record.Dog.Bond;
                return true;
            });
        if (!committed) { reason = failure; return false; }
        // This retains the existing native bait/save boundary; it is not a cross-store
        // crash transaction or a claim that native inventory consumption was tested.
        RebirthNpcAggregatePersistenceStore.Save();
        reason = "Care completed. Bond " + committedBond.ToString("0", CultureInfo.InvariantCulture) + "/100.";
        return true;
    }

    public static string BuildDebugSummary(EntityPlayer player,int animalId)
    { Ensure();StringBuilder b=new StringBuilder();int used=0,cap=GetAnimalCapacity(player),wildCount=0;string cr;TryGetAnimalCapacityUsage(player,out used,out cap,out wildCount,out cr);int dogCount=0,totalOwned=0,dogCap=0;RebirthDogLifecycleService.TryGetOwnershipCounts(player,out dogCount,out totalOwned,out dogCap);b.Append("[REBIRTH Beastmaster] acquired=").Append(HasBeastmaster(player)).Append(" skill=").Append(RebirthAnimalHandlingService.GetPlayerSkill(player).ToString("0.##",CultureInfo.InvariantCulture)).Append(" dogTrialReady=").Append(IsAdvancedDogTrainingAccomplished(player)).Append(" animalCapacity=").Append(used).Append('/').Append(cap).Append(" wildTames=").Append(wildCount).Append(" totalSafety=").Append(totalOwned).Append('/').Append(RebirthAdvancedDisciplineRegistry.GlobalCompanionSafetyLimit).Append(" dogSlots=").Append(dogCount).Append('/').Append(dogCap).Append(" species=").Append(Species.Count);if(animalId>0&&player?.world!=null){EntityAlive a=player.world.GetEntity(animalId) as EntityAlive;RebirthBeastmasterSpeciesDefinition s;string r;bool ok=CanInteract(a,player,out s,out r);b.Append("\n  animal=").Append(animalId).Append(" class=").Append(a?.EntityClass?.entityClassName??"<none>").Append(" eligible=").Append(ok).Append(" category=").Append(s?.CategoryId??"none").Append(" reason=").Append(r);}return b.ToString(); }
    public static string RunVectors(){Ensure();List<string> e=ValidateAuthoring();string r;string[] zombieAnimals={"animalZombieDog","animalZombieDog2","animalDireWolf","animalZombieBear","animalZombieBoar","animalBossGrace","animalZombieVulture","animalZombieVultureRadiated"};for(int i=0;i<zombieAnimals.Length;i++)if(!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(zombieAnimals[i],null,out r))e.Add("PC002 zombie-animal exclusion missing: "+zombieAnimals[i]);if(!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("futureAnimal",new string[]{"zombieAnimal"},out r))e.Add("PC002 zombieAnimal-tag exclusion missing");foreach(var p in Species){RebirthBlackMagicZombieAnimalDefinition zombieAnimalDef;if(RebirthBlackMagicTargetClassifier.TryGetZombieAnimalDefinition(p.Key,out zombieAnimalDef)||p.Key.IndexOf("Zombie",StringComparison.OrdinalIgnoreCase)>=0||p.Value.TamedEntityClass.IndexOf("Zombie",StringComparison.OrdinalIgnoreCase)>=0)e.Add("zombie class leaked into Beastmaster species: "+p.Key);}return "[REBIRTH Beastmaster] vectors="+(e.Count==0?"PASS":"FAIL")+" failures="+e.Count+(e.Count==0?string.Empty:" "+string.Join(" | ",e.ToArray()));}

    private static List<string> ValidateAuthoring(){List<string> e=new List<string>();if(Species.Count<5)e.Add("natural species roster too small");foreach(var p in Species){if(p.Value.CapacityCost<1)e.Add("invalid capacity: "+p.Key);if(p.Value.SourceEntityClass.StartsWith("animalZombie",StringComparison.OrdinalIgnoreCase))e.Add("PC002 source exclusion violated: "+p.Key);}return e;}
    private static bool ConsumeBait(EntityPlayer p,int count){ItemValue v=ItemClass.GetItem(baitItem);if(v.IsEmpty())return false;int inv=p.inventory!=null?p.inventory.GetItemCount(v,false,-1,-1):0,bag=p.bag!=null?p.bag.GetItemCount(v,-1,-1,false):0;if(inv+bag<count)return false;int remain=count;if(p.inventory!=null&&inv>0){int n=Math.Min(remain,inv);p.inventory.DecItem(v,n,false);remain-=n;}if(remain>0&&p.bag!=null)p.bag.DecItem(v,remain,false);return true;}
    private static string StageFor(int n,int total){float p=total<=0?1f:(float)n/total;if(p<0.26f)return "Calm";if(p<0.51f)return "Acclimate";if(p<0.99f)return "Bond";return "Tame";}
    private static void Ensure(){if(!ready)Load();}
    private static string A(XElement e,string n)=>e==null?string.Empty:((string)e.Attribute(n)??string.Empty).Trim();private static int I(XElement e,string n,int d){int v;return int.TryParse(A(e,n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}private static float F(XElement e,string n,float d){float v;return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:d;}
}

[HarmonyPatch]
public static class RebirthBeastmasterActivationPatch
{
    [HarmonyPostfix][HarmonyPatch(typeof(EntityAlive), "InitLocalActivationCommands")]
    private static void ActivationCommands(Entity __instance,Action<EntityActivationCommand> __0)
    {
        EntityAlive animal=__instance as EntityAlive;if(animal==null||__0==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;EntityPlayerLocal player=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;if(player==null)return;RebirthBeastmasterSpeciesDefinition species;string reason;if(RebirthBeastmasterService.CanInteract(animal,player,out species,out reason))__0(new EntityActivationCommand("rebirthBeastmasterInteract","hand",null,"rebirthBeastmasterInteract"));
    }

    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive), "OnEntityActivated", new Type[]{typeof(EntityActivationCommand),typeof(EntityPlayerLocal)})]
    private static bool Activated(Entity __instance,EntityActivationCommand __0,EntityPlayerLocal __1)
    {
        EntityActivationCommand command = __0; EntityPlayerLocal focusingPlayer = __1;
        if(command.commandId!="rebirthBeastmasterInteract"||focusingPlayer==null)return true;PersistentPlayerData pp=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(focusingPlayer.entityId);if(pp?.PrimaryId==null)return false;World world=GameManager.Instance.World;if(world==null)return false;if(!world.IsRemote()){string r;bool ok=RebirthBeastmasterService.TryInteract(world,focusingPlayer,__instance.entityId,out r);if(!string.IsNullOrEmpty(r))GameManager.ShowTooltipMP(focusingPlayer,r,ok?"ui_success":"ui_denied");}else SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthBeastmasterInteractionRequest>().Setup(focusingPlayer.entityId,pp.PrimaryId,__instance.entityId,false));return false;
    }
}

[Preserve]
public sealed class NetPackageRebirthBeastmasterInteractionRequest:NetPackage
{
    private int playerEntityId,animalEntityId;private PlatformUserIdentifierAbs userId;private bool care;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthBeastmasterInteractionRequest Setup(int p,PlatformUserIdentifierAbs u,int a,bool c){playerEntityId=p;userId=u;animalEntityId=a;care=c;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;playerEntityId=b.ReadInt32();userId=PlatformUserIdentifierAbs.FromStream(b);animalEntityId=b.ReadInt32();care=b.ReadBoolean();}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(playerEntityId);userId.ToStream(b);b.Write(animalEntityId);b.Write(care);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world==null||world.IsRemote()||userId==null||!ValidEntityIdForSender(playerEntityId)||!ValidUserIdForSender(userId))return;EntityPlayer p=world.GetEntity(playerEntityId) as EntityPlayer;PersistentPlayerData pp=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);if(p==null||pp?.PrimaryId==null||!pp.PrimaryId.Equals(userId))return;string r;bool ok;if(care)ok=RebirthBeastmasterService.TryCare(p,world.GetEntity(animalEntityId) as EntityRebirthDogCompanion,out r);else ok=RebirthBeastmasterService.TryInteract(world,p,animalEntityId,out r);if(!string.IsNullOrEmpty(r))GameManager.ShowTooltipMP(p,r,ok?"ui_success":"ui_denied");}
    public int GetLength()=>0;
}
