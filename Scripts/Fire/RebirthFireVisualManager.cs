using System;
using System.Collections.Generic;
using Audio;
using UnityEngine;

#nullable disable

public static class RebirthFireVisualManager
{
    // Final REBIRTH 2.6 rendered active fire only. Extinguish entries are cooldowns,
    // not smoke visuals, so all particle slots are available to active fires.
    // 112 is the normal spatially balanced budget. A 38-slot sticky reserve allows a
    // newly ignited second POI to appear without immediately stripping particles from
    // an existing fire. 150 remains the absolute client-only visual ceiling.
    public const int SoftFireParticleCap = 112;
    public const int FireParticleCap = 150;
    public const int SmokeParticleCap = 0;
    public const int TotalParticleCap = FireParticleCap;
    public const int FireLightCap = 24;
    public const int FireSoundCap = 8;
    public const float FireRenderDistance = 150f;
    public const float SmokeRenderDistance = 0f;
    public const float ReconcileIntervalSeconds = 2f;

    // Reserve normal-budget coverage across the full 150 m range. The remaining
    // hard-cap slots are sticky reserve capacity for additional fire clusters.
    public const float NearBandDistance = 50f;
    public const float MidBandDistance = 100f;
    public const int NearBandBudget = 56;
    public const int MidBandBudget = 36;
    public const int FarBandBudget = 20;

    private const int NearBandCellSize = 8;
    private const int MidBandCellSize = 12;
    private const int FarBandCellSize = 16;
    private const int VerticalCellSize = 4;
    private const float PendingSpawnTimeoutSeconds = 3f;
    private const float NewFireReserveSeconds = 60f;
    private const int ReserveClusterNormalThreshold = 8;
    private const int ClusterLinkRadius = 2;
    // Immutable ordered neighborhood: built once, rather than filtering 125 offsets per fire.
    private static readonly Vector3i[] ClusterNeighborOffsets = CreateClusterNeighborOffsets();

    private static Vector3i[] CreateClusterNeighborOffsets()
    {
        var offsets = new List<Vector3i>(24);
        for (int dx = -ClusterLinkRadius; dx <= ClusterLinkRadius; dx++)
            for (int dy = -ClusterLinkRadius; dy <= ClusterLinkRadius; dy++)
                for (int dz = -ClusterLinkRadius; dz <= ClusterLinkRadius; dz++)
                {
                    int manhattan = Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz);
                    if (manhattan > 0 && manhattan <= ClusterLinkRadius)
                        offsets.Add(new Vector3i(dx, dy, dz));
                }
        return offsets.ToArray();
    }

    private struct Candidate
    {
        public Vector3i Position;
        public float DistanceSquared;
    }

    private struct CellKey : IEquatable<CellKey>
    {
        public int X;
        public int Y;
        public int Z;

        public bool Equals(CellKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CellKey && Equals((CellKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Z;
                return hash;
            }
        }
    }

    private struct PendingDelta
    {
        public int Revision;
        public RebirthFireDeltaEntry Entry;
    }

    private sealed class CandidateComparer : IComparer<Candidate>
    {
        public static readonly CandidateComparer Instance = new CandidateComparer();

        public int Compare(Candidate x, Candidate y)
        {
            int distance = x.DistanceSquared.CompareTo(y.DistanceSquared);
            if (distance != 0)
                return distance;
            int xCompare = x.Position.x.CompareTo(y.Position.x);
            if (xCompare != 0)
                return xCompare;
            int yCompare = x.Position.y.CompareTo(y.Position.y);
            return yCompare != 0 ? yCompare : x.Position.z.CompareTo(y.Position.z);
        }
    }

    private static readonly HashSet<Vector3i> Active = new HashSet<Vector3i>();
    private static readonly Dictionary<Vector3i, ulong> Smoke = new Dictionary<Vector3i, ulong>();
    private static readonly HashSet<Vector3i> PendingSnapshotActive = new HashSet<Vector3i>();
    private static readonly Dictionary<Vector3i, ulong> PendingSnapshotSmoke =
        new Dictionary<Vector3i, ulong>();
    private static readonly List<PendingDelta> PendingSnapshotDeltas = new List<PendingDelta>();
    private static readonly HashSet<ushort> PendingSnapshotChunks = new HashSet<ushort>();
    private static readonly HashSet<Vector3i> RenderedFire = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> RenderedSmoke = new HashSet<Vector3i>();
    private static readonly Dictionary<Vector3i, float> RecentlyAddedFire =
        new Dictionary<Vector3i, float>();
    private static readonly HashSet<Vector3i> ReserveFire = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> NormalCoverage = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> FireCandidatePositions = new HashSet<Vector3i>();
    private static readonly Dictionary<Vector3i, int> ClusterByPosition =
        new Dictionary<Vector3i, int>();
    private static readonly List<int> ClusterNormalCounts = new List<int>();
    private static readonly List<Vector3i> ClusterQueue = new List<Vector3i>(128);
    // SpawnBlockParticleEffect is asynchronous in 3.1: it queues creation and
    // GameManager.updateBlockParticles() instantiates it later. Keep queued positions
    // separately so the two-second reconcile does not repeatedly remove/restart them.
    private static readonly Dictionary<Vector3i, float> PendingParticleSpawns =
        new Dictionary<Vector3i, float>();
    private static readonly HashSet<Vector3i> DesiredFire = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> DesiredSmoke = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> DesiredLight = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> DesiredSound = new HashSet<Vector3i>();
    private static readonly HashSet<Vector3i> Sounding = new HashSet<Vector3i>();
    private static readonly Dictionary<Vector3i, string> SoundNames = new Dictionary<Vector3i, string>();
    private static readonly List<Candidate> FireCandidates = new List<Candidate>(192);
    private static readonly List<Candidate> NearFireCandidates = new List<Candidate>(96);
    private static readonly List<Candidate> MidFireCandidates = new List<Candidate>(96);
    private static readonly List<Candidate> FarFireCandidates = new List<Candidate>(96);
    private static readonly List<Candidate> SmokeCandidates = new List<Candidate>(32);
    private static readonly HashSet<CellKey> SelectedCells = new HashSet<CellKey>();
    private static readonly List<Vector3i> RemovalBuffer = new List<Vector3i>(FireParticleCap);
    private static readonly List<Vector3i> VisibilityReport = new List<Vector3i>(FireParticleCap);
    private static readonly HashSet<string> UnavailableParticlesLogged =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedParticleLoads =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private const float SnapshotAssemblyTimeoutSeconds = 5f;
    private const float SnapshotRequestCooldownSeconds = 2f;

    private static float nextReconcileTime;
    private static float pendingSnapshotStartedAt;
    private static float nextSnapshotRequestTime;
    private static ulong currentWorldEpoch;
    private static ulong pendingSnapshotWorldEpoch;
    private static ulong pendingSnapshotId;
    private static ulong lastCommittedSnapshotId;
    private static int lastRevision = -1;
    private static int pendingSnapshotRevision = -1;
    private static ushort pendingSnapshotChunkCount;
    private static int pendingSnapshotExpectedFires;
    private static int pendingSnapshotExpectedCooldowns;
    private static int pendingSnapshotFireEntries;
    private static int pendingSnapshotCooldownEntries;
    private static bool snapshotResyncNeeded;
    private static bool initialized;
    private static int particlesSpawned;
    private static int particlesRemoved;
    private static int lightsDisabled;
    private static int nearCandidateCount;
    private static int midCandidateCount;
    private static int farCandidateCount;
    private static int nearSelectedCount;
    private static int midSelectedCount;
    private static int farSelectedCount;

    public static int ActiveCount { get { return Active.Count; } }
    public static int CooldownCount { get { return Smoke.Count; } }
    // Kept for compatibility with older diagnostics; this is a cooldown count, not rendered smoke.
    public static int SmokeCount { get { return Smoke.Count; } }
    public static int VisibleFireCount { get { return RenderedFire.Count; } }
    public static int VisibleSmokeCount { get { return RenderedSmoke.Count; } }
    public static int LastRevision { get { return lastRevision; } }
    public static int ParticlesSpawned { get { return particlesSpawned; } }
    public static int ParticlesRemoved { get { return particlesRemoved; } }
    public static int PendingSpawnCount { get { return PendingParticleSpawns.Count; } }
    public static int ReserveFireCount { get { return ReserveFire.Count; } }
    public static int CandidateClusterCount { get { return ClusterNormalCounts.Count; } }
    public static int LightsDisabled { get { return lightsDisabled; } }
    public static int NearCandidateCount { get { return nearCandidateCount; } }
    public static int MidCandidateCount { get { return midCandidateCount; } }
    public static int FarCandidateCount { get { return farCandidateCount; } }
    public static int NearSelectedCount { get { return nearSelectedCount; } }
    public static int MidSelectedCount { get { return midSelectedCount; } }
    public static int FarSelectedCount { get { return farSelectedCount; } }

    public static void Initialize()
    {
        initialized = true;
        nextReconcileTime = 0f;

        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!GameManager.IsDedicatedServer && manager != null && manager.IsServer)
        {
            ApplySnapshot(
                RebirthFireNetwork.GetServerWorldEpoch(),
                RebirthFireService.Instance.Revision,
                RebirthFireService.Instance.SnapshotFires(),
                RebirthFireService.Instance.SnapshotCooldowns());
        }
    }

    public static void ApplyDelta(ulong worldEpoch, int revision, RebirthFireDeltaEntry entry)
    {
        if (!AcceptWorldEpoch(worldEpoch))
            return;

        if (pendingSnapshotRevision >= 0)
        {
            if (revision > pendingSnapshotRevision)
            {
                if (PendingSnapshotDeltas.Count >= RebirthFireDefaults.MaxPendingSnapshotDeltas)
                {
                    AbortPendingSnapshot(true);
                    snapshotResyncNeeded = true;
                    ApplyDeltaImmediate(revision, entry);
                    return;
                }
                PendingSnapshotDeltas.Add(new PendingDelta { Revision = revision, Entry = entry });
            }
            return;
        }
        ApplyDeltaImmediate(revision, entry);
    }

    private static void ApplyDeltaImmediate(int revision, RebirthFireDeltaEntry entry)
    {
        if (revision < lastRevision)
            return;
        if (revision > lastRevision)
            lastRevision = revision;

        if (entry.Operation == RebirthFireDeltaOperation.Add)
        {
            Smoke.Remove(entry.Position);
            Active.Add(entry.Position);
            RecentlyAddedFire[entry.Position] = Time.realtimeSinceStartup;
        }
        else if (entry.Operation == RebirthFireDeltaOperation.Extinguish)
        {
            Active.Remove(entry.Position);
            RecentlyAddedFire.Remove(entry.Position);
            ReserveFire.Remove(entry.Position);
            if (entry.ExpiryWorldTime > 0)
                Smoke[entry.Position] = entry.ExpiryWorldTime;
            else
                Smoke.Remove(entry.Position);
            RemoveRenderedPositionImmediately(entry.Position);
        }
        else
        {
            Active.Remove(entry.Position);
            Smoke.Remove(entry.Position);
            RecentlyAddedFire.Remove(entry.Position);
            ReserveFire.Remove(entry.Position);
            RemoveRenderedPositionImmediately(entry.Position);
        }
    }

    public static void ApplyDeltas(ulong worldEpoch, int revision, IList<RebirthFireDeltaEntry> entries)
    {
        if (!AcceptWorldEpoch(worldEpoch) || revision < lastRevision)
            return;
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
                ApplyDelta(worldEpoch, revision, entries[i]);
        }
        if (pendingSnapshotRevision < 0 && revision > lastRevision)
            lastRevision = revision;
    }

    public static void ApplySnapshot(
        ulong worldEpoch,
        int revision,
        IList<RebirthFireState> fires,
        IDictionary<Vector3i, ulong> cooldowns)
    {
        if (!AcceptWorldEpoch(worldEpoch) || revision < lastRevision)
            return;
        if ((fires != null && fires.Count > RebirthFireDefaults.MaxSnapshotFires) ||
            (cooldowns != null && cooldowns.Count > RebirthFireDefaults.MaxSnapshotCooldowns))
            return;

        ClearPendingSnapshot(false);
        ReplaceAuthoritativeProjection(revision, fires, cooldowns);
    }

    public static void ApplySnapshotChunk(
        ulong worldEpoch,
        ulong snapshotId,
        int revision,
        ushort chunkIndex,
        ushort chunkCount,
        ushort totalFires,
        ushort totalCooldowns,
        IList<RebirthFireState> fires,
        IDictionary<Vector3i, ulong> cooldowns)
    {
        if (worldEpoch == 0 || snapshotId == 0 || chunkCount == 0 ||
            chunkCount > RebirthFireDefaults.MaxSnapshotChunks || chunkIndex >= chunkCount ||
            totalFires > RebirthFireDefaults.MaxSnapshotFires ||
            totalCooldowns > RebirthFireDefaults.MaxSnapshotCooldowns)
            return;
        if (!AcceptWorldEpoch(worldEpoch) || revision < lastRevision)
        {
            snapshotResyncNeeded = true;
            return;
        }
        if (snapshotId <= lastCommittedSnapshotId)
            return;

        if (pendingSnapshotRevision < 0 || pendingSnapshotWorldEpoch != worldEpoch || pendingSnapshotId != snapshotId)
        {
            if (pendingSnapshotRevision >= 0)
            {
                if (worldEpoch < pendingSnapshotWorldEpoch ||
                    (worldEpoch == pendingSnapshotWorldEpoch && snapshotId < pendingSnapshotId))
                    return;

                List<PendingDelta> carry = new List<PendingDelta>();
                if (worldEpoch == pendingSnapshotWorldEpoch)
                {
                    for (int i = 0; i < PendingSnapshotDeltas.Count; i++)
                        if (PendingSnapshotDeltas[i].Revision > revision)
                            carry.Add(PendingSnapshotDeltas[i]);
                }
                ClearPendingSnapshot(false);
                BeginPendingSnapshot(worldEpoch, snapshotId, revision, chunkCount, totalFires, totalCooldowns);
                for (int i = 0; i < carry.Count && i < RebirthFireDefaults.MaxPendingSnapshotDeltas; i++)
                    PendingSnapshotDeltas.Add(carry[i]);
            }
            else
            {
                BeginPendingSnapshot(worldEpoch, snapshotId, revision, chunkCount, totalFires, totalCooldowns);
            }
        }

        if (pendingSnapshotRevision != revision || pendingSnapshotChunkCount != chunkCount ||
            pendingSnapshotExpectedFires != totalFires || pendingSnapshotExpectedCooldowns != totalCooldowns)
        {
            AbortPendingSnapshot(true);
            snapshotResyncNeeded = true;
            return;
        }
        if (!PendingSnapshotChunks.Add(chunkIndex))
            return;

        int fireCount = fires != null ? fires.Count : 0;
        int cooldownCount = cooldowns != null ? cooldowns.Count : 0;
        if (pendingSnapshotFireEntries + fireCount > pendingSnapshotExpectedFires ||
            pendingSnapshotCooldownEntries + cooldownCount > pendingSnapshotExpectedCooldowns)
        {
            AbortPendingSnapshot(true);
            snapshotResyncNeeded = true;
            return;
        }
        pendingSnapshotFireEntries += fireCount;
        pendingSnapshotCooldownEntries += cooldownCount;
        if (fires != null)
            for (int i = 0; i < fires.Count; i++) PendingSnapshotActive.Add(fires[i].Position);
        if (cooldowns != null)
            foreach (KeyValuePair<Vector3i, ulong> pair in cooldowns) PendingSnapshotSmoke[pair.Key] = pair.Value;

        if (PendingSnapshotChunks.Count != pendingSnapshotChunkCount)
            return;
        if (pendingSnapshotFireEntries != pendingSnapshotExpectedFires ||
            pendingSnapshotCooldownEntries != pendingSnapshotExpectedCooldowns)
        {
            AbortPendingSnapshot(true);
            snapshotResyncNeeded = true;
            return;
        }

        int baselineRevision = pendingSnapshotRevision;
        ulong committedId = pendingSnapshotId;
        List<PendingDelta> deltas = new List<PendingDelta>(PendingSnapshotDeltas);
        ReplaceAuthoritativeProjection(baselineRevision, PendingSnapshotActiveAsStates(), PendingSnapshotSmoke);
        lastCommittedSnapshotId = committedId;
        ClearPendingSnapshot(false);
        deltas.Sort(delegate(PendingDelta a, PendingDelta b) { return a.Revision.CompareTo(b.Revision); });
        for (int i = 0; i < deltas.Count; i++)
            if (deltas[i].Revision > baselineRevision) ApplyDeltaImmediate(deltas[i].Revision, deltas[i].Entry);
        nextReconcileTime = 0f;
    }

    private static List<RebirthFireState> PendingSnapshotActiveAsStates()
    {
        List<RebirthFireState> states = new List<RebirthFireState>(PendingSnapshotActive.Count);
        foreach (Vector3i position in PendingSnapshotActive)
            states.Add(new RebirthFireState { Position = position });
        return states;
    }

    private static void BeginPendingSnapshot(ulong worldEpoch, ulong snapshotId, int revision, ushort chunkCount, int totalFires, int totalCooldowns)
    {
        pendingSnapshotWorldEpoch = worldEpoch;
        pendingSnapshotId = snapshotId;
        pendingSnapshotRevision = revision;
        pendingSnapshotChunkCount = chunkCount;
        pendingSnapshotExpectedFires = totalFires;
        pendingSnapshotExpectedCooldowns = totalCooldowns;
        pendingSnapshotFireEntries = 0;
        pendingSnapshotCooldownEntries = 0;
        pendingSnapshotStartedAt = Time.realtimeSinceStartup;
        PendingSnapshotActive.Clear();
        PendingSnapshotSmoke.Clear();
        PendingSnapshotDeltas.Clear();
        PendingSnapshotChunks.Clear();
    }

    private static bool AcceptWorldEpoch(ulong worldEpoch)
    {
        if (worldEpoch == 0)
            return false;
        if (currentWorldEpoch == 0)
        {
            currentWorldEpoch = worldEpoch;
            return true;
        }
        if (worldEpoch == currentWorldEpoch)
            return true;
        if (worldEpoch < currentWorldEpoch)
            return false;

        ResetProjectionForWorldEpoch(worldEpoch);
        return true;
    }

    private static void ResetProjectionForWorldEpoch(ulong worldEpoch)
    {
        RemoveAllRendered();
        Active.Clear();
        Smoke.Clear();
        RecentlyAddedFire.Clear();
        ReserveFire.Clear();
        NormalCoverage.Clear();
        ClearPendingSnapshot(false);
        currentWorldEpoch = worldEpoch;
        lastCommittedSnapshotId = 0;
        lastRevision = -1;
        nextReconcileTime = 0f;
    }

    private static void ReplaceAuthoritativeProjection(
        int revision,
        IList<RebirthFireState> fires,
        IDictionary<Vector3i, ulong> cooldowns)
    {
        Active.Clear();
        Smoke.Clear();
        RecentlyAddedFire.Clear();
        ReserveFire.Clear();
        NormalCoverage.Clear();
        if (fires != null)
            for (int i = 0; i < fires.Count; i++) Active.Add(fires[i].Position);
        if (cooldowns != null)
            foreach (KeyValuePair<Vector3i, ulong> pair in cooldowns) Smoke[pair.Key] = pair.Value;
        lastRevision = revision;
        nextReconcileTime = 0f;
    }

    private static void AbortPendingSnapshot(bool replayDeltas)
    {
        List<PendingDelta> deltas = replayDeltas ? new List<PendingDelta>(PendingSnapshotDeltas) : null;
        ClearPendingSnapshot(false);
        if (deltas == null)
            return;
        deltas.Sort(delegate(PendingDelta a, PendingDelta b) { return a.Revision.CompareTo(b.Revision); });
        for (int i = 0; i < deltas.Count; i++) ApplyDeltaImmediate(deltas[i].Revision, deltas[i].Entry);
    }

    private static void ClearPendingSnapshot(bool clearResync)
    {
        PendingSnapshotActive.Clear();
        PendingSnapshotSmoke.Clear();
        PendingSnapshotDeltas.Clear();
        PendingSnapshotChunks.Clear();
        pendingSnapshotRevision = -1;
        pendingSnapshotWorldEpoch = 0;
        pendingSnapshotId = 0;
        pendingSnapshotChunkCount = 0;
        pendingSnapshotExpectedFires = 0;
        pendingSnapshotExpectedCooldowns = 0;
        pendingSnapshotFireEntries = 0;
        pendingSnapshotCooldownEntries = 0;
        pendingSnapshotStartedAt = 0f;
        if (clearResync) snapshotResyncNeeded = false;
    }

    private static void MaintainSnapshotAssembly(World world)
    {
        float now = Time.realtimeSinceStartup;
        if (pendingSnapshotRevision >= 0 && now - pendingSnapshotStartedAt >= SnapshotAssemblyTimeoutSeconds)
        {
            AbortPendingSnapshot(true);
            snapshotResyncNeeded = true;
        }
        if (!snapshotResyncNeeded || now < nextSnapshotRequestTime)
            return;
        nextSnapshotRequestTime = now + SnapshotRequestCooldownSeconds;
        if (RebirthFireNetwork.RequestSnapshotResync(world))
            snapshotResyncNeeded = false;
    }

    public static bool IsFireParticleVisible(Vector3i position)
    {
        return RenderedFire.Contains(position) &&
               GameManager.Instance != null &&
               GameManager.Instance.HasBlockParticleEffect(position);
    }

    public static int CountNearby(Vector3i center, int range)
    {
        int count = 0;
        int radius = Math.Max(0, range);
        foreach (Vector3i position in RenderedFire)
        {
            if (Math.Abs(position.x - center.x) <= radius &&
                Math.Abs(position.z - center.z) <= radius &&
                Math.Abs(position.y - center.y) <= 2 &&
                GameManager.Instance != null &&
                GameManager.Instance.HasBlockParticleEffect(position))
                count++;
        }
        return count;
    }

    public static void Update(World world)
    {
        if (!initialized || GameManager.IsDedicatedServer || world == null || GameManager.Instance == null)
            return;

        MaintainSnapshotAssembly(world);

        // Confirm queued effects every frame. The actual Transform does not exist until
        // GameManager processes its block-particle queue, normally later in the frame.
        // Enforce the light cap immediately after confirmation so newly-instantiated
        // particles above the light budget do not run enabled until the next 2-second pass.
        bool confirmedParticle = ConfirmPendingParticleSpawns();
        if (confirmedParticle)
            ReconcileLights();

        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player != null)
            CheckLocalPlayerFireContact(player);

        if (Time.realtimeSinceStartup < nextReconcileTime)
            return;
        nextReconcileTime = Time.realtimeSinceStartup + ReconcileIntervalSeconds;

        if (player == null)
        {
            RemoveAllRendered();
            return;
        }

        ulong worldTime = world.worldTime;
        PruneStaleActiveBlocks(world);
        PruneExpiredSmoke(worldTime);
        BuildDesiredSets(world, player.position, worldTime);
        ReconcileParticles(world);
        ReconcileLights();
        ReconcileSounds(world);
        ReportVisibility(player.entityId);
    }

    private static void BuildDesiredSets(World world, Vector3 playerPosition, ulong worldTime)
    {
        FireCandidates.Clear();
        NearFireCandidates.Clear();
        MidFireCandidates.Clear();
        FarFireCandidates.Clear();
        SmokeCandidates.Clear();
        DesiredFire.Clear();
        DesiredSmoke.Clear();
        DesiredLight.Clear();
        DesiredSound.Clear();

        float nearDistanceSquared = NearBandDistance * NearBandDistance;
        float midDistanceSquared = MidBandDistance * MidBandDistance;
        float fireDistanceSquared = FireRenderDistance * FireRenderDistance;

        foreach (Vector3i position in Active)
        {
            float distance = (position.ToVector3Center() - playerPosition).sqrMagnitude;
            if (distance > fireDistanceSquared)
                continue;

            Candidate candidate = new Candidate
            {
                Position = position,
                DistanceSquared = distance
            };
            FireCandidates.Add(candidate);
            if (distance <= nearDistanceSquared)
                NearFireCandidates.Add(candidate);
            else if (distance <= midDistanceSquared)
                MidFireCandidates.Add(candidate);
            else
                FarFireCandidates.Add(candidate);
        }

        FireCandidates.Sort(CandidateComparer.Instance);
        NearFireCandidates.Sort(CandidateComparer.Instance);
        MidFireCandidates.Sort(CandidateComparer.Instance);
        FarFireCandidates.Sort(CandidateComparer.Instance);

        nearCandidateCount = NearFireCandidates.Count;
        midCandidateCount = MidFireCandidates.Count;
        farCandidateCount = FarFireCandidates.Count;

        // Reserve representation for each distance band. Existing particles are
        // preferred so stable fires are not removed/recreated every two seconds.
        SelectBand(NearFireCandidates, NearBandBudget, NearBandCellSize);
        SelectBand(MidFireCandidates, MidBandBudget, MidBandCellSize);
        SelectBand(FarFireCandidates, FarBandBudget, FarBandCellSize);

        // Roll unused reservations into the remaining global budget. Prefer any
        // already-rendered candidate first, then fill by distance.
        FillRemaining(FireCandidates, true, SoftFireParticleCap);
        FillRemaining(FireCandidates, false, SoftFireParticleCap);

        // Snapshot the 112 normal representatives, then retain the separate-cluster
        // reserve and add newly ignited under-represented areas without removing the
        // established POI. The reserve is capped at 38 by the 150 hard ceiling.
        NormalCoverage.Clear();
        foreach (Vector3i position in DesiredFire)
            NormalCoverage.Add(position);
        FillClusterReserve(FireCandidates);

        nearSelectedCount = 0;
        midSelectedCount = 0;
        farSelectedCount = 0;
        foreach (Vector3i position in DesiredFire)
        {
            float distance = (position.ToVector3Center() - playerPosition).sqrMagnitude;
            if (distance <= nearDistanceSquared)
                nearSelectedCount++;
            else if (distance <= midDistanceSquared)
                midSelectedCount++;
            else
                farSelectedCount++;
        }

        // Light and sound ownership are independent projections of the selected fires.
        // Keep their caps deterministic in the same distance ordering used for particles.
        int lightCount = 0;
        int soundCount = 0;
        for (int i = 0; i < FireCandidates.Count; i++)
        {
            Vector3i position = FireCandidates[i].Position;
            if (!DesiredFire.Contains(position))
                continue;
            if (lightCount < FireLightCap)
            {
                DesiredLight.Add(position);
                lightCount++;
            }
            if (soundCount < FireSoundCap)
            {
                RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(world.GetBlock(position));
                if (!string.IsNullOrEmpty(profile.FireSound))
                {
                    DesiredSound.Add(position);
                    soundCount++;
                }
            }
        }

        // Final 2.6 kept extinguished positions only as re-ignition cooldowns.
        // Its smoke particle spawn was commented out, so DesiredSmoke intentionally stays empty.
    }

    private static void SelectBand(List<Candidate> candidates, int budget, int cellSize)
    {
        if (budget <= 0 || candidates.Count == 0 || DesiredFire.Count >= SoftFireParticleCap)
            return;

        int targetCount = Math.Min(SoftFireParticleCap, DesiredFire.Count + budget);
        SelectedCells.Clear();

        // Retain one already-rendered representative per spatial cell first.
        for (int i = 0; i < candidates.Count && DesiredFire.Count < targetCount; i++)
        {
            Candidate candidate = candidates[i];
            if (!RenderedFire.Contains(candidate.Position))
                continue;
            CellKey cell = GetCellKey(candidate.Position, cellSize);
            if (SelectedCells.Add(cell))
                DesiredFire.Add(candidate.Position);
        }

        // Add one representative per remaining cell so a dense wall of nearby
        // blocks cannot consume the whole distance-band reservation.
        for (int i = 0; i < candidates.Count && DesiredFire.Count < targetCount; i++)
        {
            Candidate candidate = candidates[i];
            if (DesiredFire.Contains(candidate.Position))
                continue;
            CellKey cell = GetCellKey(candidate.Position, cellSize);
            if (SelectedCells.Add(cell))
                DesiredFire.Add(candidate.Position);
        }

        // If the band has fewer occupied cells than its budget, retain additional
        // existing effects before allocating new dense-cluster particles.
        for (int i = 0; i < candidates.Count && DesiredFire.Count < targetCount; i++)
        {
            Candidate candidate = candidates[i];
            if (RenderedFire.Contains(candidate.Position))
                DesiredFire.Add(candidate.Position);
        }

        for (int i = 0; i < candidates.Count && DesiredFire.Count < targetCount; i++)
            DesiredFire.Add(candidates[i].Position);
    }

    private static void FillRemaining(List<Candidate> candidates, bool renderedOnly, int cap)
    {
        for (int i = 0; i < candidates.Count && DesiredFire.Count < cap; i++)
        {
            Vector3i position = candidates[i].Position;
            if (DesiredFire.Contains(position))
                continue;
            if (renderedOnly && !RenderedFire.Contains(position))
                continue;
            DesiredFire.Add(position);
        }
    }

    private static void FillClusterReserve(List<Candidate> candidates)
    {
        FireCandidatePositions.Clear();
        for (int i = 0; i < candidates.Count; i++)
            FireCandidatePositions.Add(candidates[i].Position);
        BuildCandidateClusters();

        float now = Time.realtimeSinceStartup;
        RemovalBuffer.Clear();
        foreach (KeyValuePair<Vector3i, float> pair in RecentlyAddedFire)
        {
            if (!Active.Contains(pair.Key) || now - pair.Value > NewFireReserveSeconds)
                RemovalBuffer.Add(pair.Key);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
            RecentlyAddedFire.Remove(RemovalBuffer[i]);

        // Reserve ownership is sticky until the fire ends, leaves the 150 m range, or
        // becomes one of the normal 112 representatives. That prevents the first POI
        // from losing particles merely because a second POI begins burning.
        RemovalBuffer.Clear();
        foreach (Vector3i position in ReserveFire)
        {
            if (!Active.Contains(position) ||
                !FireCandidatePositions.Contains(position) ||
                NormalCoverage.Contains(position))
                RemovalBuffer.Add(position);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
            ReserveFire.Remove(RemovalBuffer[i]);

        foreach (Vector3i position in ReserveFire)
        {
            if (DesiredFire.Count >= FireParticleCap)
                break;
            DesiredFire.Add(position);
        }

        if (DesiredFire.Count >= FireParticleCap || RecentlyAddedFire.Count == 0)
            return;

        SelectedCells.Clear();
        for (int i = 0; i < candidates.Count && DesiredFire.Count < FireParticleCap; i++)
        {
            Vector3i position = candidates[i].Position;
            if (!IsReserveCandidate(position))
                continue;
            CellKey cell = GetCellKey(position, MidBandCellSize);
            if (SelectedCells.Add(cell))
            {
                DesiredFire.Add(position);
                ReserveFire.Add(position);
            }
        }
        for (int i = 0; i < candidates.Count && DesiredFire.Count < FireParticleCap; i++)
        {
            Vector3i position = candidates[i].Position;
            if (!IsReserveCandidate(position))
                continue;
            DesiredFire.Add(position);
            ReserveFire.Add(position);
        }
    }

    private static bool IsReserveCandidate(Vector3i position)
    {
        if (DesiredFire.Contains(position) ||
            !RecentlyAddedFire.ContainsKey(position) ||
            ReserveFire.Count >= FireParticleCap - SoftFireParticleCap)
            return false;

        int cluster;
        if (!ClusterByPosition.TryGetValue(position, out cluster) ||
            cluster < 0 || cluster >= ClusterNormalCounts.Count)
            return false;
        return ClusterNormalCounts[cluster] < ReserveClusterNormalThreshold;
    }

    private static void BuildCandidateClusters()
    {
        ClusterByPosition.Clear();
        ClusterNormalCounts.Clear();
        int cluster = 0;

        foreach (Vector3i start in FireCandidatePositions)
        {
            if (ClusterByPosition.ContainsKey(start))
                continue;

            ClusterByPosition[start] = cluster;
            ClusterQueue.Clear();
            ClusterQueue.Add(start);
            for (int queueIndex = 0; queueIndex < ClusterQueue.Count; queueIndex++)
            {
                Vector3i current = ClusterQueue[queueIndex];
                for (int offsetIndex = 0; offsetIndex < ClusterNeighborOffsets.Length; offsetIndex++)
                {
                    Vector3i offset = ClusterNeighborOffsets[offsetIndex];
                    Vector3i neighbor = new Vector3i(
                        current.x + offset.x,
                        current.y + offset.y,
                        current.z + offset.z);
                    if (!FireCandidatePositions.Contains(neighbor) ||
                        ClusterByPosition.ContainsKey(neighbor))
                        continue;
                    ClusterByPosition[neighbor] = cluster;
                    ClusterQueue.Add(neighbor);
                }
            }

            ClusterNormalCounts.Add(0);
            cluster++;
        }

        foreach (Vector3i position in NormalCoverage)
        {
            int clusterIndex;
            if (ClusterByPosition.TryGetValue(position, out clusterIndex))
                ClusterNormalCounts[clusterIndex]++;
        }
    }

    private static CellKey GetCellKey(Vector3i position, int horizontalCellSize)
    {
        return new CellKey
        {
            X = FloorDivide(position.x, horizontalCellSize),
            Y = FloorDivide(position.y, VerticalCellSize),
            Z = FloorDivide(position.z, horizontalCellSize)
        };
    }

    private static int FloorDivide(int value, int divisor)
    {
        int quotient = value / divisor;
        int remainder = value % divisor;
        if (remainder != 0 && value < 0)
            quotient--;
        return quotient;
    }

    private static void ReconcileParticles(World world)
    {
        RemoveUndesired(RenderedFire, DesiredFire);
        RemoveUndesired(RenderedSmoke, DesiredSmoke);
        RemoveMissingRendered(RenderedFire, DesiredFire);
        RemoveMissingRendered(RenderedSmoke, DesiredSmoke);

        foreach (Vector3i position in DesiredFire)
        {
            if (RenderedFire.Contains(position))
                continue;
            BlockValue block = world.GetBlock(position);
            RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(block);
            if (block.isair || !profile.Flammable)
                continue;
            if (SpawnParticle(position, profile.FireParticle, 1f))
                RenderedFire.Add(position);
        }

        foreach (Vector3i position in DesiredSmoke)
        {
            if (RenderedSmoke.Contains(position))
                continue;
            BlockValue block = world.GetBlock(position);
            RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(block);
            if (SpawnParticle(position, profile.SmokeParticle, 0f))
                RenderedSmoke.Add(position);
        }
    }

    private static void RemoveMissingRendered(HashSet<Vector3i> rendered, HashSet<Vector3i> desired)
    {
        RemovalBuffer.Clear();
        foreach (Vector3i position in rendered)
        {
            if (!desired.Contains(position))
                continue;
            if (PendingParticleSpawns.ContainsKey(position))
                continue;
            if (GameManager.Instance != null && GameManager.Instance.HasBlockParticleEffect(position))
                continue;
            RemovalBuffer.Add(position);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
            rendered.Remove(RemovalBuffer[i]);
    }

    private static void RemoveUndesired(HashSet<Vector3i> rendered, HashSet<Vector3i> desired)
    {
        RemovalBuffer.Clear();
        foreach (Vector3i position in rendered)
        {
            if (!desired.Contains(position))
                RemovalBuffer.Add(position);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            Vector3i position = RemovalBuffer[i];
            RemoveParticle(position);
            rendered.Remove(position);
        }
    }

    public static bool IsParticleAssetUsable(string particle)
    {
        if (string.IsNullOrEmpty(particle))
            return false;

        Transform prefab;
        return ParticleEffect.loadedTs.TryGetValue(ParticleEffect.ToId(particle), out prefab) && prefab != null;
    }

    public static bool EnsureParticleAssetAvailable(string particle)
    {
        if (string.IsNullOrEmpty(particle))
            return false;

        int particleId = ParticleEffect.ToId(particle);
        Transform prefab;
        if (FailedParticleLoads.Contains(particle))
            return false;
        if (ParticleEffect.loadedTs.TryGetValue(particleId, out prefab))
        {
            if (prefab != null)
                return true;

            // ParticleEffect.LoadAsset adds the dictionary entry even when the bundle load
            // returned null. Remove that poisoned entry so a corrected asset can be retried.
            ParticleEffect.loadedTs.Remove(particleId);
            #if DEBUG
            RebirthFireDiagnostics.VisualNullPrefab++;
            #endif
        }

        try
        {
            if (particle.IndexOf("#", StringComparison.Ordinal) == 0)
            {
                #if DEBUG
                RebirthFireDiagnostics.VisualLoadAttempts++;
                #endif
                ParticleEffect.LoadAsset(particle);
            }
        }
        catch (Exception ex)
        {
            #if DEBUG
            RebirthFireDiagnostics.VisualExceptions++;
            #endif
            FailedParticleLoads.Add(particle);
            if (UnavailableParticlesLogged.Add(particle))
            {
                Log.Warning(
                    "[REBIRTH Fire] Particle asset load threw for '" + particle
                    + "': " + ex.GetType().Name + ": " + ex.Message);
            }
            return false;
        }

        if (ParticleEffect.loadedTs.TryGetValue(particleId, out prefab))
        {
            if (prefab != null)
                return true;

            ParticleEffect.loadedTs.Remove(particleId);
            #if DEBUG
            RebirthFireDiagnostics.VisualNullPrefab++;
            #endif
        }

        #if DEBUG
        RebirthFireDiagnostics.VisualUnavailable++;
        #endif
        FailedParticleLoads.Add(particle);
        if (UnavailableParticlesLogged.Add(particle))
        {
            Log.Warning(
                "[REBIRTH Fire] Particle asset is unavailable or loaded as a null prefab: " + particle
                + ". Expected bundle at Mods/zzz_REBIRTH__3_0/Resources/gupFireParticles.unity3d"
                + " with prefab gupBeavis05-Heavy.");
        }
        return false;
    }

    private static bool SpawnParticle(Vector3i position, string particle, float lightValue)
    {
        #if DEBUG
        RebirthFireDiagnostics.VisualSpawnAttempts++;
        #endif
        if (string.IsNullOrEmpty(particle) || GameManager.Instance == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.VisualUnavailable++;
            #endif
            return false;
        }

        if (!EnsureParticleAssetAvailable(particle))
            return false;

        try
        {
            // A previously queued effect is already a successful logical render request.
            // Do not queue it again while waiting for GameManager to instantiate it.
            if (PendingParticleSpawns.ContainsKey(position))
                return true;

            // Adopt an already instantiated effect instead of restarting it. This mirrors
            // the 2.6 ParticlePlaying behavior and prevents synchronized two-second resets.
            if (GameManager.Instance.HasBlockParticleEffect(position))
            {
                Transform existing = GameManager.Instance.GetBlockParticleEffect(position);
                if (existing != null)
                    return true;

                // Remove a stale null entry before retrying.
                GameManager.Instance.RemoveBlockParticleEffect(position);
            }

            GameManager.Instance.SpawnBlockParticleEffect(
                position,
                new ParticleEffect(
                    particle,
                    position.ToVector3Center(),
                    Quaternion.identity,
                    lightValue,
                    Color.white));

            PendingParticleSpawns[position] = Time.realtimeSinceStartup;
            return true;
        }
        catch (Exception ex)
        {
            PendingParticleSpawns.Remove(position);
            #if DEBUG
            RebirthFireDiagnostics.VisualExceptions++;
            #endif
            if (UnavailableParticlesLogged.Add("exception:" + particle))
            {
                Log.Warning(
                    "[REBIRTH Fire] Particle spawn queue failed position=" + position
                    + " particle=" + particle
                    + " error=" + ex.GetType().Name + ": " + ex.Message);
            }
            return false;
        }
    }

    private static bool ConfirmPendingParticleSpawns()
    {
        if (PendingParticleSpawns.Count == 0 || GameManager.Instance == null)
            return false;

        bool confirmedAny = false;
        float now = Time.realtimeSinceStartup;
        RemovalBuffer.Clear();
        foreach (KeyValuePair<Vector3i, float> pair in PendingParticleSpawns)
        {
            Vector3i position = pair.Key;
            if (GameManager.Instance.HasBlockParticleEffect(position))
            {
                Transform transform = GameManager.Instance.GetBlockParticleEffect(position);
                if (transform != null)
                {
                    particlesSpawned++;
                    #if DEBUG
                    RebirthFireDiagnostics.VisualSpawnSuccess++;
                    #endif
                    confirmedAny = true;
                    RemovalBuffer.Add(position);
                    continue;
                }
            }

            if (now - pair.Value < PendingSpawnTimeoutSeconds)
                continue;

            // Remove both an unprocessed queue entry and any stale null dictionary entry.
            GameManager.Instance.RemoveBlockParticleEffect(position);
            RenderedFire.Remove(position);
            RenderedSmoke.Remove(position);
            #if DEBUG
            RebirthFireDiagnostics.VisualSpawnUntracked++;
            #endif
            RemovalBuffer.Add(position);

            if (UnavailableParticlesLogged.Add("spawn-timeout:" + position))
            {
                Log.Warning(
                    "[REBIRTH Fire] Queued block particle was not instantiated within "
                    + PendingSpawnTimeoutSeconds + " seconds. position=" + position);
            }
        }

        for (int i = 0; i < RemovalBuffer.Count; i++)
            PendingParticleSpawns.Remove(RemovalBuffer[i]);
        return confirmedAny;
    }

    private static void RemoveParticle(Vector3i position)
    {
        bool wasPending = PendingParticleSpawns.Remove(position);
        if (GameManager.Instance == null)
            return;

        bool wasInstantiated = GameManager.Instance.HasBlockParticleEffect(position);
        if (wasPending || wasInstantiated)
            GameManager.Instance.RemoveBlockParticleEffect(position);
        if (wasInstantiated)
            particlesRemoved++;
    }

    private static void ReconcileLights()
    {
        if (GameManager.Instance == null)
            return;

        // Sweep every rendered fire, not merely the first N candidates. Delayed native
        // particle instantiation can otherwise leave an excess prefab light enabled.
        foreach (Vector3i position in RenderedFire)
        {
            if (!GameManager.Instance.HasBlockParticleEffect(position))
                continue;
            Transform transform = GameManager.Instance.GetBlockParticleEffect(position);
            if (transform == null)
                continue;
            Light light = transform.GetComponentInChildren<Light>();
            if (light == null)
                continue;
            bool shouldEnable = DesiredLight.Contains(position);
            if (light.enabled && !shouldEnable)
                lightsDisabled++;
            light.enabled = shouldEnable;
        }
    }

    private static void ReconcileSounds(World world)
    {
        if (world == null)
            return;

        foreach (Vector3i position in DesiredSound)
        {
            if (!RenderedFire.Contains(position) || Sounding.Contains(position))
                continue;
            RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(world.GetBlock(position));
            if (string.IsNullOrEmpty(profile.FireSound))
                continue;
            Manager.Play(position.ToVector3Center(), profile.FireSound);
            Sounding.Add(position);
            SoundNames[position] = profile.FireSound;
        }

        RemovalBuffer.Clear();
        foreach (Vector3i position in Sounding)
        {
            if (!DesiredSound.Contains(position))
                RemovalBuffer.Add(position);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            Vector3i position = RemovalBuffer[i];
            string sound;
            if (SoundNames.TryGetValue(position, out sound))
                Manager.Stop(position.ToVector3Center(), sound);
            Sounding.Remove(position);
            SoundNames.Remove(position);
        }
    }

    private static void RemoveRenderedPositionImmediately(Vector3i position)
    {
        PendingParticleSpawns.Remove(position);
        DesiredFire.Remove(position);
        DesiredSmoke.Remove(position);
        DesiredLight.Remove(position);
        DesiredSound.Remove(position);
        NormalCoverage.Remove(position);
        FireCandidatePositions.Remove(position);
        ClusterByPosition.Remove(position);

        bool wasRendered = RenderedFire.Remove(position);
        wasRendered |= RenderedSmoke.Remove(position);
        if (wasRendered || (GameManager.Instance != null && GameManager.Instance.HasBlockParticleEffect(position)))
            RemoveParticle(position);

        string sound;
        if (SoundNames.TryGetValue(position, out sound))
            Manager.Stop(position.ToVector3Center(), sound);
        SoundNames.Remove(position);
        Sounding.Remove(position);
    }

    private static void CheckLocalPlayerFireContact(EntityPlayerLocal player)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || player == null)
            return;

        // EntityAlive invokes Block.OnEntityWalking only on the authoritative world in 3.1.
        // The local visual loop therefore checks the same two block positions for every local
        // player and acts only when the exact fire particle is currently instantiated.
        Vector3i standing = player.blockPosStandingOn;
        if (IsFireParticleVisible(standing))
        {
            RebirthFireGameplayPatches.TryHandleLocalVisibleContact(player, standing);
            return;
        }

        Vector3i above = standing + Vector3i.up;
        if (IsFireParticleVisible(above))
            RebirthFireGameplayPatches.TryHandleLocalVisibleContact(player, above);
    }

    private static void ReportVisibility(int playerEntityId)
    {
        VisibilityReport.Clear();
        foreach (Vector3i position in RenderedFire)
        {
            if (GameManager.Instance != null && GameManager.Instance.HasBlockParticleEffect(position))
                VisibilityReport.Add(position);
        }

        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null)
            return;
        if (manager.IsServer)
            RebirthFireService.Instance.RecordVisibility(playerEntityId, VisibilityReport);
        else
            manager.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthFireVisibilityBatch>()
                    .Setup(playerEntityId, VisibilityReport),
                false);
    }


    private static void PruneStaleActiveBlocks(World world)
    {
        if (world == null || Active.Count == 0) return;
        RemovalBuffer.Clear();
        foreach (Vector3i position in Active)
        {
            if (world.GetChunkFromWorldPos(position) == null) continue;
            BlockValue block = world.GetBlock(position);
            RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(block);
            if (block.isair || !profile.Flammable) RemovalBuffer.Add(position);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            Vector3i position = RemovalBuffer[i];
            Active.Remove(position);
            RecentlyAddedFire.Remove(position);
            ReserveFire.Remove(position);
            DesiredFire.Remove(position);
            RenderedFire.Remove(position);
            RemoveParticle(position);
            #if DEBUG
            RebirthFireDiagnostics.VisualStaleBlockRemovals++;
            #endif
        }
    }

    private static void PruneExpiredSmoke(ulong worldTime)
    {
        RemovalBuffer.Clear();
        foreach (KeyValuePair<Vector3i, ulong> pair in Smoke)
        {
            if (pair.Value <= worldTime)
                RemovalBuffer.Add(pair.Key);
        }
        for (int i = 0; i < RemovalBuffer.Count; i++)
            Smoke.Remove(RemovalBuffer[i]);
    }

    private static void RemoveAllRendered()
    {
        RemovalBuffer.Clear();
        foreach (Vector3i position in RenderedFire)
            RemovalBuffer.Add(position);
        foreach (Vector3i position in RenderedSmoke)
            RemovalBuffer.Add(position);
        for (int i = 0; i < RemovalBuffer.Count; i++)
            RemoveParticle(RemovalBuffer[i]);
        RenderedFire.Clear();
        RenderedSmoke.Clear();
        RecentlyAddedFire.Clear();
        ReserveFire.Clear();
        NormalCoverage.Clear();
        FireCandidatePositions.Clear();
        ClusterByPosition.Clear();
        ClusterNormalCounts.Clear();
        ClusterQueue.Clear();

        RemovalBuffer.Clear();
        foreach (Vector3i position in Sounding)
            RemovalBuffer.Add(position);
        for (int i = 0; i < RemovalBuffer.Count; i++)
        {
            Vector3i position = RemovalBuffer[i];
            string sound;
            if (SoundNames.TryGetValue(position, out sound))
                Manager.Stop(position.ToVector3Center(), sound);
        }
        Sounding.Clear();
        SoundNames.Clear();
    }

    public static void RemovePositionImmediately(Vector3i position)
    {
        Active.Remove(position);
        Smoke.Remove(position);
        PendingSnapshotActive.Remove(position);
        PendingSnapshotSmoke.Remove(position);
        RecentlyAddedFire.Remove(position);
        ReserveFire.Remove(position);
        NormalCoverage.Remove(position);
        FireCandidatePositions.Remove(position);
        ClusterByPosition.Remove(position);
        RemoveRenderedPositionImmediately(position);
    }

    public static void ClearAll()
    {
        RemoveAllRendered();
        Active.Clear();
        Smoke.Clear();
        PendingSnapshotActive.Clear();
        PendingSnapshotSmoke.Clear();
        PendingSnapshotDeltas.Clear();
        PendingSnapshotChunks.Clear();
        PendingParticleSpawns.Clear();
        pendingSnapshotRevision = -1;
        pendingSnapshotWorldEpoch = 0;
        pendingSnapshotId = 0;
        pendingSnapshotChunkCount = 0;
        pendingSnapshotExpectedFires = 0;
        pendingSnapshotExpectedCooldowns = 0;
        pendingSnapshotFireEntries = 0;
        pendingSnapshotCooldownEntries = 0;
        pendingSnapshotStartedAt = 0f;
        currentWorldEpoch = 0;
        lastCommittedSnapshotId = 0;
        snapshotResyncNeeded = false;
        nextSnapshotRequestTime = 0f;
        DesiredFire.Clear();
        DesiredSmoke.Clear();
        DesiredLight.Clear();
        DesiredSound.Clear();
        FireCandidates.Clear();
        NearFireCandidates.Clear();
        MidFireCandidates.Clear();
        FarFireCandidates.Clear();
        SmokeCandidates.Clear();
        SelectedCells.Clear();
        VisibilityReport.Clear();
        nearCandidateCount = 0;
        midCandidateCount = 0;
        farCandidateCount = 0;
        nearSelectedCount = 0;
        midSelectedCount = 0;
        farSelectedCount = 0;
        UnavailableParticlesLogged.Clear();
        FailedParticleLoads.Clear();
        lastRevision = -1;
        nextReconcileTime = 0f;
        initialized = false;
    }
}
