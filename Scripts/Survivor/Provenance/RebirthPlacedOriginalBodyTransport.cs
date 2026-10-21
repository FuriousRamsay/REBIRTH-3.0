using System;
using System.IO;
using System.Text;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    // Shared helpers below provide bounded framing/scheduling ONLY. They carry
    // no logistics, fetch, payment, native fatal or body settlement authority.
    private static readonly RebirthRemoteResourceRepairSendQueue deferredWire=new RebirthRemoteResourceRepairSendQueue();
    private static void QueueDeferredFragments(EntityPlayerLocal player,DeferredBody current)
    {
        try
        {
            var original=DeferredDecode(DeferredImage(current));original.Phase=0;original.Permit=string.Empty;original.Nonce=string.Empty;string image=DeferredImage(original);string key=original.Operation.ToString("N")+"|"+Hash(image);if(deferredWire.IsQueued(key))return;
            var body=new RebirthRemoteResourceFragments.Body(new UTF8Encoding(false,true).GetBytes(image));if(body.Length>DeferredMaximum)return;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];var world=(World)player.world;string directory=GameIO.GetSaveGameDir();
            deferredWire.Enqueue(key,body,()=>ReferenceEquals(GameManager.Instance?.World,world)&&directory==GameIO.GetSaveGameDir()&&deferredBodies.TryGetValue(current.Operation,out var live)&&ReferenceEquals(live,current)&&DeferredOwner(player,current)&&current.Phase<2&&!current.SessionPermit,
                ()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),index=>manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPlacedBodyPrepare>().Setup(player.entityId,current.Operation,body.Hash,body.Length,body.Count,index,body.Part(index)),false));
        }catch{/* Source original is already durable; transport pressure retains it. */}
    }
    internal static void WriteDeferredFragmentPayload(PooledBinaryWriter writer,int entity,Guid operation,string hash,int total,int count,int index,string text)
    {if(hash==null||hash.Length!=64||text==null||text.Length>RebirthRemoteResourceFragments.ChunkText)throw new InvalidDataException();writer.Write(entity);writer.Write(operation.ToByteArray());writer.Write(hash);writer.Write(total);writer.Write(count);writer.Write(index);writer.Write(text);}    internal static void ReceiveDeferredFragment(NetPackage packet,World world,int entity,Guid operation,string hash,int total,int count,int index,string text)
    {
        try
        {
            var sender=packet?.Sender;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||sender==null||sender.entityId!=entity||!packet.ValidEntityIdForSender(entity)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(entity),sender)||total<1||total>DeferredMaximum||world.GetEntity(entity) is not EntityPlayer actor||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return;
            DeferredScope(world);string scope="placed-body-preintent|"+world.worldState.Guid+"|"+owner.StorageKey+"|"+entity+"|"+RebirthCamperCharacterAccess.CurrentCreation(actor)+"|"+deferredTransportEpoch;
            if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,hash,total,count,index,text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var bytes)||bytes==null)return;
            string image=new UTF8Encoding(false,true).GetString(bytes);var d=DeferredDecode(image);if(d.Operation!=operation||d.Phase!=0||!DeferredOwner(actor,d))return;ReceiveDeferredBody(world,entity,image);
        }catch{/* Only a complete authenticated own original can reach admission. */}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedBodyPrepare:NetPackage
{
    private int entity,total,count,index;private Guid operation;private string hash=string.Empty,text=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedBodyPrepare Setup(int player,Guid original,string digest,int bytes,int parts,int part,string chunk){entity=player;operation=original;hash=digest;total=bytes;count=parts;index=part;text=chunk;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);hash=reader.ReadString();total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();text=reader.ReadString();if(hash.Length!=64||text.Length>RebirthRemoteResourceFragments.ChunkText)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);RebirthPlacedOriginalDeathPartition.WriteDeferredFragmentPayload(writer,entity,operation,hash,total,count,index,text);}
    public override void ProcessPackage(World world,GameManager callbacks){if(world?.IsRemote()==false&&ValidEntityIdForSender(entity))RebirthPlacedOriginalDeathPartition.ReceiveDeferredFragment(this,world,entity,operation,hash,total,count,index,text);}
    public int GetLength()=>128+Encoding.UTF8.GetByteCount(text);
}