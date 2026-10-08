using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// PC022 / Chunk I runtime consumers for Soldier, Police Officer, Hunter and Park Ranger.
/// Persistent/reward-affecting decisions are server-authoritative. Client state is presentation only.
/// </summary>
public static class RebirthCombatPatrolTrackingSignatureService
{
    public const string CombatMomentumBonusId="background_bonus.combat_momentum";
    public const string PatrolCarBonusId="background_bonus.patrol_car_familiarity";
    public const string HuntersMarkBonusId="background_bonus.hunters_mark";
    public const string CalmingPresenceBonusId="background_bonus.calming_presence";
    public const string CombatMomentumBuff="RebirthBackgroundCombatMomentum";
    public const string HunterObservationBuff="RebirthHunterObservation";
    public const string CombatStacksCVar="$rbCombatMomentumStacks";
    public const string CombatReloadCVar="$rbCombatMomentumReload";
    public const string CombatHandlingCVar="$rbCombatMomentumHandling";
    public const string CombatSpreadCVar="$rbCombatMomentumSpread";
    public const string CombatRecoilCVar="$rbCombatMomentumRecoil";

    private sealed class Contribution { public float Damage; public float MaxHealth; public float LastAt; }
    private sealed class CombatState { public int Stacks; public float ActiveUntil; public int LastSent=-1; public float LastSendAt; }
    private sealed class Observation { public int TargetId; public float StartedAt; }
    private sealed class MarkState { public int TargetId; public float StartedAt; public float ExpiresAt; }

    private static readonly object Gate=new object();
    private static readonly Dictionary<long,Contribution> Contributions=new Dictionary<long,Contribution>();
    private static readonly Dictionary<int,CombatState> CombatStates=new Dictionary<int,CombatState>();
    private static readonly Dictionary<int,Observation> Observations=new Dictionary<int,Observation>();
    private static readonly Dictionary<long,MarkState> LivingMarks=new Dictionary<long,MarkState>();
    private static readonly Dictionary<long,float> CalmingStartedAt=new Dictionary<long,float>();
    private static readonly Dictionary<long,float> CalmingConsumedUntil=new Dictionary<long,float>();
    private static readonly Dictionary<int,float> ProcessedKillUntil=new Dictionary<int,float>();
    private static readonly Dictionary<int,float> NextHunterScanAt=new Dictionary<int,float>();
    private static bool installed;
    private static float nextServerTick;
    private static float nextCleanupAt;
    private static int qualifiedKills,rejectedKills,observationsCompleted,observationsBroken,calmingSuppressions;

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Chunk I] already installed";
        if(harmony==null)return "[REBIRTH Chunk I] no Harmony";
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCombatMomentumDamagePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthHunterObservationActivationPatch));
        ModEvents.EntityKilled.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SEntityKilledData>(OnEntityKilled));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed=true;
        return "[REBIRTH Chunk I] installed soldier+police+hunter+ranger";
    }

    public static float GetTuning(string bonus,string key,float fallback)
    {
        RebirthBackgroundBonusDefinition d;
        if(!RebirthBackgroundBonusRegistry.TryGet(bonus,out d)||d==null)return fallback;
        RebirthBackgroundBonusTuningValue t;float v;
        return d.TryGetTuning(key,out t)&&t!=null&&float.TryParse(t.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:fallback;
    }

    private static int MaxStacks=>Mathf.Clamp(Mathf.RoundToInt(GetTuning(CombatMomentumBonusId,"max_stacks",3f)),1,3);
    private static float CombatGrace=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"combat_grace_seconds",12f),3f,60f);
    private static float MinDamage=>Mathf.Max(1f,GetTuning(CombatMomentumBonusId,"minimum_personal_damage",20f));
    private static float ActivityMinDamage=>Mathf.Max(1f,GetTuning(CombatMomentumBonusId,"activity_minimum_damage",5f));
    private static float MinHealthFraction=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"minimum_target_health_fraction",0.15f),0.01f,1f);
    private static float ReloadPerStack=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"reload_per_stack",0.05f),0f,0.25f);
    private static float HandlingPerStack=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"handling_per_stack",0.04f),0f,0.25f);
    private static float SpreadPerStack=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"spread_reduction_per_stack",0.04f),0f,0.25f);
    private static float RecoilPerStack=>Mathf.Clamp(GetTuning(CombatMomentumBonusId,"recoil_reduction_per_stack",0.04f),0f,0.25f);
    private static float ObserveSeconds=>Mathf.Clamp(GetTuning(HuntersMarkBonusId,"observation_seconds",3f),1f,10f);
    private static float MarkSeconds=>Mathf.Clamp(GetTuning(HuntersMarkBonusId,"mark_duration_seconds",60f),10f,300f);
    private static float ObserveDistance=>Mathf.Clamp(GetTuning(HuntersMarkBonusId,"observation_distance_meters",30f),5f,60f);
    private static float TrackDistance=>Mathf.Clamp(GetTuning(HuntersMarkBonusId,"tracking_distance_meters",120f),25f,250f);
    private static float CalmingDelay=>Mathf.Clamp(GetTuning(CalmingPresenceBonusId,"hostility_delay_seconds",8f),1f,30f);

    private static bool IsServer()
    {
        World w=GameManager.Instance!=null?GameManager.Instance.World:null;
        if(w==null||w.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        return c!=null&&c.IsServer;
    }

    private static long PairKey(int a,int b){return ((long)a<<32)^(uint)b;}

    public static void RecordDamage(EntityAlive victim,DamageSource source,int healthBefore)
    {
        if(!IsServer()||victim==null||source==null||victim.world==null)return;
        int loss=Math.Max(0,healthBefore-victim.Health);if(loss<=0)return;
        EntityPlayer attacker=victim.world.GetEntity(source.getEntityId()) as EntityPlayer;
        if(attacker!=null&&attacker!=victim&&IsDirectPersonalCombat(attacker,victim,source))
        {
            if(RebirthBackgroundBonusService.HasBonus(attacker,CombatMomentumBonusId))
            {
                float now=Time.realtimeSinceStartup;long key=PairKey(victim.entityId,attacker.entityId);
                lock(Gate)
                {
                    Contribution c;if(!Contributions.TryGetValue(key,out c)){c=new Contribution();Contributions[key]=c;}
                    c.Damage+=loss;c.MaxHealth=Math.Max(c.MaxHealth,SafeMaxHealth(victim));c.LastAt=now;
                    if(loss>=ActivityMinDamage){CombatState s=GetCombatStateLocked(attacker.entityId);s.ActiveUntil=Math.Max(s.ActiveUntil,now+CombatGrace);}
                }
            }
        }
        EntityPlayer soldierVictim=victim as EntityPlayer;
        if(soldierVictim!=null&&loss>=ActivityMinDamage&&RebirthBackgroundBonusService.HasBonus(soldierVictim,CombatMomentumBonusId)&&IsMeaningfulHostileSource(soldierVictim,source))
        {
            lock(Gate){CombatState state=GetCombatStateLocked(soldierVictim.entityId);state.ActiveUntil=Math.Max(state.ActiveUntil,Time.realtimeSinceStartup+CombatGrace);}
        }
    }

    private static bool IsDirectPersonalCombat(EntityPlayer attacker,EntityAlive victim,DamageSource source)
    {
        if(attacker==null||victim==null||source==null||victim is EntityPlayer)return false;
        if(IsExcludedCombatTarget(victim)||IsFriendly(attacker,victim))return false;
        ItemValue item=source.AttackingItem;
        if(item==null||item.IsEmpty())return false;
        string skill=RebirthProgressionRuntimeConfig.ClassifyCombat(item);
        if(string.IsNullOrEmpty(skill))return false;
        // Direct player weapon families and direct explosives are valid; deployables/proxies have a non-player causal entity.
        return RebirthWeaponFamilySkillService.IsChunk7Skill(skill)||string.Equals(skill,"skill.explosives",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMeaningfulHostileSource(EntityPlayer soldier,DamageSource source)
    {
        if(soldier==null||source==null||soldier.world==null)return false;
        EntityAlive attacker=soldier.world.GetEntity(source.getEntityId()) as EntityAlive;
        return attacker!=null&&attacker!=soldier&&!IsFriendly(soldier,attacker);
    }

    private static bool IsExcludedCombatTarget(EntityAlive target)
    {
        if(target==null)return true;
        int owner;if(RebirthBlackMagicService.IsControlled(target.entityId,out owner))return true;
        if(target is EntityRebirthDogCompanion)return true;
        try
        {
            FastTags<TagGroup.Global> excluded=FastTags<TagGroup.Global>.Parse("companion,ally,minion,summoned,token,controlled,rebirthTamedWild");
            if(target.EntityClass!=null&&target.EntityClass.Tags.Test_AnySet(excluded))return true;
        }catch{}
        return false;
    }

    private static bool IsFriendly(EntityAlive a,EntityAlive b)
    {
        if(a==null||b==null)return false;
        try
        {
            FactionManager.Relationship r=FactionManager.Instance.GetRelationshipTier(a,b);
            return r==FactionManager.Relationship.Like||r==FactionManager.Relationship.Love||r==FactionManager.Relationship.Leader;
        }catch{return a.factionId==b.factionId;}
    }

    private static float SafeMaxHealth(EntityAlive e){try{return Math.Max(1,e.GetMaxHealth());}catch{return Math.Max(1,e.Health);}}

    private static void OnEntityKilled(ref ModEvents.SEntityKilledData data)
    {
        if(!IsServer())return;EntityAlive killed=data.KilledEntitiy as EntityAlive;if(killed==null)return;
        EntityPlayer killedPlayer=killed as EntityPlayer;if(killedPlayer!=null){lock(Gate){CombatState deadState;if(CombatStates.TryGetValue(killedPlayer.entityId,out deadState)){deadState.Stacks=0;deadState.ActiveUntil=0f;}}SyncCombat(killedPlayer,0);}
        EntityPlayer killer=data.KillingEntity as EntityPlayer;if(killer==null||!RebirthBackgroundBonusService.HasBonus(killer,CombatMomentumBonusId)){rejectedKills++;return;}
        float now=Time.realtimeSinceStartup;
        lock(Gate)
        {
            float seen;if(ProcessedKillUntil.TryGetValue(killed.entityId,out seen)&&seen>now)return;
            ProcessedKillUntil[killed.entityId]=now+120f;
        }
        Contribution c=null;lock(Gate)Contributions.TryGetValue(PairKey(killed.entityId,killer.entityId),out c);
        if(c==null||now-c.LastAt>CombatGrace){rejectedKills++;return;}
        float need=Math.Max(MinDamage,c.MaxHealth*MinHealthFraction);
        if(c.Damage+0.001f<need||IsExcludedCombatTarget(killed)||IsFriendly(killer,killed)){rejectedKills++;return;}
        lock(Gate){CombatState state=GetCombatStateLocked(killer.entityId);state.Stacks=Math.Min(MaxStacks,state.Stacks+1);state.ActiveUntil=now+CombatGrace;}
        qualifiedKills++;
        lock(Gate)
        {
            List<long> remove=new List<long>();foreach(KeyValuePair<long,Contribution> kv in Contributions)if((int)(kv.Key>>32)==killed.entityId)remove.Add(kv.Key);for(int i=0;i<remove.Count;i++)Contributions.Remove(remove[i]);
        }
    }

    private static CombatState GetCombatStateLocked(int playerId){CombatState s;if(!CombatStates.TryGetValue(playerId,out s)){s=new CombatState();CombatStates[playerId]=s;}return s;}

    private static void SyncCombat(EntityPlayer p,int stacks)
    {
        if(p==null)return;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(p is EntityPlayerLocal){ApplyClientCombatState(p.entityId,stacks);return;}
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCombatMomentumState>().Setup(p.entityId,stacks),_attachedToEntityId:p.entityId);
    }

    public static void ApplyClientCombatState(int playerId,int stacks)
    {
        World w=GameManager.Instance!=null?GameManager.Instance.World:null;if(w==null)return;EntityPlayer p=w.GetEntity(playerId) as EntityPlayer;if(p==null||p.Buffs==null)return;
        stacks=Mathf.Clamp(stacks,0,MaxStacks);
        SetCVar(p,CombatStacksCVar,stacks);SetCVar(p,CombatReloadCVar,ReloadPerStack*stacks);SetCVar(p,CombatHandlingCVar,HandlingPerStack*stacks);SetCVar(p,CombatSpreadCVar,-SpreadPerStack*stacks);SetCVar(p,CombatRecoilCVar,-RecoilPerStack*stacks);
        if(stacks>0){if(!p.Buffs.HasBuff(CombatMomentumBuff))p.Buffs.AddBuff(CombatMomentumBuff);}else if(p.Buffs.HasBuff(CombatMomentumBuff))p.Buffs.RemoveBuff(CombatMomentumBuff);
    }
    private static void SetCVar(EntityPlayer p,string n,float v){if(Math.Abs(p.Buffs.GetCustomVar(n)-v)>0.0005f)p.Buffs.SetCustomVar(n,v);}

    public static bool TryClassifyHunterNaturalAnimal(EntityAlive animal,out string reason)
    {
        reason=string.Empty;if(animal==null||animal.EntityClass==null){reason="entity-unavailable";return false;}
        EntityClass ec=animal.EntityClass;FastTags<TagGroup.Global> animalTag=FastTags<TagGroup.Global>.Parse("animal");if(!ec.Tags.Test_AnySet(animalTag)){reason="not-animal";return false;}
        string hard;if(RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(ec,out hard)){reason="pc002-hard-exclusion";return false;}
        try{FastTags<TagGroup.Global> excluded=FastTags<TagGroup.Global>.Parse("boss,event,quest,summoned,companion,ally,npc,deployed,player,undead,zombieAnimal,animalZombie,infected,rebirthTamedWild");if(ec.Tags.Test_AnySet(excluded)){reason="non-natural-or-controlled-animal";return false;}}catch{}
        if(animal is EntityRebirthDogCompanion){reason="companion";return false;}
        reason="natural-animal";return true;
    }

    public static bool CanPresentHunterObservation(EntityAlive animal,EntityPlayerLocal hunter,out string reason)
    {
        reason=string.Empty;if(animal==null||hunter==null||animal.IsDead()){reason="animal-unavailable";return false;}
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(hunter,HuntersMarkBonusId)){reason="not-hunter";return false;}
        if(!TryClassifyHunterNaturalAnimal(animal,out reason))return false;
        if((animal.position-hunter.position).sqrMagnitude>ObserveDistance*ObserveDistance){reason="too-far";return false;}
        return true;
    }

    public static bool BeginObservation(EntityPlayer hunter,EntityAlive animal,out string reason)
    {
        reason=string.Empty;if(!IsServer()||hunter==null||animal==null||animal.IsDead()){reason="Observation unavailable";return false;}
        if(!RebirthBackgroundBonusService.HasBonus(hunter,HuntersMarkBonusId)){reason="Hunter's Mark is not active";return false;}
        string classify;if(!TryClassifyHunterNaturalAnimal(animal,out classify)){reason="That animal cannot be tracked";return false;}
        if((animal.position-hunter.position).sqrMagnitude>ObserveDistance*ObserveDistance){reason="Move closer to observe";return false;}
        if(!HasTrueLineOfSight(hunter,animal)){reason="Keep the animal in clear view";return false;}
        lock(Gate)Observations[hunter.entityId]=new Observation{TargetId=animal.entityId,StartedAt=Time.realtimeSinceStartup};
        SyncObservationState(hunter,true,ObserveSeconds);
        reason="Observing animal...";return true;
    }

    private static bool HasTrueLineOfSight(EntityPlayer hunter,EntityAlive target)
    {
        if(hunter==null||target==null||hunter.world==null)return false;
        try{if(hunter.CanEntityBeSeen(target))return true;}catch{}
        try
        {
            Bounds b=target.boundingBox;Vector3 c=b.center;if(VisibilityRay(hunter,target,c))return true;
            if(VisibilityRay(hunter,target,c+Vector3.up*(b.extents.y*.4f)))return true;
            return VisibilityRay(hunter,target,c-Vector3.up*(b.extents.y*.3f));
        }catch{return false;}
    }
    private static bool VisibilityRay(EntityPlayer hunter,EntityAlive target,Vector3 point)
    {
        Vector3 origin=hunter.getHeadPosition(),dir=point-origin;float distance=dir.magnitude;if(distance<=.001f)return true;if(distance>ObserveDistance+.5f)return false;
        try{if(!hunter.IsInViewCone(point))return false;}catch{}
        Ray ray=new Ray(origin,dir);ray.origin+=dir.normalized*-.1f;int layer=hunter.GetModelLayer();
        try
        {
            hunter.SetModelLayer(2);if(!Voxel.Raycast(hunter.world,ray,distance+.75f,-1612492821,64,0f))return false;Transform hit=Voxel.voxelRayHitInfo.transform;if(hit==null)return false;string tag=Voxel.voxelRayHitInfo.tag??string.Empty;if(tag.StartsWith("E_BP_",StringComparison.Ordinal))hit=GameUtils.GetHitRootTransform(tag,hit);return hit==target.transform;
        }catch{return false;}finally{try{hunter.SetModelLayer(layer);}catch{}}
    }

    public static void ReceiveObservationRequest(EntityPlayer hunter,int targetId)
    {
        if(!IsServer()||hunter==null||hunter.world==null)return;string reason;bool ok=BeginObservation(hunter,hunter.world.GetEntity(targetId) as EntityAlive,out reason);if(!string.IsNullOrEmpty(reason))GameManager.ShowTooltipMP(hunter,reason,ok?"ui_success":"ui_denied");
    }

    private static void TickObservation(EntityPlayer hunter,float now)
    {
        Observation o;lock(Gate){if(!Observations.TryGetValue(hunter.entityId,out o))return;}
        EntityAlive animal=hunter.world.GetEntity(o.TargetId) as EntityAlive;string reason;
        if(animal==null||animal.IsDead()||!TryClassifyHunterNaturalAnimal(animal,out reason)||(animal.position-hunter.position).sqrMagnitude>ObserveDistance*ObserveDistance||!HasTrueLineOfSight(hunter,animal))
        {
            lock(Gate)Observations.Remove(hunter.entityId);observationsBroken++;SyncObservationState(hunter,false,0f);GameManager.ShowTooltipMP(hunter,"Observation broken","ui_denied");return;
        }
        if(now-o.StartedAt<ObserveSeconds)return;
        lock(Gate){Observations.Remove(hunter.entityId);LivingMarks[PairKey(hunter.entityId,animal.entityId)]=new MarkState{TargetId=animal.entityId,StartedAt=now,ExpiresAt=now+MarkSeconds};}
        SyncObservationState(hunter,false,0f);observationsCompleted++;GameManager.ShowTooltipMP(hunter,"Animal marked","ui_success");
    }

    private static void SyncObservationState(EntityPlayer hunter,bool active,float seconds)
    {
        if(hunter==null)return;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(hunter is EntityPlayerLocal){ApplyClientObservationState(hunter.entityId,active,seconds);return;}if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHunterObservationState>().Setup(hunter.entityId,active,seconds),_attachedToEntityId:hunter.entityId);
    }

    public static void ApplyClientObservationState(int playerId,bool active,float seconds)
    {
        World w=GameManager.Instance!=null?GameManager.Instance.World:null;if(w==null)return;EntityPlayer p=w.GetEntity(playerId) as EntityPlayer;if(p==null||p.Buffs==null)return;if(active){if(p.Buffs.HasBuff(HunterObservationBuff))p.Buffs.RemoveBuff(HunterObservationBuff);p.Buffs.AddBuff(HunterObservationBuff,-1,false,false,Mathf.Max(.25f,seconds));}else if(p.Buffs.HasBuff(HunterObservationBuff))p.Buffs.RemoveBuff(HunterObservationBuff);
    }

    private static void SendHunterSnapshot(EntityPlayer hunter,float now)
    {
        if(hunter==null||hunter.world==null)return;float due;lock(Gate){if(NextHunterScanAt.TryGetValue(hunter.entityId,out due)&&due>now)return;NextHunterScanAt[hunter.entityId]=now+1f;}List<RebirthHunterTrackingRow> rows=new List<RebirthHunterTrackingRow>();
        lock(Gate)
        {
            List<long> remove=new List<long>();foreach(KeyValuePair<long,MarkState> kv in LivingMarks)
            {
                if((int)(kv.Key>>32)!=hunter.entityId)continue;MarkState m=kv.Value;EntityAlive a=hunter.world.GetEntity(m.TargetId) as EntityAlive;string why;
                if(m.ExpiresAt<=now||a==null||a.IsDead()||!TryClassifyHunterNaturalAnimal(a,out why)){remove.Add(kv.Key);continue;}
                float distance=Vector3.Distance(hunter.position,a.position);if(distance>TrackDistance)continue;float age=Mathf.Clamp01((now-m.StartedAt)/Mathf.Max(1f,MarkSeconds));float precision=.25f+age*2f+(distance/Mathf.Max(1f,TrackDistance))*1.5f;rows.Add(new RebirthHunterTrackingRow{EntityId=a.entityId,Position=Quantize(a.position,precision),Kind=0,SecondsRemaining=Math.Max(0f,m.ExpiresAt-now),Name=a.EntityName??string.Empty});
            }
            for(int i=0;i<remove.Count;i++)LivingMarks.Remove(remove[i]);
        }
        List<Entity> found=hunter.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(hunter.position,Vector3.one*(TrackDistance*2f)),new List<Entity>());
        for(int i=0;i<found.Count;i++)
        {
            EntityAlive a=found[i] as EntityAlive;if(a==null||!a.IsDead())continue;string why;if(!TryClassifyHunterNaturalAnimal(a,out why))continue;float distance=Vector3.Distance(hunter.position,a.position);if(distance>TrackDistance)continue;rows.Add(new RebirthHunterTrackingRow{EntityId=a.entityId,Position=Quantize(a.position,1f),Kind=1,SecondsRemaining=-1f,Name=a.EntityName??string.Empty});
        }
        ConnectionManager conn=SingletonMonoBehaviour<ConnectionManager>.Instance;if(hunter is EntityPlayerLocal){RebirthHunterMarkerService.ApplySnapshot(hunter.entityId,rows);return;}if(conn!=null&&conn.IsServer)conn.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHunterTrackingSnapshot>().Setup(hunter.entityId,rows),_attachedToEntityId:hunter.entityId);
    }
    private static Vector3 Quantize(Vector3 p,float grid){grid=Mathf.Max(.1f,grid);return new Vector3(Mathf.Round(p.x/grid)*grid,Mathf.Round(p.y/grid)*grid,Mathf.Round(p.z/grid)*grid);}

    public static bool ShouldDelayWildlifeHostility(EntityAlive animal,EntityPlayer player,out string reason)
    {
        reason=string.Empty;if(!IsServer()||animal==null||player==null||!RebirthBackgroundBonusService.HasBonus(player,CalmingPresenceBonusId))return false;
        RebirthWildAffinityCategoryDefinition c;string why;if(!RebirthWildAffinityService.TryClassify(animal,out c,out why)){reason=why;return false;}
        if(animal.GetRevengeTarget()==player){reason="ranger-provoked-revenge";return false;}
        float now=Time.realtimeSinceStartup;long key=PairKey(animal.entityId,player.entityId);
        lock(Gate)
        {
            float consumed;if(CalmingConsumedUntil.TryGetValue(key,out consumed)&&consumed>now){reason="calming-window-consumed";return false;}
            float started;if(!CalmingStartedAt.TryGetValue(key,out started)){CalmingStartedAt[key]=now;calmingSuppressions++;reason="calming-delay-started";return true;}
            if(now-started<CalmingDelay){calmingSuppressions++;reason="calming-delay-active";return true;}
            CalmingStartedAt.Remove(key);CalmingConsumedUntil[key]=now+60f;reason="calming-delay-expired";return false;
        }
    }

    public static void MarkWildlifeProvocation(EntityAlive animal,EntityPlayer player)
    {
        if(animal==null||player==null)return;long key=PairKey(animal.entityId,player.entityId);lock(Gate){CalmingStartedAt.Remove(key);CalmingConsumedUntil[key]=Time.realtimeSinceStartup+60f;}
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(GameManager.Instance==null||GameManager.Instance.World==null)return;World w=GameManager.Instance.World;float now=Time.realtimeSinceStartup;
        if(w.IsRemote())return;if(!IsServer()||now<nextServerTick)return;nextServerTick=now+.25f;
        List<EntityPlayer> players=w.Players!=null?w.Players.list:null;if(players==null)return;
        for(int i=0;i<players.Count;i++)
        {
            EntityPlayer p=players[i];if(p==null)continue;
            if(RebirthBackgroundBonusService.HasBonus(p,CombatMomentumBonusId))
            {
                int stacks;bool send=false;lock(Gate){CombatState s=GetCombatStateLocked(p.entityId);if(s.Stacks>0&&s.ActiveUntil<=now)s.Stacks=0;stacks=s.Stacks;if(s.LastSent!=stacks||now-s.LastSendAt>=2f){s.LastSent=stacks;s.LastSendAt=now;send=true;}}if(send)SyncCombat(p,stacks);
            }
            else
            {
                bool had=false;lock(Gate){CombatState prior;if(CombatStates.TryGetValue(p.entityId,out prior)){had=prior.Stacks!=0||prior.LastSent!=0;prior.Stacks=0;}}if(had)SyncCombat(p,0);
            }
            if(RebirthBackgroundBonusService.HasBonus(p,HuntersMarkBonusId)){TickObservation(p,now);SendHunterSnapshot(p,now);}else if(p is EntityPlayerLocal)RebirthHunterMarkerService.ApplySnapshot(p.entityId,new List<RebirthHunterTrackingRow>());
        }
        Cleanup(now, w);
    }

    private static void Cleanup(float now, World world)
    {
        if(now<nextCleanupAt)return;
        nextCleanupAt=now+1f;
        lock(Gate)
        {
            if(Contributions.Count>0){List<long> c=new List<long>();foreach(KeyValuePair<long,Contribution> kv in Contributions)if(now-kv.Value.LastAt>Math.Max(30f,CombatGrace*2f))c.Add(kv.Key);for(int i=0;i<c.Count;i++)Contributions.Remove(c[i]);}
            if(ProcessedKillUntil.Count>0){List<int> k=new List<int>();foreach(KeyValuePair<int,float> kv in ProcessedKillUntil)if(kv.Value<=now)k.Add(kv.Key);for(int i=0;i<k.Count;i++)ProcessedKillUntil.Remove(k[i]);}
            if(CalmingConsumedUntil.Count>0){List<long> calm=new List<long>();foreach(KeyValuePair<long,float> kv in CalmingConsumedUntil)if(kv.Value<=now)calm.Add(kv.Key);for(int i=0;i<calm.Count;i++)CalmingConsumedUntil.Remove(calm[i]);}
            if(CalmingStartedAt.Count>0){List<long> calm=new List<long>();foreach(KeyValuePair<long,float> kv in CalmingStartedAt)if(now-kv.Value>60f)calm.Add(kv.Key);for(int i=0;i<calm.Count;i++)CalmingStartedAt.Remove(calm[i]);}
            if(world!=null)
            {
                List<int> disconnected=null;
                foreach(KeyValuePair<int,CombatState> kv in CombatStates) if(!(world.GetEntity(kv.Key) is EntityPlayer)){if(disconnected==null)disconnected=new List<int>();disconnected.Add(kv.Key);}
                if(disconnected!=null)for(int i=0;i<disconnected.Count;i++){int id=disconnected[i];CombatStates.Remove(id);Observations.Remove(id);NextHunterScanAt.Remove(id);}
            }
        }
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Chunk I Signatures]");b.AppendLine("authority=server persistent/reward state; client=projection-only");b.AppendLine("qualifiedKills="+qualifiedKills+" rejectedKills="+rejectedKills+" observationsCompleted="+observationsCompleted+" observationsBroken="+observationsBroken+" calmingSuppressions="+calmingSuppressions);
        if(player!=null){CombatState s;lock(Gate)CombatStates.TryGetValue(player.entityId,out s);b.AppendLine("player="+player.entityId+" soldier="+RebirthBackgroundBonusService.HasBonus(player,CombatMomentumBonusId)+" stacks="+(s!=null?s.Stacks:0)+" hunter="+RebirthBackgroundBonusService.HasBonus(player,HuntersMarkBonusId)+" ranger="+RebirthBackgroundBonusService.HasBonus(player,CalmingPresenceBonusId));}
        b.AppendLine("soldier maxStacks="+MaxStacks+" grace="+CombatGrace.ToString("0.##",CultureInfo.InvariantCulture)+" minDamage="+MinDamage.ToString("0.##",CultureInfo.InvariantCulture)+" activityMinDamage="+ActivityMinDamage.ToString("0.##",CultureInfo.InvariantCulture)+" minHealthFraction="+MinHealthFraction.ToString("0.###",CultureInfo.InvariantCulture));
        b.AppendLine("hunter observeSeconds="+ObserveSeconds.ToString("0.##",CultureInfo.InvariantCulture)+" markSeconds="+MarkSeconds.ToString("0.##",CultureInfo.InvariantCulture)+" observeDistance="+ObserveDistance.ToString("0.##",CultureInfo.InvariantCulture)+" trackingDistance="+TrackDistance.ToString("0.##",CultureInfo.InvariantCulture));
        b.AppendLine("ranger hostilityDelay="+CalmingDelay.ToString("0.##",CultureInfo.InvariantCulture));return b.ToString().TrimEnd();
    }

    public static string RunVectors()
    {
        List<string> f=new List<string>();if(MaxStacks!=3)f.Add("max stacks");if(Math.Abs(3*ReloadPerStack-.15f)>.001f)f.Add("reload vector");if(Math.Abs(3*HandlingPerStack-.12f)>.001f)f.Add("handling vector");if(Math.Abs(3*SpreadPerStack-.12f)>.001f)f.Add("spread vector");if(ObserveSeconds<1||MarkSeconds<=ObserveSeconds)f.Add("hunter timing");if(CalmingDelay<=0)f.Add("calming delay");return "[REBIRTH Chunk I] vectors="+(f.Count==0?"PASS":"FAIL")+" failures="+f.Count+(f.Count==0?string.Empty:" "+string.Join(" | ",f.ToArray()));
    }

    public static void ResetRuntime()
    {
        lock(Gate){Contributions.Clear();CombatStates.Clear();Observations.Clear();LivingMarks.Clear();CalmingStartedAt.Clear();CalmingConsumedUntil.Clear();ProcessedKillUntil.Clear();NextHunterScanAt.Clear();}
        RebirthHunterMarkerService.Reset();nextServerTick=0f;qualifiedKills=rejectedKills=observationsCompleted=observationsBroken=calmingSuppressions=0;
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ResetRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ResetRuntime();}
}

[HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthCombatMomentumDamagePatch
{
    private static void Prefix(EntityAlive __instance,out int __state){__state=__instance!=null?__instance.Health:0;}
    private static void Postfix(EntityAlive __instance,DamageResponse _dmResponse,int __state){try{RebirthCombatPatrolTrackingSignatureService.RecordDamage(__instance,_dmResponse.Source,__state);}catch(Exception ex){if(RebirthSurvivorDebug.Enabled)Log.Warning("[REBIRTH Combat Momentum] "+ex.GetType().Name+": "+ex.Message);}}
}

[HarmonyPatch]
internal static class RebirthHunterObservationActivationPatch
{
    [HarmonyPostfix][HarmonyPatch(typeof(EntityAlive),"InitLocalActivationCommands")]
    private static void Commands(Entity __instance,Action<EntityActivationCommand> __0)
    {
        EntityAlive animal=__instance as EntityAlive;EntityPlayerLocal hunter=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;if(animal==null||hunter==null||__0==null)return;string reason;if(RebirthCombatPatrolTrackingSignatureService.CanPresentHunterObservation(animal,hunter,out reason))__0(new EntityActivationCommand("rebirthHunterObserve","search",null,"rebirthHunterObserve"));
    }
    [HarmonyPrefix][HarmonyPatch(typeof(EntityAlive),"OnEntityActivated",new Type[]{typeof(EntityActivationCommand),typeof(EntityPlayerLocal)})]
    private static bool Activated(Entity __instance,EntityActivationCommand __0,EntityPlayerLocal __1)
    {
        // Harmony positional parameters intentionally avoid coupling this patch to native parameter names.
        // V3.2 names these _command/_playerFocusing; the former named binding aborted the entire
        // Survivor progression runtime installer before the Creator or spawn gate could be trusted.
        EntityActivationCommand command=__0;
        EntityPlayerLocal focusingPlayer=__1;
        if(command.commandId!="rebirthHunterObserve")return true;if(focusingPlayer==null||__instance==null)return false;World w=GameManager.Instance?.World;if(w==null)return false;if(!w.IsRemote()){string r;bool ok=RebirthCombatPatrolTrackingSignatureService.BeginObservation(focusingPlayer,__instance as EntityAlive,out r);if(!string.IsNullOrEmpty(r))GameManager.ShowTooltipMP(focusingPlayer,r,ok?"ui_success":"ui_denied");}else{ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthHunterObservationRequest>().Setup(focusingPlayer.entityId,__instance.entityId));}return false;
    }
}
