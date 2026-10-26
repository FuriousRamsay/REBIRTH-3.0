using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

// Recovery projections are observations, never inventory/payment/lease authority.
// Current attached authenticated remote recipients are required. Client absence
// remains pending. A disconnected/replaced native connection is no longer a
// projection recipient; it must receive the normal current-generation baseline.
internal static partial class RebirthNpcRecoveryProjectionNetwork
{
    internal sealed class Probe
    {
        internal Guid Operation,Challenge;
        internal ulong Generation,Sequence,NameSequence;
        internal int Entity;
        internal RebirthNpcStableId Stable;
        internal uint Epoch,Connection,Revision;
        internal string Digest,Name;
    }
    private sealed class Delivery
    {internal ClientInfo Peer;internal Probe Probe;internal bool Acknowledged;internal double Retry;}
    private sealed class NameImage
    {internal ulong Generation,Sequence;internal RebirthNpcStableId Stable;internal string Name;internal int Entity;}
    private static Dictionary<string,Delivery> Deliveries=new Dictionary<string,Delivery>(StringComparer.Ordinal);
    private static Dictionary<int,Probe> Pending=new Dictionary<int,Probe>();
    private static Dictionary<int,NameImage> Names=new Dictionary<int,NameImage>();
    private static World context;
    private static string directory;
    private static uint clientEpoch,clientConnection;
    private static ulong sequence;
    private static uint sendCursor;
    private static int clientCursor,nameCursor;
    private static bool patched;
    private const int MaximumDeliveries=65536;
    internal static object CaptureAuditState()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world==null||!ReferenceEquals(context,world))return new {available=false};
        int acknowledged=0;foreach(var delivery in Deliveries.Values)if(delivery.Acknowledged)acknowledged++;
        return new {available=true,remoteClient=world.IsRemote(),required=Deliveries.Count,acknowledged,pendingClientActors=Pending.Count,nameFloors=Names.Count};
    }    internal static void Install()
    {
        if(patched)return;
        new Harmony("rebirth.npc.recovery.native-name.v1").CreateClassProcessor(typeof(NativeNameChanged)).Patch();patched=true;
    }
    private static void Scope(World world)
    {
        string save=GameIO.GetSaveGameDir();uint epoch=world?.IsRemote()==true?RebirthNpcNetworkEpoch.ClientEpoch:0;
        uint connection=world?.IsRemote()==true?RebirthNpcNetworkEpoch.ClientConnectionEpoch:0;
        if(ReferenceEquals(context,world)&&directory==save&&clientEpoch==epoch&&clientConnection==connection)return;
        if(Deliveries.Count!=0||Pending.Count!=0||Names.Count!=0)throw new InvalidOperationException("Original recovery projection mailbox requires typed retirement");
        Deliveries.Clear();Pending.Clear();Names.Clear();context=world;directory=save;clientEpoch=epoch;clientConnection=connection;sequence=0;clientCursor=0;nameCursor=0;
    }
    internal static ulong NextSequence()
    {Scope(GameManager.Instance?.World);if(sequence==ulong.MaxValue)throw new InvalidOperationException("Projection sequence exhausted; connection rebaseline required");return ++sequence;}
    private static string Key(ClientInfo peer,int entity)=>peer.entityId+"|"+entity;
    private static bool Eligible(World world,ConnectionManager manager,ClientInfo peer)
    {
        return peer!=null&&peer.loginDone&&!peer.disconnecting&&peer.bAttachedToEntity&&peer.entityId>0&&
            ReferenceEquals(manager.Clients.ForEntityId(peer.entityId),peer)&&world.GetEntity(peer.entityId) is EntityPlayer actor&&actor is not EntityPlayerLocal;
    }
    internal static bool Poll(Guid operation,ulong generation,RebirthNpcStableId[] affected,out string error)
    {
        error=string.Empty;var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||manager?.IsServer!=true||generation!=RebirthNpcRecoveryGeneration.RequireCurrent())
        {error="Recovery projection authority/generation changed";return false;}
        Scope(world);Install();
        var peers=new List<ClientInfo>();foreach(var peer in manager.Clients.List)if(Eligible(world,manager,peer))peers.Add(peer);
        if(peers.Count>64){error="Recovery recipient capacity pending";return false;}
        var live=new List<EntityRebirthNPC>();var ids=new HashSet<RebirthNpcStableId>(affected??new RebirthNpcStableId[0]);
        // New same-generation actors also need the current projection. Never
        // project a historical candidate over legitimate post-install actions.
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())ids.Add(state.StableId);
        foreach(var id in ids)
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity))continue;
            if(world.GetEntity(entity) is not EntityRebirthNPC npc||npc.RebirthRuntimeState?.StableId!=id)
            {error="Recovery projection native identity pending";return false;}
            if(RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc)){error="Native activation remains deferred";return false;}
            live.Add(npc);
        }
        if(live.Count>RebirthNpcNetworkProtocol.MaxBaselineActors||(long)live.Count*peers.Count>MaximumDeliveries)
        {error="Recovery projection mailbox capacity pending";return false;}
        var retained=new HashSet<string>(StringComparer.Ordinal);var digests=new Dictionary<int,string>();bool complete=true;
        var outgoing=new List<System.Tuple<Delivery,EntityRebirthNPC>>();
        double now=Time.realtimeSinceStartup;
        foreach(var peer in peers)
        {
            if(!RebirthNpcNetworkEpoch.TryGetServerConnectionEpoch(peer.entityId,out uint connection))
            {RebirthNpcNetworkBaselineService.SendTo(peer);complete=false;continue;}
            foreach(var npc in live)
            {
                string key=Key(peer,npc.entityId);retained.Add(key);
                if(!digests.TryGetValue(npc.entityId,out string digest)){if(!TryDigest(npc,generation,true,out digest)){complete=false;continue;}digests.Add(npc.entityId,digest);}
                if(!Deliveries.TryGetValue(key,out var delivery)||!ReferenceEquals(delivery.Peer,peer)||delivery.Probe.Operation!=operation||
                    delivery.Probe.Generation!=generation||delivery.Probe.Digest!=digest||delivery.Probe.Connection!=connection||delivery.Probe.Stable!=npc.RebirthRuntimeState.StableId)
                {
                    var probe=new Probe{Operation=operation,Generation=generation,Sequence=NextSequence(),NameSequence=NextSequence(),Challenge=Guid.NewGuid(),
                        Entity=npc.entityId,Stable=npc.RebirthRuntimeState.StableId,Epoch=RebirthNpcNetworkEpoch.GetServerEpoch(),Connection=connection,
                        Revision=npc.RebirthRuntimeState.Revision,Digest=digest,Name=npc.EntityName??string.Empty};
                    delivery=new Delivery{Peer=peer,Probe=probe};Deliveries[key]=delivery;
                }
                if(delivery.Acknowledged)continue;
                complete=false;if(now<delivery.Retry)continue;
                outgoing.Add(System.Tuple.Create(delivery,npc));
            }
        }
        int count=Math.Min(16,outgoing.Count);
        for(int index=0;index<count;index++)
        {
                var selected=outgoing[(int)((sendCursor+(uint)index)%(uint)outgoing.Count)];var delivery=selected.Item1;var npc=selected.Item2;var peer=delivery.Peer;
                delivery.Retry=now+1;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcRuntimeStateV2>().Setup(npc),_attachedToEntityId:peer.entityId);
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcProgressionV2>().Setup(RebirthNpcProgressionProjectionService.Capture(npc.RebirthRuntimeState.StableId,true),delivery.Probe.Connection),_attachedToEntityId:peer.entityId);
                if(RebirthHumanProgressionProjectionService.TryCapture(npc.RebirthRuntimeState.StableId,out var humanProgression))
                    manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthHumanProgression>().Setup(humanProgression,delivery.Probe.Connection,generation),_attachedToEntityId:peer.entityId);
                if(npc is EntityRebirthHumanoidNPC humanoid)
                {
                    manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcAppearanceEquipmentV2>().Setup(humanoid),_attachedToEntityId:peer.entityId);
                    manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcSemanticEquipmentV2>().Setup(humanoid,RebirthNpcEquipmentService.CaptureRecoverySnapshot(npc.RebirthRuntimeState.StableId),generation),_attachedToEntityId:peer.entityId);
                }
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcRecoveryProjectionProbe>().Setup(delivery.Probe),_attachedToEntityId:peer.entityId);
        }
        if(outgoing.Count>0)sendCursor=(sendCursor+(uint)count)%(uint)outgoing.Count;
        var obsolete=new List<string>();foreach(var pair in Deliveries)if(!retained.Contains(pair.Key))obsolete.Add(pair.Key);foreach(var key in obsolete)Deliveries.Remove(key);
        if(!complete)error="Current eligible remote native projection readbacks pending";
        return complete;
    }
    internal static bool TryCaptureCompletion(Guid operation,ulong generation,RebirthNpcStableId[] affected,out string proof,out string error)
    {
        proof=null;if(!Poll(operation,generation,affected,out error))return false;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(operation.ToByteArray());writer.Write(generation);writer.Write(RebirthNpcNetworkEpoch.GetServerEpoch());
            var keys=new List<string>(Deliveries.Keys);keys.Sort(StringComparer.Ordinal);writer.Write(keys.Count);
            foreach(string key in keys)
            {
                var delivery=Deliveries[key];if(!delivery.Acknowledged||delivery.Probe.Operation!=operation||delivery.Probe.Generation!=generation)return false;
                writer.Write(key);writer.Write(delivery.Peer.PlatformId?.ToString()??string.Empty);writer.Write(delivery.Probe.Connection);
                writer.Write(delivery.Probe.Stable.ToString());writer.Write(delivery.Probe.Challenge.ToByteArray());writer.Write(delivery.Probe.Digest);
            }
            writer.Flush();using(var hash=SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();return true;
        }
    }    internal static void Receive(World world,Probe probe)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||probe==null||
            probe.Connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(probe.Epoch)||!RebirthNpcRecoveryProjectionScope.Observe(probe.Generation))return;
        Scope(world);
        if(Pending.TryGetValue(probe.Entity,out var prior)&&(prior.Generation>probe.Generation||prior.Generation==probe.Generation&&prior.Sequence>probe.Sequence))return;
        if(Pending.Count>=RebirthNpcNetworkProtocol.MaxBaselineActors&&!Pending.ContainsKey(probe.Entity))return;
        Pending[probe.Entity]=probe;ReceiveName(world,probe.Entity,probe.Stable,probe.Epoch,probe.Connection,probe.Generation,probe.NameSequence,probe.Name);
        Apply(world,probe);
    }
    internal static void TickClient()
    {
        var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true)return;Scope(world);
        // Retain a generation-tagged authoritative name projection. Late native
        // stats/name images cannot permanently undo the latest V2 name.
        var names=new List<NameImage>(Names.Values);
        if(names.Count!=0)
        {
            int nameCount=Math.Min(8,names.Count),nameStart=nameCursor%names.Count;
            for(int i=0;i<nameCount;i++)
            {var image=names[(nameStart+i)%names.Count];if(world.GetEntity(image.Entity) is EntityRebirthNPC npc&&npc.RebirthRuntimeState?.StableId==image.Stable&&image.Generation==RebirthNpcRecoveryProjectionScope.Current&&npc.EntityName!=image.Name)npc.SetEntityName(image.Name);}
            nameCursor=(nameStart+nameCount)%names.Count;
        }
        var rows=new List<Probe>(Pending.Values);if(rows.Count==0){clientCursor=0;return;}
        int count=Math.Min(8,rows.Count),start=clientCursor%rows.Count;for(int i=0;i<count;i++)Apply(world,rows[(start+i)%rows.Count]);clientCursor=(start+count)%rows.Count;
    }
    private static void Apply(World world,Probe probe)
    {
        if(!RebirthNpcRecoveryProjectionScope.Observe(probe.Generation)) {Pending.Remove(probe.Entity);return;}
        if(world.GetEntity(probe.Entity) is not EntityRebirthNPC npc||npc.RebirthRuntimeState?.StableId!=probe.Stable)return;
        if(!TryDigest(npc,probe.Generation,false,out string actual)||actual!=probe.Digest)return;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcRecoveryProjectionAck>().Setup(probe));
        Pending.Remove(probe.Entity);
    }
    internal static void Acknowledge(NetPackage package,World world,Probe probe)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||manager?.IsServer!=true||
            package?.Sender==null||!Eligible(world,manager,package.Sender)||!package.ValidEntityIdForSender(package.Sender.entityId)||
            probe.Epoch!=RebirthNpcNetworkEpoch.GetServerEpoch()||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(package.Sender.entityId,probe.Connection))return;
        Scope(world);
        if(!Deliveries.TryGetValue(Key(package.Sender,probe.Entity),out var delivery)||!ReferenceEquals(delivery.Peer,package.Sender))return;
        var expected=delivery.Probe;
        if(expected.Operation!=probe.Operation||expected.Challenge!=probe.Challenge||expected.Generation!=probe.Generation||expected.Sequence!=probe.Sequence||expected.Stable!=probe.Stable||expected.Digest!=probe.Digest)return;
        if(world.GetEntity(probe.Entity) is not EntityRebirthNPC npc||npc.RebirthRuntimeState?.StableId!=probe.Stable||
            !TryDigest(npc,probe.Generation,true,out string current)||current!=probe.Digest)return;
        delivery.Acknowledged=true;
    }
    internal static bool TryDigest(EntityRebirthNPC npc,ulong generation,bool server,out string digest)
    {
        digest=null;if(npc?.RebirthRuntimeState==null||!npc.NativeIdentityReadable)return false;
        try
        {
            var state=npc.RebirthRuntimeState;if((npc.EntityName??string.Empty).Length>256)return false;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(generation);writer.Write(npc.entityId);writer.Write(state.StableId.ToString());writer.Write(state.ProfileId??string.Empty);writer.Write(state.Revision);
                writer.Write((byte)state.Presence);writer.Write((byte)state.OwnershipKind);writer.Write(state.OwnerId??string.Empty);writer.Write((byte)state.Order);writer.Write((byte)state.Travel);
                writer.Write(state.HasGuardPosition);var guard=state.HasGuardPosition?state.GuardPosition:Vector3.zero;writer.Write(guard.x);writer.Write(guard.y);writer.Write(guard.z);writer.Write(npc.EntityName??string.Empty);
                writer.Write(npc is EntityRebirthDogCompanion);
                if(!RebirthNpcProgressionClientCache.TryWriteRecoveryReadback(writer,state.StableId,generation,server))return false;
                if(npc is EntityRebirthDogCompanion dog)
                {
                    if(dog.Buffs==null)return false;writer.Write(dog.belongsPlayerId);writer.Write(dog.Buffs.GetCustomVar("$Leader"));
                    var owner=dog.world.GetEntity(dog.belongsPlayerId) as EntityPlayer;writer.Write(owner!=null&&owner.HasOwnedEntity(dog.entityId));
                }
                bool human=npc is EntityRebirthHumanoidNPC;writer.Write(human);
                if(npc is EntityRebirthHumanoidNPC humanoid)
                {
                    writer.Write(state.HasHumanAppearance);if(state.HasHumanAppearance)RebirthNpcAppearanceBinaryCodec.Write(writer,state.HumanAppearance);
                    var appearance=humanoid.RebirthAppearance;if(!appearance.IsValid)return false;RebirthNpcAppearanceBinaryCodec.Write(writer,appearance);
                    uint revision;Dictionary<RebirthNpcEquipmentSlot,string> slots;
                    if(server){var current=RebirthNpcEquipmentService.CaptureRecoverySnapshot(state.StableId);revision=current.Revision;slots=new Dictionary<RebirthNpcEquipmentSlot,string>(current.Slots);}
                    else if(!RebirthNpcNativeEquipmentBridge.TryObserveRecoverySnapshot(humanoid,generation,out revision,out slots))return false;
                    writer.Write(revision);var keys=new List<RebirthNpcEquipmentSlot>(slots.Keys);keys.Sort();writer.Write(keys.Count);foreach(var key in keys){writer.Write((byte)key);writer.Write(slots[key]);}
                    if(humanoid.equipment==null)return false;int count=humanoid.equipment.GetSlotCount();if(count<0||count>64)return false;writer.Write(count);
                    for(int i=0;i<count;i++)WriteItem(writer,humanoid.equipment.GetSlotItem(i));
                    var cosmetics=humanoid.equipment.GetCosmeticIDs();writer.Write(cosmetics.Length);foreach(int cosmetic in cosmetics)writer.Write(cosmetic);
                    var unlocked=humanoid.equipment.m_unlockedCosmetics;if(unlocked.Count>RebirthNpcNetworkFraming.MaxUnlockedCosmetics)return false;writer.Write(unlocked.Count);foreach(int cosmetic in unlocked)writer.Write(cosmetic);
                    WriteItem(writer,humanoid.inventory?.holdingItemItemValue);
                }
                writer.Flush();using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();return true;
            }
        }catch{return false;}
    }
    private static void WriteItem(BinaryWriter writer,ItemValue value)
    {if(value==null||value.IsEmpty()){writer.Write(string.Empty);return;}string encoded=RebirthNativeItemCodec.Encode(value);if(encoded.Length>262144)throw new InvalidDataException("Native projection item exceeds lossless bound");writer.Write(encoded);}
    internal static void ReceiveName(World world,int entity,RebirthNpcStableId stable,uint epoch,uint connection,ulong generation,ulong incoming,string name)
    {
        if(world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||connection!=RebirthNpcNetworkEpoch.ClientConnectionEpoch||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||!RebirthNpcRecoveryProjectionScope.Observe(generation)||string.IsNullOrEmpty(name)||name.Length>256)return;
        Scope(world);if(Names.TryGetValue(entity,out var prior)&&(prior.Generation>generation||prior.Generation==generation&&prior.Sequence>incoming))return;
        if(Names.Count>=RebirthNpcNetworkProtocol.MaxBaselineActors&&!Names.ContainsKey(entity))return;
        Names[entity]=new NameImage{Entity=entity,Stable=stable,Generation=generation,Sequence=incoming,Name=name};
        if(world.GetEntity(entity) is EntityRebirthNPC npc&&npc.RebirthRuntimeState?.StableId==stable&&npc.EntityName!=name)npc.SetEntityName(name);
    }
    internal static void SendName(EntityRebirthNPC npc,ClientInfo peer)
    {
        var world=GameManager.Instance?.World;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(npc?.RebirthRuntimeState==null||world?.IsRemote()!=false||manager?.IsServer!=true||!Eligible(world,manager,peer)||!RebirthNpcNetworkEpoch.TryGetServerConnectionEpoch(peer.entityId,out uint connection))return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcNameV2>().Setup(npc,connection,NextSequence()),_attachedToEntityId:peer.entityId);
    }
    [HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.SetEntityName),new Type[]{typeof(string)})]
    private static class NativeNameChanged
    {
        private static void Prefix(EntityAlive __instance,out string __state){__state=__instance.EntityName;}
        private static void Postfix(EntityAlive __instance,string __state)
        {
            if(__instance is not EntityRebirthNPC npc||npc.EntityName==__state||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||!ReferenceEquals(npc.world,GameManager.Instance.World))return;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(manager?.IsServer!=true)return;
            try{foreach(var peer in manager.Clients.List)SendName(npc,peer);}catch(Exception ex){Log.Warning("[REBIRTH NPC Recovery] Native name projection pending: "+ex.GetType().Name);}
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthNpcRecoveryProjectionProbe:NetPackage
{
    private RebirthNpcRecoveryProjectionNetwork.Probe image;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthNpcRecoveryProjectionProbe Setup(RebirthNpcRecoveryProjectionNetwork.Probe probe){image=probe;return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);Write(writer,image,true);}
    public override void read(PooledBinaryReader reader){image=Read(reader,true);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthNpcRecoveryProjectionNetwork.Receive(world,image);}
    internal static void Write(BinaryWriter w,RebirthNpcRecoveryProjectionNetwork.Probe p,bool name)
    {if(p==null||p.Operation==Guid.Empty||p.Challenge==Guid.Empty||p.Generation==0||p.Digest?.Length!=64||(name&&(p.Name==null||p.Name.Length>256)))throw new InvalidDataException("Projection image invalid");w.Write((ushort)1);w.Write(p.Operation.ToByteArray());w.Write(p.Challenge.ToByteArray());w.Write(p.Generation);w.Write(p.Sequence);w.Write(p.Entity);w.Write(p.Stable.High);w.Write(p.Stable.Low);w.Write(p.Epoch);w.Write(p.Connection);RebirthNpcNetworkFraming.WriteString(w,p.Digest,64);if(name){w.Write(p.NameSequence);w.Write(p.Revision);RebirthNpcNetworkFraming.WriteString(w,p.Name,256);}}
    internal static RebirthNpcRecoveryProjectionNetwork.Probe Read(BinaryReader r,bool name)
    {
        if(r.ReadUInt16()!=1)throw new InvalidDataException("Recovery projection codec mismatch");byte[] op=r.ReadBytes(16),challenge=r.ReadBytes(16);if(op.Length!=16||challenge.Length!=16)throw new EndOfStreamException();
        var p=new RebirthNpcRecoveryProjectionNetwork.Probe{Operation=new Guid(op),Challenge=new Guid(challenge),Generation=r.ReadUInt64(),Sequence=r.ReadUInt64(),Entity=r.ReadInt32(),Stable=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64()),Epoch=r.ReadUInt32(),Connection=r.ReadUInt32(),Digest=ReadBounded(r,64)};
        if(name){p.NameSequence=r.ReadUInt64();p.Revision=r.ReadUInt32();p.Name=NetPackageRebirthNpcRecoveryProjectionProbe.ReadBounded(r,256);}
        if(p.Operation==Guid.Empty||p.Challenge==Guid.Empty||p.Generation==0||p.Sequence==0||p.Entity<=0||p.Stable.IsEmpty||p.Epoch==0||p.Connection==0||p.Digest.Length!=64)throw new InvalidDataException("Recovery projection identity invalid");return p;
    }
    internal static string ReadBounded(BinaryReader reader,int maximum)
    {
        uint size=0;int shift=0;
        for(int i=0;i<5;i++)
        {
            byte part=reader.ReadByte();if(i==4&&(part&240)!=0)throw new InvalidDataException("Projection string length overflow");
            size|=(uint)(part&127)<<shift;
            if((part&128)==0)
            {
                if(i>0&&(part&127)==0)throw new InvalidDataException("Noncanonical projection string length");
                if(size>(uint)(maximum*3))throw new InvalidDataException("Projection string bound");
                byte[] bytes=reader.ReadBytes((int)size);if(bytes.Length!=(int)size)throw new EndOfStreamException();
                string value=new UTF8Encoding(false,true).GetString(bytes);if(value.Length>maximum)throw new InvalidDataException("Projection string character bound");return value;
            }
            shift+=7;
        }
        throw new InvalidDataException("Projection string length invalid");
    }    public int GetLength()=>2048;
}
[Preserve]
public sealed class NetPackageRebirthNpcRecoveryProjectionAck:NetPackage
{
    private RebirthNpcRecoveryProjectionNetwork.Probe image;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthNpcRecoveryProjectionAck Setup(RebirthNpcRecoveryProjectionNetwork.Probe probe){image=probe;return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);NetPackageRebirthNpcRecoveryProjectionProbe.Write(writer,image,false);}
    public override void read(PooledBinaryReader reader){image=NetPackageRebirthNpcRecoveryProjectionProbe.Read(reader,false);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthNpcRecoveryProjectionNetwork.Acknowledge(this,world,image);}
    public int GetLength()=>192;
}
[Preserve]
public sealed class NetPackageRebirthNpcNameV2:NetPackage
{
    private int entity;private RebirthNpcStableId stable;private uint epoch,connection;private ulong generation,sequence;private string name;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthNpcNameV2 Setup(EntityRebirthNPC npc,uint peerConnection,ulong seq)
    {entity=npc.entityId;stable=npc.RebirthRuntimeState.StableId;epoch=RebirthNpcNetworkEpoch.GetServerEpoch();connection=peerConnection;generation=RebirthNpcRecoveryGeneration.RequireCurrent();sequence=seq;name=npc.EntityName??string.Empty;if(name.Length>256)throw new InvalidDataException("NPC name projection exceeds lossless codec");return this;}
    public override void write(PooledBinaryWriter w){base.write(w);w.Write((ushort)2);w.Write(entity);w.Write(stable.High);w.Write(stable.Low);w.Write(epoch);w.Write(connection);w.Write(generation);w.Write(sequence);RebirthNpcNetworkFraming.WriteString(w,name,256);}
    public override void read(PooledBinaryReader r){if(r.ReadUInt16()!=2)throw new InvalidDataException();entity=r.ReadInt32();stable=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64());epoch=r.ReadUInt32();connection=r.ReadUInt32();generation=r.ReadUInt64();sequence=r.ReadUInt64();name=NetPackageRebirthNpcRecoveryProjectionProbe.ReadBounded(r,256);if(entity<=0||stable.IsEmpty||epoch==0||connection==0||sequence==0||string.IsNullOrEmpty(name))throw new InvalidDataException();}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthNpcRecoveryProjectionNetwork.ReceiveName(world,entity,stable,epoch,connection,generation,sequence,name);}
    public int GetLength()=>2048;
}
