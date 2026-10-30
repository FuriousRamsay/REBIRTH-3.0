using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

#nullable disable

// Saved native grids carry dimensions independently of loot XML. Apply the
// owner-requested named parity list after persistent reads, on the host only.
internal static class RebirthLootCapacityParity
{
    private static HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

    internal static void Load()
    {
        var loaded = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            string path = Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(), "loot_sizes_26.xml");
            foreach (var operation in XDocument.Load(path).Root.Elements("setattribute"))
            {
                string xpath = (string)operation.Attribute("xpath");
                const string prefix = "/lootcontainers/lootcontainer[@name='";
                if (xpath == null || !xpath.StartsWith(prefix, StringComparison.Ordinal) || !xpath.EndsWith("']", StringComparison.Ordinal)) continue;
                string name = xpath.Substring(prefix.Length, xpath.Length - prefix.Length - 2);
                if (name != "playerBackpack") loaded.Add(name);
            }
        }
        catch (Exception error)
        {
            Log.Error("[REBIRTH Loot] Could not load named 2.6 capacity parity: " + error.Message);
        }
        names = loaded;
    }

    internal static void AfterPersistentRead(TEFeatureStorage storage, StreamModeRead mode)
    {
        if (mode == StreamModeRead.Persistency) ApplyOnHost(storage, true);
    }

    internal static void AfterInit(TEFeatureStorage storage) => ApplyOnHost(storage, false);

    private static void ApplyOnHost(TEFeatureStorage storage, bool markDirty)
    {
        if (storage?.ItemGrid == null || storage.ItemGrid.PlayerOwned ||
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer != true) return;
        // A renamed/shared loot table can serve blocks with different legacy
        // dimensions. The explicit block override changes only the grid, not
        // the loot table/content or the current player-storage size policy.
        string raw;
        bool hasOverride = storage.Parent?.TeData?.Block?.Properties?.Values != null &&
            storage.Parent.TeData.Block.Properties.Values.TryGetValue("RebirthLootContainerSize", out raw);
        if (!hasOverride && !names.Contains(storage.lootListName)) return;
        var definition = LootContainer.GetLootContainer(storage.lootListName);
        if (definition == null) return;
        var size = definition.size;
        if (hasOverride)
        {
            storage.Parent.TeData.Block.Properties.Values.TryGetValue("RebirthLootContainerSize", out raw);
            var parts = raw.Split(',');
            int width, height;
            if (parts.Length != 2 || !int.TryParse(parts[0], out width) || !int.TryParse(parts[1], out height)) return;
            size = new Vector2i(width, height);
        }
        var grid = storage.ItemGrid;
        if (size.x <= 0 || size.y <= 0 || size.x > 20 || size.y > 20 ||
            grid.ContainerSize.x == size.x && grid.ContainerSize.y == size.y) return;
        int capacity = size.x * size.y;
        // Never discard saved items to achieve a smaller legacy grid. Such a
        // container retains its old dimensions until a later safe load.
        for (int slot = capacity; slot < grid.Length; slot++)
            if (grid[slot] != null && !grid[slot].IsEmpty()) return;
        grid.Resize(size);
        if (markDirty) storage.SetModified();
    }
}
