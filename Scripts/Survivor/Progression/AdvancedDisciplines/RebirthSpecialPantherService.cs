using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public sealed class RebirthSpecialPantherRouteDefinition
{
    public string Id=string.Empty;
    public string EntityClass=string.Empty;
    public string DisciplineId=string.Empty;
    public string DisciplineSkillId=string.Empty;
    public float DisciplineSkillMinimum;
    public string AnimalKnowledgeId=string.Empty;
    public string DisciplineKnowledgeId=string.Empty;
    public string EffectId=string.Empty;
}

/// <summary>Chunk-I authority for the Witch Doctor Horror Panther and Berserker Rage Panther cross-discipline routes.</summary>
public static class RebirthSpecialPantherService
{
    public const string WitchDoctorRoute="witch_doctor";
    public const string BerserkerRoute="berserker";
    public const string HorrorPantherClass="FuriousRamsayNPCHorrorPanther";
    public const string RagePantherClass="FuriousRamsayNPCRagePanther";
    private static readonly Dictionary<string,RebirthSpecialPantherRouteDefinition> Routes=new Dictionary<string,RebirthSpecialPantherRouteDefinition>(StringComparer.OrdinalIgnoreCase);
    private static bool ready;
    private static float minimumAnimalHandling=25f;
    private static string infectedAnimalHandlingKnowledgeId=RebirthSurvivorIds.KnowledgeInfectedAnimalBehavior;
    private static int capacityCost=2,maximumOwnedPerRoute=1,animalCapacityBase=2,animalCapacityStep=15,animalCapacityMax=6;

    public static string Install(){return Load();}
    public static string Load()
    {
        Routes.Clear();ready=false;XDocument doc=XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath);XElement root=doc.Root;XElement sp=root?.Element("special_panthers");if(sp==null)throw new InvalidDataException("advanced_disciplines.xml missing special_panthers");
        minimumAnimalHandling=F(sp,"minimum_animal_handling",25f);capacityCost=Math.Max(1,I(sp,"capacity_cost",2));maximumOwnedPerRoute=Math.Max(1,I(sp,"maximum_owned_per_route",1));
        XElement tun=root?.Element("tunables")?.Element("beastmaster_animal_capacity");if(tun!=null){animalCapacityBase=Math.Max(1,I(tun,"base_at_skill_30",2));animalCapacityStep=Math.Max(1,I(tun,"skill_step",15));animalCapacityMax=Math.Max(animalCapacityBase,I(tun,"max_capacity",6));}
        XElement infected=root?.Element("black_magic_target_classification")?.Element("zombie_animals");if(infected!=null){string kid=A(infected,"binding_knowledge");if(kid.Length>0)infectedAnimalHandlingKnowledgeId=kid;}
        foreach(XElement e in sp.Elements("route")){var d=new RebirthSpecialPantherRouteDefinition{Id=A(e,"id"),EntityClass=A(e,"entity_class"),DisciplineId=A(e,"discipline"),DisciplineSkillId=A(e,"discipline_skill"),DisciplineSkillMinimum=F(e,"discipline_skill_minimum",50f),AnimalKnowledgeId=A(e,"animal_knowledge"),DisciplineKnowledgeId=A(e,"discipline_knowledge"),EffectId=A(e,"effect")};if(d.Id.Length>0)Routes[d.Id]=d;}
        List<string> errors=ValidateAuthoring();if(errors.Count>0)throw new InvalidDataException("Special Panther authoring invalid: "+string.Join(" | ",errors.ToArray()));ready=true;return "specialPanthers routes="+Routes.Count+" AH="+minimumAnimalHandling.ToString("0",CultureInfo.InvariantCulture)+" cost="+capacityCost;
    }

    public static bool TryGetRoute(string id,out RebirthSpecialPantherRouteDefinition route){Ensure();return Routes.TryGetValue(id??string.Empty,out route);}
    public static int GetCrossDisciplineAnimalCapacity(EntityPlayer player){float ah=RebirthAnimalHandlingService.GetPlayerSkill(player);if(ah+0.001f<minimumAnimalHandling)return 0;int extra=(int)Math.Floor(Math.Max(0f,ah-30f)/animalCapacityStep);return Math.Min(animalCapacityMax,animalCapacityBase+Math.Max(0,extra));}

    public static bool CanDeploy(EntityPlayer player,string routeId,out string reason)
    {
        reason=string.Empty;Ensure();if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason="not Rebirth progression";return false;}if(player==null){reason="player unavailable";return false;}RebirthSpecialPantherRouteDefinition route;if(!Routes.TryGetValue(routeId??string.Empty,out route)){reason="unknown special panther route";return false;}
        if(!HasDiscipline(player,route.DisciplineId)){reason="required Advanced Discipline not acquired: "+route.DisciplineId;return false;}
        float ah=RebirthAnimalHandlingService.GetPlayerSkill(player);if(ah+0.001f<minimumAnimalHandling){reason="Animal Handling "+ah.ToString("0.##",CultureInfo.InvariantCulture)+" / "+minimumAnimalHandling.ToString("0",CultureInfo.InvariantCulture);return false;}
        float disciplineSkill;if(!TryGetSkillValue(player,route.DisciplineSkillId,out disciplineSkill)||disciplineSkill+0.001f<route.DisciplineSkillMinimum){reason=route.DisciplineSkillId+" "+disciplineSkill.ToString("0.##",CultureInfo.InvariantCulture)+" / "+route.DisciplineSkillMinimum.ToString("0",CultureInfo.InvariantCulture);return false;}
        if(!RebirthKnowledgeService.HasKnowledge(player,route.AnimalKnowledgeId)){reason=RebirthKnowledgeService.GetDisplayName(route.AnimalKnowledgeId)+" required";return false;}if(!RebirthKnowledgeService.HasKnowledge(player,route.DisciplineKnowledgeId)){reason=RebirthKnowledgeService.GetDisplayName(route.DisciplineKnowledgeId)+" required";return false;}
        if(player.world!=null&&player.world.IsRemote())return true;
        string owner;if(!RebirthDogLifecycleService.TryResolveOwnerId(player,out owner)){reason="owner identity unavailable";return false;}int ownedRoute=CountOwnedRoute(owner,route.Id);if(ownedRoute>=maximumOwnedPerRoute){reason="maximum owned "+route.Id+" special panthers reached";return false;}
        int used=CountAnimalCapacity(owner),cap=GetCrossDisciplineAnimalCapacity(player);if(used+capacityCost>cap){reason="Animal Capacity "+used+" + "+capacityCost+" / "+cap;return false;}
        int dogs,total,globalCap;if(!RebirthDogLifecycleService.TryGetOwnershipCounts(player,out dogs,out total,out globalCap)){reason="global companion capacity unavailable";return false;}if(total>=globalCap){reason="global companion safety limit "+total+"/"+globalCap;return false;}return true;
    }

    public static bool TryDeploy(World world,EntityPlayer player,string routeId,out string reason)
    {
        reason=string.Empty;if(world==null||world.IsRemote()||player==null||!RebirthWorldCharacterRepository.IsServerAuthority){reason="server authority required";return false;}if(!CanDeploy(player,routeId,out reason))return false;RebirthSpecialPantherRouteDefinition route=Routes[routeId];
        int classId=EntityClass.FromString(route.EntityClass);EntityClass cls;if(EntityClass.list==null||!EntityClass.list.TryGetValue(classId,out cls)||cls==null){reason="special panther entity class unavailable: "+route.EntityClass;return false;}
        Vector3 pos=player.position+new Vector3(1.5f,.1f,1.5f);EntityRebirthDogCompanion panther=EntityFactory.CreateEntity(classId,pos,new Vector3(0f,player.rotation.y,0f)) as EntityRebirthDogCompanion;if(panther==null){reason="failed to create persistent special panther";return false;}panther.SetSpawnerSource(EnumSpawnerSource.StaticSpawner);panther.PrepareNewDogDeployment();world.SpawnEntityInWorld(panther);bool committed=false;
        try{string owner;if(!RebirthDogLifecycleService.TryResolveOwnerId(player,out owner)){reason="owner identity unavailable";return false;}RebirthNpcTransactionResult own=panther.SetRebirthOwner(RebirthNpcOwnershipKind.Player,owner);if(!own.Succeeded){reason=own.Error;return false;}RebirthNpcTransactionResult order=panther.SetRebirthOrder(RebirthNpcOrderState.Follow,Vector3.zero,false);if(!order.Succeeded){reason=order.Error;return false;}if (RebirthDogStateService.EnsureView(panther) == null)
            { reason = "persistent companion state unavailable"; return false; }
            if (!RebirthNpcAggregatePersistenceStore.Mutate(panther.RebirthRuntimeState.StableId,
                delegate(RebirthNpcPersistentRecord aggregate)
                {
                    RebirthDogPersistentRecord d = aggregate.Dog;
                    if (d == null) throw new InvalidOperationException("panther state missing after initialization");
                    d.IsTamedWild = true; d.SourceEntityClass = route.EntityClass; d.TamedEntityClass = route.EntityClass;
                    d.SpeciesCategory = "special_panther"; d.AnimalCapacityCost = capacityCost;
                    d.BreedId = "special-panther:" + route.Id; d.Training = 25f; d.Bond = 40f;
                    d.LegacyCommandGrandfathered = false; d.TamedUtcTicks = DateTime.UtcNow.Ticks;
                    d.LastCareUtcTicks = d.TamedUtcTicks; d.Lifecycle = RebirthDogLifecycleKind.Active;
                    aggregate.Identity.Species = "special_panther:" + route.Id;
                    unchecked { d.Revision++; }
                })) { reason = "persistent companion state unavailable"; return false; }
            RebirthDogRuntimeService.ApplyProgressionCvars(panther);RebirthDogStateService.CaptureRuntime(panther);RebirthNpcAggregatePersistenceStore.Save();RebirthNpcStableIdentityStore.Save();RebirthDogCapacitySyncService.NotifyOwner(player);committed=true;reason=(route.Id==WitchDoctorRoute?"Horror Panther":"Rage Panther")+" deployed.";return true;}finally{if(!committed&&panther!=null)world.RemoveEntity(panther.entityId,EnumRemoveEntityReason.Despawned);}
    }

    public static bool TryGetOwner(EntityRebirthDogCompanion panther,out EntityPlayer owner)
    {
        owner=null;if(panther==null||panther.world==null||panther.RebirthRuntimeState==null)return false;RebirthNpcPersistentRecordView r;RebirthDogPersistentRecordView d;if(!RebirthDogStateService.TryGetView(panther.RebirthRuntimeState.StableId,out r,out d)||r?.Ownership==null||d==null||!d.BreedId.StartsWith("special-panther:",StringComparison.OrdinalIgnoreCase))return false;owner=FindOwnerPlayer(panther.world,r.Ownership.OwnerPlatformIdOrPersistentPlayerId);return owner!=null;
    }

    public static bool ValidateInfectedAnimalBindingHandling(EntityAlive target,EntityPlayer player,out string reason)
    {
        reason=string.Empty;if(target==null||target.EntityClass==null)return true;RebirthBlackMagicZombieAnimalDefinition animal;if(!RebirthBlackMagicTargetClassifier.TryGetZombieAnimalDefinition(target.EntityClass.entityClassName??string.Empty,out animal))return true;if(!animal.BlackMagicEligible||!animal.BindingCandidate||animal.Protected){reason="This infected animal is not an authored Black Magic binding candidate.";return false;}float ah=RebirthAnimalHandlingService.GetPlayerSkill(player);if(ah+0.001f<minimumAnimalHandling){reason="Infected-animal binding requires Animal Handling "+minimumAnimalHandling.ToString("0",CultureInfo.InvariantCulture)+"; current "+ah.ToString("0.##",CultureInfo.InvariantCulture)+". Beastmaster is not required.";return false;}if(!RebirthKnowledgeService.HasKnowledge(player,infectedAnimalHandlingKnowledgeId)){reason=RebirthKnowledgeService.GetDisplayName(infectedAnimalHandlingKnowledgeId)+" required. Beastmaster still cannot tame infected animals.";return false;}return true;
    }

    public static string BuildStatus(EntityPlayer player)
    {
        Ensure();StringBuilder b=new StringBuilder("[REBIRTH Special Panthers]");string owner;RebirthDogLifecycleService.TryResolveOwnerId(player,out owner);b.Append(" AH=").Append(RebirthAnimalHandlingService.GetPlayerSkill(player).ToString("0.##",CultureInfo.InvariantCulture)).Append(" animalCapacity=").Append(CountAnimalCapacity(owner)).Append('/').Append(GetCrossDisciplineAnimalCapacity(player));foreach(var p in Routes){string r;bool ok=CanDeploy(player,p.Key,out r);b.Append("\n  ").Append(p.Key).Append(" class=").Append(p.Value.EntityClass).Append(" owned=").Append(CountOwnedRoute(owner,p.Key)).Append('/').Append(maximumOwnedPerRoute).Append(" eligible=").Append(ok).Append(" reason=").Append(r);}return b.ToString();
    }
    public static string RunVectors(){Ensure();List<string> e=ValidateAuthoring();if(!Routes.ContainsKey(WitchDoctorRoute)||Routes[WitchDoctorRoute].EntityClass!=HorrorPantherClass)e.Add("Witch Doctor class identity mismatch");if(!Routes.ContainsKey(BerserkerRoute)||Routes[BerserkerRoute].EntityClass!=RagePantherClass)e.Add("Berserker class identity mismatch");if(minimumAnimalHandling<20f)e.Add("special panther Animal Handling gate unexpectedly low");foreach(var p in Routes)if(p.Value.AnimalKnowledgeId!=RebirthSurvivorIds.KnowledgePantherHandling)e.Add("Panther Handling missing: "+p.Key);return "[REBIRTH Special Panthers] vectors="+(e.Count==0?"PASS":"FAIL")+" failures="+e.Count+(e.Count==0?string.Empty:" "+string.Join(" | ",e.ToArray()));}



    private static bool HasDiscipline(EntityPlayer player,string disciplineId)
    {
        if(player==null||string.IsNullOrEmpty(disciplineId))return false;
        if(player.world!=null&&player.world.IsRemote())return RebirthSurvivorClientState.HasProjectedDiscipline(player,disciplineId);
        RebirthWorldCharacterRecord r;return RebirthWorldCharacterService.TryGet(player,out r)&&r?.Progression!=null&&r.Progression.AcquiredDisciplineIds.Contains(disciplineId);
    }

    private static bool TryGetSkillValue(EntityPlayer player,string skillId,out float value)
    {
        value=0f;if(player==null||string.IsNullOrEmpty(skillId))return false;
        return RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,skillId,out value);
    }

    private static int CountOwnedRoute(string owner,string route){if(string.IsNullOrEmpty(owner))return 0;int n=0;foreach(RebirthNpcPersistentRecordView r in RebirthNpcAggregatePersistenceStore.SnapshotViews()){if(r?.Dog==null||r.Ownership==null)continue;if(!string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId??string.Empty,owner,StringComparison.OrdinalIgnoreCase))continue;if(!string.Equals(r.Dog.BreedId,"special-panther:"+route,StringComparison.OrdinalIgnoreCase))continue;if(IsActive(r))n++;}return n;}
    private static int CountAnimalCapacity(string owner){if(string.IsNullOrEmpty(owner))return 0;int n=0;foreach(RebirthNpcPersistentRecordView r in RebirthNpcAggregatePersistenceStore.SnapshotViews()){if(r?.Dog==null||r.Ownership==null||!r.Dog.IsTamedWild)continue;if(!string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId??string.Empty,owner,StringComparison.OrdinalIgnoreCase))continue;if(IsActive(r))n+=Math.Max(1,r.Dog.AnimalCapacityCost);}return n;}
    private static bool IsActive(RebirthNpcPersistentRecordView r){if(r.Lifecycle!=null&&r.Lifecycle.TombstoneState)return false;if(r.Presence!=null&&string.Equals(r.Presence.PresenceState,RebirthNpcPresenceState.Removed.ToString(),StringComparison.OrdinalIgnoreCase))return false;return r.Dog==null||r.Dog.Lifecycle!=RebirthDogLifecycleKind.Removed;}
    private static EntityPlayer FindOwnerPlayer(World world,string ownerId){if(world==null||world.Players==null||world.Players.list==null||string.IsNullOrEmpty(ownerId))return null;for(int i=0;i<world.Players.list.Count;i++){EntityPlayer p=world.Players.list[i];if(p==null)continue;PersistentPlayerData pp=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(p.entityId);if(pp?.PrimaryId!=null&&string.Equals(pp.PrimaryId.ToString(),ownerId,StringComparison.OrdinalIgnoreCase))return p;}return null;}
    private static List<string> ValidateAuthoring(){List<string> e=new List<string>();if(Routes.Count!=2)e.Add("expected exactly two special panther routes");foreach(var p in Routes){if(p.Value.EntityClass.Length==0||p.Value.DisciplineId.Length==0||p.Value.DisciplineSkillId.Length==0)e.Add("incomplete route: "+p.Key);if(p.Value.AnimalKnowledgeId.Length==0||p.Value.DisciplineKnowledgeId.Length==0)e.Add("missing knowledge: "+p.Key);}return e;}
    private static void Ensure(){if(!ready)Load();}private static string A(XElement e,string n)=>e==null?string.Empty:((string)e.Attribute(n)??string.Empty).Trim();private static int I(XElement e,string n,int d){int v;return int.TryParse(A(e,n),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:d;}private static float F(XElement e,string n,float d){float v;return float.TryParse(A(e,n),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:d;}
}

[Preserve]
public sealed class MinEventActionRebirthSpecialPantherAssist : MinEventActionBase
{
    private const float AssistChance=.20f;
    private static readonly Dictionary<long,float> LastAttempt=new Dictionary<long,float>();
    public override void Execute(MinEventParams p)
    {
        if(!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer||p==null)return;EntityRebirthDogCompanion panther=p.Self as EntityRebirthDogCompanion;EntityAlive target=p.Other;if(panther==null||target==null||target.IsDead()||panther.EntityClass==null||!string.Equals(panther.EntityClass.entityClassName,RebirthSpecialPantherService.HorrorPantherClass,StringComparison.Ordinal))return;EntityPlayer owner;if(!RebirthSpecialPantherService.TryGetOwner(panther,out owner)||owner==null)return;long key=((long)panther.entityId<<32)^(uint)target.entityId;float now=Time.realtimeSinceStartup;float last;if(LastAttempt.TryGetValue(key,out last)&&now-last<5f)return;LastAttempt[key]=now;if(panther.world==null||panther.world.GetGameRandom().RandomFloat>=AssistChance)return;string reason;RebirthBlackMagicService.TrySpecialPantherDominate(panther.world,owner,target,out reason);
    }
}
