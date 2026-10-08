internal static class ActualHandModeGate
{
    internal static bool Prefix(Hand __instance)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.Allows(__instance);
}
