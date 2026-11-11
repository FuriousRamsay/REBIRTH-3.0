using System;
using System.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    internal static bool HasRecordedInventoryOperation(EntityPlayer actor,Guid operation)
    {
        if(!InventoryProtocolActive||actor==null||operation==Guid.Empty)return false;
        if(inventoryOriginals.ContainsKey(operation))return true;
        if(inventoryNativeLoads.Any(load=>ReferenceEquals(load.Actor,actor)&&load.Operations.Contains(operation)))return true;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return true;
            return System.IO.Directory.Exists(InventoryOriginalDirectory(GameIO.GetSaveGameDir(),owner.StorageKey,operation));
        }
        catch{return true;}
    }
    internal static void ReceiveInventoryRequest(EntityPlayer actor,ClientInfo peer,Guid operation,RebirthMetabolismSourceKind kind,int slot,int count,byte[] fingerprint,bool autoSip)
    {
        if(!InventoryProtocolActive||operation==Guid.Empty||!ThreadManager.IsMainThread()||RebirthRemoteResourceNetworkScope.Capture(actor)?.Current!=true)return;
        if(inventoryOriginals.TryGetValue(operation,out var retained))
        {
            if(retained.Current()&&ReferenceEquals(retained.Actor,actor)&&retained.Inventory.Kind==kind&&retained.Inventory.Slot==slot&&
                retained.Serving.Expected.count==count&&RebirthConsumptionSourceIdentity.MatchesFingerprint(retained.Serving.Expected.itemValue,fingerprint))PumpInventoryOriginal(retained);
            return;
        }
        if(TryRefuseSavedUnissuedInventoryRequest(actor,operation,kind,slot,count,fingerprint))return;
        var result=new RebirthConsumeResult{Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="The selected item changed before consumption. Try again."};
        var uploaded=peer?.latestPlayerData;
        if(uploaded==null||uploaded.id!=actor.entityId||uploaded.deaths!=actor.Died||uploaded.bDead)
        {Reply();return;}
        var bag=RebirthGearPlayerDataInventory.ReadSlots(uploaded,true,out var bagLocks);
        var belt=RebirthGearPlayerDataInventory.ReadSlots(uploaded,false,out var beltLocks);
        if(bag==null||belt==null||!ReferenceEquals(peer.latestPlayerData,uploaded))
        {Reply();return;}
        var slots=kind==RebirthMetabolismSourceKind.Backpack?bag:kind==RebirthMetabolismSourceKind.HeldToolbelt?belt:null;
        int width=kind==RebirthMetabolismSourceKind.HeldToolbelt?RebirthToolbeltCapacity.GetOwnedSlotCount(actor,slots?.Length??0):slots?.Length??0;
        if(slots==null||slot<0||slot>=width||slots[slot]==null||slots[slot].IsEmpty()||slots[slot].count!=count||!RebirthConsumptionSourceIdentity.MatchesFingerprint(slots[slot].itemValue,fingerprint))
        {Reply();return;}
        if(TryPrepareInventoryOriginal(operation,actor,kind,slot,slots[slot].Clone(),autoSip,bag,belt,bagLocks,beltLocks,out var original,out result))PumpInventoryOriginal(original);
        else Reply();
        void Reply()
        {
            if(RebirthRemoteResourceNetworkScope.Capture(actor)?.Current==true)
                peer?.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(result));
        }
    }
}
