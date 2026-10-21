using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static class RebirthCamperEnergyOwnerHold
{
    private sealed class Hold
    {internal EntityPlayerLocal Player;internal RebirthCamperEnergyTransferFrame Frame;internal byte[] Before,After;}
    private sealed class Permit
    {internal Hold Hold;internal int Area,Slot;internal ItemStack Desired;}
    private static readonly Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();
    private static Permit permit;
    internal static bool Blocks(object value)=>holds.Values.Any(h=>ReferenceEquals(value,h.Player.bag)||ReferenceEquals(value,h.Player.inventory)||
        ReferenceEquals(value,h.Player.bag.ItemGrid)||ReferenceEquals(value,h.Player.inventory.ItemGrid)||ReferenceEquals(value,h.Player.PlayerUI?.xui?.DragAndDropWindow));
    internal static bool Prepare(EntityPlayerLocal player,RebirthCamperEnergyTransferFrame frame)
    {
        if(!ThreadManager.IsMainThread()||player==null||frame==null||player.IsDead()||
            RebirthDogLifecycleService.HasHireInventoryHold(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||
            RebirthCookingOutputDelivery.IsHeld(player)||AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||
            AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||
            RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||
            RebirthCamperStationNativeActorPayment.Blocks(player.bag)||RebirthCamperStationNativeActorPayment.Blocks(player.inventory))return false;
        // Detached prior-world objects grant no custody in the newly loaded
        // world. Durable receipts and native custom vars remain untouched.
        foreach(var entry in holds.Where(e=>!ReferenceEquals(e.Value.Player.world,GameManager.Instance?.World)).ToArray())holds.Remove(entry.Key);
        if(holds.TryGetValue(frame.Operation,out var prior))return ReferenceEquals(prior.Player,player)&&
            prior.Before.SequenceEqual(frame.Write(false))&&prior.After.SequenceEqual(frame.Write(true));
        if(holds.Count>=64||holds.Values.Any(h=>ReferenceEquals(h.Player,player))||!frame.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
        holds.Add(frame.Operation,new Hold {Player=player,Frame=frame,Before=frame.Write(false),After=frame.Write(true)});return true;
    }
    internal static bool Apply(EntityPlayerLocal player,RebirthCamperEnergyTransferFrame frame)
    {
        if(!holds.TryGetValue(frame.Operation,out var hold)||!ReferenceEquals(hold.Player,player)||
            !hold.Before.SequenceEqual(frame.Write(false))||!hold.After.SequenceEqual(frame.Write(true)))return false;
        try
        {
            var actual=RebirthCamperEnergyActorImage.Capture(player);
            if(!Compatible(actual.Bag,frame.Before.Bag,frame.After.Bag)||!Compatible(actual.Belt,frame.Before.Belt,frame.After.Belt)||
                !Compatible(new[]{actual.Cursor},new[]{frame.Before.Cursor},new[]{frame.After.Cursor})||
                (actual.BagLocks==null||frame.Before.BagLocks==null?actual.BagLocks!=frame.Before.BagLocks:!actual.BagLocks.SequenceEqual(frame.Before.BagLocks)))return false;
            Write(hold,0,actual.Bag,frame.After.Bag);Write(hold,1,actual.Belt,frame.After.Belt);
            if(!RebirthCamperEnergyItems.Same(actual.Cursor,frame.After.Cursor))
            {
                permit=new Permit {Hold=hold,Area=2,Slot=0,Desired=frame.After.Cursor};
                try{player.PlayerUI.xui.DragAndDropWindow.SetCurrentStack(frame.After.Cursor.Clone(),false);}finally{permit=null;}
            }
            return frame.After.Matches(RebirthCamperEnergyActorImage.Capture(player));
        }
        catch{return false;}
        finally{permit=null;}
    }
    private static bool Compatible(ItemStack[] actual,ItemStack[] before,ItemStack[] after)=>actual!=null&&before!=null&&after!=null&&
        actual.Length==before.Length&&actual.Length==after.Length&&actual.Select((item,i)=>RebirthCamperEnergyItems.Same(item,before[i])||RebirthCamperEnergyItems.Same(item,after[i])).All(v=>v);
    private static void Write(Hold hold,int area,ItemStack[] actual,ItemStack[] after)
    {
        for(int i=0;i<after.Length;i++)
        {
            if(RebirthCamperEnergyItems.Same(actual[i],after[i]))continue;
            var value=after[i].Clone();permit=new Permit {Hold=hold,Area=area,Slot=i,Desired=value};
            try{if(area==0)hold.Player.bag.SetSlot(i,value);else hold.Player.inventory.SetItem(i,value);}
            finally{permit=null;}
        }
    }
    internal static bool Retire(EntityPlayerLocal player,RebirthCamperEnergyTransferFrame frame)
    {
        // Caller admits retirement only after the root's explicit paid proof
        // and native vehicle publication. With no retained hold this is a
        // metadata acknowledgement only, never an inventory replay.
        if(!holds.TryGetValue(frame.Operation,out var hold))return true;
        if(!ReferenceEquals(hold.Player,player)||!hold.Before.SequenceEqual(frame.Write(false))||!hold.After.SequenceEqual(frame.Write(true))||
            !frame.After.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
        holds.Remove(frame.Operation);
        RebirthCamperCommittedItemSound.Play(player,
            frame.Before.Bag.Concat(frame.Before.Belt).Concat(new[]{frame.Before.Cursor}).ToArray(),
            frame.After.Bag.Concat(frame.After.Belt).Concat(new[]{frame.After.Cursor}).ToArray(),RebirthCamperRadial.Energy);
        return true;
    }
    internal static bool Mutation(object target,MethodBase method,object[] args)
    {
        if(!Blocks(target))return true;var p=permit;if(p==null||args==null)return false;
        var player=p.Hold.Player;object container=p.Area==0?(object)player.bag:p.Area==1?(object)player.inventory:player.PlayerUI.xui.DragAndDropWindow;
        object grid=p.Area==0?(object)player.bag.ItemGrid:p.Area==1?(object)player.inventory.ItemGrid:null;
        if(!ReferenceEquals(target,container)&&!ReferenceEquals(target,grid))return false;
        if(p.Area==2)return method.Name==nameof(XUiC_DragAndDropWindow.SetCurrentStack)&&args.Length>=1&&args[0] is ItemStack cursor&&RebirthCamperEnergyItems.Same(cursor,p.Desired);
        if((method.Name!="SetItem"&&method.Name!="SetSlot")||args.Length<2||!(args[0] is int slot)||slot!=p.Slot)return false;
        if(args[1] is ItemStack stack)return RebirthCamperEnergyItems.Same(stack,p.Desired);
        return args.Length>=3&&args[1] is ItemValue item&&args[2] is int count&&count==p.Desired.count&&
            RebirthNativeItemCodec.Encode(item)==RebirthNativeItemCodec.Encode(p.Desired.itemValue);
    }
}

[HarmonyPatch]
internal static class RebirthCamperEnergyOwnerMutationGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        string[] names={"SetSlot","SetSlots","SetItem","SetItems","DecItem","AddItem","AddItemAt","ChangeSlotCount","clearSlotByIndex", "ChangeCount","ClearItems","Compact","Sort","CompactSlots","Reset","Clear","CopyFrom","SetSlotLocks","SetLocked"};
        foreach(var type in new[]{typeof(Bag),typeof(Inventory),typeof(ItemStackGrid)})
            foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(method.DeclaringType==type&&names.Contains(method.Name))yield return method;
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.SetCurrentStack));
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(object __instance,MethodBase __originalMethod,object[] __args)=>RebirthCamperEnergyOwnerHold.Mutation(__instance,__originalMethod,__args);
}

[HarmonyPatch]
internal static class RebirthCamperEnergyOwnerCursorGuard
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","SwapItem","HandleMoveToPreferredLocation"})
        {
            var method=AccessTools.DeclaredMethod(typeof(XUiC_ItemStack),name,Type.EmptyTypes);
            if(method==null)throw new MissingMethodException(typeof(XUiC_ItemStack).FullName,name);yield return method;
        }
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.PlaceItemBackInInventory));
        // Native OnClose falls through to DropItem when AddItem cannot take a
        // held cursor. Keep exact cursor custody instead of producing a drop.
        yield return AccessTools.DeclaredMethod(typeof(XUiC_DragAndDropWindow),nameof(XUiC_DragAndDropWindow.OnClose));
    }
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(XUiController __instance)=>!RebirthCamperEnergyOwnerHold.Blocks(__instance?.xui?.playerUI?.entityPlayer?.bag);
}
