using HarmonyLib;
using System;

// Explicitly installed by bootstrap before native creation. Ordinary actors have no hold.
internal static class RebirthNpcPreparedEventGateInstaller
{
    private static readonly Harmony Harmony=new Harmony("rebirth.npc.prepared-original-events");
    internal static bool IsReady =>
        HasPrefix(typeof(MinEffectController),nameof(MinEffectController.FireEvent),new[]{typeof(MinEventTypes),typeof(MinEventParams)},typeof(RebirthNpcPreparedMinEffectGate)) &&
        HasPrefix(typeof(Entity),nameof(Entity.Update),Type.EmptyTypes,typeof(RebirthNpcPreparedUnityUpdateGate)) &&
        HasPrefix(typeof(Entity),"FixedUpdate",Type.EmptyTypes,typeof(RebirthNpcPreparedFixedUpdateGate)) &&
        HasPrefix(typeof(Hand),nameof(Hand.Reconcile),Type.EmptyTypes,typeof(RebirthNpcPreparedHandReconcileGate)) &&
        HasPrefix(typeof(Hand),nameof(Hand.Equip),new[]{typeof(ItemInventoryData)},typeof(RebirthNpcPreparedHandEquipGate)) &&
        HasPrefix(typeof(Hand),nameof(Hand.SelectHoldingMode),new[]{typeof(Hand.HoldingMode),typeof(float)},typeof(RebirthNpcPreparedHandModeGate)) &&
        HasPrefix(typeof(Hand),nameof(Hand.SelectSlot),new[]{typeof(int),typeof(bool)},typeof(RebirthNpcPreparedHandSlotGate)) &&
        HasPrefix(typeof(QuestEventManager),nameof(QuestEventManager.HeldItem),new[]{typeof(ItemValue)},typeof(RebirthNpcPreparedHeldQuestGate));
    private static bool HasPrefix(Type target,string name,Type[] arguments,Type guard)
    {
        var method=AccessTools.Method(target,name,arguments);
        var prefix=AccessTools.Method(guard,"Prefix");
        if(method==null||prefix==null)return false;
        var info=HarmonyLib.Harmony.GetPatchInfo(method);
        if(info==null)return false;
        foreach(var patch in info.Prefixes)
            if(patch.owner==Harmony.Id && patch.PatchMethod==prefix)return true;
        return false;
    }
    internal static void Install()
    {
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedMinEffectGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedUnityUpdateGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedFixedUpdateGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedHandReconcileGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedHandEquipGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedHandModeGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedHandSlotGate));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony,typeof(RebirthNpcPreparedHeldQuestGate));
        if(!IsReady)throw new InvalidOperationException("Original NPC restoration event guards were not installed.");
    }
}
[HarmonyPatch(typeof(MinEffectController),nameof(MinEffectController.FireEvent),new Type[]{typeof(MinEventTypes),typeof(MinEventParams)})]
internal static class RebirthNpcPreparedMinEffectGate
{
    internal static bool Prefix(MinEventParams _eventParms)
    {
        return !(_eventParms?.Self is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending);
    }
}
[HarmonyPatch(typeof(Entity),nameof(Entity.Update))]
internal static class RebirthNpcPreparedUnityUpdateGate
{
    internal static bool Prefix(Entity __instance)=>!(__instance is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending);
}
[HarmonyPatch(typeof(Entity),"FixedUpdate")]
internal static class RebirthNpcPreparedFixedUpdateGate
{
    internal static bool Prefix(Entity __instance)=>!(__instance is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending);
}[HarmonyPatch(typeof(Hand),nameof(Hand.Reconcile))]
internal static class RebirthNpcPreparedHandReconcileGate
{
    internal static bool Prefix(Hand __instance)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.Allows(__instance);
}
[HarmonyPatch(typeof(Hand),nameof(Hand.Equip),new Type[]{typeof(ItemInventoryData)})]
internal static class RebirthNpcPreparedHandEquipGate
{
    internal static bool Prefix(Hand __instance,ItemInventoryData _held)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.AllowsEquip(__instance,_held);
}
[HarmonyPatch(typeof(Hand),nameof(Hand.SelectHoldingMode),new Type[]{typeof(Hand.HoldingMode),typeof(float)})]
internal static class RebirthNpcPreparedHandModeGate
{
    internal static bool Prefix(Hand __instance)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.Allows(__instance);
}
[HarmonyPatch(typeof(Hand),nameof(Hand.SelectSlot),new Type[]{typeof(int),typeof(bool)})]
internal static class RebirthNpcPreparedHandSlotGate
{
    internal static bool Prefix(Hand __instance)=>!(__instance?.entity is EntityRebirthHumanoidNPC npc&&npc.IsPreparedRestorationPending)||RebirthNpcPreparedHandMaterialization.Allows(__instance);
}
[HarmonyPatch(typeof(QuestEventManager),nameof(QuestEventManager.HeldItem),new Type[]{typeof(ItemValue)})]
internal static class RebirthNpcPreparedHeldQuestGate
{
    internal static bool Prefix(ItemValue newValue)=>!RebirthNpcPreparedHandMaterialization.SuppressesQuest(newValue);
}
