using Platform;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Full original native body transport custody, not an inventory/source writer.
// Local immutable intent survives after the native detached builder unloads.
// Host Prepared0 and Attempted1 remain distinct from accepted/publicized body.
internal static class RebirthLogisticsNativeBodyIntent
{
    // Exact Prepared native-body projection is connected to the same durable
    // attempt, actual accepted body and native publication gates as same-ref work.
    private static readonly bool ProjectedReconnectEnabled=true;
    internal const int MaximumBytes=524288;
    // Intent identifiers only; no native source/owner slot images or effect permission.
    internal sealed class Command
    {
        internal readonly Guid Operation;internal readonly string World,Owner,Creation,Target,Selection;internal readonly ulong Epoch;internal readonly int Request,Deaths;internal readonly QuickStackRadialAction Action;
        internal Command(Guid operation,string world,string owner,string creation,int deaths,ulong epoch,int request,QuickStackRadialAction action,string target,string selection)
        {Operation=operation;World=world;Owner=owner;Creation=creation;Deaths=deaths;Epoch=epoch;Request=request;Action=action;Target=target;Selection=selection;}
        internal string Binding=>RebirthLogisticsOriginalTransport.Digest(new XElement("command",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",World),new XAttribute("owner",Owner),
            new XAttribute("creation",Creation),new XAttribute("deaths",Deaths),new XAttribute("epoch",Epoch),new XAttribute("request",Request),new XAttribute("action",(byte)Action),
            new XElement("target",Convert.ToBase64String(Encoding.UTF8.GetBytes(Target))),new XElement("selection",Convert.ToBase64String(Encoding.UTF8.GetBytes(Selection)))).ToString(SaveOptions.DisableFormatting));
        internal static Command From(RebirthLogisticsTransferJournal.Record original)=>new Command(original.Operation,original.World,original.Owner,original.Creation,original.Deaths,original.Epoch,original.Request,original.Action,original.Target,original.Selection);
    }
    private sealed class Intent
    {internal XElement Xml;internal Guid Operation;internal string Owner,World,Creation,Binding,Text,Hash;internal int Deaths,Entity;internal byte[] Native;}
    private static World currentWorld;private static Intent pending;private static DateTime next;
    private static bool acknowledged,remotePrepared;private static string preparedNonce;
    private static readonly RebirthRemoteResourceRepairSendQueue wire=new RebirthRemoteResourceRepairSendQueue();
    private static string PathFor(Intent value)=>Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsNativeBodyIntent",value.World,value.Owner,value.Operation.ToString("N")+".xml");
    private static string ReadFile(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader).ToString(SaveOptions.DisableFormatting);
    }
    private static Intent Parse(string text)
    {
        if(text==null||Encoding.UTF8.GetByteCount(text)>MaximumBytes)throw new InvalidDataException();XElement root;
        using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
        if(root.Name!="logisticsNativeBodyIntent"||root.Attributes().Count()!=8||root.Elements().Count()!=1||root.Element("native")==null||root.Element("native").HasElements||
            (string)root.Attribute("version")!="1"||root.Element("native").Value.Length>349528)throw new InvalidDataException();
        var value=new Intent{Xml=root,Text=text,Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),World=(string)root.Attribute("world"),Owner=(string)root.Attribute("owner"),
            Creation=(string)root.Attribute("creation"),Binding=(string)root.Attribute("binding"),Deaths=(int)root.Attribute("deaths"),Entity=(int)root.Attribute("entity")};
        if(value.Operation==Guid.Empty||value.World?.Length!=32||!Guid.TryParseExact(value.World,"N",out var world)||world==Guid.Empty||value.World!=world.ToString("N")||
            value.Owner?.Length!=64||!value.Owner.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||!RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||value.Binding==null||value.Binding.Length!=0&&value.Binding.Length!=64||
            !value.Binding.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||value.Deaths<0||value.Deaths==int.MaxValue||value.Entity<0)throw new InvalidDataException();
        value.Native=Convert.FromBase64String(root.Element("native").Value);if(value.Native.Length<1||value.Native.Length>262144||Convert.ToBase64String(value.Native)!=root.Element("native").Value||root.ToString(SaveOptions.DisableFormatting)!=text)throw new InvalidDataException();
        value.Hash=RebirthLogisticsOriginalTransport.Digest(text);return value;
    }
    private static EntityCreationData Decode(Intent value)
    {
        RebirthLogisticsNativeBodyPreflight.Validate(value.Native,false);var native=new EntityCreationData();
        try
        {
            using(var stream=new MemoryStream(value.Native,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||native.id!=-1||!RebirthLogisticsNativeInvocationJournal.Encode(native).SequenceEqual(value.Native))throw new InvalidDataException();}
            return native;
        }
        catch{native.entityData?.Dispose();native.bagData?.Dispose();throw;}
    }
    private static bool Save(Intent value)
    {
        try
        {
            string path=PathFor(value)+(value.Binding.Length==0?".unbound":string.Empty),temp=path+".tmp";
            if(File.Exists(path+".bak")||File.Exists(temp)&&ReadFile(temp)!=value.Text)return false;
            if(File.Exists(path))return ReadFile(path)==value.Text;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if(!File.Exists(temp))using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {byte[] bytes=new UTF8Encoding(false).GetBytes(value.Text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temp,path);return ReadFile(path)==value.Text;
        }
        catch{return false;}
    }
    private static string HostMarker(Intent value,int stage,string nonce)=>new XElement("nativeBodyTransport",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),
        new XAttribute("world",value.World),new XAttribute("owner",value.Owner),new XAttribute("creation",value.Creation),new XAttribute("binding",value.Binding),new XAttribute("hash",value.Hash),
        new XAttribute("stage",stage),new XAttribute("nonce",nonce??string.Empty)).ToString(SaveOptions.DisableFormatting);
    private static bool SaveHostMarker(Intent value,int stage,string nonce)
    {
        try
        {
            string text=HostMarker(value,stage,nonce),path=PathFor(value)+(stage==0?".prepared":".attempted"),temp=path+".tmp";
            if(File.Exists(path+".bak")||File.Exists(temp)&&ReadFile(temp)!=text)return false;
            if(File.Exists(path))return ReadFile(path)==text;
            if(!File.Exists(temp))using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(temp,path);return ReadFile(path)==text;
        }
        catch{return false;}
    }
    private static bool LoadHostMarker(Intent value)
    {
        try
        {
            string chosen=null;bool found=false,attempted=false;
            foreach(int stage in new[]{0,1})
            {
                string path=PathFor(value)+(stage==0?".prepared":".attempted");if(File.Exists(path+".bak"))return false;
                foreach(string candidate in new[]{path,path+".tmp"})
                {
                    if(!File.Exists(candidate))continue;
                    string text=ReadFile(candidate);var root=XElement.Parse(text);string nonce=(string)root.Attribute("nonce");
                    if(nonce==null||stage==0&&nonce.Length==0||nonce.Length!=0&&(!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||nonce!=id.ToString("N"))||text!=HostMarker(value,stage,nonce)||found&&chosen!=nonce)return false;
                    chosen=nonce;found=true;attempted|=stage==1;
                }
            }
            if(!found)return true;
            // Validate ALL original candidates before promoting any temp; a
            // later Attempted receipt never hides a conflicting Prepared one.
            if(!SaveHostMarker(value,attempted?1:0,chosen))return false;
            preparedNonce=chosen;remotePrepared=true;acknowledged=attempted;return true;
        }
        catch{return false;}
    }
    private static bool TryLoadHostProxy(EntityPlayerLocal player,Guid operation,out Intent value)
    {
        value=null;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||!RebirthLogisticsOwnerReceipt.TryOriginalDeaths(player,out int deaths)||deaths==int.MaxValue||player.Died!=deaths+1)return false;
            var proxy=new Intent{Operation=operation,World=RebirthLogisticsTransferJournal.WorldKey(player.world),Owner=owner.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(player),Deaths=deaths,Entity=player.entityId};
            string path=PathFor(proxy);string[] candidates={path+".attempted",path+".attempted.tmp",path+".prepared",path+".prepared.tmp"};
            foreach(string candidate in candidates)
            {
                if(!File.Exists(candidate))continue;var root=XElement.Parse(ReadFile(candidate));proxy.Binding=(string)root.Attribute("binding");proxy.Hash=(string)root.Attribute("hash");
                if(proxy.Binding?.Length!=64||proxy.Hash?.Length!=64||!proxy.Binding.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||!proxy.Hash.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f')||!LoadHostMarker(proxy))return false;
                // No invented body bytes: only small queries are possible. The
                // host must still own exact canonical Prepared+nonce+fatal data.
                value=proxy;return remotePrepared;
            }
            return false;
        }
        catch{return false;}
    }
    internal static bool Capture(EntityPlayerLocal player,Guid operation,int originalDeaths,EntityCreationData native)
    {
        try
        {
            bool bound=RebirthLogisticsOriginalTransport.TryReadCommandScope(player,operation,out var original);
            if(!bound)
            {
                if(player?.world?.IsRemote()!=true||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var legacyOwner))return false;
                original=new Command(operation,RebirthLogisticsTransferJournal.WorldKey(player.world),legacyOwner.StorageKey,RebirthCamperCharacterAccess.CurrentCreation(player),originalDeaths,0,0,QuickStackRadialAction.Deposit,string.Empty,string.Empty);
            }
            if(original.Deaths!=originalDeaths)return false;
            if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                original==null||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var actualOperation,out _)||actualOperation!=operation||player.Died!=original.Deaths+1||original.World!=RebirthLogisticsTransferJournal.WorldKey(player.world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||owner.StorageKey!=original.Owner||!RebirthLogisticsNativeInvocationJournal.IsNativeBody(new NetPackageRequestToSpawnEntity().Setup(native),player.entityId)||native.id!=-1)return false;
            byte[] bytes=RebirthLogisticsNativeInvocationJournal.Encode(native);
            var root=new XElement("logisticsNativeBodyIntent",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.World),
                new XAttribute("owner",original.Owner),new XAttribute("creation",original.Creation),new XAttribute("binding",bound?original.Binding:string.Empty),
                new XAttribute("deaths",original.Deaths),new XAttribute("entity",player.entityId),new XElement("native",Convert.ToBase64String(bytes)));
            var value=Parse(root.ToString(SaveOptions.DisableFormatting));var checkedNative=Decode(value);checkedNative.entityData?.Dispose();checkedNative.bagData?.Dispose();
            if(!ReferenceEquals(currentWorld,player.world)){wire.Clear();pending=null;currentWorld=player.world;acknowledged=remotePrepared=false;preparedNonce=null;}
            if(pending!=null&&pending.Operation!=value.Operation){wire.Clear();pending=null;acknowledged=remotePrepared=false;preparedNonce=null;}
            if(pending!=null&&pending.Hash!=value.Hash)return false;
            pending=value;next=DateTime.MinValue;acknowledged=remotePrepared=false;preparedNonce=null;
            bool durable=Save(value);if(durable&&value.Binding.Length!=0&&!LoadHostMarker(value)){remotePrepared=true;preparedNonce=null;}
            return durable; // False suppresses first native send; exact memory intent retries persistence.
        }
        catch{return false;}
    }
    private static bool LocalCurrent(EntityPlayerLocal player,Intent value)
        =>player!=null&&value!=null&&ReferenceEquals(GameManager.Instance?.World,currentWorld)&&ReferenceEquals(player.world,currentWorld)&&currentWorld.IsRemote()&&
            ReferenceEquals(currentWorld.GetPrimaryPlayer(),player)&&player.Died==value.Deaths+1&&value.World==RebirthLogisticsTransferJournal.WorldKey(currentWorld)&&
            value.Creation==RebirthCamperCharacterAccess.CurrentCreation(player)&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.StorageKey==value.Owner&&
            RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out _)&&operation==value.Operation;
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(currentWorld,world)){wire.Clear();pending=null;currentWorld=world;acknowledged=remotePrepared=false;preparedNonce=null;next=DateTime.MinValue;}
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true)return;var player=world.GetPrimaryPlayer();
        if(player==null||!RebirthLogisticsOwnerReceipt.HasPendingOriginal(player)||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out _))
        {wire.Clear();pending=null;acknowledged=remotePrepared=false;preparedNonce=null;return;} // Files are retained, no custody/outcome inferred.
        if(pending!=null&&pending.Operation!=operation){wire.Clear();pending=null;acknowledged=remotePrepared=false;preparedNonce=null;next=DateTime.MinValue;}
        if(pending==null&&DateTime.UtcNow>=next)
        {
            next=DateTime.UtcNow.AddSeconds(3);
            try
            {
                if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return;
                var scope=new Intent{Operation=operation,World=RebirthLogisticsTransferJournal.WorldKey(world),Owner=owner.StorageKey};string path=PathFor(scope);
                if(File.Exists(path+".bak"))return;
                string candidate=File.Exists(path)?path:File.Exists(path+".tmp")?path+".tmp":File.Exists(path+".unbound")?path+".unbound":path+".unbound.tmp";
                string text=ReadFile(candidate);var restored=Parse(text);if(restored.Operation!=operation||!LocalCurrent(player,restored))return;
                pending=restored;acknowledged=remotePrepared=false;preparedNonce=null;if(restored.Binding.Length!=0&&!LoadHostMarker(restored)){remotePrepared=true;preparedNonce=null;}
            }
            catch{if(!TryLoadHostProxy(player,operation,out var proxy))return;pending=proxy;}
        }
        if(!LocalCurrent(player,pending)){wire.Clear();return;}
        if(acknowledged)return;
        if(pending.Binding.Length==0)
        {
            if(!RebirthLogisticsOriginalTransport.TryReadCommandScope(player,pending.Operation,out var command)||command.Deaths!=pending.Deaths)return;
            var root=new XElement(pending.Xml);root.SetAttributeValue("binding",command.Binding);var bound=Parse(root.ToString(SaveOptions.DisableFormatting));
            if(!Save(bound))return;pending=bound;if(!LoadHostMarker(bound)){remotePrepared=true;preparedNonce=null;}
        }
        if(DateTime.UtcNow>=next&&!wire.IsQueued(pending.Hash))
        {
            next=DateTime.UtcNow.AddSeconds(3);
            var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
            if(remotePrepared)
            {
                if(preparedNonce==null)return; // Conflicting receipt remains query-only through the original outcome protocol.
                if(RebirthLogisticsOriginalTransport.TryOwnerConnection(out var queryManager,out var queryConnection)&&RebirthLogisticsOriginalTransport.CanSendNative(queryConnection))queryManager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthLogisticsNativeBodyQuery>().Setup(
                    player.entityId,persistent.PrimaryId,pending.Operation,pending.Binding,pending.Hash,preparedNonce),false);
                return;
            }
            if(!Save(pending))return;
            if(!RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection))return;
            var value=pending;var body=new RebirthRemoteResourceFragments.Body(Encoding.UTF8.GetBytes(value.Text));
            wire.Enqueue(value.Hash,body,()=>ReferenceEquals(pending,value)&&!acknowledged&&LocalCurrent(player,value)&&
                RebirthLogisticsOriginalTransport.TryOwnerConnection(out var liveManager,out var liveConnection)&&ReferenceEquals(liveManager,manager)&&ReferenceEquals(liveConnection,connection),()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),index=>manager.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthLogisticsNativeBodyIntent>().Setup(player.entityId,persistent.PrimaryId,value.Operation,body.Hash,body.Length,body.Count,index,body.Part(index)),false));
        }
        wire.Pump(4);
    }
    internal static void Receive(ClientInfo sender,Guid operation,string digest,int total,int count,int index,string text)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||total<1||total>MaximumBytes||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||!(world.GetEntity(sender.entityId) is EntityPlayer actor)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal))return;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,world)))
            {
                if(account==null||!account.TryReserveAllocation(8L*total+4L*262144+1048576,out var allocation))return;
                using(allocation)
                {
                    string scope="logistics-native-body|"+original.World+"|"+original.Owner+"|"+original.Operation.ToString("N");
                    if(!RebirthRemoteResourceFragments.TryAdd(scope,operation,digest,total,count,index,text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var bytes)||bytes==null)return;
                    var intent=Parse(new UTF8Encoding(false,true).GetString(bytes));
                    if(intent.Operation!=operation||intent.Owner!=original.Owner||intent.World!=original.World||intent.Creation!=original.Creation||intent.Binding!=Command.From(original).Binding||
                        intent.Deaths!=original.Deaths||intent.Entity!=fatal.OwnerEntity||actor.Died<original.Deaths||actor.Died>original.Deaths+1)return;
                    if(intent.Entity!=actor.entityId)
                    {
                        // A positive pre-hold index None/Prepared can retain
                        // the immutable fatal ECD before reference projection.
                        // No native invocation occurs in this preparation lane.
                        if(!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out _,out _,out _))
                        {
                            var historicalNative=Decode(intent);
                            try
                            {
                                var historicalPacket=new NetPackageRequestToSpawnEntity().Setup(historicalNative);historicalPacket.Sender=sender;
                                if(!RebirthLogisticsNativeInvocationJournal.TryPrepareHistorical(original,fatal,actor,historicalPacket))return;
                            }
                            finally{historicalNative.entityData?.Dispose();historicalNative.bagData?.Dispose();}
                        }
                        if(!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||
                            !RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var oldNonce,out var oldNative,out int oldState)||oldNative!=Convert.ToBase64String(intent.Native))return;
                        SendAck(sender,actor,intent,oldState==1||RebirthLogisticsBodyReferenceJournal.WasAttemptedOriginal(original,oldNative,oldNonce)?1:0,oldNonce);return;
                    }
                    var native=Decode(intent);
                    try
                    {
                        var packet=new NetPackageRequestToSpawnEntity().Setup(native);packet.Sender=sender;
                        if(!RebirthLogisticsNativeInvocationJournal.IsNativeBody(packet,intent.Entity))return;
                        if(RebirthLogisticsNativeInvocationJournal.IsAttempted(original,intent.Native))
                        {RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var priorNonce,out _,out _);SendAck(sender,actor,intent,1,priorNonce??string.Empty);return;}
                        if(RebirthLogisticsNativeInvocationJournal.TryPrepare(original,fatal,actor,packet)&&RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var nonce,out var stored,out int state)&&state==0&&stored==Convert.ToBase64String(intent.Native))
                            SendAck(sender,actor,intent,0,nonce);
                    }
                    finally{native.entityData?.Dispose();native.bagData?.Dispose();}
                }
            }
        }
        catch{/* Prepared is retryable; attempted/unknown remains non-replayable. */}
    }
    private static void SendAck(ClientInfo sender,EntityPlayer actor,Intent intent,int stage,string nonce)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;if(!RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out var peer,out var connection)||!ReferenceEquals(peer,sender)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsNativeBodyAck>().Setup(intent.Operation,intent.Binding,intent.Hash,stage,nonce),_attachedToEntityId:actor.entityId);
    }
    internal static void Query(ClientInfo sender,Guid operation,string command,string hash,string nonce)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||
                !(world.GetEntity(sender.entityId) is EntityPlayer actor)||!RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||Command.From(original).Binding!=command||
                !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||!RebirthLogisticsNativeInvocationJournal.TryReadStored(original,out var storedNonce,out var nativeText,out int state)||storedNonce!=nonce)return;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,world)))
            {
                if(account==null||!account.TryReserveAllocation(8L*MaximumBytes+4L*262144+1048576,out var allocation))return;
                using(allocation)
                {
                    var root=new XElement("logisticsNativeBodyIntent",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("world",original.World),new XAttribute("owner",original.Owner),
                        new XAttribute("creation",original.Creation),new XAttribute("binding",command),new XAttribute("deaths",original.Deaths),new XAttribute("entity",fatal.OwnerEntity),new XElement("native",nativeText));
                    var intent=Parse(root.ToString(SaveOptions.DisableFormatting));if(intent.Hash!=hash)return;
                    if(state==1||RebirthLogisticsBodyReferenceJournal.WasAttemptedOriginal(original,nativeText,nonce))
                    {SendAck(sender,actor,intent,1,nonce);return;}
                    if(fatal.OwnerEntity!=actor.entityId)
                    {
                        if(!ProjectedReconnectEnabled)return;
                        if(!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||!RebirthLogisticsBodyReferenceJournal.TryRecoverMetadata(original))return;
                        if(RebirthLogisticsBodyReferenceJournal.WasAttemptedOriginal(original,nativeText,nonce)){SendAck(sender,actor,intent,1,nonce);return;}
                        if(RebirthLogisticsBodyReferenceJournal.HasCandidate(original))
                        {
                            using(var prior=RebirthLogisticsBodyReferenceJournal.TryOpenFrozen(original))
                            {
                                if(prior==null||prior.State!="prepared"&&prior.State!="rejected")return;
                                if(prior.State=="prepared"&&prior.Witness.NativeOwner!=actor.entityId&&!RebirthLogisticsBodyReferenceJournal.TryRejectPrepared(original,actor,sender,prior.Witness.Projected,nonce))return;
                            }
                        }
                        if(!RebirthLogisticsBodyReferenceJournal.TryFreezeCurrent(original,actor,sender))return;
                        using(var frozen=RebirthLogisticsBodyReferenceJournal.TryOpenFrozen(original))
                        {
                            if(frozen==null||frozen.State!="prepared"||!frozen.IsCurrent(actor,sender))return;
                            var projected=RebirthLogisticsBodyReferenceWitness.DecodeProjected(frozen.Witness);
                            try
                            {
                                var projectedPacket=new NetPackageRequestToSpawnEntity().Setup(projected);projectedPacket.Sender=sender;
                                using(var permit=RebirthLogisticsNativeInvocationJournal.PermitProjectedPrepared(original,sender,nonce,projectedPacket))
                                {if(permit==null)return;projectedPacket.ProcessPackage(world,GameManager.Instance);}
                            }
                            finally{projected.entityData?.Dispose();projected.bagData?.Dispose();}
                        }
                        if(RebirthLogisticsBodyReferenceJournal.WasAttemptedOriginal(original,nativeText,nonce))SendAck(sender,actor,intent,1,nonce);
                        return;
                    }
                    var native=Decode(intent);
                    try
                    {
                    var packet=new NetPackageRequestToSpawnEntity().Setup(native);packet.Sender=sender;
                    if(!RebirthLogisticsNativeInvocationJournal.IsNativeBody(packet,fatal.OwnerEntity))return;
                    if(state==0)
                    {
                        if((fatal.Physical?.Stage??0)<4||fatal.Physical.Requests!=1||fatal.Physical.BodyByteRefused||fatal.Physical.BodyClass!=native.entityClass||!fatal.Physical.BodyPosition.Equals(native.pos)||
                            fatal.Physical.BodyBagHash!=RebirthLogisticsPhysicalLossWitness.BlobHash(native.bagData)||fatal.Physical.BodyEntityHash!=RebirthLogisticsPhysicalLossWitness.BlobHash(native.entityData))return;
                        using(var permit=RebirthLogisticsNativeInvocationJournal.PermitPrepared(original,sender,intent.Native,nonce,packet))
                        {if(permit==null)return;packet.ProcessPackage(world,GameManager.Instance);} // SAME sole A/B native gates.
                    }
                    if(RebirthLogisticsNativeInvocationJournal.IsAttempted(original,intent.Native))SendAck(sender,actor,intent,1,nonce);
                    }
                    finally{native.entityData?.Dispose();native.bagData?.Dispose();}
                }
            }
        }
        catch{/* Missing/unknown Prepared never becomes execution permission. */}
    }
    internal static void Ack(World world,Guid operation,string binding,string hash,int stage,string nonce)
    {
        if(world?.IsRemote()!=true||!ReferenceEquals(world,currentWorld)||pending==null||pending.Operation!=operation||pending.Binding!=binding||pending.Hash!=hash||stage<0||stage>1||
            nonce==null||stage==0&&nonce.Length==0||nonce.Length!=0&&(!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty||nonce!=id.ToString("N"))||remotePrepared&&preparedNonce!=null&&preparedNonce!=nonce)return;
        if(!SaveHostMarker(pending,stage,nonce))return;
        preparedNonce=nonce;remotePrepared=true;acknowledged=stage==1;wire.Clear();next=DateTime.MinValue;
        // Only the positive durable Prepared nonce enables a small query. This
        // receipt never releases owner custody or proves body publication.
    }

}

public sealed class NetPackageRebirthLogisticsNativeBodyIntent:NetPackage
{
    private int entity,total,count,index;private PlatformUserIdentifierAbs user;private Guid operation;private string hash,text;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsNativeBodyIntent Setup(int entity,PlatformUserIdentifierAbs user,Guid operation,string hash,int total,int count,int index,string text)
    {this.entity=entity;this.user=user;this.operation=operation;this.hash=hash;this.total=total;this.count=count;this.index=index;this.text=text;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);operation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N");hash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);total=reader.ReadInt32();count=reader.ReadInt32();index=reader.ReadInt32();text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,12000);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,hash,64);writer.Write(total);writer.Write(count);writer.Write(index);RebirthSurvivorNetworkCodec.WriteString(writer,text,12000);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)&&ValidEntityIdForSender(entity)&&ValidUserIdForSender(user))RebirthLogisticsNativeBodyIntent.Receive(Sender,operation,hash,total,count,index,text);}
    public int GetLength()=>256+2*(text?.Length??0);
}
public sealed class NetPackageRebirthLogisticsNativeBodyAck:NetPackage
{
    private Guid operation;private string binding,hash,nonce;private int stage;public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsNativeBodyAck Setup(Guid operation,string binding,string hash,int stage,string nonce){this.operation=operation;this.binding=binding;this.hash=hash;this.stage=stage;this.nonce=nonce;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadByte()!=2)throw new InvalidDataException();operation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N");binding=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);hash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);stage=reader.ReadByte();nonce=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)2);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,binding,64);RebirthSurvivorNetworkCodec.WriteString(writer,hash,64);writer.Write((byte)stage);RebirthSurvivorNetworkCodec.WriteString(writer,nonce??string.Empty,32);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsNativeBodyIntent.Ack(world,operation,binding,hash,stage,nonce);}
    public int GetLength()=>320;
}

public sealed class NetPackageRebirthLogisticsNativeBodyQuery:NetPackage
{
    private int entity;private PlatformUserIdentifierAbs user;private Guid operation;private string command,hash,nonce;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsNativeBodyQuery Setup(int entity,PlatformUserIdentifierAbs user,Guid operation,string command,string hash,string nonce)
    {this.entity=entity;this.user=user;this.operation=operation;this.command=command;this.hash=hash;this.nonce=nonce;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);operation=Guid.ParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N");command=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);hash=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);nonce=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,command,64);RebirthSurvivorNetworkCodec.WriteString(writer,hash,64);RebirthSurvivorNetworkCodec.WriteString(writer,nonce,32);}
    public override void ProcessPackage(World world,GameManager callbacks){if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)&&ValidEntityIdForSender(entity)&&ValidUserIdForSender(user))RebirthLogisticsNativeBodyIntent.Query(Sender,operation,command,hash,nonce);}
    public int GetLength()=>1024;
}


// Pure exact installed v38 backpack envelope admission BEFORE native read can
// allocate declared blobs/stats/trader/profile state. No native state is set.
internal static class RebirthLogisticsNativeBodyPreflight
{
    internal const int MaximumFrame=262144;
    // Fixed v38 persistency envelope104 plus duplicated ushort-bounded body.
    internal const int MaximumCanonicalBytes=104+2*ushort.MaxValue;
    private static void Skip(Stream stream,int count){if(count<0||count>stream.Length-stream.Position)throw new EndOfStreamException();stream.Position+=count;}
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    internal static void Validate(byte[] frame,bool allocated)
    {
        if(frame==null||frame.Length<1||frame.Length>MaximumFrame)throw new InvalidDataException();
        using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=38||!EntityClass.list.TryGetValue(reader.ReadInt32(),out var definition)||definition.classname!=typeof(EntityBackpack))throw new InvalidDataException();
            int id=reader.ReadInt32();if(allocated?id<0:id!=-1)throw new InvalidDataException();
            for(int i=0;i<7;i++){float value=reader.ReadSingle();if(float.IsNaN(value)||float.IsInfinity(value)||i==0&&value<0)throw new InvalidDataException();}
            Flag(reader);if(reader.ReadInt32()!=4)throw new InvalidDataException();Skip(stream,8);
            if(Flag(reader))throw new InvalidDataException();Skip(stream,2);if(!Flag(reader))throw new InvalidDataException();
            int bag=reader.ReadInt32();if(bag<1||bag>131072)throw new InvalidDataException();Skip(stream,bag);Skip(stream,15);
            int body=reader.ReadUInt16();if(body<4||body<bag+4)throw new InvalidDataException();Skip(stream,body);if(Flag(reader))throw new InvalidDataException();Skip(stream,28);
            if(stream.Position!=stream.Length)throw new InvalidDataException();
        }
    }
}
