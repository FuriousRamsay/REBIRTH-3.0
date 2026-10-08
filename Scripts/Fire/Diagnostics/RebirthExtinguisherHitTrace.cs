#if DEBUG
using System;

#nullable disable

public static class RebirthExtinguisherHitTrace
{
    private static readonly object Sync = new object();

    public static bool Active { get; private set; }
    public static long HitCalls { get; private set; }
    public static long BlockTerrainCalls { get; private set; }
    public static long SuppressionMatched { get; private set; }
    public static long SuppressionMissed { get; private set; }
    public static long DamagingAmmoMatches { get; private set; }
    public static long MagazineAmmoMatches { get; private set; }

    public static string LastHitTag { get; private set; }
    public static int LastAttackerEntityId { get; private set; }
    public static string LastHeldItem { get; private set; }
    public static string LastHeldAction { get; private set; }
    public static int LastSelectedAmmoIndex { get; private set; }
    public static string LastMagazineNames { get; private set; }
    public static string LastDamagingItem { get; private set; }
    public static string LastResolvedAmmo { get; private set; }
    public static string LastResolutionSource { get; private set; }
    public static bool LastPropertyPresent { get; private set; }
    public static bool LastPropertyValue { get; private set; }
    public static float LastIncomingBlockDamage { get; private set; }
    public static float LastOutgoingBlockDamage { get; private set; }
    public static int LastIncomingFlags { get; private set; }
    public static int LastOutgoingFlags { get; private set; }
    public static string LastFailureReason { get; private set; }

    public static void Begin()
    {
        lock (Sync)
        {
            Active = true;
            HitCalls = 0;
            BlockTerrainCalls = 0;
            SuppressionMatched = 0;
            SuppressionMissed = 0;
            DamagingAmmoMatches = 0;
            MagazineAmmoMatches = 0;
            LastHitTag = "<none>";
            LastAttackerEntityId = -1;
            LastHeldItem = "<none>";
            LastHeldAction = "<none>";
            LastSelectedAmmoIndex = -1;
            LastMagazineNames = "<none>";
            LastDamagingItem = "<none>";
            LastResolvedAmmo = "<none>";
            LastResolutionSource = "<none>";
            LastPropertyPresent = false;
            LastPropertyValue = false;
            LastIncomingBlockDamage = 0f;
            LastOutgoingBlockDamage = 0f;
            LastIncomingFlags = 0;
            LastOutgoingFlags = 0;
            LastFailureReason = "<none>";
        }
    }

    public static void End()
    {
        lock (Sync) Active = false;
    }

    public static void Record(
        string hitTag,
        int attackerEntityId,
        string heldItem,
        string heldAction,
        int selectedAmmoIndex,
        string magazineNames,
        string damagingItem,
        string resolvedAmmo,
        string resolutionSource,
        bool propertyPresent,
        bool propertyValue,
        float incomingBlockDamage,
        float outgoingBlockDamage,
        int incomingFlags,
        int outgoingFlags,
        bool isBlockTerrain,
        bool suppressed,
        string failureReason)
    {
        if (!Active)
            return;

        lock (Sync)
        {
            HitCalls++;
            if (isBlockTerrain)
                BlockTerrainCalls++;
            if (suppressed)
                SuppressionMatched++;
            else
                SuppressionMissed++;
            if (suppressed && string.Equals(resolutionSource, "damagingItemValue", StringComparison.Ordinal))
                DamagingAmmoMatches++;
            if (suppressed && string.Equals(resolutionSource, "selectedMagazine", StringComparison.Ordinal))
                MagazineAmmoMatches++;

            LastHitTag = hitTag ?? "<null>";
            LastAttackerEntityId = attackerEntityId;
            LastHeldItem = heldItem ?? "<null>";
            LastHeldAction = heldAction ?? "<null>";
            LastSelectedAmmoIndex = selectedAmmoIndex;
            LastMagazineNames = magazineNames ?? "<null>";
            LastDamagingItem = damagingItem ?? "<null>";
            LastResolvedAmmo = resolvedAmmo ?? "<null>";
            LastResolutionSource = resolutionSource ?? "<null>";
            LastPropertyPresent = propertyPresent;
            LastPropertyValue = propertyValue;
            LastIncomingBlockDamage = incomingBlockDamage;
            LastOutgoingBlockDamage = outgoingBlockDamage;
            LastIncomingFlags = incomingFlags;
            LastOutgoingFlags = outgoingFlags;
            LastFailureReason = failureReason ?? "<null>";
        }
    }
    public static void RecordDedicatedActionHit(
        WorldRayHitInfo hitInfo,
        EntityAlive holdingEntity,
        ItemActionRanged action,
        ItemValue heldItemValue)
    {
        if (!Active)
            return;

        string heldItem = heldItemValue != null && heldItemValue.ItemClass != null
            ? heldItemValue.ItemClass.Name
            : "<none>";
        string actionName = action != null ? action.GetType().FullName : "<none>";
        string ammoName = "<none>";
        int ammoIndex = -1;
        string magazineNames = "<none>";
        bool propertyPresent = false;
        bool propertyValue = false;

        try
        {
            if (action != null && action.MagazineItemNames != null)
            {
                magazineNames = string.Join(",", action.MagazineItemNames);
                ammoIndex = heldItemValue != null ? heldItemValue.SelectedAmmoTypeIndex : -1;
                if (ammoIndex >= 0 && ammoIndex < action.MagazineItemNames.Length)
                {
                    ammoName = action.MagazineItemNames[ammoIndex];
                    ItemClass ammoClass = ItemClass.GetItemClass(ammoName);
                    if (ammoClass != null)
                    {
                        propertyPresent = ammoClass.Properties.Contains("SuppressHitSound");
                        propertyValue = propertyPresent && ammoClass.Properties.GetBool("SuppressHitSound");
                    }
                }
            }
        }
        catch (Exception)
        {
        }

        Record(
            hitInfo != null ? hitInfo.tag : "<null>",
            holdingEntity != null ? holdingEntity.entityId : -1,
            heldItem,
            actionName,
            ammoIndex,
            magazineNames,
            "<not-used>",
            ammoName,
            "dedicatedExtinguisherAction",
            propertyPresent,
            propertyValue,
            0f,
            0f,
            0,
            8,
            hitInfo != null && hitInfo.tag != null && GameUtils.IsBlockOrTerrain(hitInfo.tag),
            true,
            "native ItemActionAttack.Hit deliberately bypassed");
    }

    public static void RecordDedicatedActionMiss(EntityAlive holdingEntity, ItemActionRanged action)
    {
        if (!Active)
            return;
        Record(
            "<miss>",
            holdingEntity != null ? holdingEntity.entityId : -1,
            holdingEntity != null && holdingEntity.inventory != null && holdingEntity.inventory.holdingItemItemValue != null && holdingEntity.inventory.holdingItemItemValue.ItemClass != null
                ? holdingEntity.inventory.holdingItemItemValue.ItemClass.Name
                : "<none>",
            action != null ? action.GetType().FullName : "<none>",
            -1,
            "<none>",
            "<not-used>",
            "<none>",
            "dedicatedExtinguisherAction",
            false,
            false,
            0f,
            0f,
            0,
            8,
            false,
            false,
            "ray miss");
    }

}
#endif
