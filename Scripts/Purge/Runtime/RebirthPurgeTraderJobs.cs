using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Offer policy only. Native journals, acceptance, sharing, rally/reset and rewards
// retain their existing owners. In particular, loading an owned quest is not an offer.
internal static class RebirthPurgeTraderJobPolicy
{
    [ThreadStatic] internal static bool RemovingOffer;

    internal static bool Allows(World world, PrefabInstance poi, FastTags<TagGroup.Global> tags)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge) return true;
        try
        {
            if (world == null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world)
                || poi?.prefab == null || !poi.prefab.GetQuestTag(tags)
                || poi.prefab.SleeperVolumeList == null || !poi.prefab.SleeperVolumeList.AnyUsedEntry) return false;
            var decorator = GameManager.Instance.GetDynamicPrefabDecorator();
            if (decorator == null || !ReferenceEquals(decorator.GetPrefab(poi.id), poi)) return false;
            RebirthPoiWorldStore store;
            if (!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)) return false;
            var snapshot = store?.Published;
            if (snapshot == null || !snapshot.Binding.IsCurrent || !ReferenceEquals(snapshot.Binding.Scope, world)) return false;
            var origin = poi.boundingBoxPosition;
            var size = poi.boundingBoxSize;
            var biome = world.GetBiome(origin.x + size.x / 2, origin.z + size.z / 2);
            if (biome == null) return false;
            var identity = new RebirthPoiIdentity(poi.prefab.PrefabName, origin.x, origin.y, origin.z,
                (int)poi.rotation, size.x, size.y, size.z, biome.m_sBiomeName);
            RebirthPoiClearanceRecord record;
            // An unvisited, unrecorded native POI is non-purged. Discovery is not
            // required, and this read never discovers or resets a POI.
            if (!snapshot.TryGet(identity, out record)) return !snapshot.RecordShards.ContainsKey(identity.Key);
            return record.State == RebirthPoiClearanceState.Discovered;
        }
        catch { return false; }
    }

    internal static bool AllowsOffer(World world, Quest quest)
    {
        if (quest == null) return false;
        Vector3 origin = default(Vector3), size = default(Vector3);
        if (!quest.GetPositionData(out origin, Quest.PositionDataTypes.POIPosition)
            || !quest.GetPositionData(out size, Quest.PositionDataTypes.POISize)) return false;
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
            && ((Vector3)poi.boundingBoxSize).Equals(size) && Allows(world, poi, quest.QuestTags);
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
            __result = RebirthPurgeTraderJobPolicy.Allows(GameManager.Instance?.World, prefab, questTag);
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
