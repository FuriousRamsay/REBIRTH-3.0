using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ItemActionRangedRebirthExtinguisher : ItemActionRanged
{
    private static int nativeShotSequence;
    // The native ranged effect payload has unused userData for this action.
    // This ordinal suppresses duplicate couriers; it grants no fire authority.
    public override int GetActionEffectsValues(ItemActionData data,out Vector3 start,out Vector3 direction)
    {
        base.GetActionEffectsValues(data,out start,out direction);
        if(data?.invData?.world?.IsRemote()!=true)return 0;
        return NextNativeShotOrdinal();
    }
    private static int NextNativeShotOrdinal()
    {
        while(true)
        {
            int prior=System.Threading.Volatile.Read(ref nativeShotSequence);
            if(prior==int.MaxValue)return 0;
            int next=prior+1;
            if(System.Threading.Interlocked.CompareExchange(ref nativeShotSequence,next,prior)==prior)return next;
        }
    }

    internal bool UsesNativeInfiniteSpray=>InfiniteAmmo;
    internal float NativeShotDelay(ItemActionDataRanged data)=>60f/EffectManager.GetValue(PassiveEffects.RoundsPerMinute,
        data.invData.itemValue,60f/data.OriginalDelay,data.invData.holdingEntity);
    public override void ItemActionEffects(GameManager manager,ItemActionData data,int firingState,Vector3 start,Vector3 direction,int userData=0)
    {
        base.ItemActionEffects(manager,data,firingState,start,direction,userData);
        RebirthExtinguisherNativeShot.ObserveActualNativeEffects(this,data,firingState);
    }
    internal void ApplyReplicatedNativeHit(ItemActionDataRanged data)
    {
        var holder=data?.invData?.holdingEntity;
        var world=data?.invData?.world;
        if(holder==null||world==null||world.IsRemote()||holder.IsDead())return;
        float range=GetRange(data);
        if(float.IsNaN(range)||float.IsInfinity(range)||range<=0f)return;
        // Recompute the physical hit on authority. No client position, radius,
        // ignition cause, direction or smoke duration is consumed here.
        Ray ray=holder.GetLookRay();
        int mask=hitmaskOverride==0?8:hitmaskOverride;
        if(!Voxel.Raycast(world,ray,range,-538750997,mask,0f))return;
        var hit=Voxel.voxelRayHitInfo.Clone();
        if(hit==null||hit.hit.distanceSq>range*range)return;
        EntityAlive target=null;BlockValue block=BlockValue.Air;
        if(hit.tag!=null&&hit.tag.StartsWith("E_"))target=ItemActionAttack.FindHitEntityNoTagCheck(hit,out string _) as EntityAlive;
        else block=ItemActionAttack.GetBlockHit(world,hit);
        var context=holder.MinEventContext;var previousItem=context.ItemValue;var previousAction=context.ItemActionData;
        bool previousLocal=context.IsLocal;
        try
        {
            context.ItemValue=data.invData.itemValue;context.ItemActionData=data;
            // Native AddBuff suppresses networking for a remote Self unless
            // this is an actual authority-local event (the native damage path
            // uses the same flag). This scope exists only after server raycast.
            context.IsLocal=true;
            RebirthExtinguisherHitContext.Fire(holder,hit.hit.pos,target,block,
                data.indexInEntityOfAction==0?MinEventTypes.onSelfPrimaryActionRayHit:MinEventTypes.onSelfSecondaryActionRayHit);
        }
        finally{context.ItemValue=previousItem;context.ItemActionData=previousAction;context.IsLocal=previousLocal;}
    }

    public override Vector3 fireShot(
        int _shotIdx,
        ItemActionRanged.ItemActionDataRanged _actionData,
        ref bool hitEntityFound)
    {
        EntityAlive holdingEntity = _actionData.invData.holdingEntity;
        float range = GetRange(_actionData);
        Ray ray = holdingEntity.GetLookRay();
        ray.direction = getDirectionOffset(_actionData, ray.direction, _shotIdx);

        _actionData.waterCollisionParticles.Reset();
        _actionData.waterCollisionParticles.CheckCollision(ray.origin, ray.direction, range, holdingEntity.entityId);

        int hitMask = hitmaskOverride == 0 ? 8 : hitmaskOverride;
        hitEntityFound = false;

        if (!Voxel.Raycast(_actionData.invData.world, ray, range, -538750997, hitMask, 0f))
        {
            holdingEntity.FireEvent(_actionData.indexInEntityOfAction == 0
                ? MinEventTypes.onSelfPrimaryActionRayMiss
                : MinEventTypes.onSelfSecondaryActionRayMiss);
            #if DEBUG
            RebirthExtinguisherHitTrace.RecordDedicatedActionMiss(holdingEntity, this);
            #endif
            return ray.direction;
        }

        WorldRayHitInfo hitInfo = Voxel.voxelRayHitInfo.Clone();
        if (hitInfo == null || hitInfo.hit.distanceSq > range * range)
            return ray.direction;

        EntityAlive hitTarget=null;
        BlockValue hitBlock=BlockValue.Air;
        if (hitInfo.tag != null && hitInfo.tag.StartsWith("E_"))
        {
            EntityAlive entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string _) as EntityAlive;
            hitTarget = entity;
            hitEntityFound = entity != null;
        }
        else
        {
            hitBlock = ItemActionAttack.GetBlockHit(_actionData.invData.world, hitInfo);
        }

        _actionData.attackDetails.isCriticalHit = holdingEntity.AimingGun;
        _actionData.attackDetails.WeaponTypeTag = ItemActionAttack.RangedTag;

        RebirthExtinguisherHitContext.Fire(holdingEntity,hitInfo.hit.pos,hitTarget,hitBlock,
            _actionData.indexInEntityOfAction == 0 ? MinEventTypes.onSelfPrimaryActionRayHit
            : MinEventTypes.onSelfSecondaryActionRayHit);

        #if DEBUG
        RebirthExtinguisherHitTrace.RecordDedicatedActionHit(hitInfo, holdingEntity, this, _actionData.invData.itemValue);
        #endif
        return ray.direction;
    }
}


