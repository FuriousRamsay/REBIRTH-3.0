using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthHoldAnchor : EAIBase
{
    private EntityRebirthNPC actor;private float nextPath;private Vector3 anchor;private float facing;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=.1f;}
    private bool Ready()=>RebirthHumanAiPolicy.Ready(actor)&&!RebirthHumanAiPolicy.HasCombat(actor)&&!actor.Buffs.HasBuff("buffTalkingTo")&&
        RebirthHumanAiPolicy.TryAnchor(actor,out anchor,out facing);
    public override bool CanExecute()=>Ready();
    public override bool Continue()=>Ready();
    public override void Start(){nextPath=0f;RebirthNpcAiMovementTrace.Task(actor,"hold","start",anchor);}
    public override void Update()
    {
        if(!Ready())return;
        if((actor.position-anchor).sqrMagnitude>.25f)
        {
            if(Time.time>=nextPath){nextPath=Time.time+.75f;RebirthNpcAiMovementTrace.Task(actor,"hold","path",anchor);actor.FindPath(anchor,actor.GetMoveSpeedPanic(),false,this);}return;
        }
        actor.moveHelper.Stop();
        if(RebirthHumanAiPolicy.Offline(actor))return;
        var look=anchor+Quaternion.Euler(0f,facing,0f)*Vector3.forward*4f;
        actor.SetLookPosition(look);actor.RotateTo(look.x,look.y+2f,look.z,8f,8f);
    }
    public override void Reset(){RebirthNpcAiMovementTrace.Task(actor,"hold","end");if(actor!=null)actor.moveHelper.Stop();}
}
