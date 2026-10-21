using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class ReceivedConnection
    {internal INetConnection Connection;internal World World;}
    private static readonly ConditionalWeakTable<NetPackage,ReceivedConnection> receivedConnections=new ConditionalWeakTable<NetPackage,ReceivedConnection>();
    private static bool IsLegacyResourcePackage(NetPackage package)=>package is NetPackageRemoteResourceConsumeRequest||package is NetPackageRemoteResourceConsumeResult;
    private static bool IsPlayerConsumptionRequest(NetPackage package)=>package is NetPackageRebirthMetabolismConsumeRequest;
    private static bool IsCookingResourcePackage(NetPackage package)=>package is NetPackageRebirthCookingLegacyPullOriginal||package is NetPackageRebirthCookingLegacyPulledOriginal||
        package is NetPackageRebirthCookingLegacyPullNoPayment||package is NetPackageRebirthCookingGridOwnerWitness||package is NetPackageRebirthCookingGridOwnerWitnessResult;
    private static bool IsObservedServerPurposePackage(NetPackage package)=>RebirthCamperRemovalParcelTransport.IsPacket(package)||IsPlayerConsumptionRequest(package)||package is NetPackageRebirthNpcConsumptionOwnerReturn||IsLegacyResourcePackage(package)||IsCookingResourcePackage(package)||RebirthRecyclerPlacedSettlement.IsRecyclerPurposePackage(package)||RebirthLogisticsOriginalTransport.IsPurposePackage(package)||RebirthRemoteResourceNetworkScope.IsPacket(package);
    private static bool IsObservedPurposePackage(NetPackage package)=>RebirthCamperRemovalParcelTransport.IsPacket(package)||IsPlayerConsumptionRequest(package)||IsLegacyResourcePackage(package)||IsCookingResourcePackage(package)||IsHydrationEnvelope(package)||package is NetPackageRebirthNpcConsumptionOwner||package is NetPackageRebirthNpcConsumptionOwnerReturn||package is NetPackageRebirthDogHireOwnerGrant||package is NetPackageRebirthActionPaymentOwnerGrant||package is NetPackageRebirthUiQueueInsertionRequestV1||package is NetPackageRebirthUiQueueInsertionReplyV1||RebirthRemoteResourcePlacedStationOriginal.IsCancellationJoinPacket(package)||RebirthProductiveEarnedUiCoordinator.IsPendingQueueHandoffNative(package)||RebirthRecyclerPlacedSettlement.IsRecyclerPurposePackage(package)||RebirthLogisticsOriginalTransport.IsPurposePackage(package)||RebirthRemoteResourceNetworkScope.IsPacket(package);
    internal static void ForgetReceivedConnection(NetPackage package)
    {if(package!=null)receivedConnections.Remove(package);}
    internal static bool TryGetReceivedConnection(NetPackage package,out INetConnection connection)
    {
        connection=null;
        if(!ThreadManager.IsMainThread()||package==null||!IsObservedPurposePackage(package)||
            !receivedConnections.TryGetValue(package,out var source)||source.World==null||
            !ReferenceEquals(source.World,GameManager.Instance?.World))return false;
        connection=source.Connection;return connection!=null;
    }
    [HarmonyPatch(typeof(NetConnectionAbs),nameof(NetConnectionAbs.GetPackages))]
    private static class ReceiveDequeue
    {
        private sealed class Read
        {internal NetConnectionAbs Connection;internal NetPackage[] Before;internal bool Locked,RecyclerServer;internal World World;}
        private static void Prefix(NetConnectionAbs __instance,bool __runOriginal,out Read __state)
        {
            __state=null;
            if(!__runOriginal||!ThreadManager.IsMainThread())return;
            var read=new Read{Connection=__instance,World=GameManager.Instance?.World,RecyclerServer=__instance.cInfo!=null};
            try
            {
                Monitor.Enter(__instance.receivedPackages);read.Locked=true;
                if(!(read.RecyclerServer?__instance.receivedPackages.Any(IsObservedServerPurposePackage):__instance.receivedPackages.Any(IsObservedPurposePackage)))
                {read.Locked=false;Monitor.Exit(__instance.receivedPackages);return;}
                read.Before=__instance.receivedPackages.ToArray();__state=read;
            }
            catch{if(read.Locked)Monitor.Exit(__instance.receivedPackages);}
        }
        private static void Postfix(List<NetPackage> _dstBuf,bool __runOriginal,Read __state)
        {
            if(!__runOriginal||__state==null||!__state.Locked||_dstBuf==null||_dstBuf.Count!=__state.Before.Length||
                __state.Connection.receivedPackages.Count!=0)return;
            for(int index=0;index<_dstBuf.Count;index++)if(!ReferenceEquals(_dstBuf[index],__state.Before[index]))return;
            foreach(var package in _dstBuf)
            {
                if(!(__state.RecyclerServer?IsObservedServerPurposePackage(package):IsObservedPurposePackage(package)))continue;
                receivedConnections.Remove(package);
                receivedConnections.Add(package,new ReceivedConnection{Connection=__state.Connection,World=__state.World});
            }
        }
        private static Exception Finalizer(Exception __exception,Read __state)
        {if(__state?.Locked==true){__state.Locked=false;Monitor.Exit(__state.Connection.receivedPackages);}return __exception;}
    }
}
