using System;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionConnectionScope{internal readonly Guid Value=Guid.NewGuid();}
    private static readonly ConditionalWeakTable<object,ActionConnectionScope> actionConnectionScopes=new ConditionalWeakTable<object,ActionConnectionScope>();
    private static readonly RebirthRemoteResourceRepairSendQueue actionRequestWire=new RebirthRemoteResourceRepairSendQueue();
    private static long actionRequestNext;
    // Normal remote electrical input uses this original native checkpoint API.
    internal static bool RequestOwnedElectricalService(EntityPlayerLocal actor,Vector3i position)
    {
        if(!ThreadManager.IsMainThread())return false;
        InstallActionPaymentNativeObservers();
        if(!CaptureActionRemoteClick(actor,position,out _,out _))return false;
        // Setup invokes the real native FromPlayer. This preparation object is
        // never sent as a stale PDS: completion below makes a fresh native upload.
        var preparation=NetPackageManager.GetPackage<NetPackagePlayerData>();
        try{preparation.Setup(actor);}
        finally{NetPackageManager.FreePackage(preparation);}
        QueueActionRemoteRequest();return true;
    }
    private static void QueueActionRemoteRequest()
    {
        var click=actionRemoteClick;if(click==null||!click.CourierCurrent()||System.Diagnostics.Stopwatch.GetTimestamp()<actionRequestNext)return;
        string key="electrical-owner-click:"+click.Operation.ToString("N");if(actionRequestWire.IsQueued(key))return;
        if(!SaveActionClientInitialBefore(click))return;
        var body=PrepareActionRemoteRequestBody(click);if(body==null)return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool Ready()=>click.CourierCurrent()&&manager?.IsConnected==true&&(click.Connection is NetConnectionSimple||click.Connection is NetConnectionSteam);
        if(actionRequestWire.EnqueueObserved(key,body,click.CourierCurrent,Ready,index=>
        {
            var packet=NetPackageManager.GetPackage<NetPackageRebirthActionPaymentOwnerRequest>().Setup(click.Actor.entityId,click.Operation,body,index);
            if(!RebirthMusicLibraryClient.CanSend(manager,packet)){NetPackageManager.FreePackage(packet);throw new IOException("Electrical request native transport unavailable");}
            manager.SendToServer(packet,false);
        },sent=>
        {
            if(!sent||!Ready())return;
            // Native upload, acceptance and Save remain independent of this
            // described request. Its dispatch is never an acceptance receipt.
            manager.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(click.Actor),false);
        }))actionRequestNext=System.Diagnostics.Stopwatch.GetTimestamp()+System.Diagnostics.Stopwatch.Frequency;
    }
    private static void PumpActionRemoteRequests()
    {
        if(!ThreadManager.IsMainThread())return;
        try{PumpActionClientRestart();QueueActionRemoteRequest();PumpActionRemoteTerminalReceipts();PumpActionTerminalCouriers();actionRequestWire.Pump(8);actionGrantWire.Pump(8);}catch(Exception error){RemoteResourceDiagnostics.Write("Electrical original request retained: "+error.Message);}
    }
    internal static void ReceiveActionRemoteRequest(World world,ClientInfo peer,int actorId,Guid operation,string hash,int total,int count,int index,string fragment)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||peer==null||peer.entityId!=actorId||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actorId),peer)||peer.netConnection?[0]==null||
            !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var account)||world.GetEntity(actorId) is not EntityPlayer actor||actor.IsDead()||total>24*1024*1024+12)return;
        var connection=peer.netConnection[0];var scope=actionConnectionScopes.GetValue(connection,_=>new ActionConnectionScope());
        string key="electrical-owner-request|"+scope.Value.ToString("N")+"|"+world.worldState.Guid+"|"+account.StorageKey;
        // Reserve completion/UTF8 framing scratch before TryAdd can materialize
        // the full body. Retained host source graphs have a separate measured
        // lease; described native bytes are skipped, never owner authority.
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return;
        using(scratch)
        {
            if(!RebirthRemoteResourceFragments.TryAdd(key,operation,hash,total,count,index,fragment,Time.realtimeSinceStartup,out var complete)||complete==null)return;
            RegisterActionRemoteHostOriginal(world,actor,peer,operation,complete);
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthActionPaymentOwnerRequest:NetPackage
{
    private int actor,total,count,index;private Guid operation;private string hash,fragment;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthActionPaymentOwnerRequest Setup(int actorId,Guid original,RebirthRemoteResourceFragments.Body body,int part)
    {actor=actorId;operation=original;hash=body.Hash;total=body.Length;count=body.Count;index=part;fragment=body.Part(part);return this;}
    public override void read(PooledBinaryReader reader)
    {
        if(reader.ReadUInt32()!=1)throw new InvalidDataException("Electrical owner request version differs");
        actor=reader.ReadInt32();byte[] nonce=reader.ReadBytes(16);if(nonce.Length!=16)throw new EndOfStreamException();operation=new Guid(nonce);
        hash=RebirthCookingGridCustody.ReadWireString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();
        fragment=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);writer.Write((uint)1);writer.Write(actor);writer.Write(operation.ToByteArray());RebirthCookingGridCustody.WriteWireString(writer,hash,64);
        writer.Write(total);writer.Write(count);writer.Write(index);RebirthCookingGridCustody.WriteWireString(writer,fragment,RebirthRemoteResourceFragments.ChunkText);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(Sender!=null&&ValidEntityIdForSender(actor))RemoteResourceTransactions.ReceiveActionRemoteRequest(world,Sender,actor,operation,hash,total,count,index,fragment);}
    public int GetLength()=>128+(fragment==null?0:System.Text.Encoding.UTF8.GetByteCount(fragment));
}
