using HarmonyLib;

#nullable disable

// Native AddItem calls ReturnItem before empty backpack placement. Loose stones are
// crafting resources despite being throwable: a remembered hotbar slot must not win.
[HarmonyPatch(typeof(Inventory),nameof(Inventory.ReturnItem),new[]{typeof(ItemStack)})]
public static class RebirthLooseStoneReturnRoutingPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Inventory __instance,ItemStack _itemStack,ref bool __result)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||_itemStack==null||
            _itemStack.itemValue==null||_itemStack.itemValue.ItemClass==null||
            _itemStack.itemValue.ItemClass.GetItemName()!="resourceRockSmall")return true;
        EntityPlayerLocal player=RebirthToolbeltCapacity.ResolveOwner(__instance) as EntityPlayerLocal;
        XUiM_PlayerInventory inventory=player?.PlayerUI?.xui?.PlayerInventory;
        if(inventory==null||!ReferenceEquals(inventory.Toolbelt,__instance)||
            !inventory.Backpack.CanTakeItem(_itemStack))return true;
        // No insertion, events or source mutation here. The caller retains its original
        // native backpack/fallback/notification path and its current remaining count.
        __result=false;
        return false;
    }
}
