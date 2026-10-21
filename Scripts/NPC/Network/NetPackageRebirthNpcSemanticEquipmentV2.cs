using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEngine.Scripting;

// Distinct packet identity and explicit V2 framing; V1 bytes are never
// reinterpreted as generation-aware data. Mixed-version peers are unsupported.
[Preserve]
public sealed class NetPackageRebirthNpcSemanticEquipmentV2:NetPackage
{
    private int entityId;private string stableId;private uint worldEpoch,revision;private ulong generation;
    private Dictionary<RebirthNpcEquipmentSlot,string> slots;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcSemanticEquipmentV2 Setup(EntityRebirthHumanoidNPC npc,RebirthNpcEquipmentSnapshot snapshot,ulong recoveryGeneration)
    {entityId=npc.entityId;stableId=snapshot.NpcId.ToString();worldEpoch=RebirthNpcNetworkEpoch.GetServerEpoch();revision=snapshot.Revision;generation=recoveryGeneration;slots=new Dictionary<RebirthNpcEquipmentSlot,string>(snapshot.Slots);Validate();return this;}
    private void Validate()
    {if(entityId<=0||worldEpoch==0||!RebirthNpcStableId.TryParse(stableId,out _)||slots==null||slots.Count>RebirthNpcNetworkFraming.MaxEquipmentSlots)throw new InvalidDataException("invalid V2 semantic equipment");foreach(var pair in slots)if(!Enum.IsDefined(typeof(RebirthNpcEquipmentSlot),pair.Key)||string.IsNullOrWhiteSpace(pair.Value)||pair.Value.Length>RebirthNpcNetworkFraming.MaxId)throw new InvalidDataException("invalid V2 equipment slot");}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadUInt16()!=2)throw new InvalidDataException("unsupported equipment projection codec");entityId=reader.ReadInt32();stableId=RebirthNpcNetworkFraming.ReadString(reader,RebirthNpcNetworkFraming.MaxId);worldEpoch=reader.ReadUInt32();generation=reader.ReadUInt64();revision=reader.ReadUInt32();int count=RebirthNpcNetworkFraming.ReadByteCount(reader,RebirthNpcNetworkFraming.MaxEquipmentSlots,"equipment V2 slots");slots=new Dictionary<RebirthNpcEquipmentSlot,string>();for(int i=0;i<count;i++){var slot=(RebirthNpcEquipmentSlot)reader.ReadByte();var value=RebirthNpcNetworkFraming.ReadString(reader,RebirthNpcNetworkFraming.MaxId);if(slots.ContainsKey(slot))throw new InvalidDataException("duplicate equipment slot");slots.Add(slot,value);}Validate();}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);Validate();writer.Write((ushort)2);writer.Write(entityId);RebirthNpcNetworkFraming.WriteString(writer,stableId,RebirthNpcNetworkFraming.MaxId);writer.Write(worldEpoch);writer.Write(generation);writer.Write(revision);writer.Write((byte)slots.Count);var ordered=new List<RebirthNpcEquipmentSlot>(slots.Keys);ordered.Sort();foreach(var slot in ordered){writer.Write((byte)slot);RebirthNpcNetworkFraming.WriteString(writer,slots[slot],RebirthNpcNetworkFraming.MaxId);}}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        RebirthNpcPendingEquipmentSnapshots.Receive(world,entityId,stableId,worldEpoch,generation,revision,slots);
    }
    internal static string Digest(int entity,RebirthNpcStableId id,ulong generation,uint revision,IDictionary<RebirthNpcEquipmentSlot,string> slots)
    {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true)){writer.Write(entity);writer.Write(id.ToString());writer.Write(generation);writer.Write(revision);var keys=new List<RebirthNpcEquipmentSlot>(slots.Keys);keys.Sort();writer.Write(keys.Count);foreach(var key in keys){writer.Write((byte)key);writer.Write(slots[key]);}writer.Flush();using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();}}
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthNpcEquipmentProjectionAck:NetPackage
{
    private int entity;private string stable,digest;private ulong generation;private uint revision,worldEpoch,connectionEpoch;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthNpcEquipmentProjectionAck Setup(int npc,RebirthNpcStableId id,ulong gen,uint rev,string hash,uint epoch,uint connection){entity=npc;stable=id.ToString();generation=gen;revision=rev;digest=hash;worldEpoch=epoch;connectionEpoch=connection;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadUInt16()!=1)throw new InvalidDataException();entity=reader.ReadInt32();stable=RebirthNpcNetworkFraming.ReadString(reader,RebirthNpcNetworkFraming.MaxId);generation=reader.ReadUInt64();revision=reader.ReadUInt32();digest=RebirthNpcNetworkFraming.ReadString(reader,64);worldEpoch=reader.ReadUInt32();connectionEpoch=reader.ReadUInt32();if(digest.Length!=64)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((ushort)1);writer.Write(entity);RebirthNpcNetworkFraming.WriteString(writer,stable,RebirthNpcNetworkFraming.MaxId);writer.Write(generation);writer.Write(revision);RebirthNpcNetworkFraming.WriteString(writer,digest,64);writer.Write(worldEpoch);writer.Write(connectionEpoch);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthNpcEquipmentProjectionReceipts.Observe(this,world,entity,stable,generation,revision,digest,worldEpoch,connectionEpoch);}
    public int GetLength()=>160;
}
internal static partial class RebirthNpcEquipmentProjectionReceipts
{
    private const int MaximumReceipts=65536;
    private sealed class Receipt
    {
        internal ClientInfo Sender;internal ConnectionManager Manager;internal INetConnection Transport;
        internal EntityRebirthHumanoidNPC Actor;internal RebirthNpcRuntimeState Runtime;
        internal RebirthNpcStableId Stable;internal string Digest;internal int Entity,Recipient;
        internal ulong Generation;internal uint Revision,Epoch,Connection;
    }
    private static Dictionary<string,Receipt> receipts=new Dictionary<string,Receipt>(StringComparer.Ordinal);
    private static RebirthNpcAncillaryOriginalScope receiptScope;
    private static string Key(ClientInfo sender,int entity)=>sender.entityId+"|"+entity;
    private static bool Current(Receipt r)
    {
        if(r==null||receiptScope==null||!receiptScope.Current()||r.Generation!=receiptScope.Generation||r.Actor==null||r.Runtime==null||r.Sender==null||r.Manager==null||r.Transport==null||r.Transport.IsDisconnected()||!ReferenceEquals(r.Manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||r.Sender.entityId!=r.Recipient||r.Sender.disconnecting||!r.Sender.loginDone||!r.Sender.bAttachedToEntity||!ReferenceEquals(r.Manager.Clients.ForEntityId(r.Recipient),r.Sender)||r.Sender.netConnection==null||r.Sender.netConnection.Length==0||!ReferenceEquals(r.Sender.netConnection[0],r.Transport)||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(r.Recipient,r.Connection)||r.Epoch!=RebirthNpcNetworkEpoch.GetServerEpoch())return false;
        return ReferenceEquals(r.Actor.world,receiptScope.World)&&ReferenceEquals(receiptScope.World.GetEntity(r.Entity),r.Actor)&&ReferenceEquals(r.Actor.RebirthRuntimeState,r.Runtime)&&r.Runtime.StableId==r.Stable&&r.Actor.NativeIdentityReadable&&(r.Actor.ObservedNativeStableIdentity.IsEmpty||r.Actor.ObservedNativeStableIdentity==r.Stable);
    }
    internal static bool Acknowledged(ClientInfo sender,int entity,ulong generation,uint revision,string digest)
    {
        if(!ThreadManager.IsMainThread()||sender==null||receiptScope==null||!receiptScope.Current())return false;
        return receipts.TryGetValue(Key(sender,entity),out var receipt)&&ReferenceEquals(receipt.Sender,sender)&&Current(receipt)&&receipt.Generation==generation&&receipt.Revision==revision&&receipt.Digest==digest;
    }
    private static void RetireEnded()
    {
        // Positive captured native membership ended: projection metadata only.
        // No absent ACK becomes successful delivery or recovery convergence.
        var ended=new List<string>();foreach(var pair in receipts)
        {
            var r=pair.Value;if(r?.Manager==null||r.Sender==null||r.Manager==null||r.Transport==null||r.Actor==null)continue;
            if(!ReferenceEquals(r.Manager.Clients.ForEntityId(r.Recipient),r.Sender)||r.Transport.IsDisconnected()||r.Sender.netConnection==null||r.Sender.netConnection.Length==0||!ReferenceEquals(r.Sender.netConnection[0],r.Transport)||!ReferenceEquals(receiptScope.World.GetEntity(r.Entity),r.Actor))ended.Add(pair.Key);
        }
        foreach(string key in ended)receipts.Remove(key);
    }
    internal static void Observe(NetPackage package,World world,int entity,string stable,ulong generation,uint revision,string digest,uint epoch,uint connection)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||package?.Sender==null||!package.ValidEntityIdForSender(package.Sender.entityId)||epoch!=RebirthNpcNetworkEpoch.GetServerEpoch()||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(package.Sender.entityId,connection)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(package.Sender.entityId),package.Sender)||!RebirthNpcStableId.TryParse(stable,out var id)||world.GetEntity(entity) is not EntityRebirthHumanoidNPC npc||npc.RebirthRuntimeState?.StableId!=id||!npc.NativeIdentityReadable||(!npc.ObservedNativeStableIdentity.IsEmpty&&npc.ObservedNativeStableIdentity!=id)||!HashValid(digest))return;
        if(!RebirthNpcAncillaryOriginalScope.Use(ref receiptScope,receipts.Count==0)||receiptScope.Generation!=generation)return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var sender=package.Sender;
        if(sender.disconnecting||!sender.loginDone||!sender.bAttachedToEntity||sender.netConnection==null||sender.netConnection.Length==0||sender.netConnection[0]==null||sender.netConnection[0].IsDisconnected())return;
        var snapshot=RebirthNpcEquipmentService.CaptureRecoverySnapshot(id);
        if(snapshot.Revision!=revision||NetPackageRebirthNpcSemanticEquipmentV2.Digest(entity,id,generation,revision,snapshot.Slots)!=digest)return;
        RetireEnded();string key=Key(sender,entity);
        if(receipts.TryGetValue(key,out var prior)&&Current(prior)&&(prior.Generation>generation||(prior.Generation==generation&&prior.Revision>revision)))return;
        if(!receipts.ContainsKey(key)&&receipts.Count>=MaximumReceipts)return;
        receipts[key]=new Receipt{Sender=sender,Recipient=sender.entityId,Manager=manager,Transport=sender.netConnection[0],Actor=npc,Runtime=npc.RebirthRuntimeState,Stable=id,Digest=digest,Entity=entity,Generation=generation,Revision=revision,Epoch=epoch,Connection=connection};
    }
    private static bool HashValid(string value)
    {if(value==null||value.Length!=64)return false;foreach(char c in value)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;return true;}
}
