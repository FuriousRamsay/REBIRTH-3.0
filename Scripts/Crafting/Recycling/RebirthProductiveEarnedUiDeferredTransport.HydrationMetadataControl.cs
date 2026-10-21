using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Retransmission is metadata-only. The selected native component is never
    // resent, selected again, or processed again to obtain a missing ACK.
    private sealed class MetadataSend:IDisposable
    {
        internal HydrationDispatch Source;internal byte[] Image;internal string Digest,NativeDigest;
        internal IDisposable Memory;internal double Created,Next;internal int Retries,AckAttempts;internal long ExtraBytes;internal bool Acknowledged;
        private int references=1;private bool closed;
        internal bool Current()=>!closed&&Memory!=null&&Source.Enqueued&&ReferenceEquals(GameManager.Instance?.World,Source.World)&&
            !Source.World.IsRemote()&&ReferenceEquals(Source.World.GetEntity(Source.Actor.entityId),Source.Actor)&&
            ReferenceEquals(Source.Peer.netConnection?[0],Source.Connection)&&!Source.Connection.IsDisconnected()&&Source.Peer.loginDone&&!Source.Peer.disconnecting&&
            Source.Actor.Died==Source.Deaths&&!Source.Actor.IsDead()&&RebirthCamperCharacterAccess.CurrentCreation(Source.Actor)==Source.Creation&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Source.Peer,out var owner)&&owner.StorageKey==Source.Owner;
        internal IDisposable Retain(){if(closed||Memory==null)return null;references++;return new MetadataSendLease(this);}
        private void Release(){if(--references!=0)return;Image=null;var source=Source;Source=null;source?.DropMetadataTransport(ExtraBytes);ExtraBytes=0;Memory?.Dispose();Memory=null;}
        public void Dispose(){if(closed)return;closed=true;Image=null;Release();}
        private sealed class MetadataSendLease:IDisposable
        {private MetadataSend source;internal MetadataSendLease(MetadataSend value){source=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);value?.Release();}}
    }
    private static readonly Dictionary<Guid,MetadataSend> metadataSends=new Dictionary<Guid,MetadataSend>();
    [ThreadStatic]private static MetadataSend writingMetadataAck;
    private static void PruneAcknowledgedMetadataSends()
    {foreach(var pair in metadataSends.Where(value=>value.Value.Acknowledged&&!ReferenceEquals(value.Value,writingMetadataAck)).ToArray())
        {metadataSends.Remove(pair.Key);pair.Value.Dispose();}}
    private sealed class MetadataAck:IDisposable
    {
        internal Guid Group;internal World World;internal INetConnection Connection;internal EntityPlayerLocal Actor;
        internal string Digest,NativeDigest,Creation;internal IDisposable Memory;internal double Created,Next;internal int Tries;
        public void Dispose(){Actor=null;Connection=null;World=null;Memory?.Dispose();Memory=null;}
    }
    private static readonly Dictionary<Guid,MetadataAck> metadataAcks=new Dictionary<Guid,MetadataAck>();
    private static bool RetainHydrationMetadataSend(HydrationDispatch source,byte[] image,string nativeDigest)
    {
        if(image==null)return true;
        PruneAcknowledgedMetadataSends();
        if(metadataSends.Count>=4||metadataSends.Values.Any(value=>ReferenceEquals(value.Source.Connection,source.Connection)))return false;
        var memory=source.Retain();if(memory==null)return false;
        metadataSends.Add(source.Group,new MetadataSend{Source=source,Image=image,Digest=HydrationHash(image),NativeDigest=nativeDigest,
            Memory=memory,Created=UnityEngine.Time.realtimeSinceStartup});return true;
    }
    private static void ForgetUnenqueuedHydrationMetadata(HydrationDispatch source)
    {if(source!=null&&!source.Enqueued&&metadataSends.TryGetValue(source.Group,out var value)&&ReferenceEquals(value.Source,source))
        {metadataSends.Remove(source.Group);value.Dispose();}}
    private static byte[] MetadataNativeDigestBytes(string digest)
    {
        if(digest?.Length!=64)throw new InvalidOperationException();var result=new byte[32];
        for(int index=0;index<result.Length;index++)result[index]=Convert.ToByte(digest.Substring(2*index,2),16);return result;
    }
    private static bool SendHydrationMetadataControl(Guid group,World world,INetConnection connection,byte action,string digest,string nativeDigest)
    {
        if(action==8&&RebirthWorkstationCompletionFixture.Hold("metadataAck",group.ToString("N")))return false;
        if(world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.connectionToServer?[0],connection)||
            world.GetPrimaryPlayer()==null||world.GetPrimaryPlayer().IsDead()||!ValidHydrationDigest(digest)||!ValidHydrationDigest(nativeDigest)||
            !RebirthLogisticsOriginalTransport.CanSendNative(connection)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))return false;
        var packet=NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerV1>().Setup(group,action,world.GetPrimaryPlayer().entityId,digest,32,0,MetadataNativeDigestBytes(nativeDigest),memory);
        packet.RegisterSendQueue();
        try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(packet);return true;}
        finally{packet.SendQueueHandled();}
    }
    private static void NoteHydrationMetadataConsumed(ClientHydration source,string nativeDigest)
    {
        if(source.MetadataSelector==null||!source.Processed||!source.ReadCompleted||source.Reads!=1||source.Unknown||!source.CurrentSource()||metadataAcks.ContainsKey(source.Group)||metadataAcks.Count>=4||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))return;
        metadataAcks.Add(source.Group,new MetadataAck{Group=source.Group,World=source.World,Connection=source.Connection,Actor=source.Actor,
            Creation=source.Creation,Digest=(string)source.MetadataSelector.Attribute("digest"),NativeDigest=nativeDigest,Memory=memory,Created=UnityEngine.Time.realtimeSinceStartup});
    }
    private static void PumpHydrationMetadataControl(World world)
    {
        PruneAcknowledgedMetadataSends();
        double now=UnityEngine.Time.realtimeSinceStartup;
        foreach(var pair in metadataSends.Where(value=>!ReferenceEquals(value.Value.Source.World,world)||value.Value.Source.Enqueued&&!value.Value.Current()||now-value.Value.Created>30d).ToArray())
        {metadataSends.Remove(pair.Key);pair.Value.Dispose();}
        foreach(var pair in metadataAcks.ToArray())
        {
            var ack=pair.Value;
            if(!ReferenceEquals(ack.World,world)||ack.Connection.IsDisconnected()||!ReferenceEquals(ack.World.GetPrimaryPlayer(),ack.Actor)||
                RebirthCamperCharacterAccess.CurrentCreation(ack.Actor)!=ack.Creation||now-ack.Created>30d)
            {metadataAcks.Remove(pair.Key);ack.Dispose();continue;}
            if(ack.Tries>=4||now<ack.Next)continue;ack.Next=now+1d;
            if(SendHydrationMetadataControl(ack.Group,ack.World,ack.Connection,8,ack.Digest,ack.NativeDigest))ack.Tries++;
        }
        foreach(var waiting in waitingMetadataNative.Values.ToArray())
        {
            var source=waiting.Original;if(source.MetadataQueries>=4||now<source.NextMetadataQuery)continue;source.NextMetadataQuery=now+1d;
            if(SendHydrationMetadataControl(source.Group,source.World,source.Connection,7,waiting.MetadataDigest,source.Hash))source.MetadataQueries++;
        }
    }
    private static bool ReceiveHydrationMetadataControl(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        if(packet==null||packet.Action!=7&&packet.Action!=8)return false;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return true;
        if(!metadataSends.TryGetValue(packet.Group,out var sender)){if(packet.Action==8)ReplyHistoricalHydrationMetadata(packet,world);return true;}
        if(!sender.Current()||
            !ReferenceEquals(packet.Sender,sender.Source.Peer)||packet.Player!=sender.Source.Actor.entityId||!packet.ValidEntityIdForSender(packet.Player)||
            packet.Digest!=sender.Digest||packet.Total!=32||packet.Index!=0||packet.Part?.Length!=32||
            BitConverter.ToString(packet.Part).Replace("-","").ToLowerInvariant()!=sender.NativeDigest)return true;
        if(packet.Action==8)
        {
            if(sender.Acknowledged){sender.Source.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,9,sender.Digest));return true;}
            if(sender.AckAttempts>=4||!sender.Source.GrowMetadataTransport(262144))return true;sender.AckAttempts++;sender.ExtraBytes=checked(sender.ExtraBytes+262144);
            writingMetadataAck=sender;
            try
            {
                var record=MetadataAcknowledgedRecord(packet.Group,world,sender.Source.Owner,sender.Source.Creation,sender.Digest,sender.NativeDigest);
                string path=MetadataAcknowledgedPath(packet.Group);
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return true;
                sender.Acknowledged=true;sender.Image=null;
                sender.Source.Peer.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,9,sender.Digest));return true;
            }
            finally{writingMetadataAck=null;}
        }
        if(sender.Image==null||sender.Retries>=4||UnityEngine.Time.realtimeSinceStartup<sender.Next)return true;
        long retryBytes=checked(2L*sender.Image.Length+65536);if(!sender.Source.GrowMetadataTransport(retryBytes))return true;sender.ExtraBytes=checked(sender.ExtraBytes+retryBytes);
        sender.Retries++;sender.Next=UnityEngine.Time.realtimeSinceStartup+1d;
        int count=(sender.Image.Length+NetPackageRebirthUiDeferredHydrationV1.Chunk-1)/NetPackageRebirthUiDeferredHydrationV1.Chunk;
        for(int index=0;index<count;index++)
        {
            int start=index*NetPackageRebirthUiDeferredHydrationV1.Chunk,length=Math.Min(NetPackageRebirthUiDeferredHydrationV1.Chunk,sender.Image.Length-start);
            var lease=sender.Retain();if(lease==null)return true;var part=new byte[length];Buffer.BlockCopy(sender.Image,start,part,0,length);
            var fragment=NetPackageManager.GetPackage<NetPackageRebirthUiDeferredHydrationV1>().Setup(packet.Group,sender.Image.Length,count,index,sender.Digest,part,null,lease).AsMetadata();
            fragment.RegisterSendQueue();try{sender.Source.Peer.SendPackage(fragment);}finally{fragment.SendQueueHandled();}
        }
        return true;
    }
    private static string MetadataAcknowledgedPath(Guid group)=>System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"hydrationMetadata",group.ToString("N")+".xml");
    private static XElement MetadataAcknowledgedRecord(Guid group,World world,string owner,string creation,string digest,string native)=>
        new XElement("workstationCompletionIntent",new XAttribute("operation",group.ToString("N")),new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(world)),new XElement("before",
            new XElement("selectedHydrationMetadataAcknowledged",new XAttribute("version",1),new XAttribute("owner",owner),new XAttribute("creation",creation),
                new XAttribute("metadata",digest),new XAttribute("native",native))));
    private static void ReplyHistoricalHydrationMetadata(NetPackageRebirthUiDeferredOwnerV1 packet,World world)
    {
        if(packet.Sender==null||!packet.ValidEntityIdForSender(packet.Player)||packet.Total!=32||packet.Index!=0||packet.Part?.Length!=32||
            world.GetEntity(packet.Player) is not EntityPlayer actor||actor.IsDead()||!RebirthStablePlayerIdentity.TryFromClientInfo(packet.Sender,out var owner))return;
        string path=MetadataAcknowledgedPath(packet.Group);long size=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(size<1||size>65536||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*size+65536),out var workspace))return;
        using(workspace)
        {
            var expected=MetadataAcknowledgedRecord(packet.Group,world,owner.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(actor),packet.Digest,
                BitConverter.ToString(packet.Part).Replace("-","").ToLowerInvariant());
            if(!XNode.DeepEquals(expected,RebirthWorkstationCompletionPublication.ReadPrimary(path)))return;
            packet.Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthUiDeferredOwnerReplyV1>().Setup(packet.Group,9,packet.Digest));
        }
    }
    private static bool AcceptHydrationMetadataAck(NetPackageRebirthUiDeferredOwnerReplyV1 packet,World world)
    {
        if(packet?.Status!=9)return false;
        if(!metadataAcks.TryGetValue(packet.Group,out var ack)||!ReferenceEquals(ack.World,world)||packet.Digest!=ack.Digest||
            !TryGetReceivedConnection(packet,out var connection)||!ReferenceEquals(connection,ack.Connection))return true;
        metadataAcks.Remove(packet.Group);ack.Dispose();return true;
    }
    internal static object CaptureHydrationMetadataAudit()=>new
    {
        send=metadataSends.Values.Select(value=>new{group=value.Source.Group.ToString("N"),enqueued=value.Source.Enqueued,
            metadataBytes=value.Image?.Length??0,retries=value.Retries,acknowledged=value.Acknowledged}).ToArray(),
        waiting=waitingMetadataNative.Values.Select(value=>new{group=value.Original.Group.ToString("N"),native=value.Original.Hash,
            metadata=value.MetadataDigest,queries=value.Original.MetadataQueries,nativeEntered=false}).ToArray(),
        assembling=assemblingHydrationMetadata.Values.Select(value=>new{group=value.Group.ToString("N"),received=value.Received,count=value.Parts?.Length??0}).ToArray(),
        frozen=readyHydrationMetadata.Values.Select(value=>new{group=value.Group.ToString("N"),digest=value.Digest,expanded=value.Expanded}).ToArray(),
        ack=metadataAcks.Values.Select(value=>new{group=value.Group.ToString("N"),tries=value.Tries}).ToArray()
    };
}
