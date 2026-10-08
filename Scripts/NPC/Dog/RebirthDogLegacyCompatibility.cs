using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// 2.6 CustomCompanionInventory compatibility contract after the retired
/// Companion Inventory sandbox option was removed in v247. Physical companion
/// storage remains enabled, and legacy auto-loot keeps the former default
/// (Always) behavior instead of depending on the removed sandbox enum.
/// </summary>
public static class RebirthDogInventoryPolicy
{
    private static readonly FastTags<TagGroup.Global> GeneticsBlueprint = FastTags<TagGroup.Global>.Parse("geneticsBlueprint");

    // v247 intentionally retired the companion-inventory sandbox enum and reserved its
    // old sandbox-code slot. Do not reintroduce that option here. The previous
    // default was Always, so preserving that default means both capabilities
    // remain enabled while old encoded option values are ignored by v247+.
    public static bool InventoryEnabled { get { return true; } }
    public static bool AutoLootEnabled { get { return true; } }

    public static bool IsDogAutoLootItem(ItemValue item)
    {
        ItemClass cls = item?.ItemClass;
        if (cls == null) return false;
        string n = cls.Name ?? string.Empty;
        return cls.HasAnyTags(GeneticsBlueprint) ||
               n == "resourceCloth" || n == "resourceLeather" ||
               n == "apparelRunningShoesHP" || n == "apparelCoatJacketLetterZU" ||
               n == "foodRottingFlesh";
    }

    /// <summary>
    /// Port of the 2.6 eligible-loot routing primitive. Callers that know an item is
    /// being awarded by the NPC/zombie-loot path may offer it here before the player inventory.
    /// </summary>
    public static bool TrySelectAutoRouteTarget(EntityPlayer owner, ItemStack stack, out RebirthNpcStableId targetId, out uint expectedRevision)
    {
        targetId = default(RebirthNpcStableId); expectedRevision = 0;
        if (!AutoLootEnabled || owner == null || stack == null || stack.IsEmpty() || !IsDogAutoLootItem(stack.itemValue)) return false;
        string ownerId; if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId)) return false;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (!RebirthDogStateService.IsDog(r) || r.Dog == null || r.Ownership == null || r.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
                !string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            int eid; if (!RebirthNpcRuntimeRegistry.TryGetEntityId(r.Identity.StableNpcId, out eid)) continue;
            EntityRebirthDogCompanion dog = owner.world?.GetEntity(eid) as EntityRebirthDogCompanion;
            if (dog == null || !dog.enabled || RebirthDogRuntimeService.IsUnavailable(dog)) continue;
            RebirthDogInventorySnapshot snapshot = RebirthDogInventoryService.GetSnapshot(r.Identity.StableNpcId);
            if (!RebirthDogInventoryService.CanAcceptAll(r.Identity.StableNpcId, stack)) continue;
            targetId = r.Identity.StableNpcId; expectedRevision = snapshot.Revision; return expectedRevision != 0;
        }
        return false;
    }

    public static bool TryAutoRouteEligibleLoot(EntityPlayer owner, ItemStack stack)
    {
        if (!AutoLootEnabled || owner == null || stack == null || stack.IsEmpty() || !IsDogAutoLootItem(stack.itemValue)) return false;
        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId)) return false;
        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView r = records[i];
            if (!RebirthDogStateService.IsDog(r) || r.Dog == null || r.Ownership == null ||
                r.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
                !string.Equals(r.Ownership.OwnerPlatformIdOrPersistentPlayerId, ownerId, StringComparison.OrdinalIgnoreCase)) continue;
            int eid;
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(r.Identity.StableNpcId, out eid)) continue;
            EntityRebirthDogCompanion dog = owner.world?.GetEntity(eid) as EntityRebirthDogCompanion;
            if (dog == null || !dog.enabled || RebirthDogRuntimeService.IsUnavailable(dog)) continue;
            // 2.6 routing is all-or-nothing from the caller's perspective. Preflight
            // the complete stack so a partial dog insert can never be followed by a
            // normal player award of the original count.
            if (!RebirthDogInventoryService.CanAcceptAll(r.Identity.StableNpcId, stack)) continue;
            int added;
            if (RebirthDogInventoryService.TryAdd(r.Identity.StableNpcId, stack, out added) && added == stack.count) return true;
        }
        return false;
    }
}

public static class RebirthDogResistanceService
{
    private sealed class Mapping
    {
        public readonly string Item, Buff, Effect;
        public Mapping(string item, string buff, string effect) { Item=item; Buff=buff; Effect=effect; }
    }
    private static readonly Mapping[] Mappings = {
        new Mapping("FuriousRamsayModFireResist","FuriousRamsayResistFire","FuriousRamsayAddFireBuffEffect"),
        new Mapping("FuriousRamsayModShockResist","FuriousRamsayResistShock","FuriousRamsayAddShockBuffEffect"),
        new Mapping("FuriousRamsayModSmokeResist","FuriousRamsayResistSmoke","FuriousRamsayAddSmokeBuffEffect"),
        new Mapping("FuriousRamsayModFireShockResist","FuriousRamsayResistFireShock","FuriousRamsayAddFireShockBuffEffect"),
        new Mapping("FuriousRamsayModFireShockSmokeResist","FuriousRamsayResistFireShockSmoke","FuriousRamsayAddFireShockSmokeBuffEffect")
    };

    public static void Synchronize(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null || dog.Buffs == null) return;
        RebirthDogInventorySnapshot inv = RebirthDogInventoryService.GetSnapshot(dog.RebirthRuntimeState.StableId);
        for (int m=0;m<Mappings.Length;m++)
        {
            bool found=false;
            for (int i=0;i<inv.Slots.Length;i++)
            {
                ItemStack s=inv.Slots[i];
                if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && string.Equals(s.itemValue.ItemClass.Name, Mappings[m].Item, StringComparison.OrdinalIgnoreCase)) { found=true; break; }
            }
            SyncBuff(dog, Mappings[m].Buff, found);
            SyncBuff(dog, Mappings[m].Effect, found);
        }
    }
    private static void SyncBuff(EntityRebirthDogCompanion dog, string buff, bool wanted)
    {
        try
        {
            if (BuffManager.GetBuff(buff) == null) return;
            if (wanted && !dog.Buffs.HasBuff(buff)) dog.Buffs.AddBuff(buff);
            else if (!wanted && dog.Buffs.HasBuff(buff)) dog.Buffs.RemoveBuff(buff);
        }
        catch { }
    }
}

/// <summary>2.6 owner-vehicle contact guard, constrained to owned dog companions.</summary>
public static class RebirthDogVehicleContactGuard
{
    private const float Window = 1.25f;
    private const float DistanceSq = 25f;
    private static readonly Dictionary<int,float> ExitTime = new Dictionary<int,float>();
    private static readonly Dictionary<int,Vector3> ExitPos = new Dictionary<int,Vector3>();

    public static void Register(Entity entity, Entity before)
    {
        if (!(entity is EntityPlayer) || !(before is EntityVehicle)) return;
        ExitTime[entity.entityId]=Time.time; ExitPos[entity.entityId]=entity.position;
    }

    public static bool ShouldSuppress(EntityRebirthDogCompanion dog, DamageSource source)
    {
        if (dog == null || source == null || dog.RebirthRuntimeState == null || dog.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player) return false;
        EnumDamageTypes t=source.GetDamageType();
        if (t == EnumDamageTypes.Falling) return true;
        if (t != EnumDamageTypes.Bashing && t != EnumDamageTypes.Crushing && t != EnumDamageTypes.None && t != EnumDamageTypes.VehicleInside) return false;
        Entity src=dog.world?.GetEntity(source.getEntityId());
        if (src is EntityVehicle || src?.AttachedToEntity is EntityVehicle) return true;
        EntityPlayer owner=RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, dog.RebirthRuntimeState.OwnerId);
        if (owner == null) return false;
        float when; Vector3 where;
        return ExitTime.TryGetValue(owner.entityId,out when) && ExitPos.TryGetValue(owner.entityId,out where) && Time.time-when <= Window &&
               (dog.position-where).sqrMagnitude <= DistanceSq;
    }
}


/// <summary>
/// Modern 3.1 adapter for the authoritative 2.6 dog/vehicle PlayOneShot guard.
/// The 2.6 implementation suppressed the contact-generated null/swoosh/attack/hurt
/// clips for an owned dog within 3m of a vehicle, except while it had a live
/// non-player combat target. Keep those same bounded conditions without reviving
/// the retired 2.6 global option variables.
/// </summary>
public static class RebirthDogVehicleAudioGuard
{
    private const float VehicleBlockSoundSuppressRadius = 3.0f;
    private static readonly List<Entity> VehicleScan = new List<Entity>();

    public static bool ShouldSuppress(EntityRebirthDogCompanion dog, string clipName)
    {
        if (!IsOwnedDog(dog)) return false;
        if (!IsVehicleBlockSound(dog, clipName)) return false;
        if (HasLiveNonPlayerAttackTarget(dog)) return false;
        return HasNearbyVehicle(dog, VehicleBlockSoundSuppressRadius);
    }

    private static bool IsOwnedDog(EntityRebirthDogCompanion dog)
    {
        RebirthNpcRuntimeState state = dog != null ? dog.RebirthRuntimeState : null;
        return state != null && state.OwnershipKind == RebirthNpcOwnershipKind.Player;
    }

    private static bool IsVehicleBlockSound(EntityRebirthDogCompanion dog, string clipName)
    {
        // Authoritative 2.6 behavior treated an empty clip plus the two dog contact
        // animation sounds and the configured hurt sounds as suppressible candidates.
        if (string.IsNullOrEmpty(clipName)) return true;
        if (string.Equals(clipName, "swoosh", StringComparison.Ordinal) ||
            string.Equals(clipName, "zombiedogattack", StringComparison.Ordinal))
            return true;

        return (!string.IsNullOrEmpty(dog.soundHurt) && string.Equals(clipName, dog.soundHurt, StringComparison.Ordinal)) ||
               (!string.IsNullOrEmpty(dog.soundHurtSmall) && string.Equals(clipName, dog.soundHurtSmall, StringComparison.Ordinal));
    }

    private static bool HasLiveNonPlayerAttackTarget(EntityRebirthDogCompanion dog)
    {
        EntityAlive target = dog != null ? dog.GetAttackTarget() : null;
        return target != null && target.IsAlive() && !(target is EntityPlayer);
    }

    private static bool HasNearbyVehicle(EntityRebirthDogCompanion dog, float radius)
    {
        if (dog == null || dog.world == null) return false;

        VehicleScan.Clear();
        try
        {
            Bounds bounds = new Bounds(dog.position, new Vector3(radius * 2f, radius * 2f, radius * 2f));
            dog.world.GetEntitiesInBounds(typeof(EntityVehicle), bounds, VehicleScan);

            float radiusSq = radius * radius;
            Vector3 dogPos = dog.position;
            for (int i = 0; i < VehicleScan.Count; i++)
            {
                Entity vehicle = VehicleScan[i];
                if (vehicle == null) continue;

                Vector3 delta = vehicle.position - dogPos;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }
        finally
        {
            VehicleScan.Clear();
        }
    }
}

[HarmonyPatch(typeof(Entity), "Detach")]
public static class Harmony_RebirthDogVehicleDetachGuard
{
    private static void Prefix(Entity __instance, ref Entity __state) { __state = __instance != null ? __instance.AttachedToEntity : null; }
    private static void Postfix(Entity __instance, Entity __state) { RebirthDogVehicleContactGuard.Register(__instance, __state); }
}
