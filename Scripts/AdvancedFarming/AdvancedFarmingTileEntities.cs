#nullable disable

public enum AdvancedFarmingAuthoritativeLightState : byte
{
    Unknown = 0,
    Blocked = 1,
    Pass = 2
}

public class TileEntityPlantGrowingRebirth : TileEntity
{
    private const ushort PersistencyVersionFamilyMask = 0xFF00;
    private const ushort PersistencyVersionFamily = 0xC700;
    private const ushort PersistencyVersionWithProvenance = 0xC701;
    private const ushort PersistencyVersionWithIncarnation = 0xC702;
    private static readonly object PlantIncarnationSync = new object();
    private static ulong s_nextPlantIncarnation = 1UL;
    private const ulong WateredFlagMask = 0x8000000000000000UL;
    private const ulong AuthoritativeLightValidFlagMask = 0x4000000000000000UL;
    private const ulong AuthoritativeLightPassFlagMask = 0x2000000000000000UL;
    private const ulong AccumulatedTicksMask = 0x1FFFFFFFFFFFFFFFUL;

    public ulong GameTimerTicks;
    public ulong AccumulatedTicks;
    public bool bUpdating;
    public bool bWatered;
    public int LastProcessedWorldSeconds;
    public AdvancedFarmingAuthoritativeLightState AuthoritativeLightState;
    public ulong PlantIncarnation;
    public ulong StateRevision;

    // PC017 provenance payload. These fields carry only authored planting state; they do not
    // apply the Farmer growth bonus until the later farming-effect chunk consumes them.
    public string RebirthGrowerStableId = string.Empty;
    public string RebirthGrowerBackgroundId = string.Empty;
    public string RebirthGrowerBonusId = string.Empty;
    public float RebirthPlantingSkillValue;
    public ulong RebirthProvenanceWorldTime;

    public TileEntityPlantGrowingRebirth(Chunk _chunk) : base(_chunk)
    {
        GameTimerTicks = GameTimer.Instance.ticks;
        LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
#if DEBUG
        if (AdvancedFarmingDebug.TileEntity)
            AdvancedFarmingDebug.Log("tileentity", "constructed PlantGrowing tile entity.");
#endif
    }

    public override TileEntityType GetTileEntityType()
    {
        return (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityPlantGrowingRebirth;
    }

    public override void OnLoad()
    {
        base.OnLoad();
        AdvancedFarmingHoverTextService.InvalidatePlantState(ToWorldPos());
        AdvancedFarmingDynamicLightOpacityService.QueueLoadedCropChunkSunlightReconciliation(ToWorldPos());
    }

    public void SanitizeTimingBaselines()
    {
        ulong nowTicks = GameTimer.Instance.ticks;
        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();

        if (GameTimerTicks == 0UL || (nowTicks > 0UL && GameTimerTicks > nowTicks))
            GameTimerTicks = nowTicks;

        if (LastProcessedWorldSeconds <= 0 || LastProcessedWorldSeconds > nowSeconds)
            LastProcessedWorldSeconds = nowSeconds;

        bUpdating = false;
    }

    public bool SetAuthoritativeLightPass(bool pass)
    {
        AdvancedFarmingAuthoritativeLightState nextState = pass
            ? AdvancedFarmingAuthoritativeLightState.Pass
            : AdvancedFarmingAuthoritativeLightState.Blocked;
        if (AuthoritativeLightState == nextState)
            return false;

        AuthoritativeLightState = nextState;
        return true;
    }

    public void BeginNewPlantIncarnation()
    {
        PlantIncarnation = AllocatePlantIncarnation();
        StateRevision = 1UL;
    }

    public void EnsureAuthoritativeIncarnation()
    {
        if (PlantIncarnation == 0UL)
            PlantIncarnation = AllocatePlantIncarnation();
        ObservePlantIncarnation(PlantIncarnation);
        if (StateRevision == 0UL)
            StateRevision = 1UL;
    }

    public void AdoptStageTransitionIdentity(ulong incarnation, ulong revision)
    {
        PlantIncarnation = incarnation;
        StateRevision = revision;
        EnsureAuthoritativeIncarnation();
        AdvanceStateRevision();
    }

    public void AdvanceStateRevision()
    {
        EnsureAuthoritativeIncarnation();
        if (StateRevision == ulong.MaxValue)
        {
            // Preserve monotonic ordering by moving to a new incarnation rather than wrapping.
            BeginNewPlantIncarnation();
            return;
        }
        StateRevision++;
    }

    public bool TryApplyNetworkState(
        ulong incarnation,
        ulong revision,
        ulong accumulatedTicks,
        bool watered,
        AdvancedFarmingAuthoritativeLightState lightState)
    {
        if (incarnation == 0UL || revision == 0UL)
            return false;

        if (PlantIncarnation != 0UL)
        {
            if (incarnation < PlantIncarnation)
                return false;
            if (incarnation == PlantIncarnation && revision <= StateRevision)
                return false;
        }

        PlantIncarnation = incarnation;
        StateRevision = revision;
        ObservePlantIncarnation(incarnation);
        AccumulatedTicks = accumulatedTicks;
        bWatered = watered;
        AuthoritativeLightState = lightState;
        return true;
    }

    private static ulong AllocatePlantIncarnation()
    {
        lock (PlantIncarnationSync)
        {
            ulong value = s_nextPlantIncarnation;
            if (value == 0UL || value == ulong.MaxValue)
                value = 1UL;
            s_nextPlantIncarnation = value + 1UL;
            return value;
        }
    }

    private static void ObservePlantIncarnation(ulong incarnation)
    {
        if (incarnation == 0UL)
            return;
        lock (PlantIncarnationSync)
        {
            if (incarnation >= s_nextPlantIncarnation)
                s_nextPlantIncarnation = incarnation == ulong.MaxValue ? 1UL : incarnation + 1UL;
        }
    }

    public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
    {
        base.read(_br, _eStreamMode);
        GameTimerTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        ulong persistedAccumulatedTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        AccumulatedTicks = persistedAccumulatedTicks & AccumulatedTicksMask;
        bWatered = (persistedAccumulatedTicks & WateredFlagMask) != 0UL;
        if ((persistedAccumulatedTicks & AuthoritativeLightValidFlagMask) != 0UL)
            AuthoritativeLightState = (persistedAccumulatedTicks & AuthoritativeLightPassFlagMask) != 0UL ? AdvancedFarmingAuthoritativeLightState.Pass : AdvancedFarmingAuthoritativeLightState.Blocked;
        else
            AuthoritativeLightState = AdvancedFarmingAuthoritativeLightState.Unknown;
        try { bUpdating = AdvancedFarmingBinaryCompat.ReadBool(_br); } catch { }
        try { LastProcessedWorldSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed(); }

        int persistenceVersion = _eStreamMode == StreamModeRead.Persistency ? GetLegacyForkVersion() : PersistencyVersionWithIncarnation;
        bool hasProvenancePayload = _eStreamMode != StreamModeRead.Persistency || IsProvenancePersistenceVersion(persistenceVersion);
        if (hasProvenancePayload)
        {
            System.IO.BinaryReader br = (System.IO.BinaryReader)_br;
            RebirthGrowerStableId = RebirthSurvivorNetworkCodec.ReadString(br, 512);
            RebirthGrowerBackgroundId = RebirthSurvivorNetworkCodec.ReadString(br, 128);
            RebirthGrowerBonusId = RebirthSurvivorNetworkCodec.ReadString(br, 128);
            RebirthPlantingSkillValue = br.ReadSingle();
            RebirthProvenanceWorldTime = br.ReadUInt64();
            if (_eStreamMode != StreamModeRead.Persistency || persistenceVersion >= PersistencyVersionWithIncarnation)
            {
                PlantIncarnation = br.ReadUInt64();
                StateRevision = br.ReadUInt64();
                ObservePlantIncarnation(PlantIncarnation);
            }
            else
            {
                PlantIncarnation = 0UL;
                StateRevision = 0UL;
            }
        }
        else
        {
            RebirthGrowerStableId = string.Empty;
            RebirthGrowerBackgroundId = string.Empty;
            RebirthGrowerBonusId = string.Empty;
            RebirthPlantingSkillValue = 0f;
            RebirthProvenanceWorldTime = 0UL;
            PlantIncarnation = 0UL;
            StateRevision = 0UL;
        }
        SanitizeTimingBaselines();
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "read PlantGrowing persistenceVersion=" + persistenceVersion + " accumulatedTicks=" + AccumulatedTicks + " watered=" + bWatered + " grower=" + RebirthGrowerStableId + " bonus=" + RebirthGrowerBonusId);
#endif
    }

    public override void write(PooledBinaryWriter stream, StreamModeWrite _eStreamMode)
    {
        if (_eStreamMode == StreamModeWrite.Persistency)
        {
            System.IO.Stream baseStream = stream.BaseStream;
            if (baseStream == null || !baseStream.CanSeek)
                throw new System.IO.IOException("Plant provenance persistence requires a seekable tile-entity stream.");
            long versionPosition = baseStream.Position;
            base.write(stream, _eStreamMode);
            long payloadPosition = baseStream.Position;
            baseStream.Position = versionPosition;
            AdvancedFarmingBinaryCompat.WriteUShort(stream, PersistencyVersionWithIncarnation);
            baseStream.Position = payloadPosition;
            WritePayload(stream, true);
        }
        else
        {
            base.write(stream, _eStreamMode);
            WritePayload(stream, true);
        }
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "write PlantGrowing accumulatedTicks=" + AccumulatedTicks + " watered=" + bWatered + " grower=" + RebirthGrowerStableId + " bonus=" + RebirthGrowerBonusId);
#endif
    }

    private void WritePayload(PooledBinaryWriter stream, bool includeProvenance)
    {
        AdvancedFarmingBinaryCompat.WriteULong(stream, GameTimerTicks);
        ulong persistedAccumulatedTicks = AccumulatedTicks & AccumulatedTicksMask;
        if (bWatered) persistedAccumulatedTicks |= WateredFlagMask;
        if (AuthoritativeLightState != AdvancedFarmingAuthoritativeLightState.Unknown)
        {
            persistedAccumulatedTicks |= AuthoritativeLightValidFlagMask;
            if (AuthoritativeLightState == AdvancedFarmingAuthoritativeLightState.Pass) persistedAccumulatedTicks |= AuthoritativeLightPassFlagMask;
        }
        AdvancedFarmingBinaryCompat.WriteULong(stream, persistedAccumulatedTicks);
        AdvancedFarmingBinaryCompat.WriteBool(stream, bUpdating);
        AdvancedFarmingBinaryCompat.WriteInt(stream, LastProcessedWorldSeconds);
        if (!includeProvenance) return;
        System.IO.BinaryWriter bw = (System.IO.BinaryWriter)stream;
        RebirthSurvivorNetworkCodec.WriteString(bw, RebirthGrowerStableId ?? string.Empty, 512);
        RebirthSurvivorNetworkCodec.WriteString(bw, RebirthGrowerBackgroundId ?? string.Empty, 128);
        RebirthSurvivorNetworkCodec.WriteString(bw, RebirthGrowerBonusId ?? string.Empty, 128);
        bw.Write(RebirthPlantingSkillValue);
        bw.Write(RebirthProvenanceWorldTime);
        bw.Write(PlantIncarnation);
        bw.Write(StateRevision);
    }

    private static bool IsProvenancePersistenceVersion(int version)
    {
        return version >= 0 && (((ushort)version & PersistencyVersionFamilyMask) == PersistencyVersionFamily) && version >= PersistencyVersionWithProvenance;
    }

}

public class TileEntityFarmPlotRebirth : TileEntity
{
    // Reserve a custom local-version namespace rather than using the next base-game number.
    // This prevents an existing plot written by a newer 3.0 patch from being mistaken for a
    // REBIRTH rain-progress record if the base TileEntity version advances beyond 19.
    private const ushort PersistencyVersionFamilyMask = 0xFF00;
    private const ushort PersistencyVersionFamily = 0xAF00;
    private const ushort PersistencyVersionWithRainExposure = 0xAF01;
    private const ushort PersistencyVersionWithActivation = 0xAF02;
    public bool FarmingActivated;

    public int waterCount;
    public int waterMax = 6;
    public int LastProcessedWorldSeconds;

    // Persisted partial progress toward the next rain-water unit. The value is stored as
    // world-time units multiplied by 1000 so rainfall intensity can contribute proportionally
    // without floating-point persistence drift.
    public ulong RainExposureScaledWorldTimeUnits;

    // Runtime-only sample baseline. It is reset when the tile entity is constructed/read/loaded
    // so current rain is never applied retroactively to an unloaded period.
    public ulong LastRainSampleWorldTime;

    public TileEntityFarmPlotRebirth(Chunk _chunk) : base(_chunk)
    {
        LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ResetRainSampleBaseline();
#if DEBUG
        if (AdvancedFarmingDebug.TileEntity)
            AdvancedFarmingDebug.Log("tileentity", "constructed FarmPlot tile entity.");
#endif
    }

    public override TileEntityType GetTileEntityType()
    {
        return (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityFarmPlotRebirth;
    }

    public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
    {
        base.read(_br, _eStreamMode);
        waterCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
        try { waterMax = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { }
        try { LastProcessedWorldSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed(); }

        // Persistency is versioned per tile-entity record by TileEntity.read(). Existing plots
        // use the current base-game version and end immediately after LastProcessedWorldSeconds.
        // Only the reserved 0xAFxx REBIRTH family contains the trailing UInt64; never probe the
        // stream with try/catch.
        int persistenceVersion = _eStreamMode == StreamModeRead.Persistency
            ? GetLegacyForkVersion()
            : -1;
        if (IsRainExposurePersistenceVersion(persistenceVersion))
        {
            RainExposureScaledWorldTimeUnits = AdvancedFarmingBinaryCompat.ReadULong(_br);
        }
        else
        {
            RainExposureScaledWorldTimeUnits = 0UL;
        }

        FarmingActivated = _eStreamMode != StreamModeRead.Persistency || persistenceVersion == PersistencyVersionWithActivation
            ? AdvancedFarmingBinaryCompat.ReadBool(_br) : false;
        ResetRainSampleBaseline();
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "read FarmPlot persistenceVersion=" + (persistenceVersion >= 0 ? persistenceVersion.ToString() : "network") + " water=" + waterCount + "/" + waterMax + " rainExposureScaled=" + RainExposureScaledWorldTimeUnits + " lastProcessed=" + LastProcessedWorldSeconds);
#endif
    }

    public override void write(PooledBinaryWriter stream, StreamModeWrite _eStreamMode)
    {
        if (_eStreamMode == StreamModeWrite.Persistency)
        {
            // Let the installed game write its exact current TileEntity base header, then replace
            // only the leading UInt16 version in place. This avoids duplicating assumptions about
            // the base header and remains correct if a 3.0 hotfix changes that header. Chunk-save
            // streams are seekable; fail loudly rather than write an ambiguous unversioned payload.
            System.IO.Stream baseStream = stream.BaseStream;
            if (baseStream == null || !baseStream.CanSeek)
                throw new System.IO.IOException("Farm-plot persistence requires a seekable tile-entity stream.");

            long versionPosition = baseStream.Position;
            base.write(stream, _eStreamMode);
            long customPayloadPosition = baseStream.Position;

            baseStream.Position = versionPosition;
            AdvancedFarmingBinaryCompat.WriteUShort(stream, PersistencyVersionWithActivation);
            baseStream.Position = customPayloadPosition;

            AdvancedFarmingBinaryCompat.WriteInt(stream, waterCount);
            AdvancedFarmingBinaryCompat.WriteInt(stream, waterMax);
            AdvancedFarmingBinaryCompat.WriteInt(stream, LastProcessedWorldSeconds);
            AdvancedFarmingBinaryCompat.WriteULong(stream, RainExposureScaledWorldTimeUnits);
        }
        else
        {
            // Partial rain exposure is server-only and is not needed in the network payload.
            // Preserve the exact pre-change network format.
            base.write(stream, _eStreamMode);
            AdvancedFarmingBinaryCompat.WriteInt(stream, waterCount);
            AdvancedFarmingBinaryCompat.WriteInt(stream, waterMax);
            AdvancedFarmingBinaryCompat.WriteInt(stream, LastProcessedWorldSeconds);
        }
        AdvancedFarmingBinaryCompat.WriteBool(stream, FarmingActivated);
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "write FarmPlot persistenceVersion=" + (_eStreamMode == StreamModeWrite.Persistency ? PersistencyVersionWithRainExposure.ToString() : "network") + " water=" + waterCount + "/" + waterMax + " rainExposureScaled=" + RainExposureScaledWorldTimeUnits + " lastProcessed=" + LastProcessedWorldSeconds);
#endif
    }

    public override void OnLoad()
    {
        base.OnLoad();
        ResetRainSampleBaseline();
        if (FarmingActivated) AdvancedFarmingActiveAreaRegistry.RegisterFarmPlot(ToWorldPos());
    }

    public override void OnDestroy()
    {
        Vector3i pos = ToWorldPos();
        AdvancedFarmingWaterProviderRegistry.UnregisterProvider(pos);
        AdvancedFarmingActiveAreaRegistry.UnregisterFarmPlot(pos);
        base.OnDestroy();
    }

    private static bool IsRainExposurePersistenceVersion(int version)
    {
        return version >= 0
            && (version & PersistencyVersionFamilyMask) == PersistencyVersionFamily
            && version >= PersistencyVersionWithRainExposure;
    }

    private void ResetRainSampleBaseline()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        LastRainSampleWorldTime = world != null ? world.GetWorldTime() : 0UL;
    }
}

public class TileEntityWaterTankRebirth : TileEntity
{
    public int waterCount;
    public int waterMax = 100;
    public int timeLapsed;
    public int LastProcessedWorldSeconds;

    public TileEntityWaterTankRebirth(Chunk _chunk) : base(_chunk)
    {
        timeLapsed = RebirthUtilities.TotalGameSecondsPassed();
        LastProcessedWorldSeconds = timeLapsed;
#if DEBUG
        if (AdvancedFarmingDebug.TileEntity)
            AdvancedFarmingDebug.Log("tileentity", "constructed WaterTank tile entity.");
#endif
    }

    public override TileEntityType GetTileEntityType()
    {
        return (TileEntityType)RebirthUtilities.TileEntityRebirth.TileEntityWaterTankRebirth;
    }

    public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
    {
        base.read(_br, _eStreamMode);
        waterCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
        timeLapsed = AdvancedFarmingBinaryCompat.ReadInt(_br);
        try { waterMax = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { }
        try { LastProcessedWorldSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { LastProcessedWorldSeconds = timeLapsed; }
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "read WaterTank water=" + waterCount + "/" + waterMax + " timeLapsed=" + timeLapsed + " lastProcessed=" + LastProcessedWorldSeconds);
#endif
    }

    public override void write(PooledBinaryWriter stream, StreamModeWrite _eStreamMode)
    {
        base.write(stream, _eStreamMode);
        AdvancedFarmingBinaryCompat.WriteInt(stream, waterCount);
        AdvancedFarmingBinaryCompat.WriteInt(stream, timeLapsed);
        AdvancedFarmingBinaryCompat.WriteInt(stream, waterMax);
        AdvancedFarmingBinaryCompat.WriteInt(stream, LastProcessedWorldSeconds);
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "write WaterTank water=" + waterCount + "/" + waterMax + " timeLapsed=" + timeLapsed + " lastProcessed=" + LastProcessedWorldSeconds);
#endif
    }

    public override void OnDestroy()
    {
        AdvancedFarmingWaterProviderRegistry.UnregisterProvider(ToWorldPos());
        base.OnDestroy();
    }
}
