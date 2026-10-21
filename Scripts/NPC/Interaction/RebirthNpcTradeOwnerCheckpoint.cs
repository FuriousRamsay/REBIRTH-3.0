using System;
using System.Linq;

// Native saved preparation only. Stage 1 is not payment, stock reservation or
// owner After. This class contains no inventory setter and no NPC mutation.
internal static class RebirthNpcTradeOwnerCheckpoint
{
    internal const string Held="rbNpcTradeHeld",HeldLife="rbNpcTradeHeldLife";
    internal static string HoldMarker(Guid operation)=>"rbNpcTradeHold_"+operation.ToString("N");
    internal static string Marker(Guid operation)=>"rbNpcTrade_"+operation.ToString("N");
    internal static bool TrySaveBefore(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
    {
        try
        {
            if(!BeforeCurrent(player,original)||player.Buffs?.CVars==null)return false;
            string marker=Marker(original.Operation);
            if(player.Buffs.CVars.TryGetValue(marker,out var phase))
            {if(phase!=1f)return false;}
            else player.Buffs.SetCustomVar(marker,1f,true);
            player.Buffs.SetCustomVar(Held,1f,true);player.Buffs.SetCustomVar(HeldLife,original.Deaths,true);
            player.Buffs.SetCustomVar(HoldMarker(original.Operation),1f,true);
            var game=GameManager.Instance;var world=player.world;var bag=player.bag;var belt=player.inventory;
            bool conflictingSavedStage=false;
            bool Current()=>ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(world,player.world)&&
                ReferenceEquals(bag,player.bag)&&ReferenceEquals(belt,player.inventory)&&BeforeCurrent(player,original)&&
                player.Buffs.CVars.TryGetValue(marker,out var value)&&value==1f;
            bool Saved()
            {
                if(!Current()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=original.Account||
                    !RebirthGearNativePlayerFile.TryRead(owner,out var native))return false;
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,marker,out var value,out bool present)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,RebirthNpcTradeOwnerExchange.Step(original.Operation),out float savedStep,out bool stepPresent))
                {conflictingSavedStage=true;return false;}
                if(present&&value!=1f||stepPresent&&savedStep!=0f){conflictingSavedStage=true;return false;}
                if(native.id!=player.entityId||native.deaths!=original.Deaths||native.bDead||!present||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,Held,out float held,out bool hasHeld)||!hasHeld||held!=1f||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,HeldLife,out float life,out bool hasLife)||!hasLife||life!=original.Deaths||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(native,HoldMarker(original.Operation),out float ownHold,out bool hasOwnHold)||!hasOwnHold||ownHold!=1f||
                    !original.MatchesSlots(RebirthGearPlayerDataInventory.ReadSlots(native,true),RebirthGearPlayerDataInventory.ReadSlots(native,false),false))return false;
                return Current();
            }
            if(Saved())return true;
            if(conflictingSavedStage)return false;
            if(!Current())return false;
            game.SaveLocalPlayerData();
            return Saved();
        }
        catch{return false;}
    }
    private static bool BeforeCurrent(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
    {
        if(!ThreadManager.IsMainThread()||!RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original)||
            !original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false)||
            player.Buffs.GetCustomVar(RebirthNpcTradeOwnerExchange.Step(original.Operation))!=0f)return false;
        return PolicyCurrent(player,original);
    }
    private static bool PolicyCurrent(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
    {
        if(RebirthToolbeltCapacity.GetOwnedSlotCount(player,player.inventory.ItemGrid.items.Length)!=original.OwnedBeltSlots)return false;
        var bag=original.CopyLocks(true);var belt=original.CopyLocks(false);
        return bag.Length==player.bag.ItemGrid.items.Length&&belt.Length==player.inventory.ItemGrid.items.Length&&
            bag.Select((locked,slot)=>locked==RebirthVehicleNativePolicy.IsLocked(player,true,slot)).All(value=>value)&&
            belt.Select((locked,slot)=>locked==(slot>=original.OwnedBeltSlots||RebirthVehicleNativePolicy.IsLocked(player,false,slot))).All(value=>value);
    }
    internal static bool TryReadRetainedStage(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out int step,out int phase)
    {
        step=phase=0;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original)||!PolicyCurrent(player,original)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.CanonicalId!=original.Account||
                !RebirthGearNativePlayerFile.TryRead(owner,out var saved)||saved.bDead||saved.id!=player.entityId||saved.deaths!=original.Deaths||
                !RebirthNpcTradeService.OwnerAdmission.MatchesNativePolicy(saved,original,player)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,Marker(original.Operation),out float nativePhase,out bool present)||!present||
                nativePhase!=1f&&nativePhase!=2f&&nativePhase!=3f||player.Buffs.GetCustomVar(Marker(original.Operation))!=nativePhase||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthNpcTradeOwnerExchange.Step(original.Operation),out float nativeStep,out bool stepPresent))return false;
            if(!stepPresent)nativeStep=0f;
            if(float.IsNaN(nativeStep)||float.IsInfinity(nativeStep)||nativeStep<0||nativeStep>378||nativeStep!=Math.Floor(nativeStep)||
                ((int)nativeStep&1)!=0||player.Buffs.GetCustomVar(RebirthNpcTradeOwnerExchange.Step(original.Operation))!=nativeStep)return false;
            int nextStep=(int)nativeStep;
            if(nativePhase!=1f&&nextStep!=RebirthNpcTradePrefix.Changes(original)*2||
                !RebirthNpcTradePrefix.Matches(original,player.bag.ItemGrid.items,player.inventory.ItemGrid.items,nextStep)||
                !RebirthNpcTradePrefix.Matches(original,RebirthGearPlayerDataInventory.ReadSlots(saved,true),RebirthGearPlayerDataInventory.ReadSlots(saved,false),nextStep))return false;
            step=nextStep;phase=(int)nativePhase;return true;
        }
        catch{return false;}
    }
}
