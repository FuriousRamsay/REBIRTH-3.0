using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class EAIRebirthRangedCombat : EAIBase
{
    private EntityRebirthNPC actor;
    private EAIRebirthSelectHostileTarget gate;
    private float nextPath,nextShot,nextReload;
    private Vector3 lastPathTarget;
    private bool hasPathTarget;
    private float nextRetreat;private int retreatLeash;
    private Animator backingAnimator;private bool hasBackingParameter;private bool backing;
    private readonly System.Collections.Generic.List<Entity> shotCorridor=new System.Collections.Generic.List<Entity>();
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=3;executeDelay=0.05f;
        gate=new EAIRebirthSelectHostileTarget();gate.Init(entity);
    }
    private bool TryWeapon(out ItemActionRanged action,out ItemActionRanged.ItemActionDataRanged data,out ItemValue item)
    {
        action=null;data=null;item=actor?.inventory?.holdingItemItemValue;
        var actions=item?.ItemClass?.Actions;
        if(actions==null||actions.Length==0||actions[0] is ItemActionCatapult||actions[0] is ItemActionLauncher)return false;
        action=actions[0] as ItemActionRanged;
        var actionData=actor.inventory.holdingItemData?.actionData;
        if(action==null||actionData==null||actionData.Count==0)return false;
        data=actionData[0] as ItemActionRanged.ItemActionDataRanged;
        if(data==null||!ReferenceEquals(data.invData,actor.inventory.holdingItemData)||data.invData?.holdingEntity!=actor||data.invData.itemValue==null)return false;
        // Playerlike NPCs use actual rounds, never a native infinite-ammunition override.
        return !action.HasInfiniteAmmo(data);
    }
    public override bool CanExecute()
    {RebirthHumanWeaponSelection.Tick(actor);ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;return actor!=null&&gate.CanTarget(actor.GetAttackTarget())&&TryWeapon(out action,out data,out item);}
    public override bool Continue(){return CanExecute();}
    public override void Start(){nextPath=nextShot=nextReload=Time.time;nextRetreat=0f;retreatLeash=0;hasPathTarget=false;RebirthNpcAiMovementTrace.Task(actor,"ranged","start",actor.GetAttackTarget()?.position);}
    public override void Update()
    {
        if(!Continue())return;
        ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;
        if(!TryWeapon(out action,out data,out item))return;
        var target=actor.GetAttackTarget();actor.RotateTo(target,30f,30f);
        bool retreating=Retreat(target);
        if(ItemActionRanged.Reloading(data)){if(!retreating){hasPathTarget=false;actor.moveHelper.Stop();}RebirthNpcAiMovementTrace.RangedDecision(actor,"reloading",-1f,-1f);return;}
        if(item.Meta<=0)
        {
            if(!retreating){hasPathTarget=false;actor.moveHelper.Stop();}
            if(Time.time<nextReload||actor.bag==null||action.MagazineItemNames==null||item.SelectedAmmoTypeIndex>=action.MagazineItemNames.Length){RebirthNpcAiMovementTrace.RangedDecision(actor,"reload-unavailable",-1f,-1f);return;}
            nextReload=Time.time+1f;
            if(action.CanReload(data))GameManager.Instance.ItemReloadServer(actor.entityId);
            RebirthNpcAiMovementTrace.RangedDecision(actor,"reload-request",-1f,-1f);
            return;
        }
        float range=action.Range;
        if(range==0f)range=EffectManager.GetItemValue(PassiveEffects.MaxRange,item);
        if(float.IsNaN(range)||float.IsInfinity(range)||range<=0f){actor.moveHelper.Stop();RebirthNpcAiMovementTrace.RangedDecision(actor,"invalid-range",-1f,-1f);return;}
        range=Math.Min(range,25f);
        float distance=(target.position-actor.position).magnitude;
        if(distance>range)
        {
            RebirthNpcAiMovementTrace.RangedDecision(actor,"chase-range",distance,-1f);
            if(!RebirthHumanAiPolicy.CanChase(actor)){hasPathTarget=false;actor.moveHelper.Stop();return;}
            if(Time.time>=nextPath&&(!hasPathTarget||actor.moveHelper.BlockedTime>=0.75f||
                actor.getNavigator()==null||
                actor.getNavigator().noPathAndNotPlanningOne()||
                (target.position-lastPathTarget).sqrMagnitude>=4f))
            {
                lastPathTarget=target.position;hasPathTarget=true;
                RebirthNpcAiMovementTrace.Task(actor,"ranged","path",lastPathTarget);
                actor.FindPath(lastPathTarget,actor.GetMoveSpeedAggro(),false,this);
                nextPath=Time.time+0.75f;
            }
            return;
        }
        hasPathTarget=false;
        if(!retreating)actor.moveHelper.Stop();
        float delay=data.Delay;
        if(float.IsNaN(delay)||float.IsInfinity(delay)||delay<=0f){RebirthNpcAiMovementTrace.RangedDecision(actor,"invalid-delay",distance,-1f);return;}
        if(Time.time<nextShot){RebirthNpcAiMovementTrace.RangedDecision(actor,"shot-cooldown",distance,-1f);return;}
        if(!actor.IsAttackValid()){RebirthNpcAiMovementTrace.RangedDecision(actor,"attack-invalid",distance,-1f);return;}
        // Rotation acceptance must precede a native shot. Rig/ray convergence needs native qualification.
        Vector3 direction=(target.position+Vector3.up*target.GetEyeHeight()-actor.GetLookRay().origin).normalized;
        float alignment=Vector3.Dot(actor.GetLookVector(),direction);
        if(float.IsNaN(alignment)||float.IsInfinity(alignment)||alignment<0.995f){RebirthNpcAiMovementTrace.RangedDecision(actor,"aim",distance,alignment);return;}
        if(!RebirthNpcFriendlyFireCorridor.IsClear(actor,target,action,data,shotCorridor)){RebirthNpcAiMovementTrace.RangedDecision(actor,"corridor",distance,alignment);return;}
        if(!actor.Attack(false)){RebirthNpcAiMovementTrace.RangedDecision(actor,"attack-rejected",distance,alignment);return;}
        actor.Attack(true);
        RebirthNpcAiMovementTrace.RangedDecision(actor,"shot",distance,alignment);
        nextShot=Time.time+delay;
    }
    private bool Retreat(EntityAlive target)
    {
        float limit=RebirthHumanAiRules.RetreatDistance(target is EntityPlayer);
        bool wanted=RebirthHumanAiRules.CanRetreat(RebirthHumanAiPolicy.Owned(actor),actor.RebirthRuntimeState.Order==RebirthNpcOrderState.Follow&&!RebirthHumanAiPolicy.Offline(actor))&&
            actor.GetDistance(target)<limit;
        if(!wanted){SetBacking(false);retreatLeash=0;return false;}
        if(Time.time<nextRetreat)return backing;
        nextRetreat=Time.time+.5f;
        if(retreatLeash==0)retreatLeash=UnityEngine.Random.Range(10,25);
        var owner=RebirthHumanAiPolicy.Owner(actor);Vector3 away=actor.position-target.position;
        for(int attempt=0;attempt<3;attempt++)
        {
            var goal=RandomPositionGenerator.CalcPositionInDirection(actor,target.position,away,limit*3f,80f);
            if(goal==Vector3.zero||goal.y<actor.position.y-3f||goal.y>actor.position.y+3f||
                (owner!=null&&(goal-owner.position).sqrMagnitude>retreatLeash*retreatLeash))continue;
            actor.moveHelper.SetMoveTo(goal,true);actor.SetInvestigatePosition(goal,200);actor.SetLookPosition(target.position);
            SetBacking(true);return true;
        }
        SetBacking(false);return false;
    }
    private void SetBacking(bool value)
    {
        var animator=actor?.emodel?.avatarController?.GetAnimator();
        if(animator!=backingAnimator)
        {
            backingAnimator=animator;hasBackingParameter=false;
            backing=false;
            if(animator!=null)foreach(var parameter in animator.parameters)
                if(parameter.name=="isBackingUp"&&parameter.type==AnimatorControllerParameterType.Bool){hasBackingParameter=true;break;}
        }
        if(backing!=value&&backingAnimator!=null&&hasBackingParameter)backingAnimator.SetBool("isBackingUp",value);
        backing=value;
    }
    public override void Reset(){RebirthNpcAiMovementTrace.Task(actor,"ranged","end");shotCorridor.Clear();SetBacking(false);retreatLeash=0;hasPathTarget=false;if(actor!=null)actor.moveHelper.Stop();}
}

