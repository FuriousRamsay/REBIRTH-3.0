using System;
using System.Linq;
using System.Collections.Generic;

public static partial class RemoteResourceTransactions
{
    private static bool ActionRemoteMetadataCurrent(ActionPayment payment)
    {
        if(!ThreadManager.IsMainThread()||payment==null||!payment.Terminal||payment.Retired||payment.Original==null||payment.RemoteOwner==null||
            !ReferenceEquals(GameManager.Instance?.World,payment.World)||!ReferenceEquals(payment.World?.GetEntity(payment.Actor.entityId),payment.Actor)||payment.Actor.IsDead()||payment.Actor.Died<payment.Deaths||
            RebirthCamperCharacterAccess.CurrentCreation(payment.Actor)!=payment.Creation||payment.World.worldState.Guid!=payment.NativeWorld||payment.Save!=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(payment.Actor,out var owner)||owner.CanonicalId!=payment.Account||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(payment.Actor.entityId),payment.RemoteOwner.Peer)||
            !ReferenceEquals(payment.RemoteOwner.Peer?.netConnection?[0],payment.RemoteOwner.Connection))return false;
        // Historical full native receipts, not current inventory/target equality,
        // establish the already-positive financial outcome. This path can only
        // release metadata; it cannot issue a debit/refund/publication permit.
        if(payment.State==ActionPaymentState.Used)return payment.Events.ContainsKey("used")&&payment.Events.ContainsKey(payment.TrainingRequired?"training-saved":"training-not-required")&&payment.PublicationReturned&&payment.ServiceAfterSaved&&payment.TargetAfterSaved&&payment.OwnerAfterSaved&&payment.Legs.All(leg=>leg.State==ActionLegState.Debited&&leg.AfterSaved);
        return payment.State==ActionPaymentState.Returned&&payment.Events.ContainsKey("returned")&&!payment.PublicationEntered&&payment.OwnerReturnSaved&&
            payment.Legs.All(leg=>leg.BeforeSaved&&(leg.State==ActionLegState.Before||leg.State==ActionLegState.Refused||leg.State==ActionLegState.Returned&&leg.ReturnSaved));
    }
    private sealed class ActionTerminalCourier
    {
        internal Guid Operation;internal World World;internal EntityPlayer Actor;internal ClientInfo Peer;internal INetConnection Connection;
        internal string Account,Creation,NativeWorld,Save;internal int Deaths;internal byte Phase=7;internal long Next;internal RebirthRemoteResourceFragments.Body Body;internal IDisposable Memory;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(World?.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&Actor.Died>=Deaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(Actor,out var owner)&&owner.CanonicalId==Account&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&ReferenceEquals(Peer.netConnection?[0],Connection)&&World.worldState.Guid==NativeWorld&&Save==System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
    }
    private static readonly Dictionary<Guid,ActionTerminalCourier> actionTerminalCouriers=new Dictionary<Guid,ActionTerminalCourier>();
    private static string ActionTerminalCourierKey(Guid operation)=>"electrical-owner-terminal-ack:"+operation.ToString("N");
    private static void DropActionTerminalCourier(ActionTerminalCourier courier)
    {
        actionGrantWire.Cancel(ActionTerminalCourierKey(courier.Operation));actionTerminalCouriers.Remove(courier.Operation);
        courier.Body=null;courier.Actor=null;courier.World=null;courier.Peer=null;courier.Connection=null;courier.Memory?.Dispose();courier.Memory=null;
    }
    private static void PumpActionTerminalCouriers()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var courier in actionTerminalCouriers.Values.ToArray())
        {
            if(!ReferenceEquals(GameManager.Instance?.World,courier.World)||courier.World?.worldState.Guid!=courier.NativeWorld||
                !ReferenceEquals(courier.World?.GetEntity(courier.Actor.entityId),courier.Actor)||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(courier.Actor.entityId),courier.Peer)||
                !ReferenceEquals(courier.Peer.netConnection?[0],courier.Connection))
            {DropActionTerminalCourier(courier);continue;}
            if(!courier.Current()||System.Diagnostics.Stopwatch.GetTimestamp()<courier.Next)continue;
            if(courier.Phase!=7&&ActionCourierHasSavedTerminal(courier)){actionGrantWire.Cancel(ActionTerminalCourierKey(courier.Operation));courier.Phase=7;courier.Next=0;}
            string key=ActionTerminalCourierKey(courier.Operation);if(actionGrantWire.IsQueued(key))continue;
            byte phase=courier.Phase;
            bool Ready()=>courier.Current()&&courier.Phase==phase&&!courier.Connection.IsDisconnected()&&(courier.Connection is NetConnectionSimple||courier.Connection is NetConnectionSteam);
            if(actionGrantWire.Enqueue(key,courier.Body,courier.Current,Ready,index=>courier.Peer.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthActionPaymentOwnerGrant>().Setup(courier.Actor.entityId,courier.Operation,phase,courier.Body,index))))
                courier.Next=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;
        }
    }
    private static bool ActionCourierHasSavedTerminal(ActionTerminalCourier courier)
    {
        try
        {
            var accepted=courier.Peer?.latestPlayerData;
            if(!courier.Current()||accepted==null||accepted.id!=courier.Actor.entityId||accepted.deaths!=courier.Actor.Died||accepted.bDead||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(accepted,ActionOwnerMarker(courier.Operation),out float acceptedPhase,out bool acceptedPresent)||!acceptedPresent||acceptedPhase!=6f||
                !RebirthStablePlayerIdentity.TryFromClientInfo(courier.Peer,out var identity)||identity.CanonicalId!=courier.Account||
                !RebirthGearNativePlayerFile.TryRead(identity,out var saved)||saved.id!=accepted.id||saved.deaths!=accepted.deaths||saved.bDead||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,ActionOwnerMarker(courier.Operation),out float savedPhase,out bool savedPresent)||!savedPresent||savedPhase!=6f)return false;
            return ActionOwnerImage(saved).SequenceEqual(ActionOwnerImage(accepted))&&courier.Current()&&ReferenceEquals(courier.Peer.latestPlayerData,accepted);
        }
        catch{return false;}
    }
    private static void ObserveAcceptedActionTerminalCouriers(ClientInfo peer,PlayerDataFile data,bool ran)
    {
        if(!ran||!ThreadManager.IsMainThread()||peer==null||data==null||!ReferenceEquals(peer.latestPlayerData,data)||data.id!=peer.entityId)return;
        foreach(var courier in actionTerminalCouriers.Values.ToArray())
        {
            if(courier.Phase!=7||!ReferenceEquals(courier.Peer,peer)||!courier.Current()||data.id!=courier.Actor.entityId||data.deaths<courier.Deaths)continue;
            // The financial outcome and saved native phase6 already preceded
            // this courier. Actual accepted native removal of its own metadata
            // stamp only retires delivery allocation; it authorizes no effect.
            if(RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(data,ActionOwnerMarker(courier.Operation),out _,out bool present)&&!present)
            {
                DropActionTerminalCourier(courier);
                foreach(var loaded in SnapshotActionHostOwnerLoads().Where(value=>ReferenceEquals(value.Spawn?.Peer,peer)))
                    if(RebirthRemoteResourcePlacedStationSavedReceipt.TryActionPaymentKeys(loaded.Buffs,out var operations)&&operations.Length>0&&operations.All(loaded.TerminalMetadataDone.Contains)&&
                        !actionRemoteHostOriginals.Values.Any(value=>ReferenceEquals(value.RestartNativeSource,loaded)))DropActionHostOwnerLoad(loaded);
            }
        }
    }
    private static bool FinishRemoteActionTerminal(ActionPayment payment)
    {
        if(payment.RemoteTerminalReleased)return true;
        if(!ActionRemoteMetadataCurrent(payment))return false;
        QueueActionRemoteGrant(payment,payment.State==ActionPaymentState.Used?(byte)5:(byte)6);
        if(!SaveActionOwner(payment,"terminal"))return false;
        payment.RemoteTerminalNativeSaved=true;
        if(actionTerminalCouriers.Count>=8)return false;
        var body=payment.RemoteGrantBody;if(body==null||payment.RemoteGrantMemory==null)return false;
        var courier=new ActionTerminalCourier{Operation=payment.Operation,World=payment.World,Actor=payment.Actor,Peer=payment.RemoteOwner.Peer,Connection=payment.RemoteOwner.Connection,
            Account=payment.Account,Creation=payment.Creation,Deaths=payment.Deaths,NativeWorld=payment.NativeWorld,Save=payment.Save,Body=body,Memory=payment.RemoteGrantMemory};
        actionTerminalCouriers.Add(courier.Operation,courier);
        // The courier now owns the full immutable bytes/allocation. The owning
        // financial graph may retire after actual native saved phase6. Delivery
        // loss resends this exact immutable original until an actual accepted
        // native successor removes its stamp. No setter is retried here.
        payment.RemoteGrantBody=null;payment.RemoteGrantMemory=null;payment.RemoteTerminalReleased=true;
        var original=payment.RemoteOwner;original.Retired=true;lock(actionOwnerGate)if(original.Workers==0)DropRetiredActionRemoteHost(original);
        return true;
    }
}
