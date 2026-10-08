using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class EAIRebirthSelectHostileTarget : EAIBase
{
    private const float MaximumRange=30f;
    private readonly List<Entity> candidates=new List<Entity>();
    private EntityRebirthNPC actor;
    private EntityAlive selected;
    private bool ownsTarget;
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=0.75f;
    }
    private bool Ready()
    {
        return actor!=null&&actor.world!=null&&!actor.world.IsRemote()&&!actor.IsDead()
            &&actor.RebirthRuntimeState!=null&&actor.RebirthRuntimeState.Presence==RebirthNpcPresenceState.Active
            &&!RebirthCompanionBehaviorService.IsAttackStopped(actor.RebirthRuntimeState.StableId);
    }
    internal bool CanTarget(EntityAlive target)
    {
        if(!Ready()||target==null||target==actor||target.world!=actor.world||target.IsDead()||FactionManager.Instance==null)return false;
        if(!(target is EntityPlayer)&&!(target is EntityZombie)&&!(target is EntityRebirthNPC))return false;
        float sq=(target.position-actor.position).sqrMagnitude;
        float sense=actor.GetSeeDistance();
        if(float.IsNaN(sq)||float.IsInfinity(sq)||float.IsNaN(sense)||float.IsInfinity(sense)||sense<=0)return false;
        float range=Math.Min(MaximumRange,sense);
        if(sq>range*range)return false;
        var npc=target as EntityRebirthNPC;
        if(npc!=null&&RebirthNpcFriendlyFirePolicy.AreAllied(actor.RebirthRuntimeState,npc.RebirthRuntimeState))return false;
        var player=target as EntityPlayer;
        if(player!=null)
        {
            if(player.IsIgnoredByAI())return false;
            string owner=actor.RebirthRuntimeState.OwnerId;
            var data=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
            if(actor.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.Player)
            {
                if(string.IsNullOrWhiteSpace(owner)||data?.PrimaryId==null)return false;
                if(string.Equals(owner,data.PrimaryId.ToString(),StringComparison.OrdinalIgnoreCase))return false;
            }
            if(!actor.CanSeeStealth(Mathf.Sqrt(sq),player.Stealth.lightLevel))return false;
        }
        if(RebirthNpcFactionCombatResolver.Classify(FactionManager.Instance.GetRelationshipValue(actor,target))!=RebirthNpcCombatDisposition.Hostile)return false;
        return actor.CanSee(target);
    }
    public override bool CanExecute()
    {
        selected=null;if(!Ready())return false;
        float best=float.MaxValue;
        candidates.Clear();
        actor.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(actor.position,Vector3.one*(MaximumRange*2)),candidates);
        foreach(var entity in candidates)
        {
            var target=entity as EntityAlive;if(!CanTarget(target))continue;
            float sq=(target.position-actor.position).sqrMagnitude;
            if(sq<best||(sq==best&&selected!=null&&target.entityId<selected.entityId)){best=sq;selected=target;}
        }
        candidates.Clear();return selected!=null;
    }
    public override void Start()
    {
        if(!CanTarget(selected))return;
        actor.SetAttackTarget(selected,200);ownsTarget=true;
    }
    public override bool Continue(){return ownsTarget&&actor.GetAttackTarget()==selected&&CanTarget(selected);}
    public override void Update(){if(Continue())actor.SetAttackTarget(selected,200);}
    public override void Reset()
    {
        if(ownsTarget&&actor!=null&&actor.GetAttackTarget()==selected)actor.SetAttackTarget(null,0);
        ownsTarget=false;selected=null;candidates.Clear();
    }
}