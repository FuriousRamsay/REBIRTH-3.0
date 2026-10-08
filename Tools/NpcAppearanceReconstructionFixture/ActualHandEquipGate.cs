internal static class ActualHandEquipGate
{
    internal static bool Prefix(Hand __instance,ItemInventoryData _held)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.AllowsEquip(__instance,_held);
}
