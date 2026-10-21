using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthSelectHostileTarget : EAIBase
{
    private readonly List<Entity> candidates=new List<Entity>();
    private EntityRebirthNPC actor;private EntityAlive selected;private bool ownsTarget;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=.75f;}
    internal bool CanTarget(EntityAlive target)=>RebirthHumanAiPolicy.Allowed(actor,target,true,actor!=null&&target!=null&&target==actor.GetRevengeTarget(),false);
    private bool CanAcquire(EntityAlive target)=>RebirthHumanAiPolicy.Allowed(actor,target);
    internal bool CanPursue(EntityAlive target)
    {
        if(CanTarget(target))return true;
        var owner=RebirthHumanAiPolicy.Owner(actor);
        return owner!=null&&owner.AttachedToEntity==null&&
            (actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Follow||actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Guard)&&
            RebirthHumanAiPolicy.Allowed(actor,target,false,false,false)&&owner.CanSee(target);
    }
    public override bool CanExecute()
    {
        selected=null;if(!RebirthHumanAiPolicy.Ready(actor,true))return false;
        if(RebirthHumanAiPolicy.EventSpawn(actor))
        {
            var player=actor.world.GetClosestPlayer(actor.position,120f,false);
            if(CanAcquire(player)){selected=player;return true;}
        }
        float best=float.MaxValue;candidates.Clear();
        // Original scan covers 100 m; eligibility retains 30/10 m mode ranges,
        // the threat bypass, offline defense and owner-relative restrictions.
        actor.world.GetEntitiesInBounds(typeof(EntityAlive),new Bounds(actor.position,Vector3.one*200f),candidates);
        foreach(var entity in candidates)
        {
            var target=entity as EntityAlive;if(!CanAcquire(target))continue;
            float sq=(target.position-actor.position).sqrMagnitude;
            if(sq<best||(sq==best&&selected!=null&&target.entityId<selected.entityId)){best=sq;selected=target;}
        }
        candidates.Clear();return selected!=null;
    }
    public override void Start(){if(!CanAcquire(selected))return;actor.SetAttackTarget(selected,200);ownsTarget=true;}
    public override bool Continue()=>ownsTarget&&actor.GetAttackTarget()==selected&&CanPursue(selected);
    public override void Update(){if(Continue())actor.SetAttackTarget(selected,200);}
    public override void Reset()
    {
        if(ownsTarget&&actor!=null&&actor.GetAttackTarget()==selected)actor.SetAttackTarget(null,0);
        ownsTarget=false;selected=null;candidates.Clear();
    }
}
