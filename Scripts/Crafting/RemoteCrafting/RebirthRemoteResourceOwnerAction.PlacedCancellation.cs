using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class RebirthRemoteResourceOwnerAction
{
    private sealed class PlacedCancellationHold
    {
        internal RebirthRemoteResourcePlacedStationOriginal.CancellationClient Original;
        internal ItemStackGrid Bag,Belt;internal bool Entered;internal long MutationVersion;
        internal RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerSavedAfterHydrated ReconstructedSavedAfter;
        internal bool Current()=>Original.OwnerCurrent()&&ReferenceEquals(Original.Actor.bag?.ItemGrid,Bag)&&ReferenceEquals(Original.Actor.inventory?.ItemGrid,Belt);
    }
    private static readonly Dictionary<Guid,PlacedCancellationHold> placedCancellations=new Dictionary<Guid,PlacedCancellationHold>();
    private sealed class PlacedCancellationPermit
    {internal PlacedCancellationHold Hold;internal RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerPermission Proof;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used;internal RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerStep Step;}
    [ThreadStatic]private static PlacedCancellationPermit placedCancellationPermit;
    private static bool PlacedCancellationIsHeld(EntityPlayerLocal actor)=>actor!=null&&placedCancellations.Values.Any(value=>ReferenceEquals(value.Original.Actor,actor)&&value.Current());
    internal static bool HoldsPlacedCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)=>original!=null&&
        placedCancellations.TryGetValue(original.Operation,out var hold)&&ReferenceEquals(hold.Original,original)&&hold.Current();
    internal static bool HoldPlacedCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
    {
        if(original==null||!RebirthRemoteResourcePlacedStationOriginal.IsPreparedCancellationOwner(original))return false;
        if(placedCancellations.TryGetValue(original.Operation,out var held))return ReferenceEquals(held.Original,original)&&held.Current();
        if(!CanPrepareNativeOriginal(original.Actor)||!original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
        placedCancellations.Add(original.Operation,new PlacedCancellationHold{Original=original,Bag=original.Actor.bag.ItemGrid,Belt=original.Actor.inventory.ItemGrid});return true;
    }
    internal static bool HoldRestartCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerHydrated proof)
    {
        if(proof?.FullTerminal!=true)return false;var original=proof.Original;
        if(placedCancellations.ContainsKey(original.Operation)||holds.Values.Any(value=>ReferenceEquals(value.Player,original.Actor))||PlacedCancellationIsHeld(original.Actor))return false;
        placedCancellations.Add(original.Operation,new PlacedCancellationHold{Original=original,Bag=original.Actor.bag.ItemGrid,Belt=original.Actor.inventory.ItemGrid});return true;
    }
    internal static void DropFailedRestartCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
    {
        if(original?.RestartOwner==null&&original?.RestartSavedAfterOwner==null&&original?.RestartBeforeOwner==null)return;
        if(placedCancellations.TryGetValue(original.Operation,out var hold)&&ReferenceEquals(hold.Original,original)&&!hold.Entered&&hold.MutationVersion==0)placedCancellations.Remove(original.Operation);
    }
    internal static bool HoldRestartSavedAfterCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerSavedAfterHydrated proof)
    {
        if(proof?.Current!=true)return false;var original=proof.Original;
        if(placedCancellations.ContainsKey(original.Operation)||holds.Values.Any(value=>ReferenceEquals(value.Player,original.Actor))||PlacedCancellationIsHeld(original.Actor))return false;
        placedCancellations.Add(original.Operation,new PlacedCancellationHold{Original=original,Bag=original.Actor.bag.ItemGrid,Belt=original.Actor.inventory.ItemGrid,ReconstructedSavedAfter=proof});return true;
    }
    internal static bool HoldRestartBeforeCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerBeforeHydrated proof)
    {
        if(proof?.Current!=true)return false;var original=proof.Original;
        if(placedCancellations.ContainsKey(original.Operation)||holds.Values.Any(value=>ReferenceEquals(value.Player,original.Actor))||PlacedCancellationIsHeld(original.Actor))return false;
        placedCancellations.Add(original.Operation,new PlacedCancellationHold{Original=original,Bag=original.Actor.bag.ItemGrid,Belt=original.Actor.inventory.ItemGrid});return true;
    }
    private static bool ConsumePlacedCancellation(ItemStackGrid grid,int slot,ItemStack value)
    {
        var once=placedCancellationPermit;
        if(once==null||once.Used||!once.Hold.Current()||!once.Proof.Current||!ReferenceEquals(once.Grid,grid)||once.Slot!=slot||!ReferenceEquals(once.Value,value))return false;
        once.Used=true;return true;
    }
    internal static bool ApplyPlacedCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerPermission proof)
    {
        if(proof==null||!proof.Current||!placedCancellations.TryGetValue(proof.Operation,out var hold)||!ReferenceEquals(hold.Original,proof.Original)||hold.ReconstructedSavedAfter!=null||!hold.Current()||placedCancellationPermit!=null)return false;
        var original=hold.Original;
        if(!hold.Entered&&original.RestartBeforeOwner?.PrefixCurrent!=true&&!original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
        if(!RebirthRemoteResourcePlacedStationOriginal.TryPrepareCancellationOwnerProgress(original,out var progress))return false;
        hold.Entered=true;
        while(progress.Completed<progress.Steps.Count)
        {
            if(RebirthRemoteResourcePlacedStationOriginal.HoldCancellationScenario(original.Operation,"ownerPrepared",progress.Completed))return false;
            var next=progress.Steps[progress.Completed];var grid=next.Bag?hold.Bag:hold.Belt;
            if(next.NativeReturned)
            {
                // Only this retained actual original setter return can finish
                // its metadata/save checkpoint. Never enter the setter again.
                if(!proof.Current||!hold.Current()||!RebirthRemoteResourcePlacedStationOriginal.FinishCancellationOwnerStep(next))return false;
                hold.MutationVersion++;continue;
            }
            if(!proof.Current||!hold.Current()||!RebirthRemoteResourcePlacedStationOriginal.EnterCancellationOwnerStep(progress,grid,next.Slot,out var step))return false;
            var desired=(step.Bag?original.After.Bag:original.After.Belt)[step.Slot].Clone();
            var once=new PlacedCancellationPermit{Hold=hold,Proof=proof,Grid=grid,Slot=step.Slot,Value=desired,Step=step};placedCancellationPermit=once;
            try{grid.SetItem(step.Slot,desired);}finally{placedCancellationPermit=null;}
            if(!once.Used||!proof.Current||!hold.Current()||!RebirthRemoteResourcePlacedStationOriginal.FinishCancellationOwnerStep(step))return false;
            hold.MutationVersion++;
        }
        return proof.Current&&original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor));
    }
    internal static void ObserveCancellationOwnerSlot(ItemStackGrid grid,int slot,ItemStack value,bool runOriginal)
    {
        var once=placedCancellationPermit;
        if(!runOriginal||once?.Step==null||!once.Used||!once.Hold.Current()||!once.Proof.Current||
            !ReferenceEquals(once.Grid,grid)||once.Slot!=slot||!ReferenceEquals(once.Value,value))return;
        once.Step.NativeReturned=true;
        RebirthRemoteResourcePlacedStationOriginal.ObserveCancellationScenarioOwnerReturn(once.Step);
    }
    internal sealed class PlacedCancellationTerminalVersion
    {
        private readonly PlacedCancellationHold hold;private readonly long version;
        private PlacedCancellationTerminalVersion(PlacedCancellationHold value){hold=value;version=value.MutationVersion;}
        internal bool IsCurrent=>hold.Current()&&hold.MutationVersion==version&&
            placedCancellations.TryGetValue(hold.Original.Operation,out var current)&&ReferenceEquals(current,hold)&&
            hold.Original.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+hold.Original.Operation.ToString("N"))==3f&&
            hold.Original.After.Matches(RebirthCamperEnergyActorImage.Capture(hold.Original.Actor));
        internal static PlacedCancellationTerminalVersion Capture(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
        {
            if(original==null||!RebirthRemoteResourcePlacedStationOriginal.HasCancellationClientTerminal(original)||
                !placedCancellations.TryGetValue(original.Operation,out var hold)||!ReferenceEquals(hold.Original,original)||!hold.Current()||
                original.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+original.Operation.ToString("N"))!=3f||
                !original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return null;
            return new PlacedCancellationTerminalVersion(hold);
        }
        internal static PlacedCancellationTerminalVersion CaptureRestart(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerHydrated proof)
        {
            if(proof?.FullTerminal!=true||!placedCancellations.TryGetValue(proof.Original.Operation,out var hold)||!ReferenceEquals(hold.Original,proof.Original)||!hold.Current()||hold.Entered||hold.MutationVersion!=0)return null;
            return new PlacedCancellationTerminalVersion(hold);
        }
    }
    internal sealed class PlacedCancellationSavedAfterVersion
    {
        private readonly PlacedCancellationHold hold;private readonly long version;
        private PlacedCancellationSavedAfterVersion(PlacedCancellationHold value){hold=value;version=value.MutationVersion;}
        internal bool IsCurrent=>hold.Current()&&hold.MutationVersion==version&&(hold.Entered||hold.ReconstructedSavedAfter?.Current==true)&&
            placedCancellations.TryGetValue(hold.Original.Operation,out var current)&&ReferenceEquals(current,hold)&&
            (hold.Original.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+hold.Original.Operation.ToString("N"))==2f||
             hold.Original.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+hold.Original.Operation.ToString("N"))==3f)&&
            hold.Original.After.Matches(RebirthCamperEnergyActorImage.Capture(hold.Original.Actor));
        internal static PlacedCancellationSavedAfterVersion Capture(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
        {
            if(original==null||!original.OwnerAttempted||!original.PermissionSaved||!placedCancellations.TryGetValue(original.Operation,out var hold)||
                !ReferenceEquals(hold.Original,original)||!hold.Entered||!hold.Current()||
                original.Actor.Buffs.GetCustomVar("RebirthPlacedCancelV1_"+original.Operation.ToString("N"))!=2f||!original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return null;
            return new PlacedCancellationSavedAfterVersion(hold);
        }
        internal static PlacedCancellationSavedAfterVersion CaptureRestart(RebirthRemoteResourcePlacedStationOriginal.CancellationOwnerSavedAfterHydrated proof)
        {
            if(proof?.Current!=true||!placedCancellations.TryGetValue(proof.Original.Operation,out var hold)||!ReferenceEquals(hold.Original,proof.Original)||
                !ReferenceEquals(hold.ReconstructedSavedAfter,proof)||hold.Entered||hold.MutationVersion!=0||!hold.Current())return null;
            return new PlacedCancellationSavedAfterVersion(hold);
        }
    }
    internal static bool ReleasePlacedCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
    {
        if(original==null||!RebirthRemoteResourcePlacedStationOriginal.HasCancellationClientFinalAck(original))return false;
        if(placedCancellations.TryGetValue(original.Operation,out var held))
        {if(!ReferenceEquals(held.Original,original))return false;placedCancellations.Remove(original.Operation);}
        return true;
    }
    internal static void DropEndedPlacedCancellation(RebirthRemoteResourcePlacedStationOriginal.CancellationClient original)
    {
        if(original==null||original.Current())return;
        if(placedCancellations.TryGetValue(original.Operation,out var held)&&ReferenceEquals(held.Original,original))placedCancellations.Remove(original.Operation);
    }
}
