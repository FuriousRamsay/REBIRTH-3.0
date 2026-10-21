using System;
using System.Collections.Generic;

public static partial class RebirthNpcNetworkEpoch
{
    internal static uint CaptureOutgoingNativeEpoch(RebirthNpcSaveQuarantineScope scope)
    {lock(Sync)return ReferenceEquals(serverWorld,scope.World)?activeServerEpoch:0;}
    internal static bool IncomingNativeEpochCurrent(World incoming,uint original)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(incoming,GameManager.Instance?.World)||incoming?.IsRemote()!=false)return false;
        uint epoch=GetServerEpoch();
        lock(Sync)return epoch!=0&&epoch!=original&&CurrentServerWorld()&&ReferenceEquals(serverWorld,incoming)&&activeServerEpoch==epoch;
    }
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(clientWorld!=null||clientTransport!=null||clientManager!=null||clientEpoch!=0||clientConnectionEpoch!=0||
                serverWorld!=null&&!ReferenceEquals(serverWorld,scope.World))return null;
            var world=serverWorld;var manager=serverManager;var original=ServerConnectionEpochs;
            uint epoch=activeServerEpoch,next=nextServerEpoch,connection=nextConnectionEpoch;
            var saved=new Dictionary<int,uint>(original);
            bool Current(){lock(Sync){if(!ReferenceEquals(serverWorld,world)||!ReferenceEquals(serverManager,manager)||
                !ReferenceEquals(ServerConnectionEpochs,original)||activeServerEpoch!=epoch||nextServerEpoch!=next||nextConnectionEpoch!=connection||original.Count!=saved.Count)return false;
                foreach(var pair in saved)if(!original.TryGetValue(pair.Key,out uint value)||value!=pair.Value)return false;return true;}}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)if(ReferenceEquals(ServerConnectionEpochs,original)||ReferenceEquals(serverWorld,scope.World))return false;
                // Existing native role producer issues a fresh process-monotone
                // world epoch. No old client/connection token is copied.
                return IncomingNativeEpochCurrent(incoming,epoch);
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthNpcNetworkBaselineService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,baselineScope);
            if(!HasNoPendingBaseline)return null;
            var pending=Pending;var queue=FairOrder;var enqueued=Enqueued;var connections=Connections;var original=baselineScope;
            var peers=new Dictionary<int,PeerConnection>(Connections);var epochs=new Dictionary<int,uint>();var sequences=new Dictionary<int,ulong>();
            foreach(var pair in peers){if(pair.Value==null)return null;epochs[pair.Key]=pair.Value.Epoch;sequences[pair.Key]=pair.Value.Sequence;}
            uint oldEpoch=RebirthNpcNetworkEpoch.CaptureOutgoingNativeEpoch(scope);
            bool Current(){lock(Sync){if(!ReferenceEquals(Pending,pending)||!ReferenceEquals(FairOrder,queue)||!ReferenceEquals(Enqueued,enqueued)||
                !ReferenceEquals(Connections,connections)||!ReferenceEquals(baselineScope,original)||!HasNoPendingBaseline||Connections.Count!=peers.Count)return false;
                foreach(var pair in peers)if(!Connections.TryGetValue(pair.Key,out var peer)||!ReferenceEquals(peer,pair.Value)||peer.Epoch!=epochs[pair.Key]||peer.Sequence!=sequences[pair.Key])return false;return true;}}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                if(!RebirthNpcNetworkEpoch.IncomingNativeEpochCurrent(incoming,oldEpoch))return false;
                lock(Sync)
                {
                    if(ReferenceEquals(Connections,connections)||ReferenceEquals(Pending,pending))return false;
                    if(baselineScope==null&&(Pending.Count!=0||FairOrder.Count!=0||Enqueued.Count!=0||Connections.Count!=0))return false;
                    // New native peers still enter the ordinary baseline queue
                    // and authenticate actual header ACKs independently.
                    return UseBaselineScope()&&ReferenceEquals(baselineScope.World,incoming);
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

internal static partial class RebirthNpcEquipmentProjectionReceipts
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        RebirthNpcAncillaryOriginalScope.RequireScope(scope,receiptScope);
        var records=receipts;var original=receiptScope;string image=ReceiptImage().OuterXml;
        uint oldEpoch=RebirthNpcNetworkEpoch.CaptureOutgoingNativeEpoch(scope);
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(receipts,records)&&ReferenceEquals(receiptScope,original)&&ReceiptImage().OuterXml==image;
        bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
        {
            if(!RebirthNpcNetworkEpoch.IncomingNativeEpochCurrent(incoming,oldEpoch)||ReferenceEquals(receipts,records))return false;
            if(receiptScope==null&&receipts.Count!=0)return false;
            // Bind an observation namespace only. The receipt dictionary stays
            // empty until the actual authenticated semantic-equipment ACK path.
            return RebirthNpcAncillaryOriginalScope.Use(ref receiptScope,receipts.Count==0)&&ReferenceEquals(receiptScope.World,incoming);
        }
        return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
    }
}

internal static partial class RebirthNpcRecoveryProjectionNetwork
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(!ThreadManager.IsMainThread()||context!=null&&(!ReferenceEquals(context,scope.World)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,scope.Directory)))return null;
        var deliveries=Deliveries;var pending=Pending;var names=Names;var world=context;string save=directory,image=QuarantineMailboxImage().OuterXml;
        uint oldEpoch=RebirthNpcNetworkEpoch.CaptureOutgoingNativeEpoch(scope);
        bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(Deliveries,deliveries)&&ReferenceEquals(Pending,pending)&&ReferenceEquals(Names,names)&&
            ReferenceEquals(context,world)&&directory==save&&QuarantineMailboxImage().OuterXml==image;
        bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
        {
            if(!RebirthNpcNetworkEpoch.IncomingNativeEpochCurrent(incoming,oldEpoch)||ReferenceEquals(Deliveries,deliveries)||ReferenceEquals(Pending,pending)||ReferenceEquals(Names,names))return false;
            if(context!=null)return ReferenceEquals(context,incoming)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir());
            if(Deliveries.Count!=0||Pending.Count!=0||Names.Count!=0)return false;
            Scope(incoming);
            // No old challenge, ACK, probe, name floor or sequence grants new
            // convergence. Existing Poll/Receive/Acknowledge own future proofs.
            return ReferenceEquals(context,incoming)&&clientEpoch==0&&clientConnection==0;
        }
        return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
    }
}
