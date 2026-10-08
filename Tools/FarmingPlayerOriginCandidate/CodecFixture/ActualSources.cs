using System;using System.Collections.Generic;using System.IO;
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
    private const ushort PersistencyVersionWithOrigin = 0xC703;
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
    public AdvancedFarmingPlantOrigin PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;

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
        PlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
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
            var retainedOrigin = PlantOrigin;
            BeginNewPlantIncarnation();
            PlantOrigin = retainedOrigin; // same plant; revision rollover only
            return;
        }
        StateRevision++;
    }

    public bool TryApplyNetworkState(
        ulong incarnation,
        ulong revision,
        ulong accumulatedTicks,
        bool watered,
        AdvancedFarmingAuthoritativeLightState lightState, AdvancedFarmingPlantOrigin origin)
    {
        if (!AdvancedFarmingPlantOriginService.IsValid(origin) || incarnation == 0UL || revision == 0UL)
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
        PlantOrigin = origin;
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
        if (_eStreamMode == StreamModeRead.FromClient)
            throw new System.IO.InvalidDataException("Clients cannot write authoritative crop state.");
        base.read(_br, _eStreamMode);
        // Native base header has already been read; crop payload commits only after
        // every required field and late origin are validated. Outer native SetHandle
        // and base header mutation still require the original-owner frame guard.
        ulong parsedGameTimerTicks=0, parsedAccumulatedTicks=0, parsedPlantIncarnation=0,
            parsedStateRevision=0, parsedRebirthProvenanceWorldTime=0;
        bool parsedbWatered=false, parsedbUpdating=bUpdating;
        int parsedLastProcessedWorldSeconds=LastProcessedWorldSeconds;
        var parsedAuthoritativeLightState=AdvancedFarmingAuthoritativeLightState.Unknown;
        var parsedPlantOrigin=AdvancedFarmingPlantOrigin.Unknown;
        string parsedRebirthGrowerStableId="", parsedRebirthGrowerBackgroundId="", parsedRebirthGrowerBonusId="";
        float parsedRebirthPlantingSkillValue=0;
        parsedGameTimerTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        ulong persistedAccumulatedTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        parsedAccumulatedTicks = persistedAccumulatedTicks & AccumulatedTicksMask;
        parsedbWatered = (persistedAccumulatedTicks & WateredFlagMask) != 0UL;
        if ((persistedAccumulatedTicks & AuthoritativeLightValidFlagMask) != 0UL)
            parsedAuthoritativeLightState = (persistedAccumulatedTicks & AuthoritativeLightPassFlagMask) != 0UL ? AdvancedFarmingAuthoritativeLightState.Pass : AdvancedFarmingAuthoritativeLightState.Blocked;
        else
            parsedAuthoritativeLightState = AdvancedFarmingAuthoritativeLightState.Unknown;
        try { parsedbUpdating = AdvancedFarmingBinaryCompat.ReadBool(_br); } catch { }
        try { parsedLastProcessedWorldSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br); } catch { parsedLastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed(); }

        parsedPlantOrigin = AdvancedFarmingPlantOrigin.Unknown;
        int persistenceVersion = _eStreamMode == StreamModeRead.Persistency ? GetLegacyForkVersion() : PersistencyVersionWithIncarnation;
        if (_eStreamMode == StreamModeRead.Persistency && persistenceVersion >= 0 &&
            (((ushort)persistenceVersion & PersistencyVersionFamilyMask) == PersistencyVersionFamily) &&
            persistenceVersion > PersistencyVersionWithOrigin)
            throw new System.IO.InvalidDataException("Unsupported crop persistence version.");
        bool hasProvenancePayload = _eStreamMode != StreamModeRead.Persistency || IsProvenancePersistenceVersion(persistenceVersion);
        if (hasProvenancePayload)
        {
            System.IO.BinaryReader br = (System.IO.BinaryReader)_br;
            parsedRebirthGrowerStableId = RebirthSurvivorNetworkCodec.ReadString(br, 512);
            parsedRebirthGrowerBackgroundId = RebirthSurvivorNetworkCodec.ReadString(br, 128);
            parsedRebirthGrowerBonusId = RebirthSurvivorNetworkCodec.ReadString(br, 128);
            parsedRebirthPlantingSkillValue = br.ReadSingle();
            parsedRebirthProvenanceWorldTime = br.ReadUInt64();
            if (_eStreamMode != StreamModeRead.Persistency || persistenceVersion >= PersistencyVersionWithIncarnation)
            {
                parsedPlantIncarnation = br.ReadUInt64();
                parsedStateRevision = br.ReadUInt64();

            }
            else
            {
                parsedPlantIncarnation = 0UL;
                parsedStateRevision = 0UL;
            }
        }
        else
        {
            parsedRebirthGrowerStableId = string.Empty;
            parsedRebirthGrowerBackgroundId = string.Empty;
            parsedRebirthGrowerBonusId = string.Empty;
            parsedRebirthPlantingSkillValue = 0f;
            parsedRebirthProvenanceWorldTime = 0UL;
            parsedPlantIncarnation = 0UL;
            parsedStateRevision = 0UL;
        }
        if (_eStreamMode != StreamModeRead.Persistency || persistenceVersion == PersistencyVersionWithOrigin)
        {
            parsedPlantOrigin = (AdvancedFarmingPlantOrigin)((System.IO.BinaryReader)_br).ReadByte();
            if (!AdvancedFarmingPlantOriginService.IsValid(parsedPlantOrigin))
                throw new System.IO.InvalidDataException("Invalid crop origin.");
        }

        GameTimerTicks = parsedGameTimerTicks;
        AccumulatedTicks = parsedAccumulatedTicks;
        bWatered = parsedbWatered;
        AuthoritativeLightState = parsedAuthoritativeLightState;
        bUpdating = parsedbUpdating;
        LastProcessedWorldSeconds = parsedLastProcessedWorldSeconds;
        PlantOrigin = parsedPlantOrigin;
        RebirthGrowerStableId = parsedRebirthGrowerStableId;
        RebirthGrowerBackgroundId = parsedRebirthGrowerBackgroundId;
        RebirthGrowerBonusId = parsedRebirthGrowerBonusId;
        RebirthPlantingSkillValue = parsedRebirthPlantingSkillValue;
        RebirthProvenanceWorldTime = parsedRebirthProvenanceWorldTime;
        PlantIncarnation = parsedPlantIncarnation;
        StateRevision = parsedStateRevision;
        ObservePlantIncarnation(PlantIncarnation);
        SanitizeTimingBaselines();
#if DEBUG
        if (AdvancedFarmingDebug.SaveLoad)
            AdvancedFarmingDebug.Log("saveload", "read PlantGrowing persistenceVersion=" + persistenceVersion + " accumulatedTicks=" + AccumulatedTicks + " watered=" + bWatered + " grower=" + RebirthGrowerStableId + " bonus=" + RebirthGrowerBonusId);
#endif
    }

    public override void write(PooledBinaryWriter stream, StreamModeWrite _eStreamMode)
    {
        if (!AdvancedFarmingPlantOriginService.IsValid(PlantOrigin))
            throw new System.IO.InvalidDataException("Invalid crop origin.");
        if (_eStreamMode == StreamModeWrite.Persistency)
        {
            System.IO.Stream baseStream = stream.BaseStream;
            if (baseStream == null || !baseStream.CanSeek)
                throw new System.IO.IOException("Plant provenance persistence requires a seekable tile-entity stream.");
            long versionPosition = baseStream.Position;
            base.write(stream, _eStreamMode);
            long payloadPosition = baseStream.Position;
            baseStream.Position = versionPosition;
            AdvancedFarmingBinaryCompat.WriteUShort(stream, PersistencyVersionWithOrigin);
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
        if (!AdvancedFarmingPlantOriginService.IsValid(PlantOrigin))
            throw new System.IO.InvalidDataException("Invalid crop origin.");
        bw.Write((byte)PlantOrigin);
    }

    private static bool IsProvenancePersistenceVersion(int version)
    {
        return version >= 0 && (((ushort)version & PersistencyVersionFamilyMask) == PersistencyVersionFamily) && version >= PersistencyVersionWithProvenance;
    }

}
public class NetPackageUpdatePlantedCropRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private const ulong WateredFlagMask = 0x8000000000000000UL;
    private const ulong AuthoritativeLightValidFlagMask = 0x4000000000000000UL;
    private const ulong AuthoritativeLightPassFlagMask = 0x2000000000000000UL;
    private const ulong AccumulatedTicksMask = 0x1FFFFFFFFFFFFFFFUL;

    private Vector3i blockPos;
    private int blockID;
    private ulong accumulatedTicks;
    private bool watered;
    private AdvancedFarmingAuthoritativeLightState authoritativeLightState;
    private ulong plantIncarnation;
    private ulong stateRevision;
    private AdvancedFarmingPlantOrigin plantOrigin;

    public NetPackageUpdatePlantedCropRebirth Setup(
        Vector3i _blockPos,
        int _blockID,
        ulong _accumulatedTicks,
        bool _watered,
        AdvancedFarmingAuthoritativeLightState _authoritativeLightState,
        ulong _plantIncarnation,
        ulong _stateRevision, AdvancedFarmingPlantOrigin _plantOrigin)
    {
        blockPos = _blockPos;
        blockID = _blockID;
        accumulatedTicks = _accumulatedTicks;
        watered = _watered;
        authoritativeLightState = _authoritativeLightState;
        plantIncarnation = _plantIncarnation;
        stateRevision = _stateRevision;
        if (!AdvancedFarmingPlantOriginService.IsValid(_plantOrigin)) throw new System.IO.InvalidDataException("Invalid crop origin.");
        plantOrigin = _plantOrigin;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        Vector3i parsedblockPos;
        int parsedblockID;
        ulong parsedaccumulatedTicks, parsedplantIncarnation, parsedstateRevision;
        bool parsedwatered;
        AdvancedFarmingAuthoritativeLightState parsedauthoritativeLightState;
        AdvancedFarmingPlantOrigin parsedplantOrigin;
        parsedblockPos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        parsedblockID = AdvancedFarmingBinaryCompat.ReadInt(_br);
        ulong packedAccumulatedTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        parsedaccumulatedTicks = packedAccumulatedTicks & AccumulatedTicksMask;
        parsedwatered = (packedAccumulatedTicks & WateredFlagMask) != 0UL;
        if ((packedAccumulatedTicks & AuthoritativeLightValidFlagMask) != 0UL)
        {
            parsedauthoritativeLightState = (packedAccumulatedTicks & AuthoritativeLightPassFlagMask) != 0UL
                ? AdvancedFarmingAuthoritativeLightState.Pass
                : AdvancedFarmingAuthoritativeLightState.Blocked;
        }
        else
        {
            parsedauthoritativeLightState = AdvancedFarmingAuthoritativeLightState.Unknown;
        }
        parsedplantIncarnation = AdvancedFarmingBinaryCompat.ReadULong(_br);
        parsedstateRevision = AdvancedFarmingBinaryCompat.ReadULong(_br);
        parsedplantOrigin = (AdvancedFarmingPlantOrigin)((System.IO.BinaryReader)_br).ReadByte();
        if (!AdvancedFarmingPlantOriginService.IsValid(parsedplantOrigin)) throw new System.IO.InvalidDataException("Invalid crop origin.");
        blockPos = parsedblockPos;
        blockID = parsedblockID;
        accumulatedTicks = parsedaccumulatedTicks;
        watered = parsedwatered;
        authoritativeLightState = parsedauthoritativeLightState;
        plantIncarnation = parsedplantIncarnation;
        stateRevision = parsedstateRevision;
        plantOrigin = parsedplantOrigin;

    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        StreamUtils.Write((System.IO.BinaryWriter)_bw, blockPos);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, blockID);
        ulong packedAccumulatedTicks = accumulatedTicks & AccumulatedTicksMask;
        if (watered)
            packedAccumulatedTicks |= WateredFlagMask;
        if (authoritativeLightState != AdvancedFarmingAuthoritativeLightState.Unknown)
        {
            packedAccumulatedTicks |= AuthoritativeLightValidFlagMask;
            if (authoritativeLightState == AdvancedFarmingAuthoritativeLightState.Pass)
                packedAccumulatedTicks |= AuthoritativeLightPassFlagMask;
        }
        AdvancedFarmingBinaryCompat.WriteULong(_bw, packedAccumulatedTicks);
        AdvancedFarmingBinaryCompat.WriteULong(_bw, plantIncarnation);
        AdvancedFarmingBinaryCompat.WriteULong(_bw, stateRevision);
        ((System.IO.BinaryWriter)_bw).Write((byte)plantOrigin);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || _world == null || !_world.IsRemote())
            return;

        AdvancedFarmingDeferredPlantStateService.ApplyOrDefer(
            _world, blockPos, blockID, accumulatedTicks, watered, authoritativeLightState, plantIncarnation, stateRevision, plantOrigin);
    }

    public int GetLength() { return 49; }
}
public static class AdvancedFarmingDeferredPlantStateService
{
    private struct PendingState
    {
        public Vector3i Pos;
        public int BlockId;
        public ulong AccumulatedTicks;
        public bool Watered;
        public AdvancedFarmingAuthoritativeLightState LightState;
        public ulong Incarnation;
        public ulong Revision;
        public AdvancedFarmingPlantOrigin Origin;
        public float DeadlineRealtime;
    }

    private const int MaxPendingStates = 128;
    private const float PendingLifetimeSeconds = 5f;
    private static readonly List<PendingState> Pending = new List<PendingState>(16);

    public static void ApplyOrDefer(
        World world, Vector3i pos, int blockId, ulong accumulatedTicks, bool watered,
        AdvancedFarmingAuthoritativeLightState lightState, ulong incarnation, ulong revision, AdvancedFarmingPlantOrigin origin)
    {
        if (world == null || !AdvancedFarmingPlantOriginService.IsValid(origin) || incarnation == 0UL || revision == 0UL)
            return;

        BlockValue current = world.GetBlock(pos);
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (current.type == blockId && te != null)
        {
            if (te.TryApplyNetworkState(incarnation, revision, accumulatedTicks, watered, lightState, origin))
                AdvancedFarmingHoverTextService.InvalidatePlantState(pos);
            return;
        }

        // A newer incarnation supersedes any deferred state for the same position.
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            if (!SamePos(Pending[i].Pos, pos))
                continue;
            PendingState existing = Pending[i];
            if (existing.Incarnation > incarnation
                || (existing.Incarnation == incarnation && existing.Revision >= revision))
                return;
            Pending.RemoveAt(i);
        }

        if (Pending.Count >= MaxPendingStates)
            Pending.RemoveAt(0);
        Pending.Add(new PendingState
        {
            Pos = pos,
            BlockId = blockId,
            AccumulatedTicks = accumulatedTicks,
            Watered = watered,
            LightState = lightState,
            Incarnation = incarnation,
            Revision = revision,
            Origin = origin,
            DeadlineRealtime = Time.realtimeSinceStartup + PendingLifetimeSeconds
        });
    }

    public static void Pump(World world)
    {
        if (Pending.Count == 0)
            return;
        if (world == null)
        {
            Pending.Clear();
            return;
        }

        float now = Time.realtimeSinceStartup;
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            PendingState state = Pending[i];
            if (now >= state.DeadlineRealtime)
            {
                Pending.RemoveAt(i);
                continue;
            }

            BlockValue current = world.GetBlock(state.Pos);
            if (current.type != state.BlockId)
                continue;
            TileEntityPlantGrowingRebirth te = world.GetTileEntity(state.Pos) as TileEntityPlantGrowingRebirth;
            if (te == null)
                continue;

            Pending.RemoveAt(i);
            if (te.TryApplyNetworkState(state.Incarnation, state.Revision, state.AccumulatedTicks, state.Watered, state.LightState, state.Origin))
                AdvancedFarmingHoverTextService.InvalidatePlantState(state.Pos);
        }
    }

    public static void Clear()
    {
        Pending.Clear();
    }

    private static bool SamePos(Vector3i a, Vector3i b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }
}
internal static class RebirthSurvivorNetworkCodec {
public static string Clean(string value, int maxLength)
    {
        string s = (value ?? string.Empty).Trim();
        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }
public static void WriteString(BinaryWriter writer, string value, int maxLength)
    {
        writer.Write(Clean(value, maxLength));
    }
public static string ReadString(BinaryReader reader, int maxLength)
    {
        string value = reader.ReadString() ?? string.Empty;
        return value.Length > maxLength ? value.Substring(0, maxLength) : value;
    }}