using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Logistics-only actual accepted native body observation. State0 is NOT a
// native publication proof. Only State1 from the exact same-snapshot witness
// permits cold adoption. Cold State0 can observe a fresh exact native save;
// an entity number, missing body or prepared intent never proves publication.
internal static class RebirthLogisticsAcceptedBodyJournal
{
    private const int MaximumBytes=262144,MaximumPending=128;
    private const string Prefix="rbLogisticsAcceptedBodyV1:";
    private sealed class Repair
    {
        internal int Stage,OldOwner,NewOwner;internal string Nonce,BeforeProof;internal uint Timestamp;internal Vector3i Position;
    }
    private sealed class Record
    {
        internal Guid Operation,Incarnation;internal string Owner,World,Creation,Binding,Proof,PacketBag,PacketEntity;
        internal int HistoricalOwner=-1;internal string Reference,HistoricalEntityHash;
        internal readonly List<Repair> Repairs=new List<Repair>();
        internal int NativeOwnerEntity=>Repairs.LastOrDefault(row=>row.Stage==2||row.Stage==3)?.NewOwner??OwnerEntity;
        internal bool Invalidated;internal int Deaths,OwnerEntity,Entity,Class,X,Z,State;internal UnityEngine.Vector3 PacketPosition;internal byte[] Bag;
    }
    private sealed class Job:IDisposable
    {
        internal Record Record;internal RebirthLogisticsTransferJournal.Record Original;internal RebirthLogisticsDeathCustody.Accepted Accepted;
        internal bool ColdReadOnly,ColdReobserve;internal Chunk Chunk;internal RebirthLogisticsBackpackPublication Publication;
        public void Dispose(){Publication?.Dispose();Publication=null;}
    }
    private static readonly Dictionary<string,Job> jobs=new Dictionary<string,Job>(StringComparer.Ordinal);
    private sealed class Notice{internal ClientInfo Sender;internal string Proof;}
    private static readonly Dictionary<string,Notice> notices=new Dictionary<string,Notice>(StringComparer.Ordinal);
    private static readonly Dictionary<string,DateTime> nextRepairs=new Dictionary<string,DateTime>(StringComparer.Ordinal);
    private static World activeWorld;
    private static bool Hex(string value,int count)=>value?.Length==count&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static string Key(Record value)=>Prefix+value.Owner+":"+value.Operation.ToString("N");
    private static string PathFor(string owner,Guid operation)
    {
        if(!Hex(owner,64)||operation==Guid.Empty)throw new InvalidDataException();
        return Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies",owner,operation.ToString("N")+".xml");
    }
    private static string JobKey(string owner,Guid operation)=>owner+":"+operation.ToString("N");
    private static void WorldCurrent()
    {
        var world=GameManager.Instance?.World;if(ReferenceEquals(activeWorld,world))return;
        foreach(var job in jobs.Values)job.Dispose();jobs.Clear();notices.Clear();nextRepairs.Clear();activeWorld=world;
    }
    internal static void ResetCurrentWorld(){if(ThreadManager.IsMainThread())WorldCurrent();}
    private static byte[] BagBytes(EntityBackpack body)
    {using(var image=StreamUtils.ToBlob(writer=>body.bag.Write(writer,StreamModeWrite.ToClient)))return image.ToArray();}
    private static Bag Decode(byte[] image)
    {
        if(image==null||image.Length<1||image.Length>131072)throw new InvalidDataException();
        using(var stream=new MemoryStream(image,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);var bag=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);if(stream.Position!=stream.Length||bag?.SlotCount<1||bag.SlotCount>256)throw new InvalidDataException();using(var encoded=StreamUtils.ToBlob(writer=>bag.Write(writer,StreamModeWrite.ToClient)))if(!encoded.ToArray().SequenceEqual(image))throw new InvalidDataException();return bag;}
    }
    private static byte[] Marker(Record value,int repairLimit=-1)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            var applied=value.Repairs.Take(repairLimit<0?value.Repairs.Count:repairLimit).Where(row=>row.Stage>=2).ToArray();
            bool observedBefore=applied.Any(row=>row.Stage>=4);
            writer.Write((byte)(observedBefore?4:applied.Length>0?3:value.Reference==null?1:2));writer.Write(value.Operation.ToByteArray());writer.Write(value.Incarnation.ToByteArray());writer.Write(value.Owner);writer.Write(value.World);writer.Write(value.Creation);writer.Write(value.Binding);
            writer.Write(value.Deaths);writer.Write(value.OwnerEntity);writer.Write(value.Entity);writer.Write(value.Class);writer.Write(value.X);writer.Write(value.Z);writer.Write(value.PacketBag);writer.Write(value.PacketEntity);writer.Write(value.PacketPosition.x);writer.Write(value.PacketPosition.y);writer.Write(value.PacketPosition.z);
            writer.Write(RebirthLogisticsOriginalTransport.Digest(Convert.ToBase64String(value.Bag)));
            if(applied.Length>0)writer.Write(value.Reference!=null);
            if(value.Reference!=null){writer.Write(value.HistoricalOwner);writer.Write(value.Reference);writer.Write(value.HistoricalEntityHash);}
            if(applied.Length>0)
            {
                writer.Write(applied.Length);
                foreach(var row in applied){if(observedBefore)writer.Write((byte)(row.Stage>=4?4:2));writer.Write(row.OldOwner);writer.Write(row.NewOwner);writer.Write(row.Nonce);writer.Write(row.BeforeProof);writer.Write(row.Timestamp);writer.Write(row.Position.x);writer.Write(row.Position.y);writer.Write(row.Position.z);}
            }
            writer.Flush();return stream.ToArray();
        }
    }
    private static string ExpectedProof(Record value,int repairLimit=-1)
    {
        // Exact stable digest produced only after BackpackPublication.Saved's
        // positive region/ECD/marker-span checks. This pure equality is not a
        // producer and State0 never carries such a proof.
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(value.Operation.ToByteArray());writer.Write(value.World);writer.Write(value.Owner);writer.Write(value.Creation);writer.Write(value.Deaths);
            writer.Write(value.OwnerEntity);writer.Write(value.Entity);writer.Write(value.Class);writer.Write(value.X);writer.Write(value.Z);
            writer.Write(value.Bag.Length);writer.Write(value.Bag);var marker=Marker(value,repairLimit);writer.Write(marker.Length);writer.Write(marker);writer.Flush();
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
    private static XElement Write(Record value)
    {
        if(value==null||value.Repairs.Count>16||value.Operation==Guid.Empty||value.Incarnation==Guid.Empty||!Hex(value.Owner,64)||!Hex(value.World,32)||!Hex(value.Binding,64)||!Hex(value.PacketBag,64)||!Hex(value.PacketEntity,64)||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||value.Deaths<0||value.OwnerEntity<0||value.Entity<0||value.State<0||value.State>1||
            value.State==0&&value.Proof!=null||value.State==1&&(!Hex(value.Proof,64)||value.Proof!=ExpectedProof(value))||
            float.IsNaN(value.PacketPosition.x)||float.IsInfinity(value.PacketPosition.x)||float.IsNaN(value.PacketPosition.y)||float.IsInfinity(value.PacketPosition.y)||float.IsNaN(value.PacketPosition.z)||float.IsInfinity(value.PacketPosition.z))throw new InvalidDataException();
        if(value.Reference==null?(value.HistoricalOwner!=-1||value.HistoricalEntityHash!=null):(value.HistoricalOwner<0||value.HistoricalOwner==value.OwnerEntity||!Hex(value.Reference,64)||!Hex(value.HistoricalEntityHash,64)))throw new InvalidDataException();
        int owner=value.OwnerEntity;var nonces=new HashSet<string>(StringComparer.Ordinal);
        for(int index=0;index<value.Repairs.Count;index++)
        {
            var row=value.Repairs[index];bool last=index==value.Repairs.Count-1;
            if(row.Stage<0||row.Stage>5||!last&&row.Stage!=3&&row.Stage!=5&&row.Stage!=0||row.OldOwner!=owner||row.NewOwner<0||row.NewOwner==row.OldOwner||
                !Guid.TryParseExact(row.Nonce,"N",out var nonce)||nonce==Guid.Empty||nonce.ToString("N")!=row.Nonce||!nonces.Add(row.Nonce)||
                !Hex(row.BeforeProof,64)||row.BeforeProof!=ExpectedProof(value,index)||last&&(row.Stage==2||row.Stage==4?value.State!=0:value.State!=1))throw new InvalidDataException();
            if(row.Stage==2||row.Stage==3)owner=row.NewOwner;
        }
        Decode(value.Bag);
        var root=new XElement("logisticsAcceptedBody",new XAttribute("version",value.Repairs.Count>0?3:value.Reference==null?1:2),new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("incarnation",value.Incarnation.ToString("N")),
            new XAttribute("owner",value.Owner),new XAttribute("world",value.World),new XAttribute("creation",value.Creation),new XAttribute("binding",value.Binding),new XAttribute("deaths",value.Deaths),
            new XAttribute("ownerEntity",value.OwnerEntity),new XAttribute("entity",value.Entity),new XAttribute("class",value.Class),new XAttribute("x",value.X),new XAttribute("z",value.Z),new XAttribute("state",value.State),new XAttribute("proof",value.Proof??string.Empty),
            new XAttribute("packetBag",value.PacketBag),new XAttribute("packetEntity",value.PacketEntity),new XAttribute("packetX",value.PacketPosition.x),new XAttribute("packetY",value.PacketPosition.y),new XAttribute("packetZ",value.PacketPosition.z),new XElement("bag",Convert.ToBase64String(value.Bag)));
        if(value.Reference!=null)root.Add(new XAttribute("historicalOwner",value.HistoricalOwner),new XAttribute("reference",value.Reference),new XAttribute("historicalEntity",value.HistoricalEntityHash));
        if(value.Repairs.Count>0)root.Add(new XElement("repairs",value.Repairs.Select(row=>new XElement("repair",new XAttribute("stage",row.Stage),
            new XAttribute("old",row.OldOwner),new XAttribute("next",row.NewOwner),new XAttribute("nonce",row.Nonce),new XAttribute("beforeProof",row.BeforeProof),
            new XAttribute("timestamp",row.Timestamp),new XAttribute("x",row.Position.x),new XAttribute("y",row.Position.y),new XAttribute("z",row.Position.z)))));
        if(value.Invalidated)root.Add(new XAttribute("invalidated",1));
        if(Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting))>MaximumBytes)throw new InvalidDataException();return root;
    }
    private static Record ReadFile(string path)
    {
        var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
        int version=(int)root.Attribute("version");bool referenced=root.Attribute("reference")!=null;
        if(root.Name!="logisticsAcceptedBody"||version<1||version>3||version==1&&referenced||version==2&&!referenced||
            root.Attributes().Count()!=20+(referenced?3:0)+(root.Attribute("invalidated")==null?0:1)||root.Elements().Count()!=(version==3?2:1)||root.Element("bag")==null||
            version==3&&(root.Element("repairs")==null||root.Element("repairs").HasAttributes||root.Element("repairs").Elements().Count()<1||root.Element("repairs").Elements().Count()>16))throw new InvalidDataException();
        var value=new Record{Invalidated=root.Attribute("invalidated")!=null&&(int)root.Attribute("invalidated")==1,Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),Incarnation=Guid.ParseExact((string)root.Attribute("incarnation"),"N"),Owner=(string)root.Attribute("owner"),World=(string)root.Attribute("world"),Creation=(string)root.Attribute("creation"),Binding=(string)root.Attribute("binding"),
            Deaths=(int)root.Attribute("deaths"),OwnerEntity=(int)root.Attribute("ownerEntity"),Entity=(int)root.Attribute("entity"),Class=(int)root.Attribute("class"),X=(int)root.Attribute("x"),Z=(int)root.Attribute("z"),State=(int)root.Attribute("state"),Proof=string.IsNullOrEmpty((string)root.Attribute("proof"))?null:(string)root.Attribute("proof"),
            PacketBag=(string)root.Attribute("packetBag"),PacketEntity=(string)root.Attribute("packetEntity"),PacketPosition=new UnityEngine.Vector3((float)root.Attribute("packetX"),(float)root.Attribute("packetY"),(float)root.Attribute("packetZ")),Bag=Convert.FromBase64String((string)root.Element("bag"))};
        if(referenced){value.HistoricalOwner=(int)root.Attribute("historicalOwner");value.Reference=(string)root.Attribute("reference");value.HistoricalEntityHash=(string)root.Attribute("historicalEntity");}
        if(version==3)foreach(var row in root.Element("repairs").Elements())
        {
            if(row.Name!="repair"||row.HasElements||row.Attributes().Count()!=9)throw new InvalidDataException();
            value.Repairs.Add(new Repair{Stage=(int)row.Attribute("stage"),OldOwner=(int)row.Attribute("old"),NewOwner=(int)row.Attribute("next"),Nonce=(string)row.Attribute("nonce"),
                BeforeProof=(string)row.Attribute("beforeProof"),Timestamp=(uint)row.Attribute("timestamp"),Position=new Vector3i((int)row.Attribute("x"),(int)row.Attribute("y"),(int)row.Attribute("z"))});
        }
        if(!XNode.DeepEquals(root,Write(value)))throw new InvalidDataException();return value;
    }
    private static bool Bound(Record value,RebirthLogisticsTransferJournal.Record original)
        =>original!=null&&value.Operation==original.Operation&&value.Owner==original.Owner&&value.World==original.World&&value.Creation==original.Creation&&value.Deaths==original.Deaths&&value.Binding==RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)&&value.World==RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance?.World)&&ReferenceMatches(value,original);
    private static bool ReferenceMatches(Record value,RebirthLogisticsTransferJournal.Record original)
    {
        if(value.Reference==null)return true;
        return RebirthLogisticsBodyReferenceJournal.TryMatchAccepted(original,value.OwnerEntity,value.PacketBag,value.PacketEntity,out int oldOwner,out var reference,out var historicalHash)&&
            value.HistoricalOwner==oldOwner&&value.Reference==reference&&value.HistoricalEntityHash==historicalHash;
    }
    internal static bool HasAnyCandidate(RebirthLogisticsTransferJournal.Record original)
    {
        string path=PathFor(original.Owner,original.Operation);
        return File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies","archive",original.Owner,original.Operation.ToString("N")+".xml"));
    }
    private static bool TryRead(RebirthLogisticsTransferJournal.Record original,out Record value)
    {
        value=null;try
        {
            string path=PathFor(original.Owner,original.Operation);if(File.Exists(path+".bak"))return false;
            string archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies","archive",original.Owner,original.Operation.ToString("N")+".xml");
            var record=ReadFile(File.Exists(path)?path:archive);if(!Bound(record,original))return false;
            if(File.Exists(path)&&File.Exists(archive)&&!XNode.DeepEquals(Write(record),Write(ReadFile(archive))))return false;
            if(File.Exists(path)&&File.Exists(path+".tmp"))
            {
                // This promotion is ONLY an actual Saved observer's flushed
                // State1 temp, never a prepared State0 inferred from a body.
                var next=ReadFile(path+".tmp");var expected=Write(record);var candidate=Write(next);candidate.SetAttributeValue("state",record.State);candidate.SetAttributeValue("proof",record.Proof??string.Empty);
                if(record.Repairs.LastOrDefault()?.Stage==2&&next.Repairs.LastOrDefault()?.Stage==3||record.Repairs.LastOrDefault()?.Stage==4&&next.Repairs.LastOrDefault()?.Stage==5)candidate.Element("repairs").Elements().Last().SetAttributeValue("stage",record.Repairs.Last().Stage);
                if(record.State!=0||next.State!=1||!Bound(next,original)||!XNode.DeepEquals(expected,candidate))return false;
                File.Replace(path+".tmp",path,null);record=ReadFile(path);
            }
            value=record;return true;
        }
        catch{return false;}
    }
    private static bool AdmissionFits(string path,int bytes)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies");if(!Directory.Exists(root))return true;
        if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return false;
        // Retire only exact saved terminal settlements before counting pending
        // capacity; completed lifetime history lives under the retained archive.
        var directories=Directory.EnumerateDirectories(root).Take(4097).ToArray();if(directories.Length>4096)return false;
        foreach(var directory in directories)
        {
            if(Path.GetFileName(directory)=="archive")continue;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||!Hex(Path.GetFileName(directory),64))return false;
            var files=Directory.EnumerateFiles(directory,"*.xml").Take(MaximumPending+1).ToArray();if(files.Length>MaximumPending)return false;
            foreach(var entry in files)
            {var prior=ReadFile(entry);if(RebirthLogisticsTransferJournal.TryRead(prior.Owner,prior.Operation,out var original)&&original.Phase==4)Retire(original);}
        }
        int count=0;long total=bytes;
        foreach(var directory in directories)
        {
            if(Path.GetFileName(directory)=="archive")continue;
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||!Hex(Path.GetFileName(directory),64))return false;
            foreach(var entry in Directory.EnumerateFiles(directory))
            {if(++count>MaximumPending*2)return false;var file=new FileInfo(entry);if((file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>MaximumBytes)return false;total+=file.Length;if(total>32L*1024*1024)return false;}
        }
        return count<MaximumPending;
    }
    private static Record Clone(Record value)
    {
        var next=new Record{Operation=value.Operation,Incarnation=value.Incarnation,Owner=value.Owner,World=value.World,Creation=value.Creation,Binding=value.Binding,Proof=value.Proof,
            PacketBag=value.PacketBag,PacketEntity=value.PacketEntity,HistoricalOwner=value.HistoricalOwner,Reference=value.Reference,HistoricalEntityHash=value.HistoricalEntityHash,
            Invalidated=value.Invalidated,Deaths=value.Deaths,OwnerEntity=value.OwnerEntity,Entity=value.Entity,Class=value.Class,X=value.X,Z=value.Z,State=value.State,PacketPosition=value.PacketPosition,Bag=value.Bag};
        foreach(var row in value.Repairs)next.Repairs.Add(new Repair{Stage=row.Stage,OldOwner=row.OldOwner,NewOwner=row.NewOwner,Nonce=row.Nonce,BeforeProof=row.BeforeProof,Timestamp=row.Timestamp,Position=row.Position});
        return next;
    }
    private static bool ReplaceRepair(RebirthLogisticsTransferJournal.Record original,Record before,Record next)
    {
        string path=PathFor(original.Owner,original.Operation);
        if(File.Exists(path+".tmp")||File.Exists(path+".bak")||!XNode.DeepEquals(Write(ReadFile(path)),Write(before)))return false;
        byte[] bytes=Encoding.UTF8.GetBytes(Write(next).ToString(SaveOptions.DisableFormatting));
        using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        File.Replace(path+".tmp",path,null);return XNode.DeepEquals(Write(ReadFile(path)),Write(next));
    }
    private static bool RepairBody(Record value,EntityBackpack body,Chunk chunk,int owner,byte[] markerBytes)
    {
        return body!=null&&chunk!=null&&!chunk.InProgressUnloading&&ReferenceEquals(GameManager.Instance?.World?.GetEntity(value.Entity),body)&&
            ReferenceEquals(body.world,GameManager.Instance.World)&&body.entityClass==value.Class&&body.RefPlayerId==owner&&body.addedToChunk&&
            body.chunkPosAddedEntityTo.x==value.X&&body.chunkPosAddedEntityTo.z==value.Z&&ReferenceEquals(body.world.GetChunkSync(value.X,value.Z),chunk)&&
            chunk.ChunkCustomData.dict.TryGetValue(Key(value),out var marker)&&marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&
            marker.data!=null&&marker.data.SequenceEqual(markerBytes)&&BagBytes(body).SequenceEqual(value.Bag);
    }
    private static bool TerminalReferenceOriginal(RebirthLogisticsTransferJournal.Record original,Record value)
    {
        var row=value?.Repairs.LastOrDefault();return original?.Phase==4&&value!=null&&!value.Invalidated&&Bound(value,original)&&
            (value.State==1&&value.Proof?.Length==64||value.State==0&&(row?.Stage==2||row?.Stage==4)&&row.BeforeProof?.Length==64);
    }
    private static bool ResolveRepairActor(RebirthLogisticsTransferJournal.Record original,out EntityPlayer actor,out ClientInfo sender)
    {
        actor=null;sender=null;var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||original.Phase>4)return false;
        var list=GameManager.Instance.GetPersistentPlayerList();if(list==null)return false;
        var rows=list.Players.Values.Where(row=>RebirthStablePlayerIdentity.TryFromPersistentPlayer(row,out var owner)&&owner.StorageKey==original.Owner).Take(2).ToArray();
        if(rows.Length!=1)return false;sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(rows[0].EntityId);actor=world.GetEntity(rows[0].EntityId) as EntityPlayer;
        return RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)&&original.Creation==RebirthCamperCharacterAccess.CurrentCreation(actor)&&actor.Died>=original.Deaths+1;
    }
    private static bool RepairTracking(Record value,Repair row,EntityPlayer actor,ClientInfo sender,EntityBackpack body,out RebirthLogisticsBodyTracking.Entry entry)
    {
        entry=null;return RebirthLogisticsBodyTracking.TryObserve(actor,sender,value.Owner,body,out entry)&&entry.Timestamp==row.Timestamp&&entry.Position.Equals(row.Position);
    }
    private static readonly bool ReferenceRepairEnabled=true;
    internal static bool ReferenceReadyForSettlement(RebirthLogisticsTransferJournal.Record original)
    {
        if(!ReferenceRepairEnabled)return true;
        try
        {
            if(!TryRead(original,out var value)||value.State!=1||value.Invalidated||value.Repairs.Any(row=>row.Stage!=3&&row.Stage!=5&&row.Stage!=0)||!ResolveRepairActor(original,out var actor,out _)||value.NativeOwnerEntity!=actor.entityId)return false;
            return RepairBody(value,actor.world.GetEntity(value.Entity) as EntityBackpack,actor.world.GetChunkSync(value.X,value.Z) as Chunk,value.NativeOwnerEntity,Marker(value));
        }
        catch{return false;}
    }
    private static bool RecoverRepairMetadata(RebirthLogisticsTransferJournal.Record original)
    {
        string path=PathFor(original.Owner,original.Operation);
        if(File.Exists(path+".bak"))return false;if(!File.Exists(path+".tmp"))return true;if(!File.Exists(path))return false;
        var before=ReadFile(path);var next=ReadFile(path+".tmp");if(!Bound(before,original)||!Bound(next,original))return false;
        if(before.State==0&&next.State==1)return true; // Existing exact native Saved proof promotion below.
        bool valid=false;var normalized=Clone(next);var prior=before.Repairs.LastOrDefault();var last=next.Repairs.LastOrDefault();
        if(before.State==1&&next.State==1&&next.Repairs.Count==before.Repairs.Count+1&&last?.Stage==1)
        {normalized.Repairs.RemoveAt(normalized.Repairs.Count-1);valid=XNode.DeepEquals(Write(normalized),Write(before));}
        else if(before.State==1&&next.State==1&&next.Repairs.Count==before.Repairs.Count&&prior?.Stage==1&&last?.Stage==0)
        {normalized.Repairs.Last().Stage=1;valid=XNode.DeepEquals(Write(normalized),Write(before));}
        else if(before.State==1&&next.State==0&&next.Repairs.Count==before.Repairs.Count&&prior?.Stage==1&&last?.Stage==2)
        {normalized.State=1;normalized.Proof=before.Proof;normalized.Repairs.Last().Stage=1;valid=XNode.DeepEquals(Write(normalized),Write(before));}
        if(before.State==0&&next.State==0&&before.Repairs.Count==next.Repairs.Count&&prior?.Stage==2&&last?.Stage==4)
        {normalized.Repairs.Last().Stage=2;valid=XNode.DeepEquals(Write(normalized),Write(before));}
        if(!valid)return false;File.Replace(path+".tmp",path,null);return XNode.DeepEquals(Write(ReadFile(path)),Write(next));
    }
    private static bool TryRepairReference(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(!ReferenceRepairEnabled||original.Phase>4||RebirthLogisticsDeathJournal.HasAnyCandidate(original)||!ResolveRepairActor(original,out var actor,out var sender))return false;
            string path=PathFor(original.Owner,original.Operation);var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>MaximumBytes)return false;
            if(original.Phase==4&&(!TryRead(original,out var terminal)||!TerminalReferenceOriginal(original,terminal)))return false;
            using(var account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,()=>ReferenceEquals(GameManager.Instance?.World,actor.world)))
            {
                long tempLength=File.Exists(path+".tmp")?new FileInfo(path+".tmp").Length:0;
                if(tempLength<0||tempLength>MaximumBytes||account==null||!account.TryReserveAllocation(checked(1048576L+32L*(info.Length+tempLength)),out var allocation))return false;
                using(allocation)
                {
                    if(!RecoverRepairMetadata(original)||!TryRead(original,out var value)||value.Invalidated)return false;
                    var body=actor.world.GetEntity(value.Entity) as EntityBackpack;var chunk=actor.world.GetChunkSync(value.X,value.Z) as Chunk;
                    var row=value.Repairs.LastOrDefault();
                    if(value.State==1&&(row==null||row.Stage==3||row.Stage==5||row.Stage==0)&&value.NativeOwnerEntity==actor.entityId)
                    {
                        string noticeKey=JobKey(value.Owner,value.Operation);
                        if(row!=null&&(!notices.TryGetValue(noticeKey,out var notice)||!ReferenceEquals(notice.Sender,sender)||notice.Proof!=value.Proof)&&
                            RebirthLogisticsBodyTracking.TryObserve(actor,sender,value.Owner,body,out var unchanged)&&unchanged.Timestamp==row.Timestamp&&unchanged.Position.Equals(row.Position)&&
                            (notices.ContainsKey(noticeKey)||notices.Count<MaximumPending)&&RebirthLogisticsBodyTracking.NotifyUnchanged(unchanged,actor,sender,body))
                            notices[noticeKey]=new Notice{Sender=sender,Proof=value.Proof};
                        if(row?.Stage==3)QueueLoadedReference(original,value,body,chunk);
                        return true;
                    }
                    if(value.State==1&&(row==null||row.Stage==3||row.Stage==5||row.Stage==0))
                    {
                        if(value.Repairs.Count>=16||!RepairBody(value,body,chunk,value.NativeOwnerEntity,Marker(value))||!RebirthLogisticsBodyTracking.TryObserve(actor,sender,value.Owner,body,out var entry))return false;
                        var prepared=Clone(value);prepared.Repairs.Add(new Repair{Stage=1,OldOwner=value.NativeOwnerEntity,NewOwner=actor.entityId,Nonce=Guid.NewGuid().ToString("N"),BeforeProof=value.Proof,Timestamp=entry.Timestamp,Position=entry.Position});
                        if(!ReplaceRepair(original,value,prepared))return false;value=prepared;row=value.Repairs.Last();
                    }
                    if(row==null||row.Stage==3||row.Stage==5||row.Stage==0)return false;
                    if(!RepairTracking(value,row,actor,sender,body,out var retainedEntry))return false;
                    if(row.Stage==1&&row.NewOwner!=actor.entityId)
                    {
                        if(!RepairBody(value,body,chunk,row.OldOwner,Marker(value)))return false;
                        var rejected=Clone(value);rejected.Repairs.Last().Stage=0;
                        return ReplaceRepair(original,value,rejected);
                    }
                    if(row.Stage==1)
                    {
                        if(value.State!=1||!RepairBody(value,body,chunk,row.OldOwner,Marker(value)))return false;
                        var attempted=Clone(value);attempted.Repairs.Last().Stage=2;attempted.State=0;attempted.Proof=null;
                        if(!ReplaceRepair(original,value,attempted))return false;value=attempted;row=value.Repairs.Last();
                    }
                    if((row.Stage!=2&&row.Stage!=4)||value.State!=0)return false;
                    byte[] prior=Marker(value,value.Repairs.Count-1);
                    if(row.Stage==2&&body?.RefPlayerId==row.OldOwner&&row.NewOwner!=actor.entityId)
                    {
                        if(!RepairBody(value,body,chunk,row.OldOwner,prior))return false;
                        // Retain Attempted lineage; this is a NEW current native
                        // before-image observation, never historical no-effect.
                        var observed=Clone(value);observed.Repairs.Last().Stage=4;
                        if(!ReplaceRepair(original,value,observed))return false;value=observed;row=value.Repairs.Last();
                    }
                    byte[] after=Marker(value);
                    if(row.Stage==4)
                    {
                        if(!RepairBody(value,body,chunk,row.OldOwner,prior)&&!RepairBody(value,body,chunk,row.OldOwner,after))return false;
                    }
                    else
                    if(body?.RefPlayerId==row.OldOwner)
                    {
                        if(row.NewOwner!=actor.entityId||!RepairBody(value,body,chunk,row.OldOwner,prior)||!RebirthLogisticsBodyTracking.IsUnchanged(retainedEntry,actor,sender,body))return false;
                        // Exact original same-body pointer CAS only. No inventory
                        // write, tracking insert, new entity, or player rewrite.
                        body.RefPlayerId=row.NewOwner;
                    }
                    if(row.Stage==2&&!RepairBody(value,body,chunk,row.NewOwner,prior)&&!RepairBody(value,body,chunk,row.NewOwner,after))return false;
                    var marker=chunk.ChunkCustomData.dict[Key(value)];if(!marker.data.SequenceEqual(after)){marker.data=after;chunk.isModified=true;}
                    RebirthLogisticsDeathCustody.ObserveLiveReference(original,body,value.NativeOwnerEntity);
                    string key=JobKey(value.Owner,value.Operation);
                    if(jobs.TryGetValue(key,out var job)){job.Dispose();job.Record=value;job.Accepted.LiveOwnerEntity=value.NativeOwnerEntity;job.ColdReadOnly=false;job.ColdReobserve=false;}
                    else
                    {
                        var detached=Decode(value.Bag);var accepted=new RebirthLogisticsDeathCustody.Accepted{World=actor.world,Backpack=body,Owner=value.Owner,Creation=value.Creation,Operation=value.Operation,OriginalDeaths=value.Deaths,
                            OriginalOwnerEntity=value.OwnerEntity,LiveOwnerEntity=value.NativeOwnerEntity,EntityId=value.Entity,EntityClass=value.Class,NativeBag=ItemStack.Clone(detached.ItemGrid.items),
                            NativeLocks=Enumerable.Range(0,detached.SlotCount).Select(slot=>detached.ItemGrid.IsLocked(slot)).ToArray(),PacketBagHash=value.PacketBag,PacketEntityHash=value.PacketEntity,PacketClass=value.Class,PacketPosition=value.PacketPosition};
                        if(jobs.Count>=MaximumPending)return false;jobs.Add(key,new Job{Record=value,Original=original,Accepted=accepted,Chunk=chunk});
                    }
                    return false; // Only actual saved native AFTER can complete.
                }
            }
        }
        catch{return false;}
    }
    private static bool Current(Job job,bool requireMarker=true)
    {
        var value=job?.Record;var body=job?.Accepted?.Backpack;var world=GameManager.Instance?.World;
        if(value==null||value.Invalidated||world?.IsRemote()!=false||!Bound(value,job.Original)||!RebirthLogisticsTransferJournal.TryRead(value.Owner,value.Operation,out var retained)||(retained.Phase>=4&&!TerminalReferenceOriginal(retained,value))||!Bound(value,retained)||!ReferenceEquals(job.Accepted.World,world)||job.Accepted.Ambiguous||
            !ReferenceEquals(world.GetEntity(value.Entity),body)||body.RefPlayerId!=value.NativeOwnerEntity||body.entityClass!=value.Class||!body.addedToChunk||body.chunkPosAddedEntityTo.x!=value.X||body.chunkPosAddedEntityTo.z!=value.Z||
            job.Chunk==null||job.Chunk.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(value.X,value.Z),job.Chunk)||!BagBytes(body).SequenceEqual(value.Bag))return false;
        if(!requireMarker)return true;
        return job.Chunk.ChunkCustomData.dict.TryGetValue(Key(value),out var marker)&&marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&marker.data.SequenceEqual(Marker(value));
    }
    internal static void Observe(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathCustody.Accepted actual)
    {
        try
        {
            WorldCurrent();if(!ThreadManager.IsMainThread()||activeWorld?.IsRemote()!=false||actual==null||actual.Ambiguous||original.Phase>=4||
                actual.Operation!=original.Operation||actual.Owner!=original.Owner||actual.Creation!=original.Creation||actual.OriginalDeaths!=original.Deaths||!ReferenceEquals(actual.World,activeWorld)||
                !RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.Mode<1||fatal.Mode>5||!actual.Backpack.addedToChunk||!RebirthLogisticsDeathPartition.Same(actual.NativeBag,actual.Backpack.bag?.ItemGrid?.items)||
                actual.NativeLocks?.Length!=actual.NativeBag.Length||Enumerable.Range(0,actual.NativeLocks.Length).Any(slot=>actual.NativeLocks[slot]!=actual.Backpack.bag.ItemGrid.IsLocked(slot)))return;
            string key=JobKey(original.Owner,original.Operation);if(jobs.ContainsKey(key))return;if(jobs.Count>=MaximumPending)return;
            Record value;
            string path=PathFor(original.Owner,original.Operation);
            if(File.Exists(path)||File.Exists(path+".tmp"))
            {
                if(!File.Exists(path)&&File.Exists(path+".tmp"))
                {
                    // Resume only an uninterrupted actual accepted native
                    // reference. A cold State0 is never adopted by this path.
                    value=ReadFile(path+".tmp");if(value.State!=0||!Bound(value,original)||value.Entity!=actual.EntityId||value.Class!=actual.EntityClass||value.OwnerEntity!=actual.OriginalOwnerEntity||
                        value.PacketBag!=actual.PacketBagHash||value.PacketEntity!=actual.PacketEntityHash||!value.PacketPosition.Equals(actual.PacketPosition)||!BagBytes(actual.Backpack).SequenceEqual(value.Bag))return;
                    File.Move(path+".tmp",path);
                }
                if(!TryRead(original,out value))return;
            }
            else
            {
                value=new Record{Operation=original.Operation,Incarnation=Guid.NewGuid(),Owner=original.Owner,World=original.World,Creation=original.Creation,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),Deaths=original.Deaths,
                    OwnerEntity=actual.OriginalOwnerEntity,Entity=actual.EntityId,Class=actual.EntityClass,X=actual.Backpack.chunkPosAddedEntityTo.x,Z=actual.Backpack.chunkPosAddedEntityTo.z,
                    PacketBag=actual.PacketBagHash,PacketEntity=actual.PacketEntityHash,PacketPosition=actual.PacketPosition,Bag=BagBytes(actual.Backpack)};
                if(actual.OriginalOwnerEntity!=fatal.OwnerEntity)
                {
                    if(!RebirthLogisticsBodyReferenceJournal.TryMatchAccepted(original,actual.OriginalOwnerEntity,actual.PacketBagHash,actual.PacketEntityHash,out int historicalOwner,out var reference,out var historicalHash))return;
                    value.HistoricalOwner=historicalOwner;value.Reference=reference;value.HistoricalEntityHash=historicalHash;
                }
                var text=Encoding.UTF8.GetBytes(Write(value).ToString(SaveOptions.DisableFormatting));if(!AdmissionFits(path,text.Length))return;
                Directory.CreateDirectory(Path.GetDirectoryName(path));using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(text,0,text.Length);stream.Flush(true);}File.Move(path+".tmp",path);
            }
            var job=new Job{Record=value,Original=original,Accepted=actual,Chunk=activeWorld.GetChunkSync(value.X,value.Z) as Chunk};
            if(value.Invalidated||!Current(job,false)||actual.PacketBagHash!=value.PacketBag||actual.PacketEntityHash!=value.PacketEntity||actual.PacketClass!=value.Class||!actual.PacketPosition.Equals(value.PacketPosition))return;
            if(!job.Chunk.ChunkCustomData.dict.TryGetValue(Key(value),out var marker))
            {
                if(value.State!=0)return;marker=new ChunkCustomData(Key(value),ulong.MaxValue,false){data=Marker(value)};job.Chunk.ChunkCustomData.Set(Key(value),marker);job.Chunk.isModified=true;
            }
            if(!Current(job))return;jobs.Add(key,job);
        }
        catch{/* Native accepted body remains physical; unknown original is retained. */}
    }
    internal static bool TryAdopt(RebirthLogisticsTransferJournal.Record original,out RebirthLogisticsDeathCustody.Accepted actual)
    {
        actual=null;try
        {
            WorldCurrent();if(!ThreadManager.IsMainThread()||activeWorld?.IsRemote()!=false||original.Phase>=4||!TryRead(original,out var value)||value.State!=1||value.Invalidated||!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original))return false;
            var body=activeWorld.GetEntity(value.Entity) as EntityBackpack;if(body==null)return false;var detached=Decode(value.Bag);
            var accepted=new RebirthLogisticsDeathCustody.Accepted{World=activeWorld,Backpack=body,Owner=value.Owner,Creation=value.Creation,Operation=value.Operation,OriginalDeaths=value.Deaths,OriginalOwnerEntity=value.OwnerEntity,LiveOwnerEntity=value.NativeOwnerEntity,EntityId=value.Entity,EntityClass=value.Class,
                NativeBag=ItemStack.Clone(detached.ItemGrid.items),NativeLocks=Enumerable.Range(0,detached.SlotCount).Select(slot=>detached.ItemGrid.IsLocked(slot)).ToArray(),PacketBagHash=value.PacketBag,PacketEntityHash=value.PacketEntity,PacketClass=value.Class,PacketPosition=value.PacketPosition};
            var job=new Job{Record=value,Original=original,Accepted=accepted,Chunk=activeWorld.GetChunkSync(value.X,value.Z) as Chunk};if(!Current(job))return false;
            string key=JobKey(value.Owner,value.Operation);
            if(jobs.TryGetValue(key,out var old))
            {if(old.ColdReadOnly||old.Record.State!=1||!Current(old)){old.Dispose();jobs[key]=job;}}
            else{if(jobs.Count>=MaximumPending)return false;jobs.Add(key,job);}actual=accepted;return true;
        }
        catch{return false;}
    }
    internal static bool HasPublishedNoticeIdentity(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsLoadedBodyNotice.Notice notice,EntityBackpack body)
    {
        if(!TryRead(original,out var value)||value.State!=1||value.Proof?.Length!=64||value.Incarnation!=notice.Incarnation||value.Entity!=body.entityId||
            !RepairBody(value,body,body.world.GetChunkSync(value.X,value.Z) as Chunk,notice.Next,Marker(value)))return false;
        var row=value.Repairs.LastOrDefault();if(row==null)return notice.Old==notice.Next&&notice.Nonce==value.Incarnation;
        return row.Nonce==notice.Nonce.ToString("N")&&(row.Stage==3&&row.OldOwner==notice.Old&&row.NewOwner==notice.Next||(row.Stage==0||row.Stage==5)&&notice.Old==notice.Next);
    }
    private static bool QueueLoadedReference(RebirthLogisticsTransferJournal.Record original,Record value,EntityBackpack body,Chunk chunk)
    {
        var row=value.Repairs.LastOrDefault();if(!RebirthLogisticsLoadedBodyNotice.IsEnabled||value.State!=1||value.Proof?.Length!=64||row!=null&&row.Stage!=3&&row.Stage!=0&&row.Stage!=5)return false;
        Guid nonce=row==null?value.Incarnation:Guid.ParseExact(row.Nonce,"N");int old=row?.Stage==3?row.OldOwner:value.NativeOwnerEntity,next=value.NativeOwnerEntity;
        if(RebirthLogisticsLoadedBodyNotice.HasQueued(original,value.Incarnation,nonce))return true;
        bool Current()=>TryRead(original,out var saved)&&XNode.DeepEquals(Write(saved),Write(value))&&RepairBody(value,body,chunk,value.NativeOwnerEntity,Marker(value));
        if(!Current())return false;
        var accepted=new RebirthLogisticsDeathCustody.Accepted{World=body.world,Backpack=body,Owner=value.Owner,Creation=value.Creation,Operation=value.Operation,OriginalDeaths=value.Deaths,
            OriginalOwnerEntity=value.OwnerEntity,LiveOwnerEntity=value.NativeOwnerEntity,EntityId=value.Entity,EntityClass=value.Class};
        string key=JobKey(value.Owner,value.Operation);
        if(!jobs.TryGetValue(key,out var job))
        {if(jobs.Count>=MaximumPending)return false;job=new Job{Record=value,Original=original,Accepted=accepted,Chunk=chunk};jobs.Add(key,job);}
        if(!XNode.DeepEquals(Write(job.Record),Write(value))||!ReferenceEquals(job.Accepted.Backpack,body))return false;
        job.Original=original;if(job.Publication!=null&&!job.Publication.IsCurrent())job.Dispose();
        if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(accepted,Current,Key(value));
        if(job.Publication==null||!RebirthLogisticsLoadedBodyNotice.QueuePublished(original,value.Incarnation,nonce,body,old,next,job.Publication,Current))return false;
        job.Dispose();return true;
    }
    internal static void Tick(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            WorldCurrent();RebirthLogisticsLoadedBodyNotice.ReloadOriginal(original);
            if(original.Phase==4)
            {
                if(ReferenceRepairEnabled)TryRepairReference(original);
                if(!TryRead(original,out var terminal)||!TerminalReferenceOriginal(original,terminal))return;
                if(terminal.State==1)
                {
                    if(!RebirthLogisticsDeathJournal.HasAnyCandidate(original))QueueLoadedReference(original,terminal,activeWorld?.GetEntity(terminal.Entity) as EntityBackpack,activeWorld?.GetChunkSync(terminal.X,terminal.Z) as Chunk);
                    Retire(original);return;
                }
                // Only an already recorded terminal reference2/4 can fall
                // through to actual native AFTER observation, never item work.
            }
            if(ReferenceRepairEnabled)
            {
                string repairKey=JobKey(original.Owner,original.Operation);
                if((nextRepairs.ContainsKey(repairKey)||nextRepairs.Count<MaximumPending)&&(!nextRepairs.TryGetValue(repairKey,out var due)||DateTime.UtcNow>=due))
                {nextRepairs[repairKey]=DateTime.UtcNow.AddSeconds(1);TryRepairReference(original);}
            }
            string key=JobKey(original.Owner,original.Operation);
            if(jobs.TryGetValue(key,out var job)&&!Current(job)){job.Dispose();jobs.Remove(key);job=null;}
            if(job==null)
            {
                // Pending-original processing bootstraps only an exact existing
                // cold endpoint; no interaction/open or replacement is needed.
                if(!TryRead(original,out var cold)||cold.State!=0||cold.Invalidated||
                    !(activeWorld?.GetEntity(cold.Entity) is EntityBackpack body)||!TryHoldUnknownState0(original,body)||!jobs.TryGetValue(key,out job))return;
            }
            job.Original=original;
            if(!Current(job)||job.ColdReadOnly&&!job.ColdReobserve)return;
            // Cold State0 may request a NEW readonly native publication of the
            // existing exact GUID/body. It neither assumes an earlier save nor
            // adopts mutation-capable custody before actual Saved evidence.
            if(job.ColdReobserve&&(job.Record.State!=0||!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original)))return;
            if(job.Record.State==1){QueueLoadedReference(original,job.Record,job.Accepted.Backpack,job.Chunk);return;}
            if(job.Record.State==0)
            {
                if(job.Publication!=null&&!job.Publication.IsCurrent())job.Dispose();
                if(job.Publication==null)job.Publication=RebirthLogisticsBackpackPublication.Begin(job.Accepted,()=>Current(job),Key(job.Record));
                if(job.Publication==null||!job.Publication.Saved(out var proof)||proof!=ExpectedProof(job.Record))return;
                string path=PathFor(original.Owner,original.Operation);if(!TryRead(original,out var before))return;
                if(before.State==1){var normalized=Write(before);normalized.SetAttributeValue("state",0);normalized.SetAttributeValue("proof",string.Empty);if(job.Record.Repairs.LastOrDefault()?.Stage==2&&before.Repairs.LastOrDefault()?.Stage==3||job.Record.Repairs.LastOrDefault()?.Stage==4&&before.Repairs.LastOrDefault()?.Stage==5)normalized.Element("repairs").Elements().Last().SetAttributeValue("stage",job.Record.Repairs.Last().Stage);if(XNode.DeepEquals(normalized,Write(job.Record))){job.Record=before;job.ColdReobserve=false;job.Dispose();}return;}
                if(!XNode.DeepEquals(Write(before),Write(job.Record)))return;
                before.State=1;before.Proof=proof;if(before.Repairs.LastOrDefault()?.Stage==2)before.Repairs.Last().Stage=3;else if(before.Repairs.LastOrDefault()?.Stage==4)before.Repairs.Last().Stage=5;var bytes=Encoding.UTF8.GetBytes(Write(before).ToString(SaveOptions.DisableFormatting));
                if(File.Exists(path+".tmp"))return;
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Replace(path+".tmp",path,null);job.Record=before;job.ColdReobserve=false;
                var completedRepair=before.Repairs.LastOrDefault();
                if(completedRepair?.Stage==3&&Guid.TryParseExact(completedRepair.Nonce,"N",out var noticeNonce))
                    RebirthLogisticsLoadedBodyNotice.QueuePublished(original,before.Incarnation,noticeNonce,job.Accepted.Backpack,completedRepair.OldOwner,completedRepair.NewOwner,job.Publication,()=>Current(job));
                job.Dispose();
            }
        }
        catch{/* No positive native publication means State0 remains unknown. */}
    }
    private static bool RepairTransitionCurrent(Job job)
    {
        var value=job?.Record;var row=value?.Repairs.LastOrDefault();var body=job?.Accepted?.Backpack;
        return value!=null&&!value.Invalidated&&value.State==0&&(row?.Stage==2||row?.Stage==4)&&Bound(value,job.Original)&&
            (RepairBody(value,body,job.Chunk,row.OldOwner,Marker(value,value.Repairs.Count-1))||row.Stage==4&&RepairBody(value,body,job.Chunk,row.OldOwner,Marker(value))||row.Stage==2&&(RepairBody(value,body,job.Chunk,row.NewOwner,Marker(value,value.Repairs.Count-1))||RepairBody(value,body,job.Chunk,row.NewOwner,Marker(value))));
    }
    private static bool TryHoldRepairTransition(RebirthLogisticsTransferJournal.Record original,EntityBackpack body)
    {
        if(!TryRead(original,out var value)||value.State!=0||value.Invalidated||value.Entity!=body.entityId||(value.Repairs.LastOrDefault()?.Stage!=2&&value.Repairs.LastOrDefault()?.Stage!=4))return false;
        var accepted=new RebirthLogisticsDeathCustody.Accepted{World=activeWorld,Backpack=body,Owner=value.Owner,Creation=value.Creation,Operation=value.Operation,OriginalDeaths=value.Deaths,
            OriginalOwnerEntity=value.OwnerEntity,LiveOwnerEntity=body.RefPlayerId,EntityId=value.Entity,EntityClass=value.Class};
        var hold=new Job{Record=value,Original=original,Accepted=accepted,Chunk=activeWorld.GetChunkSync(value.X,value.Z) as Chunk,ColdReadOnly=true};
        if(!RepairTransitionCurrent(hold))return false;string key=JobKey(value.Owner,value.Operation);
        if(jobs.TryGetValue(key,out var previous)){previous.Dispose();jobs[key]=hold;}else{if(jobs.Count>=MaximumPending)return false;jobs.Add(key,hold);}return true;
    }
    private static bool TryHoldUnknownState0(RebirthLogisticsTransferJournal.Record original,EntityBackpack body)
    {
        if(!TryRead(original,out var value)||value.State!=0||value.Invalidated||value.Entity!=body.entityId||!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original))return false;
        var detached=Decode(value.Bag);
        var accepted=new RebirthLogisticsDeathCustody.Accepted{World=activeWorld,Backpack=body,Owner=value.Owner,Creation=value.Creation,Operation=value.Operation,OriginalDeaths=value.Deaths,OriginalOwnerEntity=value.OwnerEntity,LiveOwnerEntity=value.NativeOwnerEntity,EntityId=value.Entity,EntityClass=value.Class,
            NativeBag=ItemStack.Clone(detached.ItemGrid.items),NativeLocks=Enumerable.Range(0,detached.SlotCount).Select(slot=>detached.ItemGrid.IsLocked(slot)).ToArray(),PacketBagHash=value.PacketBag,PacketEntityHash=value.PacketEntity,PacketClass=value.Class,PacketPosition=value.PacketPosition};
        var hold=new Job{ColdReadOnly=true,ColdReobserve=true,Record=value,Original=original,Accepted=accepted,Chunk=activeWorld.GetChunkSync(value.X,value.Z) as Chunk};if(!Current(hold))return false;
        string key=JobKey(value.Owner,value.Operation);
        if(jobs.TryGetValue(key,out var old)){if(Current(old))return ReferenceEquals(old.Accepted.Backpack,body);old.Dispose();jobs[key]=hold;}
        else{if(jobs.Count>=MaximumPending)return false;jobs.Add(key,hold);}return true;
    }
    internal static bool Holds(EntityBackpack body)
    {
        try
        {
            WorldCurrent();if(body?.world?.IsRemote()!=false||!ReferenceEquals(body.world,activeWorld)||!body.addedToChunk)return false;
            foreach(var job in jobs.Values)if(ReferenceEquals(job.Accepted.Backpack,body)&&(Current(job)||RepairTransitionCurrent(job)))return true;
            var chunk=activeWorld.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;
            foreach(var key in chunk.ChunkCustomData.dict.Keys.Where(key=>key.StartsWith(Prefix,StringComparison.Ordinal)).Take(MaximumPending+1).ToArray())
            {
                string[] parts=key.Substring(Prefix.Length).Split(':');if(parts.Length!=2||!Hex(parts[0],64)||!Guid.TryParseExact(parts[1],"N",out var operation)||!RebirthLogisticsTransferJournal.TryRead(parts[0],operation,out var original))continue;
                if(original.Phase==4){Retire(original);continue;}
                if(TryAdopt(original,out var accepted)&&ReferenceEquals(accepted.Backpack,body)||TryHoldUnknownState0(original,body)||TryHoldRepairTransition(original,body))return true;
            }
        }
        catch{}return false;
    }
    internal static bool HasObservedOrUnknownBody(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            string path=PathFor(original.Owner,original.Operation),archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies","archive",original.Owner,original.Operation.ToString("N")+".xml");
            return File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak")||File.Exists(archive)||jobs.ContainsKey(JobKey(original.Owner,original.Operation));
        }
        catch{return true;}
    }
    internal static bool TryTerminalNoEffectPublication(RebirthLogisticsTransferJournal.Record original,out string proof)
    {
        proof=null;
        try
        {
            if(original?.Phase!=4||original.Outcome!=3||original.OwnerEscrowRecovery||original.OwnerNoEffectProof?.Length!=64||original.Legs.Any(x=>x.Attempted!=0||x.Published!=0||x.Restored)||original.Npcs.Any(x=>x.Attempted!=0||x.Published!=0)||
                !TryRead(original,out var body)||body.State!=1||body.Invalidated||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.Physical?.Stage!=5||!RebirthLogisticsNativeDeathReceipt.PhysicalPacketMatches(original,fatal,body.OwnerEntity,body.PacketBag,body.PacketEntity)||fatal.Physical.Requests!=1||fatal.Physical.BodyByteRefused||fatal.Physical.BodyClass!=body.Class||!fatal.Physical.BodyPosition.Equals(body.PacketPosition)||
                !RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(original,out var owed)||owed.Any(x=>!x.IsEmpty())||!RebirthLogisticsNativeDeathReceipt.ConservedPhysical(original,fatal.Phase,Decode(body.Bag).ItemGrid.items,fatal.RemainingBag,fatal.RemainingBelt))return false;
            proof=body.Proof;return proof?.Length==64;
        }
        catch{return false;}
    }
    internal static bool HasPublished(RebirthLogisticsTransferJournal.Record original)
        =>TryRead(original,out var value)&&value.State==1&&!value.Invalidated;
    internal static bool AllowsKnownBody(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            string path=PathFor(original.Owner,original.Operation),archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies","archive",original.Owner,original.Operation.ToString("N")+".xml");
            if(!File.Exists(path)&&!File.Exists(path+".tmp")&&!File.Exists(path+".bak")&&!File.Exists(archive))return true; // Legacy absence only.
            return TryRead(original,out var value)&&!value.Invalidated;
        }
        catch{return false;}
    }
    internal static bool Blocks(ItemStackGrid grid)
    {try{WorldCurrent();foreach(var job in jobs.Values)if(ReferenceEquals(job.Accepted.Backpack.bag?.ItemGrid,grid)&&(Current(job)||RepairTransitionCurrent(job)))return true;}catch{}return false;}
    internal static void Invalidate(EntityBackpack body)
    {
        try
        {
            WorldCurrent();foreach(var pair in jobs.Where(pair=>ReferenceEquals(pair.Value.Accepted.Backpack,body)).ToArray())
            {
                var job=pair.Value;var value=job.Record;
                if(job.Chunk!=null&&!job.Chunk.InProgressUnloading&&ReferenceEquals(activeWorld.GetChunkSync(value.X,value.Z),job.Chunk))
                {
                    if(job.Chunk.ChunkCustomData.dict.TryGetValue(Key(value),out var marker)&&marker.TriggerWriteDataDelegate==null&&marker.data!=null&&(marker.data.SequenceEqual(Marker(value))||(value.Repairs.LastOrDefault()?.Stage==2||value.Repairs.LastOrDefault()?.Stage==4)&&marker.data.SequenceEqual(Marker(value,value.Repairs.Count-1))))
                    {job.Chunk.ChunkCustomData.Remove(Key(value));job.Chunk.isModified=true;}
                    if(RebirthLogisticsDeathJournal.TryRead(job.Original,out var main)&&main.EntityId==value.Entity)
                    {
                        string key=RebirthLogisticsDeathJournal.MarkerKey(main);
                        if(job.Chunk.ChunkCustomData.dict.TryGetValue(key,out var own)&&own.data!=null&&
                            (own.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(main,false))||main.After!=null&&own.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(main,true))))
                        {job.Chunk.ChunkCustomData.Remove(key);job.Chunk.isModified=true;}
                    }
                }
                string path=PathFor(value.Owner,value.Operation);
                if(TryRead(job.Original,out var retained)&&!retained.Invalidated&&!File.Exists(path+".tmp"))
                {
                    retained.Invalidated=true;var bytes=Encoding.UTF8.GetBytes(Write(retained).ToString(SaveOptions.DisableFormatting));
                    using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Replace(path+".tmp",path,null);
                }
                job.Dispose();jobs.Remove(pair.Key);
            }
        }
        catch{/* Unknown invalidation temp and revoked native marker cannot authorize cold adoption. */}
    }
    internal static void Retire(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(original.Phase!=4||!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)||saved.Phase!=4||RebirthLogisticsTransferJournal.Encode(saved)!=RebirthLogisticsTransferJournal.Encode(original)||!TryRead(original,out var record))return;
            bool noEffect=original.Outcome==3&&!original.OwnerEscrowRecovery&&original.OwnerNoEffectProof?.Length==64&&
                original.Legs.All(leg=>leg.Attempted==0&&leg.Published==0&&!leg.Restored)&&original.Npcs.All(leg=>leg.Attempted==0&&leg.Published==0);
            bool completed=RebirthLogisticsDeathJournal.TryRead(original,out var settled)&&settled.EntityId==record.Entity&&settled.OwnerFileProof!=null&&settled.State==4&&settled.FinalProof?.Length==64;
            if(!completed&&!noEffect)return;
            var liveBody=GameManager.Instance?.World?.GetEntity(record.Entity) as EntityBackpack;
            Guid readiness=RebirthLogisticsDeathJournal.TryRead(original,out var metadata)&&metadata.EntityId==record.Entity&&metadata.EntityClass==record.Class&&metadata.BeforeProof?.Length==64?metadata.Incarnation:record.Incarnation;
            if(liveBody==null&&!RebirthLogisticsParcelReferenceJournal.WasPositivelyRemoved(original,readiness))return;
            if(liveBody!=null&&!RebirthLogisticsLoadedBodyNotice.CanRetire(original,readiness,liveBody))return;
            if(liveBody!=null)RebirthLogisticsLoadedBodyNoticeJournal.ArchiveAcknowledgedIncarnation(original,readiness,liveBody);
            if(noEffect&&!RebirthLogisticsParcelReferenceJournal.ReadyForNoEffect(original))return;
            // A positively saved original NoEffect terminal retires metadata
            // only. State0 remains unknown State0 in the retained archive; it
            // never becomes native body proof or an item/refund permission.
            var world=GameManager.Instance?.World;var chunk=world?.GetChunkSync(record.X,record.Z) as Chunk;
            if(chunk!=null&&!chunk.InProgressUnloading&&chunk.ChunkCustomData.dict.TryGetValue(Key(record),out var old)&&old.TriggerWriteDataDelegate==null&&old.expiresInWorldTime==ulong.MaxValue&&!old.isSavedToNetwork&&old.data!=null&&old.data.SequenceEqual(Marker(record)))
            {chunk.ChunkCustomData.Remove(Key(record));chunk.isModified=true;}
            string key=JobKey(original.Owner,original.Operation);notices.Remove(key);nextRepairs.Remove(key);if(jobs.TryGetValue(key,out var job))
            {job.Dispose();jobs.Remove(key);}
            string path=PathFor(original.Owner,original.Operation),archive=Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsAcceptedBodies","archive",original.Owner);Directory.CreateDirectory(archive);
            string destination=Path.Combine(archive,original.Operation.ToString("N")+".xml");
            if(File.Exists(destination)){if(!XNode.DeepEquals(Write(record),Write(ReadFile(destination))))return;return;}
            if(File.Exists(path))File.Move(path,destination);
        }
        catch{/* Exact terminal native settlement only; never erase uncertain receipt. */}
    }
}
