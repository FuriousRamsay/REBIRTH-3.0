using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthRelocateToTarget : EAIBase
{
    private EntityRebirthNPC actor;private EntityAlive target;private float nextPath;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=.1f;}
    private bool Ready()
    {
        if(!RebirthHumanAiPolicy.Ready(actor,true))return false;
        var owner=RebirthHumanAiPolicy.Owner(actor);target=actor.GetAttackTarget();
        return owner!=null&&owner.AttachedToEntity==null&&target!=null&&!actor.CanSee(target)&&owner.CanSee(target)&&
            (actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Follow||actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Guard)&&
            RebirthHumanAiPolicy.Allowed(actor,target,false,false,false);
    }
    public override bool CanExecute()=>Ready();
    public override bool Continue()=>Ready();
    public override void Start(){nextPath=0f;RebirthNpcAiMovementTrace.Task(actor,"relocate","start",target?.position);}
    public override void Update()
    {
        if(!Ready())return;RebirthHumanDoorRuntime.Check(actor);
        if(Time.time<nextPath)return;nextPath=Time.time+.75f;
        var destination=RandomPositionGenerator.CalcPositionInDirection(actor,target.position,actor.position-target.position,5f,80f);
        if(destination!=Vector3.zero){RebirthNpcAiMovementTrace.Task(actor,"relocate","path",destination);actor.FindPath(destination,actor.GetMoveSpeedAggro(),false,this);}
    }
    public override void Reset(){RebirthNpcAiMovementTrace.Task(actor,"relocate","end");if(actor!=null)actor.moveHelper.Stop();target=null;}
}
