using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum QuickStackCategory
{
    None = 0, Ammunition, RangedWeapons, MeleeWeapons, Tools, MedicalSupplies,
    ItemModifiers, Armor, Clothing, Food, Drinks, SeedsAndFarming, VehicleParts,
    BooksAndSchematics, ResourcesAndCraftingMaterials, BuildingBlocks,
    ElectricalAndMechanicalDevices, TrapsAndDefensiveDevices
}

public static class QuickStackRuntimePolicy
{
    private static RebirthQuickStackMode mode = RebirthQuickStackMode.Full;
    private static float radius = RebirthResourceDistancePolicy.QuickStackDefault;
    public static RebirthQuickStackMode Mode { get { return mode; } }
    public static bool Enabled { get { return mode != RebirthQuickStackMode.Off; } }
    public static float Radius { get { return radius; } }
    public static void SetMode(RebirthQuickStackMode value)
    {
        mode = value;
        // The registry is shared with Remote Resources. Disabling only the Quick Stack
        // consumer must not discard storage/workstation registrations needed by crafting,
        // repair, upgrade, paint, or future resource consumers.
        RemoteResourceSnapshotCache.InvalidateAll();
    }

    public static void SetDistance(int value)
    {
        float normalized = RebirthResourceDistancePolicy.NormalizeQuickStack(value);
        if (Math.Abs(radius - normalized) < 0.01f) return;
        radius = normalized;
        RemoteResourceSnapshotCache.InvalidateAll();
        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("runtime radius=" + radius + "m"); }
    }
}

public static class QuickStackDiagnostics
{
    private static bool configuredEnabled;
    public static bool Enabled
    {
        get { return configuredEnabled || QuickStackHotkeyDiagnostics.Enabled; }
        set { configuredEnabled = value; }
    }
    public static void Write(string text)
    {
        if (configuredEnabled || QuickStackHotkeyDiagnostics.ConsumeLine()) Log.Out("[QuickStack] " + text);
    }
}

public static class QuickStackCategoryRegistry
{
    // Category evidence comes from the resolved live ItemClass. Build lazily and keep
    // the result across ordinary previews/transfers: classification is definition data,
    // not inventory state. Clear/InvalidateDefinitions is reserved for definition or
    // policy reload boundaries and explicit diagnostics.
    private static readonly Dictionary<int, QuickStackCategory> cache = new Dictionary<int, QuickStackCategory>();

    public static void Clear()
    {
        cache.Clear();
    }

    public static void InvalidateDefinitions()
    {
        cache.Clear();
    }

    public static QuickStackCategory Get(int type)
    {
        QuickStackCategory category;
        if (cache.TryGetValue(type, out category))
            return category;

        ItemClass item = ItemClass.GetForId(type);
        category = Classify(item);
        cache[type] = category;
        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("category live itemType=" + type +
            " item=" + (item != null ? item.GetItemName() : "<null>") +
            " category=" + category); }
        return category;
    }

    /// <summary>Re-evaluates one item against the live classifier without flushing the
    /// shared cache for every other item. Used by the item-info diagnostics page.</summary>
    public static QuickStackCategory GetFresh(int type)
    {
        cache.Remove(type);
        return Get(type);
    }

    public static string GetDisplayName(QuickStackCategory category)
    {
        string key;
        switch (category)
        {
            case QuickStackCategory.Ammunition: key = "xuiRebirthQuickStackCategoryAmmunition"; break;
            case QuickStackCategory.RangedWeapons: key = "xuiRebirthQuickStackCategoryRangedWeapons"; break;
            case QuickStackCategory.MeleeWeapons: key = "xuiRebirthQuickStackCategoryMeleeWeapons"; break;
            case QuickStackCategory.Tools: key = "xuiRebirthQuickStackCategoryTools"; break;
            case QuickStackCategory.MedicalSupplies: key = "xuiRebirthQuickStackCategoryMedicalSupplies"; break;
            case QuickStackCategory.ItemModifiers: key = "xuiRebirthQuickStackCategoryItemModifiers"; break;
            case QuickStackCategory.Armor: key = "xuiRebirthQuickStackCategoryArmor"; break;
            case QuickStackCategory.Clothing: key = "xuiRebirthQuickStackCategoryClothing"; break;
            case QuickStackCategory.Food: key = "xuiRebirthQuickStackCategoryFood"; break;
            case QuickStackCategory.Drinks: key = "xuiRebirthQuickStackCategoryDrinks"; break;
            case QuickStackCategory.SeedsAndFarming: key = "xuiRebirthQuickStackCategorySeedsAndFarming"; break;
            case QuickStackCategory.VehicleParts: key = "xuiRebirthQuickStackCategoryVehicleParts"; break;
            case QuickStackCategory.BooksAndSchematics: key = "xuiRebirthQuickStackCategoryBooksAndSchematics"; break;
            case QuickStackCategory.ResourcesAndCraftingMaterials: key = "xuiRebirthQuickStackCategoryResourcesAndCraftingMaterials"; break;
            case QuickStackCategory.BuildingBlocks: key = "xuiRebirthQuickStackCategoryBuildingBlocks"; break;
            case QuickStackCategory.ElectricalAndMechanicalDevices: key = "xuiRebirthQuickStackCategoryElectricalAndMechanicalDevices"; break;
            case QuickStackCategory.TrapsAndDefensiveDevices: key = "xuiRebirthQuickStackCategoryTrapsAndDefensiveDevices"; break;
            default: return Localization.Get("xuiRebirthQuickStackCategoryUnassigned");
        }
        return Localization.Get(key);
    }

    private static QuickStackCategory Classify(ItemClass item)
    {
        if (item == null)
            return QuickStackCategory.None;

        string name = (item.GetItemName() ?? string.Empty).ToLowerInvariant();
        string groups = item.Groups == null ? string.Empty : string.Join("/", item.Groups).ToLowerInvariant();
        string skills = ((item.ActionSkillGroup ?? string.Empty) + "/" + (item.CraftingSkillGroup ?? string.Empty)).ToLowerInvariant();
        string tags = item.ItemTags.ToString().ToLowerInvariant();
        // Classification must be locale-independent. Display/localized text is presentation,
        // never routing evidence.
        string evidence = name + "/" + groups + "/" + skills + "/" + tags;

        // Resolved ItemClass data already includes XML inheritance. Order is intentional.
        // Authored knowledge items are a hard override because their names/tags commonly
        // contain the thing they teach (tool/ammo/medical/vehicle). They must never be
        // routed as that taught item merely because a broader substring matched first.
        if (ContainsAny(evidence, "schematic", "skillbook", "perkbook", "magazine") ||
            name.StartsWith("book", StringComparison.Ordinal))
            return QuickStackCategory.BooksAndSchematics;
        if (ContainsAny(evidence, "ammo", "ammunition", "bullet", "arrow", "crossbowbolt", "rocketammo", "fuelammo"))
            return QuickStackCategory.Ammunition;
        if (item.Actions != null && item.Actions.Length > 0 && item.Actions[0] is ItemActionRanged)
            return QuickStackCategory.RangedWeapons;
        if (ContainsAny(evidence, "melee", "club", "spear", "machete", "knife", "baton", "knuckles"))
            return QuickStackCategory.MeleeWeapons;
        if (ContainsAny(evidence, "tool", "wrench", "ratchet", "impactdriver", "pickaxe", "shovel", "chainsaw", "auger"))
            return QuickStackCategory.Tools;
        // 3.1's medicalFirstAidKit is explicitly in Group=Medical and carries
        // medical/medicalHigh tags. The name prefix is also authoritative fallback
        // evidence, so the kit cannot be lost if a tag/group string changes shape.
        if (name.StartsWith("medical", StringComparison.Ordinal) ||
            ContainsAny(evidence, "medical", "medicine", "bandage", "firstaid", "first aid", "antibiotic", "splint", "cast", "vitamin"))
            return QuickStackCategory.MedicalSupplies;
        if (ContainsAny(evidence, "itemmodifier", "modifier", "weaponmod", "armormod", "toolmod") || name.StartsWith("mod", StringComparison.Ordinal))
            return QuickStackCategory.ItemModifiers;
        // 3.1 outfits/undergarments can have a localized name that does not say
        // "armor" even though the resolved item class is ItemClassArmor. Use the
        // authoritative runtime type before falling back to name/tag evidence.
        if (item is ItemClassArmor || ContainsAny(evidence, "armor", "armour"))
            return QuickStackCategory.Armor;
        if (ContainsAny(evidence, "clothing", "apparel", "outfit"))
            return QuickStackCategory.Clothing;
        if (ContainsAny(evidence, "seed", "farming", "crop", "fertilizer", "farmplot"))
            return QuickStackCategory.SeedsAndFarming;
        if (ContainsAny(evidence, "drink", "beverage", "water", "coffee", "tea", "juice"))
            return QuickStackCategory.Drinks;
        if (ContainsAny(evidence, "food", "meal") || (item.Actions != null && item.Actions.Length > 0 && item.Actions[0] is ItemActionEat))
            return QuickStackCategory.Food;
        if (ContainsAny(evidence, "vehiclepart", "vehicle", "chassis", "handlebars", "wheel", "battery", "engine"))
            return QuickStackCategory.VehicleParts;
        if (ContainsAny(evidence, "electrical", "mechanical", "generator", "relay", "switch", "wiretool", "motion sensor"))
            return QuickStackCategory.ElectricalAndMechanicalDevices;
        if (ContainsAny(evidence, "trap", "defensive", "turret", "landmine", "blade trap", "dart trap", "electricfence"))
            return QuickStackCategory.TrapsAndDefensiveDevices;
        if (item.IsBlock())
            return QuickStackCategory.BuildingBlocks;
        if (item.IsResourceUnit || ContainsAny(evidence, "resource", "craftingmaterial", "ingredient", "ore", "scrap"))
            return QuickStackCategory.ResourcesAndCraftingMaterials;

        return QuickStackCategory.None;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (text.IndexOf(values[i], StringComparison.Ordinal) >= 0)
                return true;
        return false;
    }
}

public sealed class QuickStackContainerEntry
{
    public Vector3i Position;
    public QuickStackContainerEntry(Vector3i p) { Position = p; }
}

public static class QuickStackContainerRegistry
{
    // Compatibility facade retained for the existing Quick Stack planner. The actual
    // index is shared with Remote Resources and uses spatial buckets rather than a
    // full scan of every loaded container on each request.
    public static void Clear() { RemoteResourceRegistry.Clear(); }
    public static void Register(TileEntity te)
    {
        if (QuickStackRuntimePolicy.Enabled) RemoteResourceRegistry.Register(te);
    }
    public static void Unregister(TileEntity te) { RemoteResourceRegistry.Unregister(te); }
    public static List<QuickStackContainerEntry> Query(World world, Vector3 position, float radius)
    {
        List<RemoteResourceRegistration> registrations = RemoteResourceRegistry.Query(
            position, radius, RemoteResourceSourceKind.StaticContainer);
        List<QuickStackContainerEntry> result = new List<QuickStackContainerEntry>(registrations.Count);
        HashSet<Vector3i> seen = new HashSet<Vector3i>();

        for (int i = 0; i < registrations.Count; i++)
        {
            Vector3i candidate = registrations[i].Position;
            if (seen.Add(candidate)) result.Add(new QuickStackContainerEntry(candidate));
        }

        // Category assignment is an explicit Quick Stack opt-in. Do not require a
        // player deposit or a prior RemoteResourceRegistry lifecycle event before
        // that container can appear in Quick Stack discovery.
        List<Vector3i> categoryPositions =
            QuickStackAcceptedCategoryRegistry.QueryPositions(world, position, radius);
        for (int i = 0; i < categoryPositions.Count; i++)
        {
            Vector3i candidate = categoryPositions[i];
            if (seen.Add(candidate)) result.Add(new QuickStackContainerEntry(candidate));
        }

        result.Sort(delegate(QuickStackContainerEntry a, QuickStackContainerEntry b)
        {
            int distance = (a.Position.ToVector3() - position).sqrMagnitude.CompareTo(
                (b.Position.ToVector3() - position).sqrMagnitude);
            if (distance != 0) return distance;
            int x = a.Position.x.CompareTo(b.Position.x); if (x != 0) return x;
            int y = a.Position.y.CompareTo(b.Position.y); if (y != 0) return y;
            return a.Position.z.CompareTo(b.Position.z);
        });
        return result;
    }
}

public sealed class QuickStackTransferPlanner
{
    private enum OwnerScope
    {
        OwnedByCurrentPlayer,
        NotOwnedByCurrentPlayer
    }

    public sealed class Destination
    {
        public Vector3i Position;
        public bool Category;
        public bool RequireOwned;
        public bool RequireNotOwned;
    }

    public sealed class Plan
    {
        public readonly List<Destination> Destinations = new List<Destination>();
        public bool CategoryPass;
        public QuickStackCategory Category;
    }

    public static Plan Find(World world, EntityPlayer player, ItemStack source, bool owned, RebirthQuickStackMode mode)
    {
        Plan result = new Plan();
        if (world == null || player == null || source == null || source.IsEmpty())
            return result;

        if (QuickStackDiagnostics.Enabled)
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("planner start itemType=" + source.itemValue.type +
                " item='" + (ItemClass.GetForId(source.itemValue.type) != null ? ItemClass.GetForId(source.itemValue.type).GetItemName() : "<null>") +
                "' count=" + source.count + " owned=" + owned + " mode=" + mode); }

        List<QuickStackContainerEntry> candidates = QuickStackContainerRegistry.Query(
            world, player.position, QuickStackService.Radius);

        QuickStackCategory category = mode == RebirthQuickStackMode.Full
            ? QuickStackCategoryRegistry.Get(source.itemValue.type)
            : QuickStackCategory.None;
        result.Category = category;
        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("category assigned to item type=" + source.itemValue.type + " category=" + category); }

        // Priority is intentionally global, not per-container:
        //   1. current player's containers: top up existing stacks
        //   2. current player's containers: category destinations for the remainder
        //   3. all other accessible containers (other-player or unowned): existing stacks
        //   4. those other containers: category destinations for the remainder
        // DepositOwned stops after the first two tiers.
        AppendPass(result, world, player, source, candidates,
            OwnerScope.OwnedByCurrentPlayer, false, QuickStackCategory.None);
        if (category != QuickStackCategory.None)
            AppendPass(result, world, player, source, candidates,
                OwnerScope.OwnedByCurrentPlayer, true, category);

        if (!owned)
        {
            AppendPass(result, world, player, source, candidates,
                OwnerScope.NotOwnedByCurrentPlayer, false, QuickStackCategory.None);
            if (category != QuickStackCategory.None)
                AppendPass(result, world, player, source, candidates,
                    OwnerScope.NotOwnedByCurrentPlayer, true, category);
        }

        bool hasExact = false;
        bool hasCategory = false;
        for (int i = 0; i < result.Destinations.Count; i++)
        {
            if (result.Destinations[i].Category) hasCategory = true;
            else hasExact = true;
        }
        // Preserve the legacy diagnostic flag: true only for a category-only plan.
        result.CategoryPass = hasCategory && !hasExact;
        return result;
    }

    public static bool IsOwnedByPlayer(TileEntity te, EntityPlayer player)
    {
        TileEntityComposite composite = te as TileEntityComposite;
        if (composite == null || composite.Owner == null || player == null)
            return false;

        PlatformUserIdentifierAbs userId;
        return RemoteResourceAccess.TryGetPersistentId(player, out userId) &&
               userId != null && composite.Owner.Equals(userId);
    }

    private static void AppendPass(
        Plan result,
        World world,
        EntityPlayer player,
        ItemStack source,
        List<QuickStackContainerEntry> candidates,
        OwnerScope ownerScope,
        bool categoryPass,
        QuickStackCategory category)
    {
        bool requireOwned = ownerScope == OwnerScope.OwnedByCurrentPlayer;
        bool requireNotOwned = ownerScope == OwnerScope.NotOwnedByCurrentPlayer;

        for (int i = 0; i < candidates.Count; i++)
        {
            TileEntity te = world.GetTileEntity(candidates[i].Position);
            TEFeatureStorage loot;
            string rejection;
            if (!QuickStackService.CanUse(world, player, te, requireOwned, out loot, out rejection))
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("container rejected pos=" + candidates[i].Position + " reason=" + rejection); }
                continue;
            }

            if (requireNotOwned && IsOwnedByPlayer(te, player))
                continue;

            bool qualifies = categoryPass &&
                QuickStackAcceptedCategoryRegistry.Accepts(candidates[i].Position, category);
            bool capacity = false;
            ItemStack[] slots = loot.ItemGrid.items;
            PackedBoolArray destinationLocks = loot.ItemGrid.SlotLocks;

            for (int s = 0; s < slots.Length; s++)
            {
                bool lockedDestination = destinationLocks != null &&
                    s < destinationLocks.Length && destinationLocks[s];
                ItemStack slot = slots[s];

                if (slot == null || slot.IsEmpty())
                {
                    // Existing-stack passes never create a new stack. Empty capacity only
                    // matters to the category phase, and protected empties stay protected.
                    if (categoryPass && !lockedDestination)
                        capacity = true;
                    continue;
                }

                int stackAmount;
                bool canTopUp = slot.CanStackPartlyWith(source, out stackAmount) && stackAmount > 0;
                if (!categoryPass)
                {
                    if (canTopUp)
                    {
                        qualifies = true;
                        capacity = true;
                    }
                    continue;
                }

                // Category routing is explicit: only the container's configured
                // Quick Stack categories qualify this phase. Existing contents of a
                // different item in the same category do not implicitly opt it in.
                if (canTopUp)
                    capacity = true;
            }

            if (QuickStackDiagnostics.Enabled)
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("planner candidate pos=" + candidates[i].Position +
                    " ownerScope=" + ownerScope +
                    " pass=" + (categoryPass ? "category" : "exact") +
                    " category=" + category + " assigned=[" +
                    string.Join(",", QuickStackAcceptedCategoryRegistry.Get(candidates[i].Position)) +
                    "] qualifies=" + qualifies + " capacity=" + capacity); }

            if (!qualifies || !capacity)
                continue;

            result.Destinations.Add(new Destination
            {
                Position = candidates[i].Position,
                Category = categoryPass,
                RequireOwned = requireOwned,
                RequireNotOwned = requireNotOwned
            });
        }
    }
}

public static class QuickStackTransferExecutor
{
    /// <summary>
    /// Moves an arbitrary source stack into the live Quick Stack destinations while
    /// revalidating authorization and capacity at commit time. This is the common
    /// destination path used by backpack, vehicle, drone, and NPC-source Quick Stack.
    /// The caller owns the source inventory mutation; this method mutates only the
    /// destination containers and returns the remaining source stack.
    /// </summary>
    public static int CommitExternal(
        World world,
        EntityPlayer player,
        ItemStack source,
        QuickStackTransferPlanner.Plan plan,
        bool owned,
        out ItemStack remaining)
    {
        remaining = source != null ? source.Clone() : ItemStack.Empty;
        if (world == null || player == null || source == null || source.IsEmpty() || plan == null)
            return 0;

        int moved = 0;
        HashSet<Vector3i> changedDestinations = new HashSet<Vector3i>();

        for (int d = 0; d < plan.Destinations.Count && remaining.count > 0; d++)
        {
            QuickStackTransferPlanner.Destination destination = plan.Destinations[d];
            TileEntity te = world.GetTileEntity(destination.Position);
            TEFeatureStorage loot;
            string rejection;
            if (!QuickStackService.CanUse(world, player, te, destination.RequireOwned, out loot, out rejection))
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer revalidation failure reason=" + rejection + " pos=" + destination.Position); }
                continue;
            }
            if (destination.RequireNotOwned && QuickStackTransferPlanner.IsOwnedByPlayer(te, player))
            {
                { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer revalidation failure reason=destination ownership changed to current player pos=" + destination.Position); }
                continue;
            }

            ItemStack[] live = loot.ItemGrid.items;
            PackedBoolArray destinationLocks = loot.ItemGrid.SlotLocks;
            int movedBeforeDestination = moved;

            // Existing protected stacks are valid stack targets. Protection prevents
            // removal and prevents creating a new stack in a protected empty slot.
            for (int i = 0; i < live.Length && remaining.count > 0; i++)
            {
                if (live[i] == null || live[i].IsEmpty()) continue;
                int amount;
                if (!live[i].CanStackPartlyWith(remaining, out amount) || amount <= 0) continue;
                ItemStack after = live[i].Clone();
                after.count += amount;
                loot.UpdateSlot(i, after);
                remaining.count -= amount;
                moved += amount;
            }

            // Exact passes only top up existing stacks. Creating a new stack is a
            // category-routing operation and therefore happens only on category passes.
            if (destination.Category)
            {
                ItemClass itemClass = ItemClass.GetForId(remaining.itemValue.type);
                int maxStack = itemClass != null ? itemClass.Stacknumber.Value : 1;
                for (int i = 0; i < live.Length && remaining.count > 0; i++)
                {
                    if (destinationLocks != null && i < destinationLocks.Length && destinationLocks[i]) continue;
                    if (live[i] != null && !live[i].IsEmpty()) continue;
                    int amount = Math.Min(maxStack, remaining.count);
                    loot.UpdateSlot(i, new ItemStack(remaining.itemValue.Clone(), amount));
                    remaining.count -= amount;
                    moved += amount;
                }
            }

            if (moved > movedBeforeDestination) changedDestinations.Add(destination.Position);
        }

        if (moved > 0)
        {
            foreach (Vector3i position in changedDestinations)
                RemoteResourceStateStore.Activate(RemoteResourceIdentity.Static(position));
        }
        return moved;
    }

    public static int Commit(World world, EntityPlayer player, int bagSlot, QuickStackTransferPlanner.Plan plan, bool owned)
    {
        ItemStack[] bag = player.bag.ItemGrid.items;
        if (bagSlot < 0 || bagSlot >= bag.Length || bag[bagSlot] == null || bag[bagSlot].IsEmpty() || plan == null)
            return 0;

        ItemStack remaining;
        int moved = CommitExternal(world, player, bag[bagSlot], plan, owned, out remaining);
        if (moved > 0)
        {
            player.bag.SetSlot(bagSlot, remaining.count > 0 ? remaining : ItemStack.Empty);
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer committed slot=" + bagSlot + " moved=" + moved + " destinations=" + plan.Destinations.Count); }
        }
        return moved;
    }
}

public static class QuickStackService
{
    public static float Radius { get { return QuickStackRuntimePolicy.Radius; } }
    public static void RequestLocal(bool owned)
    {
        if (!QuickStackRuntimePolicy.Enabled) return;
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c.IsServer) ProcessServer(GameManager.Instance.World, player.entityId, persistent.PrimaryId, owned);
        else c.SendToServer(NetPackageManager.GetPackage<NetPackageQuickStackRequest>().Setup(player.entityId, persistent, owned));
    }
    public static void ProcessServer(World world, int playerId, PlatformUserIdentifierAbs userId, bool owned)
    {
        Stopwatch sw = QuickStackDiagnostics.Enabled ? Stopwatch.StartNew() : null;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData pp = GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId);
        RebirthQuickStackMode mode = RebirthSandboxOptionManager.Current.QuickStack;
        if (player == null || pp == null || pp.PrimaryId == null || userId == null || !pp.PrimaryId.Equals(userId) || mode == RebirthQuickStackMode.Off) return;
        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("request received player=" + playerId + " owned=" + owned); }
        { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("selected mode=" + mode); }
        ItemStack[] bag = player.bag.ItemGrid.items; PackedBoolArray locked = player.bag.LockedSlots;
        int moved = 0, unmoved = 0;
        for (int i = 0; i < bag.Length; i++)
        {
            if (locked != null && i < locked.Length && locked[i]) { { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("skipped protected slot=" + i); } continue; }
            if (bag[i] == null || bag[i].IsEmpty()) continue;
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("eligible backpack slot=" + i); }
            QuickStackTransferPlanner.Plan plan = QuickStackTransferPlanner.Find(world, player, bag[i], owned, mode);
            if (plan == null || plan.Destinations.Count == 0) { unmoved += bag[i].count; continue; }
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write((plan.CategoryPass ? "category destinations selected count=" : "exact destinations selected count=") + plan.Destinations.Count); }
            { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("transfer planned slot=" + i + " destinations=" + plan.Destinations.Count + " category=" + plan.CategoryPass); }
            int count = QuickStackTransferExecutor.Commit(world, player, i, plan, owned);
            moved += count; if (!bag[i].IsEmpty()) unmoved += bag[i].count;
        }
        if (sw != null) { sw.Stop(); if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("moved item count=" + moved + " unmoved item count=" + unmoved + " elapsedMs=" + sw.ElapsedMilliseconds); }
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection != null && !connection.IsSinglePlayer)
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageQuickStackResult>().Setup(moved, unmoved, sw.ElapsedMilliseconds), _attachedToEntityId: playerId);
    }
    public static bool CanUse(World world, EntityPlayer player, TileEntity te, bool owned, out TEFeatureStorage loot, out string reason)
    {
        loot = null;
        reason = "invalid";
        if (world == null || player == null || te == null) return false;
        if (te.IsUserAccessing() || RemoteResourceAccess.IsServerBusy(te))
        {
            reason = "busy container skipped";
            return false;
        }
        if (!te.TryGetSelfOrFeature<TEFeatureStorage>(out loot) || loot.ItemGrid.items == null)
        {
            reason = "unsupported storage";
            return false;
        }
        TileEntityComposite composite = te as TileEntityComposite;
        if (composite == null)
        {
            reason = "unsupported non-composite storage";
            return false;
        }
        if (!RemoteResourceAccess.AuthorizeComposite(composite, player, owned, out reason)) return false;
        string stableId = RemoteResourceIdentity.Static(te.ToWorldPos());
        PlatformUserIdentifierAbs userId;
        if (!RemoteResourceAccess.TryGetPersistentId(player, out userId))
        {
            reason = "missing persistent player";
            return false;
        }
        if (RemoteResourceStateStore.IsExcluded(stableId, userId))
        {
            reason = "excluded from remote resources for this player";
            return false;
        }
        reason = string.Empty;
        return true;
    }

}

[Preserve]
public sealed class NetPackageQuickStackRequest : NetPackage
{
    private int playerId; private PlatformUserIdentifierAbs userId; private bool owned;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageQuickStackRequest Setup(int id, PersistentPlayerData pp, bool own) { playerId=id; userId=pp.PrimaryId; owned=own; return this; }
    public override void read(PooledBinaryReader r) { BinaryReader b=(BinaryReader)r; playerId=b.ReadInt32(); userId=PlatformUserIdentifierAbs.FromStream(b); owned=b.ReadBoolean(); }
    public override void write(PooledBinaryWriter w) { base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write(playerId); userId.ToStream(b); b.Write(owned); }
    public override void ProcessPackage(World world, GameManager callbacks) { if (world != null && ValidEntityIdForSender(playerId) && ValidUserIdForSender(userId) && !world.IsRemote()) QuickStackService.ProcessServer(world, playerId, userId, owned); }
    public int GetLength() { return 32; }
}


[Preserve]
public sealed class NetPackageQuickStackResult : NetPackage
{
    private int moved; private int unmoved; private long elapsedMs;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageQuickStackResult Setup(int m, int u, long e) { moved=m; unmoved=u; elapsedMs=e; return this; }
    public override void read(PooledBinaryReader r) { BinaryReader b=(BinaryReader)r; moved=b.ReadInt32(); unmoved=b.ReadInt32(); elapsedMs=b.ReadInt64(); }
    public override void write(PooledBinaryWriter w) { base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write(moved); b.Write(unmoved); b.Write(elapsedMs); }
    public override void ProcessPackage(World world, GameManager callbacks) { { if (QuickStackDiagnostics.Enabled) QuickStackDiagnostics.Write("server result moved=" + moved + " unmoved=" + unmoved + " elapsedMs=" + elapsedMs); } }
    public int GetLength() { return 20; }
}

public static class QuickStackPatchInstaller
{
    private static bool installed;
    public static void Install()
    {
        if (installed) return; installed=true;
        Harmony h = new Harmony("rebirth.quickstack.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(h, typeof(QuickStackBackpackInitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h, typeof(QuickStackBackpackBindingPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h, typeof(RebirthQuickStackCategoriesRadialAtlasPatch));
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), nameof(XUiC_BackpackWindow.Init))]
internal static class QuickStackBackpackInitPatch
{
    private static void Postfix(XUiC_BackpackWindow __instance)
    {
        XUiController quickStack = __instance.GetChildById("btnRebirthQuickStack");
        if (quickStack != null)
            quickStack.OnPress += delegate { QuickStackRadialUiService.Open(__instance.xui); };

        XUiController library = __instance.GetChildById("btnRebirthBackpackLibrary");
        if (library != null)
            library.OnPress += delegate { __instance.xui.playerUI.windowManager.Open("rebirthBackpackLibrary", true); };
        XUiController companions = __instance.GetChildById("btnRebirthCompanions");
        if (companions != null)
            companions.OnPress += delegate { RebirthCompanionUiService.Open(__instance.xui); };
    }
}

[HarmonyPatch(typeof(XUiC_BackpackWindow), nameof(XUiC_BackpackWindow.GetBindingValueInternal))]
internal static class QuickStackBackpackBindingPatch
{
    private static void Postfix(string bindingName, ref string value, ref bool __result)
    {
        if (bindingName != "rebirth_quick_stack_enabled")
            return;

        value = QuickStackRuntimePolicy.Enabled.ToString();
        __result = true;
    }
}

[Preserve]
public sealed class RebirthQuickStackModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthSandboxMenuPatchInstaller.Install();
        RemoteResourcePatchInstaller.Install();
        RemoteResourceAccessEventPatchInstaller.Install();
        RemoteResourceConsumerPatchInstaller.Install();
        RemoteResourceClientTransactionPatchInstaller.Install();
        RemoteResourceActionConsumerPatchInstaller.Install();
        QuickStackPatchInstaller.Install();
        RebirthCompanionService.Install();
        RebirthSandboxOptionManager.Current.ApplyRuntimeState();
    }
}
