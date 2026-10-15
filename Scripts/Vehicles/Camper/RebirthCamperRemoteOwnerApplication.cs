using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

// Only the production reward/refund/debit adapter interprets these root-defined
// effects. Peers cannot submit an effect to the server or choose its target.
public enum RebirthCamperRemoteOwnerEffectKind:byte { Reward=0, Refund=1, Debit=2, EnergyTransfer=3 }
public static class RebirthCamperRemoteOwnerApplication
{
    private sealed class Pending
    {
        internal EntityPlayer Player;internal ClientInfo Sender;internal string Creation;
        internal RebirthCamperRemoteOwnerEffectKind Kind;internal byte[] Payload;internal Action<bool> Finished;
    }
    private const string ReceiptPrefix="rbCamperOwner_";
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private static Func<EntityPlayer,string> creationProvider;
    private static Func<EntityPlayerLocal,Guid,RebirthCamperRemoteOwnerEffectKind,byte[],bool> apply;
    public static bool IsRegistered=>creationProvider!=null && apply!=null;

    public static void RegisterOwnerApplication(Func<EntityPlayer,string> authenticatedCreation,
        Func<EntityPlayerLocal,Guid,RebirthCamperRemoteOwnerEffectKind,byte[],bool> applyOnce)
    {
        if(authenticatedCreation==null || applyOnce==null)throw new ArgumentNullException("Authenticated owner effect adapters required.");
        if(IsRegistered)throw new InvalidOperationException("Remote owner effects already registered.");
        // ModAPI runs before StartServer/StartClient initializes native package
        // mappings. Register adapters here; native sends resolve packages later.
        creationProvider=authenticatedCreation;apply=applyOnce;
    }

    public static bool SendOwnerApplication(EntityPlayer player,Guid operation,RebirthCamperRemoteOwnerEffectKind kind,
        byte[] serverAuthoredNativePayload,Action<bool> acknowledged)
    {
        if(!ThreadManager.IsMainThread() || !IsRegistered || player?.world==null || player.world.IsRemote()
            || !ReferenceEquals(GameManager.Instance?.World,player.world) || player.IsDead() || operation==Guid.Empty
            || acknowledged==null || !Enum.IsDefined(typeof(RebirthCamperRemoteOwnerEffectKind),kind))return false;
        RebirthCamperRemoteTransport.AssertFrame(serverAuthoredNativePayload);
        var sender=SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(player.entityId);
        string creation=creationProvider(player);
        if(sender?.InternalId==null || string.IsNullOrWhiteSpace(creation))return false;
        if(pending.TryGetValue(operation,out var existing))
        {
            // The same retained root delivery operation can observe its existing
            // application; it never changes payload or gains a second callback.
            return ReferenceEquals(existing.Player,player) && ReferenceEquals(existing.Sender,sender)
                && existing.Creation==creation && existing.Kind==kind && Equal(existing.Payload,serverAuthoredNativePayload);
        }
        if(pending.Count>=64)return false;
        var request=new Pending{Player=player,Sender=sender,Creation=creation,Kind=kind,
            Payload=(byte[])serverAuthoredNativePayload.Clone(),Finished=acknowledged};
        pending.Add(operation,request);
        try
        {
            sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperOwnerEffect>().Setup(player.entityId,operation,kind,request.Payload));
            GameManager.Instance.StartCoroutine(Observe(operation,request));return true;
        }
        catch { pending.Remove(operation);return false; }
    }

    private static IEnumerator Observe(Guid operation,Pending request)
    {
        bool acknowledged=false;
        float deadline=Time.realtimeSinceStartup+30f,next=0f;
        try
        {
            while(Time.realtimeSinceStartup<deadline && Current(request))
            {
                if(Time.realtimeSinceStartup>=next)
                {
                    if(request.Kind==RebirthCamperRemoteOwnerEffectKind.EnergyTransfer
                        ? RebirthCamperEnergyOwnerPayload.VerifySaved(request.Player,operation,request.Payload,()=>Current(request))
                        : request.Kind==RebirthCamperRemoteOwnerEffectKind.Debit
                        ? RebirthCamperStationPlayerDebitPayload.VerifySaved(request.Player,operation,request.Payload,()=>Current(request))
                        : RebirthCamperSavedCursor.VerifyPublishedReceipt(request.Sender.InternalId.CombinedString,operation,
                            ()=>Current(request),ReceiptPrefix)){acknowledged=true;yield break;}
                    next=Time.realtimeSinceStartup+.25f;
                }
                yield return null;
            }
        }
        finally
        {
            pending.Remove(operation);
            try{request.Finished(acknowledged);}catch(Exception error){Log.Warning("[CamperStation] Owner effect callback retained: "+error.Message);}
        }
    }
    private static bool Current(Pending request)
        =>ThreadManager.IsMainThread() && request.Player?.world!=null && !request.Player.world.IsRemote() && !request.Player.IsDead()
            && ReferenceEquals(GameManager.Instance?.World,request.Player.world)
            && ReferenceEquals(request.Player.world.GetEntity(request.Player.entityId),request.Player)
            && ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(request.Player.entityId),request.Sender)
            && creationProvider(request.Player)==request.Creation;

    internal static void Receive(World world,int playerId,Guid operation,RebirthCamperRemoteOwnerEffectKind kind,byte[] payload)
    {
        var player=world?.GetPrimaryPlayer();
        if(!ThreadManager.IsMainThread() || !IsRegistered || world==null || !world.IsRemote() || player==null
            || player.entityId!=playerId || player.IsDead() || operation==Guid.Empty
            || !Enum.IsDefined(typeof(RebirthCamperRemoteOwnerEffectKind),kind))return;
        string receipt=ReceiptPrefix+operation.ToString("N");
        float marker=player.Buffs.GetCustomVar(receipt);
        bool nativeDebit=kind==RebirthCamperRemoteOwnerEffectKind.Debit&&RebirthCamperStationNativeActorPayment.LooksNativePayload(payload);
        if(nativeDebit||kind==RebirthCamperRemoteOwnerEffectKind.EnergyTransfer)
        {
            // Native journal owns apply/retire phase receipts. The wrapper
            // cannot bypass its exact original-frame checks on duplicates or
            // suppress retirement after the original apply was acknowledged.
            if((nativeDebit&&RebirthSurvivorMode.IsEnabledForCurrentWorld())||(marker!=0f&&marker!=1f&&marker!=2f))return;
            try
            {
                RebirthCamperRemoteTransport.AssertFrame(payload);
                if(marker==0f)player.Buffs.SetCustomVar(receipt,2f,true);
                if(!apply(player,operation,kind,payload))return;
                player.Buffs.SetCustomVar(receipt,1f,true);PublishOwner(player);
            }
            catch(Exception error){Log.Warning("[CamperStation] Native owner effect retained: "+error.Message);}
            return;
        }
        if(marker==1f)
        {
            // An exact completed operation can be acknowledged again, with no
            // native owner mutation and no second delivery attempt. Debit still
            // requires its exact saved after-image before server acknowledgement.
            PublishOwner(player);return;
        }
        if(marker!=0f)return; // partial/unknown receipts cannot authorize replay
        try
        {
            RebirthCamperRemoteTransport.AssertFrame(payload);
            player.Buffs.SetCustomVar(receipt,2f,true);
            if(!apply(player,operation,kind,payload))return;
            // The root handler returns true only after the intended complete
            // native owner application; failures retain receipt2.
            player.Buffs.SetCustomVar(receipt,1f,true);
            PublishOwner(player);
        }
        catch(Exception error){Log.Warning("[CamperStation] Owner effect retained: "+error.Message);}
    }
    private static void PublishOwner(EntityPlayerLocal player)
        =>SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(player),false);
    private static bool Equal(byte[] a,byte[] b)
    {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
}

[Preserve]
public sealed class NetPackageRebirthCamperOwnerEffect:NetPackage
{
    private int player;private Guid operation;private RebirthCamperRemoteOwnerEffectKind kind;private byte[] payload;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCamperOwnerEffect Setup(int p,Guid op,RebirthCamperRemoteOwnerEffectKind k,byte[] bytes)
    {player=p;operation=op;kind=k;payload=bytes;return this;}
    public override void read(PooledBinaryReader r)
    {player=r.ReadInt32();operation=new Guid(r.ReadBytes(16));kind=(RebirthCamperRemoteOwnerEffectKind)r.ReadByte();payload=CamperFrame.Read(r);}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(player);w.Write(operation.ToByteArray());w.Write((byte)kind);CamperFrame.Write(w,payload);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world!=null && world.IsRemote())RebirthCamperRemoteOwnerApplication.Receive(world,player,operation,kind,payload);}
    public int GetLength()=>0;
}
