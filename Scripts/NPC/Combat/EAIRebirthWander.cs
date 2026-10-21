using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthWander : EAIBase
{
    private EntityRebirthNPC actor;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;}
    private bool Ready()=>RebirthHumanAiPolicy.Ready(actor)&&!RebirthHumanAiPolicy.Owned(actor)&&
        actor.RebirthRuntimeState.OwnershipKind==RebirthNpcOwnershipKind.None&&actor.RebirthRuntimeState.Order==RebirthNpcOrderState.None&&
        actor.GetAttackTarget()==null&&!actor.Buffs.HasBuff("buffTalkingTo")&&actor.GetTicksNoPlayerAdjacent()<120;
    public override bool CanExecute()=>Ready()&&GetRandom(1000)<(int)(200f*executeWaitTime);
    public override void Start()
    {
        if(Ready()&&RandomPositionGenerator.CalcAround(actor,10,10,actor.isSwimming,out var destination)&&destination.y!=0f)
            actor.FindPath(destination,actor.GetMoveSpeed(),false,this);
    }
    public override bool Continue()=>Ready()&&actor.moveHelper.BlockedTime<=.3f&&!actor.getNavigator().noPathAndNotPlanningOne();
    public override void Reset(){manager.lookTime=RandomFloat*3f;if(actor!=null)actor.moveHelper.Stop();}
}
