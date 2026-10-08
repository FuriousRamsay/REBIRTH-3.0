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
    private readonly System.Collections.Generic.List<Entity> shotCorridor=new System.Collections.Generic.List<Entity>();
    public override void Init(EntityAlive entity)
    {
        base.Init(entity);actor=entity as EntityRebirthNPC;MutexBits=11;executeDelay=0.05f;
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
    {ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;return actor!=null&&gate.CanTarget(actor.GetAttackTarget())&&TryWeapon(out action,out data,out item);}
    public override bool Continue(){return CanExecute();}
    public override void Start(){nextPath=nextShot=nextReload=Time.time;}
    public override void Update()
    {
        if(!Continue())return;
        ItemActionRanged action;ItemActionRanged.ItemActionDataRanged data;ItemValue item;
        if(!TryWeapon(out action,out data,out item))return;
        var target=actor.GetAttackTarget();actor.RotateTo(target,30f,30f);
        if(ItemActionRanged.Reloading(data)){actor.moveHelper.Stop();return;}
        if(item.Meta<=0)
        {
            actor.moveHelper.Stop();
            if(Time.time<nextReload||actor.bag==null||action.MagazineItemNames==null||item.SelectedAmmoTypeIndex>=action.MagazineItemNames.Length)return;
            nextReload=Time.time+1f;
            if(action.CanReload(data))GameManager.Instance.ItemReloadServer(actor.entityId);
            return;
        }
        float range=action.Range;
        if(range==0f)range=EffectManager.GetItemValue(PassiveEffects.MaxRange,item);
        if(float.IsNaN(range)||float.IsInfinity(range)||range<=0f){actor.moveHelper.Stop();return;}
        range=Math.Min(range,25f);
        float distance=(target.position-actor.position).magnitude;
        if(distance>range)
        {
            if(Time.time>=nextPath){actor.FindPath(target.position,actor.GetMoveSpeedAggro(),false,this);nextPath=Time.time+0.75f;}
            return;
        }
        actor.moveHelper.Stop();
        float delay=data.Delay;
        if(float.IsNaN(delay)||float.IsInfinity(delay)||delay<=0f||Time.time<nextShot||!actor.IsAttackValid())return;
        // Rotation acceptance must precede a native shot. Rig/ray convergence needs native qualification.
        Vector3 direction=(target.position+Vector3.up*target.GetEyeHeight()-actor.GetLookRay().origin).normalized;
        float alignment=Vector3.Dot(actor.GetLookVector(),direction);
        if(float.IsNaN(alignment)||float.IsInfinity(alignment)||alignment<0.995f)return;
        if(!RebirthNpcFriendlyFireCorridor.IsClear(actor,target,action,data,shotCorridor))return;
        if(!actor.Attack(false))return;
        actor.Attack(true);
        nextShot=Time.time+delay;
    }
    public override void Reset(){shotCorridor.Clear();if(actor!=null)actor.moveHelper.Stop();}
}