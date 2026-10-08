using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ItemActionRangedRebirthExtinguisher : ItemActionRanged
{
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

        if (hitInfo.tag != null && hitInfo.tag.StartsWith("E_"))
        {
            EntityAlive entity = ItemActionAttack.FindHitEntityNoTagCheck(hitInfo, out string _) as EntityAlive;
            holdingEntity.MinEventContext.Other = entity;
            hitEntityFound = entity != null;
        }
        else
        {
            holdingEntity.MinEventContext.BlockValue = ItemActionAttack.GetBlockHit(_actionData.invData.world, hitInfo);
        }

        _actionData.attackDetails.isCriticalHit = holdingEntity.AimingGun;
        _actionData.attackDetails.WeaponTypeTag = ItemActionAttack.RangedTag;

        holdingEntity.FireEvent(_actionData.indexInEntityOfAction == 0
            ? MinEventTypes.onSelfPrimaryActionRayHit
            : MinEventTypes.onSelfSecondaryActionRayHit);

        #if DEBUG
        RebirthExtinguisherHitTrace.RecordDedicatedActionHit(hitInfo, holdingEntity, this, _actionData.invData.itemValue);
        #endif
        return ray.direction;
    }
}
