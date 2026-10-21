using System;
using System.Runtime.CompilerServices;
using UnityEngine;

// Human-only 2.6 behavior policy. Dogs/panthers never enter this path.
internal static class RebirthHumanAiPolicy
{
    private sealed class Memory
    {
        internal bool Offline; internal Vector3 OfflineAnchor;
        internal uint AnchorRevision; internal Vector3 Anchor; internal float Facing; internal bool HasAnchor;
        internal EntityPlayer Owner;internal string OwnerId;internal int OwnerFrame=-1;
    }
    private static readonly ConditionalWeakTable<EntityRebirthHumanoidNPC,Memory> Memories=new ConditionalWeakTable<EntityRebirthHumanoidNPC,Memory>();
    private static readonly FastTags<TagGroup.Global> Melee=FastTags<TagGroup.Global>.Parse("melee,allyanimal");
    private static readonly FastTags<TagGroup.Global> NoTracking=FastTags<TagGroup.Global>.Parse("nomeleetracking");
    internal static bool IsHuman(EntityRebirthNPC npc)=>npc is EntityRebirthHumanoidNPC&&
        (npc.RebirthProfile.Category==RebirthNpcCategory.Survivor||npc.RebirthProfile.Category==RebirthNpcCategory.Bandit);
    internal static bool Ready(EntityRebirthNPC npc,bool combat=false)
    {
        if(!IsHuman(npc)||!ThreadManager.IsMainThread()||npc.world==null||npc.world.IsRemote()||
            !ReferenceEquals(npc.world,GameManager.Instance?.World)||!ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||
            npc.Buffs.GetCustomVar(RebirthHumanEquipment.Hold)==1f||npc.IsDead()||!npc.IsSpawned()||npc.RebirthRuntimeState==null||npc.RebirthRuntimeState.PreparedRestorationPending||
            npc.RebirthRuntimeState.Presence!=RebirthNpcPresenceState.Active||npc.RebirthRuntimeState.Travel!=RebirthNpcTravelState.None||
            npc.RebirthRuntimeState.Order==RebirthNpcOrderState.Mission||npc.RebirthRuntimeState.Order==RebirthNpcOrderState.Work||
            RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||
            npc.Buffs.GetCustomVar("onMission")==1f||npc.Buffs.GetCustomVar("$FR_NPC_Respawn")==1f||npc.Buffs.GetCustomVar("$FR_NPC_Hidden")==1f||
            npc.emodel?.IsRagdollActive==true||npc.sleepingOrWakingUp||npc.bodyDamage.CurrentStun!=EnumEntityStunType.None||
            (npc.Jumping&&!npc.isSwimming))return false;
        if(combat&&(RebirthCompanionBehaviorService.IsAttackStopped(npc.RebirthRuntimeState.StableId)||
            npc.Buffs.HasBuff("FuriousRamsayBuffPauseAttack")||npc.Buffs.HasBuff("buffNPCModStopAttacking")||npc.Buffs.HasBuff("FuriousRamsayStandStill")))
        {npc.SetAttackTarget(null,0);npc.SetRevengeTarget(null);return false;}
        return true;
    }
    internal static EntityPlayer Owner(EntityRebirthNPC npc)
    {
        if(!(npc is EntityRebirthHumanoidNPC human)||npc.RebirthRuntimeState?.OwnershipKind!=RebirthNpcOwnershipKind.Player||npc.world==null)return null;
        var memory=Memories.GetValue(human,_=>new Memory());string id=npc.RebirthRuntimeState.OwnerId;
        if(memory.OwnerFrame==Time.frameCount&&memory.OwnerId==id&&
            (memory.Owner==null||ReferenceEquals(npc.world.GetEntity(memory.Owner.entityId),memory.Owner)))return memory.Owner;
        memory.OwnerFrame=Time.frameCount;memory.OwnerId=id;memory.Owner=null;
        var players=npc.world.Players?.list;if(players==null)return null;
        foreach(var player in players)
        {
            if(player==null||!ReferenceEquals(npc.world.GetEntity(player.entityId),player))continue;
            var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if(data?.PrimaryId!=null&&string.Equals(data.PrimaryId.ToString(),id,StringComparison.OrdinalIgnoreCase)||
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var identity)&&string.Equals(identity.CanonicalId,id,StringComparison.OrdinalIgnoreCase))
            {memory.Owner=player;break;}
        }
        return memory.Owner;
    }
    internal static bool Owned(EntityRebirthNPC npc)=>npc.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player;
    internal static bool Offline(EntityRebirthNPC npc)=>Owned(npc)&&Owner(npc)==null;
    internal static bool EventSpawn(EntityRebirthNPC npc)=>npc.Buffs.GetCustomVar("$eventSpawn")==1f;
    internal static bool CanChase(EntityRebirthNPC npc)=>!Owned(npc)||npc.RebirthRuntimeState.Order==RebirthNpcOrderState.Follow&&!Offline(npc);
    internal static float Range(EntityRebirthNPC npc)
    {
        bool offline=Offline(npc);
        bool full=Owned(npc)&&npc.RebirthRuntimeState.Order==RebirthNpcOrderState.Follow&&
            RebirthCompanionBehaviorService.GetMode(npc.RebirthRuntimeState.StableId)==RebirthCompanionBehaviorMode.FullControl;
        return RebirthHumanAiRules.AcquisitionDistance(offline,full,FloatProperty(npc,"RebirthHuntingDistance",30f,1f,100f),
            FloatProperty(npc,"RebirthFullControlDistance",10f,1f,100f));
    }
    internal static float FloatProperty(EntityRebirthNPC npc,string key,float fallback,float min,float max)
    {
        return float.TryParse(npc.EntityClass.Properties.GetString(key),System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,out float value)&&!float.IsNaN(value)&&!float.IsInfinity(value)?Mathf.Clamp(value,min,max):fallback;
    }
    internal static bool MeleeTrackingAllowed(EntityRebirthNPC npc,EntityAlive target)=>
        RebirthHumanAiRules.TracksElevatedTarget(npc.HasAnyTags(Melee),target.HasAnyTags(NoTracking),target.position.y-npc.position.y);
    internal static bool ThreatensOwner(EntityRebirthNPC npc,EntityAlive target)
    {var owner=Owner(npc);return owner!=null&&target.GetAttackTarget()==owner;}
    internal static bool Allowed(EntityRebirthNPC npc,EntityAlive target,bool requireSight=true,bool retaliation=false,bool acquisition=true)
    {
        if(!Ready(npc,true)||target==null||target==npc||target.world!=npc.world||target.IsDead()||FactionManager.Instance==null||!MeleeTrackingAllowed(npc,target))return false;
        if(!(target is EntityPlayer||target is EntityZombie||target is EntityRebirthNPC||target is EntityAnimal))return false;
        if(target.IsIgnoredByAI())return false;
        if(acquisition&&!(EventSpawn(npc)&&target is EntityPlayer))
        {var block=World.worldToBlockPos(target.position);if(!npc.isWithinHomeDistance(block.x,block.y,block.z))return false;}
        if(target is EntityRebirthNPC other&&RebirthNpcFriendlyFirePolicy.AreAllied(npc.RebirthRuntimeState,other.RebirthRuntimeState))return false;
        if(target is EntityPlayer player)
        {
            if(player.IsIgnoredByAI()||ReferenceEquals(player,Owner(npc)))return false;
            var data=GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if(Owned(npc)&&(data?.PrimaryId==null||string.Equals(data.PrimaryId.ToString(),npc.RebirthRuntimeState.OwnerId,StringComparison.OrdinalIgnoreCase)))return false;
            if(requireSight&&!npc.CanSeeStealth(npc.GetDistance(player),player.Stealth.lightLevel))return false;
        }
        var disposition=RebirthNpcFactionCombatResolver.Classify(FactionManager.Instance.GetRelationshipValue(npc,target));
        if(disposition==RebirthNpcCombatDisposition.Allied||disposition==RebirthNpcCombatDisposition.Unknown)return false;
        if(disposition!=RebirthNpcCombatDisposition.Hostile&&!ThreatensOwner(npc,target)&&!retaliation)return false;
        float distance=npc.GetDistance(target),range=Range(npc);
        if(float.IsNaN(distance)||float.IsInfinity(distance))return false;
        bool eventPlayer=EventSpawn(npc)&&target is EntityPlayer&&distance<=120f;
        bool bypass=(target.Buffs.GetCustomVar("$FuriousRamsayAttacking")==1f||target.Buffs.GetCustomVar("$FuriousRamsayAttacked")==1f||
            npc.Buffs.HasBuff("buffNPCModThreatControlMode"))&&distance<=50f;
        var owner=Owner(npc);
        if((acquisition||Offline(npc))&&!RebirthHumanAiRules.InsideAcquisition(distance,owner!=null?owner.GetDistance(target):-1f,range,bypass,eventPlayer))return false;
        return !requireSight||eventPlayer||npc.CanSee(target);
    }
    internal static bool HasCombat(EntityRebirthNPC npc)=>npc.GetAttackTarget()!=null&&!npc.GetAttackTarget().IsDead();
    internal static bool TryAnchor(EntityRebirthNPC npc,out Vector3 anchor,out float facing)
    {
        anchor=Vector3.zero;facing=npc.rotation.y;if(!IsHuman(npc))return false;
        var state=npc.RebirthRuntimeState;var memory=Memories.GetValue((EntityRebirthHumanoidNPC)npc,_=>new Memory());
        if(Offline(npc))
        {
            if(!memory.Offline){memory.Offline=true;memory.OfflineAnchor=npc.position;}
            anchor=memory.OfflineAnchor;return true;
        }
        memory.Offline=false;
        if(state.Order!=RebirthNpcOrderState.Stay&&state.Order!=RebirthNpcOrderState.Guard)return false;
        if(!memory.HasAnchor||memory.AnchorRevision!=state.Revision)
        {
            memory.Anchor=state.HasGuardPosition?state.GuardPosition:npc.position;memory.Facing=npc.rotation.y;
            if(RebirthNpcAggregatePersistenceStore.TryGet(state.StableId,out var person)&&person.Transform!=null)
            {
                if(!state.HasGuardPosition&&person.Transform.AnchorPosition.HasValue)memory.Anchor=person.Transform.AnchorPosition.Value;
                if(person.Transform.AnchorRotation.HasValue)memory.Facing=person.Transform.AnchorRotation.Value;
            }
            memory.AnchorRevision=state.Revision;memory.HasAnchor=true;
        }
        anchor=memory.Anchor;facing=memory.Facing;return true;
    }
    internal static void CaptureFacing(EntityRebirthNPC npc,float yaw)
    {
        if(!IsHuman(npc)||!npc.RebirthRuntimeState.HasGuardPosition||float.IsNaN(yaw)||float.IsInfinity(yaw))return;
        if(!RebirthNpcAggregatePersistenceStore.TryGet(npc.RebirthRuntimeState.StableId,out _))RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
        RebirthNpcAggregatePersistenceStore.Mutate(npc.RebirthRuntimeState.StableId,person=>
        {if(person.Transform==null)person.Transform=new RebirthNpcTransformRecord();person.Transform.AnchorPosition=npc.RebirthRuntimeState.GuardPosition;person.Transform.AnchorRotation=yaw;});
        Memories.Remove((EntityRebirthHumanoidNPC)npc);
    }
}

