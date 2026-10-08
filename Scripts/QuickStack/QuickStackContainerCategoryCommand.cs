using System;

#nullable disable

public static class QuickStackContainerCategoryCommand
{
    public const string CommandName = "quickstackcategories";

    public static BlockActivationCommand[] Append(BlockActivationCommand[] source, WorldBase world, Vector3i pos, EntityAlive focusing)
    {
        BlockActivationCommand[] original = source ?? BlockActivationCommand.Empty;
        EntityPlayer player = focusing as EntityPlayer;
        World realWorld = world as World;
        TileEntity te = QuickStackAcceptedCategoryRegistry.ResolveTileEntity(world, pos);
        string reason;
        bool enabled = player != null && realWorld != null && te != null &&
            QuickStackCategoryUiService.CanManageContainer(
                realWorld, player, te, out reason);

        for (int i = 0; i < original.Length; i++)
        {
            if (string.Equals(original[i].text, CommandName, StringComparison.OrdinalIgnoreCase))
            {
                original[i].enabled = enabled;
                return original;
            }
        }

        BlockActivationCommand[] result = new BlockActivationCommand[original.Length + 1];
        Array.Copy(original, result, original.Length);
        result[original.Length] = new BlockActivationCommand(CommandName, "quickstack_categories", enabled);
        return result;
    }

    public static bool TryHandle(string command, WorldBase world, Vector3i pos, EntityPlayerLocal player)
    {
        if (!string.Equals(command, CommandName, StringComparison.OrdinalIgnoreCase)) return false;
        if (player == null || world == null) return true;
        QuickStackCategoryUiService.OpenForContainer(player.PlayerUI.xui, pos);
        return true;
    }
}
