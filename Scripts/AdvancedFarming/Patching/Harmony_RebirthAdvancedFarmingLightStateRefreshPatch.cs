#nullable disable

/// <summary>
/// Forces actual SUN-map recalculation when an opaque prefab/composite cover changes state without
/// changing the static block opacity that vanilla LightProcessor normally reads.
/// </summary>
public static class Harmony_RebirthAdvancedFarmingLightStateRefreshPatch
{
    private static readonly System.Collections.Generic.Dictionary<int, bool> s_exposureNeutralPlantLikeByBlockType =
        new System.Collections.Generic.Dictionary<int, bool>(256);

    public struct DoorFeatureStateSignature
    {
        public bool Valid;
        public Vector3i Pos;
        public int Signature;
    }

    public static void Prefix(TEFeatureDoor __instance, ref DoorFeatureStateSignature __state)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
        {
            __state = default(DoorFeatureStateSignature);
            return;
        }

        __state = CaptureDoorFeatureState(__instance);
    }

    public static void Postfix(TEFeatureDoor __instance, DoorFeatureStateSignature __state)
    {
        QueueDoorRefreshIfStateChanged(__instance, __state, false);
    }

    /// <summary>
    /// Door interaction is locally predicted through SetOpen on the client, but the authoritative
    /// server receives that state through NetPackageTileEntity -> TEFeatureDoor.Read(FromClient).
    /// Read assigns isOpen directly and therefore bypasses SetOpen. Patch the deserialization path
    /// so both the server and receiving clients rebuild SUN from the state they actually own.
    /// Persistency reads force one refresh even when the saved state equals the feature default;
    /// otherwise a closed door can inherit stale SUN values saved while the aperture was open.
    /// </summary>
    public static void Postfix_Read(
        TEFeatureDoor __instance,
        StreamModeRead _eStreamMode,
        DoorFeatureStateSignature __state)
    {
        bool forceRefresh = _eStreamMode == StreamModeRead.Persistency;
        QueueDoorRefreshIfStateChanged(__instance, __state, forceRefresh);
    }

    private static void QueueDoorRefreshIfStateChanged(
        TEFeatureDoor doorFeature,
        DoorFeatureStateSignature beforeState,
        bool forceRefresh)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || doorFeature == null ||
            !AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots)
        {
            return;
        }

        Vector3i refreshPos;
        if (!TryResolveDoorFeatureWorldPos(doorFeature, out refreshPos))
            return;

        DoorFeatureStateSignature afterState = CaptureDoorFeatureState(doorFeature);
        if (!forceRefresh
            && beforeState.Valid
            && afterState.Valid
            && SamePosition(beforeState.Pos, afterState.Pos)
            && beforeState.Signature == afterState.Signature)
        {
            return;
        }

        AdvancedFarmingDynamicLightOpacityService.RefreshSunlightAroundDoorState(refreshPos, 5);
    }

    public static void Postfix_ChunkClusterSetBlock(BlockValue __result, Vector3i _pos, BlockValue _bv, bool _isUpdateLight)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        // __result is the old block value returned by ChunkCluster.SetBlock. v105 correctly
        // widened exposure-cache invalidation to all real geometry changes before raising TTLs.
        // v106 keeps that conservative behavior for structural/dynamic-cover blocks, but skips
        // exposure-neutral crop stage swaps so accelerated plant growth does not evict every
        // cached crop exposure result.
        if (IsSameRawBlockState(__result, _bv))
            return;

        AdvancedFarmingWaterProviderRegistry.InvalidateResolvedEntryAt(_pos);

        // SetBlock is global and extremely hot. Outside a registered farm's
        // influence, Advanced Farming has no cache or crop state to invalidate.
        if (!AdvancedFarmingActiveAreaRegistry.IsAreaActive(_pos, 16, 24))
            return;

        if (_pos.y <= 0 || _pos.y >= byte.MaxValue)
            return;

        if (IsExposureNeutralPlantLikeSwap(__result, _bv))
        {
            AdvancedFarmingPerfSnapshotService.RecordExposureInvalidationBlockChange(true, false, false, false);
            return;
        }

        AdvancedFarmingLightService.NotifyWorldBlockChanged(_pos);
        AdvancedFarmingTemperatureService.NotifyWorldBlockChanged(_pos);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        IBlockAccess blockAccess = world != null ? (IBlockAccess)world : null;

        bool oldDynamic = AdvancedFarmingDynamicLightOpacityService.HasDynamicLightOpacity(blockAccess, _pos, __result);
        bool newDynamic = AdvancedFarmingDynamicLightOpacityService.HasDynamicLightOpacity(blockAccess, _pos, _bv);
        bool queueSunRefresh = oldDynamic || newDynamic;
        AdvancedFarmingPerfSnapshotService.RecordExposureInvalidationBlockChange(false, true, true, queueSunRefresh);
        if (queueSunRefresh)
        {
            AdvancedFarmingDynamicLightOpacityService.RefreshSunlightAroundDoorState(_pos, 5);
            return;
        }

        // Ordinary full blocks use vanilla's light update rather than the custom dynamic-cover
        // rebuild. Queue a bounded light-state-only crop refresh after vanilla chunk lighting has
        // settled so placement/removal does not wait for the next 10-15 second crop wake.
        if (_isUpdateLight)
            AdvancedFarmingSyncService.QueueGeometryDrivenPlantLightRefresh(_pos, 5, 8);
    }


    public static bool IsExposureNeutralPlantLikeSwap(BlockValue oldValue, BlockValue newValue)
    {
        return IsExposureNeutralPlantLikeBlock(oldValue) && IsExposureNeutralPlantLikeBlock(newValue);
    }

    public static bool IsExposureNeutralPlantLikeBlock(BlockValue blockValue)
    {
        if (blockValue.isair)
            return true;

        Block block = blockValue.Block;
        if (block == null)
            return false;

        int blockType = blockValue.type;
        bool cached;
        if (s_exposureNeutralPlantLikeByBlockType.TryGetValue(blockType, out cached))
            return cached;

        bool result = ComputeExposureNeutralPlantLikeBlock(block);
        s_exposureNeutralPlantLikeByBlockType[blockType] = result;
        return result;
    }

    private static bool ComputeExposureNeutralPlantLikeBlock(Block block)
    {
        if (block == null)
            return false;

        string name = block.GetBlockName();
        if (string.IsNullOrEmpty(name))
            return false;

        if (StringContainsOrdinalIgnoreCase(name, "farmplot") || StringContainsOrdinalIgnoreCase(name, "farmPlot"))
            return false;

        bool nameLooksPlantLike = StringContainsOrdinalIgnoreCase(name, "plant")
            || StringContainsOrdinalIgnoreCase(name, "crop")
            || StringContainsOrdinalIgnoreCase(name, "seed")
            || StringContainsOrdinalIgnoreCase(name, "mushroom");
        if (!nameLooksPlantLike)
            return false;

        int opacity = block.lightOpacity;
        if (opacity > 0)
            return false;

        return true;
    }

    private static bool StringContainsOrdinalIgnoreCase(string value, string token)
    {
        return !string.IsNullOrEmpty(value)
            && !string.IsNullOrEmpty(token)
            && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }


    private static DoorFeatureStateSignature CaptureDoorFeatureState(TEFeatureDoor doorFeature)
    {
        DoorFeatureStateSignature result = new DoorFeatureStateSignature();
        Vector3i pos;
        if (!TryResolveDoorFeatureWorldPos(doorFeature, out pos))
            return result;

        result.Pos = pos;
        result.Signature = ComputeDoorLikeMovementSignature(pos);
        result.Valid = result.Signature != int.MinValue;
        return result;
    }

    private static int ComputeDoorLikeMovementSignature(Vector3i pos)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        IBlockAccess blockAccess = world != null ? (IBlockAccess)world : null;
        if (blockAccess == null)
            return int.MinValue;

        try
        {
            unchecked
            {
                int signature = 17;
                signature = signature * 397 ^ ComputeSingleCellLightSignature(blockAccess, pos);

                // Composite/multiblock doors can report a feature state change from a child
                // position while the actual light-blocking cell is the parent or an adjacent
                // door cell. Include a small neighborhood in the no-op signature so a state
                // change that alters any queued cover face opacity still queues a rebuild.
                for (int i = 0; i < Vector3i.AllDirections.Length; i++)
                {
                    Vector3i dir = Vector3i.AllDirections[i];
                    Vector3i neighbor = new Vector3i(pos.x + dir.x, pos.y + dir.y, pos.z + dir.z);
                    signature = signature * 397 ^ ComputeSingleCellLightSignature(blockAccess, neighbor);
                }

                return signature;
            }
        }
        catch
        {
            // If the signature cannot be computed, do not suppress the refresh.
            return int.MinValue;
        }
    }

    private static int ComputeSingleCellLightSignature(IBlockAccess blockAccess, Vector3i pos)
    {
        if (blockAccess == null || pos.y <= 0 || pos.y >= byte.MaxValue)
            return 0;

        BlockValue blockValue = blockAccess.GetBlock(pos);
        return AdvancedFarmingDynamicLightOpacityService.ComputeCoverFaceOpacitySignature(blockAccess, pos, blockValue);
    }

    private static BlockFaceFlag DirectionToFace(Vector3i dir)
    {
        if (dir.x > 0) return BlockFaceFlag.East;
        if (dir.x < 0) return BlockFaceFlag.West;
        if (dir.y > 0) return BlockFaceFlag.Top;
        if (dir.y < 0) return BlockFaceFlag.Bottom;
        if (dir.z > 0) return BlockFaceFlag.North;
        if (dir.z < 0) return BlockFaceFlag.South;
        return BlockFaceFlag.None;
    }

    private static bool SamePosition(Vector3i a, Vector3i b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }

    private static bool IsSameRawBlockState(BlockValue oldValue, BlockValue newValue)
    {
        return oldValue.rawData == newValue.rawData && oldValue.damage == newValue.damage;
    }

    private static bool TryResolveDoorFeatureWorldPos(TEFeatureDoor doorFeature, out Vector3i worldPos)
    {
        worldPos = Vector3i.zero;
        if (doorFeature == null)
            return false;

        TileEntityComposite parent = doorFeature.Parent;
        if (parent != null)
        {
            worldPos = parent.ToWorldPos();
            if (worldPos.y > 0 && worldPos.y < byte.MaxValue)
                return true;
        }

        worldPos = doorFeature.ToWorldPos();
        return worldPos.y > 0 && worldPos.y < byte.MaxValue;
    }
}
