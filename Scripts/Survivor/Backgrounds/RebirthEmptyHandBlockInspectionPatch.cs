// Harmony keys __state by the patch method's declaring type, not its name.
// Keep the inspector's string state separate from workmanship's bool state.
internal static class RebirthEmptyHandBlockInspectionPatch
{
    public static void Prefix(Block __instance, WorldBase __0, BlockValueRef __1, int __4, out string __state)
    {
        RebirthWorkmanshipSignatureService.EmptyHandBlockInspectPrefix(__instance, __0, __1, __4, out __state);
    }

    public static void Postfix(string __state)
    {
        RebirthWorkmanshipSignatureService.EmptyHandBlockInspectPostfix(__state);
    }
}
