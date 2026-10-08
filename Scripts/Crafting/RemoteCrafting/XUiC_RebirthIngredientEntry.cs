using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Ingredient row that exposes the provenance already retained by Remote Resources through
/// the game's normal cursor tooltip. It never discovers/scans sources itself: authoritative
/// hosts read the existing snapshot cache and dedicated clients read the provenance replicated
/// with their existing availability projection.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthIngredientEntry : XUiC_IngredientEntry
{
    [PublicizedFrom(EAccessModifier.Protected)]
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        if (bindingName == "rebirthsourcebreakdown")
        {
            value = BuildSourceBreakdown();
            return true;
        }

        return base.GetBindingValueInternal(ref value, bindingName);
    }

    private string BuildSourceBreakdown()
    {
        // Keep the binding non-empty before the repeat row receives an ingredient. The count
        // cell also explicitly enables hover in XML, so its collider exists from InitView.
        ItemStack ingredientStack = Ingredient;
        if (ingredientStack == null || ingredientStack.IsEmpty() || ingredientStack.itemValue == null ||
            ingredientStack.itemValue.IsEmpty() || xui == null || xui.playerUI == null ||
            xui.PlayerInventory == null)
            return " ";

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player == null) return " ";

        ItemValue itemValue = ingredientStack.itemValue;
        string itemName = GetIngredientDisplayName(itemValue);
        string workstationLabel;
        int workstationCount;
        if (TryGetDisplayedWorkstationPool(itemValue, out workstationLabel, out workstationCount))
        {
            StringBuilder workstationTip = new StringBuilder(128);
            workstationTip.Append("[u]").Append(itemName).Append(" Source:[/u]")
                .Append("\n\n").Append(workstationLabel).Append(": ").Append(workstationCount);
            return workstationTip.ToString();
        }

        // Mirror vanilla XUiM_PlayerInventory.GetItemCount(ItemValue) *without calling that
        // patched method*, otherwise the aggregate Remote Resources amount would be counted a
        // second time and could not be broken down by source.
        int backpackCount = xui.PlayerInventory.Backpack.GetItemCount(itemValue);
        int toolbeltCount = xui.PlayerInventory.Toolbelt.GetItemCount(itemValue);
        List<SourceLine> remoteLines = BuildRemoteLines(player, itemValue);

        StringBuilder builder = new StringBuilder(256);
        builder.Append("[u]").Append(itemName).Append(" Source:[/u]").Append("\n");

        if (backpackCount > 0)
            builder.Append("\nBackpack: ").Append(backpackCount);
        if (toolbeltCount > 0)
            builder.Append("\nToolbelt: ").Append(toolbeltCount);

        for (int i = 0; i < remoteLines.Count; i++)
        {
            SourceLine line = remoteLines[i];
            builder.Append("\n")
                .Append(line.Name)
                .Append(": ")
                .Append(line.Count)
                .Append("  -  ")
                .Append(line.Distance.ToString("0.0"))
                .Append(" m");
        }

        if (backpackCount <= 0 && toolbeltCount <= 0 && remoteLines.Count == 0)
            builder.Append("\nNo available sources");
        return builder.ToString();
    }

    private string GetIngredientDisplayName(ItemValue itemValue)
    {
        if (materialBased)
        {
            string key = "lbl" + material;
            if (Localization.Exists(key)) return Localization.Get(key);
            return string.IsNullOrEmpty(material) ? "Ingredient" : material.UppercaseFirst();
        }

        ItemClass itemClass = itemValue != null ? itemValue.ItemClass : null;
        string itemName = itemClass != null ? itemClass.GetLocalizedItemName() : string.Empty;
        return string.IsNullOrWhiteSpace(itemName) ? "Ingredient" : itemName;
    }

    /// <summary>
    /// Mirrors XUiC_IngredientEntry's exact source selection for the number visible in the
    /// Count cell. At a forge/material recipe the displayed pool is the material grid. At a
    /// normal workstation it is the workstation input grid. Otherwise the displayed pool is
    /// player inventory (which Rebirth augments with Remote Resources).
    /// </summary>
    private bool TryGetDisplayedWorkstationPool(ItemValue itemValue, out string label, out int count)
    {
        label = string.Empty;
        count = 0;
        if (windowGroup == null || windowGroup.Controller == null) return false;

        XUiC_WorkstationMaterialInputGrid materialGrid =
            windowGroup.Controller.GetChildByType<XUiC_WorkstationMaterialInputGrid>();
        if (materialGrid != null)
        {
            if (!materialBased) return false;
            label = "Workstation Input";
            count = materialGrid.GetWeight(material);
            return true;
        }

        XUiC_WorkstationInputGrid inputGrid =
            windowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>();
        if (inputGrid == null) return false;

        label = "Workstation Input";
        count = inputGrid.GetItemCount(itemValue);
        return true;
    }

    private static List<SourceLine> BuildRemoteLines(EntityPlayerLocal player, ItemValue itemValue)
    {
        List<SourceLine> result = new List<SourceLine>();
        if (!RemoteResourcesRuntimePolicy.Enabled || player == null || itemValue == null || itemValue.IsEmpty())
            return result;

        IList<RemoteResourceSourceContribution> sources;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        bool authoritative = connection != null ? connection.IsServer : world != null && !world.IsRemote();
        if (authoritative)
            sources = RemoteResourceSnapshotCache.Get(player).Sources;
        else
            sources = RemoteResourceClientAvailability.GetSources(player);

        for (int i = 0; sources != null && i < sources.Count; i++)
        {
            RemoteResourceSourceContribution source = sources[i];
            if (source == null || source.Counts == null) continue;

            int count;
            if (!source.Counts.TryGetValue(itemValue.type, out count) || count <= 0) continue;

            result.Add(new SourceLine
            {
                Name = GetSourceName(source),
                Count = count,
                Distance = Vector3.Distance(player.position, source.Position)
            });
        }

        result.Sort(delegate(SourceLine a, SourceLine b)
        {
            int distance = a.Distance.CompareTo(b.Distance);
            return distance != 0 ? distance : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    private static string GetSourceName(RemoteResourceSourceContribution source)
    {
        if (source != null && !string.IsNullOrWhiteSpace(source.DisplayName))
        {
            // Mobile sources may expose the entity-class localization key (for example
            // vehicleBicycle) as EntityName. Resolve that key here on the viewing client so
            // dedicated servers do not bake their own language into the replicated source
            // provenance. Custom source names normally have no localization entry and pass
            // through unchanged.
            string displayName = source.DisplayName.Replace("\r", " ").Replace("\n", " ").Trim();
            if (Localization.Exists(displayName))
            {
                string localized = Localization.Get(displayName);
                if (!string.IsNullOrWhiteSpace(localized))
                    return localized.Replace("\r", " ").Replace("\n", " ").Trim();
            }
            return displayName;
        }

        if (source == null) return "Storage";
        switch (source.Kind)
        {
            case RemoteResourceSourceKind.WorkstationOutput: return "Workstation";
            case RemoteResourceSourceKind.VehicleStorage: return "Vehicle";
            case RemoteResourceSourceKind.DroneStorage: return "Drone";
            case RemoteResourceSourceKind.NpcStorage: return "Companion";
            default: return "Storage Container";
        }
    }

    private sealed class SourceLine
    {
        public string Name;
        public int Count;
        public float Distance;
    }
}
