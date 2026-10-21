using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Noemax.GZip;

// Detached target-v47 capacity calculation only. No world access, save request,
// entity creation, mutation permission, or publication proof is granted here.
internal static class RebirthLogisticsParcelPreview
{
    internal const int MaximumRawBytes=RebirthLogisticsSnapshotReservation.MaximumNativeBytes;
    internal const int MaximumCompressedBytes=255*4096-16;
    private sealed class RegionOutput:MemoryStream
    {
        // Restart is measured at the native boundary. Permit a bounded empty
        // compressor-dispose tail afterwards without growing the proof buffer.
        private const int Limit=MaximumCompressedBytes+65536;
        internal RegionOutput():base(Limit){}
        public override void Write(byte[] buffer,int offset,int count){if(count<0||Position>Limit-count)throw new IOException("Native region preview bound exceeded.");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){if(Position>=Limit)throw new IOException("Native region preview bound exceeded.");base.WriteByte(value);}
        public override void SetLength(long value){if(value<0||value>Limit)throw new IOException("Native region preview bound exceeded.");base.SetLength(value);}
    }
    internal sealed class Edit
    {
        internal bool MarkerOnly,EntityOnly;
        internal Guid Operation;
        internal int EntityId,EntityClass;
        // Null Before means an addition, never an inferred missing original.
        internal byte[] BeforeEntity,AfterEntity,BeforeMarker,AfterMarker;
        internal byte[] EmptyEntity,EmptyMarker;
        internal string Key=>"RebirthLogisticsBackpackV1:"+EntityId+":"+Operation.ToString("N");
    }
    private sealed class Span
    {internal int Start,Length;internal string Key;internal int Id,Class,Bucket;}
    private sealed class Addition{internal byte[] Bytes;internal int Bucket;}
    private sealed class Layout
    {internal int CustomCount,CustomEnd,EntityCount,EntityEnd;internal readonly List<Span> Custom=new List<Span>(),Entities=new List<Span>();}

    // Frozen native execution order: spawn all empty bodies, fill each parcel,
    // then fill the accepted original. Callers may amortize one detached stage
    // per tick. Checking only the final compressed image is insufficient.
    internal static bool TryBuildStage(byte[] baseline,int x,int z,Edit root,Edit[] parcels,int stage,out byte[] candidate,out int compressedBytes)
    {
        candidate=null;compressedBytes=0;
        if(root!=null&&(root.BeforeEntity==null||root.BeforeMarker==null)||parcels==null||parcels.Length>169||root==null&&parcels.Length==0||stage<0||stage>2*parcels.Length+(root==null?0:1))return false;
        if(stage==0)
        {
            if(!TryLayout(baseline,x,z,out _)||!TryCompressedLength(baseline,out compressedBytes))return false;
            candidate=(byte[])baseline.Clone();return true;
        }
        var edits=new List<Edit>();bool final=root!=null&&stage==2*parcels.Length+1;
        Guid operation=root?.Operation??parcels[0]?.Operation??Guid.Empty;if(operation==Guid.Empty)return false;
        if(root!=null)edits.Add(new Edit{Operation=root.Operation,EntityId=root.EntityId,EntityClass=root.EntityClass,BeforeEntity=root.BeforeEntity,BeforeMarker=root.BeforeMarker,
            AfterEntity=final?root.AfterEntity:root.BeforeEntity,AfterMarker=final?root.AfterMarker:root.BeforeMarker});
        int spawned=Math.Min(stage,parcels.Length),filled=Math.Max(0,stage-parcels.Length);
        for(int i=0;i<spawned;i++)
        {
            var parcel=parcels[i];if(parcel==null||parcel.BeforeEntity!=null||parcel.BeforeMarker!=null||parcel.Operation!=operation||
                !ValidEntity(parcel.EmptyEntity,parcel.EntityId,parcel.EntityClass)||!ValidMarker(parcel.EmptyMarker,parcel.Key))return false;
            bool complete=i<filled;
            edits.Add(new Edit{Operation=parcel.Operation,EntityId=parcel.EntityId,EntityClass=parcel.EntityClass,
                AfterEntity=complete?parcel.AfterEntity:parcel.EmptyEntity,AfterMarker=complete?parcel.AfterMarker:parcel.EmptyMarker});
        }
        return TryBuild(baseline,x,z,edits.ToArray(),out candidate,out compressedBytes);
    }

    internal static bool TryReadEntity(byte[] baseline,int x,int z,int id,int entityClass,out byte[] frame)
    {
        frame=null;if(id<0||!TryLayout(baseline,x,z,out var layout))return false;
        var entity=layout.Entities.SingleOrDefault(span=>span.Id==id);if(entity==null||entity.Class!=entityClass||entity.Length>RebirthLogisticsNativeBodyPreflight.MaximumFrame)return false;
        frame=new byte[entity.Length];Buffer.BlockCopy(baseline,entity.Start,frame,0,frame.Length);
        if(ValidEntity(frame,id,entityClass))return true;frame=null;return false;
    }
    internal static bool TryReadOriginal(byte[] baseline,int x,int z,Guid operation,int id,int entityClass,out byte[] frame,out byte[] marker)
    {
        frame=null;marker=null;if(operation==Guid.Empty||id<0||!TryLayout(baseline,x,z,out var layout))return false;
        var entity=layout.Entities.SingleOrDefault(span=>span.Id==id);string key="RebirthLogisticsBackpackV1:"+id+":"+operation.ToString("N");
        var custom=layout.Custom.SingleOrDefault(span=>span.Key==key);if(entity==null||custom==null||entity.Class!=entityClass||entity.Length>RebirthLogisticsNativeBodyPreflight.MaximumFrame||custom.Length>8192)return false;
        frame=new byte[entity.Length];Buffer.BlockCopy(baseline,entity.Start,frame,0,frame.Length);
        marker=new byte[custom.Length];Buffer.BlockCopy(baseline,custom.Start,marker,0,marker.Length);
        if(ValidEntity(frame,id,entityClass)&&ValidMarker(marker,key))return true;frame=null;marker=null;return false;
    }
    internal static bool TryReadMarker(byte[] baseline,int x,int z,string key,out byte[] marker)
    {
        marker=null;if(!TryLayout(baseline,x,z,out var layout))return false;
        var span=layout.Custom.SingleOrDefault(value=>value.Key==key);if(span==null||span.Length>8192)return false;
        marker=new byte[span.Length];Buffer.BlockCopy(baseline,span.Start,marker,0,marker.Length);return ValidMarker(marker,key);
    }
    internal static byte[] MarkerFrame(string key,byte[] data)
    {
        if(key==null||key.Length>256||data==null||data.Length<1||data.Length>4096)return null;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(key);writer.Write(ulong.MaxValue);writer.Write(false);writer.Write((ushort)data.Length);writer.Write(data);writer.Flush();return stream.ToArray();}
    }

    // Every replacement compares the complete original native span. Unrelated
    // custom data, ECDs, tile entities and the opaque suffix remain byte exact.
    internal static bool TryBuild(byte[] baseline,int x,int z,Edit[] edits,out byte[] candidate,out int compressedBytes)
    {
        candidate=null;compressedBytes=0;
        if(edits==null||edits.Length<1||edits.Length>170||!TryLayout(baseline,x,z,out var layout))return false;
        try
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);var ids=new HashSet<int>();
            var custom=new Dictionary<int,byte[]>();var entities=new Dictionary<int,byte[]>();
            var extraCustom=new List<byte[]>();var extraEntities=new List<Addition>();
            foreach(var edit in edits)
            {
                if(edit==null||edit.Operation==Guid.Empty||edit.EntityId<0||!keys.Add(edit.Key)||!ids.Add(edit.EntityId)||edit.MarkerOnly&&edit.EntityOnly)return false;
                int bucket=0;
                if(edit.MarkerOnly?(edit.BeforeEntity!=null||edit.AfterEntity!=null):!TryEntity(edit.AfterEntity,edit.EntityId,edit.EntityClass,out bucket))return false;
                if(edit.EntityOnly?(edit.BeforeMarker!=null||edit.AfterMarker!=null):!ValidMarker(edit.AfterMarker,edit.Key))return false;
                var oldEntity=layout.Entities.SingleOrDefault(s=>s.Id==edit.EntityId);
                var oldMarker=layout.Custom.SingleOrDefault(s=>s.Key==edit.Key);
                if(edit.BeforeEntity==null)
                {
                    if(oldEntity!=null)return false;
                    if(edit.BeforeMarker==null){if(oldMarker!=null)return false;if(!edit.EntityOnly)extraCustom.Add(edit.AfterMarker);}
                    else{if(oldMarker==null||!Equal(baseline,oldMarker,edit.BeforeMarker))return false;custom.Add(oldMarker.Start,edit.AfterMarker);}
                    if(!edit.MarkerOnly)extraEntities.Add(new Addition{Bytes=edit.AfterEntity,Bucket=bucket});
                }
                else
                {
                    if(edit.EntityOnly||edit.MarkerOnly||oldEntity==null||oldMarker==null||oldEntity.Class!=edit.EntityClass||oldEntity.Bucket!=bucket||
                        !Equal(baseline,oldEntity,edit.BeforeEntity)||!Equal(baseline,oldMarker,edit.BeforeMarker))return false;
                    custom.Add(oldMarker.Start,edit.AfterMarker);entities.Add(oldEntity.Start,edit.AfterEntity);
                }
            }
            if(layout.Custom.Count+extraCustom.Count>ushort.MaxValue||layout.Entities.Count+extraEntities.Count>4096)return false;
            // Match this domain's actual marker admission before any root or
            // parcel spawn. Existing original markers can still be observed.
            if(extraCustom.Count>0&&layout.Custom.Count+extraCustom.Count>4096)return false;
            long size=baseline.Length;
            foreach(var span in layout.Custom){if(custom.TryGetValue(span.Start,out var replacement))size+=replacement.Length-span.Length;}
            foreach(var span in layout.Entities){if(entities.TryGetValue(span.Start,out var replacement))size+=replacement.Length-span.Length;}
            size+=extraCustom.Sum(v=>(long)v.Length)+extraEntities.Sum(v=>(long)v.Bytes.Length);
            if(size>MaximumRawBytes)return false;
            // Key strings are no longer needed after exact edit matching.
            // Drop their references before allocating the full output graph.
            foreach(var span in layout.Custom)span.Key=null;
            using(var output=new MemoryStream((int)size))using(var writer=new BinaryWriter(output,Encoding.UTF8,true))
            {
                output.Write(baseline,0,layout.CustomCount);writer.Write((ushort)(layout.Custom.Count+extraCustom.Count));
                Copy(output,baseline,layout.Custom,custom);foreach(var bytes in extraCustom)output.Write(bytes,0,bytes.Length);
                output.Write(baseline,layout.CustomEnd,layout.EntityCount-layout.CustomEnd);writer.Write(layout.Entities.Count+extraEntities.Count);
                // Chunk.write enumerates vertical entityLists[0..15]. Native
                // AddEntityToChunk appends to its clamped floor(y/16) list.
                // Appending every ECD at the end would predict different bytes
                // and potentially a different compressed sector count.
                for(int bucket=0;bucket<16;bucket++)
                {
                    Copy(output,baseline,layout.Entities.Where(span=>span.Bucket==bucket).ToList(),entities);
                    foreach(var addition in extraEntities.Where(value=>value.Bucket==bucket))output.Write(addition.Bytes,0,addition.Bytes.Length);
                }
                output.Write(baseline,layout.EntityEnd,baseline.Length-layout.EntityEnd);writer.Flush();
                if(output.Length!=size)return false;candidate=output.ToArray();
            }
            if(!TryCompressedLength(candidate,out compressedBytes)){candidate=null;return false;}return true;
        }
        catch{candidate=null;compressedBytes=0;return false;}
    }
    // Named pointer/secondary-marker preview only. No arbitrary custom-data
    // key, entity replacement, source write or save authority is accepted.
    internal static bool TryBuildReference(byte[] baseline,int x,int z,Guid operation,int id,int entityClass,int oldOwner,int newOwner,
        byte[] originalMarker,byte[] previousReference,byte[] nextReference,out byte[] candidate)
    {
        candidate=null;
        if(operation==Guid.Empty||id<0||oldOwner<0||newOwner<0||originalMarker==null||nextReference==null||!TryLayout(baseline,x,z,out var layout))return false;
        EntityCreationData native=null;
        try
        {
            string key="RebirthLogisticsBackpackV1:"+id+":"+operation.ToString("N"),referenceKey="RebirthLogisticsReferenceV1:"+id+":"+operation.ToString("N");
            var primary=layout.Custom.SingleOrDefault(span=>span.Key==key);var reference=layout.Custom.SingleOrDefault(span=>span.Key==referenceKey);var entity=layout.Entities.SingleOrDefault(span=>span.Id==id);
            if(primary==null||entity==null||entity.Class!=entityClass||!Equal(baseline,primary,originalMarker)||!ValidMarker(originalMarker,key)||!ValidMarker(nextReference,referenceKey)||
                (previousReference==null?reference!=null:reference==null||!Equal(baseline,reference,previousReference)||!ValidMarker(previousReference,referenceKey)))return false;
            using(var stream=new MemoryStream(baseline,entity.Start,entity.Length,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
            if(native.id!=id||native.entityClass!=entityClass||!EntityClass.list.TryGetValue(entityClass,out var definition)||definition.classname!=typeof(EntityBackpack)||native.entityData==null||native.bagData==null)return false;
            byte[] frame=RebirthLogisticsNativeInvocationJournal.Encode(native);if(!Equal(baseline,entity,frame))return false;
            byte[] body=native.entityData.ToArray(),bag=native.bagData.ToArray();int offset=body.Length-bag.Length-4;
            if(offset<0||body.Length>ushort.MaxValue||bag.Length<1||bag.Length>131072||BitConverter.ToInt32(body,offset)!=oldOwner)return false;
            for(int i=0;i<bag.Length;i++)if(body[offset+4+i]!=bag[i])return false;
            Buffer.BlockCopy(BitConverter.GetBytes(newOwner),0,body,offset,4);native.entityData.Dispose();native.entityData=new MemoryStream(body);
            byte[] after=RebirthLogisticsNativeInvocationJournal.Encode(native);
            var customs=new Dictionary<int,byte[]>();if(reference!=null)customs.Add(reference.Start,nextReference);
            var entities=new Dictionary<int,byte[]>{{entity.Start,after}};
            int added=reference==null?1:0;if(layout.Custom.Count+added>4096)return false;
            long size=baseline.LongLength+after.Length-entity.Length+(reference==null?nextReference.Length:nextReference.Length-reference.Length);if(size>MaximumRawBytes)return false;
            using(var output=new MemoryStream((int)size))using(var writer=new BinaryWriter(output,Encoding.UTF8,true))
            {
                output.Write(baseline,0,layout.CustomCount);writer.Write((ushort)(layout.Custom.Count+added));Copy(output,baseline,layout.Custom,customs);if(added!=0)output.Write(nextReference,0,nextReference.Length);
                output.Write(baseline,layout.CustomEnd,layout.EntityCount-layout.CustomEnd);writer.Write(layout.Entities.Count);Copy(output,baseline,layout.Entities,entities);output.Write(baseline,layout.EntityEnd,baseline.Length-layout.EntityEnd);writer.Flush();
                if(output.Length!=size)return false;candidate=output.ToArray();
            }
            if(!TryCompressedLength(candidate,out _)){candidate=null;return false;}return true;
        }
        catch{candidate=null;return false;}
        finally{native?.entityData?.Dispose();native?.bagData?.Dispose();}
    }
    internal static bool TryCompressedLength(byte[] snapshot,out int length)
    {
        length=0;if(snapshot==null||snapshot.Length<8||snapshot.Length>MaximumRawBytes)return false;
        try
        {
            using(var output=new RegionOutput())
            {
                output.Write(snapshot,0,8);
                using(var zip=new DeflateOutputStream(output,3,leaveOpen:true))
                {
                    zip.Write(snapshot,8,snapshot.Length-8);zip.Restart();
                    // Native RegionFileChunkWriter measures after Restart,
                    // before disposing its reusable compressor.
                    length=checked((int)output.Length);
                }
            }
            return length<=MaximumCompressedBytes;
        }
        catch{length=0;return false;}
    }
    private static void Copy(Stream output,byte[] source,List<Span> spans,Dictionary<int,byte[]> replacements)
    {foreach(var span in spans){if(replacements.TryGetValue(span.Start,out var bytes))output.Write(bytes,0,bytes.Length);else output.Write(source,span.Start,span.Length);}}
    private static bool Equal(byte[] source,Span span,byte[] expected)
    {if(expected==null||expected.Length!=span.Length)return false;for(int i=0;i<expected.Length;i++)if(source[span.Start+i]!=expected[i])return false;return true;}
    private static bool ValidMarker(byte[] bytes,string key)
    {
        if(bytes==null||bytes.Length>8192)return false;
        try
        {
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {return reader.ReadString()==key&&reader.ReadUInt64()==ulong.MaxValue&&!Flag(reader)&&reader.ReadUInt16()==stream.Length-stream.Position&&stream.Length-stream.Position>0;}
        }
        catch{return false;}
    }
    private static bool ValidEntity(byte[] bytes,int id,int entityClass)=>TryEntity(bytes,id,entityClass,out _);
    private static int Bucket(float y)
    {if(float.IsNaN(y)||float.IsInfinity(y))throw new InvalidDataException();return (int)Math.Max(0,Math.Min(15,Math.Floor((double)y/16.0)));}
    private static bool TryEntity(byte[] bytes,int id,int entityClass,out int bucket)
    {
        bucket=0;
        if(bytes==null||bytes.Length>RebirthLogisticsNativeBodyPreflight.MaximumFrame)return false;
        try
        {
        RebirthLogisticsNativeBodyPreflight.Validate(bytes,id>=0);
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);var native=new EntityCreationData();try{native.read(reader,StreamModeRead.Persistency);bucket=Bucket(native.pos.y);return stream.Position==stream.Length&&native.id==id&&native.entityClass==entityClass&&native.entityData!=null&&native.entityData.Length<=ushort.MaxValue;}finally{native.entityData?.Dispose();native.bagData?.Dispose();}}
        }
        catch{return false;}
    }
    private static bool TryLayout(byte[] bytes,int x,int z,out Layout result)
    {
        result=null;if(Chunk.CurrentSaveVersion!=47||bytes==null||bytes.Length<28||bytes.Length>MaximumRawBytes||bytes[0]!=116||bytes[1]!=116||bytes[2]!=99||bytes[3]!=0||BitConverter.ToUInt32(bytes,4)!=47)return false;
        try
        {
            var layout=new Layout();
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);stream.Position=8;if(reader.ReadInt32()!=x||reader.ReadInt32()!=0||reader.ReadInt32()!=z)return false;Skip(stream,8);
                for(int i=0;i<64;i++)if(Flag(reader)){Skip(stream,Flag(reader)?1024:1);if(Flag(reader))Skip(stream,3072);}
                Channel(reader,stream,1);Skip(stream,256+256+32+256+1536+2);
                layout.CustomCount=(int)stream.Position;int count=reader.ReadUInt16();var keys=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<count;i++)
                {
                    int start=(int)stream.Position;int size=StringLength(reader);if(size>4096)return false;Skip(stream,size+8);Flag(reader);Skip(stream,reader.ReadUInt16());
                    long end=stream.Position;stream.Position=start;string key=reader.ReadString();stream.Position=end;if(!keys.Add(key))return false;
                    layout.Custom.Add(new Span{Start=start,Length=(int)end-start,Key=key});
                }
                layout.CustomEnd=(int)stream.Position;Skip(stream,768);Channel(reader,stream,1);Channel(reader,stream,1);Channel(reader,stream,2);Channel(reader,stream,6);Channel(reader,stream,2);Flag(reader);
                layout.EntityCount=(int)stream.Position;count=reader.ReadInt32();if(count<0||count>4096)return false;var ids=new HashSet<int>();
                int previous=-1;
                for(int i=0;i<count;i++){int start=(int)stream.Position;var native=new EntityCreationData();try{native.read(reader,StreamModeRead.Persistency);int bucket=Bucket(native.pos.y);if(!ids.Add(native.id)||bucket<previous)return false;previous=bucket;layout.Entities.Add(new Span{Start=start,Length=(int)stream.Position-start,Id=native.id,Class=native.entityClass,Bucket=bucket});}finally{native.entityData?.Dispose();native.bagData?.Dispose();}}
                layout.EntityEnd=(int)stream.Position;
            }
            result=layout;return true;
        }
        catch{return false;}
    }
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Channel(BinaryReader reader,Stream stream,int bytes){for(int i=0;i<64;i++)Skip(stream,Flag(reader)?bytes:1024*bytes);}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position>stream.Length-count)throw new EndOfStreamException();stream.Position+=count;}
    private static int StringLength(BinaryReader reader){uint value=0;for(int i=0;i<5;i++){byte item=reader.ReadByte();if(i==4&&(item&240)!=0)throw new InvalidDataException();value|=(uint)(item&127)<<(7*i);if((item&128)==0)return checked((int)value);}throw new InvalidDataException();}
}

// One original planner at a time. At most nine retained8MiB baselines and one
// outstanding native snapshot, with original-bound capture/copy/retained leases.
// This observer uses the accepted native save scheduling path. It is not the
// detached preview and never directly calls Chunk.save/write.
internal sealed class RebirthLogisticsParcelBaselines:IDisposable
{
    // Admission is one original, reserving the full bounded working set before
    // Watch can allocate a proof copy:72MiB baselines,192MiB detached workspace,
    //120MiB queued/inflight allowance. Native preexisting pool capacity is not
    // a proof allocation; concrete queued capacities are checked separately.
    internal const long ReservationBytes=384L*1024*1024;
    private const long NativeAllowance=120L*1024*1024;
    private static RebirthLogisticsParcelBaselines active;
    private readonly World world;private readonly int rootX,rootZ;private readonly Func<bool> authorized;
    private readonly Dictionary<long,byte[]> snapshots=new Dictionary<long,byte[]>();
    private readonly Dictionary<long,IDisposable> retained=new Dictionary<long,IDisposable>();
    private readonly RebirthLogisticsSnapshotReservation budget;
    private readonly IRebirthStationSnapshotByteBudget watchBudget;
    private readonly DateTime expires=DateTime.UtcNow.AddMinutes(5);
    private RebirthStationSnapshotEvidence.Request request;
    private Chunk watched;private long watchedKey;private bool closed;
    private int deferredScopes;private bool deferred;private int deferredX,deferredZ;
    private sealed class Deferral:IDisposable
    {private RebirthLogisticsParcelBaselines owner;internal Deferral(RebirthLogisticsParcelBaselines owner){this.owner=owner;owner.deferredScopes++;}public void Dispose(){if(owner==null)return;owner.deferredScopes--;owner=null;}}
    internal IDisposable DeferCaptures()=>new Deferral(this);
    internal bool HasDeferred=>deferred;
    internal bool CompletePending()
    {
        if(request==null)return true;
        if(watched==null||watched.InProgressUnloading||!ReferenceEquals(world.GetChunkSync(watched.X,watched.Z),watched))
        {request.Dispose();request=null;watched=null;return false;}
        return TryAcquire(watched.X,watched.Z,out _);
    }
    internal void FlushDeferred()
    {
        if(deferredScopes!=0||!deferred||request!=null||!Current())return;
        int x=deferredX,z=deferredZ;deferred=false;
        var chunk=world.GetChunkSync(x,z) as Chunk;if(chunk!=null&&!chunk.InProgressUnloading)Start(world,chunk,x,z,Key(x,z));
    }
    internal long Revision{get;private set;}
    private RebirthLogisticsParcelBaselines(World world,int x,int z,Func<bool> current,RebirthLogisticsTransferJournal.Record original)
    {this.world=world;rootX=x;rootZ=z;authorized=current;budget=RebirthLogisticsSnapshotReservation.TryOpen(original,current);watchBudget=budget?.LimitTo(RebirthLogisticsParcelPreview.MaximumRawBytes);}
    internal static RebirthLogisticsParcelBaselines Begin(RebirthLogisticsDeathSettlement.Lease owner,int anchorX,int anchorZ)
    {
        if(!ThreadManager.IsMainThread()||owner?.Current()!=true)return null;
        if(active!=null&&!active.Current())active.Dispose();if(active!=null)return null;
        RebirthLogisticsParcelPlacement.Anchor(owner,out _,out int expectedX,out int expectedZ);if(anchorX!=expectedX||anchorZ!=expectedZ)return null;
        var created=new RebirthLogisticsParcelBaselines(owner.Accepted.World,anchorX,anchorZ,owner.Current,owner.Original);if(created.budget==null){created.Dispose();return null;}return active=created;
    }
    internal static RebirthLogisticsParcelBaselines BeginRecovery(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeRecovery.Record plan,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||current?.Invoke()!=true||plan==null||plan.State!=0||
            !RebirthLogisticsNativeRecovery.CanRecoverOriginal(original,out var fatal)||!RebirthLogisticsNativeRecovery.TryRead(original,out var retained)||!RebirthLogisticsNativeRecovery.Same(retained,plan))return null;
        if(active!=null&&!active.Current())active.Dispose();if(active!=null)return null;
        var anchor=World.toChunkXZ(new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z));var created=new RebirthLogisticsParcelBaselines(GameManager.Instance.World,anchor.x,anchor.y,current,original);if(created.budget==null){created.Dispose();return null;}return active=created;
    }
    internal static RebirthLogisticsParcelBaselines BeginReference(RebirthLogisticsTransferJournal.Record original,Guid incarnation,int entity,Func<bool> current)
        =>original?.Phase<4?BeginReferenceCore(original,incarnation,entity,current):null;
    internal static RebirthLogisticsParcelBaselines BeginTerminalReference(RebirthLogisticsTransferJournal.Record original,Guid incarnation,int entity,Func<bool> current)
        =>RebirthLogisticsParcelReferenceJournal.CanObserveTerminalEndpoint(original,incarnation,entity)?BeginReferenceCore(original,incarnation,entity,current):null;
    private static RebirthLogisticsParcelBaselines BeginReferenceCore(RebirthLogisticsTransferJournal.Record original,Guid incarnation,int entity,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||current?.Invoke()!=true||original==null||original.Phase>4||!RebirthLogisticsDeathJournal.TryRead(original,out var frozen)||frozen.State<2&&!RebirthLogisticsParcelReferenceJournal.IsNativeBeforeOnly(original,frozen))return null;
        var parcel=frozen.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation&&value.EntityId==entity);
        if((frozen.Incarnation!=incarnation||frozen.EntityId!=entity)&&(parcel==null||parcel.State<2))return null;
        int x=parcel?.X??frozen.X,z=parcel?.Z??frozen.Z;var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||!(world.GetEntity(entity) is EntityBackpack body)||!body.addedToChunk||body.chunkPosAddedEntityTo.x!=x||body.chunkPosAddedEntityTo.z!=z)return null;
        if(active!=null&&!active.Current())active.Dispose();if(active!=null)return null;
        var created=new RebirthLogisticsParcelBaselines(world,x,z,current,original);if(created.budget==null){created.Dispose();return null;}return active=created;
    }
    private bool Current()=>!closed&&budget!=null&&ThreadManager.IsMainThread()&&expires>DateTime.UtcNow&&ReferenceEquals(GameManager.Instance?.World,world)&&world?.IsRemote()==false&&authorized?.Invoke()==true;
    private static long Key(int x,int z)=>((long)x<<32)|(uint)z;
    internal static long ChunkKey(int x,int z)=>Key(x,z);
    internal bool IsCurrent()=>Current();
    internal long LargestSnapshotBytes=>snapshots.Count==0?0:snapshots.Values.Max(value=>value.LongLength);
    internal long SnapshotBytesAt(int x,int z)=>snapshots.TryGetValue(Key(x,z),out var value)?value.LongLength:0;
    internal bool TryReserveWorkspace(long bytes,out IDisposable allocation){allocation=null;return Current()&&budget.TryReserveAllocation(bytes,out allocation);}
    internal void Invalidate(int x,int z){long key=Key(x,z);snapshots.Remove(key);if(retained.TryGetValue(key,out var allocation)){retained.Remove(key);allocation.Dispose();}Revision++;}
    internal void InvalidateAll(){snapshots.Clear();foreach(var allocation in retained.Values)allocation.Dispose();retained.Clear();Revision++;}
    internal bool TryConfirmAll(IEnumerable<UnityEngine.Vector2Int> coordinates,Func<bool> execute)
    {
        if(!Current()||execute==null||request!=null)return false;
        var list=coordinates.Distinct().ToArray();if(list.Length<1||list.Length>9)return false;
        var regions=(world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld)?.m_RegionFileManager;
        if(regions==null||!System.Threading.Monitor.TryEnter(regions.saveLock,0))return false;
        try
        {
            if(!QueueFits(regions,0))return false;
            foreach(var position in list)if(!TryConfirm(position.x,position.y)||!QueueFits(regions,0))return false;
            return QueueFits(regions,0)&&Current()&&execute();
        }
        finally{System.Threading.Monitor.Exit(regions.saveLock);}
    }
    // Caller holds native saveLock (TryEnter0); no writer restart or wait.
    internal static bool CanAdmitNativeSnapshot(RegionFileManager regions)=>QueueFits(regions,1,RebirthLogisticsSnapshotReservation.MaximumNativeBytes);
    internal static bool NativeQueueFits(RegionFileManager regions)=>QueueFits(regions,0);
    private static bool QueueFits(RegionFileManager regions,int prospective,int maximum=RebirthLogisticsParcelPreview.MaximumRawBytes)
    {
        if(prospective<0||prospective>9)return false;
        long bytes=0;
        lock(regions.chunkMemoryStreamsToSave)
        {
            foreach(var value in regions.chunkMemoryStreamsToSave.Values)
            {
                if(!(value is RegionFileChunkSnapshot snapshot)||snapshot.stream==null)return false;
                bytes+=Math.Max(snapshot.Size,snapshot.stream.Capacity);if(bytes>NativeAllowance)return false;
            }
        }
        return RebirthLogisticsParcelBudget.CanReserveActual(bytes,prospective,maximum,RebirthLogisticsSnapshotReservation.MaximumRetainedBytes,RebirthLogisticsSnapshotReservation.MaximumWorkspaceBytes);
    }
    internal bool TryAcquire(int x,int z,out byte[] snapshot)
    {
        snapshot=null;if(!Current()){Dispose();return false;}if(Math.Abs((long)x-rootX)>1||Math.Abs((long)z-rootZ)>1)return false;
        long key=Key(x,z);if(snapshots.TryGetValue(key,out snapshot))return true;
        var chunk=world.GetChunkSync(x,z) as Chunk;
        if(chunk==null||chunk.InProgressUnloading)return false;
        if(request!=null)
        {
            if(watchedKey!=key||!ReferenceEquals(watched,chunk))return false;
            if(!RebirthStationSnapshotEvidence.HasPublished(request))return false;
            IDisposable copyAllocation=null,retainedAllocation=null;
            try
            {
                if(!RebirthStationSnapshotEvidence.TryCopy(request,out snapshot,out copyAllocation))return false;
                if(snapshot.Length>RebirthLogisticsParcelPreview.MaximumRawBytes||!budget.TryReserveRetainedSnapshot(snapshot.Length,out retainedAllocation)){snapshot=null;return false;}
                bool added=false;
                try{retained.Add(key,retainedAllocation);added=true;snapshots.Add(key,snapshot);retainedAllocation=null;}
                catch{if(added)retained.Remove(key);snapshots.Remove(key);snapshot=null;throw;}
                request.Dispose();request=null;watched=null;return true;
            }
            finally{retainedAllocation?.Dispose();copyAllocation?.Dispose();}
        }
        if(snapshots.Count>=9)return false;
        if(deferredScopes>0){if(!deferred){deferred=true;deferredX=x;deferredZ=z;}return false;}
        Start(world,chunk,x,z,key);return false;
    }
    // Confirmation runs synchronously inside the original pre-spawn native
    // turn. A changed current snapshot is retained for positive publication
    // and a fresh detached plan; it never authorizes using the stale plan.
    internal bool TryConfirm(int x,int z)
    {
        if(!Current()||request!=null||!snapshots.TryGetValue(Key(x,z),out var before))return false;
        var chunk=world.GetChunkSync(x,z) as Chunk;
        if(chunk==null||chunk.InProgressUnloading||!Start(world,chunk,x,z,Key(x,z)))return false;
        IDisposable allocation=null;
        try
        {
            if(!RebirthStationSnapshotEvidence.TryCopy(request,out var current,out allocation))
            {request?.Dispose();request=null;watched=null;return false;}
            if(!before.SequenceEqual(current)){Invalidate(x,z);return false;}
            request.Dispose();request=null;watched=null;return true;
        }
        finally{allocation?.Dispose();}
    }
    private bool Start(World world,Chunk chunk,int x,int z,long key)
    {
        if(request!=null||!Current())return false;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return false;
        var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);
        bool Observe()=>Current()&&ReferenceEquals(world.GetChunkSync(x,z),chunk)&&!chunk.InProgressUnloading&&
            ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&
            ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
        // No in-flight native write while calculating/admitting its queue
        // allowance. Never wait for a writer or restart it to obtain space.
        if(!System.Threading.Monitor.TryEnter(regions.saveLock,0))return false;
        try
        {
            if(!QueueFits(regions,1))return false;
            request=RebirthStationSnapshotEvidence.Watch(world,chunk,x,z,Observe,budget:watchBudget);if(request==null)return false;watched=chunk;watchedKey=key;
            lock(chunk)
            {
                if(!Observe()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)
                {request.Dispose();request=null;watched=null;return false;}
                chunk.InProgressSaving=true;
            }
            try{if(!Observe()){request.Dispose();request=null;watched=null;return false;}regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
            if(!QueueFits(regions,0)){request.Dispose();request=null;watched=null;return false;}
            return true; // Capture completed; publication remains asynchronous.
        }
        catch{request?.Dispose();request=null;watched=null;return false;}
        finally{System.Threading.Monitor.Exit(regions.saveLock);}
    }
    public void Dispose()
    {if(closed)return;closed=true;request?.Dispose();request=null;watched=null;InvalidateAll();budget?.Dispose();if(ReferenceEquals(active,this))active=null;}
}

internal static class RebirthLogisticsParcelBudget
{
    // Encoded proof/buffer reservation, not a claim about global native pool or
    // ItemValue object graph RSS. The caller must own the single-original gate.
    internal static bool CanReserve(long nativeQueued,int snapshots,long baselines,long workspace)
    {
        return snapshots>=1&&CanReserveActual(nativeQueued,snapshots,8*1024*1024,baselines,workspace);
    }
    internal static bool CanReserveActual(long nativeQueued,int snapshots,int actualMaximum,long baselines,long workspace)
    {
        const long mib=1024*1024;
        if(nativeQueued<0||nativeQueued>120*mib||snapshots<0||snapshots>9||actualMaximum<9||actualMaximum>32*mib||baselines<0||baselines>72*mib||workspace<0||workspace>192*mib)return false;
        long native=nativeQueued+snapshots*(long)actualMaximum;
        return native<=120*mib&&native+baselines+workspace<=384*mib;
    }
}

internal static class RebirthLogisticsParcelPlacement
{
    // Candidate enumeration is finite and tied to the actual accepted death
    // origin, never to the current/new-life player location. Admission still
    // needs the exact loaded chunk baseline and detached native bounds check.
    internal static void Anchor(RebirthLogisticsDeathSettlement.Lease owner,out UnityEngine.Vector3 position,out int x,out int z)
    {
        position=owner.Accepted.Backpack.position;x=owner.Death.X;z=owner.Death.Z;
        if(owner.Death.Before.All(row=>row!=null&&row.IsEmpty())&&RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(owner.Original,out var fatal)&&owner.Death.OwnerFileProof==fatal.OwnerFileProof)
        {position=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var chunk=World.toChunkXZ(position);x=chunk.x;z=chunk.y;}
    }
    internal static IEnumerable<UnityEngine.Vector3> Candidates(RebirthLogisticsDeathSettlement.Lease owner)
    {
        if(owner?.Current()!=true)yield break;
        Anchor(owner,out var anchor,out int x,out int z);foreach(var candidate in Candidates(owner.Accepted.World,anchor,x,z))yield return candidate;
    }
    internal static IEnumerable<UnityEngine.Vector3> Candidates(World world,UnityEngine.Vector3 origin,int rootX,int rootZ)
    {
        foreach(int radius in new[]{0,1})for(int dx=-radius;dx<=radius;dx++)for(int dz=-radius;dz<=radius;dz++)
        {
            if(radius==1&&dx==0&&dz==0)continue;
            int x=rootX+dx,z=rootZ+dz;var chunk=world.GetChunkSync(x,z) as Chunk;
            if(chunk==null||chunk.InProgressUnloading||!world.IsInPlayfield(chunk))continue;
            // Interior 2m lattice stays away from chunk seams. Surface height
            // follows the actual native candidate chunk; cliffs are refused.
            for(int lx=3;lx<=13;lx+=2)for(int lz=3;lz<=13;lz+=2)
            {
                var position=new UnityEngine.Vector3(x*16+lx+0.5f,chunk.GetHeight(lx,lz)+1.1f,z*16+lz+0.5f);
                if(Math.Abs(position.y-origin.y)>8||(position-origin).sqrMagnitude>32*32)continue;
                yield return position;
            }
        }
    }
    internal static bool IsSafe(RebirthLogisticsDeathSettlement.Lease owner,EntityBackpack detached,IEnumerable<UnityEngine.Bounds> reserved)
    {
        if(owner?.Current()!=true||detached==null||owner.Accepted.World.GetEntity(detached.entityId)!=null)return false;
        Anchor(owner,out var anchor,out int x,out int z);return IsSafe(owner.Accepted.World,anchor,x,z,detached,reserved);
    }
    internal static bool IsSafe(World world,UnityEngine.Vector3 origin,int rootX,int rootZ,EntityBackpack detached,IEnumerable<UnityEngine.Bounds> reserved)
    {
        if(world==null||detached==null||world.GetEntity(detached.entityId)!=null)return false;
        var position=detached.position;var coordinates=World.toChunkXZ(position);
        if(Math.Abs((long)coordinates.x-rootX)>1||Math.Abs((long)coordinates.y-rootZ)>1||
            (position-origin).sqrMagnitude>32*32||Math.Abs(position.y-origin.y)>8||!world.IsChunkAreaCollidersLoaded(position))return false;
        var bounds=detached.getBoundingBox();
        if(bounds.size.x<=0||bounds.size.y<=0||bounds.size.z<=0||bounds.size.x>4||bounds.size.y>4||bounds.size.z>4||bounds.min.y<1||bounds.max.y>254)return false;
        // Native GetCollidingBounds skips missing chunks. Explicitly cover its
        // entire padded block-cache scan before trusting an empty result.
        int minX=World.toChunkXZ(Utils.Fastfloor(bounds.min.x-0.5f)-1),maxX=World.toChunkXZ(Utils.Fastfloor(bounds.max.x+0.5f)+1);
        int minZ=World.toChunkXZ(Utils.Fastfloor(bounds.min.z-0.5f)-1),maxZ=World.toChunkXZ(Utils.Fastfloor(bounds.max.z+0.5f)+1);
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
        {var chunk=world.GetChunkSync(x,z) as Chunk;if(chunk==null||chunk.InProgressUnloading||!world.IsInPlayfield(chunk))return false;}
        if(reserved!=null&&reserved.Any(other=>other.Intersects(bounds)))return false;
        return world.GetCollidingBounds(detached,bounds,new List<UnityEngine.Bounds>()).Count==0;
    }
}

// Exact original parcel planning and cold continuation. All construction is
// detached; only the existing settlement owns actual spawn/setter/publication.
internal sealed class RebirthLogisticsParcelCoordinator:IDisposable
{
    private sealed class Body
    {
        internal int X,Z,Id,Class;internal RebirthLogisticsDeathJournal.Parcel Parcel;
        internal RebirthLogisticsBackpackPublication.PlanImage Image;
        internal ItemStack[] Before,After;internal byte[] Frame,Marker;internal bool Complete;
    }
    private sealed class Stage{internal int X,Z;internal RebirthLogisticsParcelPreview.Edit[] Edits;}
    private sealed class WorkspaceFull:IOException{}
    // Every new frame is initially covered by an actual-length-derived scratch
    // lease, then transferred to this exact byte-counted per-chunk arena.
    private sealed class FrameArena:IDisposable
    {
        private readonly RebirthLogisticsParcelBaselines baselines;
        private readonly Dictionary<byte[],IDisposable> arrays=new Dictionary<byte[],IDisposable>();
        private readonly List<IDisposable> other=new List<IDisposable>();private long bytes;
        internal FrameArena(RebirthLogisticsParcelBaselines baselines){this.baselines=baselines;}
        private readonly List<IDisposable> owned=new List<IDisposable>();
        internal void Remember(byte[] value,IDisposable existing=null)
        {
            if(value==null||arrays.ContainsKey(value)){existing?.Dispose();return;}
            if(value.LongLength>64L*1024*1024-bytes){existing?.Dispose();throw new WorkspaceFull();}
            IDisposable allocation=existing;bool added=false;
            try
            {
                if(allocation==null&&!baselines.TryReserveWorkspace(value.LongLength,out allocation))throw new WorkspaceFull();
                arrays.Add(value,allocation);added=true;owned.Add(allocation);bytes+=value.LongLength;allocation=null;
            }
            catch{if(added)arrays.Remove(value);throw;}
            finally{allocation?.Dispose();}
        }
        internal void References(int count)
        {
            long size=64L+16L*count;if(size>64L*1024*1024-bytes||!baselines.TryReserveWorkspace(size,out var allocation))throw new WorkspaceFull();
            try{other.Add(allocation);bytes+=size;allocation=null;}finally{allocation?.Dispose();}
        }
        public void Dispose(){arrays.Clear();foreach(var allocation in other)allocation.Dispose();other.Clear();foreach(var allocation in owned)allocation.Dispose();owned.Clear();bytes=0;}

    }

    private readonly RebirthLogisticsDeathSettlement.Lease owner;
    private readonly RebirthLogisticsTransferJournal.Record recoveryOriginal;
    private readonly RebirthLogisticsNativeRecovery.Record recovery;
    private readonly EntityBackpack recoveryRoot;
    private readonly World recoveryWorld;private readonly UnityEngine.Vector3 anchorPosition;private readonly int anchorX,anchorZ;
    private RebirthLogisticsDeathJournal.Record recoveryDeath;
    private RebirthLogisticsTransferJournal.Record Original=>owner?.Original??recoveryOriginal;
    private RebirthLogisticsDeathJournal.Record Death{get=>owner?.Death??recoveryDeath;set{if(owner!=null)owner.Death=value;else recoveryDeath=value;}}
    private World NativeWorld=>owner?.Accepted.World??recoveryWorld;
    private EntityBackpack RootEntity=>owner?.Accepted.Backpack??recoveryRoot;
    private bool Current()
    {
        if(closed)return false;
        if(owner!=null)return owner.Current();
        try{return !closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,recoveryWorld)&&recoveryWorld?.IsRemote()==false&&
            recovery!=null&&recovery.State==0&&recoveryWorld.GetEntity(recovery.EntityId)==null&&
            RebirthLogisticsTransferJournal.TryRead(recoveryOriginal.Owner,recoveryOriginal.Operation,out var saved)&&saved.Phase>=2&&saved.Phase<4&&
            RebirthLogisticsNativeRecovery.CanRecoverOriginal(saved,out _)&&saved.Outcome==recoveryOriginal.Outcome&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved)==recovery.Binding&&
            RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(saved,out _)&&RebirthLogisticsNativeRecovery.TryRead(saved,out var plan)&&
            RebirthLogisticsNativeRecovery.Same(plan,recovery)&&RootEntity?.RefPlayerId==recovery.OwnerEntity&&
            RebirthLogisticsDeathPartition.Same(RootEntity.bag?.ItemGrid?.items,recovery.Before);}
        catch{return false;}
    }
    internal RebirthLogisticsParcelCoordinator(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeRecovery.Record plan,EntityBackpack detached)
    {
        recoveryOriginal=original;recovery=plan;recoveryRoot=detached;recoveryWorld=GameManager.Instance?.World;
        recoveryDeath=plan.Prepared??RebirthLogisticsNativeRecovery.Projection(original,plan);draft=plan.Prepared;
        if(!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var fatal))throw new InvalidDataException();anchorPosition=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var chunk=World.toChunkXZ(anchorPosition);anchorX=chunk.x;anchorZ=chunk.y;
    }
    internal bool TryPrepareRecovery(out RebirthLogisticsDeathJournal.Record prepared)
    {
        prepared=null;if(owner!=null||!Current())return false;
        if(draft==null){if(!Ready(true)||!Plan())return false;Death=draft;}
        if(!TryFrozen())return false;prepared=draft;return true;
    }
    internal bool IsCurrent()=>Current();
    private string FrozenKey()=>owner!=null?RebirthLogisticsDeathJournal.PlanKey(Death):RebirthLogisticsNativeRecovery.PreparedKey(Original,recovery,Death);
    private RebirthLogisticsParcelBaselines baselines;
    private RebirthLogisticsDeathJournal.Record draft;
    private readonly List<Stage> stages=new List<Stage>();private readonly List<UnityEngine.Vector2Int> used=new List<UnityEngine.Vector2Int>();
    private FrameArena frames;private int group;private readonly List<UnityEngine.Vector2Int> groups=new List<UnityEngine.Vector2Int>();
    private int stage;private string key;private bool approved,closed;private DateTime retry;
    internal RebirthLogisticsParcelCoordinator(RebirthLogisticsDeathSettlement.Lease owner){this.owner=owner;RebirthLogisticsParcelPlacement.Anchor(owner,out var point,out int x,out int z);anchorPosition=point;anchorX=x;anchorZ=z;}
    private bool Ready(bool neighbors)
    {
        if(closed||DateTime.UtcNow<retry||!Current())return false;
        if(baselines!=null&&!baselines.IsCurrent()){baselines.Dispose();baselines=null;Reset();}
        if(baselines==null)baselines=owner!=null?RebirthLogisticsParcelBaselines.Begin(owner,anchorX,anchorZ):RebirthLogisticsParcelBaselines.BeginRecovery(Original,recovery,Current);if(baselines==null)return false;
        if(!baselines.CompletePending()||!baselines.TryAcquire(Death.X,Death.Z,out _))return false;
        // Acquire only native chunks the original plan actually reaches.
        // Loaded but unused neighbors do not consume the retained budget.
        return true;
    }
    private void ReleaseStages(){stages.Clear();frames?.Dispose();frames=null;stage=0;}
    private void Reset(){approved=false;key=null;group=0;groups.Clear();ReleaseStages();used.Clear();}
    private sealed class ValueCount:Stream
    {
        private long length;public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Write(byte[] value,int offset,int count){if(value==null||offset<0||count<0||offset>value.Length-count||length>8L*1024*1024-count)throw new WorkspaceFull();length+=count;}
        public override void WriteByte(byte value){if(length>=8L*1024*1024)throw new WorkspaceFull();length++;}
        public override void Flush(){}public override int Read(byte[] value,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private static long ItemBytes(ItemStack item)
    {
        if(item?.itemValue==null)throw new WorkspaceFull();
        using(var stream=new ValueCount())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {var encoding=writer.Encoding;try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);item.itemValue.Write(writer);writer.Flush();return checked(stream.Length+16);}finally{writer.Encoding=encoding;}}
    }
    private IDisposable Scratch(bool planning=false,int? chunkX=null,int? chunkZ=null)
    {
        const long mib=1024*1024;
        long raw=chunkX.HasValue?baselines.SnapshotBytesAt(chunkX.Value,chunkZ.Value):baselines.LargestSnapshotBytes;if(raw<9||raw>RebirthLogisticsSnapshotReservation.MaximumNativeBytes)throw new WorkspaceFull();
        long largestVector=0,largestEdit=0;
        var positions=new List<UnityEngine.Vector2Int>();
        if(chunkX.HasValue)positions.Add(new UnityEngine.Vector2Int(chunkX.Value,chunkZ.Value));
        else{positions.Add(new UnityEngine.Vector2Int(Death.X,Death.Z));foreach(var parcel in Death.Parcels){var point=new UnityEngine.Vector2Int(parcel.X,parcel.Z);if(!positions.Contains(point))positions.Add(point);}}
        foreach(var point in positions)
        {
            long vector=0,seed=0;int bodies=0,live=0;
            if(point.x==Death.X&&point.y==Death.Z)
            {foreach(var item in Death.After??Death.Before)vector=checked(vector+ItemBytes(item));seed=Death.Spawn?.Length??0;bodies++;if(NativeWorld.GetEntity(Death.EntityId)!=null)live++;else if(Death.Spawn==null)seed+=RebirthLogisticsNativeBodyPreflight.MaximumCanonicalBytes;}
            foreach(var parcel in Death.Parcels.Where(value=>value.X==point.x&&value.Z==point.y))
            {foreach(var item in parcel.After??parcel.Before)vector=checked(vector+ItemBytes(item));seed=checked(seed+(parcel.Spawn?.Length??0));bodies++;if(NativeWorld.GetEntity(parcel.EntityId)!=null)live++;else if(parcel.Spawn==null)seed+=RebirthLogisticsNativeBodyPreflight.MaximumCanonicalBytes;}
            seed=checked(seed+Math.Min(raw,live*(long)RebirthLogisticsNativeBodyPreflight.MaximumCanonicalBytes));largestVector=Math.Max(largestVector,vector);largestEdit=Math.Max(largestEdit,checked(seed+2*vector+bodies*8192L));
        }
        if(planning)
        {
            if(!RebirthLogisticsDeathPartition.TryOwed(Original,Death,out var owed))throw new WorkspaceFull();long repeated=0;
            foreach(var item in owed.Where(value=>!value.IsEmpty()))
            {
                if(item.itemValue.ItemClass==null)throw new WorkspaceFull();int maximum=Math.Min(ushort.MaxValue,Math.Max(1,item.itemValue.ItemClass.Stacknumber.Value));
                long atoms=((long)item.count+maximum-1)/maximum;repeated=checked(repeated+atoms*ItemBytes(item));
                // Frozen conservation already bounds all repeated native item
                // metadata at16MiB; refuse impossible resource plans up front.
                if(repeated>16*mib)throw new WorkspaceFull();
            }
            largestVector=checked(largestVector+repeated);largestEdit=checked(largestEdit+2*repeated);
        }
        long metadata=Math.Min(16*mib,checked(raw*16+mib));
        long edits=planning?largestEdit:0; // Stage edit arrays already have exact leases.
        long candidate=Math.Min(RebirthLogisticsSnapshotReservation.MaximumNativeBytes,checked(raw+largestEdit));
        long required=Math.Max(2*mib,Math.Max(checked(2*raw+metadata+edits),Math.Max(checked(2*candidate+edits+RebirthLogisticsParcelPreview.MaximumCompressedBytes+65536+2*mib),checked(3*largestVector+2*mib))));
        if(required>RebirthLogisticsSnapshotReservation.MaximumWorkspaceBytes||!baselines.TryReserveWorkspace(required,out var allocation))throw new WorkspaceFull();return allocation;
    }
    private RebirthLogisticsBackpackPublication.PlanImage CaptureImage(EntityBackpack body)
    {var image=RebirthLogisticsBackpackPublication.CapturePlanImage(body);if(image!=null)frames.Remember(image.FrameReference);return image;}
    private RebirthLogisticsBackpackPublication.PlanImage ReadImage(RebirthLogisticsNativeFrame saved,ItemStack[] before)
    {
        var materialization=saved?.Open();if(materialization==null)return null;
        try
        {
            var image=RebirthLogisticsBackpackPublication.ReadSavedPlanImage(materialization.Bytes,before);if(image==null)return null;
            materialization.FinishDecode();frames.Remember(materialization.Bytes,materialization);materialization=null;return image;
        }
        finally{materialization?.Dispose();}
    }
    private static byte[] Marker(RebirthLogisticsDeathJournal.Record record,Body body,ItemStack[] after,bool effect)
    {
        var projection=new RebirthLogisticsDeathJournal.Record{Operation=record.Operation,Incarnation=body.Parcel?.Incarnation??record.Incarnation,Owner=record.Owner,World=record.World,
            Binding=record.Binding,Deaths=record.Deaths,OwnerEntity=record.OwnerEntity,EntityId=body.Id,EntityClass=body.Class,Before=body.Parcel?.Before??record.Before,After=after};
        return RebirthLogisticsParcelPreview.MarkerFrame(RebirthLogisticsDeathJournal.MarkerKey(projection),RebirthLogisticsDeathJournal.Marker(projection,effect));
    }
    private bool Root(out Body body)
    {
        body=null;var record=Death;
        if(owner==null)
        {
            if(!Current()||!baselines.TryAcquire(record.X,record.Z,out var emptySnapshot))return false;
            var image=record.Spawn==null?CaptureImage(RootEntity):ReadImage(record.Spawn,record.Before);if(image==null)return false;
            var planned=new Body{X=record.X,Z=record.Z,Id=record.EntityId,Class=record.EntityClass,Before=record.Before,After=record.After,Image=image};
            RebirthLogisticsParcelPreview.TryReadMarker(emptySnapshot,record.X,record.Z,RebirthLogisticsDeathJournal.MarkerKey(record),out planned.Marker);
            if(planned.Marker!=null)return false;body=planned;return true;
        }
        if(!baselines.TryAcquire(record.X,record.Z,out var snapshot)||!RebirthLogisticsParcelPreview.TryReadOriginal(snapshot,record.X,record.Z,record.Operation,record.EntityId,record.EntityClass,out var frame,out var marker))return false;
        bool complete=record.After!=null&&owner.Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(record,true));
        var expected=complete?record.After:record.Before;
        if(!RebirthLogisticsDeathPartition.Same(RootEntity.bag.ItemGrid.items,expected))return false;
        var result=new Body{X=record.X,Z=record.Z,Id=record.EntityId,Class=record.EntityClass,Before=expected,After=record.After,Frame=frame,Marker=marker,Complete=complete};
        if(!marker.SequenceEqual(Marker(record,result,record.After,complete)))return false;
        frames.Remember(frame);frames.Remember(marker);
        result.Image=RebirthLogisticsBackpackPublication.ReadSavedPlanImage(frame,expected);if(result.Image==null)return false;body=result;return true;
    }
    internal bool TryWarm()
    {
        if(draft==null)
        {
            if(!Ready(!RebirthLogisticsDeathJournal.HasTemporary(Original)))return false;
            if(RebirthLogisticsDeathJournal.HasTemporary(Original))
            {if(!RebirthLogisticsDeathJournal.TryReadPreparedDistribution(Original,out draft))return false;}
            else if(RebirthLogisticsNativeRecovery.TryRead(Original,out var recovered)&&recovered.Prepared!=null)
            {if(!RebirthLogisticsNativeRecovery.TryAdoptPrepared(Original,recovered,Death,out draft))return false;}
            else if(!Plan())return false;
        }
        Death=draft;
        if(!TryFrozen())return false;
        if(!RebirthLogisticsDeathJournal.Publish(Original,draft,Current)){approved=false;return false;}
        return true;
    }
    private bool Plan()
    {
        if(frames!=null)return false;
        if(Death.PreservedOwed==null&&RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(Original,out var physicalOwed))Death.PreservedOwed=physicalOwed;
        try{using(var deferred=baselines.DeferCaptures())using(var scratch=Scratch(true)){frames=new FrameArena(baselines);return PlanCore();}}
        catch(WorkspaceFull){return false;}
        finally{frames?.Dispose();frames=null;baselines.FlushDeferred();}
    }
    private bool PlanCore()
    {
        var record=Death;
        if(record.PreservedOwed==null&&RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(Original,out var physicalOwed))record.PreservedOwed=physicalOwed;
        if(record.State!=(owner==null?0:1)||record.Parcels.Count!=0||!Root(out var root)||!RebirthLogisticsDeathPartition.TryParcelLimit(Original,record,out int limit))return false;
        record.Outcome=Original.Outcome;root.After=ItemStack.Clone(record.Before);var bodies=new List<Body>{root};var reserved=new List<UnityEngine.Bounds>{RootEntity.getBoundingBox()};
        var candidates=RebirthLogisticsParcelPlacement.Candidates(NativeWorld,anchorPosition,anchorX,anchorZ).GetEnumerator();
        bool Fits(int index,ItemStack[] values)
        {
            var body=bodies[index];if(!body.Image.Fits(values))return false;var previous=body.After;body.After=ItemStack.Clone(values);
            if(Combined(record,bodies,body.X,body.Z))return true;body.After=previous;return false;
        }
        try
        {
            while(true)
            {
                foreach(var body in bodies)body.After=ItemStack.Clone(body.Before);
                if(RebirthLogisticsDeathPartition.TryPlanAll(Original,record,out var result,Fits))
                {
                    record.Spawn=RebirthLogisticsNativeFrame.Capture(Original,root.Image.FrameReference);record.After=result[0];for(int i=0;i<record.Parcels.Count;i++)record.Parcels[i].After=result[i+1];record.State=owner==null?0:2;
                    if(!RebirthLogisticsDeathPartition.ValidateFrozenPlan(Original,record))return false;draft=record;return true;
                }
                if(baselines.HasDeferred)return false;
                if(record.Parcels.Count>=limit||record.PreservedOwed==null&&(record.OwnerPhase!=2||record.Outcome==3))return false;
                bool added=false;
                while(candidates.MoveNext())
                {
                    var position=candidates.Current;var coordinates=World.toChunkXZ(position);EntityBackpack prototype=null;
                    try
                    {
                        if(!baselines.TryAcquire(coordinates.x,coordinates.y,out _))return false;
                        prototype=EntityFactory.CreateEntity(record.EntityClass,position) as EntityBackpack;
                        if(prototype?.bag?.ItemGrid?.items==null||prototype.bag.SlotCount<1||prototype.bag.SlotCount>256||prototype.entityId<0||
                            prototype.bag.ItemGrid.items.Any(item=>item==null||!item.IsEmpty())||!RebirthLogisticsParcelPlacement.IsSafe(NativeWorld,anchorPosition,anchorX,anchorZ,prototype,reserved))continue;
                        prototype.RefPlayerId=record.OwnerEntity;var image=CaptureImage(prototype);if(image==null)continue;
                        var parcel=new RebirthLogisticsDeathJournal.Parcel{Incarnation=Guid.NewGuid(),EntityId=prototype.entityId,EntityClass=prototype.entityClass,X=coordinates.x,Z=coordinates.y,
                            PosX=position.x,PosY=position.y,PosZ=position.z,Spawn=RebirthLogisticsNativeFrame.Capture(Original,image.FrameReference),Before=ItemStack.Clone(prototype.bag.ItemGrid.items),Locks=Enumerable.Range(0,prototype.bag.SlotCount).Select(i=>prototype.bag.ItemGrid.IsLocked(i)).ToArray()};
                        var body=new Body{X=parcel.X,Z=parcel.Z,Id=parcel.EntityId,Class=parcel.EntityClass,Before=parcel.Before,After=ItemStack.Clone(parcel.Before),Parcel=parcel,Image=image};
                        bodies.Add(body);if(!Combined(record,bodies,body.X,body.Z)){bodies.RemoveAt(bodies.Count-1);continue;}
                        record.Parcels.Add(parcel);reserved.Add(prototype.getBoundingBox());added=true;break;
                    }
                    finally{prototype?.OnEntityUnload();}
                }
                if(!added)return false;
            }
        }
        finally{candidates.Dispose();if(draft==null){if(owner!=null)owner.Reload();else recoveryDeath=RebirthLogisticsNativeRecovery.Projection(Original,recovery);}}
    }
    private bool Combined(RebirthLogisticsDeathJournal.Record record,List<Body> bodies,int x,int z)
    {
        if(!baselines.TryAcquire(x,z,out var snapshot))return false;var edits=new List<RebirthLogisticsParcelPreview.Edit>();
        foreach(var body in bodies.Where(value=>value.X==x&&value.Z==z))
        {
            if(!body.Image.TryBuild(body.After,out var after))return false;
            edits.Add(new RebirthLogisticsParcelPreview.Edit{Operation=record.Operation,EntityId=body.Id,EntityClass=body.Class,BeforeEntity=body.Frame,BeforeMarker=body.Marker,
                AfterEntity=after,AfterMarker=Marker(record,body,body.After,true)});
        }
        return RebirthLogisticsParcelPreview.TryBuild(snapshot,x,z,edits.ToArray(),out _,out _);
    }
    internal bool TryFrozen()
    {
        if(!Current()||!RebirthLogisticsDeathPartition.ValidateFrozenPlan(Original,Death))return false;
        string current=FrozenKey();
        if(approved&&key==current)
        {
            if(baselines==null||!baselines.IsCurrent()){Reset();return false;}
            long prior=baselines.Revision;
            if(baselines.TryConfirmAll(used,()=>Current()&&FrozenKey()==key))return true;
            if(baselines.Revision!=prior)Reset();return false;
        }
        if(!Ready(false))return false;
        if(key!=current){Reset();key=current;}
        if(groups.Count==0)
        {
            groups.Add(new UnityEngine.Vector2Int(Death.X,Death.Z));
            foreach(var parcel in Death.Parcels){var position=new UnityEngine.Vector2Int(parcel.X,parcel.Z);if(!groups.Contains(position))groups.Add(position);}
            if(groups.Count>9){Reset();return false;}
        }
        if(group<groups.Count)
        {
            if(stages.Count==0)
            {
                if(!baselines.TryAcquire(groups[group].x,groups[group].y,out _))return false;
                bool built=false;frames=new FrameArena(baselines);
                try{using(var scratch=Scratch(false,groups[group].x,groups[group].y))built=BuildStages(groups[group].x,groups[group].y);}
                catch(WorkspaceFull){return false;}
                finally{if(!built)ReleaseStages();}
                if(!built){baselines.InvalidateAll();Reset();retry=DateTime.UtcNow.AddSeconds(2);return false;}
            }
            if(stage<stages.Count)
            {
                var next=stages[stage];if(!baselines.TryAcquire(next.X,next.Z,out var snapshot))return false;
                bool fits;
                try{using(var scratch=Scratch(false,next.X,next.Z))fits=RebirthLogisticsParcelPreview.TryBuild(snapshot,next.X,next.Z,next.Edits,out _,out _);}
                catch(WorkspaceFull){return false;}
                if(!fits){baselines.Invalidate(next.X,next.Z);Reset();retry=DateTime.UtcNow.AddSeconds(2);return false;}
                stage++;return false; // At most one native compression unit per tick.
            }
            ReleaseStages();group++;return false;
        }
        // No materialized frame/stage graph remains while native captures and
        // publication clones need their full per-original workspace allowance.
        long revision=baselines.Revision;
        if(!baselines.TryConfirmAll(used,()=>Current()&&FrozenKey()==key))
        {if(baselines.Revision!=revision)Reset();return false;}
        approved=true;stages.Clear();return true;
    }
    private bool BuildStages(int chunkX,int chunkZ)
    {
        var record=Death;Body root=null;
        if(record.X==chunkX&&record.Z==chunkZ&&!Root(out root))return false;
        var bodies=new List<Body>();if(root!=null)bodies.Add(root);
        foreach(var parcel in record.Parcels.Where(value=>value.X==chunkX&&value.Z==chunkZ))
        {
            if(!baselines.TryAcquire(parcel.X,parcel.Z,out var snapshot))return false;
            var native=NativeWorld.GetEntity(parcel.EntityId) as EntityBackpack;
            var body=new Body{X=parcel.X,Z=parcel.Z,Id=parcel.EntityId,Class=parcel.EntityClass,Parcel=parcel,After=parcel.After};
            if(native==null)
            {
                if(parcel.State!=0||NativeWorld.GetEntity(parcel.EntityId)!=null)return false;
                EntityBackpack prototype=null;
                try
                {
                    prototype=EntityFactory.CreateEntity(parcel.EntityClass,parcel.EntityId,new UnityEngine.Vector3(parcel.PosX,parcel.PosY,parcel.PosZ),UnityEngine.Vector3.zero) as EntityBackpack;
                    if(prototype?.bag?.ItemGrid?.items==null||!RebirthLogisticsDeathPartition.Same(prototype.bag.ItemGrid.items,parcel.Before)||Enumerable.Range(0,parcel.Locks.Length).Any(i=>prototype.bag.ItemGrid.IsLocked(i)!=parcel.Locks[i]))return false;
                    prototype.RefPlayerId=record.OwnerEntity;body.Image=parcel.Spawn==null?CaptureImage(prototype):ReadImage(parcel.Spawn,parcel.Before);body.Before=parcel.Before;
                    RebirthLogisticsParcelPreview.TryReadMarker(snapshot,parcel.X,parcel.Z,RebirthLogisticsDeathJournal.MarkerKey(record,parcel),out body.Marker);
                    if(body.Marker!=null&&!body.Marker.SequenceEqual(Marker(record,body,parcel.After,false)))return false;
                }
                finally{prototype?.OnEntityUnload();}
            }
            else
            {
                if(parcel.State<1||native.RefPlayerId!=record.OwnerEntity||native.entityClass!=parcel.EntityClass||!native.addedToChunk||native.chunkPosAddedEntityTo.x!=parcel.X||native.chunkPosAddedEntityTo.z!=parcel.Z||
                    native.bag?.ItemGrid?.Length!=parcel.Locks.Length||Enumerable.Range(0,parcel.Locks.Length).Any(i=>native.bag.ItemGrid.IsLocked(i)!=parcel.Locks[i])||
                    !RebirthLogisticsParcelPreview.TryReadOriginal(snapshot,parcel.X,parcel.Z,record.Operation,parcel.EntityId,parcel.EntityClass,out body.Frame,out body.Marker))return false;
                body.Complete=body.Marker.SequenceEqual(Marker(record,body,parcel.After,true));body.Before=body.Complete?parcel.After:parcel.Before;
                if(!body.Marker.SequenceEqual(Marker(record,body,parcel.After,body.Complete))||!RebirthLogisticsDeathPartition.Same(native.bag.ItemGrid.items,body.Before))return false;
                frames.Remember(body.Frame);frames.Remember(body.Marker);body.Image=RebirthLogisticsBackpackPublication.ReadSavedPlanImage(body.Frame,body.Before);
            }
            if(body.Image==null)return false;frames.Remember(body.Marker);bodies.Add(body);
        }
        var states=new Dictionary<Body,RebirthLogisticsParcelPreview.Edit>();
        foreach(var body in bodies.Where(value=>value.Frame!=null))states.Add(body,Edit(record,body,body.Frame,body.Marker));
        void Capture(Body changed)
        {
            var count=states.Count(pair=>pair.Key.X==changed.X&&pair.Key.Z==changed.Z);frames.References(count);
            stages.Add(new Stage{X=changed.X,Z=changed.Z,Edits=states.Where(pair=>pair.Key.X==changed.X&&pair.Key.Z==changed.Z).Select(pair=>pair.Value).ToArray()});
            var position=new UnityEngine.Vector2Int(changed.X,changed.Z);if(!used.Contains(position))used.Add(position);
        }
        foreach(var body in bodies.Where(value=>value.Frame==null))
        {
            // Recovery root: native spawn first, marker admission afterwards.
            // Extras: marker admission first, durable attempt, native spawn.
            // Compression is not monotonic, so preview each real intermediate.
            if(body.Parcel==null)
            {var only=Edit(record,body,body.Image.FrameReference,null);only.EntityOnly=true;states[body]=only;Capture(body);}
            else if(body.Marker==null)
            {var only=Edit(record,body,null,Marker(record,body,body.After,false));only.MarkerOnly=true;states[body]=only;Capture(body);}
            states[body]=Edit(record,body,body.Image.FrameReference,Marker(record,body,body.After,false));Capture(body);
        }
        foreach(var body in bodies.Where(value=>!ReferenceEquals(value,root)).Concat(root==null?Array.Empty<Body>():new[]{root}))
        {
            byte[] after=body.Frame;
            if(!body.Complete&&!body.Image.TryBuild(body.After,out after))return false;
            states[body]=Edit(record,body,after,Marker(record,body,body.After,true));Capture(body);
        }
        return stages.Count>0;
    }
    private RebirthLogisticsParcelPreview.Edit Edit(RebirthLogisticsDeathJournal.Record record,Body body,byte[] after,byte[] marker)
    {
        frames.Remember(after);frames.Remember(marker);
        return new RebirthLogisticsParcelPreview.Edit{Operation=record.Operation,EntityId=body.Id,EntityClass=body.Class,BeforeEntity=body.Frame,BeforeMarker=body.Marker,AfterEntity=after,AfterMarker=marker};
    }
    public void Dispose(){if(closed)return;closed=true;baselines?.Dispose();baselines=null;draft=null;Reset();}
}
