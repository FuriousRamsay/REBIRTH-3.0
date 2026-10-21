using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class EAIRebirthMeleeCombat : EAIBase
{
    private sealed class AuditCounts{internal long Paths,Attacks;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityAlive,AuditCounts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityAlive,AuditCounts>();
    public static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityAlive npc)
    {
        if(npc==null||!ThreadManager.IsMainThread()||!ReferenceEquals(npc.world,GameManager.Instance?.World)||
            !ReferenceEquals(npc.world.GetEntity(npc.entityId),npc)||!(npc is EntityRebirthNPC))return new Newtonsoft.Json.Linq.JObject{["available"]=false};
        AuditCounts counts;Audit.TryGetValue(npc,out counts);
        return new Newtonsoft.Json.Linq.JObject{["available"]=true,["actor"]=npc.entityId,["hasStamina"]=npc.Stats?.Stamina!=null,
            ["paths"]=counts?.Paths??0,["attacks"]=counts?.Attacks??0,["health"]=npc.Health};
    }
    private EntityRebirthNPC actor;
    private EAIRebirthSelectHostileTarget gate;
    private bool recovering;
    private float nextPath,nextAttack;
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=3;executeDelay=0.05f;
        gate=new EAIRebirthSelectHostileTarget();gate.Init(entity);
    }
    private bool TryRange(out float range)
    {
        range=0f;var value=actor?.inventory?.holdingItemItemValue;var actions=value?.ItemClass?.Actions;
        if(actions==null||actions.Length==0||!(actions[0] is ItemActionMelee||actions[0] is ItemActionDynamicMelee))return false;
        range=actions[0].Range;
        if(range==0f)range=EffectManager.GetItemValue(PassiveEffects.MaxRange,value);
        return !float.IsNaN(range)&&!float.IsInfinity(range)&&range>0f;
    }
    public override bool CanExecute(){RebirthHumanWeaponSelection.Tick(actor);float range;return actor!=null&&gate.CanTarget(actor.GetAttackTarget())&&TryRange(out range);}
    public override bool Continue(){return CanExecute();}
    public override void Start(){nextPath=0f;nextAttack=Time.time;recovering=false;}
    public override void Update()
    {
        if(!Continue())return;
        var target=actor.GetAttackTarget();float range;if(!TryRange(out range))return;
        // Native EntityNPC may allocate Health alone. Optional stamina limits an
        // actor that actually has that stat; its absence does not disable melee.
        var staminaStat=actor.Stats?.Stamina;
        if(staminaStat!=null)
        {
        float stamina=staminaStat.Value,max=staminaStat.ModifiedMax;
        if(float.IsNaN(stamina)||float.IsInfinity(stamina)||float.IsNaN(max)||float.IsInfinity(max)||max<=0f){actor.moveHelper.Stop();return;}
        if(recovering&&RebirthCombatStaminaPolicy.Recovered(stamina,max,0.75f))recovering=false;
        if(!recovering&&RebirthCombatStaminaPolicy.NeedsRecovery(stamina,max,0.33f))recovering=true;
        }
        else recovering=false;
        actor.RotateTo(target,30f,30f);
        if(recovering){actor.moveHelper.Stop();return;}
        float distance=(target.position-actor.position).magnitude;
        // Native approach uses a margin below item range; actual rig/hit timing needs qualification.
        float reach=Math.Max(0.1f,range-0.35f);
        if(distance>reach)
        {
            if(!RebirthHumanAiPolicy.CanChase(actor)){actor.moveHelper.Stop();return;}
            if(Time.time>=nextPath){actor.FindPath(target.position,actor.GetMoveSpeedAggro(),false,this);Audit.GetOrCreateValue(actor).Paths++;nextPath=Time.time+0.75f;}
            return;
        }
        actor.moveHelper.Stop();
        if(Time.time<nextAttack||!actor.IsAttackValid())return;
        if(!actor.Attack(false))return;
        actor.Attack(true);
        Audit.GetOrCreateValue(actor).Attacks++;
        nextAttack=Time.time+Math.Max(0.05f,actor.GetAttackTimeoutTicks()*0.05f);
    }
    public override void Reset(){if(actor!=null)actor.moveHelper.Stop();recovering=false;}
}

