using System;

// Detached plans only. Publication/debit belongs to the durable coordinator;
// preparing a card/slot action never changes live actor or vehicle state.
internal static class RebirthCamperEnergySourcePlan
{
    internal enum Action:byte {Install,Remove,Component,Refuel,Enabled}
    internal sealed class Plan
    {
        internal RebirthCamperEnergyActorImage Before,After;
        internal RebirthCamperEnergyRuntime.Change Change;
    }
    internal static bool TryPrepare(RebirthCamperEnergyRuntime runtime,long revision,Action action,Guid source,
        int component,bool enabled,RebirthCamperEnergyActorImage actor,out Plan plan,out string reason,int backpackSlot=-1,int sourceCapacity=RebirthCamperEnergyCapacity.Normal)
    {
        plan=null;reason="Energy source or actor image changed.";
        if(runtime==null||actor?.Cursor?.itemValue==null)return false;
        try
        {
            var after=actor.Copy();RebirthCamperEnergyRuntime.Change change;
            bool bagSource=backpackSlot>=0;
            if(bagSource&&(backpackSlot>=actor.Bag.Length||!actor.Cursor.IsEmpty()||
                actor.BagLocks!=null&&actor.BagLocks[backpackSlot]))return false;
            var offered=bagSource?actor.Bag[backpackSlot]:actor.Cursor;
            if(action==Action.Install)
            {
                if(offered.count!=1||!RebirthCamperEnergyRuntime.TryCreateBank(offered,out var bank,out reason)||
                    !runtime.TryPrepareInstall(revision,offered,bank,out change,out reason,component,sourceCapacity))return false;
                if(bagSource)after.Bag[backpackSlot]=ItemStack.Empty.Clone();else after.Cursor=ItemStack.Empty.Clone();
            }
            else if(action==Action.Remove)
            {
                if(!actor.Cursor.IsEmpty()){reason="Put down the held item before removing a source.";return false;}
                if(bagSource&&!offered.IsEmpty())return false;
                if(!runtime.TryPrepareRemove(revision,source,out change,out reason))return false;
                var removed=change.Removed;if(removed==null||removed.count!=1)return false;
                if(bagSource)after.Bag[backpackSlot]=removed;else after.Cursor=removed;
            }
            else
            {
                if(!runtime.TryCopyBank(source,out var bank)){reason="Installed source changed.";return false;}
                var cells=RebirthCamperEnergyItems.Copy(bank.Stacks);
                ushort fuel=bank is PowerGenerator generator?generator.CurrentFuel:(ushort)0;
                bool on=bank.isOn;
                if(action==Action.Component)
                {
                    if(bank.isOn){reason="Turn the bank off before changing its components.";return false;}
                    if(component<0||component>=cells.Length)return false;
                    if(offered.IsEmpty()&&cells[component].IsEmpty())return false;
                    if(!offered.IsEmpty()&&offered.count!=1){reason="Pick up one native component.";return false;}
                    if(bagSource)after.Bag[backpackSlot]=cells[component];else after.Cursor=cells[component];
                    cells[component]=RebirthCamperEnergyItems.Copy(new[]{offered})[0];
                }
                else if(action==Action.Enabled)on=enabled;
                else if(action==Action.Refuel)
                {
                    if(bank.isOn){reason="Turn the bank off before refueling it.";return false;}
                    if(!(bank is PowerGenerator generatorBank)){reason="This source does not use fuel.";return false;}
                    var gasoline=ItemClass.GetItemWithTag(FastTags<TagGroup.Global>.Parse("gasoline"));
                    if(gasoline==null)return false;
                    int requested=Math.Min(250,generatorBank.MaxFuel-generatorBank.CurrentFuel);
                    if(requested<=0){reason="Fuel tank is full.";return false;}
                    // Native PowerSourceStats refuel debits belt first and uses
                    // bag only when the belt debit returns zero, not a top-up.
                    int paid=DebitType(after.Belt,gasoline.Id,requested);
                    if(paid==0)paid=DebitType(after.Bag,gasoline.Id,requested);
                    if(paid<=0){reason="No native gasoline is available.";return false;}
                    fuel=checked((ushort)(fuel+paid));
                }
                else return false;
                if(!runtime.TryPrepareContents(revision,source,cells,fuel,on,out change,out reason))return false;
            }
            plan=new Plan {Before=actor.Copy(),After=after,Change=change};reason=null;return true;
        }
        catch(Exception){reason="Native energy source preparation refused.";return false;}
    }
    private static int DebitType(ItemStack[] cells,int type,int requested)
    {
        int remaining=requested;
        for(int i=0;i<cells.Length&&remaining>0;i++)
        {
            var stack=cells[i];if(stack?.itemValue==null||stack.itemValue.type!=type||stack.count<=0)continue;
            if(!stack.itemValue.ItemClass.CanStack()){cells[i]=ItemStack.Empty.Clone();remaining--;continue;}
            int take=Math.Min(remaining,stack.count);remaining-=take;
            if(take==stack.count)cells[i]=ItemStack.Empty.Clone();else stack.count-=take;
        }
        return requested-remaining;
    }
}
