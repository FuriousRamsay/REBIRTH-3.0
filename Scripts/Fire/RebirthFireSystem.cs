using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable


internal static class RebirthFireHeatMapDestinationProbe
{
    private static bool resolved;
    private static System.Reflection.FieldInfo componentField;
    private static System.Reflection.PropertyInfo componentProperty;

    public static bool TryGetChunkData(AIDirector director, Vector3i position, bool createIfNeeded, out AIDirectorChunkData data)
    {
        data = null;
        if (director == null) return false;

        Resolve(director.GetType());
        try
        {
            object component = null;
            if (componentField != null) component = componentField.GetValue(director);
            else if (componentProperty != null) component = componentProperty.GetValue(director, null);
            AIDirectorChunkEventComponent typedComponent = component as AIDirectorChunkEventComponent;
            if (typedComponent == null) return false;

            data = typedComponent.GetChunkDataFromPosition(position, createIfNeeded);
            return data != null;
        }
        catch
        {
            data = null;
            return false;
        }
    }

    private static void Resolve(Type directorType)
    {
        if (resolved) return;
        resolved = true;
        try
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            componentField = directorType.GetField("chunkEventComponent", flags);
            if (componentField == null)
                componentProperty = directorType.GetProperty("ChunkEventComponent", flags);

        }
        catch
        {
            componentField = null;
            componentProperty = null;
        }
    }
}

internal static class RebirthFireTwitchGate
{
    private static bool resolved;
    private static System.Reflection.PropertyInfo bossHordeProperty;
    private static System.Reflection.FieldInfo bossHordeField;

    public static bool IsBossHordeActive()
    {
        if (!resolved)
        {
            resolved = true;
            try
            {
                Type type = Type.GetType("TwitchManager, Assembly-CSharp", false);
                if (type != null)
                {
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Static |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
                    bossHordeProperty = type.GetProperty("BossHordeActive", flags);
                    bossHordeField = type.GetField("BossHordeActive", flags);
                }
            }
            catch
            {
                bossHordeProperty = null;
                bossHordeField = null;
            }
        }

        try
        {
            if (bossHordeProperty != null && bossHordeProperty.PropertyType == typeof(bool))
                return (bool)bossHordeProperty.GetValue(null, null);
            if (bossHordeField != null && bossHordeField.FieldType == typeof(bool))
                return (bool)bossHordeField.GetValue(null);
        }
        catch
        {
        }

        return false;
    }
}

public enum RebirthFireIgnitionCause : byte
{
    Unknown,
    Molotov,
    Projectile,
    Explosion,
    EntityContact,
    BlockEvent,
    Scripted,
    Debug,
    Spread
}

public enum RebirthFireDeltaOperation : byte
{
    Add,
    Remove,
    Extinguish
}

public enum RebirthFireRequestOperation : byte
{
    Ignite,
    Extinguish,
    Remove,
    // RP41: snapshot resynchronization uses the existing authenticated request channel.
    Snapshot
}

public struct RebirthFireState
{
    public Vector3i Position;
    public ulong IgnitedWorldTime;
    public ulong NextProcessWorldTime;
    public int IgniterEntityId;
    public int SourceBlockType;
    public uint ScheduleToken;
    public byte Flags;
}

public struct RebirthFireDeltaEntry
{
    public Vector3i Position;
    public RebirthFireDeltaOperation Operation;
    public ulong ExpiryWorldTime;
}

public sealed class RebirthFireProfile
{
    public readonly int BlockType;
    public readonly bool Flammable;
    public readonly int DamagePerProcess;
    public readonly float ProcessIntervalSeconds;
    public readonly bool Spread;
    public readonly float SpreadChance;
    public readonly float ExtinguishChance;
    public readonly float SmokeSeconds;
    public readonly string DowngradeBlockName;
    public readonly string FireParticle;
    public readonly string SmokeParticle;
    public readonly string FireSound;
    public readonly string ContactBuff;
    public readonly float HeatMapStrengthPerProcess;

    public RebirthFireProfile(
        int blockType,
        bool flammable,
        int damagePerProcess,
        float processIntervalSeconds,
        bool spread,
        float spreadChance,
        float extinguishChance,
        float smokeSeconds,
        string downgradeBlockName,
        string fireParticle,
        string smokeParticle,
        string fireSound,
        string contactBuff,
        float heatMapStrengthPerProcess)
    {
        BlockType = blockType;
        Flammable = flammable;
        DamagePerProcess = damagePerProcess;
        ProcessIntervalSeconds = processIntervalSeconds;
        Spread = spread;
        SpreadChance = spreadChance;
        ExtinguishChance = extinguishChance;
        SmokeSeconds = smokeSeconds;
        DowngradeBlockName = downgradeBlockName;
        FireParticle = fireParticle;
        SmokeParticle = smokeParticle;
        FireSound = fireSound;
        ContactBuff = contactBuff;
        HeatMapStrengthPerProcess = Mathf.Max(0f, heatMapStrengthPerProcess);
    }
}

public static class RebirthFireDefaults
{
    // Rebalanced for REBIRTH 3.1: a typical 500-health wood block burns down in
    // approximately 120 / 60 / 30 seconds at Slower / Default / Faster.
    // Damage speed remains the existing 0.5x / 1x / 2x runtime multiplier.
    public const int DamagePerProcess = 84;
    public const float ProcessIntervalSeconds = 10f;
    public const float SmokeSeconds = 60f;
    public const float ExtinguishChance = 0f;
    public const float SpreadChance = 1f;
    public const bool Spread = true;

    // Final REBIRTH 2.6 used the dedicated Guppy fire bundle. The smoke spawn call
    // was disabled; SmokeSeconds remains the extinguish/re-ignition cooldown duration.
    public const string FireParticle =
        "#@modfolder(zzz_REBIRTH__3_0):Resources/gupFireParticles.unity3d?gupBeavis05-Heavy";
    public const string SmokeParticle = "";
    public const string FireSound = "Ambient_Loops/a_fire_med_lp";
    public const string ContactBuff = "buffBurningMolotov";

    // Fire heat contribution is intentionally half of the previous v65 rate.
    // At the 10-second process interval, each accepted burning-block event contributes
    // approximately 0.0333335 activity instead of 0.066667.
    public const float HeatMapStrengthPerProcess = 0.0333335f;
    public const float HeatMapDurationSeconds = 720f;

    // Safety limits for tracked state. The v37/v38 port allowed 8,192 positions,
    // which let a single runaway POI persist more than a thousand invisible fires.
    // 512 retains enough state for a large multi-chunk structure while bounding save,
    // scheduler, and network costs. Per-chunk state is capped separately.
    public const int MaxActiveFiresGlobal = 512;
    public const int MaxActiveFiresPerChunk = 96;

    // State validation remains frame-budgeted, but only the legacy 64 visible fire
    // sources may actually damage/spread during one 10-second simulation window.
    // This restores the effective 2.6 behavior while allowing up to 150 client visuals.
    public const int MaxProcessesPerUpdate = 24;
    public const int MaxSimulatedFiresPerWindow = 64;
    // Reserve a small part of the unchanged 64-source budget for newly ignited,
    // never-simulated fronts. The remaining 56 slots rotate oldest-waiting first,
    // so established fires cannot be starved by continuous new ignitions.
    public const int NewFrontSimulationReserve = 8;
    public const int MaxSpreadIgnitionsPerUpdate = 48;

    // Preserve the final 2.6 proportional spread throttle independently from the
    // client visual cap. Once active fires exceed 64, effective spread chance is
    // multiplied by 64 / activeFireCount (128 fires = half speed, 256 = quarter).
    public const bool ThrottleSpreadToLegacyParticleBudget = true;
    public const int LegacySpreadThrottleReferenceFires = 64;

    public const int MaxQueuedIgnitionsPerUpdate = 32;
    public const int HeapCompactionSlack = 1024;
    public const float PersistenceAutosaveSeconds = 300f;
    public const int MaxBlockChangesPerRpc = 50;
    public const int MaxNetworkDeltaEntries = 256;
    public const int MaxRequestPositions = 2048;
    // Read older runaway v38 saves safely, then trim during Restore.
    public const int MaxSnapshotFires = 8192;
    public const int MaxSnapshotCooldowns = 32768;
    public const int MaxSnapshotFiresPerPacket = 512;
    public const int MaxSnapshotCooldownsPerPacket = 512;
    public const int MaxSnapshotChunks = 64;
    public const int MaxPendingSnapshotDeltas = 4096;
}

public static class RebirthFireRuntimePolicy
{
    private static bool enabled = false;
    private static bool affectsHeatmap = false;
    private static RebirthFireBlockDamageSpeed blockDamageSpeed = RebirthFireBlockDamageSpeed.Default;

    public static bool Enabled
    {
        get { return enabled; }
    }

    public static bool AffectsHeatmap
    {
        get { return affectsHeatmap; }
    }

    public static RebirthFireBlockDamageSpeed BlockDamageSpeed
    {
        get { return blockDamageSpeed; }
    }

    public static float BlockDamageMultiplier
    {
        get
        {
            if (blockDamageSpeed == RebirthFireBlockDamageSpeed.Slower) return 0.5f;
            if (blockDamageSpeed == RebirthFireBlockDamageSpeed.Faster) return 2f;
            return 1f;
        }
    }

    public static void SetAffectsHeatmap(bool value)
    {
        affectsHeatmap = value;
    }

    public static void SetBlockDamageSpeed(RebirthFireBlockDamageSpeed value)
    {
        if ((int)value < (int)RebirthFireBlockDamageSpeed.Slower ||
            (int)value > (int)RebirthFireBlockDamageSpeed.Faster)
            value = RebirthFireBlockDamageSpeed.Default;
        blockDamageSpeed = value;
    }

    public static int ScaleBlockDamage(int baseDamage)
    {
        if (baseDamage <= 0)
            return 0;

        return Math.Max(1, Mathf.RoundToInt(baseDamage * BlockDamageMultiplier));
    }

    public static void SetEnabled(bool value)
    {
        if (enabled == value)
            return;

        enabled = value;
        if (!value)
        {
            RebirthFireService.Instance.ClearAll(true);
            RebirthFireVisualManager.ClearAll();
        }
    }
}

public static class RebirthFireProfileRegistry
{
    private static readonly Dictionary<int, RebirthFireProfile> Profiles =
        new Dictionary<int, RebirthFireProfile>();
    // RP40: eligibility that varies by BlockValue instance (most importantly multiblock
    // child state) must never poison the type-level material/profile cache. Keep one
    // immutable non-flammable view per type for ineligible instances instead.
    private static readonly Dictionary<int, RebirthFireProfile> IneligibleProfiles =
        new Dictionary<int, RebirthFireProfile>();

    private static readonly FastTags<TagGroup.Global> TagFlammable =
        FastTags<TagGroup.Global>.Parse("flammable");
    private static readonly FastTags<TagGroup.Global> TagInflammable =
        FastTags<TagGroup.Global>.Parse("inflammable");

    private static readonly HashSet<string> FlammableMaterialIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mplants", "Mcorn", "Mhay"
        };

    private static readonly HashSet<string> FlammableDamageCategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wood", "cloth", "corn", "grass", "plastic", "leaves", "cactus",
            "mushroom", "hay", "paper", "trash", "backpack", "organic"
        };

    private static readonly HashSet<string> FlammableSurfaceCategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "wood", "cloth", "corn", "grass", "plastic", "leaves", "cactus",
            "mushroom", "hay", "paper", "trash", "backpack", "organic"
        };

    public static int CachedCount
    {
        get { return Profiles.Count; }
    }

    public static void Clear()
    {
        Profiles.Clear();
        IneligibleProfiles.Clear();
    }

    public static RebirthFireProfile Resolve(BlockValue value)
    {
        int type = value.type;
        RebirthFireProfile cached;
        if (Profiles.TryGetValue(type, out cached))
            return cached.Flammable && !IsInstanceEligible(value, type >= 0 && type < Block.list.Length ? Block.list[type] : null)
                ? ResolveIneligible(cached)
                : cached;

        Block block = type >= 0 && type < Block.list.Length ? Block.list[type] : null;
        if (block == null)
        {
            cached = CreateNonFlammable(type);
            Profiles[type] = cached;
            return cached;
        }

        bool flammable = IsFlammableTypeDefinition(block);
        MaterialBlock material = block.blockMaterial;

        int damage = GetInt(block.Properties, "FireDamage", RebirthFireDefaults.DamagePerProcess);
        if (material != null)
            damage = GetInt(material.Properties, "FireDamage", damage);

        float interval = GetFloat(block.Properties, "FireProcessInterval", RebirthFireDefaults.ProcessIntervalSeconds);
        if (material != null)
            interval = GetFloat(material.Properties, "FireProcessInterval", interval);

        bool spread = GetBool(block.Properties, "FireSpread", RebirthFireDefaults.Spread);
        if (material != null)
            spread = GetBool(material.Properties, "FireSpread", spread);

        float spreadChance = GetFloat(block.Properties, "FireSpreadChance", RebirthFireDefaults.SpreadChance);
        if (material != null)
            spreadChance = GetFloat(material.Properties, "FireSpreadChance", spreadChance);

        float extinguishChance = GetFloat(block.Properties, "ChanceToExtinguish", RebirthFireDefaults.ExtinguishChance);
        if (material != null)
            extinguishChance = GetFloat(material.Properties, "ChanceToExtinguish", extinguishChance);

        float smokeSeconds = GetFloat(block.Properties, "SmokeTime", RebirthFireDefaults.SmokeSeconds);
        if (material != null)
            smokeSeconds = GetFloat(material.Properties, "SmokeTime", smokeSeconds);

        string downgrade = GetString(block.Properties, "FireDowngradeBlock", string.Empty);
        string fireParticle = GetString(block.Properties, "FireParticle", RebirthFireDefaults.FireParticle);
        string smokeParticle = GetString(block.Properties, "SmokeParticle", RebirthFireDefaults.SmokeParticle);
        string fireSound = GetString(block.Properties, "FireSound", RebirthFireDefaults.FireSound);
        string contactBuff = GetString(block.Properties, "BuffOnFire", RebirthFireDefaults.ContactBuff);
        float heatMapStrengthPerProcess = GetFloat(
            block.Properties,
            "FireHeatMapStrength",
            RebirthFireDefaults.HeatMapStrengthPerProcess);

        if (material != null)
        {
            fireParticle = GetString(material.Properties, "FireParticle", fireParticle);
            smokeParticle = GetString(material.Properties, "SmokeParticle", smokeParticle);
            fireSound = GetString(material.Properties, "FireSound", fireSound);
            contactBuff = GetString(material.Properties, "BuffOnFire", contactBuff);
            heatMapStrengthPerProcess = GetFloat(
                material.Properties,
                "FireHeatMapStrength",
                heatMapStrengthPerProcess);
        }

        cached = new RebirthFireProfile(
            type,
            flammable,
            Math.Max(1, damage),
            Mathf.Max(0.25f, interval),
            spread,
            Mathf.Clamp01(spreadChance),
            Mathf.Clamp01(extinguishChance),
            Mathf.Max(0f, smokeSeconds),
            downgrade,
            fireParticle,
            smokeParticle,
            fireSound,
            contactBuff,
            heatMapStrengthPerProcess);

        Profiles[type] = cached;
        return cached.Flammable && !IsInstanceEligible(value, block)
            ? ResolveIneligible(cached)
            : cached;
    }

    private static RebirthFireProfile ResolveIneligible(RebirthFireProfile source)
    {
        RebirthFireProfile cached;
        if (IneligibleProfiles.TryGetValue(source.BlockType, out cached))
            return cached;

        cached = new RebirthFireProfile(
            source.BlockType,
            false,
            source.DamagePerProcess,
            source.ProcessIntervalSeconds,
            false,
            0f,
            source.ExtinguishChance,
            source.SmokeSeconds,
            source.DowngradeBlockName,
            source.FireParticle,
            source.SmokeParticle,
            source.FireSound,
            source.ContactBuff,
            source.HeatMapStrengthPerProcess);
        IneligibleProfiles[source.BlockType] = cached;
        return cached;
    }

    public static bool IsFlammableByDefinition(BlockValue value)
    {
        Block block = value.type >= 0 && value.type < Block.list.Length ? Block.list[value.type] : null;
        return block != null && IsFlammableByDefinition(value, block);
    }

    private static bool IsFlammableByDefinition(BlockValue value, Block block)
    {
        return IsInstanceEligible(value, block) && IsFlammableTypeDefinition(block);
    }

    private static bool IsInstanceEligible(BlockValue value, Block block)
    {
        if (block == null)
            return false;
        if (value.ischild || value.isair || value.isWater)
            return false;
        return block.blockMaterial == null || !block.blockMaterial.IsLiquid;
    }

    private static bool IsFlammableTypeDefinition(Block block)
    {
        // Keep only properties that are invariant for a block type in the cache.
        if (block == null || block.HasAnyFastTags(TagInflammable))
            return false;
        if (block.HasAnyFastTags(TagFlammable))
            return true;

        MaterialBlock material = block.blockMaterial;
        if (material == null || material.IsLiquid)
            return false;
        if (!string.IsNullOrEmpty(material.id) && FlammableMaterialIds.Contains(material.id))
            return true;
        if (!string.IsNullOrEmpty(material.DamageCategory) && FlammableDamageCategories.Contains(material.DamageCategory))
            return true;
        return !string.IsNullOrEmpty(material.SurfaceCategory) &&
               FlammableSurfaceCategories.Contains(material.SurfaceCategory);
    }

    private static RebirthFireProfile CreateNonFlammable(int type)
    {
        return new RebirthFireProfile(
            type,
            false,
            RebirthFireDefaults.DamagePerProcess,
            RebirthFireDefaults.ProcessIntervalSeconds,
            false,
            0f,
            0f,
            RebirthFireDefaults.SmokeSeconds,
            string.Empty,
            RebirthFireDefaults.FireParticle,
            RebirthFireDefaults.SmokeParticle,
            RebirthFireDefaults.FireSound,
            RebirthFireDefaults.ContactBuff,
            0f);
    }

    private static string GetString(DynamicProperties properties, string key, string fallback)
    {
        if (properties == null)
            return fallback;
        string value;
        return properties.Values.TryGetValue(key, out value) ? value : fallback;
    }

    private static int GetInt(DynamicProperties properties, string key, int fallback)
    {
        string text = GetString(properties, key, null);
        int value;
        return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            ? value
            : fallback;
    }

    private static float GetFloat(DynamicProperties properties, string key, float fallback)
    {
        string text = GetString(properties, key, null);
        float value;
        return text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            ? value
            : fallback;
    }

    private static bool GetBool(DynamicProperties properties, string key, bool fallback)
    {
        string text = GetString(properties, key, null);
        bool value;
        return text != null && bool.TryParse(text, out value) ? value : fallback;
    }
}

internal struct RebirthFireDueEntry
{
    public Vector3i Position;
    public ulong DueWorldTime;
    public uint Token;
}

internal sealed class RebirthFireDueHeap
{
    private readonly List<RebirthFireDueEntry> values = new List<RebirthFireDueEntry>(128);

    public int Count
    {
        get { return values.Count; }
    }

    public void Clear()
    {
        values.Clear();
    }

    public void Push(RebirthFireDueEntry entry)
    {
        int index = values.Count;
        values.Add(entry);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (values[parent].DueWorldTime <= entry.DueWorldTime)
                break;
            values[index] = values[parent];
            index = parent;
        }
        values[index] = entry;
    }

    public bool TryPeek(out RebirthFireDueEntry entry)
    {
        if (values.Count == 0)
        {
            entry = default(RebirthFireDueEntry);
            return false;
        }
        entry = values[0];
        return true;
    }

    public bool TryPop(out RebirthFireDueEntry entry)
    {
        int count = values.Count;
        if (count == 0)
        {
            entry = default(RebirthFireDueEntry);
            return false;
        }

        entry = values[0];
        RebirthFireDueEntry last = values[count - 1];
        values.RemoveAt(count - 1);
        count--;
        if (count == 0)
            return true;

        int index = 0;
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= count)
                break;
            int right = left + 1;
            int smallest = right < count && values[right].DueWorldTime < values[left].DueWorldTime
                ? right
                : left;
            if (values[smallest].DueWorldTime >= last.DueWorldTime)
                break;
            values[index] = values[smallest];
            index = smallest;
        }
        values[index] = last;
        return true;
    }
}

internal struct RebirthFireIgnitionEntry
{
    public Vector3i Position;
    public int IgniterEntityId;
    public RebirthFireIgnitionCause Cause;
    public ulong DueWorldTime;
    public ulong Sequence;
}

internal sealed class RebirthFireIgnitionHeap
{
    private readonly List<RebirthFireIgnitionEntry> values =
        new List<RebirthFireIgnitionEntry>(128);

    public int Count { get { return values.Count; } }

    public void Clear()
    {
        values.Clear();
    }

    public void Push(RebirthFireIgnitionEntry entry)
    {
        int index = values.Count;
        values.Add(entry);
        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (Compare(values[parent], entry) <= 0)
                break;
            values[index] = values[parent];
            index = parent;
        }
        values[index] = entry;
    }

    public bool TryPeek(out RebirthFireIgnitionEntry entry)
    {
        if (values.Count == 0)
        {
            entry = default(RebirthFireIgnitionEntry);
            return false;
        }
        entry = values[0];
        return true;
    }

    public bool TryPop(out RebirthFireIgnitionEntry entry)
    {
        int count = values.Count;
        if (count == 0)
        {
            entry = default(RebirthFireIgnitionEntry);
            return false;
        }

        entry = values[0];
        RebirthFireIgnitionEntry last = values[count - 1];
        values.RemoveAt(count - 1);
        count--;
        if (count == 0)
            return true;

        int index = 0;
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= count)
                break;
            int right = left + 1;
            int smallest = right < count && Compare(values[right], values[left]) < 0
                ? right
                : left;
            if (Compare(values[smallest], last) >= 0)
                break;
            values[index] = values[smallest];
            index = smallest;
        }
        values[index] = last;
        return true;
    }

    private static int Compare(RebirthFireIgnitionEntry left, RebirthFireIgnitionEntry right)
    {
        int due = left.DueWorldTime.CompareTo(right.DueWorldTime);
        return due != 0 ? due : left.Sequence.CompareTo(right.Sequence);
    }
}

public sealed class RebirthFireService
{
    private static readonly Vector3i[] Directions =
    {
        Vector3i.left,
        Vector3i.right,
        Vector3i.forward,
        Vector3i.back,
        Vector3i.up,
        Vector3i.down
    };

    private static readonly RebirthFireService instance = new RebirthFireService();

    public static RebirthFireService Instance
    {
        get { return instance; }
    }

    private readonly Dictionary<Vector3i, RebirthFireState> fires =
        new Dictionary<Vector3i, RebirthFireState>();
    private readonly Dictionary<Vector3i, ulong> cooldowns =
        new Dictionary<Vector3i, ulong>();
    private readonly Dictionary<long, HashSet<Vector3i>> firePositionsByChunk =
        new Dictionary<long, HashSet<Vector3i>>();
    private readonly Dictionary<int, Dictionary<Vector3i, float>> visibilityByPlayer =
        new Dictionary<int, Dictionary<Vector3i, float>>();
    private readonly HashSet<Vector3i> simulationSelection = new HashSet<Vector3i>();
    private readonly Dictionary<Vector3i, ulong> lastSimulatedWorldTime =
        new Dictionary<Vector3i, ulong>();
    private readonly List<RebirthFireState> simulationCandidates =
        new List<RebirthFireState>(RebirthFireDefaults.MaxActiveFiresGlobal);
    private readonly RebirthFireDueHeap due = new RebirthFireDueHeap();
    private readonly RebirthFireDueHeap cooldownDue = new RebirthFireDueHeap();
    private readonly List<RebirthFireDeltaEntry> pendingDeltas =
        new List<RebirthFireDeltaEntry>(64);
    private readonly List<BlockChangeInfo> blockChanges =
        new List<BlockChangeInfo>(RebirthFireDefaults.MaxBlockChangesPerRpc);
    private readonly List<Vector3i> requestPositions = new List<Vector3i>(128);

    private int revision;
    private int processedLastUpdate;
    private int simulatedLastUpdate;
    private int simulatedThisWindow;
    private int skippedInvisibleLastUpdate;
    private int deferredSimulationCapLastUpdate;
    private ulong simulationWindowEndWorldTime;
    private int simulationEligibleLastWindow;
    private int simulationSelectedLastWindow;
    private int simulationNeverRunSelectedLastWindow;
    private int simulationNewFrontSelectedLastWindow;
    private int heatMapEventsLastUpdate;
    private long heatMapEventsTotal;
    private long heatMapAttemptsTotal;
    private long heatMapSkippedOptionTotal;
    private long heatMapSkippedDirectorTotal;
    private long heatMapSkippedStrengthTotal;
    private long heatMapSkippedVanillaGateTotal;
    private long heatMapDestinationUnavailableTotal;
    private long heatMapDestinationNotReadyTotal;
    private long heatMapDestinationAcceptedTotal;
    private double heatMapDestinationActivityDeltaTotal;
    private long heatMapDestinationEventDeltaTotal;
    private bool lastHeatMapDestinationAvailable;
    private bool lastHeatMapDestinationReady;
    private float lastHeatMapActivityBefore;
    private float lastHeatMapActivityAfter;
    private int lastHeatMapEventCountBefore;
    private int lastHeatMapEventCountAfter;
    private Vector3i lastHeatMapPosition;
    private float lastHeatMapStrength;
    private long skippedInvisibleTotal;
    private long deferredSimulationCapTotal;
    private int trimmedOnRestore;
    private int spreadIgnitionsLastUpdate;
    private float spreadThrottleFractionLastUpdate = 1f;
    private int spreadCandidatesLastUpdate;
    private int spreadThrottleRejectedLastUpdate;
    private long spreadCandidatesTotal;
    private long spreadThrottleRejectedTotal;
    private long spreadIgnitionsTotal;
    private int maxChunkFireCount;
    private long rejectedGlobalCap;
    private long rejectedChunkCap;
    private long rejectedTrader;
    private long rejectedCooldown;
    private long rejectedWater;
    private long rejectedNonFlammable;
    private long destroyedByFire;
    private long extinguishedByWater;
    private long extinguishedByChance;
    private long delayedForUnloadedChunk;

    public int ActiveCount { get { return fires.Count; } }
    public int CooldownCount { get { return cooldowns.Count; } }
    public int DueCount { get { return due.Count; } }
    public int Revision { get { return revision; } }
    public int ProcessedLastUpdate { get { return processedLastUpdate; } }
    public int SimulatedLastUpdate { get { return simulatedLastUpdate; } }
    public int SimulatedThisWindow { get { return simulatedThisWindow; } }
    public int SkippedInvisibleLastUpdate { get { return skippedInvisibleLastUpdate; } }
    public int DeferredSimulationCapLastUpdate { get { return deferredSimulationCapLastUpdate; } }
    public long SkippedInvisibleTotal { get { return skippedInvisibleTotal; } }
    public long DeferredSimulationCapTotal { get { return deferredSimulationCapTotal; } }
    public int TrimmedOnRestore { get { return trimmedOnRestore; } }
    public ulong SimulationWindowEndWorldTime { get { return simulationWindowEndWorldTime; } }
    public int SimulationEligibleLastWindow { get { return simulationEligibleLastWindow; } }
    public int SimulationSelectedLastWindow { get { return simulationSelectedLastWindow; } }
    public int SimulationNeverRunSelectedLastWindow { get { return simulationNeverRunSelectedLastWindow; } }
    public int SimulationNewFrontSelectedLastWindow { get { return simulationNewFrontSelectedLastWindow; } }
    public int HeatMapEventsLastUpdate { get { return heatMapEventsLastUpdate; } }
    public long HeatMapEventsTotal { get { return heatMapEventsTotal; } }
    public long HeatMapAttemptsTotal { get { return heatMapAttemptsTotal; } }
    public long HeatMapSkippedOptionTotal { get { return heatMapSkippedOptionTotal; } }
    public long HeatMapSkippedDirectorTotal { get { return heatMapSkippedDirectorTotal; } }
    public long HeatMapSkippedStrengthTotal { get { return heatMapSkippedStrengthTotal; } }
    public long HeatMapSkippedVanillaGateTotal { get { return heatMapSkippedVanillaGateTotal; } }
    public long HeatMapDestinationUnavailableTotal { get { return heatMapDestinationUnavailableTotal; } }
    public long HeatMapDestinationNotReadyTotal { get { return heatMapDestinationNotReadyTotal; } }
    public long HeatMapDestinationAcceptedTotal { get { return heatMapDestinationAcceptedTotal; } }
    public double HeatMapDestinationActivityDeltaTotal { get { return heatMapDestinationActivityDeltaTotal; } }
    public long HeatMapDestinationEventDeltaTotal { get { return heatMapDestinationEventDeltaTotal; } }
    public bool LastHeatMapDestinationAvailable { get { return lastHeatMapDestinationAvailable; } }
    public bool LastHeatMapDestinationReady { get { return lastHeatMapDestinationReady; } }
    public float LastHeatMapActivityBefore { get { return lastHeatMapActivityBefore; } }
    public float LastHeatMapActivityAfter { get { return lastHeatMapActivityAfter; } }
    public int LastHeatMapEventCountBefore { get { return lastHeatMapEventCountBefore; } }
    public int LastHeatMapEventCountAfter { get { return lastHeatMapEventCountAfter; } }
    public Vector3i LastHeatMapPosition { get { return lastHeatMapPosition; } }
    public float LastHeatMapStrength { get { return lastHeatMapStrength; } }
    public int SpreadIgnitionsLastUpdate { get { return spreadIgnitionsLastUpdate; } }
    public float SpreadThrottleFractionLastUpdate { get { return spreadThrottleFractionLastUpdate; } }
    public int SpreadCandidatesLastUpdate { get { return spreadCandidatesLastUpdate; } }
    public int SpreadThrottleRejectedLastUpdate { get { return spreadThrottleRejectedLastUpdate; } }
    public long SpreadCandidatesTotal { get { return spreadCandidatesTotal; } }
    public long SpreadThrottleRejectedTotal { get { return spreadThrottleRejectedTotal; } }
    public long SpreadIgnitionsTotal { get { return spreadIgnitionsTotal; } }
    public int MaxChunkFireCount { get { return maxChunkFireCount; } }
    public long RejectedGlobalCap { get { return rejectedGlobalCap; } }
    public long RejectedChunkCap { get { return rejectedChunkCap; } }
    public long RejectedTrader { get { return rejectedTrader; } }
    public long RejectedCooldown { get { return rejectedCooldown; } }
    public long RejectedWater { get { return rejectedWater; } }
    public long RejectedNonFlammable { get { return rejectedNonFlammable; } }
    public long DestroyedByFire { get { return destroyedByFire; } }
    public long ExtinguishedByWater { get { return extinguishedByWater; } }
    public long ExtinguishedByChance { get { return extinguishedByChance; } }
    public long DelayedForUnloadedChunk { get { return delayedForUnloadedChunk; } }

    public bool IsBurning(Vector3i position)
    {
        return RebirthFireRuntimePolicy.Enabled && fires.ContainsKey(position);
    }

    public int CountNearby(Vector3i center, int range)
    {
        int count = 0;
        int radius = Math.Max(0, range);
        int minChunkX = World.toChunkXZ(center.x - radius);
        int maxChunkX = World.toChunkXZ(center.x + radius);
        int minChunkZ = World.toChunkXZ(center.z - radius);
        int maxChunkZ = World.toChunkXZ(center.z + radius);
        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                HashSet<Vector3i> positions;
                if (!firePositionsByChunk.TryGetValue(GetChunkKey(chunkX, chunkZ), out positions))
                    continue;
                foreach (Vector3i p in positions)
                {
                    if (Math.Abs(p.x - center.x) <= radius &&
                        Math.Abs(p.y - center.y) <= radius &&
                        Math.Abs(p.z - center.z) <= radius)
                        count++;
                }
            }
        }
        return count;
    }

    public int CountVisibleNearby(int playerEntityId, Vector3i center, int range)
    {
        Dictionary<Vector3i, float> visible;
        if (!visibilityByPlayer.TryGetValue(playerEntityId, out visible))
            return 0;

        float now = Time.realtimeSinceStartup;
        int radius = Math.Max(0, range);
        int count = 0;
        foreach (KeyValuePair<Vector3i, float> report in visible)
        {
            Vector3i position = report.Key;
            if (now - report.Value <= 3f &&
                Math.Abs(position.x - center.x) <= radius &&
                Math.Abs(position.z - center.z) <= radius &&
                Math.Abs(position.y - center.y) <= 2)
                count++;
        }
        return count;
    }

    public void CollectBurningWithin(Vector3i center, float radius, List<Vector3i> output)
    {
        output.Clear();
        float clampedRadius = Mathf.Max(0f, radius);
        float radiusSquared = clampedRadius * clampedRadius;
        int blockRadius = Mathf.CeilToInt(clampedRadius);
        int minChunkX = World.toChunkXZ(center.x - blockRadius);
        int maxChunkX = World.toChunkXZ(center.x + blockRadius);
        int minChunkZ = World.toChunkXZ(center.z - blockRadius);
        int maxChunkZ = World.toChunkXZ(center.z + blockRadius);
        Vector3 centerPoint = center.ToVector3Center();
        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                HashSet<Vector3i> positions;
                if (!firePositionsByChunk.TryGetValue(GetChunkKey(chunkX, chunkZ), out positions))
                    continue;
                foreach (Vector3i position in positions)
                {
                    if ((position.ToVector3Center() - centerPoint).sqrMagnitude <= radiusSquared)
                        output.Add(position);
                }
            }
        }
    }

    public bool TryIgnite(World world, Vector3i position, int igniterEntityId, RebirthFireIgnitionCause cause)
    {
        return TryIgniteInternal(world, position, igniterEntityId, cause, true);
    }

    private bool TryIgniteInternal(
        World world,
        Vector3i position,
        int igniterEntityId,
        RebirthFireIgnitionCause cause,
        bool queueDelta)
    {
        #if DEBUG
        RebirthFireDiagnostics.ServiceIgniteAttempts++;
        #endif
        if (!RebirthFireRuntimePolicy.Enabled)
        {
            #if DEBUG
            RebirthFireDiagnostics.ServiceRejectedDisabled++;
            #endif
            LogDebugIgnition(cause, position, "rejected-disabled");
            return false;
        }
        if (world == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.ServiceRejectedNoWorld++;
            #endif
            LogDebugIgnition(cause, position, "rejected-no-world");
            return false;
        }
        if (!IsServer())
        {
            #if DEBUG
            RebirthFireDiagnostics.ServiceRejectedNotServer++;
            #endif
            LogDebugIgnition(cause, position, "rejected-not-server");
            return false;
        }
        if (fires.ContainsKey(position))
        {
            #if DEBUG
            RebirthFireDiagnostics.ServiceAlreadyBurning++;
            #endif
            LogDebugIgnition(cause, position, "already-burning");
            return true;
        }
        if (fires.Count >= RebirthFireDefaults.MaxActiveFiresGlobal)
        {
            rejectedGlobalCap++;
            LogDebugIgnition(cause, position, "rejected-global-cap");
            return false;
        }
        if (world.IsWithinTraderArea(position))
        {
            rejectedTrader++;
            LogDebugIgnition(cause, position, "rejected-trader");
            return false;
        }

        ulong now = world.worldTime;
        ulong cooldownExpiry;
        if (cooldowns.TryGetValue(position, out cooldownExpiry))
        {
            if (cooldownExpiry > now)
            {
                rejectedCooldown++;
                LogDebugIgnition(cause, position, "rejected-cooldown");
                return false;
            }
            cooldowns.Remove(position);
        }

        if (HasAdjacentWater(world, position))
        {
            rejectedWater++;
            LogDebugIgnition(cause, position, "rejected-water");
            return false;
        }

        BlockValue live = world.GetBlock(position);
        RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(live);
        if (!profile.Flammable)
        {
            rejectedNonFlammable++;
            LogDebugIgnition(cause, position, "rejected-nonflammable block=" + (live.Block != null ? live.Block.GetBlockName() : "<null>"));
            return false;
        }

        long chunkKey = GetChunkKey(position);
        HashSet<Vector3i> chunkPositions;
        if (!firePositionsByChunk.TryGetValue(chunkKey, out chunkPositions))
        {
            chunkPositions = new HashSet<Vector3i>();
            firePositionsByChunk.Add(chunkKey, chunkPositions);
        }
        if (chunkPositions.Count >= RebirthFireDefaults.MaxActiveFiresPerChunk)
        {
            rejectedChunkCap++;
            LogDebugIgnition(cause, position, "rejected-chunk-cap");
            return false;
        }

        RebirthFireState state = new RebirthFireState
        {
            Position = position,
            IgnitedWorldTime = now,
            NextProcessWorldTime = AddSeconds(now, profile.ProcessIntervalSeconds),
            IgniterEntityId = igniterEntityId,
            SourceBlockType = live.type,
            ScheduleToken = 1,
            Flags = (byte)cause
        };

        fires.Add(position, state);
        lastSimulatedWorldTime.Remove(position);
        chunkPositions.Add(position);
        if (chunkPositions.Count > maxChunkFireCount)
            maxChunkFireCount = chunkPositions.Count;

        due.Push(new RebirthFireDueEntry
        {
            Position = position,
            DueWorldTime = state.NextProcessWorldTime,
            Token = state.ScheduleToken
        });

        revision++;
        #if DEBUG
        RebirthFireDiagnostics.ServiceIgniteAccepted++;
        #endif
        RebirthFireSleeperActivation.QueueContainingDormantVolumes(world, position, RebirthFireSleeperQueueReason.IgnitionIntersection);
        LogDebugIgnition(cause, position, "accepted");
        if (queueDelta)
            QueueDelta(position, RebirthFireDeltaOperation.Add, 0);
        return true;
    }

    public int TryIgniteMany(
        World world,
        IList<Vector3i> positions,
        int igniterEntityId,
        RebirthFireIgnitionCause cause)
    {
        if (positions == null)
            return 0;
        int accepted = 0;
        int count = Math.Min(positions.Count, RebirthFireDefaults.MaxRequestPositions);
        for (int i = 0; i < count; i++)
        {
            if (TryIgnite(world, positions[i], igniterEntityId, cause))
                accepted++;
        }
        return accepted;
    }

    public void ScheduleIgnition(
        World world,
        IList<Vector3i> positions,
        int igniterEntityId,
        RebirthFireIgnitionCause cause,
        float delaySeconds)
    {
        if (world == null || positions == null)
            return;
        // All external ignition bursts enter a bounded due-time heap, including
        // zero-delay explosions and min-events. This prevents one large explosion or
        // cascade request from processing thousands of positions in a single frame.
        ulong dueWorldTime = delaySeconds <= 0f
            ? world.worldTime
            : AddSeconds(world.worldTime, delaySeconds);
        RebirthFireDelayedIgnitionQueue.Schedule(
            positions,
            igniterEntityId,
            cause,
            dueWorldTime);
    }

    public bool Extinguish(World world, Vector3i position, float smokeSeconds)
    {
        if (world == null || !IsServer())
            return false;

        bool removed = RemoveInternal(position, false);
        if (!removed && !cooldowns.ContainsKey(position))
            return false;
        ulong expiry = AddSeconds(world.worldTime, Mathf.Max(0f, smokeSeconds));
        cooldowns[position] = expiry;
        cooldownDue.Push(new RebirthFireDueEntry
        {
            Position = position,
            DueWorldTime = expiry,
            Token = 0
        });

        revision++;
        QueueDelta(position, RebirthFireDeltaOperation.Extinguish, expiry);
        return removed;
    }

    public int ExtinguishMany(World world, IList<Vector3i> positions, float smokeSeconds)
    {
        if (positions == null)
            return 0;
        int removed = 0;
        int count = Math.Min(positions.Count, RebirthFireDefaults.MaxRequestPositions);
        for (int i = 0; i < count; i++)
        {
            if (Extinguish(world, positions[i], smokeSeconds))
                removed++;
        }
        return removed;
    }

    public bool Remove(Vector3i position)
    {
        if (!IsServer())
            return false;
        bool removed = RemoveInternal(position, false);
        if (removed)
        {
            revision++;
            QueueDelta(position, RebirthFireDeltaOperation.Remove, 0);
        }
        return removed;
    }

    public int RemoveMany(IList<Vector3i> positions)
    {
        if (positions == null)
            return 0;
        int removed = 0;
        int count = Math.Min(positions.Count, RebirthFireDefaults.MaxRequestPositions);
        for (int i = 0; i < count; i++)
        {
            if (Remove(positions[i]))
                removed++;
        }
        return removed;
    }

    private bool RemoveInternal(Vector3i position, bool removeCooldown)
    {
        RebirthFireState state;
        bool removed = fires.TryGetValue(position, out state) && fires.Remove(position);
        if (removed)
        {
            long key = GetChunkKey(position);
            HashSet<Vector3i> chunkPositions;
            if (firePositionsByChunk.TryGetValue(key, out chunkPositions))
            {
                chunkPositions.Remove(position);
                if (chunkPositions.Count == 0)
                    firePositionsByChunk.Remove(key);
            }
            ClearVisibilityForPosition(position);
            simulationSelection.Remove(position);
            lastSimulatedWorldTime.Remove(position);
        }
        if (removeCooldown)
            cooldowns.Remove(position);
        return removed;
    }

    public void ClearForPoiReset(Vector3i position)
    {
        if (!IsServer())
            return;
        bool hadCooldown = cooldowns.ContainsKey(position);
        bool changed = RemoveInternal(position, true);
        if (changed || hadCooldown)
        {
            revision++;
            QueueDelta(position, RebirthFireDeltaOperation.Remove, 0);
        }
    }

    public void ClearAll(bool replicate)
    {
        fires.Clear();
        cooldowns.Clear();
        firePositionsByChunk.Clear();
        visibilityByPlayer.Clear();
        simulationSelection.Clear();
        lastSimulatedWorldTime.Clear();
        simulationCandidates.Clear();
        due.Clear();
        cooldownDue.Clear();
        pendingDeltas.Clear();
        RebirthFireDelayedIgnitionQueue.Clear();
        RebirthFireSleeperActivation.Clear();
        maxChunkFireCount = 0;
        simulatedThisWindow = 0;
        simulationWindowEndWorldTime = 0;
        simulationEligibleLastWindow = 0;
        simulationSelectedLastWindow = 0;
        simulationNeverRunSelectedLastWindow = 0;
        heatMapEventsLastUpdate = 0;
        trimmedOnRestore = 0;
        revision++;

        if (replicate && IsServer())
            RebirthFireNetwork.BroadcastSnapshot();
    }

    public void Update(World world)
    {
        processedLastUpdate = 0;
        simulatedLastUpdate = 0;
        skippedInvisibleLastUpdate = 0;
        deferredSimulationCapLastUpdate = 0;
        spreadIgnitionsLastUpdate = 0;
        spreadCandidatesLastUpdate = 0;
        spreadThrottleRejectedLastUpdate = 0;
        heatMapEventsLastUpdate = 0;
        spreadThrottleFractionLastUpdate = ComputeSpreadThrottleFraction();

        if (world == null || !IsServer())
            return;

        RebirthFireSleeperActivation.Update(world);
        PruneCooldowns(world.worldTime);
        PruneVisibilityReports();
        EnsureSimulationWindow(world);
        RebirthFireDelayedIgnitionQueue.Process(world);
        MaybeCompactDueHeaps();

        if (!RebirthFireRuntimePolicy.Enabled || fires.Count == 0)
        {
            FlushDeltas();
            return;
        }

        blockChanges.Clear();
        RebirthFireDueEntry entry;
        int dueAttempts = 0;
        // RP40: stale heap nodes are still work. Count every pop against the per-update
        // attempt budget so a stale-only heap cannot turn a 24-fire cap into an unbounded
        // cleanup scan in one frame.
        while (dueAttempts < RebirthFireDefaults.MaxProcessesPerUpdate &&
               processedLastUpdate < RebirthFireDefaults.MaxProcessesPerUpdate &&
               due.TryPeek(out entry) &&
               entry.DueWorldTime <= world.worldTime)
        {
            due.TryPop(out entry);
            dueAttempts++;
            RebirthFireState state;
            if (!fires.TryGetValue(entry.Position, out state))
                continue;
            if (entry.Token != state.ScheduleToken || entry.DueWorldTime != state.NextProcessWorldTime)
                continue;

            ProcessOne(world, state);
            processedLastUpdate++;
        }

        FlushBlockChanges();
        FlushDeltas();
    }

    private void MaybeCompactDueHeaps()
    {
        if (due.Count > fires.Count * 2 + RebirthFireDefaults.HeapCompactionSlack)
        {
            due.Clear();
            foreach (RebirthFireState state in fires.Values)
            {
                due.Push(new RebirthFireDueEntry
                {
                    Position = state.Position,
                    DueWorldTime = state.NextProcessWorldTime,
                    Token = state.ScheduleToken
                });
            }
        }

        if (cooldownDue.Count > cooldowns.Count * 2 + RebirthFireDefaults.HeapCompactionSlack)
        {
            cooldownDue.Clear();
            foreach (KeyValuePair<Vector3i, ulong> pair in cooldowns)
            {
                cooldownDue.Push(new RebirthFireDueEntry
                {
                    Position = pair.Key,
                    DueWorldTime = pair.Value,
                    Token = 0
                });
            }
        }
    }

    private void EnsureSimulationWindow(World world)
    {
        if (world == null)
            return;

        if (simulationWindowEndWorldTime != 0 && world.worldTime < simulationWindowEndWorldTime)
            return;

        simulationWindowEndWorldTime = AddSeconds(
            world.worldTime,
            RebirthFireDefaults.ProcessIntervalSeconds);
        simulatedThisWindow = 0;
        BuildSimulationSelection();
    }

    private void BuildSimulationSelection()
    {
        simulationSelection.Clear();
        simulationCandidates.Clear();

        foreach (RebirthFireState state in fires.Values)
        {
            if (HasActiveSimulationVisibility(state.Position))
                simulationCandidates.Add(state);
        }

        simulationEligibleLastWindow = simulationCandidates.Count;
        simulationNewFrontSelectedLastWindow = 0;

        // First spend at most eight of the unchanged 64 slots on the newest visible
        // positions that have never simulated. This makes a newly ignited second POI
        // start progressing promptly without allowing it to monopolize the window.
        simulationCandidates.Sort(delegate(RebirthFireState left, RebirthFireState right)
        {
            bool leftNever = !lastSimulatedWorldTime.ContainsKey(left.Position);
            bool rightNever = !lastSimulatedWorldTime.ContainsKey(right.Position);
            if (leftNever != rightNever)
                return leftNever ? -1 : 1;
            int ignition = right.IgnitedWorldTime.CompareTo(left.IgnitedWorldTime);
            if (ignition != 0)
                return ignition;
            return CompareSimulationPosition(left.Position, right.Position);
        });

        int newFrontLimit = Math.Min(
            RebirthFireDefaults.NewFrontSimulationReserve,
            RebirthFireDefaults.MaxSimulatedFiresPerWindow);
        for (int i = 0; i < simulationCandidates.Count &&
            simulationNewFrontSelectedLastWindow < newFrontLimit; i++)
        {
            RebirthFireState candidate = simulationCandidates[i];
            if (lastSimulatedWorldTime.ContainsKey(candidate.Position))
                break;
            if (simulationSelection.Add(candidate.Position))
                simulationNewFrontSelectedLastWindow++;
        }

        // Fill every remaining slot by strict fairness: never-run positions oldest
        // ignition first, then previously run positions by the oldest simulation time.
        // This guarantees progress even while another fire continues adding new blocks.
        simulationCandidates.Sort(delegate(RebirthFireState left, RebirthFireState right)
        {
            ulong leftLast;
            bool leftNever = !lastSimulatedWorldTime.TryGetValue(left.Position, out leftLast);
            ulong rightLast;
            bool rightNever = !lastSimulatedWorldTime.TryGetValue(right.Position, out rightLast);
            if (leftNever != rightNever)
                return leftNever ? -1 : 1;
            if (leftNever)
            {
                int ignition = left.IgnitedWorldTime.CompareTo(right.IgnitedWorldTime);
                if (ignition != 0)
                    return ignition;
            }
            else
            {
                int last = leftLast.CompareTo(rightLast);
                if (last != 0)
                    return last;
            }
            return CompareSimulationPosition(left.Position, right.Position);
        });

        int limit = Math.Min(
            RebirthFireDefaults.MaxSimulatedFiresPerWindow,
            simulationCandidates.Count);
        for (int i = 0; i < simulationCandidates.Count && simulationSelection.Count < limit; i++)
            simulationSelection.Add(simulationCandidates[i].Position);

        int neverRun = 0;
        foreach (Vector3i position in simulationSelection)
        {
            if (!lastSimulatedWorldTime.ContainsKey(position))
                neverRun++;
        }

        simulationSelectedLastWindow = simulationSelection.Count;
        simulationNeverRunSelectedLastWindow = neverRun;
    }

    private static int CompareSimulationPosition(Vector3i left, Vector3i right)
    {
        int x = left.x.CompareTo(right.x);
        if (x != 0)
            return x;
        int z = left.z.CompareTo(right.z);
        if (z != 0)
            return z;
        return left.y.CompareTo(right.y);
    }

    private bool TryClaimSimulationSlot(Vector3i position)
    {
        if (simulationSelection.Contains(position))
            return true;
        if (simulationSelection.Count >= RebirthFireDefaults.MaxSimulatedFiresPerWindow)
            return false;
        simulationSelection.Add(position);
        simulationSelectedLastWindow = simulationSelection.Count;
        return true;
    }

    private bool HasActiveSimulationVisibility(Vector3i position)
    {
        if (!GameManager.IsDedicatedServer &&
            RebirthFireVisualManager.IsFireParticleVisible(position))
            return true;

        float now = Time.realtimeSinceStartup;
        foreach (Dictionary<Vector3i, float> map in visibilityByPlayer.Values)
        {
            float reportTime;
            if (map.TryGetValue(position, out reportTime) &&
                now - reportTime <= 3f)
                return true;
        }
        return false;
    }

    private float ComputeSpreadThrottleFraction()
    {
        if (!RebirthFireDefaults.ThrottleSpreadToLegacyParticleBudget ||
            RebirthFireDefaults.LegacySpreadThrottleReferenceFires <= 0 ||
            fires.Count <= RebirthFireDefaults.LegacySpreadThrottleReferenceFires)
            return 1f;

        return Mathf.Clamp01(
            (float)RebirthFireDefaults.LegacySpreadThrottleReferenceFires / fires.Count);
    }

    private void ProcessOne(World world, RebirthFireState state)
    {
        Vector3i position = state.Position;
        if (world.GetChunkFromWorldPos(position) == null)
        {
            delayedForUnloadedChunk++;
            Reschedule(world, state, 5f);
            return;
        }

        BlockValue live = world.GetBlock(position);
        RebirthFireProfile profile = RebirthFireProfileRegistry.Resolve(live);
        if (!profile.Flammable)
        {
            RemoveAndDelta(position);
            return;
        }

        // 2.6 treated adjacent water as making the position non-flammable. It removed
        // the active fire without creating the manual/rain extinguish cooldown smoke.
        if (HasAdjacentWater(world, position))
        {
            extinguishedByWater++;
            RemoveAndDelta(position);
            return;
        }

        // Final 2.6 only damaged and spread positions that actually owned an active
        // fire particle. The v37/v38 scheduler accidentally simulated every tracked
        // position, including more than a thousand invisible fires. Preserve the expanded
        // client visuals, but limit authoritative damage/spread to the legacy 64-source window.
        if (!HasActiveSimulationVisibility(position))
        {
            simulationSelection.Remove(position);
            skippedInvisibleLastUpdate++;
            skippedInvisibleTotal++;
            Reschedule(world, state, profile.ProcessIntervalSeconds);
            return;
        }

        if (!TryClaimSimulationSlot(position) ||
            simulatedThisWindow >= RebirthFireDefaults.MaxSimulatedFiresPerWindow)
        {
            deferredSimulationCapLastUpdate++;
            deferredSimulationCapTotal++;
            ulong deferredUntil = simulationWindowEndWorldTime;
            if (deferredUntil <= world.worldTime)
                deferredUntil = AddSeconds(world.worldTime, 1f);
            RescheduleAt(state, deferredUntil);
            return;
        }

        simulatedThisWindow++;
        simulatedLastUpdate++;
        lastSimulatedWorldTime[position] = world.worldTime;
        NotifyHeatMap(world, position, profile);

        // Damage/destruction occurs first, followed by spread and rescheduling when the
        // resulting block remains flammable. Passive random self-extinguishing is disabled;
        // fire ends through rain/water handling, deliberate extinguishing, or loss of fuel.
        int appliedFireDamage = RebirthFireRuntimePolicy.ScaleBlockDamage(profile.DamagePerProcess);
        int nextDamage = live.damage + appliedFireDamage;
        bool destroyed = nextDamage >= live.Block.MaxDamage;
        BlockValue resulting = live;
        RebirthFireProfile resultingProfile = profile;
        if (destroyed)
        {
            resulting = ApplyDestruction(world, position, live, profile, state.IgniterEntityId);
            resultingProfile = RebirthFireProfileRegistry.Resolve(resulting);
            destroyedByFire++;
        }
        else
        {
            live.damage = (ushort)Math.Min(ushort.MaxValue, nextDamage);
            resulting = live;
            AddBlockChange(position, resulting, state.IgniterEntityId);
        }

        float extinguishChance = profile.ExtinguishChance;
        if (IsRaining(world) && WeatherManager.Instance.GetCurrentRainfallPercent() > 0.25f)
            extinguishChance = Mathf.Clamp01(extinguishChance * 2f);

        if (Deterministic01(position, state.ScheduleToken, 0x51) < extinguishChance)
        {
            extinguishedByChance++;
            Extinguish(world, position, profile.SmokeSeconds);
            return;
        }

        if (!resultingProfile.Flammable || resulting.isair)
        {
            RemoveAndDelta(position);
            return;
        }

        state.SourceBlockType = resulting.type;
        if (resultingProfile.Spread && spreadIgnitionsLastUpdate < RebirthFireDefaults.MaxSpreadIgnitionsPerUpdate)
            Spread(world, position, state, resultingProfile);

        Reschedule(world, state, resultingProfile.ProcessIntervalSeconds);
    }

    private void NotifyHeatMap(World world, Vector3i position, RebirthFireProfile profile)
    {
        heatMapAttemptsTotal++;
        lastHeatMapPosition = position;
        lastHeatMapStrength = profile != null ? profile.HeatMapStrengthPerProcess : 0f;

        if (!RebirthFireRuntimePolicy.AffectsHeatmap)
        {
            heatMapSkippedOptionTotal++;
            return;
        }
        if (world == null || profile == null || profile.HeatMapStrengthPerProcess <= 0f)
        {
            heatMapSkippedStrengthTotal++;
            return;
        }

        AIDirector aiDirector = world.GetAIDirector();
        if (aiDirector == null)
        {
            heatMapSkippedDirectorTotal++;
            return;
        }

        bool vanillaAccepts = GameStats.GetBool(EnumGameStats.ZombieHordeMeter)
            && GameStats.GetBool(EnumGameStats.IsSpawnEnemies)
            && !aiDirector.BloodMoonComponent.BloodMoonActive
            && !RebirthFireTwitchGate.IsBossHordeActive();
        if (!vanillaAccepts)
        {
            heatMapSkippedVanillaGateTotal++;
            return;
        }

        AIDirectorChunkData destination;
        if (!RebirthFireHeatMapDestinationProbe.TryGetChunkData(aiDirector, position, true, out destination) || destination == null)
        {
            lastHeatMapDestinationAvailable = false;
            lastHeatMapDestinationReady = false;
            lastHeatMapActivityBefore = 0f;
            lastHeatMapActivityAfter = 0f;
            lastHeatMapEventCountBefore = 0;
            lastHeatMapEventCountAfter = 0;
            heatMapDestinationUnavailableTotal++;
            return;
        }

        lastHeatMapDestinationAvailable = true;
        lastHeatMapDestinationReady = destination.IsReady;
        lastHeatMapActivityBefore = destination.ActivityLevel;
        lastHeatMapEventCountBefore = destination.EventCount;

        if (!destination.IsReady)
        {
            lastHeatMapActivityAfter = lastHeatMapActivityBefore;
            lastHeatMapEventCountAfter = lastHeatMapEventCountBefore;
            heatMapDestinationNotReadyTotal++;
            return;
        }

        aiDirector.NotifyActivity(
            EnumAIDirectorChunkEvent.Torch,
            position,
            profile.HeatMapStrengthPerProcess,
            RebirthFireDefaults.HeatMapDurationSeconds);

        lastHeatMapActivityAfter = destination.ActivityLevel;
        lastHeatMapEventCountAfter = destination.EventCount;
        float activityDelta = lastHeatMapActivityAfter - lastHeatMapActivityBefore;
        int eventDelta = lastHeatMapEventCountAfter - lastHeatMapEventCountBefore;

        if (activityDelta > 0f || eventDelta > 0)
        {
            heatMapEventsLastUpdate++;
            heatMapEventsTotal++;
            heatMapDestinationAcceptedTotal++;
            heatMapDestinationActivityDeltaTotal += activityDelta;
            heatMapDestinationEventDeltaTotal += eventDelta;
        }
        else
        {
            heatMapDestinationUnavailableTotal++;
        }
    }

    private BlockValue ApplyDestruction(
        World world,
        Vector3i position,
        BlockValue live,
        RebirthFireProfile profile,
        int igniterEntityId)
    {
        RebirthFireSleeperActivation.MarkFireDestroyedSupport(world, position);
        Block sourceBlock = live.Block;
        sourceBlock.SpawnDestroyParticleEffect(
            world,
            live,
            position,
            world.GetLightBrightness(position),
            sourceBlock.tintColor,
            igniterEntityId);

        if (sourceBlock.Properties.Values.ContainsKey("Explosion.ParticleIndex") ||
            sourceBlock.Properties.Classes.ContainsKey("Explosion"))
        {
            sourceBlock.OnBlockDestroyedByExplosion(
                world,
                (BlockValueRef)position,
                live,
                igniterEntityId);
        }

        BlockValue replacement = sourceBlock.DowngradeBlock;
        if (!string.IsNullOrEmpty(profile.DowngradeBlockName))
            replacement = Block.GetBlockValue(profile.DowngradeBlockName, false);

        if (!replacement.isair)
        {
            replacement = BlockPlaceholderMap.Instance.Replace(
                replacement,
                world.GetGameRandom(),
                position.x,
                position.z);
            replacement.rotation = live.rotation;
            replacement.meta = live.meta;
            replacement.damage = 0;
        }

        AddBlockChange(position, replacement, igniterEntityId);
        return replacement;
    }

    private void Spread(
        World world,
        Vector3i source,
        RebirthFireState state,
        RebirthFireProfile profile)
    {
        int start = (int)(Deterministic01(source, state.ScheduleToken, 0x29) * Directions.Length);
        for (int i = 0; i < Directions.Length; i++)
        {
            if (spreadIgnitionsLastUpdate >= RebirthFireDefaults.MaxSpreadIgnitionsPerUpdate)
                return;
            Vector3i target = source + Directions[(start + i) % Directions.Length];
            spreadCandidatesLastUpdate++;
            spreadCandidatesTotal++;
            float effectiveSpreadChance = Mathf.Clamp01(
                profile.SpreadChance * spreadThrottleFractionLastUpdate);
            float spreadRoll = Deterministic01(target, state.ScheduleToken, i + 0x31);
            if (effectiveSpreadChance < 1f && spreadRoll >= effectiveSpreadChance)
            {
                if (spreadThrottleFractionLastUpdate < 1f && spreadRoll < profile.SpreadChance)
                {
                    spreadThrottleRejectedLastUpdate++;
                    spreadThrottleRejectedTotal++;
                }
                continue;
            }
            if (TryIgniteInternal(world, target, state.IgniterEntityId, RebirthFireIgnitionCause.Spread, true))
            {
                spreadIgnitionsLastUpdate++;
                spreadIgnitionsTotal++;
            }
        }
    }

    private void Reschedule(World world, RebirthFireState state, float seconds)
    {
        RescheduleAt(state, AddSeconds(world.worldTime, seconds));
    }

    private void RescheduleAt(RebirthFireState state, ulong dueWorldTime)
    {
        state.ScheduleToken++;
        state.NextProcessWorldTime = dueWorldTime;
        fires[state.Position] = state;
        due.Push(new RebirthFireDueEntry
        {
            Position = state.Position,
            DueWorldTime = state.NextProcessWorldTime,
            Token = state.ScheduleToken
        });
    }

    private void RemoveAndDelta(Vector3i position)
    {
        if (!RemoveInternal(position, false))
            return;
        revision++;
        QueueDelta(position, RebirthFireDeltaOperation.Remove, 0);
    }

    private void AddBlockChange(Vector3i position, BlockValue value, int entityId)
    {
        // 3.1 Chunk.SetBlock assumes every changedByEntityId other than -1 resolves to a
        // PersistentPlayerData entry when a non-air block is added. Fire ignition can
        // originate from zombies/NPCs and the original igniter can also be removed
        // (for example by killall) before a delayed fire tick destroys/downgrades a
        // block. Passing that stale/non-player id makes vanilla dereference null.
        int safeChangedByEntityId = ResolveSafeChangedByEntityId(entityId);
        blockChanges.Add(new BlockChangeInfo((BlockValueRef)position, value, true, safeChangedByEntityId));
        if (blockChanges.Count >= RebirthFireDefaults.MaxBlockChangesPerRpc)
            FlushBlockChanges();
    }

    private static int ResolveSafeChangedByEntityId(int entityId)
    {
        if (entityId < 0 || GameManager.Instance == null)
            return -1;

        try
        {
            PersistentPlayerData persistent = GameManager.Instance
                .GetPersistentPlayerList()
                ?.GetPlayerDataFromEntityID(entityId);
            return persistent != null && persistent.PrimaryId != null ? entityId : -1;
        }
        catch
        {
            return -1;
        }
    }

    private void FlushBlockChanges()
    {
        if (blockChanges.Count == 0 || GameManager.Instance == null)
            return;
        GameManager.Instance.SetBlocksRPC(blockChanges);
        blockChanges.Clear();
    }

    private void QueueDelta(Vector3i position, RebirthFireDeltaOperation operation, ulong expiry)
    {
        pendingDeltas.Add(new RebirthFireDeltaEntry
        {
            Position = position,
            Operation = operation,
            ExpiryWorldTime = expiry
        });
        if (pendingDeltas.Count >= RebirthFireDefaults.MaxNetworkDeltaEntries)
            FlushDeltas();
    }

    private void FlushDeltas()
    {
        if (pendingDeltas.Count == 0 || !IsServer())
            return;
        RebirthFireNetwork.BroadcastDeltas(revision, pendingDeltas);
        pendingDeltas.Clear();
    }

    private void PruneCooldowns(ulong now)
    {
        RebirthFireDueEntry entry;
        int budget = 64;
        while (budget-- > 0 && cooldownDue.TryPeek(out entry) && entry.DueWorldTime <= now)
        {
            cooldownDue.TryPop(out entry);
            ulong expiry;
            if (cooldowns.TryGetValue(entry.Position, out expiry) && expiry <= now)
                cooldowns.Remove(entry.Position);
        }
    }

    private static bool HasAdjacentWater(World world, Vector3i position)
    {
        for (int i = 0; i < Directions.Length; i++)
        {
            Vector3i target = position + Directions[i];
            BlockValue value = world.GetBlock(target);
            if (value.isWater || value.Block is BlockLiquidv2)
                return true;
            if (world.GetWaterPercent(target) > 0.25f)
                return true;
        }
        return false;
    }

    private static bool IsRaining(World world)
    {
        return world != null && !world.IsEditor() && WeatherManager.Instance != null;
    }

    public List<RebirthFireState> SnapshotFires()
    {
        return new List<RebirthFireState>(fires.Values);
    }

    public Dictionary<Vector3i, ulong> SnapshotCooldowns()
    {
        return new Dictionary<Vector3i, ulong>(cooldowns);
    }

    public void Restore(
        World world,
        IList<RebirthFireState> savedFires,
        IDictionary<Vector3i, ulong> savedCooldowns)
    {
        ClearAll(false);
        if (world == null)
            return;

        ulong now = world.worldTime;
        if (savedFires != null)
        {
            // v38 could persist thousands of runaway entries. Read the old file, prefer
            // the newest fire front, then enforce bounded global and per-chunk state.
            List<RebirthFireState> restoreCandidates =
                new List<RebirthFireState>(savedFires.Count);
            for (int i = 0; i < savedFires.Count; i++)
                restoreCandidates.Add(savedFires[i]);
            restoreCandidates.Sort(delegate(RebirthFireState left, RebirthFireState right)
            {
                int time = right.IgnitedWorldTime.CompareTo(left.IgnitedWorldTime);
                if (time != 0)
                    return time;
                int x = left.Position.x.CompareTo(right.Position.x);
                if (x != 0)
                    return x;
                int z = left.Position.z.CompareTo(right.Position.z);
                if (z != 0)
                    return z;
                return left.Position.y.CompareTo(right.Position.y);
            });

            int loaded = 0;
            for (int i = 0;
                 i < restoreCandidates.Count &&
                 loaded < RebirthFireDefaults.MaxActiveFiresGlobal;
                 i++)
            {
                RebirthFireState state = restoreCandidates[i];
                if (world.IsWithinTraderArea(state.Position) ||
                    fires.ContainsKey(state.Position))
                    continue;

                long key = GetChunkKey(state.Position);
                HashSet<Vector3i> chunkPositions;
                if (!firePositionsByChunk.TryGetValue(key, out chunkPositions))
                {
                    chunkPositions = new HashSet<Vector3i>();
                    firePositionsByChunk.Add(key, chunkPositions);
                }
                if (chunkPositions.Count >= RebirthFireDefaults.MaxActiveFiresPerChunk)
                    continue;

                if (state.ScheduleToken == 0)
                    state.ScheduleToken = 1;
                // Do not require the chunk to be loaded during world startup. Overdue
                // entries receive one normal interval when their chunk becomes available;
                // they never run a return-time catch-up burst.
                if (state.NextProcessWorldTime <= now)
                    state.NextProcessWorldTime = AddSeconds(now, RebirthFireDefaults.ProcessIntervalSeconds);
                fires[state.Position] = state;
                chunkPositions.Add(state.Position);
                loaded++;
                if (chunkPositions.Count > maxChunkFireCount)
                    maxChunkFireCount = chunkPositions.Count;
                due.Push(new RebirthFireDueEntry
                {
                    Position = state.Position,
                    DueWorldTime = state.NextProcessWorldTime,
                    Token = state.ScheduleToken
                });
            }

            trimmedOnRestore = Math.Max(0, savedFires.Count - loaded);
            if (trimmedOnRestore > 0)
            {
                Log.Warning("[REBIRTH Fire] trimmed persisted active fires from "
                    + savedFires.Count + " to " + loaded
                    + " (globalCap=" + RebirthFireDefaults.MaxActiveFiresGlobal
                    + ", perChunkCap=" + RebirthFireDefaults.MaxActiveFiresPerChunk + ").");
            }
        }

        if (savedCooldowns != null)
        {
            foreach (KeyValuePair<Vector3i, ulong> pair in savedCooldowns)
            {
                if (pair.Value <= now)
                    continue;
                cooldowns[pair.Key] = pair.Value;
                cooldownDue.Push(new RebirthFireDueEntry
                {
                    Position = pair.Key,
                    DueWorldTime = pair.Value,
                    Token = 0
                });
            }
        }

        revision++;
    }

    public void RecordVisibility(int playerEntityId, IList<Vector3i> visiblePositions)
    {
        if (playerEntityId < 0)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        Entity player = world != null ? world.GetEntity(playerEntityId) : null;
        if (player == null)
            return;

        Dictionary<Vector3i, float> map;
        if (!visibilityByPlayer.TryGetValue(playerEntityId, out map))
        {
            map = new Dictionary<Vector3i, float>();
            visibilityByPlayer[playerEntityId] = map;
        }
        map.Clear();
        float now = Time.realtimeSinceStartup;
        float maximumDistance = RebirthFireVisualManager.FireRenderDistance + 8f;
        float maximumDistanceSquared = maximumDistance * maximumDistance;
        int count = visiblePositions == null ? 0 : Math.Min(visiblePositions.Count, RebirthFireVisualManager.FireParticleCap);
        for (int i = 0; i < count; i++)
        {
            Vector3i position = visiblePositions[i];
            if (fires.ContainsKey(position) &&
                (position.ToVector3Center() - player.position).sqrMagnitude <= maximumDistanceSquared)
                map[position] = now;
        }
    }

    public bool ShouldApplyContactBuff(Entity entity, Vector3i position)
    {
        if (!IsBurning(position) || entity == null)
            return false;

        if (entity is EntityPlayer)
        {
            // Host/single-player local players can be verified against the actual local
            // particle. Remote players are authorized only by the dedicated contact
            // package generated by their own client at the moment of visible contact.
            if (!GameManager.IsDedicatedServer && entity is EntityPlayerLocal)
                return RebirthFireVisualManager.IsFireParticleVisible(position);
            return false;
        }

        foreach (Dictionary<Vector3i, float> map in visibilityByPlayer.Values)
        {
            float report;
            if (map.TryGetValue(position, out report) && Time.realtimeSinceStartup - report <= 3f)
                return true;
        }
        return !GameManager.IsDedicatedServer && RebirthFireVisualManager.IsFireParticleVisible(position);
    }

    private void PruneVisibilityReports()
    {
        if (visibilityByPlayer.Count == 0)
            return;
        float now = Time.realtimeSinceStartup;
        List<int> emptyPlayers = null;
        foreach (KeyValuePair<int, Dictionary<Vector3i, float>> player in visibilityByPlayer)
        {
            requestPositions.Clear();
            foreach (KeyValuePair<Vector3i, float> report in player.Value)
            {
                if (now - report.Value > 3f || !fires.ContainsKey(report.Key))
                    requestPositions.Add(report.Key);
            }
            for (int i = 0; i < requestPositions.Count; i++)
                player.Value.Remove(requestPositions[i]);
            if (player.Value.Count == 0)
            {
                if (emptyPlayers == null)
                    emptyPlayers = new List<int>();
                emptyPlayers.Add(player.Key);
            }
        }
        if (emptyPlayers != null)
        {
            for (int i = 0; i < emptyPlayers.Count; i++)
                visibilityByPlayer.Remove(emptyPlayers[i]);
        }
    }

    private void ClearVisibilityForPosition(Vector3i position)
    {
        foreach (Dictionary<Vector3i, float> map in visibilityByPlayer.Values)
            map.Remove(position);
    }

    public static ulong AddSeconds(ulong worldTime, float seconds)
    {
        int ticksPerSecond = Math.Max(1, GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec));
        ulong ticks = (ulong)Math.Max(1.0, Math.Ceiling(seconds * ticksPerSecond));
        return worldTime + ticks;
    }

    public static float WorldTicksToSeconds(ulong ticks)
    {
        int ticksPerSecond = Math.Max(1, GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec));
        return ticks / (float)ticksPerSecond;
    }

    private static float Deterministic01(Vector3i position, uint sequence, int salt)
    {
        unchecked
        {
            uint hash = (uint)(position.x * 73856093 ^ position.y * 19349663 ^ position.z * 83492791);
            hash ^= sequence * 2654435761u;
            hash ^= (uint)salt;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return (hash & 0x00FFFFFFu) / 16777216f;
        }
    }

    private static void LogDebugIgnition(RebirthFireIgnitionCause cause, Vector3i position, string result)
    {
        if (cause == RebirthFireIgnitionCause.Debug)
            Log.Out("[RBFireTest][Authority] ignition position=" + position + " result=" + result);
    }

    private static long GetChunkKey(Vector3i position)
    {
        return GetChunkKey(World.toChunkXZ(position.x), World.toChunkXZ(position.z));
    }

    private static long GetChunkKey(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) ^ (uint)chunkZ;
    }

    private static bool IsServer()
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return manager == null || manager.IsServer;
    }
}

internal static class RebirthFireDelayedIgnitionQueue
{
    private static readonly RebirthFireIgnitionHeap Pending =
        new RebirthFireIgnitionHeap();
    private static ulong nextSequence;

    public static int Count { get { return Pending.Count; } }

    public static void Schedule(
        IList<Vector3i> positions,
        int igniterEntityId,
        RebirthFireIgnitionCause cause,
        ulong dueWorldTime)
    {
        int available = Math.Max(0, RebirthFireDefaults.MaxActiveFiresGlobal * 2 - Pending.Count);
        int count = Math.Min(Math.Min(positions.Count, RebirthFireDefaults.MaxRequestPositions), available);
        for (int i = 0; i < count; i++)
        {
            Pending.Push(new RebirthFireIgnitionEntry
            {
                Position = positions[i],
                IgniterEntityId = igniterEntityId,
                Cause = cause,
                DueWorldTime = dueWorldTime,
                Sequence = ++nextSequence
            });
        }
    }

    public static void Process(World world)
    {
        if (world == null || Pending.Count == 0)
            return;

        int processed = 0;
        RebirthFireIgnitionEntry pending;
        while (processed < RebirthFireDefaults.MaxQueuedIgnitionsPerUpdate &&
               Pending.TryPeek(out pending) &&
               pending.DueWorldTime <= world.worldTime)
        {
            Pending.TryPop(out pending);
            RebirthFireService.Instance.TryIgnite(
                world,
                pending.Position,
                pending.IgniterEntityId,
                pending.Cause);
            processed++;
        }
    }

    public static void Clear()
    {
        Pending.Clear();
        nextSequence = 0;
    }
}

public static class RebirthFirePersistence
{
    private const uint Magic = 0x52424652;
    private const ushort Version = 2;
    private static int lastSavedRevision = -1;
    private static float nextAutosaveTime;

    private static string SavePath
    {
        get { return Path.Combine(GameIO.GetSaveGameDir(), "RebirthFireManager.dat"); }
    }

    public static void ResetSession()
    {
        lastSavedRevision = -1;
        nextAutosaveTime = Time.realtimeSinceStartup + RebirthFireDefaults.PersistenceAutosaveSeconds;
    }

    public static void UpdateAutosave()
    {
        if (Time.realtimeSinceStartup < nextAutosaveTime)
            return;
        nextAutosaveTime = Time.realtimeSinceStartup + RebirthFireDefaults.PersistenceAutosaveSeconds;
        if (lastSavedRevision == RebirthFireService.Instance.Revision)
            return;
        SaveSafely("autosave");
    }

    public static void ClearStoredState()
    {
        string path = SavePath;
        TryDelete(path);
        TryDelete(path + ".tmp");
        TryDelete(path + ".bak");
        lastSavedRevision = RebirthFireService.Instance.Revision;
        nextAutosaveTime = Time.realtimeSinceStartup + RebirthFireDefaults.PersistenceAutosaveSeconds;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Fire] could not delete persistence file " + path + ": " + ex.Message);
        }
    }

    public static bool SaveSafely(string reason)
    {
        try
        {
            Save();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Fire] persistence " + reason + " failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    public static void Save()
    {
        if (string.IsNullOrEmpty(GameIO.GetSaveGameDir()))
            return;
        if (!RebirthFireRuntimePolicy.Enabled)
        {
            ClearStoredState();
            return;
        }

        List<RebirthFireState> fires = RebirthFireService.Instance.SnapshotFires();
        Dictionary<Vector3i, ulong> cooldowns = RebirthFireService.Instance.SnapshotCooldowns();
        string path = SavePath;
        string temp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(fires.Count);
            for (int i = 0; i < fires.Count; i++)
            {
                RebirthFireState state = fires[i];
                writer.Write(state.Position.x);
                writer.Write(state.Position.y);
                writer.Write(state.Position.z);
                writer.Write(state.IgnitedWorldTime);
                writer.Write(state.NextProcessWorldTime);
                writer.Write(state.IgniterEntityId);
                writer.Write(state.SourceBlockType);
                writer.Write(state.ScheduleToken);
                writer.Write(state.Flags);
            }

            writer.Write(cooldowns.Count);
            foreach (KeyValuePair<Vector3i, ulong> pair in cooldowns)
            {
                writer.Write(pair.Key.x);
                writer.Write(pair.Key.y);
                writer.Write(pair.Key.z);
                writer.Write(pair.Value);
            }
            writer.Flush();
            stream.Flush(true);
        }

        string publishError;
        if (!RebirthDurableFileCommit.TryPublish(temp, path, out publishError))
            throw new IOException("fire durable publication failed: " + publishError);
        lastSavedRevision = RebirthFireService.Instance.Revision;
        nextAutosaveTime = Time.realtimeSinceStartup + RebirthFireDefaults.PersistenceAutosaveSeconds;
    }

    public static void Load(World world)
    {
        if (world == null || !RebirthFireRuntimePolicy.Enabled)
            return;
        string path = SavePath;
        string backup = path + ".bak";
        if (!File.Exists(path) && !File.Exists(backup))
            return;
        Exception primaryFailure = null;
        if (File.Exists(path))
        {
            try
            {
                LoadFile(world, path);
                return;
            }
            catch (Exception primary)
            {
                primaryFailure = primary;
            }
        }
        if (!File.Exists(backup))
        {
            Log.Warning("[REBIRTH Fire] persistence load failed: " +
                (primaryFailure != null ? primaryFailure.Message : "primary missing and backup unavailable"));
            return;
        }
        try
        {
            LoadFile(world, backup);
            Log.Warning("[REBIRTH Fire] recovered persistence from backup" +
                (primaryFailure != null ? " after primary failure: " + primaryFailure.Message : " because primary was missing"));
        }
        catch (Exception secondary)
        {
            Log.Warning("[REBIRTH Fire] persistence recovery failed: " + secondary.Message);
        }
    }

    private static void LoadFile(World world, string path)
    {
        List<RebirthFireState> fires = new List<RebirthFireState>();
        Dictionary<Vector3i, ulong> cooldowns = new Dictionary<Vector3i, ulong>();

        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            if (reader.ReadUInt32() != Magic)
                throw new InvalidDataException("invalid magic");
            ushort version = reader.ReadUInt16();
            if (version != Version)
                throw new InvalidDataException("unsupported fire persistence version " + version);

            int fireCount = reader.ReadInt32();
            if (fireCount < 0 || fireCount > RebirthFireDefaults.MaxSnapshotFires)
                throw new InvalidDataException("invalid fire count");
            for (int i = 0; i < fireCount; i++)
            {
                fires.Add(new RebirthFireState
                {
                    Position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                    IgnitedWorldTime = reader.ReadUInt64(),
                    NextProcessWorldTime = reader.ReadUInt64(),
                    IgniterEntityId = reader.ReadInt32(),
                    SourceBlockType = reader.ReadInt32(),
                    ScheduleToken = reader.ReadUInt32(),
                    Flags = reader.ReadByte()
                });
            }

            int cooldownCount = reader.ReadInt32();
            if (cooldownCount < 0 || cooldownCount > RebirthFireDefaults.MaxSnapshotCooldowns)
                throw new InvalidDataException("invalid cooldown count");
            for (int i = 0; i < cooldownCount; i++)
            {
                Vector3i position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                cooldowns[position] = reader.ReadUInt64();
            }
        }

        RebirthFireService.Instance.Restore(world, fires, cooldowns);
        lastSavedRevision = RebirthFireService.Instance.Revision;
        nextAutosaveTime = Time.realtimeSinceStartup + RebirthFireDefaults.PersistenceAutosaveSeconds;
    }
}

[Preserve]
public sealed class RebirthFireModApi : IModApi
{
    private static bool initialized;

    public void InitMod(Mod mod)
    {
        if (initialized)
            return;
        initialized = true;

        ModEvents.GameStarting.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.GameStartDone.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartDoneData>(OnGameStartDone));
        ModEvents.GameUpdate.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawned));
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthFireProfileRegistry.Clear();
        RebirthFireService.Instance.ClearAll(false);
        RebirthFireVisualManager.ClearAll();
        RebirthFirePersistence.ResetSession();
    }

    private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (world != null && manager != null && manager.IsServer)
        {
            if (RebirthFireRuntimePolicy.Enabled)
                RebirthFirePersistence.Load(world);
            else
                RebirthFirePersistence.ClearStoredState();
        }
        RebirthFireVisualManager.Initialize();

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Fire] runtime ready enabled=" + RebirthFireRuntimePolicy.Enabled
            + " patches=" + RebirthFirePatchInstaller.IsInstalled
            + " active=" + RebirthFireService.Instance.ActiveCount); }
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        RebirthFireService.Instance.Update(world);
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || manager.IsServer)
            RebirthFirePersistence.UpdateAutosave();
        RebirthFireVisualManager.Update(world);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || manager.IsServer)
            RebirthFirePersistence.SaveSafely("shutdown save");
        RebirthFireService.Instance.ClearAll(false);
        RebirthFireVisualManager.ClearAll();
    }

    private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager != null && manager.IsServer)
            RebirthFireNetwork.SendSnapshot(data.ClientInfo);
    }
}
