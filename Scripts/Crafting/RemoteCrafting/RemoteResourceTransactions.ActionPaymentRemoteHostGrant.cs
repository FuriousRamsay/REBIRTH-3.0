using System;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private static readonly RebirthRemoteResourceRepairSendQueue actionGrantWire=new RebirthRemoteResourceRepairSendQueue();
    private static bool ActionRemoteGrantCurrent(ActionPayment payment,byte phase)
    {
        if(phase==5||phase==6)return ActionRemoteMetadataCurrent(payment)&&!payment.RemoteTerminalNativeSaved&&
            (phase==5?payment.State==ActionPaymentState.Used:payment.State==ActionPaymentState.Returned);
        if(payment?.ActorCurrent()!=true||!RemoteActionOriginalCurrent(payment.RemoteOwner)||payment.Original==null)return false;
        if(phase==0)return payment.State==ActionPaymentState.Paying&&payment.RemoteDebitEntered&&!payment.OwnerAfterSaved;
        if(phase==1)return payment.State==ActionPaymentState.Returning&&payment.RemoteRefundEntered&&!payment.OwnerReturnSaved&&!payment.PublicationEntered;
        if(phase==3)return payment.RemoteDebitEntered&&!payment.OwnerAfterSaved&&!payment.RemoteRefundEntered;
        if(phase==4)return payment.RemoteRefundEntered&&!payment.OwnerReturnSaved;
        return false;
    }
    private static bool QueueActionRemoteGrant(ActionPayment payment,byte phase)
    {
        if(!ActionRemoteGrantCurrent(payment,phase))return false;
        string key="electrical-owner-grant:"+payment.Operation.ToString("N")+":"+phase;
        if(actionGrantWire.IsQueued(key))return true;
        if(System.Diagnostics.Stopwatch.GetTimestamp()<payment.RemoteGrantNext)return false;
        if(payment.RemoteGrantBody==null)
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(ActionRemoteEnvelopeRetainedBytes(payment.Original,payment.RemoteOwner.NativeBefore),out var retained))return false;
            try
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return false;
                using(scratch)payment.RemoteGrantBody=new RebirthRemoteResourceFragments.Body(EncodeActionRemoteEnvelope(payment.Original,payment.RemoteOwner.NativeBefore));
                payment.RemoteGrantMemory=retained;retained=null;
            }
            finally{retained?.Dispose();}
        }
        var peer=payment.RemoteOwner.Peer;var connection=payment.RemoteOwner.Connection;var body=payment.RemoteGrantBody;
        bool Current()=>ActionRemoteGrantCurrent(payment,phase)&&ReferenceEquals(peer,payment.RemoteOwner.Peer)&&ReferenceEquals(connection,peer.netConnection?[0]);
        bool Ready()=>Current()&&!connection.IsDisconnected()&&(connection is NetConnectionSimple||connection is NetConnectionSteam);
        if(!actionGrantWire.Enqueue(key,body,Current,Ready,index=>peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthActionPaymentOwnerGrant>().Setup(payment.Actor.entityId,payment.Operation,phase,body,index))))return false;
        payment.RemoteGrantNext=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;return true;
    }
}
