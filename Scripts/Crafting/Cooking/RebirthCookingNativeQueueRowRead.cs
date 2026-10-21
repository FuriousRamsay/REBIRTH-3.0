using HarmonyLib;

// The native queue reader reuses RecipeQueueItem objects when array lengths
// match. Read() assigns Recipe for a present recipe but leaves the old field
// untouched for an empty row. Clear it before the native full-row read so a
// remote Take/Cancel can actually retire the server's previous recipe.
[HarmonyPatch(typeof(RecipeQueueItem),nameof(RecipeQueueItem.Read))]
internal static class RebirthCookingNativeQueueRowRead
{
    private static void Prefix(RecipeQueueItem __instance)
    {
        __instance.Recipe=null;
    }
}
