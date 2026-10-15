public static class RebirthCamperCraftingStation
{
    public sealed class Presentation
    {
        public readonly string InstalledBlockName;
        public readonly string RecipeWorkstationName;
        public readonly string WindowName;
        internal Presentation(string block, string recipes, string window)
        {
            InstalledBlockName = block;
            RecipeWorkstationName = recipes;
            WindowName = window;
        }
    }
    public const int ModuleSlots = 20;
    public const int Columns = 5;
    public const int Rows = ModuleSlots / Columns;

    // Resolve the installed item's actual block, including inherited workstation configuration.
    // This validates a module only; it neither places a world block nor opens a station window.
    public static bool TryResolve(ItemStack module, out BlockValue blockValue, out WorkstationData data)
    {
        blockValue = default(BlockValue);
        data = null;
        if (module == null || module.IsEmpty() || module.count != 1 || module.itemValue == null)
            return false;
        ItemClassBlock item = module.itemValue.ItemClass as ItemClassBlock;
        if (item == null) return false;
        BlockValue candidate = item.GetBlockValueFromItemValue(module.itemValue);
        BlockWorkstation station = candidate.Block as BlockWorkstation;
        if (station == null) return false;
        WorkstationData configuration = CraftingManager.GetWorkstationData(station.GetBlockName());
        // Native OnLockResponseLocal defaults an empty override to workstation_<block name>.
        // Standard forge/workbench definitions therefore do not require an explicit override.
        if (configuration == null) return false;
        blockValue = candidate;
        data = configuration;
        return true;
    }

    // Match native window selection and the existing REBIRTH recipe-family alias.
    // This descriptor does not open a window, grant access or alter global registries.
    public static bool TryGetPresentation(ItemStack module, out Presentation presentation)
    {
        presentation = null;
        BlockValue block;
        WorkstationData data;
        if (!TryResolve(module, out block, out data)) return false;
        string blockName = block.Block.GetBlockName();
        string window = string.IsNullOrEmpty(data.WorkstationWindow)
            ? "workstation_" + blockName : data.WorkstationWindow;
        if (blockName == "forge" && XUiM_Recipes.DisableSmelter) window += "_nosmelting";
        string recipes = blockName;
        string alias = block.Block.Properties.GetString("RebirthCraftingStation");
        if (!string.IsNullOrEmpty(alias) && CraftingManager.GetWorkstationData(alias) != null) recipes = alias;
        presentation = new Presentation(blockName, recipes, window);
        return true;
    }
}
