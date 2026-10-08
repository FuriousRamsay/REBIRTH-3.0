using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native 2.6-style vehicle respawn marker for 3.1.
///
/// A fully salvaged vehicle downgrades into the invisible carRespawner_FR block.
/// BlockPlantGrowing registers the due update with WorldBlockTicker, which persists
/// that scheduled update in the owning chunk. When due, the normal placeholder path
/// replaces carsRandomHelper with a random world vehicle.
/// </summary>
[Preserve]
public class BlockRespawnRebirth : BlockPlantGrowing
{
    private const ulong TicksPerMinute = 20UL * 60UL;
    private const ulong MinutesPerRebirthDay = 60UL;

    public override void LateInit()
    {
        base.LateInit();

        // Match the 2.6 respawner: no soil, light, or free-space restrictions.
        fertileLevel = 0;
        lightLevelStay = 0;
        lightLevelGrow = 0;
        isPlantGrowingRandom = false;
        isPlantGrowingIfAnythingOnTop = true;
    }

    public override bool CanGrowOn(
        WorldBase _world,
        Vector3i _blockPos,
        BlockValue _blockValueOfPlant)
    {
        return true;
    }

    public override bool CanPlantStay(
        WorldBase _world,
        Vector3i _blockPos,
        BlockValue _blockValue)
    {
        return true;
    }

    public override ulong GetTickRate()
    {
#if DEBUG && REBIRTH_DEBUG
        if (RebirthVehicleRespawnDebugPolicy.ForceOneTick)
            return 1UL;
#endif

        int respawnDays = RebirthVehicleBlockRespawnRuntimePolicy.Days;
        if (respawnDays <= 0)
            return 1UL;

        // Exact 2.6 timing: option days * 60 minutes * 20 ticks/second * 60 seconds.
        return (ulong)respawnDays * MinutesPerRebirthDay * TicksPerMinute;
    }

    public override void OnBlockAdded(
        WorldBase _world,
        Chunk _chunk,
        Vector3i _blockPos,
        BlockValue _blockValue,
        PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue, _addedByPlayer);

        if (_world.IsRemote() || RebirthVehicleBlockRespawnRuntimePolicy.Enabled)
            return;

        // 2.6 converted the marker into a custom air block when respawns were Off.
        // 3.1 can remove it directly without retaining an inert invisible block.
        _world.GetWBT().InvalidateScheduledBlockUpdate(_blockPos, blockID);
        _world.SetBlockRPC((BlockValueRef)_blockPos, BlockValue.Air);
    }

    public override int OnBlockDamaged(
        WorldBase _world,
        BlockValueRef _bvRef,
        BlockValue _blockValue,
        int _damagePoints,
        int _entityIdThatDamaged,
        ItemActionAttack.AttackHitInfo _attackHitInfo,
        bool _bUseHarvestTool,
        bool _bBypassMaxDamage,
        int _recDepth = 0)
    {
        // 3.1 vehicle blocks such as burntSedan01 enable PassThroughDamage.
        // When the final salvage hit exceeds the vehicle's remaining health,
        // Block.OnBlockDamaged recursively applies that excess damage to the
        // newly installed downgrade block. Without this guard, carRespawner_FR
        // can be destroyed in the same damage call that created it.
        //
        // Only recursive/pass-through damage is ignored. A direct hit against an
        // already existing marker still follows the normal BlockPlantGrowing path.
        if (_recDepth > 0)
        {
#if DEBUG && REBIRTH_DEBUG
            RebirthVehicleRespawnDebugPolicy.RecordSuppressedPassThroughDamage(
                _bvRef.BlockPosition,
                _damagePoints,
                _recDepth);

            Log.Out(
                "[REBIRTH Vehicle Respawn][Debug] suppressed marker pass-through damage="
                + _damagePoints
                + " recDepth=" + _recDepth
                + " pos=" + _bvRef.BlockPosition);
#endif
            return _blockValue.damage;
        }

        return base.OnBlockDamaged(
            _world,
            _bvRef,
            _blockValue,
            _damagePoints,
            _entityIdThatDamaged,
            _attackHitInfo,
            _bUseHarvestTool,
            _bBypassMaxDamage,
            _recDepth);
    }

    public override bool UpdateTick(
        WorldBase _world,
        Vector3i _blockPos,
        BlockValue _blockValue,
        bool _bRandomTick,
        ulong _ticksIfLoaded,
        GameRandom _rnd)
    {
        if (!RebirthVehicleBlockRespawnRuntimePolicy.Enabled)
        {
            if (!_world.IsRemote())
                _world.SetBlockRPC((BlockValueRef)_blockPos, BlockValue.Air);
            return true;
        }

        bool result = base.UpdateTick(
            _world,
            _blockPos,
            _blockValue,
            _bRandomTick,
            _ticksIfLoaded,
            _rnd);

        if (!_world.IsRemote())
        {
            BlockValue spawned = _world.GetBlock(_blockPos);
            if (!spawned.isair && spawned.Block != null && spawned.type != blockID)
            {
                string previousDowngrade;
                bool repeatable = RebirthVehicleRespawnRuntimeRegistry.EnsureSpawnedBlockIsRepeatable(
                    spawned.Block,
                    out previousDowngrade);

#if DEBUG && REBIRTH_DEBUG
                RebirthVehicleRespawnDebugPolicy.RecordRespawnedBlock(
                    spawned.Block.blockName,
                    previousDowngrade,
                    repeatable);

                Log.Out(
                    "[REBIRTH Vehicle Respawn][Debug] spawned=" + spawned.Block.blockName
                    + " class=" + spawned.Block.GetType().Name
                    + " previousDowngrade=" + previousDowngrade
                    + " repeatable=" + repeatable);
#endif
            }
        }

        return result;
    }
}
