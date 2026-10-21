using System;
using System.Linq;

internal static partial class RebirthConsumptionDurableDelivery
{
    private static bool inventoryProtocolInstalled;
    internal static bool InventoryProtocolActive{get;private set;}
    private static bool SendInventoryPurpose(InventoryOriginal original,byte phase)
    {
        if(!original.Current())return false;
        long now=System.Diagnostics.Stopwatch.GetTimestamp();
        if(original.LastPurpose==phase&&now<original.NextPurpose)return false;
        original.LastPurpose=phase;original.NextPurpose=now+System.Diagnostics.Stopwatch.Frequency;
        if(ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor))
        {
            if(inventoryLocalAdmission!=null)return false;inventoryLocalAdmission=original;
            try{return ApplyInventoryPurpose(original,null,original.World,GameManager.Instance,original.Actor.entityId,phase,original.Purpose);}
            finally{inventoryLocalAdmission=null;}
        }
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var peer=manager?.Clients?.ForEntityId(original.Actor.entityId);
        if(peer?.loginDone!=true||peer.disconnecting||peer.netConnection==null||peer.netConnection.Length==0||peer.netConnection[0]==null||peer.netConnection[0].IsDisconnected())return false;
        if(System.Text.Encoding.UTF8.GetByteCount(original.Purpose)>48000)return QueueInventoryPurpose(original,phase,peer,peer.netConnection[0]);
        var packet=NetPackageManager.GetPackage<NetPackageRebirthNpcConsumptionOwner>().Setup(original.Actor.entityId,phase,original.Purpose);
        if(!original.Current())return false;peer.SendPackage(packet);return true;
    }
    private static void PumpInventoryOriginal(InventoryOriginal original)
    {
        if(!original.Current())return;
        try
        {
            if(!original.BeforeIssued){original.Record("owner-before-intent");original.BeforeIssued=true;}
            if(!ArchiveInventoryOwner(original,"before")){SendInventoryPurpose(original,0);return;}
            if(!original.SourceSaved)
            {
                if(!original.SourceIssued){original.Record("source-intent");original.SourceIssued=true;}
                var commands=original.Inventory.Commands();
                for(int ordinal=0;ordinal<commands.Length;ordinal++)
                {
                    if(ordinal<original.QualifiedPrefix)continue;
                    if(ArchiveInventoryOwner(original,"prefix-"+(ordinal+1))){original.QualifiedPrefix=ordinal+1;continue;}
                    original.Record("source-command-"+ordinal);SendInventoryPurpose(original,(byte)(10+ordinal));return;
                }
                if(!ArchiveInventoryOwner(original,"source")){SendInventoryPurpose(original,8);return;}
            }
            if(original.Committed)
            {
                if(!ArchiveInventoryOwner(original,"retired")){SendInventoryPurpose(original,2);return;}
                original.Record("retired");SendInventoryPurpose(original,3);original.Terminal=true;
                inventoryOriginals.Remove(original.Operation);
                CompleteInventoryNativeLoad(original.Operation);
                ReleaseInventoryOriginalMemory(original);return;
            }
            AdvanceInventoryConsumption(original);
            if(original.DeferredReturned&&original.DeferredStateSaved&&!original.SecondarySaved)
            {
                if(!original.SecondaryEntered){original.Record("owner-secondary-intent");original.SecondaryEntered=true;}
                SendInventoryPurpose(original,4);return;
            }
            if(original.SecondarySaved&&!original.NativeSaved)
            {
                if(!original.NativeEntered){original.Record("owner-use-intent");original.NativeEntered=true;}
                SendInventoryPurpose(original,1);return;
            }
            if(original.Committed)SendInventoryPurpose(original,2);
        }
        catch(Exception error){Log.Warning("[REBIRTH Consumption] original progression retained: "+error.GetType().Name);}
    }
    private static void PumpInventoryOriginals(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread())return;
        // The installed observers preserve old saved originals. Fresh servings
        // use ConsumeAt, so clean worlds have no original work to poll.
        if(inventoryOriginals.Count==0&&inventoryPurposeWire.Count==0&&
            !inventoryNativeLoads.Any(load=>load.Operations.Length>0))return;
        foreach(var stale in inventoryOriginals.Values.Where(value=>!value.Current()).ToArray())
        {stale.ObservationRetired=true;inventoryOriginals.Remove(stale.Operation);ReleaseInventoryOriginalMemory(stale);}
        PumpLoadedInventoryOriginals();
        inventoryPurposeWire.Pump(4);
        foreach(var original in inventoryOriginals.Values.Take(4).ToArray())PumpInventoryOriginal(original);
    }
    internal static void InstallInventoryOriginals()
    {
        if(inventoryProtocolInstalled)return;
        RebirthNpcMetabolismCustody.InstallNativeObservers();RebirthCookingOutputDelivery.Install();
        ModEvents.GameUpdate.RegisterHandler(PumpInventoryOriginals);inventoryProtocolInstalled=true;
    }
    internal static void EnableReviewedInventoryOriginals()
    {
        if(InventoryProtocolActive)return;
        if(GameManager.Instance?.World!=null)throw new InvalidOperationException("Player consumption originals must activate before native world/owner loading.");
        InstallInventoryOriginals();
        if(!inventoryProtocolInstalled||!RebirthCookingOutputDelivery.IsInstalled)throw new InvalidOperationException("Player consumption native owner guards are incomplete.");
        InventoryProtocolActive=true;
    }
}
