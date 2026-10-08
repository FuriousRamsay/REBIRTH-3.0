using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public class NetPackageAdvancedFarmingRuntimePolicyRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private bool enabled;
    private int growthSeconds;
    private int minimumNaturalSunlight;

    public NetPackageAdvancedFarmingRuntimePolicyRebirth Setup(bool _enabled, int _growthSeconds, int _minimumNaturalSunlight)
    {
        enabled = _enabled;
        growthSeconds = _growthSeconds;
        minimumNaturalSunlight = _minimumNaturalSunlight;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        enabled = AdvancedFarmingBinaryCompat.ReadBool(_br);
        growthSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br);
        minimumNaturalSunlight = AdvancedFarmingBinaryCompat.ReadInt(_br);
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        AdvancedFarmingBinaryCompat.WriteBool(_bw, enabled);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, growthSeconds);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, minimumNaturalSunlight);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        AdvancedFarmingRuntimePolicy.SetEnabledFromNetwork(enabled);
        AdvancedFarmingRuntimePolicy.SetGrowthSecondsFromNetwork(growthSeconds);
        AdvancedFarmingRuntimePolicy.SetMinimumNaturalSunlightFromNetwork(minimumNaturalSunlight);
    }

    public int GetLength() { return 20; }
}


public class NetPackageWeatherRainOverrideRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private float forceRain;

    public NetPackageWeatherRainOverrideRebirth Setup(float _forceRain)
    {
        forceRain = _forceRain;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        forceRain = ((System.IO.BinaryReader)_br).ReadSingle();
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        ((System.IO.BinaryWriter)_bw).Write(forceRain);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        WeatherManager.forceRain = forceRain;
        WeatherManager.needToReUpdateWeatherSpectrums = true;
    }

    public int GetLength() { return 12; }
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

    public NetPackageUpdatePlantedCropRebirth Setup(
        Vector3i _blockPos,
        int _blockID,
        ulong _accumulatedTicks,
        bool _watered,
        AdvancedFarmingAuthoritativeLightState _authoritativeLightState,
        ulong _plantIncarnation,
        ulong _stateRevision)
    {
        blockPos = _blockPos;
        blockID = _blockID;
        accumulatedTicks = _accumulatedTicks;
        watered = _watered;
        authoritativeLightState = _authoritativeLightState;
        plantIncarnation = _plantIncarnation;
        stateRevision = _stateRevision;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        blockPos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        blockID = AdvancedFarmingBinaryCompat.ReadInt(_br);
        ulong packedAccumulatedTicks = AdvancedFarmingBinaryCompat.ReadULong(_br);
        accumulatedTicks = packedAccumulatedTicks & AccumulatedTicksMask;
        watered = (packedAccumulatedTicks & WateredFlagMask) != 0UL;
        if ((packedAccumulatedTicks & AuthoritativeLightValidFlagMask) != 0UL)
        {
            authoritativeLightState = (packedAccumulatedTicks & AuthoritativeLightPassFlagMask) != 0UL
                ? AdvancedFarmingAuthoritativeLightState.Pass
                : AdvancedFarmingAuthoritativeLightState.Blocked;
        }
        else
        {
            authoritativeLightState = AdvancedFarmingAuthoritativeLightState.Unknown;
        }
        plantIncarnation = AdvancedFarmingBinaryCompat.ReadULong(_br);
        stateRevision = AdvancedFarmingBinaryCompat.ReadULong(_br);
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
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || _world == null)
            return;

        AdvancedFarmingDeferredPlantStateService.ApplyOrDefer(
            _world, blockPos, blockID, accumulatedTicks, watered, authoritativeLightState, plantIncarnation, stateRevision);
    }

    public int GetLength() { return 48; }
}

/// <summary>
/// Holds a small bounded set of crop states that arrived before the independent world
/// block replacement which creates their target stage. The packet bytes are copied; pooled
/// NetPackage instances are never retained.
/// </summary>
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
        public float DeadlineRealtime;
    }

    private const int MaxPendingStates = 128;
    private const float PendingLifetimeSeconds = 5f;
    private static readonly List<PendingState> Pending = new List<PendingState>(16);

    public static void ApplyOrDefer(
        World world, Vector3i pos, int blockId, ulong accumulatedTicks, bool watered,
        AdvancedFarmingAuthoritativeLightState lightState, ulong incarnation, ulong revision)
    {
        if (world == null || incarnation == 0UL || revision == 0UL)
            return;

        BlockValue current = world.GetBlock(pos);
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (current.type == blockId && te != null)
        {
            if (te.TryApplyNetworkState(incarnation, revision, accumulatedTicks, watered, lightState))
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
            if (te.TryApplyNetworkState(state.Incarnation, state.Revision, state.AccumulatedTicks, state.Watered, state.LightState))
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

public class NetPackageUpdateFarmPlotRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private Vector3i blockPos;
    private int contentCount;

    public NetPackageUpdateFarmPlotRebirth Setup(Vector3i _blockPos, int _contentCount)
    {
        blockPos = _blockPos;
        contentCount = _contentCount;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        blockPos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        contentCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        StreamUtils.Write((System.IO.BinaryWriter)_bw, blockPos);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, contentCount);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || _world == null)
            return;

        TileEntityFarmPlotRebirth te = _world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (te != null)
        {
            if (contentCount < 0)
                contentCount = 0;
            int maxWater = te.waterMax > 0 ? te.waterMax : 6;
            if (contentCount > maxWater)
                contentCount = maxWater;

            te.waterCount = contentCount;
            te.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
            te.setModified();
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                AdvancedFarmingSyncService.SendFarmPlot(blockPos, te.waterCount);
        }
    }

    public int GetLength() { return 24; }
}


public class NetPackageRequestWaterTankItemExchangeRebirth : NetPackage
{
    private int playerEntityId;
    private Vector3i blockPos;
    private string itemName;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRequestWaterTankItemExchangeRebirth Setup(int _playerEntityId, Vector3i _blockPos, string _itemName)
    {
        playerEntityId = _playerEntityId;
        blockPos = _blockPos;
        itemName = _itemName ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        playerEntityId = AdvancedFarmingBinaryCompat.ReadInt(_br);
        blockPos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        itemName = ((System.IO.BinaryReader)_br).ReadString();
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, playerEntityId);
        StreamUtils.Write((System.IO.BinaryWriter)_bw, blockPos);
        ((System.IO.BinaryWriter)_bw).Write(itemName ?? string.Empty);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (_world == null || _world.IsRemote())
            return;

        if (!ValidEntityIdForSender(playerEntityId))
            return;

        AdvancedFarmingItemWaterExchange.TryApplyServerAuthoritativeExchange(_world, playerEntityId, blockPos, itemName);
    }

    public int GetLength() { return 0; }
}

public class NetPackageUpdateWaterTankRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private Vector3i blockPos;
    private int contentCount;

    public NetPackageUpdateWaterTankRebirth Setup(Vector3i _blockPos, int _contentCount)
    {
        blockPos = _blockPos;
        contentCount = _contentCount;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        blockPos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        contentCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        StreamUtils.Write((System.IO.BinaryWriter)_bw, blockPos);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, contentCount);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled || _world == null)
            return;

        TileEntityWaterTankRebirth te = _world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        if (te != null)
        {
            if (contentCount < 0)
                contentCount = 0;
            int maxWater = te.waterMax > 0 ? te.waterMax : 400;
            BlockValue blockValue = _world.GetBlock(blockPos);
            if (blockValue.Block != null && blockValue.Block.Properties.Values.ContainsKey("MaxWater") && int.TryParse(blockValue.Block.Properties.Values["MaxWater"], out int parsedMax) && parsedMax > 0)
            {
                maxWater = parsedMax;
                te.waterMax = parsedMax;
            }
            if (contentCount > maxWater)
                contentCount = maxWater;

            te.waterCount = contentCount;
            int now = RebirthUtilities.TotalGameSecondsPassed();
            te.timeLapsed = now;
            te.LastProcessedWorldSeconds = now;
            te.setModified();
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
                AdvancedFarmingSyncService.SendWaterTank(blockPos, te.waterCount);
        }
    }

    public int GetLength() { return 24; }
}


public class NetPackageRequestCropGrowthDiagnosticRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    private int playerEntityId;
    private string sessionId;
    private Vector3i center;
    private int radius;
    private int durationSeconds;
    private int intervalSeconds;
    private Vector3i[] cropPositions = new Vector3i[0];

    public NetPackageRequestCropGrowthDiagnosticRebirth Setup(
        int _playerEntityId,
        string _sessionId,
        Vector3i _center,
        int _radius,
        int _durationSeconds,
        int _intervalSeconds,
        Vector3i[] _cropPositions)
    {
        playerEntityId = _playerEntityId;
        sessionId = _sessionId ?? string.Empty;
        center = _center;
        radius = _radius;
        durationSeconds = _durationSeconds;
        intervalSeconds = _intervalSeconds;

        int count = _cropPositions != null ? System.Math.Min(AdvancedFarmingCropGrowthDiagnosticService.MaxCrops, _cropPositions.Length) : 0;
        cropPositions = new Vector3i[count];
        if (count > 0)
            System.Array.Copy(_cropPositions, cropPositions, count);
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        playerEntityId = AdvancedFarmingBinaryCompat.ReadInt(_br);
        sessionId = ((System.IO.BinaryReader)_br).ReadString();
        center = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
        radius = AdvancedFarmingBinaryCompat.ReadInt(_br);
        durationSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br);
        intervalSeconds = AdvancedFarmingBinaryCompat.ReadInt(_br);

        int serializedCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
        if (serializedCount < 0)
            serializedCount = 0;
        int retainedCount = System.Math.Min(AdvancedFarmingCropGrowthDiagnosticService.MaxCrops, serializedCount);
        cropPositions = new Vector3i[retainedCount];
        for (int i = 0; i < serializedCount; i++)
        {
            Vector3i pos = StreamUtils.ReadVector3i((System.IO.BinaryReader)_br);
            if (i < retainedCount)
                cropPositions[i] = pos;
        }
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, playerEntityId);
        ((System.IO.BinaryWriter)_bw).Write(sessionId ?? string.Empty);
        StreamUtils.Write((System.IO.BinaryWriter)_bw, center);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, radius);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, durationSeconds);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, intervalSeconds);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, cropPositions != null ? cropPositions.Length : 0);
        if (cropPositions != null)
        {
            for (int i = 0; i < cropPositions.Length; i++)
                StreamUtils.Write((System.IO.BinaryWriter)_bw, cropPositions[i]);
        }
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        if (_world == null || _world.IsRemote())
            return;

        if (!ValidEntityIdForSender(playerEntityId))
            return;

        EntityPlayer player = _world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null)
            return;

        Vector3i actualCenter = player.GetBlockPosition();
        if (System.Math.Abs(actualCenter.x - center.x) > 3
            || System.Math.Abs(actualCenter.y - center.y) > 4
            || System.Math.Abs(actualCenter.z - center.z) > 3)
        {
            SendAck(false, 0, "Rejected: requested center is not near the sending player.");
            Log.Warning("[AdvancedFarming GrowthTest] server request rejected session=" + sessionId
                + " playerEntityId=" + playerEntityId
                + " requestedCenter=" + center
                + " actualCenter=" + actualCenter);
            return;
        }

        radius = AdvancedFarmingCropGrowthDiagnosticService.ClampRadius(radius);
        durationSeconds = AdvancedFarmingCropGrowthDiagnosticService.ClampDurationSeconds(durationSeconds);
        intervalSeconds = AdvancedFarmingCropGrowthDiagnosticService.ClampIntervalSeconds(intervalSeconds);

        string result = AdvancedFarmingCropGrowthDiagnosticService.Start(
            _world,
            sessionId,
            "SERVER",
            playerEntityId,
            center,
            radius,
            durationSeconds,
            intervalSeconds,
            cropPositions);
        Log.Out(result);
        SendAck(true, cropPositions != null ? cropPositions.Length : 0, "Dedicated-server crop growth diagnostic started.");
    }

    private void SendAck(bool accepted, int cropCount, string message)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return;

        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageCropGrowthDiagnosticAckRebirth>().Setup(sessionId, accepted, cropCount, message),
            _attachedToEntityId: playerEntityId);
    }

    public int GetLength() { return 0; }
}

public class NetPackageCropGrowthDiagnosticAckRebirth : NetPackage
{
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    private string sessionId;
    private bool accepted;
    private int cropCount;
    private string message;

    public NetPackageCropGrowthDiagnosticAckRebirth Setup(string _sessionId, bool _accepted, int _cropCount, string _message)
    {
        sessionId = _sessionId ?? string.Empty;
        accepted = _accepted;
        cropCount = _cropCount;
        message = _message ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        sessionId = ((System.IO.BinaryReader)_br).ReadString();
        accepted = AdvancedFarmingBinaryCompat.ReadBool(_br);
        cropCount = AdvancedFarmingBinaryCompat.ReadInt(_br);
        message = ((System.IO.BinaryReader)_br).ReadString();
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        ((System.IO.BinaryWriter)_bw).Write(sessionId ?? string.Empty);
        AdvancedFarmingBinaryCompat.WriteBool(_bw, accepted);
        AdvancedFarmingBinaryCompat.WriteInt(_bw, cropCount);
        ((System.IO.BinaryWriter)_bw).Write(message ?? string.Empty);
    }

    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        Log.Out("[AdvancedFarming GrowthTest] SERVER_ACK"
            + " session=" + sessionId
            + " accepted=" + accepted
            + " cropCount=" + cropCount
            + " message=" + message);
    }

    public int GetLength() { return 0; }
}

public static class AdvancedFarmingSyncService
{
    private const int SpawnSnapshotRadius = 32;
    private const float SpawnSnapshotRetrySeconds = 2f;
    private const int GeometryLightRefreshRadiusLimit = 16;
    private const float GeometryLightRefreshDelaySeconds = 0.2f;
    private const float GeometryLightRefreshLatestSeconds = 0.75f;
    private const int MaxPendingGeometryLightRefreshes = 64;

    private struct PendingGeometryLightRefresh
    {
        public Vector3i Center;
        public int HorizontalRadius;
        public int VerticalRadius;
        public float DueRealtime;
        public float LatestRealtime;
    }

    private sealed class PendingPlantSnapshot
    {
        public ClientInfo ClientInfo;
        public Vector3i Center;
        public float DueRealtime;
    }

    private static readonly List<PendingPlantSnapshot> s_pendingPlantSnapshots = new List<PendingPlantSnapshot>(4);
    private static readonly List<PendingGeometryLightRefresh> s_pendingGeometryLightRefreshes =
        new List<PendingGeometryLightRefresh>(MaxPendingGeometryLightRefreshes);

    public static void BroadcastRuntimePolicy()
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return;

        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageAdvancedFarmingRuntimePolicyRebirth>().Setup(AdvancedFarmingRuntimePolicy.Enabled, AdvancedFarmingRuntimePolicy.GrowthSeconds, AdvancedFarmingRuntimePolicy.MinimumNaturalSunlight));
    }

    public static void SendPlant(Vector3i pos, int blockId, ulong accumulatedTicks, bool watered)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled || connection == null || !connection.IsServer)
            return;

        TileEntityPlantGrowingRebirth te = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetTileEntity(pos) as TileEntityPlantGrowingRebirth
            : null;
        if (te == null)
            return;

        te.EnsureAuthoritativeIncarnation();
        te.AdvanceStateRevision();
        AdvancedFarmingAuthoritativeLightState lightState = te.AuthoritativeLightState;

        AdvancedFarmingPerfSnapshotService.RecordPlantSync();
        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageUpdatePlantedCropRebirth>().Setup(
                pos, blockId, accumulatedTicks, watered, lightState, te.PlantIncarnation, te.StateRevision));
    }

    public static void QueueNearbyPlantStateSnapshot(ClientInfo clientInfo, Vector3i center)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled || connection == null || !connection.IsServer || clientInfo == null)
            return;

        SendNearbyPlantStateSnapshot(clientInfo, center);
        s_pendingPlantSnapshots.Add(new PendingPlantSnapshot
        {
            ClientInfo = clientInfo,
            Center = center,
            DueRealtime = Time.realtimeSinceStartup + SpawnSnapshotRetrySeconds
        });
    }

    public static void PumpPendingPlantStateSnapshots()
    {
        if (s_pendingPlantSnapshots.Count == 0)
            return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
        {
            s_pendingPlantSnapshots.Clear();
            return;
        }

        float now = Time.realtimeSinceStartup;
        for (int i = s_pendingPlantSnapshots.Count - 1; i >= 0; i--)
        {
            PendingPlantSnapshot pending = s_pendingPlantSnapshots[i];
            if (pending == null || now < pending.DueRealtime)
                continue;

            SendNearbyPlantStateSnapshot(pending.ClientInfo, pending.Center);
            s_pendingPlantSnapshots.RemoveAt(i);
        }
    }

    public static void QueueGeometryDrivenPlantLightRefresh(Vector3i center, int horizontalRadius, int verticalRadius)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled
            || !AdvancedFarmingActiveAreaRegistry.IsAreaActive(
                center, Math.Max(8, horizontalRadius + 8), Math.Max(16, verticalRadius + 8))
            || connection == null
            || !connection.IsServer
            || center.y <= 0
            || center.y >= byte.MaxValue)
        {
            return;
        }

        if (horizontalRadius < 1)
            horizontalRadius = 1;
        if (horizontalRadius > GeometryLightRefreshRadiusLimit)
            horizontalRadius = GeometryLightRefreshRadiusLimit;
        if (verticalRadius < 1)
            verticalRadius = 1;
        if (verticalRadius > GeometryLightRefreshRadiusLimit)
            verticalRadius = GeometryLightRefreshRadiusLimit;

        float now = Time.realtimeSinceStartup;
        float requestedDue = now + GeometryLightRefreshDelaySeconds;

        for (int i = 0; i < s_pendingGeometryLightRefreshes.Count; i++)
        {
            PendingGeometryLightRefresh existing = s_pendingGeometryLightRefreshes[i];
            int dx = Math.Abs(existing.Center.x - center.x);
            int dz = Math.Abs(existing.Center.z - center.z);
            int dy = Math.Abs(existing.Center.y - center.y);
            if (dx > existing.HorizontalRadius + horizontalRadius
                || dz > existing.HorizontalRadius + horizontalRadius
                || dy > existing.VerticalRadius + verticalRadius)
            {
                continue;
            }

            int minX = Math.Min(existing.Center.x - existing.HorizontalRadius, center.x - horizontalRadius);
            int maxX = Math.Max(existing.Center.x + existing.HorizontalRadius, center.x + horizontalRadius);
            int minZ = Math.Min(existing.Center.z - existing.HorizontalRadius, center.z - horizontalRadius);
            int maxZ = Math.Max(existing.Center.z + existing.HorizontalRadius, center.z + horizontalRadius);
            int minY = Math.Min(existing.Center.y - existing.VerticalRadius, center.y - verticalRadius);
            int maxY = Math.Max(existing.Center.y + existing.VerticalRadius, center.y + verticalRadius);

            Vector3i mergedCenter = new Vector3i((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            int mergedHorizontalRadius = Math.Max(
                Math.Max(maxX - mergedCenter.x, mergedCenter.x - minX),
                Math.Max(maxZ - mergedCenter.z, mergedCenter.z - minZ));
            int mergedVerticalRadius = Math.Max(maxY - mergedCenter.y, mergedCenter.y - minY);
            if (mergedHorizontalRadius > GeometryLightRefreshRadiusLimit
                || mergedVerticalRadius > GeometryLightRefreshRadiusLimit)
            {
                continue;
            }

            existing.Center = mergedCenter;
            existing.HorizontalRadius = mergedHorizontalRadius;
            existing.VerticalRadius = mergedVerticalRadius;
            existing.DueRealtime = Math.Min(Math.Max(existing.DueRealtime, requestedDue), existing.LatestRealtime);
            s_pendingGeometryLightRefreshes[i] = existing;
            return;
        }

        if (s_pendingGeometryLightRefreshes.Count >= MaxPendingGeometryLightRefreshes)
            s_pendingGeometryLightRefreshes.RemoveAt(0);

        s_pendingGeometryLightRefreshes.Add(new PendingGeometryLightRefresh
        {
            Center = center,
            HorizontalRadius = horizontalRadius,
            VerticalRadius = verticalRadius,
            DueRealtime = requestedDue,
            LatestRealtime = now + GeometryLightRefreshLatestSeconds
        });
    }

    public static void PumpPendingGeometryDrivenPlantLightRefreshes()
    {
        if (s_pendingGeometryLightRefreshes.Count == 0)
            return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (!AdvancedFarmingRuntimePolicy.Enabled
            || connection == null
            || !connection.IsServer
            || world == null)
        {
            s_pendingGeometryLightRefreshes.Clear();
            return;
        }

        float now = Time.realtimeSinceStartup;
        for (int i = s_pendingGeometryLightRefreshes.Count - 1; i >= 0; i--)
        {
            PendingGeometryLightRefresh pending = s_pendingGeometryLightRefreshes[i];
            if (now < pending.DueRealtime && now < pending.LatestRealtime)
                continue;

            if (!IsGeometryLightRefreshAreaReady(world, pending.Center, pending.HorizontalRadius))
                continue;

            s_pendingGeometryLightRefreshes.RemoveAt(i);
            RefreshAuthoritativePlantLightStatesInVolume(
                world,
                pending.Center.x - pending.HorizontalRadius,
                pending.Center.x + pending.HorizontalRadius,
                Math.Max(1, pending.Center.y - pending.VerticalRadius),
                Math.Min(byte.MaxValue - 1, pending.Center.y + pending.VerticalRadius),
                pending.Center.z - pending.HorizontalRadius,
                pending.Center.z + pending.HorizontalRadius);
            return;
        }
    }

    public static void ClearPendingPlantStateSnapshots()
    {
        s_pendingPlantSnapshots.Clear();
        s_pendingGeometryLightRefreshes.Clear();
    }

    private static bool IsGeometryLightRefreshAreaReady(World world, Vector3i center, int horizontalRadius)
    {
        if (world == null || world.ChunkCache == null)
            return false;

        int minChunkX = World.toChunkXZ(center.x - horizontalRadius);
        int maxChunkX = World.toChunkXZ(center.x + horizontalRadius);
        int minChunkZ = World.toChunkXZ(center.z - horizontalRadius);
        int maxChunkZ = World.toChunkXZ(center.z + horizontalRadius);

        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                Chunk chunk = world.GetChunkSync(chunkX, chunkZ) as Chunk;
                if (chunk == null
                    || chunk.InProgressUnloading
                    || chunk.InProgressLighting
                    || chunk.NeedsLightCalculation)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void SendNearbyPlantStateSnapshot(ClientInfo clientInfo, Vector3i center)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (clientInfo == null || world == null)
            return;

        int minChunkX = World.toChunkXZ(center.x - SpawnSnapshotRadius);
        int maxChunkX = World.toChunkXZ(center.x + SpawnSnapshotRadius);
        int minChunkZ = World.toChunkXZ(center.z - SpawnSnapshotRadius);
        int maxChunkZ = World.toChunkXZ(center.z + SpawnSnapshotRadius);

        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                Chunk chunk = world.GetChunkSync(chunkX, chunkZ) as Chunk;
                if (chunk == null)
                    continue;

                List<TileEntity> tileEntities = chunk.GetTileEntities().list;
                for (int i = 0; i < tileEntities.Count; i++)
                {
                    TileEntityPlantGrowingRebirth te = tileEntities[i] as TileEntityPlantGrowingRebirth;
                    if (te == null)
                        continue;

                    Vector3i pos = te.ToWorldPos();
                    if (Math.Abs(pos.x - center.x) > SpawnSnapshotRadius
                        || Math.Abs(pos.z - center.z) > SpawnSnapshotRadius
                        || Math.Abs(pos.y - center.y) > 16)
                    {
                        continue;
                    }

                    BlockValue blockValue = world.GetBlock(pos);
                    BlockPlantGrowingRebirth plant = blockValue.Block as BlockPlantGrowingRebirth;
                    if (plant == null || plant.IsFullyGrownCrop(blockValue))
                        continue;

                    EnsureAuthoritativeLightState(world, pos, blockValue, plant, te);
                    te.EnsureAuthoritativeIncarnation();
                    clientInfo.SendPackage(
                        NetPackageManager.GetPackage<NetPackageUpdatePlantedCropRebirth>().Setup(
                            pos,
                            blockValue.type,
                            te.AccumulatedTicks,
                            te.bWatered,
                            te.AuthoritativeLightState,
                            te.PlantIncarnation,
                            te.StateRevision));
                }
            }
        }
    }

    public static int RefreshAuthoritativePlantLightStatesInVolume(
        World world,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled
            || !AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots
            || world == null
            || connection == null
            || !connection.IsServer)
        {
            return 0;
        }

        if (minX > maxX || minY > maxY || minZ > maxZ)
            return 0;

        int minChunkX = World.toChunkXZ(minX);
        int maxChunkX = World.toChunkXZ(maxX);
        int minChunkZ = World.toChunkXZ(minZ);
        int maxChunkZ = World.toChunkXZ(maxZ);
        int changedCount = 0;

        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                Chunk chunk = world.GetChunkSync(chunkX, chunkZ) as Chunk;
                if (chunk == null || chunk.InProgressUnloading)
                    continue;

                List<TileEntity> tileEntities = chunk.GetTileEntities().list;
                for (int i = 0; i < tileEntities.Count; i++)
                {
                    TileEntityPlantGrowingRebirth te = tileEntities[i] as TileEntityPlantGrowingRebirth;
                    if (te == null)
                        continue;

                    Vector3i pos = te.ToWorldPos();
                    if (pos.x < minX || pos.x > maxX
                        || pos.y < minY || pos.y > maxY
                        || pos.z < minZ || pos.z > maxZ)
                    {
                        continue;
                    }

                    BlockValue blockValue = world.GetBlock(pos);
                    BlockPlantGrowingRebirth plant = blockValue.Block as BlockPlantGrowingRebirth;
                    if (plant == null || plant.IsFullyGrownCrop(blockValue))
                        continue;

                    bool lightPass = EvaluateAuthoritativePlantLightPass(world, pos, blockValue, plant);
                    if (!te.SetAuthoritativeLightPass(lightPass))
                        continue;

                    te.setModified();
                    AdvancedFarmingHoverTextService.InvalidatePlantState(pos);
                    SendPlant(pos, blockValue.type, te.AccumulatedTicks, te.bWatered);
                    changedCount++;
                }
            }
        }

        return changedCount;
    }

    private static bool EvaluateAuthoritativePlantLightPass(
        World world,
        Vector3i pos,
        BlockValue blockValue,
        BlockPlantGrowingRebirth plant)
    {
        if (world == null || plant == null)
            return false;

        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
        bool isMushroom = !string.IsNullOrEmpty(blockName)
            && blockName.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isMushroom)
            return true;

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            plant.DiagnosticLightLevelStay,
            plant.DiagnosticLightLevelGrow,
            false,
            AdvancedFarmingRuntimePolicy.Enabled);
        return (((light.HasSunLight || light.HasOpenSky) && world.IsDaytime()) || light.HasBlockLight);
    }

    private static void EnsureAuthoritativeLightState(
        World world,
        Vector3i pos,
        BlockValue blockValue,
        BlockPlantGrowingRebirth plant,
        TileEntityPlantGrowingRebirth te)
    {
        if (world == null || plant == null || te == null
            || te.AuthoritativeLightState != AdvancedFarmingAuthoritativeLightState.Unknown)
        {
            return;
        }

        bool lightPass = EvaluateAuthoritativePlantLightPass(world, pos, blockValue, plant);
        if (te.SetAuthoritativeLightPass(lightPass))
        {
            te.AdvanceStateRevision();
            te.setModified();
        }
    }

    public static void SendFarmPlot(Vector3i pos, int waterCount)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled || connection == null || !connection.IsServer)
            return;
        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageUpdateFarmPlotRebirth>().Setup(pos, waterCount));
    }

    public static void SendWaterTank(Vector3i pos, int waterCount)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!AdvancedFarmingRuntimePolicy.Enabled || connection == null || !connection.IsServer)
            return;
        connection.SendPackage(
            NetPackageManager.GetPackage<NetPackageUpdateWaterTankRebirth>().Setup(pos, waterCount));
    }
}
