using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class EAIRebirthRetaliate : EAITarget
{
    private EntityRebirthNPC actor;private EntityAlive selected;private float view;private int ticks;
    public override void Init(EntityAlive entity){base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=1;executeDelay=.1f;}
    public override bool CanExecute()
    {
        selected=null;if(!RebirthHumanAiPolicy.Ready(actor,true))return false;
        var revenge=actor.GetRevengeTarget();var attack=actor.GetAttackTarget();
        if(revenge==null||revenge==attack||!RebirthHumanAiPolicy.Allowed(actor,revenge,false,true,false))return false;
        if(attack!=null&&!attack.IsDead()&&RandomFloat<.66f){actor.SetRevengeTarget(null);return false;}
        if(base.check(revenge)){selected=revenge;return true;}
        float radius=actor.EntityClass.SearchRadius;
        Vector3 direction=(actor.position-revenge.position).normalized;
        Vector3 position=revenge.position+direction*(radius*.35f)+manager.random.RandomOnUnitSphere*radius;position.y=revenge.position.y;
        int height=(int)actor.world.GetHeight((int)position.x,(int)position.z);if(height>0)position.y=height;
        int duration=actor.CalcInvestigateTicks(1200,revenge);actor.SetInvestigatePosition(position,duration,true);actor.SetAlertTicks(duration);actor.SetRevengeTarget(null);return false;
    }
    public override void Start(){view=actor.GetMaxViewAngle();ticks=100;actor.SetMaxViewAngle(270f);actor.SetAttackTarget(selected,400);base.Start();}
    public override bool Continue()=>RebirthHumanAiPolicy.Ready(actor,true)&&actor.GetAttackTarget()==selected&&
        RebirthHumanAiPolicy.Allowed(actor,selected,false,true,false)&&(actor.GetRevengeTarget()==null||actor.GetRevengeTarget()==selected)&&base.Continue();
    public override void Update(){if(ticks>0&&--ticks==0)Restore();}
    private void Restore(){if(actor!=null&&view>0f)actor.SetMaxViewAngle(view);view=0f;}
    public override void Reset(){if(actor!=null&&actor.GetAttackTarget()==selected)base.Reset();Restore();selected=null;}
}
