using System;
using System.Collections.Generic;
using System.Linq;

// Exact native owner objects remain frozen through saved owner/world settlement.
// Permission is never granted by an entity ID or an unqualified parsed plan.
internal static class RebirthVehicleOwnerCustody
{
    private sealed class Entry
    {
        internal EntityPlayerLocal Player;internal World World;internal GameManager Game;internal Bag Bag;internal Inventory Belt;
        internal RebirthVehicleTransferPlan Plan;internal Func<bool> Scope;
        internal string BagLocks,BeltLocks;internal int OwnedBelt;
    }
    private static readonly Dictionary<EntityPlayerLocal,Entry> entries=new Dictionary<EntityPlayerLocal,Entry>();
    private static readonly List<Entry> revoked=new List<Entry>();
    internal static bool BlocksInventory(object inventory,bool bag)
    {
        if(inventory==null)return false;
        foreach(var entry in entries.Values)if(ReferenceEquals(inventory,bag?(object)entry.Bag:entry.Belt))return true;
        foreach(var entry in revoked)if(ReferenceEquals(inventory,bag?(object)entry.Bag:entry.Belt))return true;
        return false;
    }
    private static bool Current(Entry entry)
    {
        try{return ThreadManager.IsMainThread()&&entry.Scope()&&RebirthVehicleClientCustody.HasCurrentTransferSourcePolicy(entry.Player,entry.Plan)&&ReferenceEquals(entry.Game,GameManager.Instance)&&ReferenceEquals(entry.World,entry.Player.world)&&
            ReferenceEquals(entry.Game.World,entry.World)&&ReferenceEquals(entry.World.GetPrimaryPlayer(),entry.Player)&&ReferenceEquals(entry.World.GetEntity(entry.Player.entityId),entry.Player)&&
            ReferenceEquals(entry.Bag,entry.Player.bag)&&ReferenceEquals(entry.Belt,entry.Player.inventory)&&!entry.Player.IsDead()&&entry.Player.IsSpawned()&&
            entry.BagLocks==RebirthVehicleNativePolicy.Capture(entry.Player,true)&&entry.BeltLocks==RebirthVehicleNativePolicy.Capture(entry.Player,false)&&
            entry.OwnedBelt==RebirthToolbeltCapacity.GetOwnedSlotCount(entry.Player,entry.Belt.ItemGrid.items.Length);}
        catch{return false;}
    }
    internal static bool TryAcquire(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,Func<bool> authenticatedOriginal)
    {
        if(!ThreadManager.IsMainThread()||player==null||plan==null||authenticatedOriginal==null||!authenticatedOriginal()||!RebirthVehicleClientCustody.HasCurrentTransferSourcePolicy(player,plan))return false;
        entries.TryGetValue(player,out var retained);
        if(retained!=null&&(retained.Plan.ToXml().ToString()!=plan.ToXml().ToString()||Current(retained)))return retained.Plan.ToXml().ToString()==plan.ToXml().ToString()&&Current(retained);
        if(entries.Count+revoked.Count>=64)return false;
        foreach(var prior in entries.Values)
            if(prior.Plan.ActorId==plan.ActorId&&ReferenceEquals(prior.World,player.world)&&(prior.Plan.TransactionId!=plan.TransactionId||Current(prior)))return false;
        if(retained==null&&(!RebirthVehicleInventoryWrite.CanMutate(player,true)||!RebirthVehicleInventoryWrite.CanMutate(player,false)||
            !RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)))return false;
        if(retained!=null&&(RebirthGearOwnerReservation.BlocksInventoryExceptVehicle(player.bag,true)||RebirthGearOwnerReservation.BlocksInventoryExceptVehicle(player.inventory,false)||
            RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory)||RebirthRemoteResourceOwnerAction.IsHeld(player)))return false;
        var next=new Entry{Player=player,World=player.world,Game=GameManager.Instance,Bag=player.bag,Belt=player.inventory,Plan=plan,Scope=authenticatedOriginal,
            BagLocks=RebirthVehicleNativePolicy.Capture(player,true),BeltLocks=RebirthVehicleNativePolicy.Capture(player,false),OwnedBelt=RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length)};
        if(!Current(next))return false;if(retained!=null)revoked.Add(retained);entries[player]=next;return true;
    }
    internal static bool NativeCanEnter(EntityPlayer player,bool bag)
    {
        if(!(player is EntityPlayerLocal local)||!entries.TryGetValue(local,out var entry)||!Current(entry))return false;
        object inventory=bag?(object)entry.Bag:entry.Belt;
        return !RebirthGearOwnerReservation.BlocksInventoryExceptVehicle(inventory,bag)&&
            !RebirthCamperStationNativeActorPayment.Blocks(inventory)&&!RebirthRemoteResourceOwnerAction.IsHeld(local);
    }
    internal static bool Matches(EntityPlayerLocal player,RebirthVehicleTransferPlan plan)
        =>player!=null&&plan!=null&&entries.TryGetValue(player,out var entry)&&entry.Plan.ToXml().ToString()==plan.ToXml().ToString()&&Current(entry);
    internal static bool TryGetPlan(EntityPlayerLocal player,IEnumerable<RebirthVehicleTransferItem> operations,out RebirthVehicleTransferPlan plan)
    {
        plan=null;var items=operations?.ToArray();
        if(player==null||items==null||items.Length==0||!entries.TryGetValue(player,out var entry)||!Current(entry)||
            !items.All(item=>entry.Plan.Items.Any(original=>item!=null&&item.ToXml().ToString()==original.ToXml().ToString())))return false;
        plan=entry.Plan;return true;
    }
    internal static bool CreationMatches(EntityPlayerLocal player,string current,string offered,IEnumerable<RebirthVehicleTransferItem> operations)
        =>RebirthSurvivorRequestScope.Matches(current,offered)||current==string.Empty&&offered==string.Empty&&
            TryGetPlan(player,operations,out var plan)&&plan.CreationId==string.Empty&&plan.NativeDeaths==player.Died;
    internal static bool Release(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,Func<bool> jointSaved)
    {
        if(jointSaved==null||!Matches(player,plan)||!jointSaved()||!Matches(player,plan))return false;
        if(!entries.Remove(player))return false;
        foreach(var old in entries.Where(pair=>pair.Value.Plan.TransactionId==plan.TransactionId&&!Current(pair.Value)).Select(pair=>pair.Key).ToArray())entries.Remove(old);
        revoked.RemoveAll(entry=>entry.Plan.TransactionId==plan.TransactionId&&!Current(entry));return true;
    }
    internal static bool RetireHistorical(EntityPlayerLocal player,RebirthVehicleTransferPlan plan,Func<bool> acknowledgedTerminal)
    {
        if(!ThreadManager.IsMainThread()||player==null||plan==null||acknowledgedTerminal==null||!acknowledgedTerminal())return false;
        var matching=entries.Where(pair=>pair.Value.Plan.TransactionId==plan.TransactionId).ToArray();
        if(matching.Any(pair=>pair.Value.Plan.ToXml().ToString()!=plan.ToXml().ToString())||revoked.Any(entry=>entry.Plan.TransactionId==plan.TransactionId&&entry.Plan.ToXml().ToString()!=plan.ToXml().ToString()))return false;
        if(!acknowledgedTerminal())return false;
        foreach(var pair in matching)entries.Remove(pair.Key);revoked.RemoveAll(entry=>entry.Plan.TransactionId==plan.TransactionId);return true;
    }
    internal static void WriteSlots(EntityPlayerLocal player,bool bag,ItemStack[] candidate,IEnumerable<RebirthVehicleTransferItem> operations,Func<bool> current)
    {
        var items=operations?.ToArray();
        bool Bound()=>items!=null&&items.Length>0&&entries.TryGetValue(player,out var entry)&&items.All(item=>item!=null&&
            entry.Plan.Items.Any(original=>original.ToXml().ToString()==item.ToXml().ToString()));
        if(!NativeCanEnter(player,bag)||current==null||!current()||candidate==null||!Bound())throw new InvalidOperationException("Vehicle native custody write unavailable.");
        var original=bag?player.bag.ItemGrid.items:player.inventory.ItemGrid.items;
        if(candidate.Length!=original.Length)throw new InvalidOperationException("Vehicle native write changed owner dimensions.");
        int owned=bag?original.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,original.Length);
        for(int index=0;index<original.Length;index++)if((index>=owned||RebirthVehicleNativePolicy.IsLocked(player,bag,index))&&
            !RebirthStationGridIngredients.IsSameStackSnapshot(original[index],candidate[index]))throw new InvalidOperationException("Vehicle native write changed a protected cell.");
        bool Scope()=>current()&&NativeCanEnter(player,bag)&&Bound();
        if(bag)
        {
            var native=player.bag;
            RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BagBacking,native,-1,candidate,candidate.Length,Scope,()=>native.SetSlots(candidate,true));
        }
        else
        {
            var native=player.inventory;
            for(int index=0;index<candidate.Length;index++)
            {
                if(RebirthStationGridIngredients.IsSameStackSnapshot(native.ItemGrid.items[index],candidate[index]))continue;
                int slot=index;var value=candidate[index].itemValue.Clone();int count=candidate[index].count;
                RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BeltSlot,native,slot,value,count,Scope,()=>native.SetItem(slot,value,count));
            }
        }
    }
}
