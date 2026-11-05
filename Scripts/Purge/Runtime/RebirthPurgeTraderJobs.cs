using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Offer policy only. Native journals, acceptance, sharing, rally/reset and rewards
// retain their existing owners. In particular, loading an owned quest is not an offer.
internal static class RebirthPurgeTraderJobPolicy
{
    [ThreadStatic] internal static bool RemovingOffer;
    private static readonly Dictionary<string, int> rejections = new Dictionary<string, int>();
    private static bool Reject(string reason)
    {
        int count;
        rejections.TryGetValue(reason, out count);
        rejections[reason] = count + 1;
        return false;
    }

    // Reported only for an empty list, which hides the native Jobs response entirely.
    internal static void LogListing(EntityTrader trader, int generated, int offered)
    {
        if (offered > 0) { rejections.Clear(); return; }
        var reasons = new List<string>();
        foreach (var rejection in rejections) reasons.Add(rejection.Key + "=" + rejection.Value);
        rejections.Clear();
        Log.Warning("[REBIRTH Purge Jobs] trader=" + trader?.entityId + " generated=" + generated + " offered=" + offered +
            " rejected=[" + string.Join(",", reasons) + "]");
    }

    // World.GetBiome reads only loaded chunks, and job POIs are usually far from the
    // player. Use the authored world map, like the Purge POI census.
    private static BiomeDefinition BiomeAt(World world, int x, int z)
    {
        var provider = world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        return provider?.GetBiomeProvider()?.GetBiomeAt(x, z);
    }

    internal static bool Allows(World world, PrefabInstance poi, FastTags<TagGroup.Global> tags, Vector3 traderPosition)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        try
        {
            if (world == null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world)) return Reject("world");
            if (poi?.prefab == null || !poi.prefab.GetQuestTag(tags)
                || poi.prefab.SleeperVolumeList == null || !poi.prefab.SleeperVolumeList.AnyUsedEntry) return Reject("not-questable");
            var decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null || !ReferenceEquals(decorator.GetPrefab(poi.id), poi)) return Reject("not-decorator-instance");
            RebirthPoiWorldStore store;
            if (!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)) return Reject("store-unavailable");
            var snapshot = store?.Published;
            if (snapshot == null || !snapshot.Binding.IsCurrent || !ReferenceEquals(snapshot.Binding.Scope, world)) return Reject("snapshot-stale");
            var origin = poi.boundingBoxPosition;
            var size = poi.boundingBoxSize;
            var biome = BiomeAt(world, origin.x + size.x / 2, origin.z + size.z / 2);
            if (biome == null) return Reject("biome");
            var traderBiome = BiomeAt(world, (int)traderPosition.x, (int)traderPosition.z);
            if (traderBiome == null) return Reject("trader-biome");
            if (biome.m_sBiomeName != traderBiome.m_sBiomeName) return Reject("other-biome");
            var identity = new RebirthPoiIdentity(poi.prefab.PrefabName, origin.x, origin.y, origin.z,
                (int)poi.rotation, size.x, size.y, size.z, biome.m_sBiomeName);
            RebirthPoiClearanceRecord record;
            // An unvisited, unrecorded native POI is non-purged. Discovery is not
            // required, and this read never discovers or resets a POI.
            if (!snapshot.TryGet(identity, out record)) return !snapshot.RecordShards.ContainsKey(identity.Key) || Reject("record-unreadable");
            return record.State == RebirthPoiClearanceState.Discovered || Reject("cleared");
        }
        catch (Exception ex) { return Reject("exception:" + ex.GetType().Name); }
    }

    internal static bool AllowsOffer(World world, Quest quest)
    {
        if (quest == null) return false;
        Vector3 origin = default(Vector3), size = default(Vector3);
        if (!quest.GetPositionData(out origin, Quest.PositionDataTypes.POIPosition)
            || !quest.GetPositionData(out size, Quest.PositionDataTypes.POISize)) return false;
        Vector3 traderPosition;
        if (!quest.GetPositionData(out traderPosition, Quest.PositionDataTypes.TraderPosition)) return false;
        var poi = quest.QuestPrefab;
        // Server-generated offers normally retain their exact native instance.
        // A reconstructed cache must resolve the complete native origin and size,
        // not the localized POI name or a point in an overlapping bounding box.
        if (poi == null)
        {
            var candidates = GameManager.Instance?.GetDynamicPrefabDecorator()?.GetPrefabsFromWorldPosInside(
                origin + size * .5f, quest.QuestTags);
            if (candidates == null) return false;
            foreach (var candidate in candidates)
                if (candidate != null && ((Vector3)candidate.boundingBoxPosition).Equals(origin)
                    && ((Vector3)candidate.boundingBoxSize).Equals(size))
                {
                    if (poi != null) return false;
                    poi = candidate;
                }
        }
        return poi != null && ((Vector3)poi.boundingBoxPosition).Equals(origin)
            && ((Vector3)poi.boundingBoxSize).Equals(size) && Allows(world, poi, quest.QuestTags, traderPosition);
    }

    internal static void Filter(World world, List<Quest> offers)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || world == null || world.IsRemote() || offers == null) return;
        for (int i = offers.Count - 1; i >= 0; i--)
            if (!AllowsOffer(world, offers[i])) offers.RemoveAt(i);
    }
}

// Native selection keeps trying its remaining candidates when this additional
// predicate rejects one; tags, difficulty, biome, lockouts and player history stay native.
[HarmonyPatch(typeof(DynamicPrefabDecorator), "ValidPrefabForQuest")]
internal static class RebirthPurgeQuestCandidateHook
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EntityTrader trader, PrefabInstance prefab, FastTags<TagGroup.Global> questTag, ref bool __result)
    {
        if (__result && trader != null && RebirthSandboxOptionManager.Current.IsPurge)
            __result = RebirthPurgeTraderJobPolicy.Allows(GameManager.Instance?.World, prefab, questTag,
                trader.traderArea != null ? (Vector3)trader.traderArea.Position : trader.position);
    }
}

// Purge offers are not random: walk the same native trader POI lists nearest-first. All native
// validity checks stay in ValidPrefabForQuest, and the caller's used list yields the next POI.
[HarmonyPatch(typeof(DynamicPrefabDecorator), nameof(DynamicPrefabDecorator.GetRandomPOINearTrader))]
internal static class RebirthPurgeNearestPoiHook
{
    private static World cachedWorld;
    private static readonly Dictionary<TraderArea, Dictionary<byte, List<PrefabInstance>>> nearest =
        new Dictionary<TraderArea, Dictionary<byte, List<PrefabInstance>>>();

    private static List<PrefabInstance> Nearest(TraderArea area, byte difficulty)
    {
        var world = GameManager.Instance?.World;
        if (!ReferenceEquals(cachedWorld, world)) { cachedWorld = world; nearest.Clear(); }
        Dictionary<byte, List<PrefabInstance>> byDifficulty;
        if (!nearest.TryGetValue(area, out byDifficulty)) nearest[area] = byDifficulty = new Dictionary<byte, List<PrefabInstance>>();
        List<PrefabInstance> sorted;
        if (byDifficulty.TryGetValue(difficulty, out sorted)) return sorted;
        sorted = new List<PrefabInstance>();
        for (int band = 0; band < 3; band++)
        {
            // A null random leaves the native list unshuffled.
            var prefabs = QuestEventManager.Current.GetPrefabsForTrader(area, difficulty, band, null);
            if (prefabs != null) sorted.AddRange(prefabs);
        }
        Vector2 center = new Vector2(area.Position.x, area.Position.z);
        Func<PrefabInstance, float> distance = poi => (new Vector2(
            poi.boundingBoxPosition.x + poi.boundingBoxSize.x / 2f,
            poi.boundingBoxPosition.z + poi.boundingBoxSize.z / 2f) - center).sqrMagnitude;
        sorted.Sort((a, b) => distance(a).CompareTo(distance(b)));
        byDifficulty[difficulty] = sorted;
        return sorted;
    }

    private static bool Prefix(EntityTrader trader, FastTags<TagGroup.Global> questTag, byte difficulty,
        List<Vector2> usedPOILocations, int entityIDforQuests, BiomeFilterTypes biomeFilterType, string biomeFilter, ref PrefabInstance __result)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || trader?.traderArea == null || QuestEventManager.Current == null) return true;
        __result = null;
        foreach (var poi in Nearest(trader.traderArea, difficulty))
            if (DynamicPrefabDecorator.ValidPrefabForQuest(trader, poi, questTag, usedPOILocations, entityIDforQuests, biomeFilterType, biomeFilter))
            {
                __result = poi;
                break;
            }
        return false;
    }
}

[HarmonyPatch(typeof(EntityTrader), nameof(EntityTrader.PopulateActiveQuests))]
internal static class RebirthPurgeTraderGenerationHook
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref List<Quest> __result)
    {
        RebirthPurgeTraderJobPolicy.Filter(GameManager.Instance?.World, __result);
    }
}

[HarmonyPatch(typeof(QuestEventManager), nameof(QuestEventManager.GetQuestList),
    new Type[] { typeof(World), typeof(int), typeof(int) })]
internal static class RebirthPurgeCachedTraderListingHook
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(World __0, ref List<Quest> __result)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || __0 == null || __0.IsRemote()
            || RebirthPurgeTraderJobPolicy.RemovingOffer) return;
        RebirthPurgeTraderJobPolicy.Filter(__0, __result);
        // Native callers regenerate and replace this player's cache on null.
        // This also recovers saved empty caches from the previous jobs-disabled policy.
        if (__result != null && __result.Count == 0) __result = null;
    }
}

// RemoveQuest addresses the last delivered list by tier/index. Re-filtering it
// during removal could consume a different offer when another POI just cleared.
[HarmonyPatch(typeof(NetPackageNPCQuestList), nameof(NetPackageNPCQuestList.ProcessPackage))]
internal static class RebirthPurgeQuestRemovalScopeHook
{
    private static void Prefix(NetPackageNPCQuestList __instance, out bool __state)
    {
        __state = RebirthPurgeTraderJobPolicy.RemovingOffer;
        if (__instance.eventType == NetPackageNPCQuestList.NPCQuestEventTypes.RemoveQuest)
            RebirthPurgeTraderJobPolicy.RemovingOffer = true;
    }
    private static Exception Finalizer(Exception __exception, bool __state)
    {
        RebirthPurgeTraderJobPolicy.RemovingOffer = __state;
        return __exception;
    }
}
