using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Read-only projection of the exact ingredient inputs used by ItemActionEntryCraft.hasItems().
/// It mirrors CraftingIngredientCount, selected crafting tier, batch multiplier and the
/// PlayerInventory projection (which REBIRTH Remote Resources already augments).
/// </summary>
public static class RebirthCraftingRequirementProjectionService
{
    public sealed class Requirement
    {
        public ItemStack Ingredient;
        public ItemValue ItemValue;
        public string Name = string.Empty;
        public string Icon = string.Empty;
        public Color32 IconTint = new Color32(255, 255, 255, 255);
        public int Have;
        public int Need;
        public bool HasEnough;
        public bool QualityIngredient;
        public string SourceBreakdown = " ";

        public int Fingerprint
        {
            get
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (ItemValue != null ? ItemValue.type : 0);
                    hash = hash * 31 + Have;
                    hash = hash * 31 + Need;
                    hash = hash * 31 + (HasEnough ? 1 : 0);
                    hash = hash * 31 + (SourceBreakdown != null ? StringComparer.Ordinal.GetHashCode(SourceBreakdown) : 0);
                    return hash;
                }
            }
        }
    }

    public static List<Requirement> Build(XUi xui, Recipe recipe, int craftCount, int craftingTier)
    {
        List<Requirement> result = new List<Requirement>();
        if (xui == null || xui.PlayerInventory == null || xui.playerUI == null ||
            xui.playerUI.entityPlayer == null || recipe == null || recipe.ingredients == null)
            return result;

        EntityPlayerLocal localPlayer = xui.playerUI.entityPlayer;
        int batches = Math.Max(1, craftCount);
        List<ItemStack> allStacks = xui.PlayerInventory.GetAllItemStacks();

        for (int i = 0; i < recipe.ingredients.Count; i++)
        {
            ItemStack source = recipe.ingredients[i];
            if (source == null || source.IsEmpty() || source.itemValue == null || source.itemValue.IsEmpty())
                continue;

            ItemValue itemValue = source.itemValue;
            ItemClass itemClass = itemValue.ItemClass;
            bool quantityValid = RebirthCraftingIngredientQuantity.TryResolveTotal(localPlayer, recipe, source, craftingTier, batches, out int need);
            bool qualityIngredient = itemValue.HasQuality;
            if (qualityIngredient && quantityValid && need == 0) need = batches;

            int have = CountEligibleItems(allStacks, itemValue, qualityIngredient);
            if (!quantityValid) need = int.MaxValue;

            Requirement projection = null;
            for (int existingIndex = 0; existingIndex < result.Count; existingIndex++)
            {
                Requirement existing = result[existingIndex];
                if (existing.QualityIngredient == qualityIngredient && RebirthCraftingIngredientQuantity.SameRecipeIdentity(existing.ItemValue, itemValue))
                {
                    projection = existing;
                    break;
                }
            }
            if (projection != null)
            {
                long combined = (long)projection.Need + need;
                projection.Need = combined > int.MaxValue ? int.MaxValue : (int)combined;
                projection.Have = have;
                projection.HasEnough = projection.Have >= projection.Need;
                projection.SourceBreakdown = BuildSourceBreakdown(xui, localPlayer, projection);
                continue;
            }

            projection = new Requirement
            {
                Ingredient = source,
                ItemValue = itemValue,
                Name = itemClass != null ? itemClass.GetLocalizedItemName() : SafeItemName(itemClass),
                Icon = itemClass != null ? itemValue.GetPropertyOverride("CustomIcon", itemClass.GetIconName()) : string.Empty,
                IconTint = itemClass != null ? itemClass.GetIconTint(itemValue) : new Color32(255, 255, 255, 255),
                Have = have,
                Need = need,
                HasEnough = have >= need,
                QualityIngredient = qualityIngredient
            };
            projection.SourceBreakdown = BuildSourceBreakdown(xui, localPlayer, projection);
            result.Add(projection);
        }

        return result;
    }

    private static int CountEligibleItems(List<ItemStack> stacks, ItemValue required, bool qualityIngredient)
    {
        long count = 0;
        for (int i = 0; stacks != null && i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.IsEmpty() || stack.itemValue == null) continue;
            // Native quality admission matches item type, not quality/wear metadata.
            if (qualityIngredient ? stack.itemValue.type != required.type :
                !RebirthCraftingIngredientQuantity.SameRecipeIdentity(stack.itemValue, required)) continue;
            if (qualityIngredient)
            {
                if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
                count++;
            }
            else count += stack.count;
            if (count >= int.MaxValue) return int.MaxValue;
        }
        return (int)count;
    }

    private static string BuildSourceBreakdown(XUi xui, EntityPlayerLocal player, Requirement requirement)
    {
        if (xui == null || xui.PlayerInventory == null || player == null ||
            requirement == null || requirement.ItemValue == null)
            return " ";

        ItemValue itemValue = requirement.ItemValue;
        string itemName = string.IsNullOrWhiteSpace(requirement.Name) ? Localize("xuiRebirthRequiredMaterial", "Required material") : requirement.Name;
        int backpack = xui.PlayerInventory.Backpack.GetItemCount(itemValue);
        int toolbelt = xui.PlayerInventory.Toolbelt.GetItemCount(itemValue);
        IList<RemoteResourceSourceContribution> sources = GetRemoteSources(player);

        StringBuilder builder = new StringBuilder(256);
        builder.Append("[u]").Append(itemName).Append(" ")
            .Append(Localize("xuiRebirthSourceStockTotals", "Total source stock")).Append(":[/u]");

        if (backpack > 0) builder.Append("\n").Append(Localize("xuiRebirthBackpack", "Backpack")).Append(": ").Append(backpack);
        if (toolbelt > 0) builder.Append("\n").Append(Localize("xuiRebirthToolbelt", "Toolbelt")).Append(": ").Append(toolbelt);

        List<SourceLine> remote = new List<SourceLine>();
        for (int i = 0; sources != null && i < sources.Count; i++)
        {
            RemoteResourceSourceContribution source = sources[i];
            if (source == null || source.Counts == null) continue;
            int count;
            if (!source.Counts.TryGetValue(itemValue.type, out count) || count <= 0) continue;
            remote.Add(new SourceLine
            {
                Name = GetSourceName(source),
                Count = count,
                Distance = Vector3.Distance(player.position, source.Position)
            });
        }
        remote.Sort(delegate(SourceLine a, SourceLine b)
        {
            int distance = a.Distance.CompareTo(b.Distance);
            return distance != 0 ? distance : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < remote.Count; i++)
        {
            SourceLine line = remote[i];
            builder.Append("\n").Append(line.Name).Append(": ").Append(line.Count)
                .Append("  -  ").Append(line.Distance.ToString("0.0", CultureInfo.InvariantCulture)).Append(" m");
        }

        if (backpack <= 0 && toolbelt <= 0 && remote.Count == 0)
            builder.Append("\n").Append(Localize("xuiRebirthNoAvailableSources", "No available sources"));

        // Source provenance carries type totals on remote clients, not exact eligible
        // quality/metadata variants. Do not present these as recipe-usable counts.
        builder.Append("\n\n").Append(string.Format(CultureInfo.CurrentCulture,
            Localize("xuiRebirthSourceUsableCount", "Usable for this recipe: {0}"),requirement.Have));
        builder.Append("\n").Append(Localize("xuiRebirthSourceVariantNote",
            "Stock totals may include items that do not match the required variant."));
        if(requirement.QualityIngredient)
            builder.Append("\n").Append(Localize("xuiRebirthSourceInstalledModsNote",
                "Remove installed mods from equipment before using it as a crafting ingredient."));
        return builder.ToString();
    }

    private static IList<RemoteResourceSourceContribution> GetRemoteSources(EntityPlayerLocal player)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || player == null)
            return new RemoteResourceSourceContribution[0];

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        bool authoritative = connection != null ? connection.IsServer : world != null && !world.IsRemote();
        return authoritative
            ? RemoteResourceSnapshotCache.Get(player).Sources
            : RemoteResourceClientAvailability.GetSources(player);
    }

    private static string GetSourceName(RemoteResourceSourceContribution source)
    {
        if (source != null && !string.IsNullOrWhiteSpace(source.DisplayName))
        {
            string displayName = source.DisplayName.Replace("\r", " ").Replace("\n", " ").Trim();
            if (Localization.Exists(displayName))
            {
                string localized = Localization.Get(displayName);
                if (!string.IsNullOrWhiteSpace(localized)) return localized.Replace("\r", " ").Replace("\n", " ").Trim();
            }
            return displayName;
        }

        if (source == null) return Localize("xuiRebirthStorage", "Storage");
        switch (source.Kind)
        {
            case RemoteResourceSourceKind.WorkstationOutput: return Localize("xuiRebirthWorkstation", "Workstation");
            case RemoteResourceSourceKind.VehicleStorage: return Localize("xuiRebirthVehicle", "Vehicle");
            case RemoteResourceSourceKind.DroneStorage: return Localize("xuiRebirthDrone", "Drone");
            case RemoteResourceSourceKind.NpcStorage: return Localize("xuiRebirthCompanion", "Companion");
            default: return Localize("xuiRebirthStorageContainer", "Storage Container");
        }
    }

    private static string SafeItemName(ItemClass itemClass)
    {
        try { return itemClass != null ? itemClass.GetItemName() : string.Empty; }
        catch { return string.Empty; }
    }

    private static string Localize(string key, string fallback)
    {
        string value = Localization.Get(key ?? string.Empty);
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private sealed class SourceLine
    {
        public string Name;
        public int Count;
        public float Distance;
    }
}
