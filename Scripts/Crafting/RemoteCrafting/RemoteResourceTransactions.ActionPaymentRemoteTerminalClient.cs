using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionRemoteTerminalReceipt
    {
        internal Guid Operation;internal World World;internal EntityPlayerLocal Actor;internal INetConnection Connection;
        internal string Account,Creation,Original,NativeWorld;internal int Deaths;internal byte Outcome;internal long Next;
        internal RebirthRemoteResourceFragments.Body Request;internal IDisposable Memory;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==true&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&
            !Actor.IsDead()&&Actor.Died>=Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)&&owner.CanonicalId==Account&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],Connection)&&World.worldState.Guid==NativeWorld;
    }
    private static readonly Dictionary<Guid,ActionRemoteTerminalReceipt> actionRemoteTerminalReceipts=new Dictionary<Guid,ActionRemoteTerminalReceipt>();
    private static string ActionRemoteRequestKey(Guid operation)=>"electrical-owner-click:"+operation.ToString("N");
    private static void FinishActionRemoteTerminal(ActionRemoteClick click,byte phase)
    {
        if(click?.OwnerMetadataCurrent()!=true||click.Plan==null||phase==5&&!click.Debited&&!click.Plan.NoOwnerContribution||phase==6&&!click.Refunded&&!click.DebitRefused&&!click.Plan.NoOwnerContribution||phase!=5&&phase!=6||
            actionRemoteTerminalReceipts.Count>=8||click.RequestBody==null)return;
        long retained=checked((long)click.RequestBody.Length+2L*click.Plan.Original.Length+2L*(click.Account.Length+click.Creation.Length+click.NativeWorld.Length)+8192);
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(retained,out var memory))return;
        var receipt=new ActionRemoteTerminalReceipt{Operation=click.Operation,World=click.World,Actor=click.Actor,Connection=click.Connection,Account=click.Account,Creation=click.Creation,
            Deaths=click.Deaths,NativeWorld=click.NativeWorld,Original=click.Plan.Original,Outcome=phase,Request=click.RequestBody,Memory=memory};
        try
        {
            if(!SaveActionClientTerminal(click,phase))return;
            // Positive host terminal + this private actual debit/refund/no-run
            // original permits metadata release only. Never install host items.
            click.Actor.Buffs.SetCustomVar(ActionOwnerMarker(click.Operation),6f,true);click.Actor.Buffs.SetCustomVar("rbActionPaymentHeld",0f,true);
            PublishActionRemoteNative(click);
            ReleaseActionClientLoadedHold(click);
            actionRemoteTerminalReceipts.Add(receipt.Operation,receipt);memory=null;
            var serviceSnapshot=click.Plan.ServiceSnapshot;
            actionRequestWire.Cancel(ActionRemoteRequestKey(click.Operation));
            lock(actionOwnerGate)
            {
                if(!ReferenceEquals(actionRemoteClick,click))throw new InvalidOperationException("Electrical terminal original changed");
                actionRemoteClick=null;
                ReleaseActionClientCheckpoint(click.InitialCheckpoint);ReleaseActionClientCheckpoint(click.DebitCheckpoint);ReleaseActionClientCheckpoint(click.RefundCheckpoint);
                ReleaseActionClientCheckpoint(click.DebitOutcome);ReleaseActionClientCheckpoint(click.RefundOutcome);
                click.DebitOutcome=null;click.RefundOutcome=null;
                click.InitialCheckpoint=null;click.DebitCheckpoint=null;click.RefundCheckpoint=null;
                click.NativeBefore=null;click.RequestBody=null;click.Plan=null;click.Price=null;
                click.BagBefore=null;click.BeltBefore=null;click.BagReferences=null;click.BeltReferences=null;click.AppliedBagReferences=null;click.AppliedBeltReferences=null;
                click.Bag=null;click.Belt=null;click.Actor=null;click.Target=null;click.World=null;click.Connection=null;click.Original=null;
                click.GrantMemory?.Dispose();click.GrantMemory=null;click.Memory.Dispose();click.Memory=null;
            }
            try
            {
                if(phase==5&&serviceSnapshot!=null)RebirthInfrastructureWorkService.ReceiveClientSnapshot(serviceSnapshot);
                RebirthElectricalServiceUiFeedback.Receive(phase==5,phase==5?
                    "Electrical service completed.":"Electrical service payment returned; no service was applied.");
            }
            catch(Exception error){RemoteResourceDiagnostics.Write("Electrical terminal feedback unavailable: "+error.Message);}
        }
        finally{memory?.Dispose();}
    }
    private static void PumpActionRemoteTerminalReceipts()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var receipt in actionRemoteTerminalReceipts.Values.ToArray())
        {
            if(!ReferenceEquals(receipt.World,GameManager.Instance?.World)||receipt.World?.worldState.Guid!=receipt.NativeWorld||!ReferenceEquals(receipt.Actor,receipt.World?.GetPrimaryPlayer())||
                !ReferenceEquals(receipt.Connection,SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0]))
            {
                actionRequestWire.Cancel(ActionRemoteRequestKey(receipt.Operation));actionRemoteTerminalReceipts.Remove(receipt.Operation);
                receipt.Request=null;receipt.Original=null;receipt.Actor=null;receipt.World=null;receipt.Connection=null;receipt.Memory.Dispose();receipt.Memory=null;continue;
            }
            if(!receipt.Current()||receipt.Request==null||System.Diagnostics.Stopwatch.GetTimestamp()<receipt.Next)continue;
            string key=ActionRemoteRequestKey(receipt.Operation);if(actionRequestWire.IsQueued(key))continue;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var body=receipt.Request;
            bool Ready()=>receipt.Current()&&manager?.IsConnected==true&&(receipt.Connection is NetConnectionSimple||receipt.Connection is NetConnectionSteam);
            if(actionRequestWire.EnqueueObserved(key,body,receipt.Current,Ready,index=>manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthActionPaymentOwnerRequest>().Setup(receipt.Actor.entityId,receipt.Operation,body,index),false),sent=>
            {if(sent&&receipt.Current())manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(receipt.Actor),false);}))receipt.Next=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;
        }
    }
    private static bool ReceiveActionRemoteTerminalAck(Guid operation,string original)
    {
        if(!actionRemoteTerminalReceipts.TryGetValue(operation,out var receipt)||!receipt.Current()||receipt.Original!=original||receipt.Actor.Buffs.GetCustomVar(ActionOwnerMarker(operation))!=6f)return false;
        // This is positive completion metadata. A newer independent operation's
        // global Held marker must never be cleared by an old ACK retry.
        receipt.Actor.Buffs.RemoveCustomVar(ActionOwnerMarker(operation));
        GameManager.Instance.SaveLocalPlayerData();SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(receipt.Actor),false);
        actionRequestWire.Cancel(ActionRemoteRequestKey(operation));actionRemoteTerminalReceipts.Remove(operation);
        receipt.Request=null;receipt.Original=null;receipt.Actor=null;receipt.World=null;receipt.Connection=null;receipt.Memory.Dispose();receipt.Memory=null;return true;
    }
}
