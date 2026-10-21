using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;

// Placement's own accepted body lineage and positive native region publication.
// No logistics receipt, journal, lease or body adoption authority is consumed.
internal static class RebirthPlacedOriginalBodyPublication
{
    private sealed class Entry
    {
        internal Guid Operation;internal string World,Owner,Creation,Binding,Request,Initial,Proof=string.Empty;internal int Deaths,NativeOwner,Entity,Class;internal bool Ambiguous;
        internal EntityBackpack Body;internal Chunk Chunk;internal RebirthStationSnapshotEvidence.Request Watch;internal byte[] Serialized;internal float Started;internal Func<bool> Provider;internal uint Block;internal Vector3i Witness;
    }
    private static readonly Dictionary<Guid,Entry> active=new Dictionary<Guid,Entry>();private static World scope;private static string directory;private static bool installed;
    private static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    private static string Image(Entry e){var xml=XElement.Parse(BareImage(e));xml.SetAttributeValue("checksum",Hash(Encoding.UTF8.GetBytes(BareImage(e))));return xml.ToString(SaveOptions.DisableFormatting);}
    private static string BareImage(Entry e)=>new XElement("placedBodyPublication",new XAttribute("version",1),new XAttribute("operation",e.Operation.ToString("N")),new XAttribute("world",e.World),new XAttribute("owner",e.Owner),new XAttribute("creation",e.Creation),new XAttribute("binding",e.Binding),new XAttribute("deaths",e.Deaths),new XAttribute("nativeOwner",e.NativeOwner),new XAttribute("entity",e.Entity),new XAttribute("class",e.Class),new XAttribute("ambiguous",e.Ambiguous),new XAttribute("proof",e.Proof),new XElement("request",e.Request),new XElement("initial",e.Initial)).ToString(SaveOptions.DisableFormatting);
    private static string PathFor(Guid op)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","native-bodies",op.ToString("N")+".xml");
    private static EntityCreationData Ecd(string image)
    {var bytes=Convert.FromBase64String(image);if(bytes.Length>131072)throw new InvalidDataException();using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(stream);var e=new EntityCreationData();e.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||e.entityData==null||e.entityData.Length>ushort.MaxValue||Frame(e)!=image)return throwInvalid();return e;}}
    private static EntityCreationData throwInvalid(){throw new InvalidDataException();}
    private static string Frame(EntityCreationData e){using(var blob=StreamUtils.ToBlob(writer=>e.write(writer,StreamModeWrite.Persistency))){if(blob.Length>131072)throw new InvalidDataException();return Convert.ToBase64String(blob.ToArray());}}
    private static int NativeOwner(EntityCreationData e){var body=e.entityData?.ToArray();var bag=e.bagData?.ToArray();if(body==null||bag==null||body.Length<bag.Length+4||!body.Skip(body.Length-bag.Length).SequenceEqual(bag))throw new InvalidDataException();return BitConverter.ToInt32(body,body.Length-bag.Length-4);}
    private static bool Hex(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static bool Read(Guid op,out Entry e){e=null;try{string path=PathFor(op);return File.Exists(path)&&new FileInfo(path).Length<=512*1024&&Decode(op,File.ReadAllText(path,new UTF8Encoding(false,true)),out e);}catch{return false;}}
    private static bool Decode(Guid op,string text,out Entry e)
    {
        e=null;try{if(text==null||text.Length>512*1024)return false;XElement x;using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=512*1024}))x=XElement.Load(reader);if(x.Name!="placedBodyPublication"||(int?)x.Attribute("version")!=1)return false;
            var r=new Entry{Operation=Guid.ParseExact((string)x.Attribute("operation"),"N"),World=(string)x.Attribute("world"),Owner=(string)x.Attribute("owner"),Creation=(string)x.Attribute("creation"),Binding=(string)x.Attribute("binding"),Deaths=(int)x.Attribute("deaths"),NativeOwner=(int)x.Attribute("nativeOwner"),Entity=(int)x.Attribute("entity"),Class=(int)x.Attribute("class"),Ambiguous=(bool)x.Attribute("ambiguous"),Proof=(string)x.Attribute("proof"),Request=(string)x.Element("request"),Initial=(string)x.Element("initial")};var native=Ecd(r.Initial);var request=Ecd(r.Request);
            if(r.Operation!=op||r.Operation==Guid.Empty||!Guid.TryParse(r.World,out _)||!RebirthOriginalActionScope.Validate(r.Owner,r.Creation)||!Hex(r.Binding)||r.Deaths<0||r.NativeOwner<=0||NativeOwner(native)!=r.NativeOwner||NativeOwner(request)!=r.NativeOwner||r.Entity<=0||native.id!=r.Entity||native.entityClass!=r.Class||request.id!=-1||request.entityClass!=r.Class||r.Proof==null||r.Proof.Length!=0&&!Hex(r.Proof)||Image(r)!=text||!native.bagData.ToArray().SequenceEqual(request.bagData.ToArray()))return false;e=r;return true;
        }catch{return false;}
    }
    private static bool Save(Entry e,string expected=null)
    {
        try{string path=PathFor(e.Operation),image=Image(e);if(!Decode(e.Operation,image,out _))return false;if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(e.Operation,out var old)||expected==null&&Image(old)!=image||expected!=null&&Image(old)!=expected)return false;}else if(expected!=null)return false;Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),e.Operation.ToString("N")+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>512*1024||!Decode(e.Operation,File.ReadAllText(pending[0],new UTF8Encoding(false,true)),out var retained)||Image(retained)!=image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _)&&Read(e.Operation,out var recovered)&&Image(recovered)==image;}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(!Decode(e.Operation,File.ReadAllText(temp,new UTF8Encoding(false,true)),out var prepared)||Image(prepared)!=image)return false;return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&Read(e.Operation,out var saved)&&Image(saved)==image;}catch{return false;}
    }
    private static bool SameBody(Entry a,Entry b)=>a.Operation==b.Operation&&a.World==b.World&&a.Owner==b.Owner&&a.Creation==b.Creation&&a.Binding==b.Binding&&a.Request==b.Request&&a.Initial==b.Initial&&a.Deaths==b.Deaths&&a.NativeOwner==b.NativeOwner&&a.Entity==b.Entity&&a.Class==b.Class;
    private static bool RecoverPrepared(Entry e)
    {
        try
        {
            string path=PathFor(e.Operation),folder=Path.GetDirectoryName(path);if(!Directory.Exists(folder))return true;var files=Directory.EnumerateFiles(folder,e.Operation.ToString("N")+".*.tmp").ToArray();if(files.Length==0)return true;if(files.Length!=1||new FileInfo(files[0]).Length>512*1024||!Decode(e.Operation,File.ReadAllText(files[0],new UTF8Encoding(false,true)),out var prepared)||!SameBody(e,prepared))return false;
            if(File.Exists(path)||File.Exists(path+".bak")){if(!Read(e.Operation,out var old)||!SameBody(old,prepared)||old.Ambiguous&&!prepared.Ambiguous||old.Proof.Length!=0&&old.Proof!=prepared.Proof)return false;}
            return RebirthDurableFileCommit.TryPublish(files[0],path,out _)&&Read(e.Operation,out var saved)&&Image(saved)==Image(prepared);
        }catch{return false;}
    }
    private static string Key(Guid op)=>"rebirth.placed.body.v1:"+op.ToString("N");
    private static byte[] Marker(Entry e)=>Encoding.UTF8.GetBytes(e.Operation.ToString("N")+"|"+e.World+"|"+e.Owner+"|"+e.Creation+"|"+e.Binding+"|"+e.Deaths+"|"+e.NativeOwner+"|"+e.Entity+"|"+e.Class+"|"+Hash(Convert.FromBase64String(e.Request))+"|"+Hash(Convert.FromBase64String(e.Initial)));
    private static void Scope(World world){if(ReferenceEquals(scope,world)&&directory==GameIO.GetSaveGameDir())return;foreach(var e in active.Values)e.Watch?.Dispose();active.Clear();scope=world;directory=GameIO.GetSaveGameDir();}
    internal static void Admit(World world,RebirthPlacedOriginal.Record original,int nativeOwner,EntityBackpack body,string request)
    {
        try
        {
            Scope(world);if(world?.IsRemote()!=false||body==null||!ReferenceEquals(world.GetEntity(body.entityId),body)||body.RefPlayerId!=nativeOwner||body.bag?.ItemGrid?.items==null)return;var input=Ecd(request);var initial=new EntityCreationData(body);if(!input.bagData.ToArray().SequenceEqual(initial.bagData.ToArray()))return;
            if(Read(original.Operation,out var existing)){if(existing.Entity!=body.entityId||existing.Request!=request||existing.World!=original.World){string old=Image(existing);existing.Ambiguous=true;Save(existing,old);if(active.TryGetValue(original.Operation,out var known))known.Ambiguous=true;}return;}
            var e=new Entry{Operation=original.Operation,World=original.World,Owner=original.Owner,Creation=original.Creation,Binding=RebirthPlacedOriginal.Digest(original),Deaths=original.Deaths,NativeOwner=nativeOwner,Entity=body.entityId,Class=initial.entityClass,Request=request,Initial=Frame(initial),Body=body};Save(e);
            foreach(var prior in active.Values.Where(item=>item.Proof.Length!=0).ToArray()){prior.Watch?.Dispose();active.Remove(prior.Operation);}if(active.Count<128)active[e.Operation]=e;
            Stamp(world,e,body); // Failed intent publication retains this exact accepted body.
        }catch{}
    }
    internal static bool AdmitOwed(World world,Guid operation,EntityBackpack body,string request)
    {
        try
        {
            Scope(world);if(!RebirthPlacedOriginalDeathPartition.TryDescribeOwedBody(world,operation,request,out var own)||body==null||!ReferenceEquals(world.GetEntity(body.entityId),body)||body.RefPlayerId!=own.NativeOwner||body.bag?.ItemGrid?.items==null)return false;var input=Ecd(request);var initial=new EntityCreationData(body);if(input.entityClass!="Backpack".GetHashCode()||initial.entityClass!=input.entityClass||!input.bagData.ToArray().SequenceEqual(initial.bagData.ToArray()))return false;
            if(Read(operation,out var existing)){if(existing.Entity!=body.entityId||existing.Request!=request||existing.World!=own.World||existing.Binding!=own.Binding){string previous=Image(existing);existing.Ambiguous=true;Save(existing,previous);if(active.TryGetValue(operation,out var known))known.Ambiguous=true;return false;}return !existing.Ambiguous;}
            var e=new Entry{Operation=own.Operation,World=own.World,Owner=own.Owner,Creation=own.Creation,Binding=own.Binding,Deaths=own.Deaths,NativeOwner=own.NativeOwner,Entity=body.entityId,Class=initial.entityClass,Request=request,Initial=Frame(initial),Body=body};Save(e);foreach(var prior in active.Values.Where(item=>item.Proof.Length!=0).ToArray()){prior.Watch?.Dispose();active.Remove(prior.Operation);}if(active.Count<128)active[e.Operation]=e;Stamp(world,e,body);return true;
        }catch{return false;}
    }
    internal static bool AdmitTerminal(World world,Guid operation,EntityBackpack body,string request)
    {
        try
        {
            Scope(world);if(!RebirthPlacedOriginalDeathPartition.TryDescribeTerminalNativeBody(world,operation,request,out var own)||body==null||!ReferenceEquals(world.GetEntity(body.entityId),body)||body.RefPlayerId!=own.NativeOwner||body.bag?.ItemGrid?.items==null)return false;var input=Ecd(request);var initial=new EntityCreationData(body);if(input.entityClass!="Backpack".GetHashCode()||initial.entityClass!=input.entityClass||!input.bagData.ToArray().SequenceEqual(initial.bagData.ToArray()))return false;
            if(Read(operation,out var existing)){if(existing.Entity!=body.entityId||existing.Request!=request||existing.World!=own.World||existing.Binding!=own.Binding){string previous=Image(existing);existing.Ambiguous=true;Save(existing,previous);if(active.TryGetValue(operation,out var known))known.Ambiguous=true;return false;}return !existing.Ambiguous;}
            var e=new Entry{Operation=own.Operation,World=own.World,Owner=own.Owner,Creation=own.Creation,Binding=own.Binding,Deaths=own.Deaths,NativeOwner=own.NativeOwner,Entity=body.entityId,Class=initial.entityClass,Request=request,Initial=Frame(initial),Body=body};Save(e);foreach(var prior in active.Values.Where(item=>item.Proof.Length!=0).ToArray()){prior.Watch?.Dispose();active.Remove(prior.Operation);}if(active.Count<128)active[e.Operation]=e;Stamp(world,e,body);return true;
        }catch{return false;}
    }
    private static bool Stamp(World world,Entry e,EntityBackpack body)
    {
        if(!body.addedToChunk)return false;var chunk=world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading)return false;byte[] bytes=Marker(e);string key=Key(e.Operation);if(chunk.ChunkCustomData.dict.TryGetValue(key,out var old))return old?.data!=null&&old.expiresInWorldTime==ulong.MaxValue&&!old.isSavedToNetwork&&old.TriggerWriteDataDelegate==null&&old.data.SequenceEqual(bytes);if(chunk.ChunkCustomData.dict.Count>=ushort.MaxValue)return false;chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=bytes});chunk.isModified=true;return true;
    }
    private static byte[] LiveBag(EntityBackpack body){using(var blob=StreamUtils.ToBlob(writer=>body.bag.Write(writer,StreamModeWrite.ToClient)))return blob.ToArray();}
    private static bool Current(Entry e)
    {
        if(e.Ambiguous||e.Proof.Length!=0||e.Body==null||scope?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,scope)||e.World!=scope.worldState.Guid||!ReferenceEquals(scope.GetEntity(e.Entity),e.Body)||e.Body.RefPlayerId!=e.NativeOwner||!e.Body.addedToChunk||e.Provider!=null&&!e.Provider())return false;
        var chunk=scope.GetChunkSync(e.Body.chunkPosAddedEntityTo.x,e.Body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null||chunk.InProgressUnloading||e.Chunk!=null&&!ReferenceEquals(chunk,e.Chunk)||!LiveBag(e.Body).SequenceEqual(Ecd(e.Initial).bagData.ToArray()))return false;
        return chunk.ChunkCustomData.dict.TryGetValue(Key(e.Operation),out var marker)&&marker?.data!=null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.TriggerWriteDataDelegate==null&&marker.data.SequenceEqual(Marker(e));
    }
    private static bool MarkerTargets(ChunkCustomData marker,Guid operation,World world,EntityBackpack body)
    {
        try{if(marker?.data==null||marker.data.Length>2048||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.TriggerWriteDataDelegate!=null)return false;var fields=new UTF8Encoding(false,true).GetString(marker.data).Split('|');return fields.Length==11&&fields[0]==operation.ToString("N")&&fields[1]==world.worldState.Guid&&RebirthOriginalActionScope.Validate(fields[2],fields[3])&&Hex(fields[4])&&int.TryParse(fields[5],out int deaths)&&deaths>=0&&int.TryParse(fields[6],out int owner)&&owner==body.RefPlayerId&&int.TryParse(fields[7],out int entity)&&entity==body.entityId&&int.TryParse(fields[8],out int type)&&type==body.entityClass&&Hex(fields[9])&&Hex(fields[10]);}catch{return false;}
    }
    private static bool ReadPreparedMarker(Guid operation,ChunkCustomData marker,World world,EntityBackpack body,out Entry e)
    {
        e=null;try{string folder=Path.GetDirectoryName(PathFor(operation));if(!Directory.Exists(folder))return false;var files=Directory.EnumerateFiles(folder,operation.ToString("N")+".*.tmp").ToArray();if(files.Length!=1||new FileInfo(files[0]).Length>512*1024||!Decode(operation,File.ReadAllText(files[0],new UTF8Encoding(false,true)),out var candidate)||candidate.World!=world.worldState.Guid||candidate.Entity!=body.entityId||candidate.Class!=body.entityClass||candidate.NativeOwner!=body.RefPlayerId||!marker.data.SequenceEqual(Marker(candidate))||!LiveBag(body).SequenceEqual(Ecd(candidate.Initial).bagData.ToArray()))return false;e=candidate;return true;}catch{return false;}
    }
    internal static bool Holds(EntityBackpack body)
    {
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||body==null||!ReferenceEquals(world.GetEntity(body.entityId),body))return false;Scope(world);
            var runtime=active.Values.FirstOrDefault(e=>ReferenceEquals(e.Body,body));if(runtime!=null)return !RebirthPlacedOriginalDeathPartition.CanReleaseBodyCustody(runtime.Proof.Length==64,runtime.Proof.Length==64&&RebirthPlacedOriginalDeathPartition.HasSavedBodySource(world,runtime.Operation,runtime.Request));
            if(!body.addedToChunk)return false;var chunk=world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;if(chunk==null)return false;
            foreach(var pair in chunk.ChunkCustomData.dict.Where(pair=>pair.Key.StartsWith("rebirth.placed.body.v1:",StringComparison.Ordinal)).Take(128))
            {
                if(!Guid.TryParseExact(pair.Key.Substring("rebirth.placed.body.v1:".Length),"N",out var op))continue;
                if(!MarkerTargets(pair.Value,op,world,body)){if(Read(op,out var corruptMarker)&&(corruptMarker.Proof.Length==0||!RebirthPlacedOriginalDeathPartition.HasSavedBodySource(world,corruptMarker.Operation,corruptMarker.Request))&&corruptMarker.World==world.worldState.Guid&&corruptMarker.Entity==body.entityId&&corruptMarker.Class==body.entityClass&&corruptMarker.NativeOwner==body.RefPlayerId)return true;continue;}
                // The exact typed native marker is custody evidence for HOLD only.
                // A corrupt/missing domain receipt never releases that body.
                if(!Read(op,out var e)){if(ReadPreparedMarker(op,pair.Value,world,body,out var prepared)){prepared.Body=body;if(active.Count<128){if(active.TryGetValue(op,out var prior))prior.Watch?.Dispose();active[op]=prepared;}}return true;}
                if(e.World!=world.worldState.Guid||e.Entity!=body.entityId||e.Class!=body.entityClass||e.NativeOwner!=body.RefPlayerId||!pair.Value.data.SequenceEqual(Marker(e)))return true;
                if(e.Proof.Length!=0){e.Body=body;e.Chunk=chunk;if(active.Count<128)active[e.Operation]=e;return !RebirthPlacedOriginalDeathPartition.CanReleaseBodyCustody(e.Proof.Length==64,RebirthPlacedOriginalDeathPartition.HasSavedBodySource(world,e.Operation,e.Request));}if(e.Ambiguous)return true;if(!LiveBag(body).SequenceEqual(Ecd(e.Initial).bagData.ToArray()))return true;e.Body=body;if(active.Count<128){if(active.TryGetValue(op,out var prior))prior.Watch?.Dispose();active[op]=e;}return true;
            }
            return false;
        }catch{return false;}
    }
    internal static bool HasAccepted(World world,RebirthPlacedOriginal.Record original,string request)
    {
        try
        {
            if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||world.worldState.Guid!=original.World)return false;Entry e=null;if(ReferenceEquals(scope,world)&&directory==GameIO.GetSaveGameDir()&&active.TryGetValue(original.Operation,out var runtime))e=runtime;else if(Read(original.Operation,out var saved))e=saved;
            else{string folder=Path.GetDirectoryName(PathFor(original.Operation));if(Directory.Exists(folder)){var files=Directory.EnumerateFiles(folder,original.Operation.ToString("N")+".*.tmp").ToArray();if(files.Length==1&&new FileInfo(files[0]).Length<=512*1024)Decode(original.Operation,File.ReadAllText(files[0],new UTF8Encoding(false,true)),out e);}}
            return e!=null&&e.Operation==original.Operation&&e.World==original.World&&e.Owner==original.Owner&&e.Creation==original.Creation&&e.Deaths==original.Deaths&&e.Binding==RebirthPlacedOriginal.Digest(original)&&e.Request==request&&e.Entity>0;
        }catch{return false;}
    }
    internal static void ObserveTrace(World world,Guid operation,string request){Scope(world);if(active.TryGetValue(operation,out var e)&&e.Request!=request&&request.Length!=0){string old=Image(e);e.Ambiguous=true;Save(e,old);}}
    internal static bool TryPublished(Guid operation,string request,out string proof)
    {proof=null;var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!Read(operation,out var e)||e.World!=world.worldState.Guid||e.Ambiguous||e.Request!=request||e.Proof.Length!=64)return false;proof=e.Proof;return true;}
    internal static void OnRemoving(World world,EntityBackpack body)
    {
        try
        {
            // Invoked only AFTER positively observed non-unload native removal.
            if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||body==null||ReferenceEquals(world.GetEntity(body.entityId),body))return;Scope(world);
            var chunk=world.GetChunkSync(body.chunkPosAddedEntityTo.x,body.chunkPosAddedEntityTo.z) as Chunk;
            if(chunk!=null)foreach(var pair in chunk.ChunkCustomData.dict.Where(pair=>pair.Key.StartsWith("rebirth.placed.body.v1:",StringComparison.Ordinal)).Take(128).ToArray())
            {
                if(!Guid.TryParseExact(pair.Key.Substring("rebirth.placed.body.v1:".Length),"N",out var operation)||!MarkerTargets(pair.Value,operation,world,body))continue;
                if(Read(operation,out var retained)&&retained.Proof.Length==0){string old=Image(retained);retained.Ambiguous=true;Save(retained,old);}
                chunk.ChunkCustomData.Remove(pair.Key);chunk.isModified=true;
            }
            foreach(var e in active.Values.Where(e=>ReferenceEquals(e.Body,body)).ToArray())
            {e.Watch?.Dispose();e.Watch=null;if(e.Proof.Length==0){string old=Image(e);e.Ambiguous=true;Save(e,old);}active.Remove(e.Operation);}
        }catch{}
    }
    internal static void Tick(World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false)return;Scope(world);foreach(var old in active.Values.Where(value=>value.Proof.Length!=0&&RebirthPlacedOriginalDeathPartition.HasSavedBodySource(world,value.Operation,value.Request)||value.Ambiguous||value.Body==null||!ReferenceEquals(world.GetEntity(value.Entity),value.Body)).ToArray()){if(old.Proof.Length!=0)RetireMarker(old);old.Watch?.Dispose();active.Remove(old.Operation);}foreach(var e in active.Values.Take(128).ToArray())
        {
            try
            {
                if(e.Body==null)continue;if(!RecoverPrepared(e))continue;if(Read(e.Operation,out var persisted)&&SameBody(e,persisted)){e.Ambiguous=persisted.Ambiguous;if(persisted.Proof.Length!=0){e.Proof=persisted.Proof;e.Watch?.Dispose();e.Watch=null;RetireMarker(e);continue;}}else if(!Save(e))continue;if(e.Ambiguous||e.Proof.Length!=0)continue;if(!Stamp(world,e,e.Body))continue;
                if(e.Watch!=null)
                {
                    if(RebirthStationSnapshotEvidence.HasPublished(e.Watch)&&RebirthStationSnapshotEvidence.TryCopy(e.Watch,out var bytes)&&e.Serialized!=null&&RebirthLogisticsEntity47.Contains(bytes,e.Chunk.X,e.Chunk.Z,e.Entity,e.Class,Ecd(e.Initial).bagData.ToArray(),e.Serialized)&&RebirthPlacedOriginalPublication.TrySnapshot(bytes,e.Chunk.X,e.Chunk.Z,e.Witness,e.Block,Key(e.Operation),Marker(e),out var digest))
                    {string old=Image(e);e.Proof=digest;if(!Save(e,old)){e.Proof=string.Empty;continue;}e.Watch.Dispose();e.Watch=null;RetireMarker(e);continue;}
                    if(UnityEngine.Time.realtimeSinceStartup-e.Started>15f||!Current(e)){e.Watch.Dispose();e.Watch=null;e.Chunk=null;e.Provider=null;e.Serialized=null;}else continue;
                }
                if(!Current(e)||active.Values.Count(item=>item.Watch!=null)>=4)continue;var chunk=world.GetChunkSync(e.Body.chunkPosAddedEntityTo.x,e.Body.chunkPosAddedEntityTo.z) as Chunk;var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))continue;var access=regions.regionFileAccess;string path=Path.GetFullPath(regions.saveDirectory);e.Provider=()=>ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==path;e.Chunk=chunk;e.Witness=new Vector3i(chunk.X*16,0,chunk.Z*16);e.Block=world.GetBlock(e.Witness).rawData;e.Serialized=null;e.Watch=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,()=>Current(e),e.Body);if(e.Watch==null){e.Chunk=null;e.Provider=null;continue;}e.Started=UnityEngine.Time.realtimeSinceStartup;
                if(!System.Threading.Monitor.TryEnter(regions.saveLock,0)){e.Watch.Dispose();e.Watch=null;continue;}try{if(!RebirthLogisticsParcelBaselines.CanAdmitNativeSnapshot(regions)){e.Watch.Dispose();e.Watch=null;continue;}lock(chunk){if(!Current(e)||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){e.Watch.Dispose();e.Watch=null;continue;}chunk.InProgressSaving=true;}try{regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}}finally{System.Threading.Monitor.Exit(regions.saveLock);}
            }
            catch{e.Watch?.Dispose();e.Watch=null;e.Chunk=null;e.Provider=null;e.Serialized=null;}
        }
    }
    private static void RetireMarker(Entry e){var chunk=e.Chunk;if(e.Proof.Length!=64||chunk==null||!RebirthPlacedOriginalDeathPartition.HasSavedBodySource(scope,e.Operation,e.Request))return;string key=Key(e.Operation);if(chunk.ChunkCustomData.dict.TryGetValue(key,out var marker)&&marker?.data!=null&&marker.data.SequenceEqual(Marker(e))){chunk.ChunkCustomData.Remove(key);chunk.isModified=true;}}
    internal static void Install(Harmony harmony){if(installed)return;RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Serialized));installed=true;}
    [HarmonyPatch(typeof(EntityBackpack),nameof(EntityBackpack.Write))]
    private static class Serialized
    {
        private static void Prefix(EntityBackpack __instance,PooledBinaryWriter _bw,out long __state)=>__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);
        private static void Postfix(EntityBackpack __instance,PooledBinaryWriter _bw,long __state)
        {if(__state<0)return;try{foreach(var e in active.Values)if(ReferenceEquals(e.Body,__instance)&&e.Watch!=null&&Current(e)){var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;if(length<1||length>ushort.MaxValue||!stream.CanRead||!stream.CanSeek)return;var bytes=new byte[(int)length];try{stream.Position=__state;int total=0;while(total<bytes.Length){int count=stream.Read(bytes,total,bytes.Length-total);if(count<=0)return;total+=count;}}finally{stream.Position=end;}e.Serialized=bytes;}}catch{}}
    }
}
