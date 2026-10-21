// Reuses authenticated creation-scoped support requests, with native inventory sync before exact-item equip.
public static class RebirthOverviewWaterRequest
{
    public static string EquippedItem(EntityPlayer player)
    {
        if(player?.world==null)return string.Empty;
        RebirthMetabolismSnapshot snapshot;
        if(!player.world.IsRemote())
        {
            RebirthMetabolismState state;
            if(!RebirthMetabolismService.IsServerAuthority||!RebirthMetabolismStateRepository.TryGet(player,out state)||state.HydrationSlotItem==null||state.HydrationSlotItem.IsEmpty())return string.Empty;
            return state.HydrationSlotItem.itemValue?.ItemClass?.GetItemName()??string.Empty;
        }
        else if(!RebirthMetabolismClientState.TryGet(out snapshot)||snapshot.OwnerEntityId!=player.entityId)return string.Empty;
        return snapshot.HydrationSlotItemName??string.Empty;
    }
    public static bool Accepts(ItemValue item)
    {
        RebirthConsumableDefinition definition;
        return item!=null&&RebirthConsumableResolver.TryResolve(item,out definition)&&definition!=null&&definition.IsDrink&&definition.HydrationEquippable;
    }
    public static void Dispatch(EntityPlayer player,ItemValue item,bool unequip=false)
    {
        if(player?.world==null)return;
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!player.world.IsRemote())
        {
            if(connection==null||!connection.IsServer)return;
            string message;
            bool ok=unequip?RebirthMetabolismService.UnequipHydrationContainer(player,out message):
                RebirthMetabolismService.EquipMatchingHydrationContainer(player,item,out message);
            RebirthSurvivorSupportUiFeedback.Receive(ok,message);return;
        }
        var local=player as EntityPlayerLocal;
        bool pending=local!=null&&RebirthHydrationOwningAction.BeginMatching(local,item,unequip);
        RebirthSurvivorSupportUiFeedback.Receive(pending,Localization.Get(pending?"xuiRebirthHydrationTransferPending":"xuiRebirthHydrationTransferUnavailable"));
    }
}
