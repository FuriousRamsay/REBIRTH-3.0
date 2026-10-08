using System;

// Read-only, exact-call discovery evaluation. Native Recipe.IsUnlocked still decides native flags.
// Only the existing REBIRTH presentation gate recognizes this scope; no recipe flags are changed.
internal static class RebirthStationDiscoveryScope
{
    private sealed class Context
    {
        internal Recipe Recipe;
        internal EntityPlayer Player;
        internal object World;
        internal Context Parent;
    }
    [ThreadStatic] private static Context current;

    internal static bool Matches(Recipe recipe, EntityPlayer player)
    {
        Context active = current;
        return active != null && recipe != null && player != null
            && ReferenceEquals(active.Recipe, recipe) && ReferenceEquals(active.Player, player)
            && ReferenceEquals(active.World, player.world);
    }

    internal static bool IsUnlockedForDiscovery(Recipe recipe, EntityPlayer player)
    {
        if (recipe == null || player == null) return false;
        var context = new Context { Recipe = recipe, Player = player, World = player.world, Parent = current };
        current = context;
        try
        {
            bool unlocked = recipe.IsUnlocked(player);
            return unlocked && ReferenceEquals(context.World, player.world);
        }
        finally { current = context.Parent; }
    }
}
