using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthFireGameplayPatches
{
    private static readonly List<Vector3i> ExtinguishBuffer = new List<Vector3i>();
    private static readonly List<Vector3i> IgniteBuffer = new List<Vector3i>();


    public static void ExplosionAttackBlocksPostfix(
        Explosion __instance,
        int _entityThatCausedExplosion,
        ItemValue _itemValueExplosionSource)
    {
        #if DEBUG
        RebirthFireDiagnostics.ExplosionPatchCalls++;
        #endif
        if (__instance == null || !RebirthFireRuntimePolicy.Enabled)
        {
            #if DEBUG
            RebirthFireDiagnostics.ExplosionSkippedDisabled++;
            #endif
            return;
        }
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer)
        {
            #if DEBUG
            RebirthFireDiagnostics.ExplosionSkippedAuthority++;
            #endif
            return;
        }

        ExplosionData data = __instance.explosionData;
        if (data.BlockDamage == 0f || data.ParticleIndex == 0 || data.ParticleIndex == 36)
        {
            #if DEBUG
            RebirthFireDiagnostics.ExplosionSkippedInvalidDamageOrParticle++;
            #endif
            return;
        }

        World world = __instance.world;
        if (world == null)
            return;

        EntityAlive source = world.GetEntity(_entityThatCausedExplosion) as EntityAlive;
        if (source != null)
        {
            if (source.EntityClass.Properties.Contains("SpreadFire") &&
                !source.EntityClass.Properties.GetBool("SpreadFire"))
            {
                #if DEBUG
                RebirthFireDiagnostics.ExplosionSkippedSpreadPolicy++;
                #endif
                return;
            }
            if (source.Buffs.HasCustomVar("SpreadFire") && source.Buffs.GetCustomVar("SpreadFire") == -1f)
            {
                #if DEBUG
                RebirthFireDiagnostics.ExplosionSkippedSpreadPolicy++;
                #endif
                return;
            }
        }

        if (data.BlockDamage < 0f)
        {
            RebirthFireService.Instance.CollectBurningWithin(
                __instance.blockPos,
                Mathf.Max(1f, data.BlockRadius),
                ExtinguishBuffer);
            RebirthFireService.Instance.ExtinguishMany(
                world,
                ExtinguishBuffer,
                RebirthFireDefaults.SmokeSeconds);
            return;
        }

        if (__instance.ChangedBlockPositions == null || __instance.ChangedBlockPositions.Count == 0)
        {
            #if DEBUG
            RebirthFireDiagnostics.ExplosionNoChangedBlocks++;
            #endif
            return;
        }
        #if DEBUG
        RebirthFireDiagnostics.ExplosionChangedBlockPositions += __instance.ChangedBlockPositions.Count;
        #endif

        RebirthFireIgnitionCause cause = IsMolotov(_itemValueExplosionSource)
            ? RebirthFireIgnitionCause.Molotov
            : RebirthFireIgnitionCause.Explosion;

        IgniteBuffer.Clear();
        foreach (KeyValuePair<Vector3i, BlockChangeInfo> pair in __instance.ChangedBlockPositions)
        {
            if (IgniteBuffer.Count >= RebirthFireDefaults.MaxRequestPositions)
                break;
            IgniteBuffer.Add(pair.Key);
        }
        #if DEBUG
        RebirthFireDiagnostics.ExplosionScheduledPositions += IgniteBuffer.Count;
        #endif
        RebirthFireService.Instance.ScheduleIgnition(
            world,
            IgniteBuffer,
            _entityThatCausedExplosion,
            cause,
            0f);
    }

    private const float LocalContactRequestCooldownSeconds = 0.5f;
    private static Vector3i lastLocalContactPosition;
    private static float lastLocalContactRequestTime = -1000f;
    private static bool hasLastLocalContactPosition;

    public static void BlockOnEntityWalkingPostfix(
        WorldBase _world,
        int _x,
        int _y,
        int _z,
        BlockValue _blockValue,
        Entity entity)
    {
        #if DEBUG
        RebirthFireDiagnostics.ContactWalkCalls++;
        #endif
        if (!RebirthFireRuntimePolicy.Enabled || entity == null)
            return;

        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer)
            return;

        // Player ignition is handled from that player's local visual update. This guarantees
        // an instantiated particle at contact time and also catches a fire that starts beneath
        // a stationary player. The authoritative walking hook remains for non-player entities.
        if (entity is EntityPlayer)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactServerPlayerWalkSuppressed++;
            #endif
            return;
        }

        Vector3i position = new Vector3i(_x, _y, _z);
        if (!RebirthFireService.Instance.ShouldApplyContactBuff(entity, position))
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedInvisible++;
            #endif
            return;
        }

        ApplyAuthoritativeContactBuff(entity, _blockValue);
    }

    public static void TryHandleLocalVisibleContact(EntityPlayerLocal localPlayer, Vector3i position)
    {
        if (!RebirthFireRuntimePolicy.Enabled || localPlayer == null)
            return;
        if (!RebirthFireVisualManager.IsFireParticleVisible(position))
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;

        RebirthFireProfile localProfile = RebirthFireProfileRegistry.Resolve(world.GetBlock(position));
        string localBuff = localProfile.ContactBuff;
        if (string.IsNullOrEmpty(localBuff) || BuffManager.GetBuff(localBuff) == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedInvalidBuff++;
            #endif
            return;
        }
        if (localPlayer.Buffs.HasBuff(localBuff))
            return;

        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null)
            return;

        float now = Time.realtimeSinceStartup;
        if (hasLastLocalContactPosition &&
            PositionsEqual(lastLocalContactPosition, position) &&
            now - lastLocalContactRequestTime < LocalContactRequestCooldownSeconds)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactLocalRateLimited++;
            #endif
            return;
        }

        hasLastLocalContactPosition = true;
        lastLocalContactPosition = position;
        lastLocalContactRequestTime = now;

        if (localPlayer.Buffs.HasBuff("FuriousRamsayResistFire"))
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedImmune++;
            #endif
            return;
        }

        if (manager.IsServer)
        {
            if (!RebirthFireService.Instance.IsBurning(position))
            {
                #if DEBUG
                RebirthFireDiagnostics.ContactRejectedNoFire++;
                #endif
                return;
            }

            #if DEBUG
            RebirthFireDiagnostics.ContactHostDirect++;
            #endif
            ApplyAuthoritativeContactBuff(localPlayer, world.GetBlock(position));
            return;
        }

        #if DEBUG
        RebirthFireDiagnostics.ContactClientRequests++;
        #endif
        manager.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthFireContactRequest>()
                .Setup(localPlayer.entityId, position),
            false);
    }

    public static bool ApplyAuthoritativeContactBuff(Entity entity, BlockValue blockValue)
    {
        EntityAlive alive = entity as EntityAlive;
        if (alive == null)
            return false;
        if (alive.Buffs.HasBuff("FuriousRamsayResistFire"))
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedImmune++;
            #endif
            return false;
        }

        RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(blockValue);
        string buff = profile.ContactBuff;
        if (string.IsNullOrEmpty(buff) || BuffManager.GetBuff(buff) == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedInvalidBuff++;
            #endif
            return false;
        }
        if (alive.Buffs.HasBuff(buff))
            return true;

        alive.Buffs.AddBuff(buff, -1, true, false);
        #if DEBUG
        RebirthFireDiagnostics.ContactBuffApplied++;
        #endif
        return true;
    }

    private static bool PositionsEqual(Vector3i left, Vector3i right)
    {
        return left.x == right.x && left.y == right.y && left.z == right.z;
    }

    public static void ChunkSetBlockPostfix(
        Chunk __instance,
        int x,
        int y,
        int z,
        bool _fromReset)
    {
        if (!_fromReset || __instance == null)
            return;
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer)
            return;

        Vector3i position = new Vector3i(
            __instance.GetBlockWorldPosX(x),
            y,
            __instance.GetBlockWorldPosZ(z));
        RebirthFireService.Instance.ClearForPoiReset(position);
    }



    private static bool IsMolotov(ItemValue itemValue)
    {
        if (itemValue == null || itemValue.ItemClass == null)
            return false;
        string name = itemValue.ItemClass.GetItemName();
        return !string.IsNullOrEmpty(name) && name.IndexOf("molotov", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public static class RebirthFireStabilityPatches
{
    public static void WorldAddFallingBlockPrefix(World __instance, Vector3i _blockPos)
    {
        if (__instance == null || __instance.IsRemote())
            return;

        // The world block is about to leave its coordinate and become an EntityFallingBlock.
        // Remove authoritative fire state now instead of waiting for the next simulation tick.
        RebirthFireService.Instance.Remove(_blockPos);

        // A non-dedicated server also owns local visuals. Remove every local reference and
        // queued particle immediately so the old flame cannot remain suspended in mid-air.
        RebirthFireVisualManager.RemovePositionImmediately(_blockPos);

        RebirthFireSleeperActivation.OnFallingBlock(__instance, _blockPos);
    }
}
