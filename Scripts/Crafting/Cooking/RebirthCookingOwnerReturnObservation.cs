using System;
using System.Linq;

// Observes the native owner's existing AddItem call. Its boolean can mean that
// only part of an oversized offer moved; quantity comes from actual postimages.
// This observation does not replace the owning action's durable receipt.
internal sealed class RebirthCookingOwnerReturnObservation : IDisposable
{
    private EntityPlayerLocal actor;
    private World world;
    private ItemStackGrid bag,belt;
    private ItemStack[] beforeBag,beforeBelt,beforeEquipment;
    private ItemStack offered;
    internal ItemStack Argument {get;private set;}
    private int deaths;
    private string creation;
    private IDisposable allocation;

    internal static RebirthCookingOwnerReturnObservation Begin(EntityPlayerLocal player,ItemStack offer)
    {
        if(!ThreadManager.IsMainThread()||player?.bag?.ItemGrid==null||player.inventory?.ItemGrid==null||player.equipment==null||offer==null||offer.IsEmpty()||offer.count<1||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
            !ReferenceEquals(player.world.GetEntity(player.entityId),player)||!player.IsSpawned()||player.IsDead())return null;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(64L*1024*1024,out var memory))return null;
        var result=new RebirthCookingOwnerReturnObservation {actor=player,world=player.world,bag=player.bag.ItemGrid,belt=player.inventory.ItemGrid,
            deaths=player.Died,creation=RebirthCamperCharacterAccess.CurrentCreation(player),allocation=memory};
        try
        {
            if(string.IsNullOrEmpty(result.creation))throw new InvalidOperationException("Owner creation unavailable");
            long remaining=2L*1024*1024;
            byte[] b=RemoteResourceTransactions.CaptureOriginalNativeFrame(result.bag.items,ref remaining),
                t=RemoteResourceTransactions.CaptureOriginalNativeFrame(result.belt.items,ref remaining),
                e=RemoteResourceTransactions.CaptureOriginalNativeFrame(Equipment(player),ref remaining),
                o=RemoteResourceTransactions.CaptureOriginalNativeFrame(new[]{offer},ref remaining);
            long graph=checked(RemoteResourceTransactions.MeasureOriginalNativeFrame(b)+RemoteResourceTransactions.MeasureOriginalNativeFrame(t)+
                RemoteResourceTransactions.MeasureOriginalNativeFrame(e)+RemoteResourceTransactions.MeasureOriginalNativeFrame(o));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,checked(64L*1024*1024+2*graph)))throw new InvalidOperationException("Owner return observation budget unavailable");
            result.beforeBag=RebirthCamperInventorySnapshot.Restore(b);result.beforeBelt=RebirthCamperInventorySnapshot.Restore(t);
            result.beforeEquipment=RebirthCamperInventorySnapshot.Restore(e);result.offered=RebirthCamperInventorySnapshot.Restore(o)[0];
            result.Argument=RebirthCamperInventorySnapshot.Restore(o)[0];
            return result;
        }
        catch{result.Dispose();return null;}
    }
    private static ItemStack[] Equipment(EntityPlayerLocal player)=>Enumerable.Range(0,player.equipment.GetSlotCount())
        .Select(slot=>new ItemStack(player.equipment.GetSlotItem(slot),1)).ToArray();
    private bool Current()=>ThreadManager.IsMainThread()&&actor!=null&&ReferenceEquals(GameManager.Instance?.World,world)&&
        ReferenceEquals(world.GetPrimaryPlayer(),actor)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&ReferenceEquals(actor.world,world)&&
        actor.IsSpawned()&&!actor.IsDead()&&actor.Died==deaths&&RebirthCamperCharacterAccess.CurrentCreation(actor)==creation&&
        ReferenceEquals(actor.bag.ItemGrid,bag)&&ReferenceEquals(actor.inventory.ItemGrid,belt);
    private bool SameKind(ItemStack value)=>value!=null&&!value.IsEmpty()&&RebirthStationGridIngredients.IsSameStackSnapshot(
        new ItemStack(value.itemValue,1),new ItemStack(offered.itemValue,1));
    private bool Added(ItemStack[] before,ItemStack[] after,ref long moved)
    {
        if(before==null||after==null||before.Length!=after.Length)return false;
        for(int slot=0;slot<before.Length;slot++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot]))continue;
            // The native additive path may fill an empty cell or increase an
            // identical rich stack. Other changes require original recovery.
            if(before[slot]==null||after[slot]==null||before[slot].count<0||after[slot].count<0||
                !SameKind(after[slot])||!before[slot].IsEmpty()&&!SameKind(before[slot]))return false;
            int delta=after[slot].count-(before[slot].IsEmpty()?0:before[slot].count);
            if(delta<0)return false;moved+=delta;if(moved>offered.count)return false;
        }
        return true;
    }
    internal bool Observe(ItemStack remainder,out int delivered)
    {
        delivered=0;
        if(!Current()||remainder==null||remainder.count<0||remainder.count>offered.count||remainder.count>0&&!SameKind(remainder))return false;
        long moved=0;
        if(!Added(beforeBag,bag.items,ref moved)||!Added(beforeBelt,belt.items,ref moved)||!Added(beforeEquipment,Equipment(actor),ref moved)||
            moved+remainder.count!=offered.count||!Current())return false;
        delivered=(int)moved;return true;
    }
    public void Dispose()
    {allocation?.Dispose();allocation=null;actor=null;world=null;bag=belt=null;beforeBag=beforeBelt=beforeEquipment=null;offered=null;Argument=null;}
}
