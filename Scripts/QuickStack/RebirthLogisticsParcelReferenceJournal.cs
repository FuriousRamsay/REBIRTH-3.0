using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Separate pointer-only history. Frozen DeathJournal plan/Spawn/payment proofs
// remain byte-for-byte unchanged. Reference-only continuation cannot reopen
// original item settlement or create replacement bodies.
internal static class RebirthLogisticsParcelReferenceJournal
{
    private static readonly bool Enabled=true;
    private const int MaximumBytes=32768;
    private sealed class Endpoint
    {
        internal RebirthLogisticsTransferJournal.Record Original;internal string Binding;internal RebirthLogisticsDeathJournal.Record Death;internal RebirthLogisticsDeathJournal.Parcel Parcel;
        internal Guid Incarnation=>Parcel?.Incarnation??Death.Incarnation;
        internal int Entity=>Parcel?.EntityId??Death.EntityId;internal int Class=>Parcel?.EntityClass??Death.EntityClass;
        internal int X=>Parcel?.X??Death.X;internal int Z=>Parcel?.Z??Death.Z;
        internal string Key=>"RebirthLogisticsReferenceV1:"+Entity+":"+Original.Operation.ToString("N");
        internal string OriginalKey=>Parcel==null?RebirthLogisticsDeathJournal.MarkerKey(Death):RebirthLogisticsDeathJournal.MarkerKey(Death,Parcel);
        internal byte[] Before=>Parcel==null?RebirthLogisticsDeathJournal.Marker(Death,false):RebirthLogisticsDeathJournal.Marker(Death,Parcel,false);
        internal byte[] After=>Parcel==null?(Death.State==1?Before:RebirthLogisticsDeathJournal.Marker(Death,true)):RebirthLogisticsDeathJournal.Marker(Death,Parcel,true);
        internal bool[] Locks=>Parcel?.Locks??Death.Locks;
        internal string Identity=>RebirthLogisticsOriginalTransport.Digest(Original.Owner+"|"+Original.Operation.ToString("N")+"|"+Original.World+"|"+Original.Creation+"|"+Original.Deaths+"|"+Death.OwnerEntity+"|"+Incarnation.ToString("N")+"|"+Entity+"|"+Class+"|"+X+"|"+Z+"|"+Hash(Before)+"|"+Hash(After)+"|"+Death.OwnerFileProof);
    }
    private sealed class TrackingRow
    {
        internal PlatformUserIdentifierAbs Key;internal PersistentPlayerData Row;internal object Primary;internal int Entity;internal bool Null;
        internal readonly Dictionary<int,PersistentPlayerData.ProtectedBackpack> Cells=new Dictionary<int,PersistentPlayerData.ProtectedBackpack>();
    }
    private sealed class TrackingObservation
    {
        internal RebirthLogisticsBodyTracking.Entry Tracked;internal PersistentPlayerList List;internal int Body;internal string Owner,Witness;
        internal readonly List<TrackingRow> Rows=new List<TrackingRow>();
        internal bool Untracked=>Tracked==null;
        internal uint Timestamp=>Tracked?.Timestamp??0;
        internal Vector3i Position=>Tracked?.Position??Vector3i.zero;
    }
    private static TrackingObservation ObserveTracking(EntityPlayer actor,ClientInfo sender,string owner,EntityBackpack body)
    {
        if(RebirthLogisticsBodyTracking.TryObserve(actor,sender,owner,body,out var tracked))
        {var trackedObservation=new TrackingObservation{Tracked=tracked,Body=body.entityId,Owner=owner};trackedObservation.Witness=TrackingWitness(trackedObservation);return trackedObservation;}
        if(!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,owner,out _))return null;
        var list=GameManager.Instance.GetPersistentPlayerList();if(list?.Players==null||list.Players.Count>4096)return null;
        var observation=new TrackingObservation{List=list,Body=body.entityId,Owner=owner};
        foreach(var pair in list.Players)
        {
            if(pair.Key?.CombinedString==null||pair.Key.CombinedString.Length>512||pair.Value==null||pair.Value.PrimaryId?.CombinedString?.Length>512||pair.Value.backpacksByID?.Count>3)return null;
            var row=new TrackingRow{Key=pair.Key,Row=pair.Value,Primary=pair.Value.PrimaryId,Entity=pair.Value.EntityId,Null=pair.Value.backpacksByID==null};
            if(!row.Null)foreach(var cell in pair.Value.backpacksByID)
            {
                if(cell.Key==body.entityId||cell.Value.EntityID==body.entityId||cell.Key!=cell.Value.EntityID)return null;
                row.Cells.Add(cell.Key,cell.Value);
            }
            observation.Rows.Add(row);
        }
        observation.Witness=TrackingWitness(observation);return observation;
    }
    internal static bool TryUntrackedNoticeWitness(EntityPlayer actor,ClientInfo sender,EntityBackpack body,out string witness)
    {
        witness=null;var observation=ObserveTracking(actor,sender,RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)?owner.StorageKey:null,body);
        if(observation==null||!observation.Untracked||!TrackingUnchanged(observation,actor,sender,body))return false;witness=observation.Witness;return true;
    }
    private static string TrackingWitness(TrackingObservation observation)
    {
        using(var hash=System.Security.Cryptography.SHA256.Create())
        using(var crypto=new System.Security.Cryptography.CryptoStream(Stream.Null,hash,System.Security.Cryptography.CryptoStreamMode.Write))
        using(var writer=new BinaryWriter(crypto,Encoding.UTF8,true))
        {
            writer.Write(observation.Owner);writer.Write(observation.Body);writer.Write(observation.Untracked);
            if(!observation.Untracked){writer.Write(observation.Timestamp);writer.Write(observation.Position.x);writer.Write(observation.Position.y);writer.Write(observation.Position.z);}
            else foreach(var row in observation.Rows.OrderBy(value=>value.Key.CombinedString,StringComparer.Ordinal))
            {
                writer.Write(row.Key.CombinedString);writer.Write((row.Primary as PlatformUserIdentifierAbs)?.CombinedString??string.Empty);writer.Write(row.Entity);writer.Write(row.Null);writer.Write(row.Cells.Count);
                foreach(var cell in row.Cells.OrderBy(value=>value.Key)){writer.Write(cell.Key);writer.Write(cell.Value.EntityID);writer.Write(cell.Value.Position.x);writer.Write(cell.Value.Position.y);writer.Write(cell.Value.Position.z);writer.Write(cell.Value.Timestamp);}
            }
            writer.Flush();crypto.FlushFinalBlock();return BitConverter.ToString(hash.Hash).Replace("-",string.Empty).ToLowerInvariant();
        }
    }
    private static bool TrackingUnchanged(TrackingObservation observation,EntityPlayer actor,ClientInfo sender,EntityBackpack body)
    {
        if(observation==null||observation.Body!=body.entityId)return false;
        if(!observation.Untracked)return RebirthLogisticsBodyTracking.IsUnchanged(observation.Tracked,actor,sender,body);
        if(!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,observation.Owner,out _)||!ReferenceEquals(GameManager.Instance.GetPersistentPlayerList(),observation.List)||observation.List.Players.Count!=observation.Rows.Count)return false;
        foreach(var expected in observation.Rows)
        {
            if(!observation.List.Players.TryGetValue(expected.Key,out var row)||!ReferenceEquals(row,expected.Row)||!ReferenceEquals(row.PrimaryId,expected.Primary)||row.EntityId!=expected.Entity||
                (row.backpacksByID==null)!=expected.Null||!expected.Null&&row.backpacksByID.Count!=expected.Cells.Count)return false;
            foreach(var cell in expected.Cells)
                if(!row.backpacksByID.TryGetValue(cell.Key,out var value)||value.EntityID!=cell.Value.EntityID||value.Timestamp!=cell.Value.Timestamp||!value.Position.Equals(cell.Value.Position))return false;
        }
        return true;
    }
    private sealed class Job:IDisposable
    {
        internal Endpoint Endpoint;internal EntityBackpack Body;internal Chunk Chunk;internal EntityPlayer Actor;internal ClientInfo Sender;
        internal XElement History;internal byte[] Image,OriginalMarker;internal TrackingObservation Tracking;
        internal RebirthLogisticsBackpackPublication Publication;internal RebirthLogisticsParcelBaselines Baselines;internal bool CapacityApplied;internal int CapacityTarget;internal RebirthLogisticsSnapshotReservation Account;internal IDisposable Allocation;
        internal DateTime Expires;
        public void Dispose(){Publication?.Dispose();Publication=null;Baselines?.Dispose();Baselines=null;Image=null;OriginalMarker=null;Allocation?.Dispose();Allocation=null;Account?.Dispose();Account=null;}
    }
    private static readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>(StringComparer.Ordinal);
    private static World world;
    private static string Hash(byte[] bytes)=>RebirthLogisticsOriginalTransport.Digest(Convert.ToBase64String(bytes));
    private static string Id(Endpoint endpoint)=>endpoint.Original.Owner+":"+endpoint.Original.Operation.ToString("N")+":"+endpoint.Incarnation.ToString("N");
    private static string DomainPath(string owner,Guid operation,Guid incarnation,bool archived)
    {
        if(!Hex(owner)||operation==Guid.Empty||incarnation==Guid.Empty)throw new InvalidDataException();
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsParcelReferences"),directory=archived?Path.Combine(root,"archive",owner,operation.ToString("N")):Path.Combine(root,owner,operation.ToString("N"));
        string current=directory;while(current!=Path.GetDirectoryName(root))
        {if(Directory.Exists(current)&&(new DirectoryInfo(current).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();if(current==root)break;current=Path.GetDirectoryName(current);}
        return Path.Combine(directory,incarnation.ToString("N")+".xml");
    }
    private static string PathFor(Endpoint endpoint)=>DomainPath(endpoint.Original.Owner,endpoint.Original.Operation,endpoint.Incarnation,false);
    private static XElement Empty(Endpoint endpoint)=>new XElement("parcelReferences",new XAttribute("version",1),new XAttribute("owner",endpoint.Original.Owner),new XAttribute("operation",endpoint.Original.Operation.ToString("N")),new XAttribute("incarnation",endpoint.Incarnation.ToString("N")),new XAttribute("identity",endpoint.Identity),new XAttribute("binding",endpoint.Binding));
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static XElement ReadFile(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
        if(root.Name!="parcelReferences"||root.Attributes().Count()!=6||(string)root.Attribute("version")!="1"||root.Elements().Count()>16||!Hex((string)root.Attribute("owner"))||!Hex((string)root.Attribute("identity"))||!Hex((string)root.Attribute("binding")))throw new InvalidDataException();
        if(Guid.ParseExact((string)root.Attribute("operation"),"N")==Guid.Empty||Guid.ParseExact((string)root.Attribute("incarnation"),"N")==Guid.Empty||root.Nodes().Any(node=>!(node is XElement)))throw new InvalidDataException();
        var nonces=new HashSet<string>(StringComparer.Ordinal);int? expected=null;int index=0;
        foreach(var row in root.Elements())
        {
            int state=(int)row.Attribute("state"),old=(int)row.Attribute("old"),next=(int)row.Attribute("next");string nonce=(string)row.Attribute("nonce");bool last=++index==root.Elements().Count();
            if(row.Name!="repair"||row.HasElements||row.Value!=string.Empty||row.Attributes().Count()!=15||state<0||state>5||!last&&state!=0&&state!=3&&state!=5||old<0||next<0||old==next||expected.HasValue&&old!=expected.Value||
                !Guid.TryParseExact(nonce,"N",out var guid)||guid==Guid.Empty||nonce!=guid.ToString("N")||!nonces.Add(nonce)||!Hex((string)row.Attribute("before"))||!Hex((string)row.Attribute("image"))||!Hex((string)row.Attribute("marker"))||
                (state==3||state==5?!Hex((string)row.Attribute("after")):row.Attribute("after").Value!=string.Empty))throw new InvalidDataException();
            string[] fields={"state","old","next","nonce","before","after","image","marker","timestamp","x","y","z","reserved","tracking","witness"};
            if(row.Attributes().Any(a=>a.Name.Namespace!=XNamespace.None||!fields.Contains(a.Name.LocalName))||(int)row.Attribute("reserved")!=0||!Hex((string)row.Attribute("witness")))throw new InvalidDataException();
            _=(uint)row.Attribute("timestamp");_=(int)row.Attribute("x");_=(int)row.Attribute("y");_=(int)row.Attribute("z");
            string tracking=(string)row.Attribute("tracking");if(tracking!="tracked"&&tracking!="untracked"||tracking=="untracked"&&((uint)row.Attribute("timestamp")!=0||(int)row.Attribute("x")!=0||(int)row.Attribute("y")!=0||(int)row.Attribute("z")!=0))throw new InvalidDataException();
            expected=state==2||state==3?next:old;
        }
        return root;
    }
    private static string ArchivePath(Endpoint endpoint)=>DomainPath(endpoint.Original.Owner,endpoint.Original.Operation,endpoint.Incarnation,true);
    private static XElement Read(Endpoint endpoint)
    {
        string path=PathFor(endpoint),archive=ArchivePath(endpoint);
        if(File.Exists(archive)){if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))throw new InvalidDataException();path=archive;}
        if(File.Exists(path+".bak"))throw new InvalidDataException();
        if(!File.Exists(path)&&!File.Exists(path+".tmp"))return Empty(endpoint);
        var root=File.Exists(path)?ReadFile(path):Empty(endpoint);var normalized=new XElement(root);normalized.RemoveNodes();if(!XNode.DeepEquals(normalized,Empty(endpoint)))throw new InvalidDataException();
        if(File.Exists(path+".tmp"))
        {
            var next=ReadFile(path+".tmp");var compare=new XElement(next);bool valid=false;
            if(next.Elements().Count()==root.Elements().Count()+1&&(int)next.Elements().Last().Attribute("state")==1)
            {compare.Elements().Last().Remove();valid=XNode.DeepEquals(compare,root);}
            else if(next.Elements().Count()==root.Elements().Count()&&root.HasElements)
            {
                var before=root.Elements().Last();var after=compare.Elements().Last();int old=(int)before.Attribute("state"),state=(int)after.Attribute("state");
                if(old==1&&state==0||old==1&&state==2||old==2&&state==4||old==2&&state==3||old==4&&state==5)
                {after.SetAttributeValue("state",old);after.SetAttributeValue("after",before.Attribute("after").Value);valid=XNode.DeepEquals(compare,root);}
            }
            if(!valid)throw new InvalidDataException();if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);root=ReadFile(path);
        }
        if(root.HasElements&&(int)root.Elements().First().Attribute("old")!=endpoint.Death.OwnerEntity)throw new InvalidDataException();
        return root;
    }
    private static bool Capacity(int incoming)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsParcelReferences");if(!Directory.Exists(root))return true;int count=0;long bytes=incoming;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            if((new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)return false;if(Path.GetFileName(directory)=="archive")continue;
            if(!Hex(Path.GetFileName(directory)))return false;
            foreach(string operationDirectory in Directory.EnumerateDirectories(directory))
            {
            if((new DirectoryInfo(operationDirectory).Attributes&FileAttributes.ReparsePoint)!=0||!Guid.TryParseExact(Path.GetFileName(operationDirectory),"N",out var directoryOperation)||directoryOperation==Guid.Empty||Directory.EnumerateDirectories(operationDirectory).Any())return false;
            foreach(string path in Directory.EnumerateFiles(operationDirectory,"*",SearchOption.TopDirectoryOnly))
            {
                if(!path.EndsWith(".xml",StringComparison.Ordinal))return false;var record=ReadFile(path);string owner=(string)record.Attribute("owner");Guid operation=Guid.ParseExact((string)record.Attribute("operation"),"N");
                if(Path.GetFileName(directory)!=owner||Path.GetFileName(Path.GetDirectoryName(path))!=operation.ToString("N")||Path.GetFileNameWithoutExtension(path)!=(string)record.Attribute("incarnation"))return false;
                if(RebirthLogisticsTransferJournal.TryRead(owner,operation,out var terminal)&&terminal.Phase==4&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(terminal)==(string)record.Attribute("binding")&&record.HasElements&&((int)record.Elements().Last().Attribute("state")==3||(int)record.Elements().Last().Attribute("state")==5))
                {
                    string archive=DomainPath(owner,operation,Guid.ParseExact((string)record.Attribute("incarnation"),"N"),true);if(File.Exists(archive))return false;
                    Directory.CreateDirectory(Path.GetDirectoryName(archive));File.Move(path,archive);continue;
                }
                if(++count>=4096)return false;bytes+=new FileInfo(path).Length;if(bytes>33554432)return false;
            }
            }
            if(Directory.EnumerateFiles(directory).Any())return false;
        }
        return true;
    }
    private static bool Publish(Endpoint endpoint,XElement before,XElement next)
    {
        var normalized=new XElement(next);bool transition=false;
        if(next.Elements().Count()==before.Elements().Count()+1&&(int)next.Elements().Last().Attribute("state")==1)
        {normalized.Elements().Last().Remove();transition=XNode.DeepEquals(normalized,before);}
        else if(next.Elements().Count()==before.Elements().Count()&&before.HasElements)
        {
            var previous=before.Elements().Last();var row=normalized.Elements().Last();int old=(int)previous.Attribute("state"),state=(int)row.Attribute("state");
            if(old==1&&state==0||old==1&&state==2||old==2&&state==4||old==2&&state==3||old==4&&state==5)
            {row.SetAttributeValue("state",old);row.SetAttributeValue("after",previous.Attribute("after").Value);transition=XNode.DeepEquals(normalized,before);}
        }
        if(!transition)return false;
        if(!XNode.DeepEquals(Read(endpoint),before)||!Capacity(Encoding.UTF8.GetByteCount(next.ToString(SaveOptions.DisableFormatting))))return false;
        string path=PathFor(endpoint);byte[] bytes=Encoding.UTF8.GetBytes(next.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumBytes)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path));using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return XNode.DeepEquals(Read(endpoint),next);
    }
    private static int Owner(Endpoint endpoint,XElement history)
    {int owner=endpoint.Death.OwnerEntity;foreach(var row in history.Elements())owner=(int)row.Attribute((int)row.Attribute("state")==2||(int)row.Attribute("state")==3?"next":"old");return owner;}
    private static byte[] Marker(Endpoint endpoint,XElement history)=>StoredMarker(history);
    private static byte[] StoredMarker(XElement history)
    {
        var normalized=new XElement(history);normalized.Elements().Where(row=>(int)row.Attribute("state")<2).Remove();foreach(var row in normalized.Elements()){int state=(int)row.Attribute("state");row.SetAttributeValue("state",state==3?2:state==5?4:state);row.SetAttributeValue("after",string.Empty);}
        string value=(string)history.Attribute("owner")+"|"+(string)history.Attribute("operation")+"|"+(string)history.Attribute("incarnation")+"|"+(string)history.Attribute("identity")+"|"+RebirthLogisticsOriginalTransport.Digest(normalized.ToString(SaveOptions.DisableFormatting));
        return Encoding.UTF8.GetBytes(value);
    }
    private static bool MarkerCurrent(Endpoint endpoint,Chunk chunk,XElement history)
    {
        if(!history.Elements().Any(row=>(int)row.Attribute("state")>=2))return !chunk.ChunkCustomData.dict.ContainsKey(endpoint.Key);
        return chunk.ChunkCustomData.dict.TryGetValue(endpoint.Key,out var marker)&&marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&marker.data.SequenceEqual(Marker(endpoint,history));
    }
    private static byte[] Bag(EntityBackpack body)
    {using(var blob=StreamUtils.ToBlob(writer=>body.bag.Write(writer,StreamModeWrite.ToClient)))return blob.ToArray();}
    private sealed class CountStream:Stream
    {
        private long count;public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] bytes,int offset,int length){if(bytes==null||offset<0||length<0||offset>bytes.Length-length||count>4194304L-length)throw new InvalidDataException();count+=length;}
        public override void WriteByte(byte value){if(++count>4194304)throw new InvalidDataException();}public override void Flush(){}public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    private static long SnapshotBytes(ItemStack[] slots)
    {
        if(slots==null||slots.Length<1||slots.Length>256)throw new InvalidDataException();long count=8;
        foreach(var stack in slots)
        {
            if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException();
            using(var stream=new CountStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {var encoding=writer.Encoding;try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);stack.itemValue.Write(writer);writer.Flush();count=checked(count+8+stream.Length);}finally{writer.Encoding=encoding;}}
            if(count>3145728)throw new InvalidDataException();
        }
        return count;
    }
    private static bool Reserve(Endpoint endpoint,out RebirthLogisticsSnapshotReservation account,out IDisposable allocation)
    {
        account=null;allocation=null;
        try
        {
            long before=SnapshotBytes(endpoint.Parcel?.Before??endpoint.Death.Before),after=SnapshotBytes(endpoint.Parcel?.After??endpoint.Death.After??endpoint.Death.Before);
            int rows=GameManager.Instance?.GetPersistentPlayerList()?.Players?.Count??-1;if(rows<0||rows>4096)return false;
            var captured=GameManager.Instance?.World;account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(endpoint.Original,()=>ReferenceEquals(GameManager.Instance?.World,captured)&&captured?.IsRemote()==false);
            if(account==null||!account.TryReserveAllocation(checked(1048576+12*(before+after)+4L*MaximumBytes+512L*rows),out allocation)){account?.Dispose();account=null;return false;}return true;
        }catch{allocation?.Dispose();account?.Dispose();allocation=null;account=null;return false;}
    }
    // State1 reference publication is solely metadata. No synthetic After,
    // owner debit, source payment or material-delivery proof is created here.
    private static bool IsNativeBeforeBody(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death)
    {
        return original!=null&&death!=null&&death.State==1&&death.After==null&&death.BeforeProof?.Length==64&&death.Parcels.Count==0&&
            RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)&&saved.Phase<=4&&!saved.OwnerEscrowRecovery&&(saved.Outcome==0||saved.Outcome==3)&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved)==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&
            saved.Legs.All(value=>value.Attempted==0&&value.Published==0&&!value.Restored)&&saved.Npcs.All(value=>value.Attempted==0&&value.Published==0);
    }
    internal static bool IsNativeBeforeOnly(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death)
    {
        return original!=null&&death!=null&&death.State==1&&death.After==null&&death.BeforeProof?.Length==64&&death.Parcels.Count==0&&
            RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)&&saved.Outcome==3&&!saved.OwnerEscrowRecovery&&saved.OwnerNoEffectProof?.Length==64&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved)==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&
            saved.Legs.All(value=>value.Attempted==0&&value.Published==0&&!value.Restored)&&saved.Npcs.All(value=>value.Attempted==0&&value.Published==0);
    }
    // Item terminal evidence is immutable and independent of live client nav.
    // Callers must still rederive their original saved owner NoEffect proof;
    // this does not authorize cleanup, After synthesis or a new item effect.
    internal static bool ReadyForNoEffectItemTerminal(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(original==null||original.Outcome!=3||original.OwnerEscrowRecovery||original.OwnerNoEffectProof?.Length!=64||
                original.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||original.Npcs.Any(leg=>leg.Attempted!=0||leg.Published!=0))return false;
            if(!RebirthLogisticsDeathJournal.TryRead(original,out var death))return !RebirthLogisticsDeathJournal.HasAnyCandidate(original);
            if(death.State!=1)return death.State>=2;
            return IsNativeBeforeOnly(original,death)&&death.OwnerFileProof?.Length==64;
        }
        catch{return false;}
    }
    internal static bool ReadyForNoEffect(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(!RebirthLogisticsDeathJournal.TryRead(original,out var death))return !RebirthLogisticsDeathJournal.HasAnyCandidate(original);
            if(death.State!=1)return death.State>=2;
            if(!IsNativeBeforeOnly(original,death))return false;
            var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death};
            var body=GameManager.Instance?.World?.GetEntity(death.EntityId) as EntityBackpack;
            // Absence is deliberately not a removal receipt. An unloaded,
            // failed-spawn or retired marker cannot authorize retirement.
            if(body==null)return HasPositiveRemoval(endpoint);
            if(!Actor(endpoint,out var actor,out _)||!Reserve(endpoint,out var account,out var allocation))return false;
            using(account)using(allocation)
                if(!Native(endpoint,body,body.world.GetChunkSync(endpoint.X,endpoint.Z) as Chunk)||!RebirthLogisticsDeathPartition.Same(body.bag.ItemGrid.items,death.Before))return false;
            if(!Enabled)return false;
            if(!RebirthLogisticsLoadedBodyNotice.CanRetire(original,death.Incarnation,body))return false;
            return TryEffective(original,death,null,body,out var effective)&&effective==actor.entityId;
        }catch{return false;}
    }
    private static bool Native(Endpoint endpoint,EntityBackpack body,Chunk chunk)
    {
        if(body==null||chunk==null||chunk.InProgressUnloading||!ReferenceEquals(body.world,GameManager.Instance?.World)||endpoint.Original.World!=RebirthLogisticsTransferJournal.WorldKey(body.world)||!ReferenceEquals(GameManager.Instance?.World?.GetEntity(endpoint.Entity),body)||body.entityClass!=endpoint.Class||!body.addedToChunk||
            body.chunkPosAddedEntityTo.x!=endpoint.X||body.chunkPosAddedEntityTo.z!=endpoint.Z||!ReferenceEquals(body.world.GetChunkSync(endpoint.X,endpoint.Z),chunk)||body.bag?.ItemGrid?.Length!=endpoint.Locks.Length||
            Enumerable.Range(0,endpoint.Locks.Length).Any(i=>body.bag.ItemGrid.IsLocked(i)!=endpoint.Locks[i]))return false;
        return chunk.ChunkCustomData.dict.TryGetValue(endpoint.OriginalKey,out var marker)&&marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&
            (marker.data.SequenceEqual(endpoint.Before)&&RebirthLogisticsDeathPartition.Same(body.bag.ItemGrid.items,endpoint.Parcel?.Before??endpoint.Death.Before)||
             marker.data.SequenceEqual(endpoint.After)&&RebirthLogisticsDeathPartition.Same(body.bag.ItemGrid.items,endpoint.Parcel?.After??endpoint.Death.After??endpoint.Death.Before));
    }
    internal static bool HasPublishedNoticeIdentity(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice notice,EntityBackpack body)
    {
        try
        {
            if(!RebirthLogisticsDeathJournal.TryRead(original,out var death))return false;
            var parcel=death.Parcels.SingleOrDefault(value=>value.Incarnation==notice.Incarnation&&value.EntityId==body.entityId);
            if((death.Incarnation!=notice.Incarnation||death.EntityId!=body.entityId)&&parcel==null)return false;
            var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel};if(!Reserve(endpoint,out var account,out var allocation))return false;
            using(account)using(allocation)
            {
                var chunk=body.world.GetChunkSync(endpoint.X,endpoint.Z) as Chunk;if(!Native(endpoint,body,chunk))return false;
                var history=Read(endpoint);if(!MarkerCurrent(endpoint,chunk,history))return false;var row=history.Elements().LastOrDefault();
                if(row==null)return notice.Old==notice.Next&&notice.Nonce==endpoint.Incarnation;
                int state=(int)row.Attribute("state");return (string)row.Attribute("nonce")==notice.Nonce.ToString("N")&&
                    (state==3&&(int)row.Attribute("old")==notice.Old&&(int)row.Attribute("next")==notice.Next||
                     (state==0||state==5)&&notice.Old==notice.Next&&body.RefPlayerId==notice.Next);
            }
        }catch{return false;}
    }
    internal static bool TryEffective(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death,RebirthLogisticsDeathJournal.Parcel parcel,EntityBackpack body,out int owner)
    {
        owner=death.OwnerEntity;if(!Enabled)return true;
        try
        {
            var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel};if(jobs.ContainsKey(Id(endpoint)))return false;
            if(death.State<2&&!IsNativeBeforeOnly(original,death)||parcel!=null&&parcel.State<2)return body?.RefPlayerId==death.OwnerEntity&&!File.Exists(PathFor(endpoint))&&!File.Exists(PathFor(endpoint)+".tmp");
            if(!Reserve(endpoint,out var account,out var allocation))return false;
            using(account)using(allocation)
            {
            var history=Read(endpoint);EntityPlayer current=null;
            // Terminal read supports exact metadata retirement only. Producer
            // Tick/BeginReference reject phase4; no CAS/item permission follows.
            if(original.Phase<4&&!Actor(endpoint,out current,out _))return false;var last=history.Elements().LastOrDefault();
            if(last!=null&&(int)last.Attribute("state")!=0&&(int)last.Attribute("state")!=3&&(int)last.Attribute("state")!=5)return false;
            owner=Owner(endpoint,history);var chunk=body?.world?.GetChunkSync(endpoint.X,endpoint.Z) as Chunk;
            return (original.Phase==4||owner==current.entityId)&&Native(endpoint,body,chunk)&&body.RefPlayerId==owner&&MarkerCurrent(endpoint,chunk,history);
            }
        }catch{return false;}
    }
    private static bool Actor(Endpoint endpoint,out EntityPlayer actor,out ClientInfo sender)
    {
        actor=null;sender=null;var world=GameManager.Instance?.World;if(world?.IsRemote()!=false)return false;
        var rows=GameManager.Instance.GetPersistentPlayerList()?.Players.Values.Where(row=>RebirthStablePlayerIdentity.TryFromPersistentPlayer(row,out var identity)&&identity.StorageKey==endpoint.Original.Owner).Take(2).ToArray();
        if(rows?.Length!=1)return false;sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(rows[0].EntityId);actor=world.GetEntity(rows[0].EntityId) as EntityPlayer;
        return RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,endpoint.Original.Owner,out _)&&RebirthCamperCharacterAccess.CurrentCreation(actor)==endpoint.Original.Creation&&actor.Died>=endpoint.Original.Deaths+1;
    }
    private static RebirthLogisticsDeathCustody.Accepted Accepted(Job job)=>new RebirthLogisticsDeathCustody.Accepted{World=world,Backpack=job.Body,Owner=job.Endpoint.Original.Owner,Creation=job.Endpoint.Original.Creation,
        Operation=job.Endpoint.Original.Operation,OriginalDeaths=job.Endpoint.Original.Deaths,OriginalOwnerEntity=job.Endpoint.Death.OwnerEntity,LiveOwnerEntity=job.Body.RefPlayerId,EntityId=job.Endpoint.Entity,EntityClass=job.Endpoint.Class};
    private static bool HistoryNative(Job job)
    {
        var row=job.History.Elements().LastOrDefault();int state=row==null?0:(int)row.Attribute("state");
        if(state==2||state==4)
        {
            int old=(int)row.Attribute("old"),next=(int)row.Attribute("next");if(job.Body.RefPlayerId!=old&&(state==4||job.Body.RefPlayerId!=next))return false;
            var previous=new XElement(job.History);previous.Elements().Last().Remove();return MarkerCurrent(job.Endpoint,job.Chunk,previous)||MarkerCurrent(job.Endpoint,job.Chunk,job.History);
        }
        if(job.Body.RefPlayerId!=Owner(job.Endpoint,job.History))return false;
        if(state==1){var previous=new XElement(job.History);previous.Elements().Last().Remove();return MarkerCurrent(job.Endpoint,job.Chunk,previous);}
        return MarkerCurrent(job.Endpoint,job.Chunk,job.History);
    }
    internal static bool CanObserveTerminalEndpoint(RebirthLogisticsTransferJournal.Record original,Guid incarnation,int entity)
    {
        try
        {
            if(original?.Phase!=4||!RebirthLogisticsDeathJournal.TryRead(original,out var death))return false;
            var parcel=death.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation&&value.EntityId==entity);
            if((death.Incarnation!=incarnation||death.EntityId!=entity)&&parcel==null)return false;
            var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel};var body=GameManager.Instance?.World?.GetEntity(entity) as EntityBackpack;
            if(!TerminalMetadataOriginal(endpoint,original)||!Actor(endpoint,out _,out _)||!Reserve(endpoint,out var account,out var allocation))return false;
            using(account)using(allocation)return Native(endpoint,body,body?.world?.GetChunkSync(endpoint.X,endpoint.Z) as Chunk);
        }catch{return false;}
    }
    // Named terminal continuation: reference metadata only against the exact
    // retained positive native GUID/body. No item/source writer consumes it.
    private static bool TerminalMetadataOriginal(Endpoint endpoint,RebirthLogisticsTransferJournal.Record original)
        =>original?.Phase==4&&endpoint.Death.BeforeProof?.Length==64&&
          (endpoint.Death.State==1&&IsNativeBeforeOnly(original,endpoint.Death)||endpoint.Death.State>=2&&endpoint.Death.FinalProof?.Length==64)&&
          RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)==endpoint.Binding;
    private static bool Current(Job job)
    {
        return ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&job.Actor!=null&&job.Actor.Died>=job.Endpoint.Original.Deaths+1&&RebirthCamperCharacterAccess.CurrentCreation(job.Actor)==job.Endpoint.Original.Creation&&XNode.DeepEquals(Read(job.Endpoint),job.History)&&Native(job.Endpoint,job.Body,job.Chunk)&&HistoryNative(job)&&TrackingUnchanged(job.Tracking,job.Actor,job.Sender,job.Body)&&job.Expires>DateTime.UtcNow&&job.Image!=null&&Bag(job.Body).SequenceEqual(job.Image)&&
            job.Chunk.ChunkCustomData.dict[job.Endpoint.OriginalKey].data.SequenceEqual(job.OriginalMarker)&&RebirthLogisticsTransferJournal.TryRead(job.Endpoint.Original.Owner,job.Endpoint.Original.Operation,out var original)&&(original.Phase<4||TerminalMetadataOriginal(job.Endpoint,original))&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)==job.Endpoint.Binding;
    }
    private static bool Preflight(Job job,XElement proposed,int owner)
    {
        if(job.CapacityApplied&&job.CapacityTarget==owner&&job.Body.RefPlayerId==owner)return true;
        if(job.Publication!=null){job.Publication.Dispose();job.Publication=null;}
        if(job.Baselines!=null&&!job.Baselines.IsCurrent()){job.Baselines.Dispose();job.Baselines=null;}
        if(job.Baselines==null)job.Baselines=job.Endpoint.Original.Phase==4?
            RebirthLogisticsParcelBaselines.BeginTerminalReference(job.Endpoint.Original,job.Endpoint.Incarnation,job.Endpoint.Entity,()=>Current(job)):
            RebirthLogisticsParcelBaselines.BeginReference(job.Endpoint.Original,job.Endpoint.Incarnation,job.Endpoint.Entity,()=>Current(job));
        if(job.Baselines==null||!job.Baselines.TryAcquire(job.Endpoint.X,job.Endpoint.Z,out var baseline))return false;
        bool fits;
        // Detached parser/layout, native ECD clone and compressed candidate all
        // have bounded charged lifetime; release before confirmation copy.
        if(!job.Baselines.TryReserveWorkspace(checked(5L*baseline.LongLength+6L*1024*1024),out var workspace))return false;
        using(workspace)
        {
            byte[] previous=null;if(job.Chunk.ChunkCustomData.dict.TryGetValue(job.Endpoint.Key,out var marker))previous=RebirthLogisticsParcelPreview.MarkerFrame(job.Endpoint.Key,marker.data);
            fits=RebirthLogisticsParcelPreview.TryBuildReference(baseline,job.Endpoint.X,job.Endpoint.Z,job.Endpoint.Original.Operation,job.Endpoint.Entity,job.Endpoint.Class,job.Body.RefPlayerId,owner,
                RebirthLogisticsParcelPreview.MarkerFrame(job.Endpoint.OriginalKey,job.OriginalMarker),previous,RebirthLogisticsParcelPreview.MarkerFrame(job.Endpoint.Key,Marker(job.Endpoint,proposed)),out _);
        }
        if(!fits){job.Baselines.Invalidate(job.Endpoint.X,job.Endpoint.Z);return false;}
        // Native capture completes synchronously; compare full current chunk
        // before this same turn's metadata effect. Changed baseline replans.
        if(!job.Baselines.TryConfirm(job.Endpoint.X,job.Endpoint.Z))return false;
        job.Baselines.Dispose();job.Baselines=null;job.CapacityTarget=owner;return true;
    }
    private static void Step(Endpoint endpoint)
    {
        string id=Id(endpoint);if(!Actor(endpoint,out var actor,out var sender))return;
        if(!jobs.TryGetValue(id,out var job))
        {
            if(jobs.Count>=128||!Reserve(endpoint,out var account,out var allocation))return;
            try
            {
                var history=Read(endpoint);var body=world.GetEntity(endpoint.Entity) as EntityBackpack;var chunk=world.GetChunkSync(endpoint.X,endpoint.Z) as Chunk;
                if(!Native(endpoint,body,chunk))return;var last=history.Elements().LastOrDefault();int owner=Owner(endpoint,history);
                if(last==null||((int)last.Attribute("state")==0||(int)last.Attribute("state")==3||(int)last.Attribute("state")==5))
                {if(body.RefPlayerId!=owner||!MarkerCurrent(endpoint,chunk,history))return;}
                byte[] image=Bag(body);if(image.Length>131072)return;
                var tracking=ObserveTracking(actor,sender,endpoint.Original.Owner,body);if(tracking==null)return;
                job=new Job{Endpoint=endpoint,Actor=actor,Sender=sender,Body=body,Chunk=chunk,History=history,Image=image,OriginalMarker=(byte[])chunk.ChunkCustomData.dict[endpoint.OriginalKey].data.Clone(),Tracking=tracking,Account=account,Allocation=allocation,Expires=DateTime.UtcNow.AddSeconds(45)};
                jobs.Add(id,job);account=null;allocation=null;
            }finally{allocation?.Dispose();account?.Dispose();}
        }
        if(!Current(job)){job.Dispose();jobs.Remove(id);return;}
        job.Actor=actor;job.Sender=sender;var row=job.History.Elements().LastOrDefault();
        if(job.Body.RefPlayerId==actor.entityId&&(row==null||(int)row.Attribute("state")==0||(int)row.Attribute("state")==5))
        {
            if(job.Publication!=null&&!job.Publication.IsCurrent()){job.Publication.Dispose();job.Publication=null;}
            if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(job),()=>Current(job),endpoint.OriginalKey,row!=null&&job.History.Elements().Any(value=>(int)value.Attribute("state")>=2)?endpoint.Key:null);
            Guid nonce=row==null?endpoint.Incarnation:Guid.ParseExact((string)row.Attribute("nonce"),"N");
            if(job.Publication!=null&&RebirthLogisticsLoadedBodyNotice.QueuePublished(endpoint.Original,endpoint.Incarnation,nonce,job.Body,actor.entityId,actor.entityId,job.Publication,()=>Current(job)))
            {job.Dispose();jobs.Remove(id);}return;
        }
        // Completed cold rows reobserve publication, never replay pointer CAS.
        if(row!=null&&(int)row.Attribute("state")==3&&job.Body.RefPlayerId==actor.entityId)
        {
            if(job.Publication!=null&&!job.Publication.IsCurrent()){job.Publication.Dispose();job.Publication=null;}
            if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(job),()=>Current(job),endpoint.OriginalKey,endpoint.Key);
            if(job.Publication!=null&&RebirthLogisticsLoadedBodyNotice.QueuePublished(endpoint.Original,endpoint.Incarnation,Guid.ParseExact((string)row.Attribute("nonce"),"N"),job.Body,(int)row.Attribute("old"),(int)row.Attribute("next"),job.Publication,()=>Current(job)))
            {job.Dispose();jobs.Remove(id);}return;
        }
        if(row==null||((int)row.Attribute("state")==0||(int)row.Attribute("state")==3||(int)row.Attribute("state")==5))
        {
            if(job.Publication!=null&&!job.Publication.IsCurrent()){job.Publication.Dispose();job.Publication=null;}
            if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(job),()=>Current(job),endpoint.OriginalKey,job.History.Elements().Any(value=>(int)value.Attribute("state")>=2)?endpoint.Key:null);
            if(job.Publication==null||!job.Publication.Saved(out var before))return;
            if(!TrackingUnchanged(job.Tracking,actor,sender,job.Body))return;
            var prepared=new XElement(job.History);prepared.Add(new XElement("repair",new XAttribute("state",1),new XAttribute("old",job.Body.RefPlayerId),new XAttribute("next",actor.entityId),new XAttribute("nonce",Guid.NewGuid().ToString("N")),new XAttribute("before",before),new XAttribute("after",string.Empty),new XAttribute("image",Hash(job.Image)),new XAttribute("marker",Hash(job.OriginalMarker)),new XAttribute("timestamp",job.Tracking.Timestamp),new XAttribute("x",job.Tracking.Position.x),new XAttribute("y",job.Tracking.Position.y),new XAttribute("z",job.Tracking.Position.z),new XAttribute("reserved",0),new XAttribute("tracking",job.Tracking.Untracked?"untracked":"tracked"),new XAttribute("witness",job.Tracking.Witness)));
            if(prepared.Elements().Count()>16||!Publish(endpoint,job.History,prepared))return;job.History=prepared;row=prepared.Elements().Last();job.Publication.Dispose();job.Publication=null;
        }
        int state=(int)row.Attribute("state"),old=(int)row.Attribute("old"),next=(int)row.Attribute("next");
        if((string)row.Attribute("witness")!=job.Tracking.Witness||(string)row.Attribute("tracking")!=(job.Tracking.Untracked?"untracked":"tracked")||(string)row.Attribute("image")!=Hash(job.Image)||(string)row.Attribute("marker")!=Hash(job.OriginalMarker)||(uint)row.Attribute("timestamp")!=job.Tracking.Timestamp||!new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z")).Equals(job.Tracking.Position))return;
        bool capacityReady=false;
        if(state==1&&actor.entityId!=next)
        {
            if(job.Body.RefPlayerId!=old||!TrackingUnchanged(job.Tracking,actor,sender,job.Body))return;
            var rejected=new XElement(job.History);rejected.Elements().Last().SetAttributeValue("state",0);
            if(!Publish(endpoint,job.History,rejected))return;job.Dispose();jobs.Remove(id);return;
        }
        if(state==1)
        {
            if(job.Body.RefPlayerId!=old||actor.entityId!=next||!TrackingUnchanged(job.Tracking,actor,sender,job.Body))return;
            var attempted=new XElement(job.History);attempted.Elements().Last().SetAttributeValue("state",2);
            if(!Preflight(job,attempted,next)||!Publish(endpoint,job.History,attempted))return;
            capacityReady=true;job.History=attempted;row=attempted.Elements().Last();state=2;
        }
        if(state==2&&job.Body.RefPlayerId==old&&actor.entityId!=next)
        {var observed=new XElement(job.History);observed.Elements().Last().SetAttributeValue("state",4);if(!Publish(endpoint,job.History,observed))return;job.History=observed;row=observed.Elements().Last();state=4;}
        int target=state==4?old:next;
        if(!capacityReady&&!Preflight(job,job.History,target))return;
        if(!job.Chunk.ChunkCustomData.dict.ContainsKey(endpoint.Key)&&job.Chunk.ChunkCustomData.dict.Count>=4096)return;
        if(state==2&&job.Body.RefPlayerId==old)
        {if(actor.entityId!=next||!TrackingUnchanged(job.Tracking,actor,sender,job.Body))return;job.Body.RefPlayerId=next;}
        if(job.Body.RefPlayerId!=(state==4?old:next))return;
        byte[] markerBytes=Marker(endpoint,job.History);
        if(!job.Chunk.ChunkCustomData.dict.TryGetValue(endpoint.Key,out var marker))
        {if(job.Chunk.ChunkCustomData.dict.Count>=4096)return;marker=new ChunkCustomData(endpoint.Key,ulong.MaxValue,false){data=markerBytes};job.Chunk.ChunkCustomData.Set(endpoint.Key,marker);job.Chunk.isModified=true;}
        else if(marker.data==null||!marker.data.SequenceEqual(markerBytes)){marker.data=markerBytes;job.Chunk.isModified=true;}
        job.CapacityApplied=true;job.CapacityTarget=target;
        if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(job),()=>Current(job),endpoint.OriginalKey,endpoint.Key);
        if(job.Publication==null||!job.Publication.Saved(out var proof))return;
        var completed=new XElement(job.History);var terminal=completed.Elements().Last();terminal.SetAttributeValue("state",state==4?5:3);terminal.SetAttributeValue("after",proof);
        if(!Publish(endpoint,job.History,completed))return;job.History=completed;
        if(state!=4&&!job.Tracking.Untracked&&TrackingUnchanged(job.Tracking,actor,sender,job.Body))RebirthLogisticsBodyTracking.NotifyUnchanged(job.Tracking.Tracked,actor,sender,job.Body);
        if(state!=4&&!RebirthLogisticsLoadedBodyNotice.QueuePublished(endpoint.Original,endpoint.Incarnation,Guid.ParseExact((string)terminal.Attribute("nonce"),"N"),job.Body,old,next,job.Publication,()=>Current(job)))return;
        job.Dispose();jobs.Remove(id);
    }
    internal static void Tick(RebirthLogisticsTransferJournal.Record original)
    {
        if(!Enabled)return;
        try
        {
            if(!ThreadManager.IsMainThread())return;var current=GameManager.Instance?.World;if(!ReferenceEquals(world,current)){foreach(var job in jobs.Values)job.Dispose();jobs.Clear();terminalPulses.Clear();world=current;}
            foreach(var pair in jobs.ToArray())if(pair.Value.Expires<=DateTime.UtcNow){pair.Value.Dispose();jobs.Remove(pair.Key);}
            if(world?.IsRemote()!=false||original.Phase>4||!RebirthLogisticsDeathJournal.TryRead(original,out var death)||death.State<2&&!IsNativeBeforeOnly(original,death))return;
            var root=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death};if(original.Phase==4&&!TerminalMetadataOriginal(root,original))return;
            RebirthLogisticsLoadedBodyNotice.ReloadOriginal(original);Step(root);
            foreach(var parcel in death.Parcels)if(parcel.State>=2)Step(new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel});
        }catch{/* Unknown history/native lineage is retained, never respawned. */}
    }
    private static readonly Dictionary<string,DateTime> terminalPulses=new Dictionary<string,DateTime>(StringComparer.Ordinal);
    internal static void PulseTerminalBody(EntityBackpack body)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||body?.world?.IsRemote()!=false||!body.addedToChunk)return;
        try
        {
            foreach(var old in terminalPulses.Where(pair=>pair.Value<DateTime.UtcNow.AddSeconds(-30)).Select(pair=>pair.Key).ToArray())terminalPulses.Remove(old);
            var chunk=body.world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading)return;
            string prefix="RebirthLogisticsBackpackV1:"+body.entityId+":";var markers=chunk.ChunkCustomData.dict.Where(pair=>pair.Key.StartsWith(prefix,StringComparison.Ordinal)).Take(2).ToArray();if(markers.Length!=1)return;
            var marker=markers[0].Value;if(marker?.data==null||marker.data.Length>4096)return;
            using(var stream=new MemoryStream(marker.data,false))using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadByte()!=1)return;var incarnation=new Guid(reader.ReadBytes(16));var op=new Guid(reader.ReadBytes(16));string owner=reader.ReadString(),key=owner+":"+op.ToString("N");
                if(terminalPulses.TryGetValue(key,out var due)&&due>DateTime.UtcNow||terminalPulses.Count>=128&&!terminalPulses.ContainsKey(key))return;
                terminalPulses[key]=DateTime.UtcNow.AddSeconds(2);
                if(!RebirthLogisticsTransferJournal.TryRead(owner,op,out var original)||original.Phase!=4||!CanObserveTerminalEndpoint(original,incarnation,body.entityId))return;
                // Actual retained native GUID, never archive/ID absence, starts
                // this terminal-only metadata rebind after a cold save boundary.
                Tick(original);RebirthLogisticsAcceptedBodyJournal.Tick(original);RebirthLogisticsLoadedBodyNotice.ReloadOriginal(original);CleanupTerminalMetadata(original);
            }
        }catch{/* Malformed/retired native identity remains unknown. */}
    }
    internal static void CleanupTerminalMetadata(RebirthLogisticsTransferJournal.Record original)
    {
        if(!Enabled||original?.Phase!=4||!ThreadManager.IsMainThread()||!RebirthLogisticsDeathJournal.TryRead(original,out var death)||death.State!=4&&!(death.State==1&&IsNativeBeforeOnly(original,death)))return;
        var endpoints=new List<Endpoint>{new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death}};endpoints.AddRange(death.Parcels.Select(parcel=>new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel}));
        foreach(var endpoint in endpoints)
        {
            var body=GameManager.Instance?.World?.GetEntity(endpoint.Entity) as EntityBackpack;
            if(body==null)
            {
                if(HasPositiveRemoval(endpoint)){if(endpoint.Parcel==null)RebirthLogisticsAcceptedBodyJournal.Retire(original);RebirthLogisticsLoadedBodyNoticeJournal.ArchiveRemovedIncarnation(original,endpoint.Incarnation);}
                continue;
            }
            if(!Reserve(endpoint,out var account,out var allocation))continue;
            using(account)using(allocation)
            {
                var chunk=body.world.GetChunkSync(endpoint.X,endpoint.Z) as Chunk;
                if(!Native(endpoint,body,chunk)||!RebirthLogisticsLoadedBodyNotice.CanRetire(original,endpoint.Incarnation,body))continue;
                var history=Read(endpoint);if(!MarkerCurrent(endpoint,chunk,history))continue;
                if(endpoint.Parcel==null)RebirthLogisticsAcceptedBodyJournal.Retire(original);
                RebirthLogisticsLoadedBodyNoticeJournal.ArchiveAcknowledgedIncarnation(original,endpoint.Incarnation,body);
                // Metadata only, after actual body/nav ACK. Frozen plans,
                // native body/items/tracking and item Phase4 remain unchanged.
                chunk.ChunkCustomData.Remove(endpoint.OriginalKey);
                if(chunk.ChunkCustomData.dict.ContainsKey(endpoint.Key))chunk.ChunkCustomData.Remove(endpoint.Key);
                chunk.isModified=true;
            }
        }
    }
    internal static void ResetCurrentWorld()
    {
        if(!ThreadManager.IsMainThread())return;
        if(!ReferenceEquals(world,GameManager.Instance?.World)){foreach(var job in jobs.Values)job.Dispose();jobs.Clear();terminalPulses.Clear();world=GameManager.Instance?.World;}
        foreach(var pair in jobs.ToArray())if(pair.Value.Expires<=DateTime.UtcNow){pair.Value.Dispose();jobs.Remove(pair.Key);}
        // Only transient buffers/watches expire; persisted history and exact
        // cold original custody continue to govern access and retry.
    }
    private sealed class RemovedMarker
    {internal string Key;internal ChunkCustomData Marker;internal byte[] Bytes;}
    private static bool PublishedRemovalEndpoint(Endpoint endpoint)
        =>endpoint.Parcel==null&&IsNativeBeforeBody(endpoint.Original,endpoint.Death)||endpoint.Original.Phase==4&&endpoint.Death.State>=3&&endpoint.Death.FinalProof?.Length==64&&
          (endpoint.Parcel==null||endpoint.Parcel.State>=3&&endpoint.Parcel.FinalProof?.Length==64);
    internal static bool WasPositivelyRemoved(RebirthLogisticsTransferJournal.Record original,Guid incarnation)
    {
        try
        {
            if(!RebirthLogisticsDeathJournal.TryRead(original,out var death))return false;
            var parcel=death.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation);if(death.Incarnation!=incarnation&&parcel==null)return false;
            return HasPositiveRemoval(new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel});
        }catch{return false;}
    }
    private sealed class BeforeRemoval
    {internal Endpoint Endpoint;internal string Key;internal ChunkCustomData Marker;internal byte[] Bytes;}
    private static string RemovalPath(Endpoint endpoint)
    {
        // Validate owner/op/incarnation through the existing domain resolver,
        // then keep this fixed receipt schema outside the history directory.
        DomainPath(endpoint.Original.Owner,endpoint.Original.Operation,endpoint.Incarnation,false);
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsReferenceRemovals"),directory=Path.Combine(root,endpoint.Original.Owner);
        if(Directory.Exists(root)&&(new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)!=0||Directory.Exists(directory)&&(new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        return Path.Combine(directory,endpoint.Original.Operation.ToString("N")+(endpoint.Parcel==null?string.Empty:"_"+endpoint.Incarnation.ToString("N"))+".xml");
    }
    private static bool RemovalCapacity()
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsReferenceRemovals");if(!Directory.Exists(root))return true;
        int count=0;long bytes=2048;
        foreach(string directory in Directory.EnumerateDirectories(root))
        {
            if((new DirectoryInfo(directory).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(Path.GetFileName(directory)=="archive")continue;
            if(!Hex(Path.GetFileName(directory))||Directory.EnumerateDirectories(directory).Any())return false;
            foreach(string path in Directory.EnumerateFiles(directory))
            {
                var file=new FileInfo(path);if((file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>2048)return false;
                var name=Path.GetFileNameWithoutExtension(path).Split('_');
                if((name.Length==1||name.Length==2)&&Guid.TryParseExact(name[0],"N",out var op)&&Path.GetExtension(path)==".xml"&&
                    RebirthLogisticsTransferJournal.TryRead(Path.GetFileName(directory),op,out var saved)&&saved.Phase==4&&RebirthLogisticsDeathJournal.TryRead(saved,out var death))
                {
                    RebirthLogisticsDeathJournal.Parcel parcel=null;
                    if(name.Length==2){if(!Guid.TryParseExact(name[1],"N",out var incarnation))return false;parcel=death.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation);if(parcel==null)return false;}
                    var endpoint=new Endpoint{Original=saved,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved),Death=death,Parcel=parcel};
                    if(HasPositiveRemoval(endpoint))
                    {
                        string archive=Path.Combine(root,"archive",saved.Owner);if(Directory.Exists(archive)&&(new DirectoryInfo(archive).Attributes&FileAttributes.ReparsePoint)!=0)return false;
                        Directory.CreateDirectory(archive);string target=Path.Combine(archive,Path.GetFileName(path));
                        if(!File.Exists(target)){File.Move(path,target);continue;}
                    }
                }
                if(++count>=4096||checked(bytes+=file.Length)>8*1024*1024)return false;
            }
        }
        return !Directory.EnumerateFiles(root).Any();
    }
    private static XElement RemovalDocument(Endpoint endpoint)=>new XElement("removed",new XAttribute("version",1),new XAttribute("owner",endpoint.Original.Owner),
        new XAttribute("operation",endpoint.Original.Operation.ToString("N")),new XAttribute("incarnation",endpoint.Incarnation.ToString("N")),new XAttribute("identity",endpoint.Identity),
        new XAttribute("binding",RebirthLogisticsTransferJournal.ImmutablePlanDigest(endpoint.Original)),new XAttribute("before",endpoint.Parcel?.BeforeProof??endpoint.Death.BeforeProof),new XAttribute("publication",endpoint.Parcel!=null?endpoint.Parcel.FinalProof:endpoint.Death.State==1?endpoint.Death.BeforeProof:endpoint.Death.FinalProof),new XAttribute("entity",endpoint.Entity));
    private static bool HasPositiveRemoval(Endpoint endpoint)
    {
        if(!Enabled||!Reserve(endpoint,out var account,out var allocation))return false;
        using(account)using(allocation)
        {
        string path=RemovalPath(endpoint);if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
        string archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsReferenceRemovals","archive",endpoint.Original.Owner,Path.GetFileName(path));
        string archiveRoot=Path.GetDirectoryName(Path.GetDirectoryName(archive)),archiveOwner=Path.GetDirectoryName(archive);
        if(Directory.Exists(archiveRoot)&&(new DirectoryInfo(archiveRoot).Attributes&FileAttributes.ReparsePoint)!=0||Directory.Exists(archiveOwner)&&(new DirectoryInfo(archiveOwner).Attributes&FileAttributes.ReparsePoint)!=0)return false;
        if(File.Exists(path)&&File.Exists(archive))return false;
        if(!File.Exists(path))
        {
            if(!RebirthLogisticsTransferJournal.TryRead(endpoint.Original.Owner,endpoint.Original.Operation,out var terminal)||terminal.Phase!=4||RebirthLogisticsTransferJournal.ImmutablePlanDigest(terminal)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(endpoint.Original))return false;
            path=archive;
        }
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>2048||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2048}))
            return XNode.DeepEquals(XElement.Load(reader),RemovalDocument(endpoint));
        }
    }
    private sealed class Removal
    {internal World World;internal EntityBackpack Body;internal Chunk Chunk;internal int X,Z;internal readonly List<RemovedMarker> Markers=new List<RemovedMarker>();internal readonly List<BeforeRemoval> Before=new List<BeforeRemoval>();}
    internal static object CaptureRemoval(EntityBackpack body)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||body?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,body.world)||!body.addedToChunk)return null;
        try
        {
            var chunk=body.world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading)return null;
            var token=new Removal{World=body.world,Body=body,Chunk=chunk,X=chunk.X,Z=chunk.Z};string prefix="RebirthLogisticsReferenceV1:"+body.entityId+":";
            foreach(var pair in chunk.ChunkCustomData.dict.Where(value=>value.Key.StartsWith(prefix,StringComparison.Ordinal)))
            {
                if(token.Markers.Count>=16)return null;var marker=pair.Value;if(marker?.data==null||marker.data.Length>512||marker.TriggerWriteDataDelegate!=null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork)continue;
                string[] parts=new UTF8Encoding(false,true).GetString(marker.data).Split('|');if(parts.Length!=5||!Hex(parts[0])||!Hex(parts[3])||!Hex(parts[4])||!Guid.TryParseExact(parts[1],"N",out var operation)||operation==Guid.Empty||!Guid.TryParseExact(parts[2],"N",out var incarnation)||incarnation==Guid.Empty||pair.Key!=prefix+operation.ToString("N"))continue;
                string path=DomainPath(parts[0],operation,incarnation,false),archive=DomainPath(parts[0],operation,incarnation,true);
                if(File.Exists(path)&&File.Exists(archive)||File.Exists(path+".tmp")||File.Exists(path+".bak"))continue;var root=ReadFile(File.Exists(path)?path:archive);
                if((string)root.Attribute("owner")!=parts[0]||(string)root.Attribute("operation")!=parts[1]||(string)root.Attribute("incarnation")!=parts[2]||(string)root.Attribute("identity")!=parts[3]||!StoredMarker(root).SequenceEqual(marker.data))continue;
                token.Markers.Add(new RemovedMarker{Key=pair.Key,Marker=marker,Bytes=(byte[])marker.data.Clone()});
            }
            // Capture positive original native Before before any existing
            // invalidation prefix runs. This token grants no removal proof.
            string originalPrefix="RebirthLogisticsBackpackV1:"+body.entityId+":";
            var originalMarkers=chunk.ChunkCustomData.dict.Where(value=>value.Key.StartsWith(originalPrefix,StringComparison.Ordinal)).Take(2).ToArray();
            if(originalMarkers.Length>1)return null; // conflicting retained originals never share one removal effect
            foreach(var pair in originalMarkers)
            {
                if(token.Before.Count>=1)return null;var marker=pair.Value;if(marker?.data==null||marker.data.Length>4096)continue;
                using(var bytes=new MemoryStream(marker.data,false))using(var reader=new BinaryReader(bytes))
                {
                    if(reader.ReadByte()!=1)continue;var incarnation=new Guid(reader.ReadBytes(16));var operation=new Guid(reader.ReadBytes(16));string owner=reader.ReadString();
                    if(!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||!RebirthLogisticsDeathJournal.TryRead(original,out var death))continue;
                    var parcel=death.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation&&value.EntityId==body.entityId);
                    if((death.Incarnation!=incarnation||death.EntityId!=body.entityId)&&parcel==null)continue;
                    var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel};if(!PublishedRemovalEndpoint(endpoint))continue;
                    if(pair.Key!=endpoint.OriginalKey||!Reserve(endpoint,out var account,out var allocation))continue;
                    using(account)using(allocation)
                    {
                        if(!Native(endpoint,body,chunk))continue;
                        token.Before.Add(new BeforeRemoval{Endpoint=endpoint,Key=pair.Key,Marker=marker,Bytes=(byte[])marker.data.Clone()});
                    }
                }
            }
            return token;
        }catch{return null;}
    }
    internal static void OnRemoved(World actual,EntityBackpack body,object captured)
    {
        if(!(captured is Removal token)||!ReferenceEquals(token.World,actual)||!ReferenceEquals(token.Body,body)||ReferenceEquals(actual.GetEntity(body.entityId),body)||!ReferenceEquals(GameManager.Instance?.World,actual)||
            token.Chunk.InProgressUnloading||!ReferenceEquals(actual.GetChunkSync(token.X,token.Z),token.Chunk))return;
        foreach(var before in token.Before)
        {
            // Only the actual completed non-Unloaded native removal reaches
            // this callback. Missing entities elsewhere never create receipts.
            if(!PublishedRemovalEndpoint(before.Endpoint)||!Reserve(before.Endpoint,out var account,out var allocation))continue;
            using(account)using(allocation)
            {
            string path=RemovalPath(before.Endpoint);var document=RemovalDocument(before.Endpoint);
            if(File.Exists(path)){if(!HasPositiveRemoval(before.Endpoint))continue;}
            else
            {
                if(File.Exists(path+".tmp")||File.Exists(path+".bak")||!RemovalCapacity())continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] encoded=Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));if(encoded.Length>2048)continue;
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(encoded,0,encoded.Length);stream.Flush(true);}
                File.Move(path+".tmp",path);if(!HasPositiveRemoval(before.Endpoint))continue;
            }
            if(token.Chunk.ChunkCustomData.dict.TryGetValue(before.Key,out var originalMarker)&&ReferenceEquals(originalMarker,before.Marker)&&originalMarker.data!=null&&originalMarker.data.SequenceEqual(before.Bytes))
            {token.Chunk.ChunkCustomData.Remove(before.Key);token.Chunk.isModified=true;}
            }
        }
        foreach(var entry in token.Markers)if(token.Chunk.ChunkCustomData.dict.TryGetValue(entry.Key,out var marker)&&ReferenceEquals(marker,entry.Marker)&&marker.data!=null&&marker.data.SequenceEqual(entry.Bytes))
        {token.Chunk.ChunkCustomData.Remove(entry.Key);token.Chunk.isModified=true;}
        foreach(var pair in jobs.Where(value=>ReferenceEquals(value.Value.Body,body)).ToArray()){pair.Value.Dispose();jobs.Remove(pair.Key);}
        // Durable history remains: actual removal is not item-loss/refund or
        // replacement-spawn authority, and no absent-body replay is admitted.
    }
    internal static bool Blocks(ItemStackGrid grid)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||grid==null||GameManager.Instance?.World?.IsRemote()!=false)return false;
        if(jobs.Values.Any(job=>ReferenceEquals(job.Body?.bag?.ItemGrid,grid)))return true;
        // Exact native grid-reference lookup closes the cold interval before
        // an interaction/first pending-original tick has populated a lease.
        foreach(var entity in GameManager.Instance.World.Entities.list)
            if(entity is EntityBackpack body&&ReferenceEquals(body.bag?.ItemGrid,grid))return Holds(body);
        return false;
    }
    internal static bool Holds(EntityBackpack body)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||body?.world?.IsRemote()!=false||!body.addedToChunk)return false;
        if(jobs.Values.Any(job=>ReferenceEquals(job.Body,body)))return true;
        try
        {
            var chunk=body.world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
            string prefix="RebirthLogisticsBackpackV1:"+body.entityId+":";
            foreach(var marker in chunk.ChunkCustomData.dict.Where(pair=>pair.Key.StartsWith(prefix,StringComparison.Ordinal)).Select(pair=>pair.Value))
            {
                if(marker?.data==null||marker.data.Length>4096)continue;
                using(var stream=new MemoryStream(marker.data,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadByte()!=1)continue;reader.ReadBytes(16);var operation=new Guid(reader.ReadBytes(16));string owner=reader.ReadString();
                    if(!RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)||original.Phase>4||!RebirthLogisticsDeathJournal.TryRead(original,out var death))continue;
                    var parcel=death.Parcels.SingleOrDefault(value=>value.EntityId==body.entityId);if(body.entityId!=death.EntityId&&parcel==null)continue;
                    if(death.State<2&&!IsNativeBeforeOnly(original,death)||parcel!=null&&parcel.State<2)continue;
                    var endpoint=new Endpoint{Original=original,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Death=death,Parcel=parcel};if(!Reserve(endpoint,out var account,out var allocation))continue;
                    using(account)using(allocation)if(Native(endpoint,body,chunk))return true;
                }
            }
        }catch{}return false;
    }
}
