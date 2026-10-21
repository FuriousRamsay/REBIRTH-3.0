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
            string root=System.IO.Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","PlayerConsumption");
            if(!System.IO.Directory.Exists(root))return false;
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return true;
            return System.IO.Directory.Exists(System.IO.Path.Combine(root,owner.StorageKey,operation.ToString("N")));
        }
        catch{return true;}
    }
    internal static void ReceiveInventoryRequest(EntityPlayer actor,Guid operation,RebirthMetabolismSourceKind kind,int slot,int count,byte[] fingerprint,bool autoSip)
    {
        if(!InventoryProtocolActive||operation==Guid.Empty||!ThreadManager.IsMainThread()||RebirthRemoteResourceNetworkScope.Capture(actor)?.Current!=true)return;
        if(inventoryOriginals.TryGetValue(operation,out var retained))
        {
            if(retained.Current()&&ReferenceEquals(retained.Actor,actor)&&retained.Inventory.Kind==kind&&retained.Inventory.Slot==slot&&
                retained.Serving.Expected.count==count&&RebirthConsumptionSourceIdentity.MatchesFingerprint(retained.Serving.Expected.itemValue,fingerprint))PumpInventoryOriginal(retained);
            return;
        }
        if(TryRefuseSavedUnissuedInventoryRequest(actor,operation,kind,slot,count,fingerprint))return;
        var slots=kind==RebirthMetabolismSourceKind.Backpack?actor.bag?.ItemGrid?.items:kind==RebirthMetabolismSourceKind.HeldToolbelt?actor.inventory?.ItemGrid?.items:null;
        int width=kind==RebirthMetabolismSourceKind.HeldToolbelt?RebirthToolbeltCapacity.GetOwnedSlotCount(actor,slots?.Length??0):slots?.Length??0;
        if(slots==null||slot<0||slot>=width||slots[slot]==null||slots[slot].IsEmpty()||slots[slot].count!=count||!RebirthConsumptionSourceIdentity.MatchesFingerprint(slots[slot].itemValue,fingerprint))return;
        if(TryPrepareInventoryOriginal(operation,actor,kind,slot,slots[slot].Clone(),autoSip,out var original,out var result))PumpInventoryOriginal(original);
        else
        {
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(manager?.IsServer==true)manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>().Setup(result),_attachedToEntityId:actor.entityId);
        }
    }
}
