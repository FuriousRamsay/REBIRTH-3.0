using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

public static class RebirthMetabolismStateRepository
{
    private const int PersistenceIngestionSafetyCeiling = 1024;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, RebirthMetabolismState> States =
        new Dictionary<string, RebirthMetabolismState>(StringComparer.Ordinal);
    private static bool loaded;
    private static bool serverAuthority;
    private const int LoadRetrySeconds = 5;
    private static long nextLoadAttempt;
    private static string failedLoadPath;
    private static int loadGeneration;

    public static void Reset(bool asServer)
    {
        lock (Sync)
        {
            States.Clear();
            loaded = false;
            nextLoadAttempt = 0;
            failedLoadPath = null;
            unchecked { loadGeneration++; }
            serverAuthority = asServer;
        }
    }

    public static bool IsServerAuthority { get { return serverAuthority; } }

    public static void SetServerAuthority(bool asServer)
    {
        serverAuthority = asServer;
    }

    public static string GetStablePlayerId(EntityPlayer player)
    {
        if (player == null || GameManager.Instance == null)
            return string.Empty;

        // New Rebirth-progression characters use the exact same audited server-derived identity
        // contract as world-character persistence. This prevents Survivor origin and metabolism
        // from being keyed by different platform identifier flavors.
        if (RebirthSurvivorMode.IsEnabledForCurrentWorld() && RebirthWorldCharacterRepository.IsServerAuthority)
        {
            RebirthStablePlayerIdentity identity;
            if (RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) && identity != null)
                return identity.CanonicalId;
            return string.Empty; // fail closed until authenticated server identity is available
        }

        // Base Game progression keeps the established metabolism identity for compatibility with
        // existing saves/codes that predate the Survivor system.
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        return persistent != null && persistent.PrimaryId != null ? persistent.PrimaryId.CombinedString : string.Empty;
    }

    public static RebirthMetabolismState GetOrCreate(EntityPlayer player)
    {
        string id = GetStablePlayerId(player);
        if (string.IsNullOrEmpty(id))
            return null;

        if (!EnsureLoaded())
            return null;
        lock (Sync)
        {
            RebirthMetabolismState state;
            if (!States.TryGetValue(id, out state) || state == null)
            {
                state = CreateDefault(player);
                States[id] = state;
                state.Dirty = true;
            }
            return state;
        }
    }

    public static bool TryGet(EntityPlayer player, out RebirthMetabolismState state)
    {
        state = null;
        string id = GetStablePlayerId(player);
        if (string.IsNullOrEmpty(id))
            return false;
        if (!EnsureLoaded())
            return false;
        lock (Sync)
            return States.TryGetValue(id, out state) && state != null;
    }

    public static RebirthMetabolismState ResetForFreshCharacter(EntityPlayer player, string reason)
    {
        string id = GetStablePlayerId(player);
        if (string.IsNullOrEmpty(id))
            return null;

        if (!EnsureLoaded())
            return null;
        RebirthMetabolismState fresh = CreateDefault(player);
        fresh.Dirty = true;
        lock (Sync)
            States[id] = fresh;

        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Metabolism] reset persisted state for fresh character id=" + id
            + " reason=" + (reason ?? "fresh-spawn")); }
        return fresh;
    }

    public static RebirthMetabolismState ResetForFreshSurvivorCharacter(EntityPlayer player, string creationId, string reason)
    {
        if (string.IsNullOrEmpty(creationId))
            return null;

        RebirthMetabolismState fresh = ResetForFreshCharacter(player, reason);
        if (fresh == null)
            return null;

        fresh.SurvivorCreationId = creationId;
        fresh.Version = RebirthMetabolismState.CurrentVersion;
        fresh.Touch();
        return fresh;
    }


    public static void PrepareOnlineState(EntityPlayer player)
    {
        RebirthMetabolismState state = GetOrCreate(player);
        if (state == null)
            return;
        ulong now = player.world != null ? player.world.GetWorldTime() : 0UL;
        state.LastProcessedWorldTime = now;
        state.NextRealTickTime = UnityEngine.Time.realtimeSinceStartup;
        state.LastRealProcessTime = UnityEngine.Time.realtimeSinceStartup;
        state.LastStamina = player.Stats != null && player.Stats.Stamina != null ? player.Stats.Stamina.Value : -1f;
    }

    public static void SaveIfDirty(string reason)
    {
        string ignored;
        TrySaveIfDirty(reason, out ignored);
    }

    public static bool TrySaveIfDirty(string reason, out string error)
    {
        error = string.Empty;
        if (!serverAuthority)
        {
            error = "metabolism repository is not server-authoritative";
            return false;
        }

        if (!EnsureLoaded())
        {
            error = "metabolism persistence could not be loaded safely";
            return false;
        }
        bool dirty = false;
        lock (Sync)
        {
            foreach (RebirthMetabolismState state in States.Values)
            {
                if (state != null && state.Dirty)
                {
                    dirty = true;
                    break;
                }
            }
        }

        if (!dirty)
            return true;
        return TrySave(reason, out error);
    }

    public static void Save(string reason)
    {
        string error;
        if (!TrySave(reason, out error) && !string.IsNullOrEmpty(error))
            Log.Error("[REBIRTH Metabolism] persistence save failed: " + error);
    }

    public static bool TrySave(string reason, out string error)
    {
        error = string.Empty;
        if (!serverAuthority)
        {
            error = "metabolism repository is not server-authoritative";
            return false;
        }

        if (!EnsureLoaded())
        {
            error = "metabolism persistence could not be loaded safely";
            return false;
        }
        string path = PathName;
        if (string.IsNullOrEmpty(path))
        {
            error = "metabolism persistence path is unavailable";
            return false;
        }

        XElement root = new XElement("rebirthMetabolismPlayers", new XAttribute("version", RebirthMetabolismState.CurrentVersion));
        int playerCount;
        Dictionary<string, int> writtenRevisions = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            lock (Sync)
            {
                foreach (KeyValuePair<string, RebirthMetabolismState> pair in States)
                {
                    ValidateStateForPersistence(pair.Value);
                    root.Add(SerializePlayer(pair.Key, pair.Value));
                    if (pair.Value != null)
                        writtenRevisions[pair.Key] = pair.Value.Revision;
                }
                playerCount = States.Count;
            }
        }
        catch (Exception ex)
        {
            error = "metabolism snapshot validation failed: " + ex.GetType().Name + ": " + ex.Message;
            return false;
        }

        if (!RebirthAtomicXmlFile.TryWrite(path, new XDocument(root), out error))
            return false;

        lock (Sync)
        {
            foreach (KeyValuePair<string, int> written in writtenRevisions)
            {
                RebirthMetabolismState current;
                if (States.TryGetValue(written.Key, out current) && current != null && current.Revision == written.Value)
                    current.Dirty = false;
            }
        }

        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Metabolism] persistence saved players=" + playerCount + " reason=" + (reason ?? "unspecified")); }
        return true;
    }

    private static bool EnsureLoaded()
    {
        if (loaded)
            return true;

        string path;
        bool authority;
        int generation;
        lock (Sync)
        {
            if (loaded) return true;
            path = PathName;
            authority = serverAuthority;
            generation = loadGeneration;
            // A damaged file must remain fail-closed, without reparsing and logging
            // on every player/update request. A new path or Reset retries immediately.
            if (string.Equals(failedLoadPath, path, StringComparison.Ordinal) &&
                System.Diagnostics.Stopwatch.GetTimestamp() < nextLoadAttempt)
                return false;
            if (!authority || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                States.Clear();
                loaded = true;
                return true;
            }
        }

        try
        {
            XDocument doc = XDocument.Load(path);
            XElement root = doc.Root;
            if (root == null)
                throw new InvalidDataException("metabolism persistence root is missing");

            Dictionary<string, RebirthMetabolismState> candidate =
                new Dictionary<string, RebirthMetabolismState>(StringComparer.Ordinal);
            foreach (XElement node in root.Elements("player"))
            {
                string id = A(node, "id", string.Empty);
                if (string.IsNullOrEmpty(id))
                    throw new InvalidDataException("metabolism persistence player id is missing");
                RebirthMetabolismState state = DeserializePlayer(node);
                if (state == null)
                    throw new InvalidDataException("metabolism persistence player could not be decoded: " + id);
                candidate[id] = state;
            }

            lock (Sync)
            {
                if (generation != loadGeneration) return false;
                if (loaded) return true;
                nextLoadAttempt = 0;
                failedLoadPath = null;
                States.Clear();
                foreach (KeyValuePair<string, RebirthMetabolismState> pair in candidate)
                    States[pair.Key] = pair.Value;
                loaded = true;
            }
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Metabolism] persistence loaded players=" + candidate.Count); }
            return true;
        }
        catch (Exception ex)
        {
            // Never publish a partial candidate or overwrite the unreadable file.
            // Ignore an old world's failure after Reset, including its retry deadline.
            lock (Sync)
            {
                if (generation != loadGeneration || loaded) return false;
                failedLoadPath = path;
                nextLoadAttempt = System.Diagnostics.Stopwatch.GetTimestamp() +
                    LoadRetrySeconds * System.Diagnostics.Stopwatch.Frequency;
            }
            Log.Error("[REBIRTH Metabolism] persistence load failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }

    private static RebirthMetabolismState CreateDefault(EntityPlayer player)
    {
        RebirthMetabolismState state = new RebirthMetabolismState();
        state.Initialized = false;
        state.DigestiveHealth = RebirthMetabolismConfig.BaseDigestiveHealth;
        state.Energy = RebirthMetabolismConfig.EnergyMax;
        state.StartupSanitized = false;
        state.HydrationSlotItem = ItemStack.Empty.Clone();
        state.LastProcessedWorldTime = player != null && player.world != null ? player.world.GetWorldTime() : 0UL;
        return state;
    }

    private static XElement SerializePlayer(string id, RebirthMetabolismState state)
    {
        state = state ?? new RebirthMetabolismState();
        XElement e = new XElement("player",
            new XAttribute("id", id ?? string.Empty),
            new XAttribute("version", state.Version),
            new XAttribute("initialized", state.Initialized),
            new XAttribute("survivorCreationId", state.SurvivorCreationId ?? string.Empty),
            new XAttribute("digestiveHealth", S(state.DigestiveHealth)),
            new XAttribute("energy", S(state.Energy)),
            new XAttribute("startupSanitized", state.StartupSanitized),
            new XAttribute("autoSip", state.AutoSipEnabled),
            new XAttribute("autoSipCooldownRealSeconds", S(state.AutoSipCooldownRemainingRealSeconds)),
            new XAttribute("lastWorldTime", state.LastProcessedWorldTime),
            new XAttribute("nextEntryId", state.NextEntryId));

        if (state.HydrationSlotItem != null && !state.HydrationSlotItem.IsEmpty())
            e.Add(new XElement("hydrationSlot", EncodeItemStack(state.HydrationSlotItem)));

        XElement entries = new XElement("ingestionEntries");
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry x = state.IngestionEntries[i];
            if (x == null) continue;
            entries.Add(new XElement("entry",
                new XAttribute("id", x.EntryId),
                new XAttribute("kind", (int)x.Kind),
                new XAttribute("source", x.SourceItemName ?? string.Empty),
                new XAttribute("liquid", x.LiquidProfileId ?? string.Empty),
                new XAttribute("digestion", x.DigestionProfileId ?? string.Empty),
                new XAttribute("safety", x.FoodSafetyProfileId ?? string.Empty),
                new XAttribute("contamination", x.ContaminationProfileId ?? string.Empty),
                new XAttribute("time", x.IngestedWorldTime),
                new XAttribute("fluidMl", S(x.RemainingFluidVolumeMl)),
                new XAttribute("solidMl", S(x.RemainingSolidVolumeMl)),
                new XAttribute("nutrition", S(x.RemainingNutritionUnits)),
                new XAttribute("energy", S(x.RemainingEnergyUnits)),
                new XAttribute("intestinalFluidMl", S(x.IntestinalFluidVolumeMl)),
                new XAttribute("intestinalSolidMl", S(x.IntestinalSolidVolumeMl)),
                new XAttribute("intestinalNutrition", S(x.IntestinalNutritionUnits)),
                new XAttribute("intestinalEnergy", S(x.IntestinalEnergyUnits)),
                new XAttribute("intestinalFluidResidenceSeconds", S(x.IntestinalFluidResidenceSecondsRemaining)),
                new XAttribute("intestinalNutritionResidenceSeconds", S(x.IntestinalNutritionResidenceSecondsRemaining)),
                new XAttribute("mealCredited", x.MeaningfulMealCredited),
                new XAttribute("fluidHoldSeconds", S(x.FluidHoldSecondsRemaining)),
                new XAttribute("solidHoldSeconds", S(x.SolidHoldSecondsRemaining)),
                new XAttribute("fluidHalfTimeSeconds", S(x.FluidGastricHalfTimeSecondsSnapshot)),
                new XAttribute("fluidMealBound", x.FluidIsMealBound),
                new XAttribute("fluidYield", S(x.FluidYieldMultiplierSnapshot)),
                new XAttribute("nutrientYield", S(x.NutrientYieldMultiplierSnapshot)),
                new XAttribute("digestionRate", S(x.DigestionRateMultiplierSnapshot)),
                new XAttribute("preparedEnergyEfficiency", S(x.PreparedMealEnergyEfficiencyMultiplier)),
                new XAttribute("preparedDrinkEffectSeconds", S(x.PreparedDrinkSpecialEffectDurationSeconds)),
                new XAttribute("preparedDrinkEffectActivated", x.PreparedDrinkSpecialEffectActivated)));
        }
        e.Add(entries);

        XElement effects = new XElement("timedEffects");
        for (int i = 0; i < state.TimedEffects.Count; i++)
        {
            RebirthMetabolismTimedEffect x = state.TimedEffects[i];
            if (x == null) continue;
            effects.Add(new XElement("effect",
                new XAttribute("profile", x.ProfileId ?? string.Empty),
                new XAttribute("remainingRealSeconds", S(x.RemainingRealSeconds)),
                new XAttribute("stacks", Math.Max(1, x.Stacks))));
        }
        e.Add(effects);
        return e;
    }

    private static RebirthMetabolismState DeserializePlayer(XElement e)
    {
        RebirthMetabolismState state = new RebirthMetabolismState();
        int loadedVersion = AI(e, "version", RebirthMetabolismState.CurrentVersion);
        state.Version = loadedVersion;
        state.Initialized = AB(e, "initialized", false);
        state.SurvivorCreationId = A(e, "survivorCreationId", string.Empty);
        state.DigestiveHealth = AF(e, "digestiveHealth", RebirthMetabolismConfig.BaseDigestiveHealth);
        state.Energy = AF(e, "energy", RebirthMetabolismConfig.EnergyMax);
        state.StartupSanitized = AB(e, "startupSanitized", false);
        state.AutoSipEnabled = AB(e, "autoSip", true);
        state.AutoSipCooldownRemainingRealSeconds = AF(e, "autoSipCooldownRealSeconds", 0f);
        state.LastProcessedWorldTime = AU(e, "lastWorldTime", 0UL);
        state.NextEntryId = Math.Max(1, AI(e, "nextEntryId", 1));
        state.HydrationSlotItem = ItemStack.Empty.Clone();

        XElement slot = e.Element("hydrationSlot");
        if (slot != null)
        {
            ItemStack stack;
            if (!TryDecodeItemStack(slot.Value, out stack))
                throw new InvalidDataException("invalid hydration-slot item payload");
            if (stack != null)
                state.HydrationSlotItem = stack;
        }

        XElement entries = e.Element("ingestionEntries");
        if (entries != null)
        {
            foreach (XElement x in entries.Elements("entry"))
            {
                if (state.IngestionEntries.Count >= PersistenceIngestionSafetyCeiling)
                    throw new InvalidDataException("ingestion entry safety ceiling exceeded");
                RebirthIngestionEntry entry = new RebirthIngestionEntry
                {
                    EntryId = AI(x, "id", 0),
                    Kind = (RebirthIngestionKind)AI(x, "kind", 0),
                    SourceItemName = A(x, "source", string.Empty),
                    LiquidProfileId = A(x, "liquid", string.Empty),
                    DigestionProfileId = A(x, "digestion", string.Empty),
                    FoodSafetyProfileId = A(x, "safety", string.Empty),
                    ContaminationProfileId = A(x, "contamination", string.Empty),
                    IngestedWorldTime = AU(x, "time", 0UL),
                    RemainingFluidVolumeMl = AF(x, "fluidMl", 0f),
                    RemainingSolidVolumeMl = AF(x, "solidMl", 0f),
                    RemainingNutritionUnits = AF(x, "nutrition", 0f),
                    RemainingEnergyUnits = AF(x, "energy", 0f),
                    IntestinalFluidVolumeMl = AF(x, "intestinalFluidMl", 0f),
                    IntestinalSolidVolumeMl = AF(x, "intestinalSolidMl", 0f),
                    IntestinalNutritionUnits = AF(x, "intestinalNutrition", 0f),
                    IntestinalEnergyUnits = AF(x, "intestinalEnergy", 0f),
                    IntestinalFluidResidenceSecondsRemaining = AF(x, "intestinalFluidResidenceSeconds", -1f),
                    IntestinalNutritionResidenceSecondsRemaining = AF(x, "intestinalNutritionResidenceSeconds", -1f),
                    // Pre-v7 saves predate meaningful-meal-at-absorption tracking. Mark in-flight legacy
                    // entries credited so loading a world cannot retroactively create meal history.
                    MeaningfulMealCredited = AB(x, "mealCredited", loadedVersion < 7),
                    FluidHoldSecondsRemaining = AF(x, "fluidHoldSeconds", -1f),
                    SolidHoldSecondsRemaining = AF(x, "solidHoldSeconds", -1f),
                    FluidGastricHalfTimeSecondsSnapshot = AF(x, "fluidHalfTimeSeconds", 0f),
                    FluidIsMealBound = AB(x, "fluidMealBound", false),
                    FluidYieldMultiplierSnapshot = AF(x, "fluidYield", 1f),
                    NutrientYieldMultiplierSnapshot = AF(x, "nutrientYield", 1f),
                    DigestionRateMultiplierSnapshot = AF(x, "digestionRate", 1f),
                    PreparedMealEnergyEfficiencyMultiplier = AF(x, "preparedEnergyEfficiency", 1f),
                    PreparedDrinkSpecialEffectDurationSeconds = AF(x, "preparedDrinkEffectSeconds", 0f),
                    PreparedDrinkSpecialEffectActivated = AB(x, "preparedDrinkEffectActivated", false)
                };
                if (!Enum.IsDefined(typeof(RebirthIngestionKind), entry.Kind))
                    throw new InvalidDataException("invalid ingestion kind=" + ((int)entry.Kind).ToString(CultureInfo.InvariantCulture));
                ValidateFiniteEntry(entry);
                // v1 saves had only a single stomach compartment. Treat those values as
                // stomach contents and give them the new gastric residence defaults rather than
                // instantly absorbing them on load.
                if (entry.FluidHoldSecondsRemaining < 0f)
                {
                    entry.FluidIsMealBound = entry.Kind == RebirthIngestionKind.Food ||
                        (entry.Kind == RebirthIngestionKind.Fluid && entry.RemainingNutritionUnits > 0.001f);
                    entry.FluidHoldSecondsRemaining = entry.FluidIsMealBound
                        ? RebirthMetabolismConfig.MealFluidHoldRealSeconds
                        : RebirthMetabolismConfig.ClearLiquidHoldRealSeconds;
                }
                if (entry.SolidHoldSecondsRemaining < 0f)
                    entry.SolidHoldSecondsRemaining = entry.RemainingSolidVolumeMl > 0.001f
                        ? RebirthMetabolismConfig.GetFoodGastricLagRealSeconds(entry.DigestionProfileId)
                        : 0f;
                if (entry.FluidGastricHalfTimeSecondsSnapshot <= 0f && entry.RemainingFluidVolumeMl > 0.001f)
                    entry.FluidGastricHalfTimeSecondsSnapshot = RebirthMetabolismConfig.GetFluidGastricHalfTimeRealSeconds(entry.FluidIsMealBound);


                // v4 and earlier could pass intestinal material straight through during the same
                // fixed tick. Existing in-flight content is migrated into the new visible residence
                // phase instead of being absorbed immediately on load.
                if (entry.IntestinalFluidResidenceSecondsRemaining < 0f)
                    entry.IntestinalFluidResidenceSecondsRemaining = entry.IntestinalFluidVolumeMl > 0.001f
                        ? RebirthMetabolismConfig.IntestinalFluidResidenceRealSeconds
                        : 0f;
                if (entry.IntestinalNutritionResidenceSecondsRemaining < 0f)
                    entry.IntestinalNutritionResidenceSecondsRemaining =
                        (entry.IntestinalNutritionUnits > 0.001f || entry.IntestinalSolidVolumeMl > 0.001f)
                        ? RebirthMetabolismConfig.IntestinalNutritionResidenceRealSeconds
                        : 0f;

                if (entry.EntryId <= 0)
                    entry.EntryId = state.AllocateEntryId();
                else if (entry.EntryId >= state.NextEntryId)
                    state.NextEntryId = entry.EntryId == int.MaxValue ? 1 : entry.EntryId + 1;
                state.IngestionEntries.Add(entry);
            }
        }

        XElement effects = e.Element("timedEffects");
        if (effects != null)
        {
            foreach (XElement x in effects.Elements("effect"))
                state.TimedEffects.Add(new RebirthMetabolismTimedEffect
                {
                    ProfileId = A(x, "profile", string.Empty),
                    RemainingRealSeconds = AF(x, "remainingRealSeconds", 0f),
                    Stacks = Math.Max(1, AI(x, "stacks", 1))
                });
        }

        state.Energy = UnityEngine.Mathf.Clamp(state.Energy, 0f, RebirthMetabolismConfig.EnergyMax);

        if (state.Version < RebirthMetabolismState.CurrentVersion)
        {
            state.Version = RebirthMetabolismState.CurrentVersion;
            state.Dirty = true;
        }
        else
        {
            state.Dirty = false;
        }
        return state;
    }

    private static string EncodeItemStack(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return string.Empty;
        using (MemoryStream ms = new MemoryStream())
        using (PooledBinaryWriter bw = MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            bw.SetBaseStream(ms);
            bw.Write(stack.count);
            stack.itemValue.Write(bw);
            bw.Flush();
            return Convert.ToBase64String(ms.ToArray());
        }
    }

    private static void ValidateStateForPersistence(RebirthMetabolismState state)
    {
        if (state == null) throw new InvalidDataException("null metabolism state");
        if (float.IsNaN(state.DigestiveHealth) || float.IsInfinity(state.DigestiveHealth) ||
            float.IsNaN(state.Energy) || float.IsInfinity(state.Energy) ||
            float.IsNaN(state.AutoSipCooldownRemainingRealSeconds) || float.IsInfinity(state.AutoSipCooldownRemainingRealSeconds))
            throw new InvalidDataException("non-finite metabolism state value");
        if (state.IngestionEntries.Count > PersistenceIngestionSafetyCeiling)
            throw new InvalidDataException("ingestion entry safety ceiling exceeded");
        for (int i = 0; i < state.IngestionEntries.Count; i++) ValidateFiniteEntry(state.IngestionEntries[i]);
        for (int i = 0; i < state.TimedEffects.Count; i++)
        {
            RebirthMetabolismTimedEffect effect = state.TimedEffects[i];
            if (effect != null && (float.IsNaN(effect.RemainingRealSeconds) || float.IsInfinity(effect.RemainingRealSeconds)))
                throw new InvalidDataException("non-finite timed effect value");
        }
    }

    private static bool TryDecodeItemStack(string encoded, out ItemStack stack)
    {
        stack = ItemStack.Empty.Clone();
        if (string.IsNullOrEmpty(encoded)) return true;
        try
        {
            byte[] data = Convert.FromBase64String(encoded.Trim());
            using (MemoryStream ms = new MemoryStream(data))
            using (PooledBinaryReader br = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                br.SetBaseStream(ms);
                int count = br.ReadInt32();
                ItemValue value = new ItemValue();
                value.Read(br);
                if (ms.Position != ms.Length || value.IsEmpty() || count <= 0)
                    return false;
                stack = new ItemStack(value, count);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateFiniteEntry(RebirthIngestionEntry e)
    {
        if (e == null) throw new InvalidDataException("null ingestion entry");
        float[] values =
        {
            e.RemainingFluidVolumeMl, e.RemainingSolidVolumeMl, e.RemainingNutritionUnits, e.RemainingEnergyUnits,
            e.IntestinalFluidVolumeMl, e.IntestinalSolidVolumeMl, e.IntestinalNutritionUnits, e.IntestinalEnergyUnits,
            e.IntestinalFluidResidenceSecondsRemaining, e.IntestinalNutritionResidenceSecondsRemaining,
            e.FluidHoldSecondsRemaining, e.SolidHoldSecondsRemaining, e.FluidGastricHalfTimeSecondsSnapshot,
            e.FluidYieldMultiplierSnapshot, e.NutrientYieldMultiplierSnapshot, e.DigestionRateMultiplierSnapshot,
            e.PreparedMealEnergyEfficiencyMultiplier, e.PreparedDrinkSpecialEffectDurationSeconds
        };
        for (int i = 0; i < values.Length; i++)
            if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                throw new InvalidDataException("non-finite ingestion entry value");
    }

    private static string PathName
    {
        get
        {
            string root = GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(root) ? string.Empty : Path.Combine(root, "RebirthData", "Metabolism", "players.xml");
        }
    }

    private static string S(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
    private static string A(XElement e, string n, string f) { XAttribute a = e.Attribute(n); return a != null ? a.Value : f; }
    private static int AI(XElement e, string n, int f) { int v; return int.TryParse(A(e,n,null), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : f; }
    private static float AF(XElement e, string n, float f)
    {
        string raw = A(e, n, null);
        if (raw == null) return f;
        float v;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v) || float.IsNaN(v) || float.IsInfinity(v))
            throw new InvalidDataException("invalid finite float attribute " + n);
        return v;
    }
    private static ulong AU(XElement e, string n, ulong f) { ulong v; return ulong.TryParse(A(e,n,null), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : f; }
    private static bool AB(XElement e, string n, bool f) { bool v; return bool.TryParse(A(e,n,null), out v) ? v : f; }
}
