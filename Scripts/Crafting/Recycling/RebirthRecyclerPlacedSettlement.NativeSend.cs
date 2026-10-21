using System;
using System.Linq;
using System.Runtime.CompilerServices;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private sealed class RecyclerAutomaticSetup {internal RecyclerClient Client;internal bool Ran;}
    private static readonly ConditionalWeakTable<NetPackageTileEntity,RecyclerAutomaticSetup> recyclerAutomaticSetups=new ConditionalWeakTable<NetPackageTileEntity,RecyclerAutomaticSetup>();
    internal static void BeginRecyclerNativeSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode)
    {
        if(packet==null)return;recyclerAutomaticSetups.Remove(packet);
        if(!ThreadManager.IsMainThread()||mode!=StreamModeWrite.ToServer||tile is not TileEntityWorkstation station)return;
        var client=recyclerClients.SingleOrDefault(value=>ReferenceEquals(value.Source?.Station,station)&&ReferenceEquals(value.Source.World,GameManager.Instance?.World)&&!value.Acknowledged);
        if(client!=null)recyclerAutomaticSetups.Add(packet,new RecyclerAutomaticSetup{Client=client});
    }
    internal static void ObserveRecyclerNativeSetup(NetPackageTileEntity packet,bool run)
    {if(packet!=null&&recyclerAutomaticSetups.TryGetValue(packet,out var setup))setup.Ran=run;}
    internal static void ForgetRecyclerNativeSetup(NetPackage packet)
    {if(packet is NetPackageTileEntity native)recyclerAutomaticSetups.Remove(native);}
    internal static bool TrySuppressRecyclerNativeSend(NetPackage packet,bool run)
    {
        if(!run||!ThreadManager.IsMainThread())return false;
        if(packet is NetPackageTileEntity native&&recyclerAutomaticSetups.TryGetValue(native,out var setup))
        {
            var client=setup.Client;
            bool selected=ReferenceEquals(sendingRecyclerStation,native)&&setup.Ran&&client.Live()&&client.Applied&&!client.Unknown&&client.Owner?.BeforeCheckpointSaved==true&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(client.Source.Station,client.Source.AfterNative,client.Source.AfterOffset);
            if(selected)return false;
            // This actual Setup was made from the held native TE. An automatic
            // packet cannot cross its before/after barrier, including a partial
            // native outcome. Unrelated native Setup calls remain untouched.
            recyclerAutomaticSetups.Remove(native);if(native.inSendQueuesCount==0)NetPackageManager.FreePackage(native);return true;
        }
        if(packet is NetPackagePlayerData owner&&owner.playerDataFile!=null&&recyclerClients.Any(value=>!value.Acknowledged&&value.Source?.OwnerLineageCurrent()==true&&
            value.Source.Actor.entityId==owner.playerDataFile.id&&value.Source.Deaths==owner.playerDataFile.deaths))
        {
            if(CanSendFinalAutomaticRecyclerOwner(owner))return false;
            CaptureAutomaticRecyclerOwner(owner);
            if(owner.inSendQueuesCount==0)NetPackageManager.FreePackage(owner);return true;
        }
        return false;
    }
}
