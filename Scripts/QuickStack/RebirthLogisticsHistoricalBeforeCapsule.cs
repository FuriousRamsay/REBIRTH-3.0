using Platform;
using System;
using System.Linq;
using System.Xml;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Exact owner-native historical attestation. This is historical
// owner evidence, never a native current player file or payment capability.
internal static class RebirthLogisticsHistoricalBeforeCapsule
{
    private const long MaximumBytes=16L*1024*1024;
    private sealed class BoundedWrite:Stream
    {
        private readonly Stream inner;internal BoundedWrite(Stream value){inner=value;}
        public override bool CanRead=>false;public override bool CanSeek=>inner.CanSeek;public override bool CanWrite=>true;
        public override long Length=>inner.Length;public override long Position{get=>inner.Position;set{if(value<0||value>MaximumBytes)throw new InvalidDataException();inner.Position=value;}}
        public override void Flush()=>inner.Flush();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s){long target=s==SeekOrigin.Begin?o:s==SeekOrigin.Current?checked(inner.Position+o):checked(inner.Length+o);if(target<0||target>MaximumBytes)throw new InvalidDataException();return inner.Seek(target,SeekOrigin.Begin);}public override void SetLength(long n){if(n<0||n>MaximumBytes)throw new InvalidDataException();inner.SetLength(n);}
        public override void Write(byte[] b,int o,int c){if(c<0||inner.Position>MaximumBytes-c)throw new InvalidDataException("Historical native frame exceeds original bound.");inner.Write(b,o,c);}
        public override void WriteByte(byte v){if(inner.Position>=MaximumBytes)throw new InvalidDataException();inner.WriteByte(v);}
    }
    internal static bool TryPrepareLocal(EntityPlayerLocal player,RebirthLogisticsNativeBodyIntent.Command command,string acceptedNonce)
    {
        try
        {
            var world=player?.world;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||!ReferenceEquals(world.GetPrimaryPlayer(),player)||!ReferenceEquals(world.GetEntity(player.entityId),player)||player.IsDead()||
                player.Died!=command.Deaths||command.World!=RebirthLogisticsTransferJournal.WorldKey(world)||command.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||owner.StorageKey!=command.Owner||!RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out int phase)||operation!=command.Operation||phase!=0||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=1f||
                !RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out var liveNonce)||liveNonce!=acceptedNonce)return false;
            var scope=new RebirthLogisticsTransferJournal.Record{Operation=command.Operation,World=command.World,Owner=command.Owner,Creation=command.Creation,Deaths=command.Deaths,Epoch=command.Epoch,Request=command.Request,Action=command.Action,Target=command.Target,Selection=command.Selection};
            if(!RebirthLogisticsHeaderAdmission.TryAcceptedNonce(player,scope,out var accepted)||accepted!=acceptedNonce)return false;
            string directory=Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsHeaderAdmission",command.World,command.Owner,operation.ToString("N"));
            string body=Path.Combine(directory,"native0.ttp"),metadata=body+".xml";
            if(!Directory.Exists(directory)||File.Exists(body)||File.Exists(metadata)||File.Exists(body+".tmp")||File.Exists(metadata+".tmp")||File.Exists(body+".bak")||File.Exists(metadata+".bak"))return false;
            if(!RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out var saved)||saved.id!=player.entityId||saved.deaths!=command.Deaths||saved.bDead||
                !RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var savedNonce)||savedNonce!=acceptedNonce||!RebirthLogisticsDeathPartition.Same(RebirthGearPlayerDataInventory.ReadSlots(saved,true),player.bag?.ItemGrid?.items)||
                !RebirthLogisticsDeathPartition.Same(RebirthGearPlayerDataInventory.ReadSlots(saved,false),player.inventory?.ItemGrid?.items))return false;
            // Same existing owner original directory; archive follows header
            // metadata. No separate inventory repository or synthetic Before.
            using(var file=new FileStream(body+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var bounded=new BoundedWrite(file);
                using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
                {writer.SetBaseStream(bounded);writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);saved.Write(writer,StreamModeWrite.Persistency);writer.Flush();file.Flush(true);}
            }
            // The detached file was the sole serialized source. Capture only
            // bounded metadata; transport materialization is not enabled here.
            string hash;long length;
            using(var file=new FileStream(body+".tmp",FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())
            {length=file.Length;if(length<1||length>MaximumBytes)return false;hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
            var root=new XElement("logisticsHistoricalNativeZero",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("world",command.World),new XAttribute("owner",command.Owner),new XAttribute("creation",command.Creation),new XAttribute("deaths",command.Deaths),new XAttribute("entity",saved.id),new XAttribute("header",acceptedNonce),new XAttribute("binding",command.Binding),new XAttribute("bytes",length),new XAttribute("sha256",hash));
            var bytes=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(bytes.Length>4096)return false;
            using(var file=new FileStream(metadata+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            File.Move(body+".tmp",body);File.Move(metadata+".tmp",metadata);return true;
        }
        catch{return false;} // Failed candidates retained, never native proof.
    }
    private static string LocalPath(RebirthLogisticsNativeBodyIntent.Command c)=>Path.Combine(GameIO.GetPlayerDataDir(),"RebirthLogisticsHeaderAdmission",c.World,c.Owner,c.Operation.ToString("N"),"native0.ttp");
    private static string HostPath(string owner,Guid operation)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHistoricalBefore",owner,operation.ToString("N")+".owner0"),archive=Path.Combine(Path.GetDirectoryName(root),"archive",Path.GetFileName(root));
        if(File.Exists(root)&&File.Exists(archive))throw new InvalidDataException();return File.Exists(archive)?archive:root;
    }
    private static XElement HostMetadata(string body)
    {
        string root=Path.GetDirectoryName(body);if(Path.GetFileName(root)=="archive")root=Path.GetDirectoryName(root);
        string primary=Path.Combine(root,Path.GetFileName(body)+".xml"),archive=Path.Combine(root,"archive",Path.GetFileName(body)+".xml");
        if(File.Exists(primary)&&File.Exists(archive))throw new InvalidDataException();var meta=Metadata(File.Exists(primary)?primary:archive);
        if(meta.Name!="ownerHistoricalZero"||meta.Attributes().Count()!=4||meta.Attributes().Any(a=>a.Name!="version"&&a.Name!="hash"&&a.Name!="header"&&a.Name!="binding")||meta.HasElements||(string)meta.Attribute("version")!="1")throw new InvalidDataException();return meta;
    }
    private static bool HostBudget(int incoming)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHistoricalBefore");if(!Directory.Exists(root))return true;int owners=0,pending=0;long bytes=incoming;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            if(++owners>4096||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
            if(Directory.EnumerateFiles(directory,"*.tmp").Any()||Directory.EnumerateFiles(directory,"*.bak").Any())return false;
            foreach(string file in Directory.EnumerateFiles(directory,"*.owner0"))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file),"N",out var operation))return false;var meta=HostMetadata(file);
                if(RebirthLogisticsTransferJournal.TryRead(Path.GetFileName(directory),operation,out var terminal)&&terminal.Phase==4&&(string)meta.Attribute("binding")==RebirthLogisticsNativeBodyIntent.Command.From(terminal).Binding)
                {string archive=Path.Combine(directory,"archive");Directory.CreateDirectory(archive);if(File.Exists(Path.Combine(archive,Path.GetFileName(file))))return false;File.Move(file,Path.Combine(archive,Path.GetFileName(file)));File.Move(file+".xml",Path.Combine(archive,Path.GetFileName(file)+".xml"));continue;}
                if(++pending>128)return false;long length=new FileInfo(file).Length;if(length<1||length>MaximumWire)return false;bytes+=length;if(bytes>64L*1024*1024)return false;
            }
        }
        return pending<128&&bytes<=64L*1024*1024;
    }
    private static byte[] ReadHostBody(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumWire||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();return File.ReadAllBytes(path);
    }
    private static string BytesHash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private const int MaximumWire=9*1024*1024+512;
    private static INetConnection acknowledgedConnection;private static string acknowledgedHash;
    private static Guid captureOperation;private static DateTime nextCapture;
    private static World sendWorld;private static Guid sendOperation;private static RebirthRemoteResourceFragments.Body sendBody;private static DateTime nextSend;
    private static readonly RebirthRemoteResourceRepairSendQueue sends=new RebirthRemoteResourceRepairSendQueue();
    private static string assemblyKey;private static DateTime assemblyUntil;
    private static XElement Metadata(string path)
    {
        if(!File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak")||new FileInfo(path).Length>4096||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))return XElement.Load(reader);
    }
    private static PlayerDataFile ReadLocalCapsule(RebirthLogisticsNativeBodyIntent.Command c,string nonce,out string fullHash)
    {
        string path=LocalPath(c),bodySource=File.Exists(path)?path:path+".tmp",metaSource=File.Exists(path+".xml")?path+".xml":path+".xml.tmp";var m=Metadata(metaSource);fullHash=(string)m.Attribute("sha256");
        if(m.Name!="logisticsHistoricalNativeZero"||m.Attributes().Count()!=11||(string)m.Attribute("version")!="1"||(string)m.Attribute("operation")!=c.Operation.ToString("N")||(string)m.Attribute("world")!=c.World||(string)m.Attribute("owner")!=c.Owner||(string)m.Attribute("creation")!=c.Creation||(int)m.Attribute("deaths")!=c.Deaths||(string)m.Attribute("header")!=nonce||(string)m.Attribute("binding")!=c.Binding||fullHash?.Length!=64||File.Exists(path+".bak")||File.Exists(path)&&File.Exists(path+".tmp")||File.Exists(path+".xml")&&File.Exists(path+".xml.tmp")||(File.GetAttributes(bodySource)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        using(var file=new FileStream(bodySource,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete))
        {
            if(file.Length<1||file.Length>MaximumBytes||file.Length!=(long)m.Attribute("bytes"))throw new InvalidDataException();
            using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=fullHash)throw new InvalidDataException();file.Position=0;
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(file);if(reader.ReadChar()!='t'||reader.ReadChar()!='t'||reader.ReadChar()!='p'||reader.ReadByte()!=0||reader.ReadByte()!=PlayerDataFile.cFileVersion)throw new InvalidDataException();var saved=new PlayerDataFile();saved.Read(reader,PlayerDataFile.cFileVersion,StreamModeRead.Persistency);if(file.Position!=file.Length||saved.id!=(int)m.Attribute("entity"))throw new InvalidDataException();if(bodySource!=path)File.Move(bodySource,path);if(metaSource!=path+".xml")File.Move(metaSource,path+".xml");return saved;}
        }
    }
    private static byte[] Projection(PlayerDataFile saved,string fullHash)
    {
        var spans=new[]{saved.bagData,saved.inventoryData,saved.buffData};int[] bounds={4*1024*1024,4*1024*1024,1048576};
        for(int i=0;i<3;i++)if(spans[i]==null||spans[i].Length<1||spans[i].Length>bounds[i])throw new InvalidDataException();
        using(var stream=new MemoryStream(checked(87+(int)spans.Sum(x=>x.Length))))using(var writer=new BinaryWriter(stream))
        {writer.Write((byte)1);writer.Write(saved.id);writer.Write(saved.deaths);writer.Write(saved.bDead);writer.Write(fullHash);foreach(var span in spans){writer.Write((int)span.Length);span.WriteTo(stream);}writer.Flush();if(stream.Length>MaximumWire)throw new InvalidDataException();return stream.ToArray();}
    }
    private static PlayerDataFile DecodeProjection(byte[] bytes)
    {
        if(bytes==null||bytes.Length<82||bytes.Length>MaximumWire)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadByte()!=1)throw new InvalidDataException();int entity=reader.ReadInt32(),deaths=reader.ReadInt32();bool dead=reader.ReadBoolean();
            // Exact known one-byte native BinaryWriter string framing for64hex.
            if(reader.ReadByte()!=64)throw new InvalidDataException();var hash=Encoding.ASCII.GetString(reader.ReadBytes(64));if(hash.Length!=64||hash.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))throw new InvalidDataException();
            var offsets=new long[3];var lengths=new int[3];int[] bounds={4*1024*1024,4*1024*1024,1048576};
            for(int i=0;i<3;i++){int n=reader.ReadInt32();if(n<1||n>bounds[i]||n>stream.Length-stream.Position)throw new InvalidDataException();lengths[i]=n;offsets[i]=stream.Position;stream.Position+=n;}if(stream.Position!=stream.Length||dead||entity<0||deaths<0)throw new InvalidDataException();
            var spans=new MemoryStream[3];for(int i=0;i<3;i++)spans[i]=new MemoryStream(bytes,(int)offsets[i],lengths[i],false,true);
            return new PlayerDataFile{id=entity,deaths=deaths,bDead=false,bagData=spans[0],inventoryData=spans[1],buffData=spans[2]};
        }
    }
    internal static void ResetWorld(World world){if(!ThreadManager.IsMainThread())return;if(!ReferenceEquals(sendWorld,world)){sends.Clear();sendBody=null;sendOperation=Guid.Empty;acknowledgedConnection=null;acknowledgedHash=null;sendWorld=world;assemblyKey=null;}}
    internal static void ForgetTransport(Guid operation){if(sendOperation==operation){sends.Clear();sendBody=null;sendOperation=Guid.Empty;acknowledgedConnection=null;acknowledgedHash=null;}}
    internal static void OwnerTick(EntityPlayerLocal player,RebirthLogisticsNativeBodyIntent.Command c,string nonce)
    {
        try
        {
            if(!ReferenceEquals(sendWorld,player.world)){sends.Clear();sendBody=null;sendOperation=Guid.Empty;acknowledgedConnection=null;acknowledgedHash=null;sendWorld=player.world;}
            if(player.world?.IsRemote()!=true||c.Creation!=RebirthCamperCharacterAccess.CurrentCreation(player)||player.Died<c.Deaths||player.Died>c.Deaths+1)return;
            if(string.IsNullOrEmpty(nonce))
            {
                var scope=new RebirthLogisticsTransferJournal.Record{Operation=c.Operation,World=c.World,Owner=c.Owner,Creation=c.Creation,Deaths=c.Deaths,Epoch=c.Epoch,Request=c.Request,Action=c.Action,Target=c.Target,Selection=c.Selection};
                if(!RebirthLogisticsHeaderAdmission.TryAcceptedNonce(player,scope,out nonce)||!RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out var actualNonce)||actualNonce!=nonce)return;
            }
            if(!File.Exists(LocalPath(c))){if(captureOperation!=c.Operation){captureOperation=c.Operation;nextCapture=DateTime.MinValue;}if(DateTime.UtcNow>=nextCapture){nextCapture=DateTime.UtcNow.AddSeconds(3);TryPrepareLocal(player,c,nonce);}}
            if(!player.IsDead()&&player.Died==c.Deaths)return; // Local historical capture is ready; no live-operation retransmission.
            if(sendBody==null||sendOperation!=c.Operation)
            {sends.Clear();sendBody=null;sendOperation=Guid.Empty;var native=ReadLocalCapsule(c,nonce,out var fullHash);if(native.deaths!=c.Deaths||native.bDead||!RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(native,out var operation,out int phase,out float held)||operation!=c.Operation||phase!=0||held!=1f||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(native,out var accepted)||accepted!=nonce)return;sendBody=new RebirthRemoteResourceFragments.Body(Projection(native,fullHash));sendOperation=c.Operation;nextSend=DateTime.MinValue;}
            if(!RebirthLogisticsOriginalTransport.TryOwnerConnection(out var manager,out var connection))return;
            var body=sendBody;var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
            if(ReferenceEquals(acknowledgedConnection,connection)&&acknowledgedHash==body.Hash){sends.Clear();return;}
            string key="logistics-historical-zero|"+c.World+"|"+c.Owner+"|"+c.Operation.ToString("N")+"|"+body.Hash;var world=player.world;
            bool Current()=>ReferenceEquals(sendWorld,world)&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&sendOperation==c.Operation&&ReferenceEquals(sendBody,body)&&RebirthCamperCharacterAccess.CurrentCreation(player)==c.Creation&&RebirthLogisticsTransferJournal.WorldKey(world)==c.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.StorageKey==c.Owner&&RebirthLogisticsOriginalTransport.TryOwnerConnection(out var liveManager,out var liveConnection)&&ReferenceEquals(liveManager,manager)&&ReferenceEquals(liveConnection,connection);
            if(DateTime.UtcNow>=nextSend&&!sends.IsQueued(key)&&sends.Enqueue(key,body,Current,()=>RebirthLogisticsOriginalTransport.CanSendNative(connection),i=>manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthLogisticsHistoricalZero>().Setup(player.entityId,persistent.PrimaryId,c.Operation,nonce,body.Hash,body.Length,i,body.Count,body.Part(i)),false)))nextSend=DateTime.UtcNow.AddSeconds(3);
            sends.Pump(8);
        }
        catch{/* Existing immutable capsule remains unknown, never recaptured. */}
    }
    internal static bool ValidateHostOriginal(RebirthLogisticsTransferJournal.Record original,string nonce,string digest)
    {
        try
        {
            string path=HostPath(original.Owner,original.Operation);var meta=HostMetadata(path);if((string)meta.Attribute("hash")!=digest||(string)meta.Attribute("header")!=nonce||(string)meta.Attribute("binding")!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding||File.Exists(path+".tmp")||File.Exists(path+".bak")||new FileInfo(path).Length>MaximumWire)return false;
            byte[] bytes=ReadHostBody(path);if(BytesHash(bytes)!=digest)return false;var saved=DecodeProjection(bytes);
            return saved.deaths==original.Deaths&&RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(saved,out var op,out int phase,out float held)&&op==original.Operation&&phase==0&&held==1f&&RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var header)&&header==nonce&&
                RebirthLogisticsDeathPartition.Same(original.OriginalOwnerBag,RebirthGearPlayerDataInventory.ReadSlots(saved,true))&&RebirthLogisticsDeathPartition.Same(original.OriginalOwnerBelt,RebirthGearPlayerDataInventory.ReadSlots(saved,false));
        }
        catch{return false;}
    }
    internal static void TryAdoptPending(EntityPlayer actor,Guid operation){if(RebirthLogisticsHeaderAdmission.TryHistoricalNonce(actor,operation,out var nonce)&&RebirthLogisticsHeaderAdmission.TryHistoricalHeader(actor,operation,nonce,out var command,out _)){try{RecoverHostCandidate(command,nonce);}catch{return;}TryAdopt(actor,operation,nonce);}}
    internal static void TryAdopt(EntityPlayer actor,Guid operation,string nonce)
    {
        try
        {
            if(!RebirthLogisticsHeaderAdmission.TryHistoricalHeader(actor,operation,nonce,out var c,out int entity))return;string path=HostPath(c.Owner,operation);var meta=HostMetadata(path);string hash=(string)meta.Attribute("hash");
            if(File.Exists(path+".tmp")||File.Exists(path+".bak")||new FileInfo(path).Length>MaximumWire)return;byte[] bytes=ReadHostBody(path);if(BytesHash(bytes)!=hash)return;var saved=DecodeProjection(bytes);
            var original=new RebirthLogisticsTransferJournal.Record{Operation=c.Operation,World=c.World,Owner=c.Owner,Creation=c.Creation,Deaths=c.Deaths,Epoch=c.Epoch,Request=c.Request,Action=c.Action,Target=c.Target,Selection=c.Selection};
            if(saved.id!=entity||saved.deaths!=c.Deaths||!RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(saved,out var op,out int phase,out float held)||op!=operation||phase!=0||held!=1f||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var header)||header!=nonce)return;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);if(bag==null||belt==null)return;original.OriginalOwnerBag=bag;original.OriginalOwnerBelt=belt;original.BeforeOwner=bag.Select(_=>ItemStack.Empty.Clone()).ToArray();original.DebitedOwner=ItemStack.Clone(original.BeforeOwner);original.AfterOwner=ItemStack.Clone(original.BeforeOwner);
            if(!ValidateHostOriginal(original,nonce,hash)||!RebirthLogisticsNativeDeathReceipt.TryImportHistoricalFatal(original,nonce)||!RebirthLogisticsHeaderAdmission.EndHistoricalHeader(actor,operation,nonce))return;
            RebirthLogisticsPreAdmissionReceipt.PrepareHistorical(original,saved,hash);RebirthLogisticsPreAdmissionReceipt.Recover(actor,operation);
            RebirthLogisticsNativeDeathReceipt.ObserveSaved(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId));
        }
        catch{/* Historical attestation never claims server native publication. */}
    }
    private static void SendAck(ClientInfo sender,Guid operation,string nonce,string digest)
    {
        var actor=GameManager.Instance?.World?.GetEntity(sender.entityId) as EntityPlayer;
        if(!RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out var peer,out var connection)||!ReferenceEquals(peer,sender)||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthLogisticsHistoricalZeroAck>().Setup(operation,nonce,digest),_attachedToEntityId:sender.entityId);
    }
    internal static void Acknowledge(World world,Guid operation,string nonce,string digest)
    {
        if(!ThreadManager.IsMainThread())return;var player=world?.GetPrimaryPlayer();if(world?.IsRemote()!=true||!ReferenceEquals(sendWorld,world)||operation!=sendOperation||sendBody?.Hash!=digest||!RebirthLogisticsOwnerReceipt.TryLocalHeader(player,out var actual)||actual!=nonce)return;
        if(!RebirthLogisticsOriginalTransport.TryOwnerConnection(out _,out var connection))return;
        acknowledgedConnection=connection;acknowledgedHash=digest;sends.Clear(); // Transport only, never releases native hold.
    }
    private static void RecoverHostCandidate(RebirthLogisticsNativeBodyIntent.Command c,string nonce)
    {
        string path=HostPath(c.Owner,c.Operation);if(File.Exists(path)&&File.Exists(path+".xml")&&!File.Exists(path+".tmp")&&!File.Exists(path+".xml.tmp"))return;if(File.Exists(path+".bak")||File.Exists(path+".xml.bak")||File.Exists(path)&&File.Exists(path+".tmp")||File.Exists(path+".xml")&&File.Exists(path+".xml.tmp"))return;
        string body=File.Exists(path)?path:path+".tmp",metadata=File.Exists(path+".xml")?path+".xml":path+".xml.tmp";
        if(!File.Exists(body)||!File.Exists(metadata)||(File.GetAttributes(body)&FileAttributes.ReparsePoint)!=0||new FileInfo(body).Length>MaximumWire)return;
        var m=Metadata(metadata);if(m.Name!="ownerHistoricalZero"||m.Attributes().Count()!=4||m.Attributes().Any(a=>a.Name!="version"&&a.Name!="hash"&&a.Name!="header"&&a.Name!="binding")||m.HasElements||(string)m.Attribute("version")!="1"||(string)m.Attribute("header")!=nonce||(string)m.Attribute("binding")!=c.Binding)return;
        byte[] bytes=File.ReadAllBytes(body);if(BytesHash(bytes)!=(string)m.Attribute("hash"))return;var saved=DecodeProjection(bytes);
        if(saved.deaths!=c.Deaths||!RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(saved,out var op,out int phase,out float held)||op!=c.Operation||phase!=0||held!=1f||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var header)||header!=nonce||RebirthGearPlayerDataInventory.ReadSlots(saved,true)==null||RebirthGearPlayerDataInventory.ReadSlots(saved,false)==null)return;
        if(body!=path)File.Move(body,path);if(metadata!=path+".xml")File.Move(metadata,path+".xml");
    }
    internal static void Receive(ClientInfo sender,Guid operation,string nonce,string digest,int total,int index,int count,string text)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||!(world.GetEntity(sender.entityId) is EntityPlayer actor)||!RebirthLogisticsHeaderAdmission.TryHistoricalHeader(actor,operation,nonce,out var c,out _)||total<1||total>MaximumWire)return;
            string key="logistics-historical-zero|"+c.World+"|"+c.Owner+"|"+operation.ToString("N")+"|"+nonce;
            RecoverHostCandidate(c,nonce);string path=HostPath(c.Owner,operation);if(File.Exists(path)){if(index!=0)return;var old=HostMetadata(path);if((string)old.Attribute("hash")!=digest||(string)old.Attribute("header")!=nonce||(string)old.Attribute("binding")!=c.Binding||new FileInfo(path).Length!=total||BytesHash(ReadHostBody(path))!=digest)return;SendAck(sender,operation,nonce,digest);TryAdopt(actor,operation,nonce);return;}
            if(File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(path+".xml")||File.Exists(path+".xml.tmp"))return;
            if(assemblyKey!=null&&assemblyKey!=key&&DateTime.UtcNow<assemblyUntil)return;
            if((assemblyKey!=key||DateTime.UtcNow>=assemblyUntil)&&!HostBudget(total))return; // BEFORE fragment allocation.
            assemblyKey=key;assemblyUntil=DateTime.UtcNow.AddSeconds(30);
            if(!RebirthRemoteResourceFragments.TryAdd(key,operation,digest,total,count,index,text,DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond,out var bytes)||bytes==null)return;assemblyKey=null;
            var saved=DecodeProjection(bytes);if(saved.id!=actor.entityId||saved.deaths!=c.Deaths||!RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(saved,out var op,out int phase,out float held)||op!=operation||phase!=0||held!=1f||!RebirthLogisticsOwnerReceipt.TryReadDetachedHeader(saved,out var accepted)||accepted!=nonce||RebirthGearPlayerDataInventory.ReadSlots(saved,true)==null||RebirthGearPlayerDataInventory.ReadSlots(saved,false)==null)return;
            if(!HostBudget(bytes.Length))return;Directory.CreateDirectory(Path.GetDirectoryName(path));using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            var meta=new XElement("ownerHistoricalZero",new XAttribute("version",1),new XAttribute("hash",digest),new XAttribute("header",nonce),new XAttribute("binding",c.Binding));byte[] xml=Encoding.UTF8.GetBytes(meta.ToString(SaveOptions.DisableFormatting));using(var file=new FileStream(path+".xml.tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(xml,0,xml.Length);file.Flush(true);}File.Move(path+".tmp",path);File.Move(path+".xml.tmp",path+".xml");SendAck(sender,operation,nonce,digest);TryAdopt(actor,operation,nonce);
        }
        catch{/* No SavePlayerData/ToPlayer or current owner mutation. */}
    }

}

public sealed class NetPackageRebirthLogisticsHistoricalZero:NetPackage
{
    private int entity,total,index,count;private PlatformUserIdentifierAbs user;private Guid operation;private string nonce,digest,text;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthLogisticsHistoricalZero Setup(int actor,PlatformUserIdentifierAbs identity,Guid op,string header,string hash,int length,int part,int parts,string value)
    {entity=actor;user=identity;operation=op;nonce=header;digest=hash;total=length;index=part;count=parts;text=value;return this;}
    public override void read(PooledBinaryReader reader)
    {if(reader.ReadByte()!=1)throw new InvalidDataException();entity=reader.ReadInt32();user=PlatformUserIdentifierAbs.FromStream(reader);if(!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation)||operation==Guid.Empty)throw new InvalidDataException();nonce=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);total=reader.ReadInt32();index=reader.ReadInt32();count=reader.ReadInt32();text=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,12000);}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(entity);user.ToStream(writer);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,nonce,32);RebirthSurvivorNetworkCodec.WriteString(writer,digest,64);writer.Write(total);writer.Write(index);writer.Write(count);RebirthSurvivorNetworkCodec.WriteString(writer,text,12000);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,true)&&ValidEntityIdForSender(entity)&&ValidUserIdForSender(user))RebirthLogisticsHistoricalBeforeCapsule.Receive(Sender,operation,nonce,digest,total,index,count,text);}
    public int GetLength()=>13200;
}

public sealed class NetPackageRebirthLogisticsHistoricalZeroAck:NetPackage
{
    private Guid operation;private string nonce,digest;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthLogisticsHistoricalZeroAck Setup(Guid op,string header,string hash){operation=op;nonce=header;digest=hash;return this;}
    public override void read(PooledBinaryReader reader){if(reader.ReadByte()!=1||!Guid.TryParseExact(RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32),"N",out operation))throw new InvalidDataException();nonce=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,32);digest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write((byte)1);RebirthSurvivorNetworkCodec.WriteString(writer,operation.ToString("N"),32);RebirthSurvivorNetworkCodec.WriteString(writer,nonce,32);RebirthSurvivorNetworkCodec.WriteString(writer,digest,64);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(RebirthLogisticsOriginalTransport.AcceptPacket(this,world,callbacks,false))RebirthLogisticsHistoricalBeforeCapsule.Acknowledge(world,operation,nonce,digest);}
    public int GetLength()=>180;
}
