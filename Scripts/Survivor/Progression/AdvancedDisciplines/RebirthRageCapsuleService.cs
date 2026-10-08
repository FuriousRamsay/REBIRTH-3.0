using System;
#nullable disable
public static class RebirthRageCapsuleService
{
    public static bool Consume(EntityPlayer p,int type,ushort seed,out string message)
    {
        message=string.Empty;if(p==null||!RebirthWorldCharacterRepository.IsServerAuthority||!RebirthSurvivorMode.IsEnabledForCurrentWorld()){message="Not authoritative.";return false;}
        if(p.IsDead()||RebirthCharacterCreationHoldService.IsHeld(p)){message="Rage Capsules require a living, completed Survivor.";return false;}
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(p)){message=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        RebirthWorldCharacterRecord r;if(!RebirthWorldCharacterService.TryGet(p,out r)||r==null||!r.IsComplete||r.Progression==null||!r.Progression.AcquiredDisciplineIds.Contains(RebirthSurvivorIds.DisciplineBerserker)||!r.Progression.KnowledgeIds.Contains(RebirthSurvivorIds.KnowledgeRageOffensive)){message="Offensive Rage Knowledge is required to exploit a Rage Capsule.";return false;}
        bool bag;int slot;ItemStack stack;if(!Find(p,type,seed,out bag,out slot,out stack)){message="Rage Capsule is no longer available.";return false;}
        string itemName=stack.itemValue!=null&&stack.itemValue.ItemClass!=null?(stack.itemValue.ItemClass.Name??string.Empty):string.Empty;if(!string.Equals(itemName,"rebirthRageCapsule",StringComparison.OrdinalIgnoreCase)){message="The selected item is not a Rage Capsule.";return false;}
        string reason;if(!RebirthRageService.TryActivate(p,"offensive",out reason)){message=reason;return false;}stack.count--;if(stack.count<=0)stack=ItemStack.Empty.Clone();if(bag)p.bag.SetSlot(slot,stack);else p.inventory.SetItem(slot,stack);message="Rage Capsule consumed. "+reason;return true;
    }
    private static bool Find(EntityPlayer p,int type,ushort seed,out bool bag,out int slot,out ItemStack found){bag=false;slot=-1;found=ItemStack.Empty.Clone();if(p==null||p.inventory==null||p.bag==null)return false;ItemStack[] tool=p.inventory.ItemGrid.items;for(int i=0;i<RebirthToolbeltCapacity.GetOwnedSlotCount(p,tool.Length);i++){ItemStack s=tool[i];if(Match(s,type,seed)){slot=i;found=s;return true;}}ItemStack[] bags=p.bag.ItemGrid.items;for(int i=0;i<bags.Length;i++){ItemStack s=bags[i];if(Match(s,type,seed)){bag=true;slot=i;found=s;return true;}}return false;}
    private static bool Match(ItemStack s,int type,ushort seed){return s!=null&&!s.IsEmpty()&&s.itemValue!=null&&s.itemValue.type==type&&s.itemValue.Seed==seed;}
}
