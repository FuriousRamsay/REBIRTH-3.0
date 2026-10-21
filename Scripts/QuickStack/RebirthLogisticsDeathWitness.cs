using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Exact target native Chunk.CurrentSaveVersion47 prefix through the declared
// ECD array. Separate from Survivor's deliberately bounded <=46 recovery parser.
internal static class RebirthLogisticsEntity47
{
    internal static bool Contains(byte[] snapshot,int x,int z,int id,int entityClass,byte[] bag,byte[] entity)
    {
        if(Chunk.CurrentSaveVersion!=47||snapshot==null||snapshot.Length<9||snapshot.Length>RebirthLogisticsSnapshotReservation.MaximumNativeBytes||bag==null||entity==null||
            snapshot[0]!=116||snapshot[1]!=116||snapshot[2]!=99||snapshot[3]!=0||BitConverter.ToUInt32(snapshot,4)!=47)return false;
        try
        {
            using(var stream=new MemoryStream(snapshot,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);stream.Position=8;
                if(reader.ReadInt32()!=x||reader.ReadInt32()!=0||reader.ReadInt32()!=z)return false;Skip(stream,8);
                for(int i=0;i<64;i++)if(Flag(reader)){Skip(stream,Flag(reader)?1024:1);if(Flag(reader))Skip(stream,3072);}
                Channel(reader,stream,1);Skip(stream,256+256+32+256+1536+2);
                int custom=reader.ReadUInt16();
                for(int i=0;i<custom;i++){int length=StringLength(reader);if(length>4096)return false;Skip(stream,length+8);Flag(reader);Skip(stream,reader.ReadUInt16());}
                Skip(stream,768);Channel(reader,stream,1);Channel(reader,stream,1);Channel(reader,stream,2);Channel(reader,stream,6);Channel(reader,stream,2);Flag(reader);
                int count=reader.ReadInt32();if(count<0||count>4096)return false;bool found=false;
                for(int i=0;i<count;i++)
                {
                    var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);if(native.id!=id)continue;
                    if(found||native.entityClass!=entityClass||native.bagData==null||native.entityData==null||!native.bagData.ToArray().SequenceEqual(bag)||!native.entityData.ToArray().SequenceEqual(entity))return false;found=true;
                }
                return found;
            }
        }
        catch{return false;}
    }
    private static bool Flag(BinaryReader reader){byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static void Channel(BinaryReader reader,Stream stream,int bytes){for(int i=0;i<64;i++)Skip(stream,Flag(reader)?bytes:1024*bytes);}
    private static void Skip(Stream stream,int count){if(count<0||stream.Position>stream.Length-count)throw new EndOfStreamException();stream.Position+=count;}
    private static int StringLength(BinaryReader reader){uint value=0;for(int i=0;i<5;i++){byte item=reader.ReadByte();if(i==4&&(item&240)!=0)throw new InvalidDataException();value|=(uint)(item&127)<<(7*i);if((item&128)==0)return checked((int)value);}throw new InvalidDataException();}
}

internal sealed class RebirthLogisticsBackpackPublication:IDisposable
{
    private static readonly List<RebirthLogisticsBackpackPublication> pending=new List<RebirthLogisticsBackpackPublication>();
    private static bool installed;
    private RebirthLogisticsDeathCustody.Accepted original;private World world;private Chunk chunk;private Func<bool> current;
    private RebirthStationSnapshotEvidence.Request request;private byte[] bag,entity;private DateTime expires;private bool closed;
    private RebirthLogisticsSnapshotReservation budget;
    private IDisposable bagAllocation,markerAllocation,markerSpanAllocation,entityAllocation;
    private string markerKey;private ChunkCustomData marker;private byte[] markerBytes,markerSpan;private long markerOffset=-1;
    private string extraMarkerKey;private ChunkCustomData extraMarker;private byte[] extraMarkerBytes,extraMarkerSpan;private long extraMarkerOffset=-1;private IDisposable extraMarkerAllocation,extraMarkerSpanAllocation;
    private bool Current()=>!closed&&ThreadManager.IsMainThread()&&expires>DateTime.UtcNow&&current?.Invoke()==true&&original!=null&&!original.Ambiguous&&
        ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&ReferenceEquals(world.GetEntity(original.EntityId),original.Backpack)&&
        original.Backpack.RefPlayerId==original.NativeOwnerEntity&&original.Backpack.addedToChunk&&original.Backpack.chunkPosAddedEntityTo.x==chunk.X&&original.Backpack.chunkPosAddedEntityTo.z==chunk.Z&&
        ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&!chunk.InProgressUnloading&&
        BagMatches(original.Backpack,bag)&&(markerKey==null||chunk.ChunkCustomData.dict.TryGetValue(markerKey,out var live)&&ReferenceEquals(live,marker)&&
            marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&marker.data.SequenceEqual(markerBytes))&&
        (extraMarkerKey==null||chunk.ChunkCustomData.dict.TryGetValue(extraMarkerKey,out var extra)&&ReferenceEquals(extra,extraMarker)&&extra.TriggerWriteDataDelegate==null&&extra.expiresInWorldTime==ulong.MaxValue&&!extra.isSavedToNetwork&&extra.data!=null&&extra.data.SequenceEqual(extraMarkerBytes));
    internal bool IsCurrent()=>Current();
    private static byte[] BagImage(EntityBackpack backpack)
    {if(backpack?.bag==null)return null;using(var blob=StreamUtils.ToBlob(writer=>backpack.bag.Write(writer,StreamModeWrite.ToClient)))return blob?.ToArray();}
    private sealed class BagStream:Stream
    {
        private readonly byte[] expected;private long count;
        internal BagStream(byte[] expected=null){this.expected=expected;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] bytes,int offset,int length){if(bytes==null||offset<0||length<0||offset>bytes.Length-length||count>1048576L-length||expected!=null&&count>expected.Length-length)throw new IOException();if(expected!=null)for(int i=0;i<length;i++)if(bytes[offset+i]!=expected[(int)count+i])throw new IOException();count+=length;}
        public override void WriteByte(byte value){if(count>=1048576||expected!=null&&(count>=expected.Length||expected[(int)count]!=value))throw new IOException();count++;}
        public override void Flush(){}public override int Read(byte[] bytes,int offset,int length)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private static long BagLength(EntityBackpack backpack)
    {using(var stream=new BagStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);backpack.bag.Write(writer,StreamModeWrite.ToClient);writer.Flush();return stream.Length;}}
    internal static bool NativeBagWithinBound(EntityBackpack backpack,int maximum)
    {try{long size=BagLength(backpack);return size>0&&size<=maximum;}catch{return false;}}
    private static bool BagMatches(EntityBackpack backpack,byte[] expected)
    {try{if(expected==null||backpack?.bag==null)return false;using(var stream=new BagStream(expected))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);backpack.bag.Write(writer,StreamModeWrite.ToClient);writer.Flush();return stream.Length==expected.Length;}}catch{return false;}}
    private sealed class NativeBodyAllocation:IDisposable
    {
        internal RebirthLogisticsSnapshotReservation Account;internal IDisposable Bytes;
        public void Dispose(){Bytes?.Dispose();Bytes=null;Account?.Dispose();Account=null;}
    }
    internal static IDisposable ReserveNativeBody(EntityBackpack backpack,RebirthLogisticsTransferJournal.Record original,Func<bool> current)
    {
        var result=new NativeBodyAllocation();bool handed=false;
        try
        {
            result.Account=RebirthLogisticsSnapshotReservation.TryOpenRetainedRead(original,current);
            if(result.Account==null||backpack==null)return null;
            long bagBytes=BagLength(backpack),bodyBytes;
            using(var counter=new BagStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(counter);backpack.Write(writer,StreamModeWrite.ToClient);writer.Flush();bodyBytes=counter.Length;}
            if(bodyBytes>ushort.MaxValue)return null;
            long required=Math.Max(2L*1024*1024,checked(8*(bagBytes+bodyBytes+Encoding.UTF8.GetByteCount(backpack.spawnByName??string.Empty)+4096L)));
            if(!result.Account.TryReserveAllocation(required,out result.Bytes))return null;
            handed=true;return result;
        }
        catch{return null;}
        finally{if(!handed)result.Dispose();}
    }
    internal static bool CanSerializePlanned(EntityBackpack backpack,ItemStack[] planned,RebirthLogisticsTransferJournal.Record original,Func<bool> current)
    {using(var allocation=ReserveNativeBody(backpack,original,current))return allocation!=null&&CapturePlanImage(backpack)?.Fits(planned)==true;}
    internal sealed class PlanImage
    {
        private readonly byte[] frame;
        internal PlanImage(byte[] frame){this.frame=frame;}
        // Internal immutable alias: callers must never mutate canonical native
        // frames. Avoid retaining duplicate native bag/body graphs per parcel.
        internal byte[] FrameReference=>frame;
        internal byte[] OriginalFrame()=>frame==null?null:(byte[])frame.Clone();
        private bool Parts(out byte[] before,out byte[] body)
        {
            before=null;body=null;if(frame==null||frame.Length>131072)return false;
            var native=new EntityCreationData();using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
            before=native.bagData?.ToArray();body=native.entityData?.ToArray();return before!=null&&body!=null;
        }
        internal bool TryBuild(ItemStack[] planned,out byte[] encoded)
        {
            encoded=null;if(frame==null||!Fits(planned))return false;
            try
            {
                if(!Parts(out var before,out var body))return false;
                Bag detached;
                using(var stream=new MemoryStream(before,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
                {reader.SetBaseStream(stream);detached=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);}
                for(int slot=0;slot<planned.Length;slot++)detached.SetSlot(slot,planned[slot].Clone());
                byte[] after;using(var blob=StreamUtils.ToBlob(writer=>detached.Write(writer,StreamModeWrite.ToClient)))after=blob.ToArray();
                var entity=new EntityCreationData();
                using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
                {reader.SetBaseStream(stream);entity.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
                var tail=new byte[body.Length-before.Length+after.Length];Buffer.BlockCopy(body,0,tail,0,body.Length-before.Length);Buffer.BlockCopy(after,0,tail,body.Length-before.Length,after.Length);
                if(tail.Length>ushort.MaxValue)return false;
                entity.bagData=new MemoryStream(after,false);entity.entityData=new MemoryStream(tail,false);
                using(var blob=StreamUtils.ToBlob(writer=>entity.write(writer,StreamModeWrite.Persistency)))encoded=blob.ToArray();
                return encoded!=null&&encoded.Length<=131072;
            }
            catch{encoded=null;return false;}
        }
        internal bool Fits(ItemStack[] planned)
    {
        try
        {
            if(!Parts(out var before,out var body))return false;
            // Exact target EntityBackpack.Write appends its native Bag.Write
            // last. Verify that span instead of assuming a fixed base size.
            if(before==null||body==null||body.Length<before.Length||body.Length>ushort.MaxValue||!body.Skip(body.Length-before.Length).SequenceEqual(before))return false;
            Bag detached;
            using(var stream=new MemoryStream(before,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {reader.SetBaseStream(stream);detached=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);if(stream.Position!=stream.Length)return false;}
            if(detached?.SlotCount!=planned.Length)return false;
            for(int slot=0;slot<planned.Length;slot++){if(planned[slot]==null)return false;detached.SetSlot(slot,planned[slot].Clone());}
            if(!RebirthLogisticsDeathPartition.Same(detached.ItemGrid.items,planned))return false;
            using(var after=new BagStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(after);detached.Write(writer,StreamModeWrite.ToClient);writer.Flush();return (long)body.Length-before.Length+after.Length<=ushort.MaxValue;}
        }
        catch{return false;}
    }
    }
    internal static PlanImage CapturePlanImage(EntityBackpack backpack)
    {
        try
        {
            if(backpack?.bag?.ItemGrid?.items==null||!RebirthLogisticsPhysicalLossWitness.OriginalBodyFitsNative(backpack))return null;
            // Chunk.write constructs ECD with its default ToClient inner bag/
            // body mode, then writes the outer ECD in Persistency mode.
            var native=new EntityCreationData(backpack);
            byte[] frame;using(var blob=StreamUtils.ToBlob(writer=>native.write(writer,StreamModeWrite.Persistency)))frame=blob.ToArray();
            if(frame.Length>131072)return null;var result=new PlanImage(frame);
            return result.Fits(backpack.bag.ItemGrid.items)?result:null;
        }
        catch{return null;}
    }
    internal static PlanImage ReadSavedPlanImage(byte[] frame,ItemStack[] expectedBefore)
    {
        try
        {
            if(frame==null||frame.Length>131072||expectedBefore==null)return null;
            var native=new EntityCreationData();
            using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return null;}
            var bag=native.bagData?.ToArray();if(bag==null)return null;
            using(var stream=new MemoryStream(bag,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);var detached=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);
                if(stream.Position!=stream.Length||!RebirthLogisticsDeathPartition.Same(detached?.ItemGrid?.items,expectedBefore))return null;
            }
            var result=new PlanImage(frame);return result.Fits(expectedBefore)?result:null;
        }
        catch{return null;}
    }
    internal static bool ValidateSpawn(RebirthLogisticsNativeFrame frame,int id,int entityClass,int owner,UnityEngine.Vector3 position,ItemStack[] before,bool[] locks)
    {using(var bytes=frame?.Open())return bytes!=null&&TryReadSpawn(bytes.Bytes,id,entityClass,owner,position,before,locks,out _);}
    internal static bool TryReadSpawn(RebirthLogisticsNativeFrame frame,int id,int entityClass,int owner,UnityEngine.Vector3 position,ItemStack[] before,bool[] locks,out EntityCreationData creation,out RebirthLogisticsNativeFrame.Materialization materialization)
    {
        creation=null;materialization=frame?.Open();if(materialization!=null&&TryReadSpawn(materialization.Bytes,id,entityClass,owner,position,before,locks,out creation))return true;
        materialization?.Dispose();materialization=null;return false;
    }
    internal static bool TryReadSpawn(byte[] frame,int id,int entityClass,int owner,UnityEngine.Vector3 position,ItemStack[] before,bool[] locks,out EntityCreationData creation)
    {
        creation=null;
        try
        {
            if(frame==null||ReadSavedPlanImage(frame,before)==null||locks?.Length!=before.Length)return false;
            var native=new EntityCreationData();using(var stream=new MemoryStream(frame,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);native.read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
            var bag=native.bagData.ToArray();var body=native.entityData.ToArray();
            if(native.id!=id||native.entityClass!=entityClass||(native.pos.x!=position.x||native.pos.y!=position.y||native.pos.z!=position.z)||body.Length<bag.Length+4||BitConverter.ToInt32(body,body.Length-bag.Length-4)!=owner)return false;
            using(var stream=new MemoryStream(bag,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {reader.SetBaseStream(stream);var detached=Bag.Read(reader,XUiC_ItemStack.StackLocationTypes.Backpack,StreamModeRead.Persistency,null);
                if(stream.Position!=stream.Length||Enumerable.Range(0,locks.Length).Any(i=>detached.ItemGrid.IsLocked(i)!=locks[i]))return false;}
            creation=native;return true;
        }
        catch{return false;}
    }
    internal static RebirthLogisticsBackpackPublication Begin(RebirthLogisticsDeathCustody.Accepted original,Func<bool> originalCurrent,string exactMarkerKey=null,string exactAdditionalMarkerKey=null)
    {
        if(!installed||!ThreadManager.IsMainThread()||original?.World?.IsRemote()!=false||original.Backpack==null||original.Ambiguous||originalCurrent?.Invoke()!=true||
            !original.Backpack.addedToChunk||!ReferenceEquals(GameManager.Instance?.World,original.World)||!ReferenceEquals(original.World.GetEntity(original.EntityId),original.Backpack))return null;
        foreach(var stale in pending.ToArray())if(stale.expires<=DateTime.UtcNow||!ReferenceEquals(stale.world,original.World))stale.Dispose();if(pending.Count>=4)return null;
        var world=original.World;var chunk=world.GetChunkSync(original.Backpack.chunkPosAddedEntityTo.x,original.Backpack.chunkPosAddedEntityTo.z) as Chunk;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(chunk==null||chunk.InProgressUnloading||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory)||
            !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained))return null;
        string directory=Path.GetFullPath(regions.saveDirectory);var access=regions.regionFileAccess;
        var result=new RebirthLogisticsBackpackPublication{original=original,world=world,chunk=chunk,expires=DateTime.UtcNow.AddSeconds(30)};
        bool handedOff=false;
        try
        {
            result.current=()=>originalCurrent()&&ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
            result.budget=RebirthLogisticsSnapshotReservation.TryOpen(retained,result.current);if(result.budget==null)return null;
            long length=BagLength(original.Backpack);if(length<1||!result.budget.TryReserveAllocation(2*length,out var temporary))return null;
            using(temporary)
            {
                var image=BagImage(original.Backpack);if(image==null||image.LongLength!=length||!result.budget.TryReserveAllocation(length,out result.bagAllocation))return null;
                result.bag=image;
            }
            if(exactMarkerKey!=null)
            {
                if(exactMarkerKey.Length>256||!chunk.ChunkCustomData.dict.TryGetValue(exactMarkerKey,out var marker)||marker.data==null||marker.data.Length>4096||
                    !result.budget.TryReserveAllocation(marker.data.Length,out result.markerAllocation))return null;
                result.markerKey=exactMarkerKey;result.marker=marker;result.markerBytes=(byte[])marker.data.Clone();
            }
            if(exactAdditionalMarkerKey!=null)
            {
                if(exactMarkerKey==null||exactAdditionalMarkerKey==exactMarkerKey||exactAdditionalMarkerKey.Length>256||!chunk.ChunkCustomData.dict.TryGetValue(exactAdditionalMarkerKey,out var extra)||extra.data==null||extra.data.Length>4096||
                    !result.budget.TryReserveAllocation(extra.data.Length,out result.extraMarkerAllocation))return null;
                result.extraMarkerKey=exactAdditionalMarkerKey;result.extraMarker=extra;result.extraMarkerBytes=(byte[])extra.data.Clone();
            }
            if(!result.Current())return null;
            result.request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,result.Current,original.Backpack,budget:result.budget);if(result.request==null)return null;pending.Add(result);
            if(!System.Threading.Monitor.TryEnter(regions.saveLock,0))return null;
            try
            {
                if(!RebirthLogisticsParcelBaselines.CanAdmitNativeSnapshot(regions))return null;
                lock(chunk){if(!result.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)return null;chunk.InProgressSaving=true;}
                try{if(!result.Current())return null;regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
                if(!RebirthLogisticsParcelBaselines.NativeQueueFits(regions))return null;
                handedOff=true;return result;
            }
            finally{System.Threading.Monitor.Exit(regions.saveLock);}
        }
        catch{return null;}
        finally{if(!handedOff)result.Dispose();}
    }
    internal bool Saved(out string digest)
    {
        digest=null;IDisposable allocation=null;
        try
        {
        if(!Current()||entity==null||!RebirthStationSnapshotEvidence.HasPublished(request)||!RebirthStationSnapshotEvidence.TryCopy(request,out var bytes,out allocation)||
            !RebirthLogisticsEntity47.Contains(bytes,chunk.X,chunk.Z,original.EntityId,original.EntityClass,bag,entity)||
            markerKey!=null&&!SpanMatches(bytes,markerOffset,markerSpan)||extraMarkerKey!=null&&!SpanMatches(bytes,extraMarkerOffset,extraMarkerSpan))return false;
        // The positive native region/ECD/span checks above establish the save.
        // Name the stable published custody rather than unrelated changing
        // entities elsewhere in the same chunk, so exact original retries keep
        // the same proof while that native custody remains unchanged.
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(original.Operation.ToByteArray());writer.Write(RebirthLogisticsTransferJournal.WorldKey(original.World));writer.Write(original.Owner);writer.Write(original.Creation);writer.Write(original.OriginalDeaths);
            writer.Write(original.OriginalOwnerEntity);writer.Write(original.EntityId);writer.Write(original.EntityClass);writer.Write(chunk.X);writer.Write(chunk.Z);
            writer.Write(bag.Length);writer.Write(bag);writer.Write(markerBytes?.Length??0);if(markerBytes!=null)writer.Write(markerBytes);if(extraMarkerBytes!=null){writer.Write(extraMarkerKey);writer.Write(extraMarkerBytes.Length);writer.Write(extraMarkerBytes);}writer.Flush();
            using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
        return Current();
        }
        finally{allocation?.Dispose();}
    }
    public void Dispose(){if(closed)return;closed=true;request?.Dispose();bag=null;entity=null;markerBytes=null;markerSpan=null;bagAllocation?.Dispose();entityAllocation?.Dispose();markerAllocation?.Dispose();markerSpanAllocation?.Dispose();extraMarkerBytes=null;extraMarkerSpan=null;extraMarkerAllocation?.Dispose();extraMarkerSpanAllocation?.Dispose();budget?.Dispose();budget=null;pending.Remove(this);}
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.death.publication.v1");
        try{RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Write));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(MarkerWrite));ModEvents.GameUpdate.RegisterHandler(Update);installed=true;}catch{harmony.UnpatchSelf();throw;}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {foreach(var item in pending.ToArray())try{if(!item.Current())item.Dispose();}catch{item.Dispose();}}
    // Actual published canonical ECD only; detached capture is not proof.
    // Caller owns the copied frame allocation until its notice is released.
    internal bool TryCopyPublishedFrame(out byte[] frame,out IDisposable frameAllocation)
    {
        frame=null;frameAllocation=null;IDisposable snapshotAllocation=null,decodeAllocation=null;bool handed=false;
        try
        {
            if(!Saved(out _)||budget==null||!budget.TryReserveAllocation(262144,out frameAllocation)||
                !RebirthStationSnapshotEvidence.TryCopy(request,out var snapshot,out snapshotAllocation)||
                !budget.TryReserveAllocation(checked(4L*snapshot.Length+1048576),out decodeAllocation)||
                !RebirthLogisticsParcelPreview.TryReadEntity(snapshot,chunk.X,chunk.Z,original.EntityId,original.EntityClass,out frame))return false;
            if(!Current())return false;handed=true;return true;
        }
        finally{decodeAllocation?.Dispose();snapshotAllocation?.Dispose();if(!handed){frame=null;frameAllocation?.Dispose();frameAllocation=null;}}
    }
    private static bool SpanMatches(byte[] bytes,long offset,byte[] span)
    {if(bytes==null||span==null||offset<8||offset>bytes.Length-span.Length)return false;for(int i=0;i<span.Length;i++)if(bytes[(int)offset+i]!=span[i])return false;return true;}
    [HarmonyPatch(typeof(ChunkCustomData),nameof(ChunkCustomData.Write))]
    private static class MarkerWrite
    {
        private static void Prefix(ChunkCustomData __instance,BinaryWriter _bw,out long __state)
        {
            __state=-1;
            try{foreach(var item in pending)if((ReferenceEquals(item.marker,__instance)||ReferenceEquals(item.extraMarker,__instance))&&item.Current())
            {__state=RebirthStationSnapshotEvidence.StartStationSpan(item.original.Backpack,_bw.BaseStream);break;}}catch{__state=-1;}
        }
        private static void Postfix(ChunkCustomData __instance,BinaryWriter _bw,long __state)
        {
            if(__state<8)return;
            try{foreach(var item in pending)if((ReferenceEquals(item.marker,__instance)||ReferenceEquals(item.extraMarker,__instance))&&item.Current())
            {
                var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;
                if(length<1||length>8192||!stream.CanRead||!stream.CanSeek)return;
                if(item.budget==null||!item.budget.TryReserveAllocation(length,out var allocation))return;
                try
                {
                    var bytes=new byte[(int)length];try{stream.Position=__state;int copied=0;while(copied<bytes.Length){int count=stream.Read(bytes,copied,bytes.Length-copied);if(count<=0)return;copied+=count;}}finally{stream.Position=end;}
                    if(ReferenceEquals(item.extraMarker,__instance))
                    {item.extraMarkerSpan=null;item.extraMarkerSpanAllocation?.Dispose();item.extraMarkerOffset=__state;item.extraMarkerSpan=bytes;item.extraMarkerSpanAllocation=allocation;allocation=null;}
                    else{item.markerSpan=null;item.markerSpanAllocation?.Dispose();item.markerOffset=__state;item.markerSpan=bytes;item.markerSpanAllocation=allocation;allocation=null;}
                }
                finally{allocation?.Dispose();}
            }}catch{ /* Exact same-snapshot observation only. */ }
        }
    }
    [HarmonyPatch(typeof(EntityBackpack),nameof(EntityBackpack.Write))]
    private static class Write
    {
        private static void Prefix(EntityBackpack __instance,PooledBinaryWriter _bw,out long __state)=>__state=RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream);
        private static void Postfix(EntityBackpack __instance,PooledBinaryWriter _bw,long __state)
        {
            if(__state<0)return;
            try{foreach(var item in pending)if(ReferenceEquals(item.original.Backpack,__instance)&&item.Current())
            {
                var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;if(length<=0||length>ushort.MaxValue||!stream.CanRead||!stream.CanSeek)return;
                if(item.budget==null||!item.budget.TryReserveAllocation(length,out var allocation))return;
                try
                {
                    var bytes=new byte[(int)length];try{stream.Position=__state;int copied=0;while(copied<bytes.Length){int count=stream.Read(bytes,copied,bytes.Length-copied);if(count<=0)return;copied+=count;}}finally{stream.Position=end;}
                    item.entity=null;item.entityAllocation?.Dispose();item.entity=bytes;item.entityAllocation=allocation;allocation=null;
                }
                finally{allocation?.Dispose();}
            }}catch{ /* Native serialization result remains authoritative. */ }
        }
    }
}

// Positive native accepted-spawn observation only. This does not authorize a
// debit, credit, refund, or settlement until original death/file custody matches.
internal static class RebirthLogisticsDeathCustody
{
    internal sealed class Accepted
    {
        internal World World;internal EntityBackpack Backpack;internal string Owner,Creation;
        internal Guid Operation;internal int OriginalDeaths,OriginalOwnerEntity,EntityId,EntityClass;
        internal int LiveOwnerEntity=-1;internal int NativeOwnerEntity=>LiveOwnerEntity>=0?LiveOwnerEntity:OriginalOwnerEntity;
        internal ItemStack[] NativeBag;internal bool[] NativeLocks;internal string PacketBagHash,PacketEntityHash;internal int PacketClass;internal UnityEngine.Vector3 PacketPosition;
        internal ItemStack[] RemainingBag,RemainingBelt;internal int OwnerPhase;internal string OwnerFileProof;
        internal bool Ambiguous;
    }
    private sealed class Frame
    {
        internal Frame Previous;internal NetPackageRequestToSpawnEntity Packet;internal ClientInfo Sender;
        internal EntityPlayer Player;internal World World;internal RebirthLogisticsTransferJournal.Record Record;
    }
    [ThreadStatic] private static Frame current;
    private static World evidenceWorld;
    private static readonly Dictionary<Guid,Accepted> accepted=new Dictionary<Guid,Accepted>();
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.accepted.death.v1");
        try
        {
            foreach(var type in new[]{typeof(RebirthLogisticsDeathPacketScope),typeof(RebirthLogisticsDeathSpawnObservation),typeof(LocalDeathBoundary),typeof(LocalDeathOriginalBoundary),typeof(NativePhysicalSelection),typeof(NativePhysicalDegrade),typeof(NativePhysicalRespawn),typeof(NativePhysicalFactory),typeof(NativePhysicalRequest),typeof(NativePhysicalConstructor),typeof(NativePhysicalBuilderAdd),typeof(NativeFatalBoundary)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
            RebirthLogisticsBackpackPublication.Install();ModEvents.SavePlayerData.RegisterHandler(OnSaved);installed=true;
        }
        catch{harmony.UnpatchSelf();throw;}
    }
    private static void OnSaved(ref ModEvents.SSavePlayerDataData data)
    {
        try{AdvancedFarmingWaterExchangeOriginal.OnNativeOwnerSaved(data.ClientInfo);}catch{/* Each domain derives its own typed original authority. */}
        try{RebirthPlacedOriginal.OnNativeOwnerSaved(data.ClientInfo);}catch{/* Placement derives only its own typed original authority. */}
        RebirthLogisticsNativeDeathReceipt.ObserveSaved(data.ClientInfo);
        try
        {
            var world=GameManager.Instance?.World;var sender=data.ClientInfo;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||data.PlayerDataFile==null||data.PlayerDataFile.id!=sender.entityId||
                !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||
                !RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var operation,out int phase,out float held,out var saved)||held!=1f||phase>4||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||!TryGet(original,out var custody)||
                saved.id!=sender.entityId||saved.deaths!=original.Deaths+1||custody.NativeOwnerEntity!=sender.entityId||
                !ReferenceEquals(world.GetEntity(custody.EntityId),custody.Backpack)||
                !RebirthLogisticsDeathPartition.Same(custody.NativeBag,custody.Backpack.bag?.ItemGrid?.items))return;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            if(!RebirthLogisticsNativeDeathReceipt.PartitionReady(original)||!RebirthLogisticsNativeDeathReceipt.AcceptedPhysicalMatches(original,custody)||!RebirthLogisticsDeathPartition.TryExpected(original,phase,out var expected)||
                !RebirthLogisticsNativeDeathReceipt.ConservedMaterial(original,phase,custody.NativeBag,bag,belt))return;
            string proof;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
            {
                writer.Write(RebirthLogisticsTransferJournal.ImmutablePlanDigest(original));writer.Write(saved.deaths);writer.Write(phase);
                foreach(var image in new[]{saved.bagData,saved.inventoryData,saved.buffData}){var bytes=image.ToArray();writer.Write(bytes.Length);writer.Write(bytes);}writer.Flush();
                using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();
            }
            if(custody.OwnerFileProof!=null&&(custody.OwnerPhase!=phase||custody.OwnerFileProof!=proof||
                !RebirthLogisticsDeathPartition.Same(custody.RemainingBag,bag)||!RebirthLogisticsDeathPartition.Same(custody.RemainingBelt,belt)))return;
            custody.OwnerPhase=phase;custody.OwnerFileProof=proof;custody.RemainingBag=ItemStack.Clone(bag);custody.RemainingBelt=ItemStack.Clone(belt);
            RebirthLogisticsDeathJournal.Prepare(original,custody,out _);
            // Durable death journal + same-snapshot native marker publication
            // must still succeed before this observation grants any effect.
        }
        catch{ /* Native final file remains authoritative; unknown is retained. */ }
    }
    // Reordered detached evidence can complete AFTER native5 was saved and
    // after a later life saved again. Use only the already-published exact
    // typed native5 receipt, never a supplied image or the new-life inventory.
    internal static void ReconcilePublishedPhysical(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||
                !fatal.PhysicalExpanded||fatal.Physical?.Stage!=5||fatal.Phase<0||fatal.OwnerFileProof?.Length!=64||
                !TryGet(original,out var custody)||custody.Ambiguous||!RebirthLogisticsNativeDeathReceipt.PhysicalPacketMatches(original,fatal,custody.OriginalOwnerEntity,custody.PacketBagHash,custody.PacketEntityHash)||
                !ReferenceEquals(world.GetEntity(custody.EntityId),custody.Backpack)||!RebirthLogisticsDeathPartition.Same(custody.NativeBag,custody.Backpack.bag?.ItemGrid?.items)||
                !RebirthLogisticsNativeDeathReceipt.AcceptedPhysicalMatches(original,custody)||
                !RebirthLogisticsNativeDeathReceipt.ConservedMaterial(original,fatal.Phase,custody.NativeBag,fatal.RemainingBag,fatal.RemainingBelt))return;
            if(custody.OwnerFileProof!=null)return; // Never replace an existing actual owner-file observation.
            custody.OwnerPhase=fatal.Phase;custody.OwnerFileProof=fatal.OwnerFileProof;custody.RemainingBag=ItemStack.Clone(fatal.RemainingBag);custody.RemainingBelt=ItemStack.Clone(fatal.RemainingBelt);
            RebirthLogisticsDeathJournal.Prepare(original,custody,out _);
        }
        catch{/* Exact original native body + historical native5 proof required. */}
    }
    [HarmonyPatch(typeof(EntityAlive),nameof(EntityAlive.SetDead))]
    private static class NativeFatalBoundary
    {
        private static void Prefix(EntityAlive __instance,out object __state)
        {
            __state=null;if(!(__instance is EntityPlayer player))return;
            try{__state=RebirthPlacedOriginalDeathPartition.BeforeTerminalNativeFatal(player);}catch{/* Independent B observation cannot change native death. */}
        }
        private static void Postfix(EntityAlive __instance,object __state)
        {
            if(!(__instance is EntityPlayer player))return;
            try{RebirthPlacedOriginalDeathPartition.AfterTerminalNativeFatal(player,__state);}catch{/* Actual native transition corroboration only. */}
            try{AdvancedFarmingWaterExchangeOriginal.OnNativeFatal(player);}catch{/* Readonly observer cannot change native death. */}
            try{RebirthPlacedOriginal.OnNativeFatal(player);}catch{/* Placement observation never changes native death. */}
            try{RebirthLogisticsHeaderAdmission.OnNativeFatal(player);}catch{/* Header lineage carries no native Before/payment authority. */}
            try{RebirthLogisticsPreAdmissionReceipt.OnNativeFatal(player);}catch{/* Pre-admission evidence never changes native death. */}
            try{RebirthLogisticsNativeDeathReceipt.ObserveFatal(player);}catch{/* Native death always proceeds. */}
        }
    }
    [HarmonyPatch(typeof(EntityPlayerLocal),"dropBackpack")]
    private static class LocalDeathBoundary
    {
        private static void Prefix(EntityPlayerLocal __instance,bool _isDying,out bool __state)
        {__state=false;try{RebirthLogisticsPhysicalLossWitness.BeforeDrop(__instance,_isDying);__state=RebirthLogisticsDeleteWitness.EnterDrop(__instance,_isDying);}catch{/* Native callback continues. */}}
        private static void Postfix(EntityPlayerLocal __instance,bool _isDying,bool __state)
        {if(_isDying&&!__state&&!RebirthLogisticsPhysicalLossWitness.OwnsEpisode(__instance))RebirthLogisticsOwnerReceipt.UploadDeathBoundary(__instance);}
        private static Exception Finalizer(EntityPlayerLocal __instance,bool __state,Exception __exception)
        {
            RebirthLogisticsDeleteWitness.AfterDrop(__instance,__state,__exception);
            RebirthLogisticsPhysicalLossWitness.AfterDrop(__instance,__exception);
            if(__state)RebirthLogisticsOwnerReceipt.UploadDeathBoundary(__instance);
            return __exception;
        }
    }
    [HarmonyPatch(typeof(EntityPlayerLocal),"dropItemOnDeath")]
    private static class LocalDeathOriginalBoundary
    {
        private static void Prefix(EntityPlayerLocal __instance,out object __state){try{RebirthPlacedOriginalDeathPartition.OnNativeDropStarting(__instance);}catch{/* Placement observation cannot alter native removal. */}RebirthLogisticsPhysicalLossWitness.Begin(__instance);__state=RebirthLogisticsDeleteWitness.Begin(__instance);}
        private static Exception Finalizer(EntityPlayerLocal __instance,object __state,Exception __exception){try{RebirthPlacedOriginalDeathPartition.OnNativeDropFinished(__instance,__exception);}catch{/* Preserve original native exception. */}RebirthLogisticsDeleteWitness.End(__state);RebirthLogisticsPhysicalLossWitness.End(__instance,__exception);return __exception;}
    }
    [HarmonyPatch]
    private static class NativePhysicalSelection
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach(var type in new[]{typeof(Bag),typeof(Inventory),typeof(Equipment)})
            {var method=AccessTools.Method(typeof(EntityPlayerLocal),"HandleRemoveRandomItems",new[]{type});if(method==null)throw new MissingMethodException();yield return method;}
        }
        private static void Prefix(EntityPlayerLocal __instance){try{RebirthPlacedOriginalDeathPartition.Selection(__instance);}catch{/* Typed observation only. */}RebirthLogisticsPhysicalLossWitness.Selection(__instance);}
    }
    [HarmonyPatch(typeof(EntityPlayerLocal),"degradeItemsOnDeath")]
    private static class NativePhysicalDegrade
    {private static void Prefix(EntityPlayerLocal __instance){try{RebirthPlacedOriginalDeathPartition.AfterRemoval(__instance);}catch{/* Typed observation only. */}RebirthLogisticsPhysicalLossWitness.AfterRemoval(__instance);}}
    [HarmonyPatch(typeof(EntityPlayerLocal),nameof(EntityPlayerLocal.Respawn),new[]{typeof(RespawnType)})]
    private static class NativePhysicalRespawn
    {private static void Prefix(EntityPlayerLocal __instance,RespawnType _reason){if(_reason==RespawnType.Died){try{RebirthPlacedOriginalDeathPartition.OnNativeBeforeRespawn(__instance);}catch{/* Each domain owns its final proof. */}RebirthLogisticsPhysicalLossWitness.BeforeRespawn(__instance);}}}
    [HarmonyPatch(typeof(EntityFactory),nameof(EntityFactory.CreateEntity),new[]{typeof(int),typeof(UnityEngine.Vector3)})]
    private static class NativePhysicalFactory
    {private static void Postfix(int _et,UnityEngine.Vector3 _transformPos,Entity __result){try{RebirthPlacedOriginalDeathPartition.Factory(_et,_transformPos,__result);}catch{/* Typed observation only. */}RebirthLogisticsPhysicalLossWitness.Factory(_et,_transformPos,__result);}}
    [HarmonyPatch(typeof(GameManager),nameof(GameManager.RequestToSpawnEntityServer))]
    private static class NativePhysicalRequest
    {private static bool Prefix(EntityCreationData _ecd){bool placement=RebirthPlacedOriginalDeathPartition.BeforeRequest(_ecd);bool logistics=RebirthLogisticsPhysicalLossWitness.AllowRequest(_ecd);return placement&&logistics;}}
    [HarmonyPatch(typeof(EntityCreationData),MethodType.Constructor,new[]{typeof(Entity),typeof(StreamModeWrite)})]
    private static class NativePhysicalConstructor
    {private static bool Prefix(EntityCreationData __instance,Entity _e,StreamModeWrite _eStreamMode){bool placement=RebirthPlacedOriginalDeathPartition.BeforeConstruction(__instance,_e,_eStreamMode);bool logistics=RebirthLogisticsPhysicalLossWitness.AllowConstruction(__instance,_e,_eStreamMode);return placement&&logistics;}}
    [HarmonyPatch(typeof(Bag),nameof(Bag.AddItem))]
    private static class NativePhysicalBuilderAdd
    {
        private sealed class State{internal object Logistics,Placement;}
        private static void Prefix(Bag __instance,ItemStack _itemStack,out State __state)
        {__state=new State();try{__state.Placement=RebirthPlacedOriginalDeathPartition.BeforeBuilderAdd(__instance,_itemStack);}catch{/* Typed observation only. */}__state.Logistics=RebirthLogisticsPhysicalLossWitness.BeforeAdd(__instance,_itemStack);}
        private static void Postfix(Bag __instance,ItemStack _itemStack,bool __result,State __state)
        {try{RebirthPlacedOriginalDeathPartition.AfterBuilderAdd(__instance,_itemStack,__result,__state?.Placement);}catch{/* Preserve native result. */}RebirthLogisticsPhysicalLossWitness.AfterAdd(__instance,_itemStack,__result,__state?.Logistics);}
    }
    internal static object Enter(NetPackageRequestToSpawnEntity packet,World world)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet?.Sender==null)return null;
        var sender=packet.Sender;var player=world.GetEntity(sender.entityId) as EntityPlayer;
        if(player==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var identity)||
            !RebirthLogisticsOwnerReceipt.TrySavedOperation(identity,out var operation,out int phase,out float held,out var saved)||held!=1f||phase>4||
            !RebirthLogisticsTransferJournal.TryRead(identity.StorageKey,operation,out var record)||record.Phase>=4||
            RebirthCamperCharacterAccess.CurrentCreation(player)!=record.Creation||saved.deaths<record.Deaths||saved.deaths>record.Deaths+1)return null;
        var frame=new Frame{Previous=current,Packet=packet,Sender=sender,Player=player,World=world,Record=record};current=frame;return frame;
    }
    internal static object Enter(NetPackageRequestToSpawnEntity packet,World world,out bool allowNative)
    {
        allowNative=true;var token=Enter(packet,world);
        try
        {
        if(!(token is Frame frame))
        {
            var sender=packet?.Sender;
            if(ThreadManager.IsMainThread()&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&sender!=null&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)&&RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var heldOperation,out _,out float held,out _)&&held==1f)
            {
                if(RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,heldOperation,out var heldOriginal)&&RebirthLogisticsNativeInvocationJournal.MustRefuseRetainedOldReference(packet,world,heldOriginal))allowNative=false;
                if(RebirthLogisticsNativeInvocationJournal.IsNativeBody(packet,sender.entityId))allowNative=false;
            }
            return token;
        }
        if(RebirthLogisticsNativeInvocationJournal.MustRefuseRetainedOldReference(packet,world,frame.Record)){allowNative=false;return token;}
        if(!RebirthLogisticsNativeInvocationJournal.IsNativeBody(packet,frame.Sender.entityId))return token;
        allowNative=false;
        // Already attempted original is refused even if its later fatal proof
        // is temporarily unavailable. Absence never authorizes another spawn.
        if(RebirthLogisticsNativeInvocationJournal.HasAttempt(frame.Record)){allowNative=false;return token;}
        if(!RebirthLogisticsNativeDeathReceipt.TryRead(frame.Record,out var fatal)||fatal.Mode<1||fatal.Mode>5)return token;
        allowNative=false;
        allowNative=RebirthLogisticsNativeInvocationJournal.TryBegin(frame.Record,fatal,frame.Player,packet);return token;
        }
        catch{Leave(token);throw;}
    }
    internal static void Leave(object token)
    {if(token is Frame frame&&ReferenceEquals(current,frame))current=frame.Previous;}
    internal static void Observe(Entity entity)
    {
        var frame=current;if(frame==null||!(entity is EntityBackpack backpack)||frame.Packet.ecd==null||frame.Packet.ecd.id<0||backpack.entityId!=frame.Packet.ecd.id||backpack.RefPlayerId!=frame.Sender.entityId||
            !ReferenceEquals(GameManager.Instance?.World,frame.World)||!ReferenceEquals(frame.World.GetEntity(backpack.entityId),backpack)||
            !ReferenceEquals(frame.World.GetEntity(frame.Player.entityId),frame.Player)||!ReferenceEquals(frame.Packet.Sender,frame.Sender)||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(frame.Sender.entityId),frame.Sender)||
            !frame.Packet.ValidEntityIdForSender(backpack.RefPlayerId)||RebirthCamperCharacterAccess.CurrentCreation(frame.Player)!=frame.Record.Creation||
            backpack.bag?.ItemGrid?.items==null||backpack.bag.SlotCount<1||backpack.bag.SlotCount>256)return;
        if(!ReferenceEquals(evidenceWorld,frame.World)){accepted.Clear();evidenceWorld=frame.World;}
        if(accepted.TryGetValue(frame.Record.Operation,out var previous))
        {if(!ReferenceEquals(previous.Backpack,backpack)){RebirthLogisticsAcceptedBodyJournal.Invalidate(previous.Backpack);previous.Ambiguous=true;}return;}
        if(accepted.Count>=128)
            foreach(var old in accepted.Values.ToArray())if(RebirthLogisticsTransferJournal.TryRead(old.Owner,old.Operation,out var terminal)&&terminal.Phase==4)
            {RebirthLogisticsAcceptedBodyJournal.Retire(terminal);PruneTerminal(terminal);}
        if(accepted.Count>=128)return;
        if(!RebirthLogisticsBackpackPublication.NativeBagWithinBound(backpack,131072))return;
        accepted.Add(frame.Record.Operation,new Accepted{World=frame.World,Backpack=backpack,Owner=frame.Record.Owner,Creation=frame.Record.Creation,
            Operation=frame.Record.Operation,OriginalDeaths=frame.Record.Deaths,OriginalOwnerEntity=backpack.RefPlayerId,EntityId=backpack.entityId,EntityClass=backpack.entityClass,
            NativeBag=ItemStack.Clone(backpack.bag.ItemGrid.items),NativeLocks=Enumerable.Range(0,backpack.bag.SlotCount).Select(slot=>backpack.bag.ItemGrid.IsLocked(slot)).ToArray(),
            PacketBagHash=RebirthLogisticsPhysicalLossWitness.BlobHash(frame.Packet.ecd.bagData),PacketEntityHash=RebirthLogisticsPhysicalLossWitness.BlobHash(frame.Packet.ecd.entityData),PacketClass=frame.Packet.ecd.entityClass,PacketPosition=frame.Packet.ecd.pos});
        RebirthLogisticsAcceptedBodyJournal.Observe(frame.Record,accepted[frame.Record.Operation]);
    }
    internal static bool TryGet(RebirthLogisticsTransferJournal.Record record,out Accepted evidence)
    {
        evidence=null;if(record==null)return false;var world=GameManager.Instance?.World;
        if(!ReferenceEquals(evidenceWorld,world)){accepted.Clear();evidenceWorld=world;}
        if(!accepted.TryGetValue(record.Operation,out var value)||!ReferenceEquals(world?.GetEntity(value.EntityId),value.Backpack))
        {
            if(accepted.Count>=128||!RebirthLogisticsAcceptedBodyJournal.TryAdopt(record,out value))return false;
            accepted[record.Operation]=value;
        }
        if(value.Ambiguous||value.Owner!=record.Owner||value.Creation!=record.Creation||value.OriginalDeaths!=record.Deaths||RebirthLogisticsTransferJournal.WorldKey(value.World)!=record.World)return false;
        evidence=value;return true;
    }
    internal static void ObserveLiveReference(RebirthLogisticsTransferJournal.Record original,EntityBackpack body,int nativeOwner)
    {
        if(!ThreadManager.IsMainThread()||body?.world?.IsRemote()!=false||body.RefPlayerId!=nativeOwner||!ReferenceEquals(GameManager.Instance?.World,body.world))return;
        if(accepted.TryGetValue(original.Operation,out var value)&&value.Owner==original.Owner&&value.Creation==original.Creation&&ReferenceEquals(value.Backpack,body))value.LiveOwnerEntity=nativeOwner;
    }
    internal static void PruneTerminal(RebirthLogisticsTransferJournal.Record original)
    {
        if(original?.Phase!=4||!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)||saved.Phase!=4||RebirthLogisticsTransferJournal.Encode(saved)!=RebirthLogisticsTransferJournal.Encode(original))return;
        if(accepted.TryGetValue(original.Operation,out var value)&&value.Owner==original.Owner&&value.Creation==original.Creation&&value.OriginalDeaths==original.Deaths)accepted.Remove(original.Operation);
    }
    internal static void ResetWorld(World world)
    {if(ReferenceEquals(evidenceWorld,world))return;accepted.Clear();current=null;evidenceWorld=world;}
    internal static Accepted[] Snapshot()=>ReferenceEquals(evidenceWorld,GameManager.Instance?.World)?accepted.Values.ToArray():new Accepted[0];
}

// Pure exact metadata conservation. Unsupported loss/degradation/equipment or
// shrink projections stay unknown; no guessed replacement quantity is minted.
internal static class RebirthLogisticsDeathPartition
{
    internal static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((value,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(value,b[index])).All(value=>value);
    internal static bool TryExpected(RebirthLogisticsTransferJournal.Record original,int phase,out ItemStack[] expected)
    {
        expected=null;if(original?.OriginalOwnerBag==null||phase<0||phase>4||phase>=3&&original.Outcome==0||phase==4&&(original.Outcome!=1&&original.Outcome!=2||string.IsNullOrEmpty(original.OwnerDebitProof)))return false;
        var stage=phase<2?original.BeforeOwner:phase==2?original.DebitedOwner:original.Outcome==2?original.BeforeOwner:original.AfterOwner;
        if(stage==null||stage.Length!=original.OriginalOwnerBag.Length||original.BeforeOwner?.Length!=stage.Length||original.AfterOwner?.Length!=stage.Length)return false;
        expected=ItemStack.Clone(original.OriginalOwnerBag);
        for(int i=0;i<stage.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(original.BeforeOwner[i],original.AfterOwner[i]))expected[i]=stage[i].Clone();
        return true;
    }
    internal static bool SurvivedUnchanged(RebirthLogisticsTransferJournal.Record original,int phase,ItemStack[] bag,ItemStack[] belt)
        =>TryExpected(original,phase,out var expected)&&Same(expected,bag)&&Same(original.OriginalOwnerBelt,belt);
    internal static bool Conserved(ItemStack[] expected,ItemStack[] belt,ItemStack[] dropped,ItemStack[] remaining,ItemStack[] remainingBelt,int maximumVectorSlots=256)
    {
        try
        {
            if(maximumVectorSlots<1||maximumVectorSlots>607)return false;
            var counts=new Dictionary<string,long>(StringComparer.Ordinal);int budget=16*1024*1024;
            bool Add(ItemStack[] slots,int sign)
            {
                if(slots==null||slots.Length>maximumVectorSlots)return false;
                foreach(var stack in slots)
                {
                    if(stack==null)return false;if(stack.IsEmpty()){if(stack.count!=0)return false;continue;}
                    if(stack.count<1||stack.count>ushort.MaxValue||stack.itemValue?.ItemClass==null)return false;
                    string image=RebirthNativeItemCodec.Encode(stack.itemValue);if(string.IsNullOrEmpty(image)||image.Length>196608)return false;
                    budget-=image.Length;if(budget<0)return false;
                    counts.TryGetValue(image,out long quantity);counts[image]=checked(quantity+sign*(long)stack.count);
                }
                return true;
            }
            return Add(expected,1)&&Add(belt,1)&&Add(dropped,-1)&&Add(remaining,-1)&&Add(remainingBelt,-1)&&counts.Values.All(value=>value==0);
        }
        catch{return false;}
    }
    internal static bool TryPlan(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death,out ItemStack[] after)
    {after=null;if(!TryPlanAll(original,death,out var plans))return false;after=plans[0];return true;}
    internal static bool TryParcelLimit(RebirthLogisticsTransferJournal.Record original,int ownerPhase,out int limit)
    {
        limit=0;try
        {
            if(original==null||original.Outcome<1||original.Outcome>3)return false;
            if(ownerPhase==3||ownerPhase==4||original.Outcome==3)return true;if(ownerPhase!=2)return false;
            var target=original.Outcome==2?original.BeforeOwner:original.AfterOwner;
            if(target==null||target.Length!=original.DebitedOwner?.Length||target.Length>169)return false;
            long total=0;
            for(int slot=0;slot<target.Length;slot++)
            {
                var final=target[slot];var paid=original.DebitedOwner[slot];if(final==null||paid==null)return false;
                int amount=(final.IsEmpty()?0:final.count)-(paid.IsEmpty()?0:paid.count);if(amount<0)return false;if(amount==0)continue;
                if(final.itemValue?.ItemClass==null)return false;int maximum=Math.Min(ushort.MaxValue,Math.Max(1,final.itemValue.ItemClass.Stacknumber.Value));
                total=checked(total+((long)amount+maximum-1)/maximum);
            }
            // Each additional body can hold at least one individually native-
            // serializable original stack. Never derive an entity budget from
            // packet counts, or from an unbounded overstacked source quantity.
            if(total>target.Length)return false;limit=(int)total;return true;
        }
        catch{return false;}
    }
    // A frozen distribution is validated against the original paid delta,
    // not regenerated with today's native entity header/serialization state.
    // Each native cell permits only the original positive top-up/insertion.
    internal static bool TryOwed(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death,out ItemStack[] owed)
    {
        owed=null;if(original?.BeforeOwner==null||death==null)return false;
        if(death.PreservedOwed!=null)
        {
            if(death.PreservedOwed.Length<1||death.PreservedOwed.Length>2*original.OriginalOwnerBag.Length+original.OriginalOwnerBelt.Length+13||death.PreservedOwed.Length>RebirthLogisticsDeathJournal.MaximumParcels||death.PreservedOwed.Any(item=>item==null||item.count<0||item.count>ushort.MaxValue||item.IsEmpty()&&item.count!=0))return false;
            owed=ItemStack.Clone(death.PreservedOwed);return true;
        }
        owed=Enumerable.Range(0,original.BeforeOwner.Length).Select(_=>ItemStack.Empty.Clone()).ToArray();
        if(death.OwnerPhase==3||death.OwnerPhase==4||original.Outcome==3)return true;if(death.OwnerPhase!=2)return false;
        var target=original.Outcome==2?original.BeforeOwner:original.AfterOwner;
        if(target?.Length!=original.DebitedOwner?.Length||target.Length!=owed.Length)return false;
        for(int i=0;i<target.Length;i++)
        {
            var final=target[i];var paid=original.DebitedOwner[i];if(final==null||paid==null)return false;
            int amount=(final.IsEmpty()?0:final.count)-(paid.IsEmpty()?0:paid.count);if(amount<0||amount>ushort.MaxValue)return false;
            if(!paid.IsEmpty()){var a=final.Clone();var b=paid.Clone();a.count=b.count=1;if(!RebirthStationGridIngredients.IsSameStackSnapshot(a,b))return false;}
            if(amount>0){owed[i]=final.Clone();owed[i].count=amount;}
        }
        return true;
    }
    internal static bool TryParcelLimit(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death,out int limit)
    {
        limit=0;if(death.PreservedOwed==null)return TryParcelLimit(original,death.OwnerPhase,out limit);
        if(!TryOwed(original,death,out var owed))return false;long total=0;
        foreach(var item in owed)if(!item.IsEmpty())
        {if(item.itemValue?.ItemClass==null)return false;int maximum=Math.Min(ushort.MaxValue,Math.Max(1,item.itemValue.ItemClass.Stacknumber.Value));total+=((long)item.count+maximum-1)/maximum;}
        // The atom count is an upper bound on bodies needed, not a minimum:
        // many atoms fit one ordinary native bag. Old oversized physical stacks
        // under a smaller current authored max must get a bounded packing
        // attempt rather than immediate refusal. Native grids/byte/region
        // bounds still apply, and an impossible complete plan never spawns.
        limit=(int)Math.Min(total,RebirthLogisticsDeathJournal.MaximumParcels);return true;
    }
    internal static bool ValidateFrozenPlan(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death)
    {
        try
        {
            if(original==null||death==null||original.Outcome==0||death.Outcome!=original.Outcome||!TryParcelLimit(original,death,out int limit)||death.Parcels.Count>limit)return false;
            var before=new List<ItemStack[]>{death.Before};var after=new List<ItemStack[]>{death.After};var locks=new List<bool[]>{death.Locks};
            foreach(var parcel in death.Parcels){before.Add(parcel.Before);after.Add(parcel.After);locks.Add(parcel.Locks);}
            var counts=new Dictionary<string,long>(StringComparer.Ordinal);int budget=16*1024*1024;
            bool Add(ItemStack item,int sign)
            {
                if(item==null||item.count<0||item.count>ushort.MaxValue)return false;if(item.IsEmpty())return item.count==0;
                string key=RebirthNativeItemCodec.Encode(item.itemValue);if(string.IsNullOrEmpty(key)||key.Length>196608)return false;
                budget-=key.Length;if(budget<0)return false;counts.TryGetValue(key,out long quantity);counts[key]=checked(quantity+sign*(long)item.count);return true;
            }
            for(int body=0;body<before.Count;body++)
            {
                if(before[body]==null||before[body].Length<1||before[body].Length>256||after[body]?.Length!=before[body].Length||locks[body]?.Length!=before[body].Length)return false;
                for(int slot=0;slot<before[body].Length;slot++)
                {
                    var old=before[body][slot];var next=after[body][slot];if(!Add(old,1)||!Add(next,-1))return false;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(old,next))continue;
                    if(death.PreservedOwed==null&&(death.OwnerPhase==3||death.OwnerPhase==4||original.Outcome==3)||next.IsEmpty()||next.count<1||next.itemValue?.ItemClass==null||next.count>Math.Max(1,next.itemValue.ItemClass.Stacknumber.Value))return false;
                    if(old.IsEmpty()){if(locks[body][slot])return false;}
                    else {var a=old.Clone();var b=next.Clone();a.count=b.count=1;if(next.count<old.count||!RebirthStationGridIngredients.IsSameStackSnapshot(a,b))return false;}
                }
            }
            if(!TryOwed(original,death,out var owed))return false;
            foreach(var item in owed)if(!item.IsEmpty()&&!Add(item,1))return false;
            return counts.Values.All(value=>value==0);
        }
        catch{return false;}
    }
    internal static bool TryPlanAll(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death,out ItemStack[][] plans,Func<int,ItemStack[],bool> fits=null)
    {
        plans=null;
        try
        {
            if(original==null||death?.Before==null||death.Locks?.Length!=death.Before.Length||original.Outcome==0||original.Outcome!=death.Outcome)return false;
            var before=new List<ItemStack[]>{death.Before};var locks=new List<bool[]>{death.Locks};
            if(!TryParcelLimit(original,death,out int limit)||death.Parcels.Count>limit)return false;
            foreach(var parcel in death.Parcels){if(parcel.Before==null||parcel.Locks?.Length!=parcel.Before.Length||parcel.Before.Any(item=>item==null||!item.IsEmpty()))return false;before.Add(parcel.Before);locks.Add(parcel.Locks);}
            plans=before.Select(ItemStack.Clone).ToArray();
            if(fits!=null&&plans.Where((value,index)=>!fits(index,value)).Any()){plans=null;return false;}
            if(!TryOwed(original,death,out var owed))return false;
            foreach(var value in owed)
            {
                if(value.IsEmpty())continue;var incoming=value.Clone();
                for(int destination=0;destination<plans.Length&&incoming.count>0;destination++)
                {
                var after=plans[destination];
                for(int i=0;i<after.Length&&incoming.count>0;i++)
                {
                    var current=after[i];if(current==null||current.IsEmpty())continue;
                    var a=current.Clone();var b=incoming.Clone();a.count=b.count=1;
                    if(!RebirthStationGridIngredients.IsSameStackSnapshot(a,b)||!current.CanStackPartlyWith(incoming,out int capacity)||capacity<1)continue;
                    int move=Math.Min(incoming.count,capacity);var next=current.Clone();next.count=checked(next.count+move);if(next.count>ushort.MaxValue)return false;
                    after[i]=next;if(fits!=null&&!fits(destination,after)){after[i]=current;continue;}incoming.count-=move;
                }
                int maximum=Math.Min(ushort.MaxValue,Math.Max(1,incoming.itemValue.ItemClass.Stacknumber.Value));
                for(int i=0;i<after.Length&&incoming.count>0;i++)if(!locks[destination][i]&&after[i]!=null&&after[i].IsEmpty())
                {int move=Math.Min(maximum,incoming.count);var next=incoming.Clone();next.count=move;var previous=after[i];after[i]=next;if(fits!=null&&!fits(destination,after)){after[i]=previous;break;}incoming.count-=move;}
                }
                if(incoming.count!=0){plans=null;return false;}
            }
            return true;
        }
        catch{plans=null;return false;}
    }
}

// Original accepted death custody only. Native backpack/owner remain physical
// authorities; this bounded journal retains their observed immutable binding.
internal static class RebirthLogisticsDeathJournal
{
    internal const int MaximumBytes=64*1024*1024,MaximumParcels=607; // 2*169 original bag +256 original belt +13 equipment; no native grid enlargement.
    internal sealed class Parcel
    {
        internal Guid Incarnation;internal int EntityId,EntityClass,X,Z,State;internal float PosX,PosY,PosZ;
        internal ItemStack[] Before,After;internal bool[] Locks;internal string BeforeProof,FinalProof;
        internal RebirthLogisticsNativeFrame Spawn;
    }
    internal sealed class Record
    {
        internal Guid Operation,Incarnation;internal string Owner,World,Creation,Binding,OwnerFileProof,BeforeProof,FinalProof;
        internal int Deaths,OwnerPhase,OwnerEntity,EntityId,EntityClass,X,Z,State,Outcome,AnchorX,AnchorZ;internal bool HasRecoveryAnchor;
        internal ItemStack[] Before,RemainingBag,RemainingBelt,After;internal bool[] Locks;
        internal RebirthLogisticsNativeFrame Spawn;internal ItemStack[] PreservedOwed;
        internal readonly List<Parcel> Parcels=new List<Parcel>();
    }
    internal static string MarkerKey(Record record)=>"RebirthLogisticsBackpackV1:"+record.EntityId+":"+record.Operation.ToString("N");
    internal static string MarkerKey(Record record,Parcel parcel)=>"RebirthLogisticsBackpackV1:"+parcel.EntityId+":"+record.Operation.ToString("N");
    private static bool Hex(string value,int length)=>value?.Length==length&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    private static string Image(ItemStack[] slots)
    {if(slots==null||slots.Length<1||slots.Length>256)throw new InvalidDataException();return Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(slots));}
    private static ItemStack[] Slots(string text)
    {if(string.IsNullOrEmpty(text)||text.Length>4194304)throw new InvalidDataException();var value=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(text));if(Image(value)!=text)throw new InvalidDataException();return value;}
    private static string OwedImage(ItemStack[] items)
    {if(items==null||items.Length<1||items.Length>MaximumParcels)throw new InvalidDataException();var text=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));if(text.Length>MaximumBytes)throw new InvalidDataException();return text;}
    private static ItemStack[] OwedSlots(string text)
    {if(string.IsNullOrEmpty(text)||text.Length>MaximumBytes)throw new InvalidDataException();var items=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(text));if(OwedImage(items)!=text)throw new InvalidDataException();return items;}
    private static string PathFor(string owner,Guid operation)
    {if(!Hex(owner,64)||operation==Guid.Empty)throw new InvalidDataException();return Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsDeaths",owner,operation.ToString("N")+".xml");}
    internal static string Hash(ItemStack[] slots)=>RebirthLogisticsOriginalTransport.Digest(Image(slots));
    internal static string PlanKey(Record value)
    {
        var root=Write(value);root.SetAttributeValue("state",2);root.SetAttributeValue("finalProof",string.Empty);
        foreach(var parcel in root.Elements("parcel")){parcel.SetAttributeValue("state",0);parcel.SetAttributeValue("beforeProof",string.Empty);parcel.SetAttributeValue("finalProof",string.Empty);}
        return RebirthLogisticsOriginalTransport.Digest(root.ToString(SaveOptions.DisableFormatting));
    }
    private static XElement Write(Record value)
    {
        if(value==null)throw new InvalidDataException();
        if(value.Spawn==null&&!value.Parcels.Any(parcel=>parcel?.Spawn!=null))return WriteCore(value);
        using(var scope=RebirthLogisticsNativeFrame.BeginRead(value.Owner,value.Operation,value.World,value.Binding))
        {if(scope==null)throw new InvalidDataException();return WriteCore(value);}
    }
    private static XElement WriteCore(Record value)
    {
        if(value==null||value.Operation==Guid.Empty||value.Incarnation==Guid.Empty||!Hex(value.Owner,64)||!Guid.TryParseExact(value.World,"N",out var world)||world==Guid.Empty||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||!Hex(value.Binding,64)||!Hex(value.OwnerFileProof,64)||value.Deaths<0||value.OwnerPhase<0||value.OwnerPhase>4||
            value.OwnerEntity<0||value.EntityId<0||value.State<0||value.State>4||value.Outcome<0||value.Outcome>3||
            value.Locks==null||value.Before==null||value.Locks.Length!=value.Before.Length||
            value.State<2&&(value.Outcome!=0||value.After!=null)||value.State<1&&!string.IsNullOrEmpty(value.BeforeProof)||value.State<3&&!string.IsNullOrEmpty(value.FinalProof)||
            value.State>=1&&!Hex(value.BeforeProof,64)||value.State>=2&&(value.Outcome==0||value.After==null)||value.State>=3&&!Hex(value.FinalProof,64))throw new InvalidDataException();
        if(value.HasRecoveryAnchor&&(Math.Abs((long)value.X-value.AnchorX)>1||Math.Abs((long)value.Z-value.AnchorZ)>1||value.Before.Any(item=>item==null||!item.IsEmpty())))throw new InvalidDataException();
        if(value.Parcels.Count>MaximumParcels||value.Parcels.Count>0&&(value.State<2||value.PreservedOwed==null&&(value.OwnerPhase!=2||value.Outcome==3))||
            value.Parcels.Select(item=>item.Incarnation).Distinct().Count()!=value.Parcels.Count||value.Parcels.Select(item=>item.EntityId).Distinct().Count()!=value.Parcels.Count||value.Parcels.Any(item=>item.EntityId==value.EntityId))throw new InvalidDataException();
        var root=new XElement("logisticsDeath",new XAttribute("version",2),new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("incarnation",value.Incarnation.ToString("N")),
            new XAttribute("owner",value.Owner),new XAttribute("world",value.World),new XAttribute("creation",value.Creation),new XAttribute("binding",value.Binding),
            new XAttribute("ownerFileProof",value.OwnerFileProof),new XAttribute("beforeProof",value.BeforeProof??string.Empty),new XAttribute("finalProof",value.FinalProof??string.Empty),
            new XAttribute("deaths",value.Deaths),new XAttribute("ownerPhase",value.OwnerPhase),new XAttribute("ownerEntity",value.OwnerEntity),new XAttribute("entity",value.EntityId),
            new XAttribute("class",value.EntityClass),new XAttribute("x",value.X),new XAttribute("z",value.Z),new XAttribute("state",value.State),new XAttribute("outcome",value.Outcome),
            new XElement("before",Image(value.Before)),new XElement("remainingBag",Image(value.RemainingBag)),new XElement("remainingBelt",Image(value.RemainingBelt)),
            new XElement("after",value.After==null?string.Empty:Image(value.After)),new XElement("locks",Convert.ToBase64String(value.Locks.Select(item=>(byte)(item?1:0)).ToArray())));
        if(value.HasRecoveryAnchor)root.Add(new XAttribute("anchorX",value.AnchorX),new XAttribute("anchorZ",value.AnchorZ));
        if(value.PreservedOwed!=null)root.Add(new XElement("owed",OwedImage(value.PreservedOwed)));
        foreach(var parcel in value.Parcels)
        {
            if(parcel.Incarnation==Guid.Empty||parcel.EntityId<0||parcel.State<0||parcel.State>3||parcel.Before==null||parcel.Before.Any(item=>item==null||!item.IsEmpty())||
                parcel.After?.Length!=parcel.Before.Length||parcel.Locks?.Length!=parcel.Before.Length||parcel.State>=2&&!Hex(parcel.BeforeProof,64)||parcel.State==3&&!Hex(parcel.FinalProof,64)||
                parcel.State<2&&!string.IsNullOrEmpty(parcel.BeforeProof)||parcel.State<3&&!string.IsNullOrEmpty(parcel.FinalProof)||
                value.State>=3&&parcel.State!=3||float.IsNaN(parcel.PosX)||float.IsInfinity(parcel.PosX)||float.IsNaN(parcel.PosY)||float.IsInfinity(parcel.PosY)||float.IsNaN(parcel.PosZ)||float.IsInfinity(parcel.PosZ))throw new InvalidDataException();
            if(Math.Abs((long)parcel.X-(value.HasRecoveryAnchor?value.AnchorX:value.X))>1||Math.Abs((long)parcel.Z-(value.HasRecoveryAnchor?value.AnchorZ:value.Z))>1||World.toChunkXZ(Utils.Fastfloor(parcel.PosX))!=parcel.X||World.toChunkXZ(Utils.Fastfloor(parcel.PosZ))!=parcel.Z)throw new InvalidDataException();
            root.Add(new XElement("parcel",new XAttribute("incarnation",parcel.Incarnation.ToString("N")),new XAttribute("entity",parcel.EntityId),new XAttribute("class",parcel.EntityClass),
                new XAttribute("x",parcel.X),new XAttribute("z",parcel.Z),new XAttribute("state",parcel.State),new XAttribute("posX",parcel.PosX),new XAttribute("posY",parcel.PosY),new XAttribute("posZ",parcel.PosZ),
                new XAttribute("beforeProof",parcel.BeforeProof??string.Empty),new XAttribute("finalProof",parcel.FinalProof??string.Empty),
                new XElement("before",Image(parcel.Before)),new XElement("after",Image(parcel.After)),new XElement("locks",Convert.ToBase64String(parcel.Locks.Select(item=>(byte)(item?1:0)).ToArray()))));
            if(parcel.Spawn!=null)
            {
                if(!parcel.Spawn.Matches(value.Owner,value.Operation,value.World,value.Binding)||!RebirthLogisticsBackpackPublication.ValidateSpawn(parcel.Spawn,parcel.EntityId,parcel.EntityClass,value.OwnerEntity,new UnityEngine.Vector3(parcel.PosX,parcel.PosY,parcel.PosZ),parcel.Before,parcel.Locks))throw new InvalidDataException();
                root.Elements("parcel").Last().Add(new XElement("spawn",parcel.Spawn.Encoded));
            }
        }
        if(Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting))>MaximumBytes)throw new InvalidDataException();return root;
    }
    private static Record Read(string path)
    {
        var file=new FileInfo(path);if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader,LoadOptions.None);
        if(root.Name!="logisticsDeath"||root.Attributes().Count()!=19+(root.Attribute("anchorX")==null?0:2)||((string)root.Attribute("version")!="1"&&(string)root.Attribute("version")!="2")||root.Elements().Count()!=5+root.Elements("parcel").Count()+(root.Element("owed")==null?0:1))throw new InvalidDataException();
        byte[] locks=Convert.FromBase64String((string)root.Element("locks"));if(locks.Length<1||locks.Length>256||locks.Any(item=>item>1))throw new InvalidDataException();
        var value=new Record{HasRecoveryAnchor=root.Attribute("anchorX")!=null,AnchorX=root.Attribute("anchorX")==null?0:(int)root.Attribute("anchorX"),AnchorZ=root.Attribute("anchorZ")==null?0:(int)root.Attribute("anchorZ"),Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),Incarnation=Guid.ParseExact((string)root.Attribute("incarnation"),"N"),Owner=(string)root.Attribute("owner"),World=(string)root.Attribute("world"),
            Creation=(string)root.Attribute("creation"),Binding=(string)root.Attribute("binding"),OwnerFileProof=(string)root.Attribute("ownerFileProof"),BeforeProof=(string)root.Attribute("beforeProof"),FinalProof=(string)root.Attribute("finalProof"),
            Deaths=(int)root.Attribute("deaths"),OwnerPhase=(int)root.Attribute("ownerPhase"),OwnerEntity=(int)root.Attribute("ownerEntity"),EntityId=(int)root.Attribute("entity"),EntityClass=(int)root.Attribute("class"),X=(int)root.Attribute("x"),Z=(int)root.Attribute("z"),
            State=(int)root.Attribute("state"),Outcome=(int)root.Attribute("outcome"),Before=Slots((string)root.Element("before")),RemainingBag=Slots((string)root.Element("remainingBag")),RemainingBelt=Slots((string)root.Element("remainingBelt")),
            After=string.IsNullOrEmpty((string)root.Element("after"))?null:Slots((string)root.Element("after")),Locks=locks.Select(item=>item!=0).ToArray()};
        value.PreservedOwed=root.Element("owed")==null?null:OwedSlots((string)root.Element("owed"));
        foreach(var node in root.Elements("parcel"))
        {
            if(node.Attributes().Count()!=11||node.Elements().Count()!=(node.Element("spawn")==null?3:4))throw new InvalidDataException();
            byte[] parcelLocks=Convert.FromBase64String((string)node.Element("locks"));if(parcelLocks.Length<1||parcelLocks.Length>256||parcelLocks.Any(item=>item>1))throw new InvalidDataException();
            value.Parcels.Add(new Parcel{Incarnation=Guid.ParseExact((string)node.Attribute("incarnation"),"N"),EntityId=(int)node.Attribute("entity"),EntityClass=(int)node.Attribute("class"),X=(int)node.Attribute("x"),Z=(int)node.Attribute("z"),State=(int)node.Attribute("state"),
                PosX=(float)node.Attribute("posX"),PosY=(float)node.Attribute("posY"),PosZ=(float)node.Attribute("posZ"),BeforeProof=(string)node.Attribute("beforeProof"),FinalProof=(string)node.Attribute("finalProof"),
                Before=Slots((string)node.Element("before")),After=Slots((string)node.Element("after")),Spawn=RebirthLogisticsNativeFrame.Read(value.Owner,value.Operation,value.World,value.Binding,(string)node.Element("spawn")),Locks=parcelLocks.Select(item=>item!=0).ToArray()});
        }
        if((string)root.Attribute("version")=="1"){if(value.Parcels.Count!=0)throw new InvalidDataException();root.SetAttributeValue("version",2);}
        if(!XNode.DeepEquals(root,Write(value)))throw new InvalidDataException();return value;
    }
    internal static bool TryRead(RebirthLogisticsTransferJournal.Record original,out Record value)
    {
        value=null;
        try
        {
            if(original==null||!RebirthLogisticsNativeDeathReceipt.PartitionReady(original)||!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained))return false;
            var record=Read(PathFor(original.Owner,original.Operation));
            if(record.Operation!=original.Operation||record.Owner!=original.Owner||record.World!=original.World||record.Creation!=original.Creation||record.Deaths!=original.Deaths||
                record.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||!RebirthLogisticsDeathPartition.TryExpected(original,record.OwnerPhase,out var expected)||
                !(RebirthLogisticsNativeDeathReceipt.ConservedMaterial(original,record.OwnerPhase,record.Before,record.RemainingBag,record.RemainingBelt)||
                RebirthLogisticsNativeDeathReceipt.ConservedDeleted(original,record)))return false;
            if(record.HasRecoveryAnchor)
            {
                if(!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var anchorFatal)||record.OwnerFileProof!=anchorFatal.OwnerFileProof)return false;
                var anchor=World.toChunkXZ(new UnityEngine.Vector3(anchorFatal.X,anchorFatal.Y,anchorFatal.Z));if(record.AnchorX!=anchor.x||record.AnchorZ!=anchor.y)return false;
            }
            if(record.PreservedOwed!=null&&!RebirthLogisticsNativeDeathReceipt.ConservedPreserved(original,record))return false;
            if(record.After!=null)
            {
                if(!RebirthLogisticsDeathPartition.ValidateFrozenPlan(retained,record))return false;
            }
            value=record;return true;
        }
        catch{return false;}
    }
    internal static bool HasAnyCandidate(RebirthLogisticsTransferJournal.Record original)
    {
        string path=PathFor(original.Owner,original.Operation);
        return File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak");
    }
    internal static bool Prepare(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathCustody.Accepted accepted,out Record value)
    {
        value=null;
        try
        {
            if(!RebirthLogisticsNativeDeathReceipt.PartitionReady(original)||!RebirthLogisticsAcceptedBodyJournal.ReferenceReadyForSettlement(original))return false;
            if(!ThreadManager.IsMainThread()||original==null||accepted==null||accepted.Ambiguous||accepted.OwnerFileProof==null||accepted.Operation!=original.Operation||
                accepted.Owner!=original.Owner||accepted.Creation!=original.Creation||accepted.OriginalDeaths!=original.Deaths||accepted.World?.IsRemote()!=false||
                !ReferenceEquals(GameManager.Instance?.World,accepted.World)||!ReferenceEquals(accepted.World.GetEntity(accepted.EntityId),accepted.Backpack)||!accepted.Backpack.addedToChunk||
                !RebirthLogisticsDeathPartition.Same(accepted.NativeBag,accepted.Backpack.bag?.ItemGrid?.items)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original))return false;
            string path=PathFor(original.Owner,original.Operation);
            if(File.Exists(path))return TryRead(original,out value);
            if(File.Exists(path+".bak"))return false;
            var record=new Record{Operation=original.Operation,Incarnation=Guid.NewGuid(),Owner=original.Owner,World=original.World,Creation=original.Creation,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),
                Deaths=original.Deaths,OwnerPhase=accepted.OwnerPhase,OwnerEntity=accepted.NativeOwnerEntity,EntityId=accepted.EntityId,EntityClass=accepted.EntityClass,
                X=accepted.Backpack.chunkPosAddedEntityTo.x,Z=accepted.Backpack.chunkPosAddedEntityTo.z,OwnerFileProof=accepted.OwnerFileProof,
                Before=ItemStack.Clone(accepted.NativeBag),RemainingBag=ItemStack.Clone(accepted.RemainingBag),RemainingBelt=ItemStack.Clone(accepted.RemainingBelt),
                Locks=Enumerable.Range(0,accepted.NativeBag.Length).Select(index=>accepted.Backpack.bag.ItemGrid.IsLocked(index)).ToArray()};
            if(original.Outcome!=0&&RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(original,out var physicalOwed))record.PreservedOwed=physicalOwed;
            if(!RebirthLogisticsDeathPartition.TryExpected(original,record.OwnerPhase,out var expected)||!(RebirthLogisticsNativeDeathReceipt.ConservedMaterial(original,record.OwnerPhase,record.Before,record.RemainingBag,record.RemainingBelt)||
                RebirthLogisticsNativeDeathReceipt.ConservedDeleted(original,record)))return false;
            if(File.Exists(path+".tmp"))
            {
                // Preserve the initial incarnation from an interrupted exact
                // preparation. Actual accepted native body/file evidence is
                // required again; no cold guessed entity may adopt it.
                var pending=Read(path+".tmp");record.Incarnation=pending.Incarnation;
                if(!XNode.DeepEquals(Write(record),Write(pending)))return false;
                File.Move(path+".tmp",path);return TryRead(original,out value);
            }
            string text=Write(record).ToString(SaveOptions.DisableFormatting);Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(path+".tmp",path);return TryRead(original,out value);
        }
        catch{return false;}
    }
    internal static bool PrepareNativeRecovery(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeRecovery.Record planned)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthLogisticsNativeRecovery.TryRead(original,out var current)||
                !RebirthLogisticsNativeRecovery.Same(current,planned)||!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var fatal)||
                planned.FatalProof!=fatal.OwnerFileProof)return false;
            string path=PathFor(original.Owner,original.Operation);
            if(File.Exists(path))return TryRead(original,out var existing)&&existing.Incarnation==planned.Incarnation&&existing.EntityId==planned.EntityId;
            if(File.Exists(path+".bak"))return false;
            var record=RebirthLogisticsNativeRecovery.Projection(original,planned);record.Outcome=0;
            if(!RebirthLogisticsDeathPartition.TryExpected(original,record.OwnerPhase,out var expected)||!(RebirthLogisticsDeathPartition.Conserved(expected,original.OriginalOwnerBelt,record.Before,record.RemainingBag,record.RemainingBelt)||RebirthLogisticsNativeDeathReceipt.ConservedPreserved(original,record)))return false;
            if(File.Exists(path+".tmp"))
            {
                if(!XNode.DeepEquals(Write(record),Write(Read(path+".tmp"))))return false;
                File.Move(path+".tmp",path);return TryRead(original,out var recovered)&&recovered.Incarnation==planned.Incarnation;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=Encoding.UTF8.GetBytes(Write(record).ToString(SaveOptions.DisableFormatting));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(path+".tmp",path);
            return TryRead(original,out var saved)&&saved.Incarnation==planned.Incarnation;
        }
        catch{return false;}
    }
    internal static bool Publish(RebirthLogisticsTransferJournal.Record original,Record value,Func<bool> current)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||current?.Invoke()!=true||!TryRead(original,out var before)||value.State<before.State||value.State>before.State+1)return false;
            var old=Write(before);var next=Write(value);var normalized=new XElement(next);
            foreach(string field in new[]{"state","outcome","beforeProof","finalProof"})normalized.SetAttributeValue(field,old.Attribute(field).Value);
            normalized.Element("after").Value=old.Element("after").Value;
            if(before.PreservedOwed==null&&value.PreservedOwed!=null)
            {if(before.State!=1||value.State!=2||!RebirthLogisticsNativeDeathReceipt.ConservedPreserved(original,value))return false;normalized.Element("owed").Remove();}
            if(before.Parcels.Count==0&&value.Parcels.Count>0){if(before.State!=1||value.State!=2)return false;normalized.Elements("parcel").Remove();}
            else
            {
                if(before.Parcels.Count!=value.Parcels.Count)return false;
                var oldParcels=old.Elements("parcel").ToArray();var newParcels=normalized.Elements("parcel").ToArray();
                for(int i=0;i<before.Parcels.Count;i++)
                {
                    var a=before.Parcels[i];var b=value.Parcels[i];if(b.State<a.State||b.State>a.State+1||!string.IsNullOrEmpty(a.BeforeProof)&&b.BeforeProof!=a.BeforeProof||!string.IsNullOrEmpty(a.FinalProof)&&b.FinalProof!=a.FinalProof)return false;
                    foreach(string field in new[]{"state","beforeProof","finalProof"})newParcels[i].SetAttributeValue(field,oldParcels[i].Attribute(field).Value);
                }
            }
            if(!XNode.DeepEquals(old,normalized)||before.Outcome!=0&&value.Outcome!=before.Outcome||before.After!=null&&!RebirthLogisticsDeathPartition.Same(before.After,value.After)||
                !string.IsNullOrEmpty(before.BeforeProof)&&value.BeforeProof!=before.BeforeProof||!string.IsNullOrEmpty(before.FinalProof)&&value.FinalProof!=before.FinalProof)return false;
            string path=PathFor(value.Owner,value.Operation);
            if(File.Exists(path+".tmp")){if(!XNode.DeepEquals(next,Write(Read(path+".tmp"))))return false;}
            else using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(next.ToString(SaveOptions.DisableFormatting));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            return current()&&RebirthDurableFileCommit.TryPublish(path+".tmp",path,out _)&&current()&&XNode.DeepEquals(next,Write(Read(path)));
        }
        catch{return false;}
    }
    internal static bool HasTemporary(RebirthLogisticsTransferJournal.Record original)
    {try{return File.Exists(PathFor(original.Owner,original.Operation)+".tmp");}catch{return true;}}
    internal static bool TryReadPreparedDistribution(RebirthLogisticsTransferJournal.Record original,out Record value)
    {
        value=null;try
        {
            if(!TryRead(original,out var before)||before.State!=1)return false;
            var pending=Read(PathFor(original.Owner,original.Operation)+".tmp");
            if(pending.State!=2||pending.Parcels.Any(parcel=>parcel.State!=0)||!RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,pending))return false;
            // Publish still performs full immutable origin comparison against
            // the positive native BEFORE record before adopting this plan.
            value=pending;return true;
        }
        catch{return false;}
    }
    internal static byte[] Marker(Record record,bool effect)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write((byte)1);writer.Write(record.Incarnation.ToByteArray());writer.Write(record.Operation.ToByteArray());writer.Write(record.Owner);writer.Write(record.World);writer.Write(record.Binding);
            writer.Write(record.Deaths);writer.Write(record.OwnerEntity);writer.Write(record.EntityId);writer.Write(record.EntityClass);writer.Write(Hash(record.Before));writer.Write(effect);
            writer.Write(effect?Hash(record.After):string.Empty);writer.Flush();return stream.ToArray();
        }
    }
    internal static byte[] Marker(Record record,Parcel parcel,bool effect)
    {
        var image=new Record{Incarnation=parcel.Incarnation,Operation=record.Operation,Owner=record.Owner,World=record.World,Binding=record.Binding,Deaths=record.Deaths,OwnerEntity=record.OwnerEntity,EntityId=parcel.EntityId,EntityClass=parcel.EntityClass,Before=parcel.Before,After=parcel.After};
        return Marker(image,effect);
    }
}

// Only the exact accepted physical backpack and its durable original journal
// can acquire this custody. A matching entity number alone grants nothing.
internal static class RebirthLogisticsDeathSettlement
{
    internal sealed class Lease:IDisposable
    {
        internal RebirthLogisticsTransferJournal.Record Original;internal RebirthLogisticsDeathJournal.Record Death;
        internal RebirthLogisticsDeathCustody.Accepted Accepted;internal Chunk Chunk;internal ChunkCustomData Marker;
        internal RebirthLogisticsBackpackPublication Publication;internal bool Closed;
        private RebirthLogisticsParcelCoordinator capacity;
        internal bool ApplyingOwned;private DateTime nextPlan;
        internal bool Current()
        {
            try
            {
                if(Closed||!ThreadManager.IsMainThread()||Original==null||Death==null||Accepted?.World?.IsRemote()!=false||
                    !ReferenceEquals(GameManager.Instance?.World,Accepted.World)||!ReferenceEquals(Accepted.World.GetEntity(Death.EntityId),Accepted.Backpack)||
                    !RebirthLogisticsParcelReferenceJournal.TryEffective(Original,Death,null,Accepted.Backpack,out int nativeOwner)||Accepted.Backpack.RefPlayerId!=nativeOwner||Accepted.Backpack.entityClass!=Death.EntityClass||Accepted.Ambiguous||
                    !Accepted.Backpack.addedToChunk||Accepted.Backpack.chunkPosAddedEntityTo.x!=Death.X||Accepted.Backpack.chunkPosAddedEntityTo.z!=Death.Z||
                    Chunk==null||Chunk.InProgressUnloading||!ReferenceEquals(Accepted.World.GetChunkSync(Death.X,Death.Z),Chunk)||
                    !Chunk.ChunkCustomData.dict.TryGetValue(RebirthLogisticsDeathJournal.MarkerKey(Death),out var marker)||!ReferenceEquals(marker,Marker)||
                    marker.TriggerWriteDataDelegate!=null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||
                    !RebirthLogisticsTransferJournal.TryRead(Original.Owner,Original.Operation,out var current)||RebirthLogisticsTransferJournal.ImmutablePlanDigest(current)!=Death.Binding||
                    Accepted.Backpack.bag?.ItemGrid?.Length!=Death.Locks.Length||Enumerable.Range(0,Death.Locks.Length).Any(index=>Accepted.Backpack.bag.ItemGrid.IsLocked(index)!=Death.Locks[index]))return false;
                byte[] before=RebirthLogisticsDeathJournal.Marker(Death,false);
                return marker.data!=null&&(marker.data.SequenceEqual(before)||Death.After!=null&&marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Death,true)));
            }
            catch{return false;}
        }
        internal bool ObserveBefore()
        {
            if(!Current()||!Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Death,false))||
                !RebirthLogisticsDeathPartition.Same(Accepted.Backpack.bag?.ItemGrid?.items,Death.Before))return false;
            if(Death.State>=1)return true;
            if(Publication==null){Accepted.LiveOwnerEntity=Accepted.Backpack.RefPlayerId;Publication=RebirthLogisticsBackpackPublication.Begin(Accepted,Current,RebirthLogisticsDeathJournal.MarkerKey(Death));}
            if(Publication==null||!Publication.Saved(out string proof))return false;
            Death.BeforeProof=proof;Death.State=1;
            if(!RebirthLogisticsDeathJournal.Publish(Original,Death,Current)){Reload();return false;}
            Publication.Dispose();Publication=null;return true;
        }
        internal bool Reload()=>RebirthLogisticsTransferJournal.TryRead(Original.Owner,Original.Operation,out Original)&&RebirthLogisticsDeathJournal.TryRead(Original,out Death);
        private bool PlanDelivery()
        {
            if(capacity==null)capacity=new RebirthLogisticsParcelCoordinator(this);
            return capacity.TryWarm();
        }
        private bool IndividuallyEncodable(RebirthLogisticsBackpackPublication.PlanImage primary,int primarySlots,RebirthLogisticsBackpackPublication.PlanImage extra,int extraSlots)
        {
            var target=Original.Outcome==2?Original.BeforeOwner:Original.AfterOwner;
            for(int slot=0;slot<target.Length;slot++)
            {
                var final=target[slot];var paid=Original.DebitedOwner[slot];int amount=(final.IsEmpty()?0:final.count)-(paid.IsEmpty()?0:paid.count);if(amount<=0)continue;
                var atom=final.Clone();atom.count=Math.Min(amount,Math.Min(ushort.MaxValue,Math.Max(1,atom.itemValue.ItemClass.Stacknumber.Value)));
                var a=Enumerable.Range(0,primarySlots).Select(_=>ItemStack.Empty.Clone()).ToArray();var b=Enumerable.Range(0,extraSlots).Select(_=>ItemStack.Empty.Clone()).ToArray();
                a[0]=atom.Clone();b[0]=atom.Clone();if(!primary.Fits(a)&&!extra.Fits(b))return false;
            }
            return true;
        }
        internal bool Deliver()
        {
            if(!Reload()||!Current()||Original.Phase<2||Original.Outcome==0||Death.State<1)return false;
            if(Death.State==1)
            {
                if(DateTime.UtcNow<nextPlan)return false;nextPlan=DateTime.UtcNow.AddMilliseconds(100);
                if(!PlanDelivery())return false;
            }
            if(Death.State==2)
            {
                if(capacity==null)capacity=new RebirthLogisticsParcelCoordinator(this);
                if(!capacity.TryFrozen())return false;
                // Every empty native parcel and its incarnation must be saved
                // before the first original owed item is written anywhere.
                foreach(var id in Death.Parcels.Select(value=>value.Incarnation).ToArray())
                    if(!TryParcel(this,id,out var parcel)||!parcel.ObserveBefore())return false;
                if(!RebirthLogisticsBackpackPublication.CanSerializePlanned(Accepted.Backpack,Death.After,Original,Current))return false;
                foreach(var id in Death.Parcels.Select(value=>value.Incarnation).ToArray())
                    if(!TryParcel(this,id,out var parcel)||!RebirthLogisticsBackpackPublication.CanSerializePlanned(parcel.Backpack,parcel.Record.After,Original,Current))return false;
                foreach(var id in Death.Parcels.Select(value=>value.Incarnation).ToArray())
                    if(!TryParcel(this,id,out var parcel)||!parcel.Deliver())return false;
                bool receipt=Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Death,true));
                var grid=Accepted.Backpack.bag.ItemGrid;
                if(!receipt)
                {
                    if(!ApplyingOwned&&!RebirthLogisticsDeathPartition.Same(grid.items,Death.Before))return false;
                    if(!RebirthLogisticsOwnerReceipt.Matches(grid.items,Death.Before,Death.After,true,false))return false;
                    ApplyingOwned=true; // Only this uninterrupted, exclusive original can resume partial native setters.
                    for(int i=0;i<Death.Before.Length;i++)
                    {
                        if(RebirthStationGridIngredients.IsSameStackSnapshot(Death.Before[i],Death.After[i])||RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],Death.After[i]))continue;
                        if(!Current()||!RebirthLogisticsOwnerReceipt.Matches(grid.items,Death.Before,Death.After,true,false)||permit!=null)return false;
                        var next=Death.After[i].Clone();var entry=new Permit{Grid=grid,Slot=i,Value=next,Current=Current};permit=entry;
                        try{Accepted.Backpack.bag.SetSlot(i,next);}finally{permit=null;}
                        if(!entry.Used||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],Death.After[i]))return false;
                    }
                    if(!Current()||!RebirthLogisticsDeathPartition.Same(grid.items,Death.After))return false;
                    // Stamp only after the actual original setters/postimages;
                    // the marker is not a prepared-intent receipt.
                    Marker.data=RebirthLogisticsDeathJournal.Marker(Death,true);Chunk.isModified=true;
                    Publication?.Dispose();Publication=null;
                }
                if(!RebirthLogisticsDeathPartition.Same(grid.items,Death.After))return false;
                if(Publication==null){Accepted.LiveOwnerEntity=Accepted.Backpack.RefPlayerId;Publication=RebirthLogisticsBackpackPublication.Begin(Accepted,Current,RebirthLogisticsDeathJournal.MarkerKey(Death));}
                if(Publication==null||!Publication.Saved(out string proof))return false;
                Death.FinalProof=proof;Death.State=3;
                if(!RebirthLogisticsDeathJournal.Publish(Original,Death,Current)){Reload();return false;}
                Publication.Dispose();Publication=null;
            }
            return Death.State>=3&&Current()&&Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Death,true))&&RebirthLogisticsDeathPartition.Same(Accepted.Backpack.bag.ItemGrid.items,Death.After);
        }
        public void Dispose(){capacity?.Dispose();capacity=null;Publication?.Dispose();Publication=null;Closed=true;}
    }
    private static readonly Dictionary<Guid,Lease> held=new Dictionary<Guid,Lease>();
    private sealed class ParcelCustody:IDisposable
    {
        internal Lease Owner;internal Guid Incarnation;internal EntityBackpack Backpack;internal Chunk Chunk;internal ChunkCustomData Marker;
        internal RebirthLogisticsBackpackPublication Publication;internal bool Applying,Closed;
        internal RebirthLogisticsDeathJournal.Parcel Record=>Owner.Death?.Parcels.SingleOrDefault(value=>value.Incarnation==Incarnation);
        internal bool Current()
        {
            var record=Record;
            if(Closed||record==null||!Owner.Current()||Backpack==null||!ReferenceEquals(Owner.Accepted.World.GetEntity(record.EntityId),Backpack)||Backpack.entityClass!=record.EntityClass||
                !RebirthLogisticsParcelReferenceJournal.TryEffective(Owner.Original,Owner.Death,record,Backpack,out int nativeOwner)||Backpack.RefPlayerId!=nativeOwner||!Backpack.addedToChunk||Backpack.chunkPosAddedEntityTo.x!=record.X||Backpack.chunkPosAddedEntityTo.z!=record.Z||
                Chunk==null||Chunk.InProgressUnloading||!ReferenceEquals(Owner.Accepted.World.GetChunkSync(record.X,record.Z),Chunk)||
                !Chunk.ChunkCustomData.dict.TryGetValue(RebirthLogisticsDeathJournal.MarkerKey(Owner.Death,record),out var marker)||!ReferenceEquals(marker,Marker)||
                marker.TriggerWriteDataDelegate!=null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.data==null||
                Backpack.bag?.ItemGrid?.Length!=record.Locks.Length||Enumerable.Range(0,record.Locks.Length).Any(index=>Backpack.bag.ItemGrid.IsLocked(index)!=record.Locks[index]))return false;
            return marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Owner.Death,record,false))||marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Owner.Death,record,true));
        }
        private RebirthLogisticsDeathCustody.Accepted Accepted()=>new RebirthLogisticsDeathCustody.Accepted{World=Owner.Accepted.World,Backpack=Backpack,Owner=Owner.Death.Owner,Creation=Owner.Death.Creation,
            Operation=Owner.Death.Operation,OriginalDeaths=Owner.Death.Deaths,OriginalOwnerEntity=Owner.Death.OwnerEntity,LiveOwnerEntity=Backpack.RefPlayerId,EntityId=Record.EntityId,EntityClass=Record.EntityClass,NativeBag=Record.Before};
        internal bool ObserveBefore()
        {
            if(!Current())return false;var record=Record;if(record.State>=2)return !string.IsNullOrEmpty(record.BeforeProof);
            if(record.State!=1||!Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Owner.Death,record,false))||!RebirthLogisticsDeathPartition.Same(Backpack.bag.ItemGrid.items,record.Before))return false;
            if(Publication==null)Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(),Current,RebirthLogisticsDeathJournal.MarkerKey(Owner.Death,record));
            if(Publication==null||!Publication.Saved(out string proof))return false;
            record.BeforeProof=proof;record.State=2;
            if(!RebirthLogisticsDeathJournal.Publish(Owner.Original,Owner.Death,Current)){Owner.Reload();return false;}
            Publication.Dispose();Publication=null;return true;
        }
        internal bool Deliver()
        {
            if(!Current()||Record.State<2||!RebirthLogisticsBackpackPublication.CanSerializePlanned(Backpack,Record.After,Owner.Original,Current))return false;var record=Record;var grid=Backpack.bag.ItemGrid;
            if(record.State==3)return !string.IsNullOrEmpty(record.FinalProof)&&Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Owner.Death,record,true))&&RebirthLogisticsDeathPartition.Same(grid.items,record.After);
            if(!Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(Owner.Death,record,true)))
            {
                if(!Applying&&!RebirthLogisticsDeathPartition.Same(grid.items,record.Before)||!RebirthLogisticsOwnerReceipt.Matches(grid.items,record.Before,record.After,true,false))return false;
                Applying=true;
                for(int i=0;i<record.Before.Length;i++)
                {
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(record.Before[i],record.After[i])||RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],record.After[i]))continue;
                    if(!Current()||permit!=null||!RebirthLogisticsOwnerReceipt.Matches(grid.items,record.Before,record.After,true,false))return false;
                    var next=record.After[i].Clone();var entry=new Permit{Grid=grid,Slot=i,Value=next,Current=Current};permit=entry;
                    try{Backpack.bag.SetSlot(i,next);}finally{permit=null;}
                    if(!entry.Used||!RebirthStationGridIngredients.IsSameStackSnapshot(grid.items[i],record.After[i]))return false;
                }
                if(!Current()||!RebirthLogisticsDeathPartition.Same(grid.items,record.After))return false;
                Marker.data=RebirthLogisticsDeathJournal.Marker(Owner.Death,record,true);Chunk.isModified=true;Publication?.Dispose();Publication=null;
            }
            if(!RebirthLogisticsDeathPartition.Same(grid.items,record.After))return false;
            if(Publication==null)Publication=RebirthLogisticsBackpackPublication.Begin(Accepted(),Current,RebirthLogisticsDeathJournal.MarkerKey(Owner.Death,record));
            if(Publication==null||!Publication.Saved(out string proof))return false;
            record.FinalProof=proof;record.State=3;
            if(!RebirthLogisticsDeathJournal.Publish(Owner.Original,Owner.Death,Current)){Owner.Reload();return false;}
            Publication.Dispose();Publication=null;return true;
        }
        public void Dispose(){Publication?.Dispose();Publication=null;Closed=true;}
    }
    private static readonly Dictionary<Guid,ParcelCustody> parcelHeld=new Dictionary<Guid,ParcelCustody>();
    private static bool TryParcel(Lease owner,Guid incarnation,out ParcelCustody result)
    {
        result=null;
        try
        {
            var record=owner.Death.Parcels.SingleOrDefault(value=>value.Incarnation==incarnation);if(record==null||!owner.Current())return false;
            if(parcelHeld.TryGetValue(incarnation,out var previous)&&previous.Current()){result=previous;return true;}
            previous?.Dispose();parcelHeld.Remove(incarnation);if(parcelHeld.Count>=384)return false;
            var world=owner.Accepted.World;var chunk=world.GetChunkSync(record.X,record.Z) as Chunk;
            if(chunk==null||chunk.InProgressUnloading||Math.Abs((long)record.X-(owner.Death.HasRecoveryAnchor?owner.Death.AnchorX:owner.Death.X))>1||Math.Abs((long)record.Z-(owner.Death.HasRecoveryAnchor?owner.Death.AnchorZ:owner.Death.Z))>1)return false;
            string key=RebirthLogisticsDeathJournal.MarkerKey(owner.Death,record);
            if(record.State==0)
            {
                if(world.GetEntity(record.EntityId)!=null)return false;
                if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var initial))
                {
                    if(chunk.ChunkCustomData.dict.Count>=4096)return false;
                    initial=new ChunkCustomData(key,ulong.MaxValue,false){data=RebirthLogisticsDeathJournal.Marker(owner.Death,record,false)};chunk.ChunkCustomData.Set(key,initial);chunk.isModified=true;
                }
                if(initial.data==null||!initial.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(owner.Death,record,false)))return false;
                EntityBackpack prototype=null;RebirthLogisticsNativeFrame.Materialization nativeFrame=null;IDisposable nativeBody=null;
                try
                {
                    prototype=EntityFactory.CreateEntity(record.EntityClass,record.EntityId,new UnityEngine.Vector3(record.PosX,record.PosY,record.PosZ),UnityEngine.Vector3.zero) as EntityBackpack;
                    if(prototype?.bag?.ItemGrid?.items==null||!RebirthLogisticsDeathPartition.Same(prototype.bag.ItemGrid.items,record.Before)||
                        Enumerable.Range(0,record.Locks.Length).Any(index=>prototype.bag.ItemGrid.IsLocked(index)!=record.Locks[index]))return false;
                    prototype.RefPlayerId=owner.Death.OwnerEntity;
                    EntityCreationData creation=null;
                    if(record.Spawn==null){nativeBody=RebirthLogisticsBackpackPublication.ReserveNativeBody(prototype,owner.Original,owner.Current);if(nativeBody==null)return false;creation=new EntityCreationData(prototype){id=record.EntityId};}
                    if(record.Spawn!=null&&!RebirthLogisticsBackpackPublication.TryReadSpawn(record.Spawn,record.EntityId,record.EntityClass,owner.Death.OwnerEntity,
                        new UnityEngine.Vector3(record.PosX,record.PosY,record.PosZ),record.Before,record.Locks,out creation,out nativeFrame))return false;
                    record.State=1;
                    if(!RebirthLogisticsDeathJournal.Publish(owner.Original,owner.Death,owner.Current)){owner.Reload();return false;}
                    // An attempted spawn is durable before native execution.
                    // Absence after this point is never automatic respawn permission.
                    var spawned=GameManager.Instance.SpawnEntityServer(creation) as EntityBackpack;
                    if(spawned==null||!ReferenceEquals(world.GetEntity(record.EntityId),spawned)||spawned.RefPlayerId!=owner.Death.OwnerEntity||spawned.entityClass!=record.EntityClass)return false;
                }
                finally{nativeFrame?.Dispose();nativeBody?.Dispose();prototype?.OnEntityUnload();}
            }
            var backpack=world.GetEntity(record.EntityId) as EntityBackpack;
            if(backpack==null||!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))return false;
            var bound=new ParcelCustody{Owner=owner,Incarnation=incarnation,Backpack=backpack,Chunk=chunk,Marker=marker};
            // A newly spawned body may await native chunk attachment. Retain
            // its exact reference now so expiry/access cannot race the witness.
            parcelHeld[incarnation]=bound;if(!bound.Current())return false;result=bound;return true;
        }
        catch{return false;}
    }
    private static World activeWorld;private static DateTime nextScan;
    private sealed class Permit{internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal Func<bool> Current;internal bool Used;}
    [ThreadStatic]private static Permit permit;
    private static bool installed;
    internal static bool Blocks(ItemStackGrid grid)
    {
        if(grid==null||GameManager.Instance?.World?.IsRemote()!=false)return false;
        if(RebirthLogisticsNativeRecovery.Blocks(grid)||RebirthLogisticsAcceptedBodyJournal.Blocks(grid)||RebirthLogisticsParcelReferenceJournal.Blocks(grid))return true;
        foreach(var value in parcelHeld.Values)if(!value.Closed&&value.Owner.Death!=null&&ReferenceEquals(value.Owner.Accepted?.World,GameManager.Instance.World)&&ReferenceEquals(value.Backpack?.bag?.ItemGrid,grid)&&
            RebirthLogisticsTransferJournal.TryRead(value.Owner.Death.Owner,value.Owner.Death.Operation,out var original)&&original.Phase<4)return true;
        foreach(var value in held.Values)if(!value.Closed&&value.Death!=null&&ReferenceEquals(value.Accepted?.World,GameManager.Instance.World)&&ReferenceEquals(value.Accepted.Backpack?.bag?.ItemGrid,grid)&&
            RebirthLogisticsTransferJournal.TryRead(value.Death.Owner,value.Death.Operation,out var original)&&original.Phase<4)return true;
        foreach(var value in RebirthLogisticsDeathCustody.Snapshot())if(!value.Ambiguous&&ReferenceEquals(value.Backpack?.bag?.ItemGrid,grid)&&
            RebirthLogisticsTransferJournal.TryRead(value.Owner,value.Operation,out var original)&&original.Phase<4)return true;
        return false;
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var entry=permit;if(entry==null||entry.Used||!ReferenceEquals(entry.Grid,grid)||entry.Slot!=slot||!ReferenceEquals(entry.Value,value)||entry.Current?.Invoke()!=true)return false;
        entry.Used=true;return true;
    }
    private static bool HoldsBackpack(EntityBackpack backpack)
    {
        if(backpack==null||backpack.world?.IsRemote()!=false)return false;
        try{if(RebirthPlacedOriginalDeathPartition.HoldsBody(backpack))return true;}catch{/* Each domain resolves only its own original. */}
        if(Blocks(backpack.bag?.ItemGrid)||RebirthLogisticsAcceptedBodyJournal.Holds(backpack)||RebirthLogisticsParcelReferenceJournal.Holds(backpack))return true;
        // Lock/expiry can arrive before the first game-update scan on load.
        // Read the actual native marker and exact original before permitting
        // those native effects, rather than relying on a warm dictionary.
        if(!backpack.addedToChunk)return false;
        var chunk=backpack.world.GetChunkSync(backpack.chunkPosAddedEntityTo.x,backpack.chunkPosAddedEntityTo.z) as Chunk;
        if(chunk==null||chunk.InProgressUnloading)return false;
        string prefix="RebirthLogisticsBackpackV1:"+backpack.entityId+":";
        foreach(var entry in chunk.ChunkCustomData.dict.Where(pair=>pair.Key.StartsWith(prefix,StringComparison.Ordinal)).ToArray())
        {
            try
            {
                byte[] bytes=entry.Value?.data;if(bytes==null||bytes.Length>4096)continue;
                using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
                {
                    if(reader.ReadByte()!=1)continue;reader.ReadBytes(16);var operation=new Guid(reader.ReadBytes(16));string owner=reader.ReadString();
                    if(RebirthLogisticsTransferJournal.TryRead(owner,operation,out var original)&&original.Phase<4&&TryAcquire(original,out var lease))
                    {
                        if(ReferenceEquals(lease.Accepted.Backpack,backpack))return true;
                        var parcel=lease.Death.Parcels.SingleOrDefault(value=>value.EntityId==backpack.entityId);
                        if(parcel!=null&&parcel.State>=1&&TryParcel(lease,parcel.Incarnation,out var physical)&&ReferenceEquals(physical.Backpack,backpack))return true;
                    }
                }
            }
            catch{ /* Unknown marker never invents a native endpoint identity. */ }
        }
        return false;
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.original.backpack.custody.v1");
        try{foreach(var type in new[]{typeof(NativeLock),typeof(NativeExpiry),typeof(NativeRemove)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);installed=true;}
        catch{harmony.UnpatchSelf();throw;}
    }
    [HarmonyPatch(typeof(Entity),nameof(Entity.OnLockRequestServer))]
    private static class NativeLock
    {
        private static bool Prefix(Entity __instance,ref bool __result)
        {if(!(__instance is EntityBackpack backpack)||!HoldsBackpack(backpack))return true;__result=false;return false;}
    }
    [HarmonyPatch(typeof(EntityContainerAbs),nameof(EntityContainerAbs.OnUpdateEntity))]
    private static class NativeExpiry
    {private static bool Prefix(EntityContainerAbs __instance)=>!(__instance is EntityBackpack backpack)||!HoldsBackpack(backpack);}
    [HarmonyPatch(typeof(World),nameof(World.RemoveEntity),new[]{typeof(int),typeof(EnumRemoveEntityReason)})]
    private static class NativeRemove
    {
        private sealed class RemovalState{internal EntityBackpack Body;internal object Reference;internal World World;internal EntityRebirthDogCompanion Dog;}
        private static void Prefix(World __instance,int _entityId,EnumRemoveEntityReason _reason,out RemovalState __state)
        {
            __state=null;
            // Separate primitive runtime observation; unloaded dogs are included.
            // Capture the exact object before native removal, never by later ID.
            if(__instance!=null&&__instance.GetEntity(_entityId) is EntityRebirthDogCompanion dog)
                __state=new RemovalState{World=__instance,Dog=dog};
            if(_reason==EnumRemoveEntityReason.Unloaded||__instance.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,__instance)||!(__instance.GetEntity(_entityId) is EntityBackpack backpack))return;
            __state=new RemovalState{Body=backpack};
            try{__state.Reference=RebirthLogisticsParcelReferenceJournal.CaptureRemoval(backpack);}catch{/* Observation never blocks native removal. */}
            HoldsBackpack(backpack); // Resolve a cold known marker before actual native invalidation.
            RebirthLogisticsAcceptedBodyJournal.Invalidate(backpack);
            foreach(var value in parcelHeld.Values.Where(value=>ReferenceEquals(value.Backpack,backpack)).ToArray())
            {
                var record=value.Record;
                if(record!=null&&value.Chunk!=null&&!value.Chunk.InProgressUnloading&&ReferenceEquals(__instance.GetChunkSync(record.X,record.Z),value.Chunk))
                {value.Chunk.ChunkCustomData.Remove(RebirthLogisticsDeathJournal.MarkerKey(value.Owner.Death,record));value.Chunk.isModified=true;}
                value.Dispose();
            }
            foreach(var value in held.Values.Where(value=>ReferenceEquals(value.Accepted?.Backpack,backpack)).ToArray())
            {
                if(value.Chunk!=null&&!value.Chunk.InProgressUnloading&&ReferenceEquals(__instance.GetChunkSync(value.Death.X,value.Death.Z),value.Chunk))
                {value.Chunk.ChunkCustomData.Remove(RebirthLogisticsDeathJournal.MarkerKey(value.Death));value.Chunk.isModified=true;}
                value.Dispose();
            }
            foreach(var value in RebirthLogisticsDeathCustody.Snapshot())if(ReferenceEquals(value.Backpack,backpack))value.Ambiguous=true;
            RebirthLogisticsNativeRecovery.Invalidate(backpack);
        }
        private static void Postfix(World __instance,int _entityId,EnumRemoveEntityReason _reason,RemovalState __state,bool __runOriginal)
        {
            if(!__runOriginal)return;
            if(__state?.Dog!=null&&ReferenceEquals(__state.World,__instance)&&!ReferenceEquals(__instance.GetEntity(_entityId),__state.Dog))
                try{RebirthDogRuntimeService.OnNativeRuntimeRemoved(__state.World,__state.Dog);}catch{/* Primitive original runtime cache only. */}
            if(__state?.Body==null||_reason==EnumRemoveEntityReason.Unloaded||__instance.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,__instance)||ReferenceEquals(__instance.GetEntity(_entityId),__state.Body))return;
            try{RebirthLogisticsParcelReferenceJournal.OnRemoved(__instance,__state.Body,__state.Reference);}catch{/* Exact original metadata only. */}
            try{RebirthPlacedOriginalDeathPartition.OnBodyRemoving(__instance,__state.Body);}catch{/* Exact actual native removal only. */}
        }
    }
    internal static bool TryAcquire(RebirthLogisticsTransferJournal.Record original,out Lease lease)
    {
        lease=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||original==null||original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||
                !RebirthLogisticsDeathJournal.TryRead(original,out var death))return false;
            if(!ReferenceEquals(activeWorld,world)){foreach(var old in held.Values)old.Dispose();foreach(var old in parcelHeld.Values)old.Dispose();held.Clear();parcelHeld.Clear();activeWorld=world;}
            if(held.TryGetValue(original.Operation,out var existing)&&existing.Current())
            {existing.Original=original;existing.Death=death;lease=existing;return true;}
            existing?.Dispose();held.Remove(original.Operation);if(held.Count>=128)return false;
            var chunk=world.GetChunkSync(death.X,death.Z) as Chunk;var backpack=world.GetEntity(death.EntityId) as EntityBackpack;
            if(chunk==null||chunk.InProgressUnloading||backpack==null||backpack.entityClass!=death.EntityClass||!RebirthLogisticsParcelReferenceJournal.TryEffective(original,death,null,backpack,out int nativeOwner)||backpack.RefPlayerId!=nativeOwner||
                !backpack.addedToChunk||backpack.chunkPosAddedEntityTo.x!=death.X||backpack.chunkPosAddedEntityTo.z!=death.Z)return false;
            string key=RebirthLogisticsDeathJournal.MarkerKey(death);
            if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
            {
                // Mint only from the uninterrupted actual accepted spawn and
                // final native owner observation, never a cold entity lookup.
                if(death.State!=0||!(RebirthLogisticsDeathCustody.TryGet(original,out var accepted)||RebirthLogisticsNativeRecovery.TryGetAccepted(original,out accepted))||!ReferenceEquals(accepted.Backpack,backpack)||
                    accepted.OwnerFileProof!=death.OwnerFileProof||!RebirthLogisticsDeathPartition.Same(backpack.bag.ItemGrid.items,death.Before)||chunk.ChunkCustomData.dict.Count>=4096)return false;
                marker=new ChunkCustomData(key,ulong.MaxValue,false){data=RebirthLogisticsDeathJournal.Marker(death,false)};
                chunk.ChunkCustomData.Set(key,marker);chunk.isModified=true;
            }
            var bound=new RebirthLogisticsDeathCustody.Accepted{World=world,Backpack=backpack,Owner=death.Owner,Creation=death.Creation,Operation=death.Operation,
                OriginalDeaths=death.Deaths,OriginalOwnerEntity=death.OwnerEntity,LiveOwnerEntity=backpack.RefPlayerId,EntityId=death.EntityId,EntityClass=death.EntityClass,NativeBag=ItemStack.Clone(death.Before),
                RemainingBag=ItemStack.Clone(death.RemainingBag),RemainingBelt=ItemStack.Clone(death.RemainingBelt),OwnerPhase=death.OwnerPhase,OwnerFileProof=death.OwnerFileProof};
            var candidate=new Lease{Original=original,Death=death,Accepted=bound,Chunk=chunk,Marker=marker};if(!candidate.Current())return false;
            held.Add(original.Operation,candidate);lease=candidate;return true;
        }
        catch{return false;}
    }
    internal static void Tick()
    {
        if(!ThreadManager.IsMainThread())return;var world=GameManager.Instance?.World;
        if(!ReferenceEquals(activeWorld,world))
        {
            foreach(var old in held.Values)old.Dispose();foreach(var old in parcelHeld.Values)old.Dispose();held.Clear();parcelHeld.Clear();activeWorld=world;nextScan=DateTime.MinValue;
        }
        RebirthLogisticsDeathCustody.ResetWorld(world);RebirthLogisticsAcceptedBodyJournal.ResetCurrentWorld();RebirthLogisticsParcelReferenceJournal.ResetCurrentWorld();RebirthLogisticsNativeRecovery.ResetWorld(world);RebirthLogisticsNativeDeathReceipt.ResetWorld(world);
        if(world?.IsRemote()!=false)return;
        foreach(var accepted in RebirthLogisticsDeathCustody.Snapshot())
            if(RebirthLogisticsTransferJournal.TryRead(accepted.Owner,accepted.Operation,out var original))
            {
                RebirthLogisticsAcceptedBodyJournal.Observe(original,accepted);RebirthLogisticsAcceptedBodyJournal.Tick(original);
                if(original.Phase==4){RebirthLogisticsParcelReferenceJournal.Tick(original);RebirthLogisticsLoadedBodyNotice.ReloadOriginal(original);RebirthLogisticsDeathCustody.PruneTerminal(original);continue;}
                if(accepted.OwnerFileProof!=null)RebirthLogisticsDeathJournal.Prepare(original,accepted,out _);
            }
        if(DateTime.UtcNow>=nextScan)
        {
            nextScan=DateTime.UtcNow.AddSeconds(2);
            if(RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(world,out var originals))
            {
                RebirthLogisticsNativeRecovery.SynchronizePending(originals);
                foreach(var original in originals)
                {
                RebirthLogisticsAcceptedBodyJournal.Tick(original);
                RebirthLogisticsParcelReferenceJournal.Tick(original);
                RebirthLogisticsDeathCustody.ReconcilePublishedPhysical(original);
                RebirthLogisticsNativeDeathReceipt.CloseSurvivingOriginal(original);
                RebirthLogisticsNativeRecovery.Tick(original);
                CloseSavedNoEffect(world,original);
                if(RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var current)&&current.Phase<4)TryAcquire(current,out _);
                }
            }
        }
        foreach(var lease in held.Values.ToArray())try
        {
            if(!lease.Reload()||!lease.Current())continue;
            if(lease.Original.Phase==4&&lease.Original.Outcome==3&&!lease.Original.OwnerEscrowRecovery)
            {
                if(!RebirthLogisticsParcelReferenceJournal.ReadyForNoEffect(lease.Original))continue;
                // No item delta was ever owed. Its positive saved NoEffect
                // outcome retires only domain metadata, even after native loot.
                lease.Chunk.ChunkCustomData.Remove(RebirthLogisticsDeathJournal.MarkerKey(lease.Death));lease.Chunk.isModified=true;
                lease.Dispose();held.Remove(lease.Death.Operation);continue;
            }
            if(lease.Death.State==0)lease.ObserveBefore();
            if(lease.Death.State>=1&&lease.Death.OwnerPhase<2&&lease.Original.Phase<2&&lease.Original.Legs.All(value=>value.Attempted==0)&&lease.Original.Npcs.All(value=>value.Attempted==0)&&string.IsNullOrEmpty(lease.Original.OwnerDebitProof))
            {
                // The original incarnation has ended. Positive native death
                // partition proves full Before survived; a delayed old-life
                // debit permission can no longer execute in this incarnation.
                if(lease.Original.Phase==0){lease.Original.Phase=1;lease.Original.Rejecting=true;if(!RebirthLogisticsTransferJournal.TryPublish(lease.Original,lease.Current)){lease.Reload();continue;}}
                if(!lease.Original.Rejecting){lease.Original.Rejecting=true;if(!RebirthLogisticsTransferJournal.TryPublish(lease.Original,lease.Current)){lease.Reload();continue;}}
                lease.Original.OwnerNoEffectProof=RebirthLogisticsOriginalTransport.Digest(lease.Death.Binding+":"+lease.Death.OwnerFileProof+":"+lease.Death.BeforeProof);
                if(RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(lease.Original,out var physicalOwed))lease.Original.OwnerEscrowRecovery=physicalOwed.Any(item=>!item.IsEmpty());
                lease.Original.Outcome=3;lease.Original.Phase=2;if(!RebirthLogisticsTransferJournal.TryPublish(lease.Original,lease.Current)){lease.Reload();continue;}
            }
            // Once the original paid outcome is durable, completing its owed
            // physical body requires the retained native custody, not a new
            // life or a fresh permission to debit sources. Phase1 remains in
            // the authenticated actor/source coordinator exclusively.
            bool settledOutcome=lease.Original.Outcome==3||lease.Original.Outcome>=1&&lease.Original.Outcome<=2&&
                !string.IsNullOrEmpty(lease.Original.OwnerDebitProof)&&lease.Death.OwnerPhase>=2;
            if(settledOutcome&&lease.Original.Phase>=2&&lease.Original.Phase<4&&lease.Deliver())
            {lease.Original.Phase++;RebirthLogisticsTransferJournal.TryPublish(lease.Original,lease.Current);lease.Reload();}
            if(lease.Original.Phase==4){RebirthLogisticsParcelReferenceJournal.Tick(lease.Original);RebirthLogisticsLoadedBodyNotice.ReloadOriginal(lease.Original);}
            if(lease.Original.Phase==4&&lease.Death.State==3)
            {
                lease.Death.State=4;
                if(RebirthLogisticsDeathJournal.Publish(lease.Original,lease.Death,lease.Current))
                {lease.Reload();}
                else lease.Reload();
            }
            if(lease.Original.Phase==4&&lease.Death.State==4&&RebirthLogisticsLoadedBodyNotice.CanRetire(lease.Original,lease.Death.Incarnation,lease.Accepted.Backpack)&&
                lease.Death.Parcels.All(parcel=>TryParcel(lease,parcel.Incarnation,out var custody)&&custody.Current()&&RebirthLogisticsLoadedBodyNotice.CanRetire(lease.Original,parcel.Incarnation,custody.Backpack)))
            {
                    RebirthLogisticsAcceptedBodyJournal.Retire(lease.Original);
                    RebirthLogisticsLoadedBodyNoticeJournal.ArchiveAcknowledgedIncarnation(lease.Original,lease.Death.Incarnation,lease.Accepted.Backpack);
                    foreach(var parcel in lease.Death.Parcels)
                    {
                        if(parcelHeld.TryGetValue(parcel.Incarnation,out var physical))
                        {
                            RebirthLogisticsLoadedBodyNoticeJournal.ArchiveAcknowledgedIncarnation(lease.Original,parcel.Incarnation,physical.Backpack);
                            if(physical.Chunk!=null&&!physical.Chunk.InProgressUnloading&&physical.Marker?.data?.SequenceEqual(RebirthLogisticsDeathJournal.Marker(lease.Death,parcel,true))==true)
                            {physical.Chunk.ChunkCustomData.Remove(RebirthLogisticsDeathJournal.MarkerKey(lease.Death,parcel));physical.Chunk.isModified=true;}
                            physical.Dispose();parcelHeld.Remove(parcel.Incarnation);
                        }
                    }
                    lease.Chunk.ChunkCustomData.Remove(RebirthLogisticsDeathJournal.MarkerKey(lease.Death));lease.Chunk.isModified=true;
                    lease.Dispose();held.Remove(lease.Death.Operation);
            }
        }
        catch{ /* Unknown retains original. */ }
    }
    private static void CloseSavedNoEffect(World world,RebirthLogisticsTransferJournal.Record original)
    {
        if(original==null||original.OwnerEscrowRecovery||RebirthLogisticsNativeDeathReceipt.HasDeleteOwed(original)||RebirthLogisticsNativeDeathReceipt.TryPhysicalOwed(original,out var physicalOwed)&&physicalOwed.Any(item=>!item.IsEmpty())||original.Outcome!=3||original.Phase<2||original.Phase>3||string.IsNullOrEmpty(original.OwnerNoEffectProof)||
            original.Legs.Any(value=>value.Attempted!=0||value.Published!=0||value.Restored)||original.Npcs.Any(value=>value.Attempted!=0||value.Published!=0))return;
        string binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),proof=original.OwnerNoEffectProof;
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&world?.IsRemote()==false&&RebirthLogisticsTransferJournal.WorldKey(world)==original.World&&
            RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var saved)&&saved.Outcome==3&&!saved.OwnerEscrowRecovery&&saved.OwnerNoEffectProof==proof&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(saved)==binding&&saved.Legs.All(value=>value.Attempted==0&&value.Published==0&&!value.Restored)&&saved.Npcs.All(value=>value.Attempted==0&&value.Published==0);
        if(!Current()||!RebirthLogisticsParcelReferenceJournal.ReadyForNoEffectItemTerminal(original))return;original.Phase++;RebirthLogisticsTransferJournal.TryPublish(original,()=>Current()&&RebirthLogisticsParcelReferenceJournal.ReadyForNoEffectItemTerminal(original));
    }
    // Read-only original continuation gate. It grants no new native owner write
    // and no unrelated action scope to a respawned character.
    internal static bool MatchesOriginalActor(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            return actor?.world?.IsRemote()==false&&!actor.IsDead()&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                original!=null&&original.Deaths<int.MaxValue&&actor.Died==original.Deaths+1&&RebirthCamperCharacterAccess.CurrentCreation(actor)==original.Creation&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==original.Owner&&
                (RebirthLogisticsNativeDeathReceipt.TryPaidNoneCustody(original,out _)||RebirthLogisticsNativeDeathReceipt.TryPaidDeletedCustody(original,out _)||RebirthLogisticsNativeDeathReceipt.TryPaidPhysicalCustody(original,out _)||TryAcquire(original,out var lease)&&lease.Death.State>=1&&lease.Death.OwnerPhase>=2&&!string.IsNullOrEmpty(lease.Death.BeforeProof)&&lease.Current()&&
                (original.Phase>=2||lease.Marker.data.SequenceEqual(RebirthLogisticsDeathJournal.Marker(lease.Death,false))&&RebirthLogisticsDeathPartition.Same(lease.Accepted.Backpack.bag.ItemGrid.items,lease.Death.Before)));
        }
        catch{return false;}
    }
    // Named existing paid SOURCE continuation only. Generic actor/owner setter
    // scopes remain strict. No current new-life inventory image is consulted.
    internal static bool MatchesPaidSourceContinuation(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||original==null||original.Phase<1||original.Phase>3||original.OwnerEscrowRecovery||
                original.Deaths==int.MaxValue||actor.Died<original.Deaths+1||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                RebirthLogisticsTransferJournal.WorldKey(actor.world)!=original.World||RebirthCamperCharacterAccess.CurrentCreation(actor)!=original.Creation)return false;
            var sender=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
            if(sender?.loginDone!=true||!RebirthLogisticsBodyReferenceWitness.TryCurrentNativeOwner(actor,sender,original.Owner,out _)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||retained.Phase<1||retained.Phase>4||retained.OwnerEscrowRecovery||
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||
                retained.Phase<original.Phase||retained.Phase==1&&retained.Outcome!=0||retained.Phase>=2&&(retained.Outcome<1||retained.Outcome>2)||
                !string.IsNullOrEmpty(original.OwnerDebitProof)&&retained.OwnerDebitProof!=original.OwnerDebitProof)return false;
            // Phase1 may first publish its already ACTUAL native debit proof;
            // existing leg gates still demand durable OwnerDebitProof before
            // any source effect. This branch never performs an owner debit.
            if(RebirthLogisticsNativeDeathReceipt.TryPaidNoneCustody(original,out _)||RebirthLogisticsNativeDeathReceipt.TryPaidDeletedCustody(original,out _))return true;
            if(!RebirthLogisticsNativeDeathReceipt.TryRead(original,out var fatal)||fatal.Phase!=2||fatal.Physical?.Stage!=5||fatal.OwnerFileProof?.Length!=64)return false;
            if(fatal.Physical.Requests==0&&RebirthLogisticsNativeDeathReceipt.TryPaidPhysicalCustody(original,out _))return true;
            if(!RebirthLogisticsDeathJournal.TryRead(original,out var death)||death.State<1||death.OwnerPhase!=2||death.BeforeProof?.Length!=64||death.OwnerFileProof!=fatal.OwnerFileProof||
                !(RebirthLogisticsNativeDeathReceipt.ConservedPhysical(original,2,death.Before,death.RemainingBag,death.RemainingBelt)||RebirthLogisticsNativeDeathReceipt.ConservedPreserved(original,death)))return false;
            return TryAcquire(original,out var lease)&&lease.Current()&&lease.Death.State>=1&&lease.Death.OwnerPhase==2&&lease.Death.BeforeProof==death.BeforeProof;
        }
        catch{return false;}
    }
    internal static bool ActorCurrent(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original)
        =>actor!=null&&original!=null&&(!actor.IsDead()&&actor.Died==original.Deaths&&!RebirthLogisticsNativeDeathReceipt.IncarnationEnded(original)&&RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)||MatchesOriginalActor(actor,original));
    internal static bool TryOwnerDebitProof(RebirthStablePlayerIdentity owner,RebirthLogisticsTransferJournal.Record original,Func<bool> current,out string proof)
    {
        proof=null;
        if(owner?.StorageKey==original?.Owner&&current?.Invoke()==true&&RebirthLogisticsNativeDeathReceipt.TryPaidDeletedCustody(original,out var deleted)&&current()==true)
        {proof=RebirthLogisticsOriginalTransport.Digest(deleted.Binding+":"+deleted.DeleteBeforeProof+":"+deleted.OwnerFileProof+":native-delete-debit");return true;}
        if(owner?.StorageKey==original?.Owner&&current?.Invoke()==true&&RebirthLogisticsNativeDeathReceipt.TryPaidPhysicalCustody(original,out var physical)&&physical.Physical.Requests==0&&current()==true)
        {proof=RebirthLogisticsOriginalTransport.Digest(physical.Binding+":"+physical.Physical.BeforeDigest+":"+physical.OwnerFileProof+":native-physical-no-body-debit");return true;}
        if(owner?.StorageKey==original?.Owner&&current?.Invoke()==true&&RebirthLogisticsNativeDeathReceipt.TryPaidNoneCustody(original,out var none)&&current()==true)
        {proof=RebirthLogisticsOriginalTransport.Digest(none.Binding+":"+none.OwnerFileProof+":native-none-debit");return true;}
        if(owner?.StorageKey!=original?.Owner||current?.Invoke()!=true||!TryAcquire(original,out var lease)||lease.Death.OwnerPhase!=2||
            !lease.ObserveBefore()||lease.Death.State<1||!lease.Current()||current()!=true)return false;
        proof=RebirthLogisticsOriginalTransport.Digest(lease.Death.Binding+":"+lease.Death.OwnerFileProof+":"+lease.Death.BeforeProof);return true;
    }
    internal static bool TryComplete(RebirthStablePlayerIdentity owner,RebirthLogisticsTransferJournal.Record original,Func<bool> current,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(owner?.StorageKey!=original?.Owner||current?.Invoke()!=true||original.Phase<2||original.Phase>3||original.Outcome==0||
            !TryAcquire(original,out var lease)||!lease.Deliver()||lease.Death.State<3||current()!=true)return false;
        if(!RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original))return false;
        retained.Phase++;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,()=>current()&&lease.Current()&&lease.Death.State>=3&&RebirthLogisticsDeathPartition.Same(lease.Accepted.Backpack.bag.ItemGrid.items,lease.Death.After)))return false;
        published=retained;return true;
    }
}
// A paid None-mode original has no accepted dropped body. Its separately
// observed native death and saved owner partition may create one original
// recovery parcel at that observed death location. Attempted missing spawns
// remain unknown; they are never replayed into a second parcel.
internal static class RebirthLogisticsNativeRecovery
{
    private const int MaximumBytes=RebirthLogisticsDeathJournal.MaximumBytes;
    internal sealed class Record
    {
        internal Guid Operation,Incarnation;internal string Owner,World,Creation,Binding,FatalProof;
        internal int Deaths,OwnerEntity,EntityId,EntityClass,X,Z,State;internal float PosX,PosY,PosZ;internal bool SafeRoot;
        internal ItemStack[] Before;internal bool[] Locks;
        internal RebirthLogisticsDeathJournal.Record Prepared;
    }
    // This detached intent has no root publication proof and cannot be written
    // through DeathJournal.Write. Only ObserveBefore supplies that later proof.
    internal static RebirthLogisticsDeathJournal.Record Projection(RebirthLogisticsTransferJournal.Record original,Record plan)
    {
        if(!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var fatal))throw new InvalidDataException();
        ItemStack[] owed=null;if((fatal.Mode==6||fatal.Physical!=null)&&!RebirthLogisticsNativeDeathReceipt.TryPreservedOwed(original,out owed))throw new InvalidDataException();
        var anchor=World.toChunkXZ(new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z));
        return new RebirthLogisticsDeathJournal.Record{HasRecoveryAnchor=plan.SafeRoot,AnchorX=plan.SafeRoot?anchor.x:0,AnchorZ=plan.SafeRoot?anchor.y:0,Operation=original.Operation,Incarnation=plan.Incarnation,Owner=original.Owner,World=original.World,Creation=original.Creation,
            Binding=plan.Binding,Deaths=original.Deaths,OwnerPhase=fatal.Phase,OwnerEntity=plan.OwnerEntity,EntityId=plan.EntityId,EntityClass=plan.EntityClass,X=plan.X,Z=plan.Z,
            Outcome=original.Outcome,PreservedOwed=owed,OwnerFileProof=plan.FatalProof,Before=ItemStack.Clone(plan.Before),Locks=(bool[])plan.Locks.Clone(),
            RemainingBag=ItemStack.Clone(fatal.RemainingBag),RemainingBelt=ItemStack.Clone(fatal.RemainingBelt)};
    }
    private static XElement PreparedXml(Record plan,RebirthLogisticsDeathJournal.Record value)
    {
        if(plan==null||value==null)throw new InvalidDataException();
        if(value.Spawn==null&&!value.Parcels.Any(parcel=>parcel?.Spawn!=null))return PreparedXmlCore(plan,value);
        using(var scope=RebirthLogisticsNativeFrame.BeginRead(plan.Owner,plan.Operation,plan.World,plan.Binding))
        {if(scope==null)throw new InvalidDataException();return PreparedXmlCore(plan,value);}
    }
    private static XElement PreparedXmlCore(Record plan,RebirthLogisticsDeathJournal.Record value)
    {
        if(value==null||value.State!=0||value.BeforeProof!=null||value.FinalProof!=null||value.After?.Length!=plan.Before.Length||
            value.Operation!=plan.Operation||value.Incarnation!=plan.Incarnation||value.EntityId!=plan.EntityId||value.EntityClass!=plan.EntityClass||
            value.Binding!=plan.Binding||value.Owner!=plan.Owner||value.World!=plan.World||value.Creation!=plan.Creation||value.Deaths!=plan.Deaths||
            value.OwnerEntity!=plan.OwnerEntity||value.X!=plan.X||value.Z!=plan.Z||value.HasRecoveryAnchor!=plan.SafeRoot||value.HasRecoveryAnchor&&(Math.Abs((long)value.AnchorX-plan.X)>1||Math.Abs((long)value.AnchorZ-plan.Z)>1)||(value.OwnerPhase<0||value.OwnerPhase>4)||value.OwnerFileProof!=plan.FatalProof||
            !RebirthLogisticsDeathPartition.Same(value.Before,plan.Before)||!value.Locks.SequenceEqual(plan.Locks)||value.Parcels.Count>RebirthLogisticsDeathJournal.MaximumParcels)
            throw new InvalidDataException();
        var root=new XElement("prepared",new XAttribute("outcome",value.Outcome),new XElement("after",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value.After))));
        if(value.HasRecoveryAnchor)root.Add(new XAttribute("anchorX",value.AnchorX),new XAttribute("anchorZ",value.AnchorZ));
        if(value.PreservedOwed!=null)root.Add(new XElement("owed",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value.PreservedOwed))));
        if(value.Spawn!=null)
        {
            if(!value.Spawn.Matches(plan.Owner,plan.Operation,plan.World,plan.Binding)||!RebirthLogisticsBackpackPublication.ValidateSpawn(value.Spawn,plan.EntityId,plan.EntityClass,plan.OwnerEntity,new UnityEngine.Vector3(plan.PosX,plan.PosY,plan.PosZ),plan.Before,plan.Locks))throw new InvalidDataException();
            root.Add(new XElement("spawn",value.Spawn.Encoded));
        }
        var identities=new HashSet<int>{plan.EntityId};var incarnations=new HashSet<Guid>{plan.Incarnation};
        foreach(var parcel in value.Parcels)
        {
            if(parcel==null||parcel.State!=0||parcel.BeforeProof!=null||parcel.FinalProof!=null||parcel.Incarnation==Guid.Empty||!identities.Add(parcel.EntityId)||parcel.EntityId<0||
                !incarnations.Add(parcel.Incarnation)||parcel.Before==null||parcel.Before.Length<1||parcel.Before.Length>256||parcel.Before.Any(item=>item==null||!item.IsEmpty())||
                parcel.After?.Length!=parcel.Before.Length||parcel.Locks?.Length!=parcel.Before.Length||Math.Abs((long)parcel.X-(value.HasRecoveryAnchor?value.AnchorX:plan.X))>1||Math.Abs((long)parcel.Z-(value.HasRecoveryAnchor?value.AnchorZ:plan.Z))>1||
                float.IsNaN(parcel.PosX)||float.IsInfinity(parcel.PosX)||float.IsNaN(parcel.PosY)||float.IsInfinity(parcel.PosY)||float.IsNaN(parcel.PosZ)||float.IsInfinity(parcel.PosZ))throw new InvalidDataException();
            var coordinate=World.toChunkXZ(new UnityEngine.Vector3(parcel.PosX,parcel.PosY,parcel.PosZ));if(coordinate.x!=parcel.X||coordinate.y!=parcel.Z)throw new InvalidDataException();
            root.Add(new XElement("parcel",new XAttribute("incarnation",parcel.Incarnation.ToString("N")),new XAttribute("entity",parcel.EntityId),new XAttribute("class",parcel.EntityClass),
                new XAttribute("x",parcel.X),new XAttribute("z",parcel.Z),new XAttribute("posX",parcel.PosX),new XAttribute("posY",parcel.PosY),new XAttribute("posZ",parcel.PosZ),
                new XElement("before",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(parcel.Before))),new XElement("after",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(parcel.After))),
                new XElement("locks",Convert.ToBase64String(parcel.Locks.Select(item=>(byte)(item?1:0)).ToArray()))));
            if(parcel.Spawn!=null)
            {
                if(!parcel.Spawn.Matches(plan.Owner,plan.Operation,plan.World,plan.Binding)||!RebirthLogisticsBackpackPublication.ValidateSpawn(parcel.Spawn,parcel.EntityId,parcel.EntityClass,plan.OwnerEntity,new UnityEngine.Vector3(parcel.PosX,parcel.PosY,parcel.PosZ),parcel.Before,parcel.Locks))throw new InvalidDataException();
                root.Elements("parcel").Last().Add(new XElement("spawn",parcel.Spawn.Encoded));
            }
        }
        if(Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting))>MaximumBytes)throw new InvalidDataException();return root;
    }
    private static bool SameOptional(ItemStack[] left,ItemStack[] right)=>left==null?right==null:RebirthLogisticsDeathPartition.Same(left,right);
    internal static string PreparedKey(RebirthLogisticsTransferJournal.Record original,Record plan,RebirthLogisticsDeathJournal.Record value)
    {
        if(!RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,value))throw new InvalidDataException();
        return RebirthLogisticsOriginalTransport.Digest(PreparedXml(plan,value).ToString(SaveOptions.DisableFormatting));
    }
    internal static bool TryAdoptPrepared(RebirthLogisticsTransferJournal.Record original,Record plan,RebirthLogisticsDeathJournal.Record observed,out RebirthLogisticsDeathJournal.Record adopted)
    {
        adopted=null;
        try
        {
            if(plan.Prepared==null||plan.State!=2||observed.State!=1||observed.BeforeProof?.Length!=64||observed.After!=null||observed.Parcels.Count!=0||
                !TryRead(original,out var current)||!Same(plan,current)||observed.Operation!=plan.Operation||observed.Incarnation!=plan.Incarnation||
                observed.EntityId!=plan.EntityId||observed.EntityClass!=plan.EntityClass||observed.Binding!=plan.Binding||observed.Owner!=plan.Owner||observed.World!=plan.World||
                observed.Creation!=plan.Creation||observed.Deaths!=plan.Deaths||observed.X!=plan.X||observed.Z!=plan.Z||observed.OwnerEntity!=plan.OwnerEntity||
                observed.OwnerFileProof!=plan.FatalProof||observed.OwnerPhase!=current.Prepared.OwnerPhase||
                !SameOptional(observed.PreservedOwed,current.Prepared.PreservedOwed)||!RebirthLogisticsDeathPartition.Same(observed.Before,plan.Before)||!observed.Locks.SequenceEqual(plan.Locks))return false;
            var result=current.Prepared;result.BeforeProof=observed.BeforeProof;result.State=2;
            if(!RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,result))return false;adopted=result;return true;
        }
        catch{return false;}
    }
    private static RebirthLogisticsDeathJournal.Record ReadPrepared(RebirthLogisticsTransferJournal.Record original,Record plan,XElement root)
    {
        if(root.Name!="prepared"||root.Attributes().Count()!=1+(root.Attribute("anchorX")==null?0:2)||root.Elements().Count()!=1+root.Elements("parcel").Count()+(root.Element("spawn")==null?0:1)+(root.Element("owed")==null?0:1))throw new InvalidDataException();
        var result=Projection(original,plan);
        if(result.HasRecoveryAnchor!=(root.Attribute("anchorX")!=null)||result.HasRecoveryAnchor&&((int)root.Attribute("anchorX")!=result.AnchorX||(int)root.Attribute("anchorZ")!=result.AnchorZ))throw new InvalidDataException();
        result.Outcome=(int)root.Attribute("outcome");result.After=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)root.Element("after")));
        result.Spawn=RebirthLogisticsNativeFrame.Read(result.Owner,result.Operation,result.World,result.Binding,(string)root.Element("spawn"));
        var storedOwed=root.Element("owed")==null?null:RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)root.Element("owed")));
        if(!SameOptional(result.PreservedOwed,storedOwed))throw new InvalidDataException();
        foreach(var node in root.Elements("parcel"))
        {
            if(node.Attributes().Count()!=8||node.Elements().Count()!=(node.Element("spawn")==null?3:4))throw new InvalidDataException();
            var locks=Convert.FromBase64String((string)node.Element("locks"));if(locks.Any(item=>item>1))throw new InvalidDataException();
            result.Parcels.Add(new RebirthLogisticsDeathJournal.Parcel{Incarnation=Guid.ParseExact((string)node.Attribute("incarnation"),"N"),EntityId=(int)node.Attribute("entity"),EntityClass=(int)node.Attribute("class"),
                X=(int)node.Attribute("x"),Z=(int)node.Attribute("z"),PosX=(float)node.Attribute("posX"),PosY=(float)node.Attribute("posY"),PosZ=(float)node.Attribute("posZ"),
                Before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)node.Element("before"))),After=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)node.Element("after"))),
                Spawn=RebirthLogisticsNativeFrame.Read(result.Owner,result.Operation,result.World,result.Binding,(string)node.Element("spawn")),Locks=locks.Select(item=>item!=0).ToArray()});
        }
        if(!XNode.DeepEquals(root,PreparedXml(plan,result))||!RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,result))throw new InvalidDataException();return result;
    }
    internal static bool CanRecoverOriginal(RebirthLogisticsTransferJournal.Record original,out RebirthLogisticsNativeDeathReceipt.Record fatal)
    {
        fatal=null;if(original==null||original.Phase<2||original.Phase>3||!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var receipt))return false;
        if(receipt.Mode==6||receipt.Physical!=null)
        {
            if(!RebirthLogisticsNativeDeathReceipt.TryPreservedOwed(original,out var owed)||!owed.Any(item=>!item.IsEmpty()))return false;
            if(receipt.Phase<=1)
            {if(original.Outcome!=3||!original.OwnerEscrowRecovery||string.IsNullOrEmpty(original.OwnerNoEffectProof))return false;}
            else if((original.Outcome!=1&&original.Outcome!=2)||string.IsNullOrEmpty(original.OwnerDebitProof))return false;
        }
        else if((original.Outcome!=1&&original.Outcome!=2)||string.IsNullOrEmpty(original.OwnerDebitProof))return false;
        fatal=receipt;return true;
    }
    private static readonly Dictionary<Guid,RebirthLogisticsDeathCustody.Accepted> accepted=new Dictionary<Guid,RebirthLogisticsDeathCustody.Accepted>();
    private static RebirthLogisticsParcelCoordinator preparing;
    private static EntityBackpack preparingRoot;
    private static Record preparingPlan;
    private static void ClearPreparing(){preparing?.Dispose();preparing=null;preparingRoot?.OnEntityUnload();preparingRoot=null;preparingPlan=null;}
    private static World activeWorld;
    internal static void ResetWorld(World world)
    {if(ReferenceEquals(activeWorld,world))return;ClearPreparing();accepted.Clear();rootSelection.Clear();activeWorld=world;}

    private static string PathFor(RebirthLogisticsTransferJournal.Record original)=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeRecovery",original.Owner,original.Operation.ToString("N")+".xml");
    private static XElement Write(Record value)
    {
        if(value==null||value.Operation==Guid.Empty||value.Incarnation==Guid.Empty||value.Owner?.Length!=64||value.World?.Length!=32||value.Binding?.Length!=64||value.FatalProof?.Length!=64||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||value.Deaths<0||value.OwnerEntity<0||value.EntityId<0||value.State<0||value.State>2||
            value.Before==null||value.Before.Length<1||value.Before.Length>256||value.Before.Any(item=>item==null||!item.IsEmpty())||value.Locks?.Length!=value.Before.Length||
            float.IsNaN(value.PosX)||float.IsInfinity(value.PosX)||float.IsNaN(value.PosY)||float.IsInfinity(value.PosY)||float.IsNaN(value.PosZ)||float.IsInfinity(value.PosZ))throw new InvalidDataException();
        var root=new XElement("nativeLogisticsRecovery",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("incarnation",value.Incarnation.ToString("N")),
            new XAttribute("owner",value.Owner),new XAttribute("world",value.World),new XAttribute("creation",value.Creation),new XAttribute("binding",value.Binding),new XAttribute("fatalProof",value.FatalProof),
            new XAttribute("deaths",value.Deaths),new XAttribute("ownerEntity",value.OwnerEntity),new XAttribute("entity",value.EntityId),new XAttribute("class",value.EntityClass),new XAttribute("x",value.X),new XAttribute("z",value.Z),
            new XAttribute("state",value.State),new XAttribute("posX",value.PosX),new XAttribute("posY",value.PosY),new XAttribute("posZ",value.PosZ),
            new XElement("before",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value.Before))),new XElement("locks",Convert.ToBase64String(value.Locks.Select(item=>(byte)(item?1:0)).ToArray())));
        if(value.SafeRoot)root.Add(new XAttribute("safeRoot",1));
        if(value.Prepared!=null)root.Add(PreparedXml(value,value.Prepared));return root;
    }
    internal static bool Same(Record left,Record right){try{return XNode.DeepEquals(Write(left),Write(right));}catch{return false;}}
    internal static bool TryRead(RebirthLogisticsTransferJournal.Record original,out Record value,bool pending=false)
    {
        value=null;try
        {
            if(original==null||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance?.World))return false;
            var file=new FileInfo(PathFor(original)+(pending?".tmp":string.Empty));if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            XElement root;using(var reader=XmlReader.Create(file.FullName,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))root=XElement.Load(reader);
            if(root.Name!="nativeLogisticsRecovery"||root.Attributes().Count()!=(root.Attribute("safeRoot")==null?18:19)||root.Elements().Count()!=(root.Element("prepared")==null?2:3)||(string)root.Attribute("version")!="1")return false;
            var locks=Convert.FromBase64String((string)root.Element("locks"));if(locks.Any(item=>item>1))return false;
            var record=new Record{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),Incarnation=Guid.ParseExact((string)root.Attribute("incarnation"),"N"),Owner=(string)root.Attribute("owner"),World=(string)root.Attribute("world"),
                Creation=(string)root.Attribute("creation"),Binding=(string)root.Attribute("binding"),FatalProof=(string)root.Attribute("fatalProof"),Deaths=(int)root.Attribute("deaths"),OwnerEntity=(int)root.Attribute("ownerEntity"),EntityId=(int)root.Attribute("entity"),
                EntityClass=(int)root.Attribute("class"),X=(int)root.Attribute("x"),Z=(int)root.Attribute("z"),State=(int)root.Attribute("state"),SafeRoot=root.Attribute("safeRoot")!=null&&(int)root.Attribute("safeRoot")==1,PosX=(float)root.Attribute("posX"),PosY=(float)root.Attribute("posY"),PosZ=(float)root.Attribute("posZ"),
                Before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)root.Element("before"))),Locks=locks.Select(item=>item!=0).ToArray()};
            if(root.Element("prepared")!=null)record.Prepared=ReadPrepared(original,record,root.Element("prepared"));
            if(record.Operation!=original.Operation||record.Owner!=original.Owner||record.World!=original.World||record.Creation!=original.Creation||record.Deaths!=original.Deaths||
                record.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||!RebirthLogisticsNativeDeathReceipt.TryRecoveryCustody(original,out var fatal)||
                record.FatalProof!=fatal.OwnerFileProof||record.OwnerEntity!=fatal.OwnerEntity||(!record.SafeRoot&&(record.PosX!=fatal.X||record.PosY!=fatal.Y||record.PosZ!=fatal.Z))||
                !XNode.DeepEquals(root,Write(record)))return false;
            var rootPosition=new UnityEngine.Vector3(record.PosX,record.PosY,record.PosZ);var coordinates=World.toChunkXZ(rootPosition);if(coordinates.x!=record.X||coordinates.y!=record.Z)return false;
            if(record.SafeRoot){var anchor=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var anchorChunk=World.toChunkXZ(anchor);if((rootPosition-anchor).sqrMagnitude>32*32||Math.Abs(rootPosition.y-anchor.y)>8||Math.Abs((long)record.X-anchorChunk.x)>1||Math.Abs((long)record.Z-anchorChunk.y)>1)return false;}
            value=record;return true;
        }
        catch{return false;}
    }
    private static bool Publish(RebirthLogisticsTransferJournal.Record original,Record value,bool initial,bool preparation=false)
    {
        try
        {
            if(GameManager.Instance?.World?.IsRemote()!=false||!ThreadManager.IsMainThread()||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World))return false;
            string path=PathFor(original);var next=Write(value);
            if(File.Exists(path+".tmp")||File.Exists(path+".bak"))return false;
            if(initial){if(File.Exists(path)||value.State!=0)return false;}
            else
            {
                if(!TryRead(original,out var prior))return false;
                if(preparation){if(prior.State!=0||value.State!=0||prior.Prepared!=null||value.Prepared==null)return false;}
                else if(value.State!=prior.State+1)return false;
                var normalized=new XElement(next);normalized.SetAttributeValue("state",prior.State);if(preparation)normalized.Element("prepared").Remove();
                if(!XNode.DeepEquals(normalized,Write(prior)))return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));var bytes=Encoding.UTF8.GetBytes(next.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumBytes)return false;
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(initial)File.Move(path+".tmp",path);else File.Replace(path+".tmp",path,null);return TryRead(original,out var saved)&&Same(value,saved);
        }
        catch{return false;}
    }
    private static bool PrepareDistribution(RebirthLogisticsTransferJournal.Record original,ref Record plan,out EntityBackpack preparedBody)
    {
        preparedBody=null;
        if(plan.State!=0)return false;
        // An interrupted same-state publication can only adopt its complete
        // original mapping. A new plan never overwrites an uncertain mapping.
        string path=PathFor(original);
        if(File.Exists(path+".tmp"))
        {
            if(plan.Prepared!=null||!TryRead(original,out var pending,true)||pending.State!=0||pending.Prepared==null)return false;
            var normalized=Write(pending);normalized.Element("prepared").Remove();if(!XNode.DeepEquals(normalized,Write(plan)))return false;
            File.Replace(path+".tmp",path,null);ClearPreparing();if(!TryRead(original,out plan))return false;
        }
        if(preparing!=null&&!preparing.IsCurrent())ClearPreparing();
        // One domain proof-buffer reservation at a time. Other originals wait
        // without repeatedly evicting the active operation's staged work.
        if(preparingPlan!=null&&!Same(preparingPlan,plan))return false;
        if(preparing==null)
        {
            var root=EntityFactory.CreateEntity(plan.EntityClass,plan.EntityId,new UnityEngine.Vector3(plan.PosX,plan.PosY,plan.PosZ),UnityEngine.Vector3.zero) as EntityBackpack;
            var originalLocks=plan.Locks;
            if(root?.bag?.ItemGrid?.items==null||!RebirthLogisticsDeathPartition.Same(root.bag.ItemGrid.items,plan.Before)||
                Enumerable.Range(0,originalLocks.Length).Any(i=>root.bag.ItemGrid.IsLocked(i)!=originalLocks[i])){root?.OnEntityUnload();return false;}
            root.RefPlayerId=plan.OwnerEntity;preparingRoot=root;preparingPlan=plan;preparing=new RebirthLogisticsParcelCoordinator(original,plan,root);
        }
        if(!preparing.TryPrepareRecovery(out var frozen))return false;
        if(plan.Prepared==null)
        {
            // Mutating this detached record does not grant native setters.
            plan.Prepared=frozen;
            if(!Publish(original,plan,false,true)){ClearPreparing();return false;}
        }
        if(!TryRead(original,out plan)||plan.Prepared==null){ClearPreparing();return false;}
        preparedBody=preparingRoot;preparingRoot=null;ClearPreparing();return preparedBody!=null;
    }
    internal static bool TryGetAccepted(RebirthLogisticsTransferJournal.Record original,out RebirthLogisticsDeathCustody.Accepted value)
    {
        value=null;
        if(original==null||!ReferenceEquals(activeWorld,GameManager.Instance?.World)||!accepted.TryGetValue(original.Operation,out var body)||body.Ambiguous||
            !TryRead(original,out var planned)||planned.State!=2||body.EntityId!=planned.EntityId||!ReferenceEquals(body.World.GetEntity(body.EntityId),body.Backpack)||
            body.OwnerFileProof!=planned.FatalProof||!RebirthLogisticsDeathPartition.Same(body.Backpack.bag.ItemGrid.items,planned.Before))return false;
        value=body;return true;
    }
    internal static bool Blocks(ItemStackGrid grid)
    {
        if(grid==null||!ReferenceEquals(activeWorld,GameManager.Instance?.World))return false;
        return accepted.Values.Any(body=>!body.Ambiguous&&ReferenceEquals(body.Backpack?.bag?.ItemGrid,grid)&&
            RebirthLogisticsTransferJournal.TryRead(body.Owner,body.Operation,out var original)&&original.Phase<4);
    }
    internal static void Invalidate(EntityBackpack backpack)
    {foreach(var body in accepted.Values)if(ReferenceEquals(body.Backpack,backpack))body.Ambiguous=true;}
    private static readonly Dictionary<Guid,int> rootSelection=new Dictionary<Guid,int>();
    internal static void SynchronizePending(RebirthLogisticsTransferJournal.Record[] originals)
    {var ids=new HashSet<Guid>(originals.Select(value=>value.Operation));foreach(var id in rootSelection.Keys.Where(id=>!ids.Contains(id)).ToArray())rootSelection.Remove(id);}
    private static bool TryNewRoot(World world,RebirthLogisticsTransferJournal.Record original,RebirthLogisticsNativeDeathReceipt.Record fatal,out EntityBackpack prototype)
    {
        prototype=null;var anchor=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var chunk=World.toChunkXZ(anchor);
        IEnumerable<UnityEngine.Vector3> Positions(){yield return anchor+UnityEngine.Vector3.up*2f;foreach(var point in RebirthLogisticsParcelPlacement.Candidates(world,anchor,chunk.x,chunk.y))yield return point;}
        rootSelection.TryGetValue(original.Operation,out int skip);int index=0,processed=0;
        foreach(var position in Positions())
        {
            if(index++<skip)continue;if(processed++>=8){rootSelection[original.Operation]=index-1;return false;}
            EntityBackpack candidate=null;
            try
            {
                candidate=EntityFactory.CreateEntity("Backpack".GetHashCode(),position) as EntityBackpack;
                if(candidate?.bag?.ItemGrid?.items==null||candidate.entityId<0||candidate.bag.ItemGrid.items.Any(item=>item==null||!item.IsEmpty())||
                    !RebirthLogisticsParcelPlacement.IsSafe(world,anchor,chunk.x,chunk.y,candidate,new UnityEngine.Bounds[0]))continue;
                prototype=candidate;candidate=null;return true;
            }
            finally{candidate?.OnEntityUnload();}
        }
        rootSelection[original.Operation]=0;return false;
    }
    internal static void Tick(RebirthLogisticsTransferJournal.Record original)
    {
        EntityBackpack prototype=null;
        try
        {
            var world=GameManager.Instance?.World;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!CanRecoverOriginal(original,out var fatal))return;
            ResetWorld(world);
            var position=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var coordinates=World.toChunkXZ(position);var chunk=world.GetChunkSync(coordinates.x,coordinates.y) as Chunk;
            if(chunk==null||chunk.InProgressUnloading)return;
            if(!TryRead(original,out var planned))
            {
                string path=PathFor(original);if(File.Exists(path)||File.Exists(path+".bak"))return;
                if(File.Exists(path+".tmp"))
                {
                    // Only a complete initial no-attempt plan can be adopted.
                    // Preserve its original ID/incarnation; an attempted stage
                    // is never converted back into permission to spawn.
                    if(!TryRead(original,out planned,true)||planned.State!=0||world.GetEntity(planned.EntityId)!=null)return;
                    prototype=EntityFactory.CreateEntity(planned.EntityClass,planned.EntityId,new UnityEngine.Vector3(planned.PosX,planned.PosY,planned.PosZ),UnityEngine.Vector3.zero) as EntityBackpack;
                    if(prototype?.bag?.ItemGrid?.items==null||!RebirthLogisticsDeathPartition.Same(prototype.bag.ItemGrid.items,planned.Before)||
                        Enumerable.Range(0,planned.Locks.Length).Any(index=>prototype.bag.ItemGrid.IsLocked(index)!=planned.Locks[index]))return;
                    File.Move(path+".tmp",path);if(!TryRead(original,out planned))return;
                }
                else
                {
                    if(!TryNewRoot(world,original,fatal,out prototype))return;
                    position=prototype.position;coordinates=World.toChunkXZ(position);
                    planned=new Record{Operation=original.Operation,Incarnation=Guid.NewGuid(),Owner=original.Owner,World=original.World,Creation=original.Creation,Binding=fatal.Binding,FatalProof=fatal.OwnerFileProof,
                        Deaths=original.Deaths,OwnerEntity=fatal.OwnerEntity,EntityId=prototype.entityId,EntityClass=prototype.entityClass,X=coordinates.x,Z=coordinates.y,PosX=position.x,PosY=position.y,PosZ=position.z,SafeRoot=true,
                        Before=ItemStack.Clone(prototype.bag.ItemGrid.items),Locks=Enumerable.Range(0,prototype.bag.SlotCount).Select(index=>prototype.bag.ItemGrid.IsLocked(index)).ToArray()};
                    if(!Publish(original,planned,true))return;rootSelection.Remove(original.Operation);
                }
            }
            position=new UnityEngine.Vector3(planned.PosX,planned.PosY,planned.PosZ);
            if(planned.State!=0||world.GetEntity(planned.EntityId)!=null||accepted.Count>=128)return;
            prototype?.OnEntityUnload();prototype=null;
            if(!PrepareDistribution(original,ref planned,out prototype)||!RebirthLogisticsDeathJournal.PrepareNativeRecovery(original,planned))return;
            if(prototype?.bag?.ItemGrid?.items==null||!RebirthLogisticsDeathPartition.Same(prototype.bag.ItemGrid.items,planned.Before)||
                Enumerable.Range(0,planned.Locks.Length).Any(index=>prototype.bag.ItemGrid.IsLocked(index)!=planned.Locks[index]))return;
            if(planned.SafeRoot){var anchor=new UnityEngine.Vector3(fatal.X,fatal.Y,fatal.Z);var anchorChunk=World.toChunkXZ(anchor);if(!RebirthLogisticsParcelPlacement.IsSafe(world,anchor,anchorChunk.x,anchorChunk.y,prototype,new UnityEngine.Bounds[0]))return;}
            prototype.RefPlayerId=fatal.OwnerEntity;EntityCreationData creation=null;
            IDisposable nativeBody=null;
            RebirthLogisticsNativeFrame.Materialization nativeFrame=null;
            EntityBackpack body;
            try
            {
                if(planned.Prepared?.Spawn==null){nativeBody=RebirthLogisticsBackpackPublication.ReserveNativeBody(prototype,original,()=>CanRecoverOriginal(original,out _));if(nativeBody==null)return;creation=new EntityCreationData(prototype,StreamModeWrite.Persistency);}
                if(planned.Prepared?.Spawn!=null&&!RebirthLogisticsBackpackPublication.TryReadSpawn(planned.Prepared.Spawn,planned.EntityId,planned.EntityClass,planned.OwnerEntity,
                    position,planned.Before,planned.Locks,out creation,out nativeFrame))return;
                planned.State=1;if(!Publish(original,planned,false))return; // No replay after this point.
                body=GameManager.Instance.SpawnEntityServer(creation) as EntityBackpack;
            }
            finally{nativeFrame?.Dispose();nativeBody?.Dispose();}
            if(body==null||body.entityId!=planned.EntityId||body.entityClass!=planned.EntityClass||body.RefPlayerId!=planned.OwnerEntity||
                !ReferenceEquals(world.GetEntity(body.entityId),body)||!RebirthLogisticsDeathPartition.Same(body.bag?.ItemGrid?.items,planned.Before))return;
            planned.State=2;if(!Publish(original,planned,false))return;
            accepted.Add(original.Operation,new RebirthLogisticsDeathCustody.Accepted{World=world,Backpack=body,Owner=original.Owner,Creation=original.Creation,Operation=original.Operation,
                OriginalDeaths=original.Deaths,OriginalOwnerEntity=fatal.OwnerEntity,EntityId=body.entityId,EntityClass=body.entityClass,NativeBag=ItemStack.Clone(planned.Before),
                OwnerPhase=fatal.Phase,OwnerFileProof=fatal.OwnerFileProof,RemainingBag=ItemStack.Clone(fatal.RemainingBag),RemainingBelt=ItemStack.Clone(fatal.RemainingBelt)});
        }
        catch{/* Attempted absence, native mismatch, or publication failure retains the original. */}
        finally{prototype?.OnEntityUnload();}
    }
}
// Exact native owner callback observations only. The digest is metadata in the
// native file, never a replacement inventory or a custom-packet image writer.
internal static class RebirthLogisticsDeleteWitness
{
    private const string Prefix="rbLogisticsDelete";
    internal sealed class Context
    {
        internal EntityPlayerLocal Player;internal World World;internal Guid Operation;
        internal int Deaths,Phase,DegradeType;internal float DegradeAmount;internal string Owner,Creation,WorldKey,Marker;internal byte[] EquipmentAfter;
        internal bool Nested,Complete;
    }
    [ThreadStatic]private static Context active;
    internal static byte[] EquipmentBytes(Equipment equipment)
    {using(var blob=StreamUtils.ToBlob(writer=>equipment.Write(writer,StreamModeWrite.Persistency)))return blob.ToArray();}
    // Callers supply detached native clones/readers only. No player/slot writer.
    internal static void DegradeDetached(ItemValue item,int type,float amount)
    {
        if(item==null||type<0||type>3||float.IsNaN(amount)||float.IsInfinity(amount)||amount<0||amount>1)throw new InvalidDataException();
        if(!item.HasQuality)return;
        if(type==2||type==3){float modifier=item.MaxDurabilityModifier-amount;if(modifier<amount)modifier=amount;item.MaxDurabilityModifier=modifier;}
        if(type==1||type==3){int maximum=item.MaxUseTimes;item.UseTimes+=(int)(maximum*amount);if(item.UseTimes>maximum)item.UseTimes=maximum;}
    }
    internal static bool TryEquipmentProjection(byte[] before,out byte[] after,int degradeType=0,float degradeAmount=0.1f)
    {
        after=null;try
        {
            if(before==null||before.Length<1||before.Length>1048576||degradeType<0||degradeType>3||float.IsNaN(degradeAmount)||float.IsInfinity(degradeAmount)||degradeAmount<0||degradeAmount>1)return false;Equipment detached;
            using(var stream=new MemoryStream(before,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {reader.SetBaseStream(stream);detached=Equipment.Read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length)return false;}
            if(detached.GetSlotCount()!=13||!EquipmentBytes(detached).SequenceEqual(before))return false;
            for(int i=0;i<13;i++)
            {
                var item=detached.GetSlotItem(i);if(item==null)return false;
                if(!item.IsEmpty()&&!item.ItemClassOrMissing.KeepOnDeath())detached.ItemGrid[i]=ItemStack.Empty.Clone();
                else if(!item.IsEmpty()&&item.HasQuality)
                {
                    DegradeDetached(item,degradeType,degradeAmount);
                }
            }
            after=EquipmentBytes(detached);return after.Length<=1048576;
        }
        catch{return false;}
    }
    internal static string Digest(Guid operation,string owner,string world,string creation,int deaths,int phase,ItemStack[] bag,ItemStack[] belt,byte[] equipmentBefore,byte[] equipmentAfter,int degradeType,float degradeAmount)
    {
        if(operation==Guid.Empty||owner?.Length!=64||world?.Length!=32||!RebirthOriginalActionScope.Validate(owner,creation)||deaths<0||phase<0||phase>4||
            bag==null||bag.Length<1||bag.Length>256||belt==null||belt.Length<1||belt.Length>256||equipmentBefore==null||equipmentBefore.Length>1048576||
            equipmentAfter==null||equipmentAfter.Length>1048576||degradeType<0||degradeType>3||float.IsNaN(degradeAmount)||float.IsInfinity(degradeAmount)||degradeAmount<0||degradeAmount>1)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write(operation.ToByteArray());writer.Write(owner);writer.Write(world);writer.Write(creation);writer.Write(deaths);writer.Write(phase);writer.Write(6);writer.Write(degradeType);writer.Write(degradeAmount);
            writer.Write(RebirthLogisticsDeathJournal.Hash(bag));writer.Write(RebirthLogisticsDeathJournal.Hash(belt));
            writer.Write(equipmentBefore.Length);writer.Write(equipmentBefore);writer.Write(equipmentAfter.Length);writer.Write(equipmentAfter);writer.Flush();
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();
        }
    }
    private static void Set(EntityPlayerLocal player,string digest,int state)
    {
        player.Buffs.SetCustomVar(Prefix+"State",0f,true);
        for(int i=0;i<16;i++)player.Buffs.SetCustomVar(Prefix+i,Convert.ToUInt16(digest.Substring(i*4,4),16),true);
        player.Buffs.SetCustomVar(Prefix+"State",state,true);
    }
    internal static bool TryRead(PlayerDataFile data,out string marker,out int state)
    {
        marker=null;state=0;try
        {
            var bytes=data?.buffData?.ToArray();if(bytes==null||bytes.Length>1048576)return false;
            var values=new Dictionary<string,float>(StringComparer.Ordinal);
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
                int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
                count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(values.ContainsKey(key))return false;values.Add(key,value);}
                if(stream.Position!=stream.Length)return false;
            }
            if(!values.TryGetValue(Prefix+"State",out float stage)||(stage!=1f&&stage!=2f))return false;
            var text=new StringBuilder(64);bool any=false;
            for(int i=0;i<16;i++)
            {values.TryGetValue(Prefix+i,out float part);if(float.IsNaN(part)||float.IsInfinity(part)||part<0||part>ushort.MaxValue||part!=(int)part)return false;any|=part!=0; text.Append(((ushort)part).ToString("x4"));}
            if(!any)return false;marker=text.ToString();state=(int)stage;return true;
        }
        catch{return false;}
    }
    private static bool Current(Context context)
        =>context!=null&&ReferenceEquals(active,context)&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,context.World)&&context.World?.IsRemote()==true&&
            ReferenceEquals(context.World.GetPrimaryPlayer(),context.Player)&&context.Player.IsDead()&&context.Player.Died==context.Deaths+1&&
            ReferenceEquals(context.World.GetEntity(context.Player.entityId),context.Player)&&RebirthStablePlayerIdentity.TryResolveServerEntity(context.Player,out var owner)&&owner.StorageKey==context.Owner&&
            RebirthCamperCharacterAccess.CurrentCreation(context.Player)==context.Creation&&RebirthLogisticsTransferJournal.WorldKey(context.World)==context.WorldKey&&
            RebirthLogisticsOwnerReceipt.TryOperation(context.Player.Buffs,out var operation,out int phase)&&operation==context.Operation&&phase==context.Phase&&
            context.Player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")==1f&&(int)EntityPlayerLocal.DegradeOnDeathType==context.DegradeType&&EntityPlayerLocal.DegradeAmountOnDeath==context.DegradeAmount;
    internal static object Begin(EntityPlayerLocal player)
    {
        try
        {
            if(active!=null||!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=true||!player.IsDead()||
                (int)EntityPlayerLocal.DropOnDeathOption!=6||string.IsNullOrEmpty(player.dropInventoryBlock)||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out int phase)||phase>4||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=1f||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return null;
            if(!RebirthLogisticsOwnerReceipt.TryOriginalDeaths(player,out int deaths))
            {if(!RebirthLogisticsOriginalTransport.TryReadCurrentOriginal(player,operation,out var authenticated))return null;deaths=authenticated.Deaths;}
            if(deaths==int.MaxValue||player.Died!=deaths+1)return null;
            int degradeType=(int)EntityPlayerLocal.DegradeOnDeathType;float degradeAmount=EntityPlayerLocal.DegradeAmountOnDeath;
            var equipment=EquipmentBytes(player.equipment);if(!TryEquipmentProjection(equipment,out var projected,degradeType,degradeAmount))return null;
            var context=new Context{Player=player,World=player.world,Operation=operation,Deaths=deaths,Phase=phase,Owner=owner.StorageKey,
                Creation=RebirthCamperCharacterAccess.CurrentCreation(player),WorldKey=RebirthLogisticsTransferJournal.WorldKey(player.world),EquipmentAfter=projected,DegradeType=degradeType,DegradeAmount=degradeAmount};
            if(!RebirthOriginalActionScope.Validate(context.Owner,context.Creation)||context.WorldKey?.Length!=32)return null;
            context.Marker=Digest(operation,context.Owner,context.WorldKey,context.Creation,deaths,phase,player.bag.ItemGrid.items,player.inventory.ItemGrid.items,equipment,projected,degradeType,degradeAmount);
            active=context;Set(player,context.Marker,1);RebirthLogisticsOriginalTransport.SendPhysicalEvidence(player,6);return context;
        }
        catch{active=null;return null;}
    }
    internal static bool EnterDrop(EntityPlayerLocal player,bool dying)
    {
        var context=active;if(!dying||!Current(context)||!ReferenceEquals(context.Player,player)||context.Nested||context.Complete||
            (int)EntityPlayerLocal.DropOnDeathOption!=6||string.IsNullOrEmpty(player.dropInventoryBlock))return false;
        context.Nested=true;return true;
    }
    internal static void AfterDrop(EntityPlayerLocal player,bool entered,Exception exception)
    {
        var context=active;
        try
        {
            if(!entered||exception!=null||!Current(context)||!context.Nested||context.Complete||!ReferenceEquals(context.Player,player)||
                (int)EntityPlayerLocal.DropOnDeathOption!=6||player.bag.ItemGrid.items.Any(item=>item==null||!item.IsEmpty())||
                player.inventory.ItemGrid.items.Any(item=>item==null||!item.IsEmpty())||!EquipmentBytes(player.equipment).SequenceEqual(context.EquipmentAfter))return;
            context.Complete=true;Set(player,context.Marker,2);
        }
        catch{/* No completed native witness; original remains unknown. */}
    }
    internal static void End(object state){if(ReferenceEquals(active,state))active=null;}
}

// Observes the actual old-life native pipeline. These native-file markers are
// not an effect receipt by themselves: the server must separately bind original
// source images, its own fatal policy, accepted ECD and final primary data.
internal static class RebirthLogisticsPhysicalLossWitness
{
    private const string Prefix="rbLogisticsLoss";
    private sealed class Context
    {
        internal EntityPlayerLocal Player;internal World World;internal Guid Operation;
        internal string Owner,Creation,WorldKey,BeforeDigest;internal int Deaths,Phase,Mode,LossMode,Minimum,Maximum,DegradeType,Stage;
        internal float DegradeAmount;internal ItemStack[] Bag,Belt,RemovedBag,RemovedBelt,DropBag,DropBelt;
        internal byte[] Equipment,RemovedEquipment,DropEquipment,Selection;internal bool Selected,Failed;
        internal EntityBackpack Builder;internal EntityCreationData RefusedCreation;internal bool BodyByteRefused;internal int Builders,Requests,BodyClass;internal UnityEngine.Vector3 BodyPosition;internal string BodyBagHash,BodyEntityHash;
        internal ItemStack[] Pool;internal string[] PoolKeys;internal readonly List<BuilderEntry> Inputs=new List<BuilderEntry>();internal bool Adding;
    }
    internal sealed class BuilderEntry{internal int Pool,Count;internal bool Failed;}
    private sealed class BuilderCall{internal Context Owner;internal ItemStack Input;internal ItemStack[] Before;internal int Pool;}
    internal sealed class NativeMarker
    {internal bool BodyByteRefused;internal int Stage,BagWidth,BeltWidth,Builders,Requests,BodyClass;internal UnityEngine.Vector3 BodyPosition;internal string Digest,BodyBagHash,BodyEntityHash;internal byte[] Selected;internal BuilderEntry[] Inputs;}
    // Read-only native-file syntax. This deliberately has no original/effect
    // permission API; the domain server receipt must rederive every field.
    internal static bool TryReadMarker(PlayerDataFile data,out NativeMarker marker)
    {
        marker=null;
        try
        {
            var bytes=data?.buffData?.ToArray();if(bytes==null||bytes.Length>1048576)return false;
            var values=new Dictionary<string,float>(StringComparer.Ordinal);
            using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
                int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
                count=reader.ReadUInt16();for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(values.ContainsKey(key))return false;values.Add(key,value);}
                if(stream.Position!=stream.Length)return false;
            }
            bool Integer(string key,int minimum,int maximum,out int result)
            {values.TryGetValue(key,out float value);result=0;if(float.IsNaN(value)||float.IsInfinity(value)||value<minimum||value>maximum||value!=(int)value)return false;result=(int)value;return true;}
            if(!Integer(Prefix+"State",1,5,out int stage)||!Integer(Prefix+"BagWidth",1,256,out int bag)||!Integer(Prefix+"BeltWidth",1,256,out int belt))return false;
            var selected=new byte[(bag+belt+13+7)/8];
            for(int i=0;i<34;i++)
            {
                if(!Integer(Prefix+"Select"+i,0,ushort.MaxValue,out int word))return false;
                for(int part=0;part<2;part++){int index=i*2+part;byte value=(byte)(word>>(part*8));if(index>=selected.Length){if(value!=0)return false;}else selected[index]=value;}
            }
            for(int bit=bag+belt+13;bit<selected.Length*8;bit++)if((selected[bit/8]&(1<<(bit%8)))!=0)return false;
            if(stage==1&&selected.Any(value=>value!=0))return false;
            var digest=new StringBuilder(64);bool any=false;
            for(int i=0;i<16;i++){if(!Integer(Prefix+i,0,ushort.MaxValue,out int word))return false;any|=word!=0;digest.Append(word.ToString("x4"));}
            if(!Integer(Prefix+"BodyByteRefused",0,1,out int refused))return false;
            if(!any||!Integer(Prefix+"Builders",0,1,out int builders)||!Integer(Prefix+"Requests",0,1,out int requests)||stage<4&&(builders!=0||requests!=0||refused!=0)||stage>=4&&(refused==1?builders!=1||requests!=0:builders!=requests))return false;
            string ReadHash(string name)
            {var text=new StringBuilder(64);bool nonzero=false;for(int i=0;i<16;i++){if(!Integer(Prefix+name+i,0,ushort.MaxValue,out int word))throw new InvalidDataException();nonzero|=word!=0;text.Append(word.ToString("x4"));}return nonzero?text.ToString():null;}
            string bodyBag=ReadHash("BodyBag"),bodyEntity=ReadHash("BodyEntity");if(requests==0?(bodyBag!=null||bodyEntity!=null):(bodyBag==null||bodyEntity==null))return false;
            if(!Integer(Prefix+"BodyClass0",0,ushort.MaxValue,out int low)||!Integer(Prefix+"BodyClass1",0,ushort.MaxValue,out int high))return false;
            values.TryGetValue(Prefix+"BodyX",out float x);values.TryGetValue(Prefix+"BodyY",out float y);values.TryGetValue(Prefix+"BodyZ",out float z);
            if(float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)||float.IsNaN(z)||float.IsInfinity(z))return false;
            int bodyClass=unchecked((int)((uint)low|((uint)high<<16)));if(requests==0&&(bodyClass!=0||x!=0||y!=0||z!=0))return false;
            if(!Integer(Prefix+"InputCount",0,bag+belt+13,out int inputCount)||stage<4&&inputCount!=0||builders==0&&inputCount!=0||builders==1&&inputCount==0)return false;
            var inputs=new BuilderEntry[inputCount];
            for(int i=0;i<bag+belt+13;i++)
            {
                if(!Integer(Prefix+"Input"+(i*2),0,ushort.MaxValue,out int code)||!Integer(Prefix+"Input"+(i*2+1),0,ushort.MaxValue,out int count))return false;
                if(i>=inputCount){if(code!=0||count!=0)return false;continue;}
                int pool=(code&32767)-1;if(pool<0||pool>=bag+belt+13||count<1)return false;inputs[i]=new BuilderEntry{Pool=pool,Count=count,Failed=(code&32768)!=0};
            }
            marker=new NativeMarker{BodyByteRefused=refused==1,Stage=stage,BagWidth=bag,BeltWidth=belt,Digest=digest.ToString(),Selected=selected,Builders=builders,Requests=requests,BodyBagHash=bodyBag,BodyEntityHash=bodyEntity,BodyClass=bodyClass,BodyPosition=new UnityEngine.Vector3(x,y,z),Inputs=inputs};return true;
        }
        catch{return false;}
    }
    private static Context pending;
    // Includes an already-failed scoped observer until its real native callback
    // ends, so the generic postdrop save cannot publish pre-clear equipment.
    internal static bool OwnsEpisode(EntityPlayerLocal player)
        =>pending!=null&&ReferenceEquals(pending.Player,player)&&ReferenceEquals(pending.World,GameManager.Instance?.World)&&
            ReferenceEquals(player?.world,pending.World)&&player.IsDead()&&pending.Deaths<int.MaxValue&&player.Died==pending.Deaths+1;
    private static bool Current(Context value)
        =>value!=null&&!value.Failed&&ThreadManager.IsMainThread()&&ReferenceEquals(pending,value)&&ReferenceEquals(GameManager.Instance?.World,value.World)&&
            value.World?.IsRemote()==true&&ReferenceEquals(value.World.GetPrimaryPlayer(),value.Player)&&ReferenceEquals(value.World.GetEntity(value.Player.entityId),value.Player)&&
            value.Player.IsDead()&&value.Deaths<int.MaxValue&&value.Player.Died==value.Deaths+1&&
            SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType)==value.LossMode&&
            RebirthLogisticsOwnerReceipt.TryOperation(value.Player.Buffs,out var operation,out int phase)&&operation==value.Operation&&phase==value.Phase&&
            value.Player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")==1f&&RebirthCamperCharacterAccess.CurrentCreation(value.Player)==value.Creation&&
            RebirthLogisticsTransferJournal.WorldKey(value.World)==value.WorldKey&&RebirthStablePlayerIdentity.TryResolveServerEntity(value.Player,out var owner)&&owner.StorageKey==value.Owner&&
            (int)EntityPlayerLocal.DropOnDeathOption==value.Mode&&(int)EntityPlayerLocal.DegradeOnDeathType==value.DegradeType&&EntityPlayerLocal.DegradeAmountOnDeath==value.DegradeAmount&&
            EntityPlayerLocal.LostItemOnDeathMin==value.Minimum&&EntityPlayerLocal.LostItemOnDeathMax==value.Maximum;
    private static Equipment ReadEquipment(byte[] image)
    {
        if(image==null||image.Length<1||image.Length>1048576)throw new InvalidDataException();
        using(var stream=new MemoryStream(image,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);var result=Equipment.Read(reader,StreamModeRead.Persistency);if(stream.Position!=stream.Length||result.GetSlotCount()!=13||!RebirthLogisticsDeleteWitness.EquipmentBytes(result).SequenceEqual(image))throw new InvalidDataException();return result;}
    }
    internal sealed class Projection
    {
        internal ItemStack[] RemovedBag,RemovedBelt,DegradedBag,DegradedBelt,DroppedEquipment;
        internal byte[] RemovedEquipment,DegradedEquipment,FinalEquipment;
    }
    // Detached native material projection. Selection is supplied only by the
    // actual native-file observer; this pure result grants no mutation lease.
    internal static bool TryProject(ItemStack[] beforeBag,ItemStack[] beforeBelt,byte[] beforeEquipment,byte[] selected,
        int mode,int lossMode,int minimum,int maximum,int degradeType,float degradeAmount,out Projection projection)
    {
        projection=null;
        try
        {
            if(beforeBag==null||beforeBag.Length<1||beforeBag.Length>256||beforeBelt==null||beforeBelt.Length<1||beforeBelt.Length>256||
                beforeBag.Concat(beforeBelt).Any(row=>row==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0)||mode<1||mode>5||lossMode<0||lossMode>5||
                minimum<0||maximum<minimum||maximum>1024||degradeType<0||degradeType>3||float.IsNaN(degradeAmount)||float.IsInfinity(degradeAmount)||degradeAmount<0||degradeAmount>1||
                selected?.Length!=(beforeBag.Length+beforeBelt.Length+13+7)/8)return false;
            var equipment=ReadEquipment(beforeEquipment);var bag=ItemStack.Clone(beforeBag);var belt=ItemStack.Clone(beforeBelt);int eligible=0,drawn=0;
            bool Allowed(int location,int slot)
            {
                if(location==0)return (lossMode==1||lossMode==3||lossMode==5)&&!bag[slot].IsEmpty()&&!bag[slot].itemValue.ItemClassOrMissing.KeepOnDeath();
                if(location==1)return (lossMode==1||lossMode==2||lossMode==5)&&!belt[slot].IsEmpty()&&!belt[slot].itemValue.ItemClassOrMissing.KeepOnDeath();
                var item=equipment.GetSlotItem(slot);return (lossMode==1||lossMode==4)&&((EquipmentSlots)slot).SlotType()!=EquipmentSlotType.BiomeGear&&item!=null&&!item.IsEmpty()&&!item.ItemClassOrMissing.KeepOnDeath();
            }
            for(int bit=0;bit<selected.Length*8;bit++)
            {
                bool chosen=(selected[bit/8]&(1<<(bit%8)))!=0;
                if(bit>=bag.Length+belt.Length+13){if(chosen)return false;continue;}
                int location=bit<bag.Length?0:bit<bag.Length+belt.Length?1:2;int slot=location==0?bit:location==1?bit-bag.Length:bit-bag.Length-belt.Length;
                bool allowed=Allowed(location,slot);if(allowed)eligible++;if(!chosen)continue;if(!allowed)return false;drawn++;
            }
            if(lossMode==0?drawn!=0:drawn<Math.Min(minimum,eligible)||drawn>Math.Min(maximum,eligible))return false;
            for(int bit=0;bit<bag.Length+belt.Length+13;bit++)if((selected[bit/8]&(1<<(bit%8)))!=0)
            {if(bit<bag.Length)bag[bit]=ItemStack.Empty.Clone();else if(bit<bag.Length+belt.Length)belt[bit-bag.Length]=ItemStack.Empty.Clone();else equipment.ItemGrid[bit-bag.Length-belt.Length]=ItemStack.Empty.Clone();}
            var result=new Projection{RemovedBag=ItemStack.Clone(bag),RemovedBelt=ItemStack.Clone(belt),RemovedEquipment=RebirthLogisticsDeleteWitness.EquipmentBytes(equipment)};
            foreach(var row in bag.Concat(belt))if(!row.IsEmpty())RebirthLogisticsDeleteWitness.DegradeDetached(row.itemValue,degradeType,degradeAmount);
            for(int i=0;i<13;i++){var item=equipment.GetSlotItem(i);if(item!=null&&!item.IsEmpty())RebirthLogisticsDeleteWitness.DegradeDetached(item,degradeType,degradeAmount);}
            result.DegradedBag=bag;result.DegradedBelt=belt;result.DegradedEquipment=RebirthLogisticsDeleteWitness.EquipmentBytes(equipment);
            var dropped=new List<ItemStack>();
            if(mode==1||mode==4)for(int i=0;i<13;i++)
            {
                var item=equipment.GetSlotItem(i);if(item==null||item.IsEmpty()||item.ItemClassOrMissing.KeepOnDeath())continue;
                dropped.Add(new ItemStack(item.Clone(),1));equipment.ItemGrid[i]=ItemStack.Empty.Clone();
            }
            result.DroppedEquipment=dropped.ToArray();result.FinalEquipment=RebirthLogisticsDeleteWitness.EquipmentBytes(equipment);projection=result;return true;
        }
        catch{return false;}
    }
    private static string Digest(Context value,int stage)
        =>DigestObserved(value.Operation,value.Owner,value.Creation,value.WorldKey,value.Deaths,value.Phase,value.Mode,value.LossMode,value.Minimum,value.Maximum,
            value.DegradeType,value.DegradeAmount,stage,value.BeforeDigest,value.Selection,value.Player.bag.ItemGrid.items,value.Player.inventory.ItemGrid.items,RebirthLogisticsDeleteWitness.EquipmentBytes(value.Player.equipment),value.Builders,value.Requests,value.BodyBagHash,value.BodyEntityHash,value.BodyClass,value.BodyPosition,value.Inputs.ToArray(),value.BodyByteRefused);
    // Pure native marker rederivation for the domain's server receipt reader.
    // This function does not read/write an owner, source or journal.
    internal static string DigestObserved(Guid operation,string owner,string creation,string world,int deaths,int phase,int mode,int lossMode,int minimum,int maximum,
        int degradeType,float degradeAmount,int stage,string beforeDigest,byte[] selected,ItemStack[] bag,ItemStack[] belt,byte[] equipment,int builders=0,int requests=0,string bodyBagHash=null,string bodyEntityHash=null,int bodyClass=0,UnityEngine.Vector3 bodyPosition=default(UnityEngine.Vector3),BuilderEntry[] inputs=null,bool bodyByteRefused=false)
    {
        if(operation==Guid.Empty||owner?.Length!=64||world?.Length!=32||!RebirthOriginalActionScope.Validate(owner,creation)||deaths<0||phase<0||phase>4||mode<1||mode>5||lossMode<0||lossMode>5||
            minimum<0||maximum<minimum||maximum>1024||degradeType<0||degradeType>3||float.IsNaN(degradeAmount)||float.IsInfinity(degradeAmount)||degradeAmount<0||degradeAmount>1||stage<1||stage>5||
            stage==1&&!string.IsNullOrEmpty(beforeDigest)||stage>1&&beforeDigest?.Length!=64||selected==null||selected.Length<1||selected.Length>66||bag==null||bag.Length<1||bag.Length>256||belt==null||belt.Length<1||belt.Length>256||equipment==null||equipment.Length<1||equipment.Length>1048576)throw new InvalidDataException();
        if(builders<0||builders>1||requests<0||requests>1||stage<4&&(builders!=0||requests!=0||bodyByteRefused)||stage>=4&&(bodyByteRefused?builders!=1||requests!=0:builders!=requests)||(requests==0?(bodyBagHash!=null||bodyEntityHash!=null):(bodyBagHash?.Length!=64||bodyEntityHash?.Length!=64)))throw new InvalidDataException();
        if(float.IsNaN(bodyPosition.x)||float.IsInfinity(bodyPosition.x)||float.IsNaN(bodyPosition.y)||float.IsInfinity(bodyPosition.y)||float.IsNaN(bodyPosition.z)||float.IsInfinity(bodyPosition.z)||requests==0&&(bodyClass!=0||!bodyPosition.Equals(UnityEngine.Vector3.zero)))throw new InvalidDataException();
        inputs=inputs??new BuilderEntry[0];if(inputs.Length>525||stage<4&&inputs.Length!=0||builders==0&&inputs.Length!=0||builders==1&&inputs.Length==0||inputs.Any(row=>row==null||row.Pool<0||row.Pool>=525||row.Count<1||row.Count>ushort.MaxValue))throw new InvalidDataException();
        ReadEquipment(equipment);
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write(operation.ToByteArray());writer.Write(owner);writer.Write(creation);writer.Write(world);writer.Write(deaths);writer.Write(phase);
            writer.Write(mode);writer.Write(lossMode);writer.Write(minimum);writer.Write(maximum);writer.Write(degradeType);writer.Write(degradeAmount);writer.Write(stage);
            writer.Write(beforeDigest??string.Empty);writer.Write(selected.Length);writer.Write(selected);
            writer.Write(RebirthLogisticsDeathJournal.Hash(bag));writer.Write(RebirthLogisticsDeathJournal.Hash(belt));
            writer.Write(equipment.Length);writer.Write(equipment);writer.Write(builders);writer.Write(requests);writer.Write(bodyBagHash??string.Empty);writer.Write(bodyEntityHash??string.Empty);writer.Write(bodyClass);writer.Write(bodyPosition.x);writer.Write(bodyPosition.y);writer.Write(bodyPosition.z);writer.Write(inputs.Length);foreach(var row in inputs){writer.Write((ushort)(row.Pool+1|(row.Failed?32768:0)));writer.Write((ushort)row.Count);}if(bodyByteRefused)writer.Write("native-original-body-byte-refused-v1");writer.Flush();
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();
        }
    }
    private static void Stamp(Context value,int stage)
    {
        if(!Current(value))throw new InvalidDataException();string digest=Digest(value,stage);
        value.Player.Buffs.SetCustomVar(Prefix+"State",0f,true);
        // Fixed bounded native words. Zero omission in native persistency is
        // intentional; a future reader must validate declared exact widths.
        for(int i=0;i<34;i++)
        {int offset=i*2;ushort word=(ushort)((offset<value.Selection.Length?value.Selection[offset]:0)|((offset+1<value.Selection.Length?value.Selection[offset+1]:0)<<8));value.Player.Buffs.SetCustomVar(Prefix+"Select"+i,word,true);}
        value.Player.Buffs.SetCustomVar(Prefix+"BagWidth",value.Bag.Length,true);value.Player.Buffs.SetCustomVar(Prefix+"BeltWidth",value.Belt.Length,true);
        for(int i=0;i<16;i++)value.Player.Buffs.SetCustomVar(Prefix+i,Convert.ToUInt16(digest.Substring(i*4,4),16),true);
        value.Player.Buffs.SetCustomVar(Prefix+"BodyByteRefused",value.BodyByteRefused?1f:0f,false);
        value.Player.Buffs.SetCustomVar(Prefix+"Builders",value.Builders,true);value.Player.Buffs.SetCustomVar(Prefix+"Requests",value.Requests,true);
        for(int i=0;i<16;i++){value.Player.Buffs.SetCustomVar(Prefix+"BodyBag"+i,value.BodyBagHash==null?0:Convert.ToUInt16(value.BodyBagHash.Substring(i*4,4),16),true);value.Player.Buffs.SetCustomVar(Prefix+"BodyEntity"+i,value.BodyEntityHash==null?0:Convert.ToUInt16(value.BodyEntityHash.Substring(i*4,4),16),true);}
        value.Player.Buffs.SetCustomVar(Prefix+"BodyClass0",unchecked((uint)value.BodyClass)&65535,true);value.Player.Buffs.SetCustomVar(Prefix+"BodyClass1",unchecked((uint)value.BodyClass)>>16,true);
        value.Player.Buffs.SetCustomVar(Prefix+"BodyX",value.BodyPosition.x,true);value.Player.Buffs.SetCustomVar(Prefix+"BodyY",value.BodyPosition.y,true);value.Player.Buffs.SetCustomVar(Prefix+"BodyZ",value.BodyPosition.z,true);
        int prior=(int)value.Player.Buffs.GetCustomVar(Prefix+"InputCount");if(prior<0||prior>525)throw new InvalidDataException();
        for(int i=0;i<Math.Max(prior,value.Inputs.Count);i++)
        {var row=i<value.Inputs.Count?value.Inputs[i]:null;value.Player.Buffs.SetCustomVar(Prefix+"Input"+(i*2),row==null?0:row.Pool+1|(row.Failed?32768:0),false);value.Player.Buffs.SetCustomVar(Prefix+"Input"+(i*2+1),row?.Count??0,false);}
        value.Player.Buffs.SetCustomVar(Prefix+"InputCount",value.Inputs.Count,false);
        value.Player.Buffs.SetCustomVar(Prefix+"State",stage,true);value.Stage=stage;
        if(stage==1)value.BeforeDigest=digest;
        // Pre-clear evidence must never replace the native owner save: a cold
        // load restores those cells without replaying dropItemOnDeath.
        if(stage<=4)RebirthLogisticsOriginalTransport.SendPhysicalEvidence(value.Player,stage);
        else RebirthLogisticsOwnerReceipt.UploadDeathBoundary(value.Player);
    }
    internal static void Begin(EntityPlayerLocal player)
    {
        try
        {
            if(!ThreadManager.IsMainThread())return;
            if(pending!=null&&!Current(pending))pending=null;
            if(pending!=null||player?.world?.IsRemote()!=true||!player.IsDead()||(int)EntityPlayerLocal.DropOnDeathOption<1||(int)EntityPlayerLocal.DropOnDeathOption>5||
                string.IsNullOrEmpty(player.dropInventoryBlock)||!ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !RebirthLogisticsOwnerReceipt.TryOperation(player.Buffs,out var operation,out int phase)||phase>4||player.Buffs.GetCustomVar("rbLogisticsOwnerHeld")!=1f||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner))return;
            if(!RebirthLogisticsOwnerReceipt.TryOriginalDeaths(player,out int deaths))
            {if(!RebirthLogisticsOriginalTransport.TryReadCurrentOriginal(player,operation,out var original))return;deaths=original.Deaths;}
            if(deaths==int.MaxValue||player.Died!=deaths+1)return;
            var bag=ItemStack.Clone(player.bag.ItemGrid.items);var belt=ItemStack.Clone(player.inventory.ItemGrid.items);
            if(bag.Length<1||bag.Length>256||belt.Length<1||belt.Length>256||bag.Concat(belt).Any(row=>row==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0))return;
            var equipment=RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment);ReadEquipment(equipment);
            var value=new Context{Player=player,World=player.world,Owner=owner.StorageKey,Operation=operation,Phase=phase,Deaths=deaths,
                Creation=RebirthCamperCharacterAccess.CurrentCreation(player),WorldKey=RebirthLogisticsTransferJournal.WorldKey(player.world),Mode=(int)EntityPlayerLocal.DropOnDeathOption,
                LossMode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType),Minimum=EntityPlayerLocal.LostItemOnDeathMin,Maximum=EntityPlayerLocal.LostItemOnDeathMax,
                DegradeType=(int)EntityPlayerLocal.DegradeOnDeathType,DegradeAmount=EntityPlayerLocal.DegradeAmountOnDeath,Bag=bag,Belt=belt,Equipment=equipment,Selection=new byte[(bag.Length+belt.Length+13+7)/8]};
            if(!RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||value.WorldKey?.Length!=32||value.Minimum<0||value.Maximum<value.Minimum||value.Maximum>1024||
                value.LossMode<0||value.LossMode>5||value.DegradeType<0||value.DegradeType>3||float.IsNaN(value.DegradeAmount)||float.IsInfinity(value.DegradeAmount)||value.DegradeAmount<0||value.DegradeAmount>1)return;
            pending=value;Stamp(value,1);
        }
        catch{if(pending!=null)pending.Failed=true;}
    }
    internal static void Selection(EntityPlayerLocal player)
    {
        var value=pending;
        try
        {
            if(!Current(value)||!ReferenceEquals(value.Player,player)||value.Stage!=1||value.Selected)return;
            var list=player.DestroyLocationSlotList;if(list==null||list.Count>value.Bag.Length+value.Belt.Length+13)throw new InvalidDataException();
            var equipment=ReadEquipment(value.Equipment);int eligible=0;
            bool BagEnabled=value.LossMode==1||value.LossMode==3||value.LossMode==5;
            bool BeltEnabled=value.LossMode==1||value.LossMode==2||value.LossMode==5;
            bool EquipmentEnabled=value.LossMode==1||value.LossMode==4;
            bool Allowed(int location,int slot)
            {
                if(location==0)return BagEnabled&&slot>=0&&slot<value.Bag.Length&&player.dropStackCondition(value.Bag[slot]);
                if(location==1)return BeltEnabled&&slot>=0&&slot<value.Belt.Length&&player.dropStackCondition(value.Belt[slot]);
                return location==2&&EquipmentEnabled&&slot>=0&&slot<13&&((EquipmentSlots)slot).SlotType()!=EquipmentSlotType.BiomeGear&&player.dropValueCondition(equipment.GetSlotItem(slot));
            }
            for(int i=0;i<value.Bag.Length;i++)if(Allowed(0,i))eligible++;
            for(int i=0;i<value.Belt.Length;i++)if(Allowed(1,i))eligible++;
            for(int i=0;i<13;i++)if(Allowed(2,i))eligible++;
            if(list.Count<Math.Min(value.Minimum,eligible)||list.Count>Math.Min(value.Maximum,eligible))throw new InvalidDataException();
            foreach(var row in list)
            {
                int location=(int)row.Location;if(location<0||location>2||!Allowed(location,row.Slot))throw new InvalidDataException();
                int bit=location==0?row.Slot:location==1?value.Bag.Length+row.Slot:value.Bag.Length+value.Belt.Length+row.Slot;
                if((value.Selection[bit/8]&(1<<(bit%8)))!=0)throw new InvalidDataException();value.Selection[bit/8]|=(byte)(1<<(bit%8));
            }
            value.Selected=true;
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static void AfterRemoval(EntityPlayerLocal player)
    {
        var value=pending;
        try
        {
            if(!Current(value)||!ReferenceEquals(value.Player,player)||value.Stage!=1)return;
            // The exact native mask must also satisfy native eligibility and
            // authored draw bounds, including the no-handler/zero-draw case.
            if(!TryProject(value.Bag,value.Belt,value.Equipment,value.Selection,value.Mode,value.LossMode,value.Minimum,value.Maximum,value.DegradeType,value.DegradeAmount,out var projection)||
                !RebirthLogisticsDeathPartition.Same(projection.RemovedBag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(projection.RemovedBelt,player.inventory.ItemGrid.items)||
                !projection.RemovedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();
            value.RemovedBag=projection.RemovedBag;value.RemovedBelt=projection.RemovedBelt;value.RemovedEquipment=projection.RemovedEquipment;Stamp(value,2);
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static void BeforeDrop(EntityPlayerLocal player,bool dying)
    {
        var value=pending;
        try
        {
            if(!dying||!Current(value)||!ReferenceEquals(value.Player,player)||value.Stage!=2)return;
            if(!TryProject(value.Bag,value.Belt,value.Equipment,value.Selection,value.Mode,value.LossMode,value.Minimum,value.Maximum,value.DegradeType,value.DegradeAmount,out var projection)||
                !RebirthLogisticsDeathPartition.Same(projection.DegradedBag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(projection.DegradedBelt,player.inventory.ItemGrid.items)||
                !projection.DegradedEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();
            value.Pool=Pool(projection);value.PoolKeys=value.Pool.Select(row=>RebirthNativeItemCodec.Encode(row.itemValue)).ToArray();Stamp(value,3);
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static ItemStack[] Pool(Projection projection)
        =>projection.DegradedBag.Concat(projection.DegradedBelt).Concat(projection.DroppedEquipment).Where(row=>row!=null&&!row.IsEmpty())
            .GroupBy(row=>RebirthNativeItemCodec.Encode(row.itemValue),StringComparer.Ordinal).OrderBy(group=>group.Key,StringComparer.Ordinal).Select(group=>{var row=group.First().Clone();row.count=1;return row;}).ToArray();
    internal static object BeforeAdd(Bag bag,ItemStack input)
    {
        var value=pending;try
        {
            if(!Current(value)||value.Stage!=3||value.Builder==null||!ReferenceEquals(value.Builder.bag,bag))return null;
            if(value.Adding||input==null||input.IsEmpty()||input.count<1||input.count>ushort.MaxValue||value.Inputs.Count>=value.Bag.Length+value.Belt.Length+13)throw new InvalidDataException();
            string key=RebirthNativeItemCodec.Encode(input.itemValue);int pool=Array.BinarySearch(value.PoolKeys,key,StringComparer.Ordinal);if(pool<0)throw new InvalidDataException();
            value.Adding=true;return new BuilderCall{Owner=value,Input=input.Clone(),Before=ItemStack.Clone(bag.ItemGrid.items),Pool=pool};
        }
        catch{if(value!=null)value.Failed=true;return null;}
    }
    internal static void AfterAdd(Bag bag,ItemStack input,bool result,object state)
    {
        if(!(state is BuilderCall call))return;var value=call.Owner;
        try
        {
            if(!Current(value)||!ReferenceEquals(value.Builder?.bag,bag)||!value.Adding)throw new InvalidDataException();
            if(result)
            {if(!RebirthLogisticsDeathPartition.Conserved(call.Before,new[]{call.Input},bag.ItemGrid.items,new ItemStack[0],new ItemStack[0]))throw new InvalidDataException();}
            else if(!RebirthLogisticsDeathPartition.Same(call.Before,bag.ItemGrid.items)||!RebirthStationGridIngredients.IsSameStackSnapshot(call.Input,input))throw new InvalidDataException();
            value.Inputs.Add(new BuilderEntry{Pool=call.Pool,Count=call.Input.count,Failed=!result});
        }
        catch{value.Failed=true;}
        finally{value.Adding=false;}
    }
    internal static byte[] EncodeInputs(BuilderEntry[] inputs)
    {
        if(inputs==null||inputs.Length>525)throw new InvalidDataException();var bytes=new byte[inputs.Length*4];
        for(int i=0;i<inputs.Length;i++){var row=inputs[i];if(row==null||row.Pool<0||row.Pool>=525||row.Count<1||row.Count>ushort.MaxValue)throw new InvalidDataException();ushort code=(ushort)(row.Pool+1|(row.Failed?32768:0));bytes[4*i]=(byte)code;bytes[4*i+1]=(byte)(code>>8);bytes[4*i+2]=(byte)row.Count;bytes[4*i+3]=(byte)(row.Count>>8);}return bytes;
    }
    internal static BuilderEntry[] DecodeInputs(byte[] bytes)
    {
        if(bytes==null||bytes.Length%4!=0||bytes.Length>2100)throw new InvalidDataException();var result=new BuilderEntry[bytes.Length/4];
        for(int i=0;i<result.Length;i++){int code=bytes[4*i]|bytes[4*i+1]<<8;result[i]=new BuilderEntry{Pool=(code&32767)-1,Failed=(code&32768)!=0,Count=bytes[4*i+2]|bytes[4*i+3]<<8};}
        if(!EncodeInputs(result).SequenceEqual(bytes))throw new InvalidDataException();return result;
    }
    internal static bool SameInputs(BuilderEntry[] a,BuilderEntry[] b)=>a!=null&&b!=null&&EncodeInputs(a).SequenceEqual(EncodeInputs(b));
    internal static bool TryBuilderMaterial(Projection projection,BuilderEntry[] inputs,int maximumInputs,out ItemStack[] failed,out ItemStack[] succeeded)
    {
        failed=null;succeeded=null;
        try
        {
            if(inputs==null||inputs.Length>maximumInputs||maximumInputs>525)return false;var pool=Pool(projection);var spent=new long[pool.Length];
            var budget=projection.DegradedBag.Concat(projection.DegradedBelt).Concat(projection.DroppedEquipment).Where(row=>!row.IsEmpty()).GroupBy(row=>RebirthNativeItemCodec.Encode(row.itemValue),StringComparer.Ordinal)
                .ToDictionary(group=>group.Key,group=>group.Sum(row=>(long)row.count),StringComparer.Ordinal);
            var no=new List<ItemStack>();var yes=new List<ItemStack>();
            foreach(var row in inputs)
            {
                if(row==null||row.Pool<0||row.Pool>=pool.Length||row.Count<1||row.Count>ushort.MaxValue)return false;
                var item=pool[row.Pool].Clone();item.count=row.Count;spent[row.Pool]=checked(spent[row.Pool]+row.Count);
                if(spent[row.Pool]>budget[RebirthNativeItemCodec.Encode(item.itemValue)])return false;
                (row.Failed?no:yes).Add(item);
            }
            failed=no.ToArray();succeeded=yes.ToArray();return true;
        }
        catch{return false;}
    }
    // A counting stream exercises the installed native codec without creating
    // an oversized ECD byte buffer. It never calls ECD.write (whose ushort body
    // length would truncate) and never changes the detached native builder.
    internal static bool OriginalBodyFitsNative(EntityBackpack builder)=>MeasureOriginalBody(builder)==1;
    // 1=fits, 2=positively measured byte overflow, 0=unknown codec failure.
    private static int MeasureOriginalBody(EntityBackpack builder)
    {
        try
        {
            if(builder?.bag==null)return 0;
            using(var stream=new NativeBodyCounter(ushort.MaxValue))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);builder.Write(writer,StreamModeWrite.ToClient);writer.Flush();if(stream.Length<1)return 0;}
            using(var stream=new NativeBodyCounter(1048576))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {writer.SetBaseStream(stream);builder.bag.Write(writer,StreamModeWrite.ToClient);writer.Flush();if(stream.Length<1)return 0;}
            return 1;
        }
        catch(NativeBodyTooLarge){return 2;}
        catch{return 0;}
    }
    private sealed class NativeBodyTooLarge:IOException{}
    private sealed class NativeBodyCounter:Stream
    {
        private long length;private readonly long maximum;
        internal NativeBodyCounter(long maximum){this.maximum=maximum;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Flush(){}
        public override void Write(byte[] buffer,int offset,int count)
        {if(buffer==null||offset<0||count<0||offset>buffer.Length-count)throw new InvalidDataException();if(length>maximum-count)throw new NativeBodyTooLarge();length+=count;}
        public override void WriteByte(byte value){if(length>=maximum)throw new NativeBodyTooLarge();length++;}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
    internal static string BlobHash(MemoryStream stream)
    {if(stream==null||stream.Length<1||stream.Length>2097152)throw new InvalidDataException();using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-",string.Empty).ToLowerInvariant();}
    internal static void Factory(int type,UnityEngine.Vector3 position,Entity entity)
    {
        var value=pending;try
        {
            if(!Current(value)||value.Stage!=3||type!="Backpack".GetHashCode())return;
            if(!(entity is EntityBackpack backpack)||!position.Equals(value.Player.position+value.Player.transform.up*2f)||backpack.bag?.ItemGrid?.items==null||backpack.bag.ItemGrid.items.Any(row=>row==null||!row.IsEmpty())||++value.Builders!=1)throw new InvalidDataException();
            value.Builder=backpack;
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static bool AllowConstruction(EntityCreationData creation,Entity entity,StreamModeWrite mode)
    {
        var value=pending;
        if(!Current(value)||value.Stage!=3||!ReferenceEquals(entity,value.Builder)||mode!=StreamModeWrite.ToClient)return true;
        // Actual caller thereafter assigns only entityName/id, calls the exact
        // Request, then unloads the original detached builder. No other consumer
        // may see this deliberately unserialized constructor result.
        if(value.Builder.RefPlayerId!=value.Player.entityId)
        {value.Failed=true;throw new InvalidDataException("Original native builder owner scope changed before serialization.");}
        int measured=MeasureOriginalBody(value.Builder);
        if(measured==1)return true;
        if(measured!=2||value.RefusedCreation!=null)
        {value.Failed=true;throw new InvalidDataException("Original native body codec outcome is unknown; no unsafe body serialization permitted.");}
        value.RefusedCreation=creation;return false;
    }
    internal static bool AllowRequest(EntityCreationData creation)
    {
        var value=pending;
        if(Current(value)&&value.Stage==3&&ReferenceEquals(creation,value.RefusedCreation))
        {
            if(value.BodyByteRefused||value.Builders!=1||value.Requests!=0||creation.id!=-1){value.Failed=true;return false;}
            value.BodyByteRefused=true;return false; // Typed byte refusal, never native AddItem failure.
        }
        Request(creation);
        if(Current(value)&&value.Stage==3&&value.World.IsRemote())
        {
            RebirthLogisticsNativeBodyIntent.Capture(value.Player,value.Operation,value.Deaths,creation);
            return false; // Full intent/Prepared handshake owns this exact original request.
        }
        return true;
    }
    internal static void Request(EntityCreationData creation)
    {
        var value=pending;try
        {
            if(!Current(value)||value.Stage!=3)return;
            if(value.Builder==null||creation==null||creation.id!=-1||creation.entityClass!=value.Builder.entityClass||!creation.pos.Equals(value.Builder.position)||value.Builder.RefPlayerId!=value.Player.entityId||++value.Requests!=1)throw new InvalidDataException();
            using(var expected=StreamUtils.ToBlob(writer=>value.Builder.bag.Write(writer,StreamModeWrite.ToClient)))if(BlobHash(expected)!=BlobHash(creation.bagData))throw new InvalidDataException();
            value.BodyBagHash=BlobHash(creation.bagData);value.BodyEntityHash=BlobHash(creation.entityData);value.BodyClass=creation.entityClass;value.BodyPosition=creation.pos;
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static void AfterDrop(EntityPlayerLocal player,Exception exception)
    {
        var value=pending;
        try
        {
            if(exception!=null){if(value!=null&&ReferenceEquals(value.Player,player))value.Failed=true;return;}
            if(!Current(value)||!ReferenceEquals(value.Player,player)||value.Stage!=3)return;
            if(value.RefusedCreation!=null&&!value.BodyByteRefused)throw new InvalidDataException();
            value.DropBag=ItemStack.Clone(player.bag.ItemGrid.items);value.DropBelt=ItemStack.Clone(player.inventory.ItemGrid.items);value.DropEquipment=RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment);Stamp(value,4);
        }
        catch{if(value!=null)value.Failed=true;}
    }
    internal static void End(EntityPlayerLocal player,Exception exception)
    {
        var value=pending;
        if(value!=null&&ReferenceEquals(value.Player,player)&&(exception!=null||value.Stage<4))
        {value.Failed=true;pending=null;}
    }
    internal static void BeforeRespawn(EntityPlayerLocal player)
    {
        var value=pending;
        try
        {
            if(!Current(value)||!ReferenceEquals(value.Player,player)||value.Stage!=4)return;
            if(!TryProject(value.Bag,value.Belt,value.Equipment,value.Selection,value.Mode,value.LossMode,value.Minimum,value.Maximum,value.DegradeType,value.DegradeAmount,out var projection)||
                !projection.DegradedEquipment.SequenceEqual(value.DropEquipment)||
                !RebirthLogisticsDeathPartition.Same(value.DropBag,player.bag.ItemGrid.items)||!RebirthLogisticsDeathPartition.Same(value.DropBelt,player.inventory.ItemGrid.items)||
                !projection.FinalEquipment.SequenceEqual(RebirthLogisticsDeleteWitness.EquipmentBytes(player.equipment)))throw new InvalidDataException();
            Stamp(value,5);pending=null;
        }
        catch{if(value!=null)value.Failed=true;}
    }
}

// This receipt is authored only inside the actual server native SetDead
// boundary. A later player-stats death counter or an empty inventory cannot
// create it. It is metadata, never permission to write a new-life inventory.
internal static class RebirthLogisticsNativeDeathReceipt
{
    internal sealed class Record
    {
        internal Guid Operation;internal string Owner,World,Creation,Binding,OwnerFileProof;
        internal int Deaths,OwnerEntity,Mode,Phase=-1;internal float X,Y,Z;
        internal ItemStack[] RemainingBag,RemainingBelt;
        internal int DeletePhase=-1,DeleteDegradeType;internal float DeleteDegradeAmount;internal bool DeletePolicy;internal string DeleteMarker,DeleteBeforeProof;
        internal byte[] DeleteEquipmentBefore,DeleteEquipmentAfter,DeleteInputMask;
        internal bool PhysicalPolicy,PhysicalExpanded;internal int PhysicalDegradeType,PhysicalLossMode,PhysicalMinimum,PhysicalMaximum;internal float PhysicalDegradeAmount;
        internal PhysicalObservation Physical;
    }
    internal sealed class PhysicalObservation
    {
        internal int Stage,OwnerPhase;internal string BeforeDigest,LatestDigest;internal byte[] Input,Selected,Equipment;
        internal bool BodyByteRefused;internal ItemStack[] DropBag,DropBelt;internal RebirthLogisticsPhysicalLossWitness.BuilderEntry[] Inputs=new RebirthLogisticsPhysicalLossWitness.BuilderEntry[0];internal int Builders,Requests,BodyClass;internal UnityEngine.Vector3 BodyPosition;internal string BodyBagHash,BodyEntityHash;
    }
    private static string PathFor(RebirthLogisticsTransferJournal.Record original)
        =>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsNativeDeaths",original.Owner,original.Operation.ToString("N")+".xml");
    internal static bool PartitionReady(RebirthLogisticsTransferJournal.Record original)
    {
        if(!TryRead(original,out var fatal)||!RebirthLogisticsAcceptedBodyJournal.AllowsKnownBody(original))return false;
        if(fatal.Mode<1||fatal.Mode>5)return true;
        return fatal.PhysicalExpanded?fatal.Physical?.Stage==5&&(fatal.Physical.Requests==0||RebirthLogisticsAcceptedBodyJournal.HasPublished(original)):fatal.PhysicalPolicy;
    }
    internal static bool PhysicalPacketMatches(RebirthLogisticsTransferJournal.Record original,Record fatal,int nativeOwner,string bagHash,string entityHash)
    {
        if(fatal?.Physical==null)return false;
        if(nativeOwner==fatal.OwnerEntity)return bagHash==fatal.Physical.BodyBagHash&&entityHash==fatal.Physical.BodyEntityHash;
        return RebirthLogisticsBodyReferenceJournal.TryMatchAccepted(original,nativeOwner,bagHash,entityHash,out int historicalOwner,out _,out var historicalHash)&&historicalOwner==fatal.OwnerEntity&&historicalHash==fatal.Physical.BodyEntityHash;
    }
    internal static bool AcceptedPhysicalMatches(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathCustody.Accepted body)
    {
        if(!TryRead(original,out var fatal)||!RebirthLogisticsAcceptedBodyJournal.AllowsKnownBody(original)||fatal.PhysicalExpanded&&!RebirthLogisticsAcceptedBodyJournal.HasPublished(original))return false;
        if(!fatal.PhysicalExpanded)return true;
        return fatal.Physical?.Stage==5&&fatal.Physical.Requests==1&&body!=null&&PhysicalPacketMatches(original,fatal,body.OriginalOwnerEntity,body.PacketBagHash,body.PacketEntityHash)&&body.PacketClass==fatal.Physical.BodyClass&&body.EntityClass==fatal.Physical.BodyClass&&body.PacketPosition.Equals(fatal.Physical.BodyPosition);
    }
    internal static bool ConservedMaterial(RebirthLogisticsTransferJournal.Record original,int phase,ItemStack[] dropped,ItemStack[] remaining,ItemStack[] belt)
    {
        if(!TryRead(original,out var fatal))return false;
        if(fatal.Mode>=1&&fatal.Mode<=5&&fatal.PhysicalExpanded)return ConservedPhysical(original,phase,dropped,remaining,belt);
        return RebirthLogisticsDeathPartition.TryExpected(original,phase,out var expected)&&
            (RebirthLogisticsDeathPartition.Conserved(expected,original.OriginalOwnerBelt,dropped,remaining,belt)||ConservedPhysical(original,phase,dropped,remaining,belt));
    }
    internal static bool ConservedPhysical(RebirthLogisticsTransferJournal.Record original,int phase,ItemStack[] dropped,ItemStack[] remaining,ItemStack[] remainingBelt)
    {
        try
        {
            if(!TryRead(original,out var fatal)||!fatal.PhysicalPolicy||!RebirthLogisticsAcceptedBodyJournal.AllowsKnownBody(original))return false;
            if(fatal.PhysicalExpanded)
            {
                if(fatal.Physical?.Stage!=5||fatal.Phase!=phase||!ValidatePhysical(original,fatal,out var projection)||
                    !RebirthLogisticsDeathPartition.Same(remaining,fatal.RemainingBag)||!RebirthLogisticsDeathPartition.Same(remainingBelt,fatal.RemainingBelt))return false;
                if(fatal.Physical.Requests==0&&(RebirthLogisticsAcceptedBodyJournal.HasObservedOrUnknownBody(original)||dropped.Any(row=>row==null||!row.IsEmpty()||row.count!=0)))return false;
                if(!RebirthLogisticsPhysicalLossWitness.TryBuilderMaterial(projection,fatal.Physical.Inputs,original.OriginalOwnerBag.Length+original.OriginalOwnerBelt.Length+13,out var failed,out var succeeded)||
                    !RebirthLogisticsDeathPartition.Conserved(fatal.Physical.BodyByteRefused?new ItemStack[0]:succeeded,new ItemStack[0],dropped,new ItemStack[0],new ItemStack[0],607))return false;
                var retained=fatal.Physical.BodyByteRefused?failed.Concat(succeeded):failed;
                return RebirthLogisticsDeathPartition.Conserved(projection.DegradedBag.Concat(projection.DroppedEquipment).ToArray(),projection.DegradedBelt,dropped.Concat(retained).ToArray(),remaining,remainingBelt,607);
            }
            if(fatal.Phase!=-1||!RebirthLogisticsDeathPartition.TryExpected(original,phase,out var before))return false;
            ItemStack[] Transform(ItemStack[] input)
            {
                var output=ItemStack.Clone(input);
                foreach(var row in output)
                {
                    if(row==null)throw new InvalidDataException();if(row.IsEmpty()||!row.itemValue.HasQuality)continue;var item=row.itemValue;
                    RebirthLogisticsDeleteWitness.DegradeDetached(item,fatal.PhysicalDegradeType,fatal.PhysicalDegradeAmount);
                }
                return output;
            }
            return RebirthLogisticsDeathPartition.Conserved(Transform(before),Transform(original.OriginalOwnerBelt),dropped,remaining,remainingBelt);
        }
        catch{return false;}
    }
    internal static bool TryRecoveryCustody(RebirthLogisticsTransferJournal.Record original,out Record receipt)
    {
        receipt=null;if(!TryRead(original,out var actual)||actual.OwnerFileProof?.Length!=64||!RebirthLogisticsAcceptedBodyJournal.AllowsKnownBody(original))return false;
        if(actual.Mode==0&&actual.Phase==2&&RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,actual.RemainingBag,actual.RemainingBelt))
        {receipt=actual;return true;}
        if(actual.Mode==6&&(actual.Phase==1||actual.Phase==2)&&actual.DeleteMarker!=null&&actual.DeleteBeforeProof?.Length==64)
        {receipt=actual;return true;}
        // Exact native zero-material completion OR positively typed scoped
        // constructor byte refusal has no accepted body to adopt. Full typed
        // conservation preserves all refused physical material; absence alone
        // grants nothing, and any conflicting actual accepted body refuses.
        if(actual.Physical?.Stage==5&&actual.Physical.Requests==0&&!RebirthLogisticsAcceptedBodyJournal.HasObservedOrUnknownBody(original)&&!RebirthLogisticsDeathCustody.Snapshot().Any(body=>body.Operation==original.Operation&&body.Owner==original.Owner)&&actual.Phase>=0&&actual.Phase<=4&&
            ConservedPhysical(original,actual.Phase,new ItemStack[0],actual.RemainingBag,actual.RemainingBelt))
        {receipt=actual;return true;}return false;
    }
    internal static bool TryPhysicalOwed(RebirthLogisticsTransferJournal.Record original,out ItemStack[] owed)
    {
        owed=null;if(!TryRead(original,out var fatal)||fatal.Physical?.Stage!=5||fatal.Phase<0||!ValidatePhysical(original,fatal,out var projection)||
            !TryUndeliveredDelete(original,fatal.Phase,fatal.Physical.Input,out var pending)||
            !RebirthLogisticsPhysicalLossWitness.TryBuilderMaterial(projection,fatal.Physical.Inputs,original.OriginalOwnerBag.Length+original.OriginalOwnerBelt.Length+13,out var failed,out var succeeded))return false;
        owed=pending.Concat(failed).Concat(fatal.Physical.BodyByteRefused?succeeded:new ItemStack[0]).ToArray();return true;
    }
    internal static bool TryPreservedOwed(RebirthLogisticsTransferJournal.Record original,out ItemStack[] owed)
        =>TryDeleteOwed(original,out owed)||TryPhysicalOwed(original,out owed);
    internal static bool ConservedPreserved(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death)
        =>ConservedDeleted(original,death)||TryRead(original,out var fatal)&&fatal.Physical?.Stage==5&&fatal.Phase==death.OwnerPhase&&fatal.OwnerFileProof==death.OwnerFileProof&&
            TryPhysicalOwed(original,out var owed)&&RebirthLogisticsDeathPartition.Same(death.PreservedOwed,owed)&&ConservedPhysical(original,death.OwnerPhase,death.Before,death.RemainingBag,death.RemainingBelt);
    internal static bool TryPaidPhysicalCustody(RebirthLogisticsTransferJournal.Record original,out Record receipt)
    {
        receipt=null;if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase<1||original.Phase>4||
            !TryRead(original,out var fatal)||fatal.Physical?.Stage!=5||fatal.Phase!=2||fatal.OwnerFileProof?.Length!=64)return false;
        if(fatal.Physical.Requests==0&&!RebirthLogisticsAcceptedBodyJournal.HasObservedOrUnknownBody(original)&&!RebirthLogisticsDeathCustody.Snapshot().Any(body=>body.Operation==original.Operation&&body.Owner==original.Owner)&&ConservedPhysical(original,2,new ItemStack[0],fatal.RemainingBag,fatal.RemainingBelt)){receipt=fatal;return true;}
        if(!RebirthLogisticsDeathSettlement.TryAcquire(original,out var lease)||lease.Death.OwnerPhase!=2||!lease.ObserveBefore()||lease.Death.State<1||!lease.Current()||
            !ConservedPhysical(original,2,lease.Death.Before,lease.Death.RemainingBag,lease.Death.RemainingBelt))return false;
        receipt=fatal;return true;
    }
    internal static bool TryDeleteOwed(RebirthLogisticsTransferJournal.Record original,out ItemStack[] owed)
    {
        owed=null;
        return TryRead(original,out var fatal)&&fatal.Mode==6&&fatal.Phase>=0&&fatal.DeleteMarker!=null&&
            TryUndeliveredDelete(original,fatal.Phase,fatal.DeleteInputMask,out owed);
    }
    // Pure original arithmetic only. Authority remains the actual fatal +
    // native before/after receipt checked by TryDeleteOwed. A bit means that
    // exact original setter postimage was PHYSICAL before native loss.
    internal static bool TryUndeliveredDelete(RebirthLogisticsTransferJournal.Record original,int phase,byte[] mask,out ItemStack[] owed)
    {
        owed=null;
        if(original?.BeforeOwner==null||original.BeforeOwner.Length<1||original.BeforeOwner.Length>169||
            original.DebitedOwner?.Length!=original.BeforeOwner.Length||original.AfterOwner?.Length!=original.BeforeOwner.Length||
            !TryDeleteInput(original,phase,mask,out _))return false;
        owed=Enumerable.Range(0,original.BeforeOwner.Length).Select(_=>ItemStack.Empty.Clone()).ToArray();
        if(phase==0||phase==3||phase==4)return true;
        if(phase!=1&&phase!=2)return false;
        if(phase==2&&original.Outcome!=1&&original.Outcome!=2)return false;
        var target=phase==1||original.Outcome==2?original.BeforeOwner:original.AfterOwner;
        for(int i=0;i<owed.Length;i++)
        {
            bool advanced=mask!=null&&(mask[i/8]&(1<<(i%8)))!=0;
            if(phase==1?!advanced:advanced)continue;
            var final=target[i];var paid=original.DebitedOwner[i];if(final==null||paid==null)return false;
            int amount=(final.IsEmpty()?0:final.count)-(paid.IsEmpty()?0:paid.count);
            if(amount<0||amount>ushort.MaxValue)return false;if(amount==0)continue;
            if(!paid.IsEmpty()){var a=paid.Clone();var b=final.Clone();a.count=b.count=1;if(!RebirthStationGridIngredients.IsSameStackSnapshot(a,b))return false;}
            owed[i]=final.Clone();owed[i].count=amount;
        }
        return true;
    }
    internal static bool HasDeleteOwed(RebirthLogisticsTransferJournal.Record original)
        =>TryDeleteOwed(original,out var owed)&&owed.Any(item=>!item.IsEmpty());
    internal static bool ConservedDeleted(RebirthLogisticsTransferJournal.Record original,RebirthLogisticsDeathJournal.Record death)
        =>TryRecoveryCustody(original,out var fatal)&&fatal.Mode==6&&TryDeleteOwed(original,out var owed)&&death.OwnerPhase==fatal.Phase&&death.OwnerFileProof==fatal.OwnerFileProof&&
            RebirthLogisticsDeathPartition.Same(death.PreservedOwed,owed)&&death.Before.All(item=>item!=null&&item.IsEmpty()&&item.count==0)&&
            RebirthLogisticsDeathPartition.Same(death.RemainingBag,fatal.RemainingBag)&&RebirthLogisticsDeathPartition.Same(death.RemainingBelt,fatal.RemainingBelt);
    internal static bool TryPaidNoneCustody(RebirthLogisticsTransferJournal.Record original,out Record receipt)
    {
        receipt=null;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase<1||original.Phase>=4||
            !TryRead(original,out var saved)||saved.Mode!=0||saved.Phase!=2||saved.OwnerFileProof?.Length!=64||
            !RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,saved.RemainingBag,saved.RemainingBelt))return false;
        receipt=saved;return true;
    }
    internal static bool TryPaidDeletedCustody(RebirthLogisticsTransferJournal.Record original,out Record receipt)
    {
        receipt=null;
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase<1||original.Phase>4||
            !TryRead(original,out var saved)||saved.Mode!=6||saved.Phase!=2||saved.DeletePhase!=2||saved.OwnerFileProof?.Length!=64||saved.DeleteBeforeProof?.Length!=64)return false;
        receipt=saved;return true;
    }
    private static XElement Write(Record value)
    {
        if(value==null||value.Operation==Guid.Empty||value.Owner?.Length!=64||value.World?.Length!=32||value.Binding?.Length!=64||
            !RebirthOriginalActionScope.Validate(value.Owner,value.Creation)||value.Deaths<0||value.Deaths==int.MaxValue||value.OwnerEntity<0||value.Mode<0||value.Mode>6||
            value.Phase< -1||value.Phase>4||float.IsNaN(value.X)||float.IsInfinity(value.X)||float.IsNaN(value.Y)||float.IsInfinity(value.Y)||float.IsNaN(value.Z)||float.IsInfinity(value.Z)||
            value.Phase<0&&(value.OwnerFileProof!=null||value.RemainingBag!=null||value.RemainingBelt!=null)||value.Phase>=0&&(value.OwnerFileProof?.Length!=64||value.RemainingBag==null||value.RemainingBelt==null))throw new InvalidDataException();
        var root=new XElement("nativeLogisticsDeath",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("owner",value.Owner),
            new XAttribute("world",value.World),new XAttribute("creation",value.Creation),new XAttribute("binding",value.Binding),new XAttribute("deaths",value.Deaths),
            new XAttribute("ownerEntity",value.OwnerEntity),new XAttribute("mode",value.Mode),new XAttribute("x",value.X),new XAttribute("y",value.Y),new XAttribute("z",value.Z),
            new XAttribute("phase",value.Phase),new XAttribute("ownerFileProof",value.OwnerFileProof??string.Empty));
        if(value.PhysicalPolicy)
        {
            if((value.PhysicalExpanded?(value.Mode<1||value.Mode>5):(value.Mode!=2&&value.Mode!=3&&value.Mode!=5))||value.PhysicalDegradeType<0||value.PhysicalDegradeType>3||float.IsNaN(value.PhysicalDegradeAmount)||float.IsInfinity(value.PhysicalDegradeAmount)||value.PhysicalDegradeAmount<0||value.PhysicalDegradeAmount>1)throw new InvalidDataException();
            var policy=new XElement("physicalPolicy",new XAttribute("degradeType",value.PhysicalDegradeType),new XAttribute("degradeAmount",value.PhysicalDegradeAmount));
            if(value.PhysicalExpanded)
            {if(value.PhysicalLossMode<0||value.PhysicalLossMode>5||value.PhysicalMinimum<0||value.PhysicalMaximum<value.PhysicalMinimum||value.PhysicalMaximum>1024)throw new InvalidDataException();policy.Add(new XAttribute("lossMode",value.PhysicalLossMode),new XAttribute("minimum",value.PhysicalMinimum),new XAttribute("maximum",value.PhysicalMaximum));}
            root.Add(policy);
        }
        if(value.Physical!=null)
        {
            var observed=value.Physical;
            if(!value.PhysicalExpanded||observed.Stage<1||observed.Stage>5||observed.OwnerPhase<0||observed.OwnerPhase>4||observed.BeforeDigest?.Length!=64||observed.LatestDigest?.Length!=64||
                observed.Input==null||observed.Input.Length<1||observed.Input.Length>32||observed.Selected==null||observed.Selected.Length<1||observed.Selected.Length>66||observed.Equipment==null||observed.Equipment.Length<1||observed.Equipment.Length>1048576||
                observed.Stage<4&&(observed.DropBag!=null||observed.DropBelt!=null)||observed.Stage>=4&&(observed.DropBag==null||observed.DropBelt==null)||
                observed.Stage<5&&value.Phase>=0||observed.Stage==5&&value.Phase!=observed.OwnerPhase)throw new InvalidDataException();
            var node=new XElement("physicalObservation",new XAttribute("stage",observed.Stage),new XAttribute("ownerPhase",observed.OwnerPhase),new XAttribute("before",observed.BeforeDigest),new XAttribute("latest",observed.LatestDigest),
                new XElement("input",Convert.ToBase64String(observed.Input)),new XElement("selected",Convert.ToBase64String(observed.Selected)),new XElement("equipment",Convert.ToBase64String(observed.Equipment)));
            if(observed.Stage>=4)
            {
                if(observed.Builders<0||observed.Builders>1||(observed.BodyByteRefused?observed.Builders!=1||observed.Requests!=0:observed.Requests!=observed.Builders)||(observed.Requests==0?(observed.BodyBagHash!=null||observed.BodyEntityHash!=null):(observed.BodyBagHash?.Length!=64||observed.BodyEntityHash?.Length!=64)))throw new InvalidDataException();
                node.Add(new XAttribute("builders",observed.Builders),new XAttribute("requests",observed.Requests),new XAttribute("bodyBag",observed.BodyBagHash??string.Empty),new XAttribute("bodyEntity",observed.BodyEntityHash??string.Empty),new XAttribute("bodyClass",observed.BodyClass),new XAttribute("bodyX",observed.BodyPosition.x),new XAttribute("bodyY",observed.BodyPosition.y),new XAttribute("bodyZ",observed.BodyPosition.z));
            }
            if(observed.Stage<4&&(observed.BodyByteRefused||observed.Builders!=0||observed.Requests!=0||observed.BodyBagHash!=null||observed.BodyEntityHash!=null))throw new InvalidDataException();
            if(observed.BodyByteRefused)node.Add(new XAttribute("bodyByteRefused",1));
            if(observed.Stage>=4)node.Add(new XElement("builder",Convert.ToBase64String(RebirthLogisticsPhysicalLossWitness.EncodeInputs(observed.Inputs))));
            else if(observed.Inputs==null||observed.Inputs.Length!=0)throw new InvalidDataException();
            if(observed.Stage==4)node.Add(new XElement("dropBag",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(observed.DropBag))),new XElement("dropBelt",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(observed.DropBelt))));
            root.Add(node);
        }
        if(value.DeletePolicy)
        {
            if(value.Mode!=6||value.DeleteDegradeType<0||value.DeleteDegradeType>3||float.IsNaN(value.DeleteDegradeAmount)||float.IsInfinity(value.DeleteDegradeAmount)||value.DeleteDegradeAmount<0||value.DeleteDegradeAmount>1)throw new InvalidDataException();
            root.Add(new XAttribute("degradeType",value.DeleteDegradeType),new XAttribute("degradeAmount",value.DeleteDegradeAmount));
        }
        if(value.Phase>=0)
        {
            if(value.RemainingBag.Length<1||value.RemainingBag.Length>256||value.RemainingBelt.Length<1||value.RemainingBelt.Length>256)throw new InvalidDataException();
            root.Add(new XElement("bag",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value.RemainingBag))),
                new XElement("belt",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(value.RemainingBelt))));
        }
        if(value.DeleteMarker!=null)
        {
            if(value.Mode!=6||value.DeletePhase<0||value.DeletePhase>4||value.DeleteMarker.Length!=64||value.DeleteBeforeProof?.Length!=64||value.DeleteInputMask!=null&&value.DeleteInputMask.Length>32||
                !RebirthLogisticsDeleteWitness.TryEquipmentProjection(value.DeleteEquipmentBefore,out var after,value.DeleteDegradeType,value.DeleteDegradeAmount)||!after.SequenceEqual(value.DeleteEquipmentAfter))throw new InvalidDataException();
            root.Add(new XElement("delete",new XAttribute("phase",value.DeletePhase),new XAttribute("marker",value.DeleteMarker),new XAttribute("beforeProof",value.DeleteBeforeProof),
                new XElement("equipmentBefore",Convert.ToBase64String(value.DeleteEquipmentBefore)),new XElement("equipmentAfter",Convert.ToBase64String(value.DeleteEquipmentAfter))));
            if(value.DeleteInputMask!=null)root.Element("delete").Add(new XElement("input",Convert.ToBase64String(value.DeleteInputMask)));
        }
        else if(value.DeletePhase!=-1||value.DeleteBeforeProof!=null||value.DeleteEquipmentBefore!=null||value.DeleteEquipmentAfter!=null||value.DeleteInputMask!=null)throw new InvalidDataException();
        if(Encoding.UTF8.GetByteCount(root.ToString(SaveOptions.DisableFormatting))>4194304)throw new InvalidDataException();return root;
    }
    private static bool DeleteStages(RebirthLogisticsTransferJournal.Record original,int phase,out ItemStack[] before,out ItemStack[] alternate)
    {
        before=null;alternate=null;if(phase<0||phase>4||!RebirthLogisticsDeathPartition.TryExpected(original,phase==4?3:phase,out before))return false;
        alternate=before;
        if(phase==1)
        {
            if(!string.IsNullOrEmpty(original.OwnerDebitProof)||original.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||original.Npcs.Any(npc=>npc.Attempted!=0||npc.Published!=0))return false;
            return RebirthLogisticsDeathPartition.TryExpected(original,2,out alternate);
        }
        if(phase==2&&(original.Outcome==1||original.Outcome==2))return RebirthLogisticsDeathPartition.TryExpected(original,3,out alternate);
        return true;
    }
    private static bool TryDeleteInput(RebirthLogisticsTransferJournal.Record original,int phase,byte[] mask,out ItemStack[] input)
    {
        input=null;if(!DeleteStages(original,phase,out var before,out var alternate))return false;
        if(mask==null){input=before;return true;}if(mask.Length!=(before.Length+7)/8)return false;
        input=ItemStack.Clone(before);
        for(int i=0;i<mask.Length*8;i++)if((mask[i/8]&(1<<(i%8)))!=0)
        {
            if(i>=before.Length||RebirthStationGridIngredients.IsSameStackSnapshot(before[i],alternate[i]))return false;input[i]=alternate[i].Clone();
        }
        return true;
    }
    private static bool TryCaptureDeleteInput(RebirthLogisticsTransferJournal.Record original,int phase,ItemStack[] actual,out byte[] mask)
    {
        mask=null;if(!DeleteStages(original,phase,out var before,out var alternate)||actual?.Length!=before.Length)return false;
        var result=new byte[(before.Length+7)/8];
        for(int i=0;i<before.Length;i++)
        {
            if(RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],before[i]))continue;
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(actual[i],alternate[i]))return false;result[i/8]|=(byte)(1<<(i%8));
        }
        mask=result;return true;
    }
    private static string DeletedProof(Record record)
        =>RebirthLogisticsOriginalTransport.Digest(record.Binding+":delete-after:"+record.DeleteMarker+":"+RebirthLogisticsDeathJournal.Hash(record.RemainingBag)+":"+
            RebirthLogisticsDeathJournal.Hash(record.RemainingBelt)+":"+Convert.ToBase64String(record.DeleteEquipmentAfter));
    private static bool ValidateDelete(RebirthLogisticsTransferJournal.Record original,Record record)
    {
        if(record.Mode!=6)return false;
        if(record.DeleteMarker==null)return record.Phase<0;
        if(!record.DeletePolicy)return false;
        if(record.DeletePhase<0||record.DeletePhase>4||record.DeletePhase==4&&!HasSettledPaidOutcome(original)||
            !TryDeleteInput(original,record.DeletePhase,record.DeleteInputMask,out var expected)||
            !RebirthLogisticsDeleteWitness.TryEquipmentProjection(record.DeleteEquipmentBefore,out var equipment,record.DeleteDegradeType,record.DeleteDegradeAmount)||!equipment.SequenceEqual(record.DeleteEquipmentAfter)||
            record.DeleteMarker!=RebirthLogisticsDeleteWitness.Digest(original.Operation,original.Owner,original.World,original.Creation,original.Deaths,record.DeletePhase,
                expected,original.OriginalOwnerBelt,record.DeleteEquipmentBefore,record.DeleteEquipmentAfter,record.DeleteDegradeType,record.DeleteDegradeAmount)||
            record.DeleteBeforeProof!=RebirthLogisticsOriginalTransport.Digest(record.Binding+":delete-before:"+record.DeleteMarker))return false;
        if(record.Phase<0)return true;
        return record.Phase==record.DeletePhase&&record.RemainingBag!=null&&record.RemainingBag.Length>=1&&record.RemainingBag.Length<=256&&record.RemainingBelt?.Length==original.OriginalOwnerBelt.Length&&
            record.RemainingBag.All(item=>item!=null&&item.IsEmpty()&&item.count==0)&&record.RemainingBelt.All(item=>item!=null&&item.IsEmpty()&&item.count==0)&&record.OwnerFileProof==DeletedProof(record);
    }
    private static string PhysicalDigest(RebirthLogisticsTransferJournal.Record original,Record fatal,int stage,string before,byte[] selected,ItemStack[] bag,ItemStack[] belt,byte[] equipment)
        =>RebirthLogisticsPhysicalLossWitness.DigestObserved(original.Operation,original.Owner,original.Creation,original.World,original.Deaths,fatal.Physical.OwnerPhase,
            fatal.Mode,fatal.PhysicalLossMode,fatal.PhysicalMinimum,fatal.PhysicalMaximum,fatal.PhysicalDegradeType,fatal.PhysicalDegradeAmount,stage,before,selected,bag,belt,equipment,stage<4?0:fatal.Physical.Builders,stage<4?0:fatal.Physical.Requests,stage<4?null:fatal.Physical.BodyBagHash,stage<4?null:fatal.Physical.BodyEntityHash,stage<4?0:fatal.Physical.BodyClass,stage<4?UnityEngine.Vector3.zero:fatal.Physical.BodyPosition,stage<4?null:fatal.Physical.Inputs,stage>=4&&fatal.Physical.BodyByteRefused);
    private static string PhysicalProof(Record fatal)
        =>RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":physical-native:"+fatal.Physical.BeforeDigest+":"+fatal.Physical.LatestDigest+":"+
            RebirthLogisticsDeathJournal.Hash(fatal.RemainingBag)+":"+RebirthLogisticsDeathJournal.Hash(fatal.RemainingBelt));
    private static bool ValidatePhysical(RebirthLogisticsTransferJournal.Record original,Record fatal,out RebirthLogisticsPhysicalLossWitness.Projection projection)
    {
        projection=null;var observed=fatal.Physical;
        if(!fatal.PhysicalExpanded||!fatal.PhysicalPolicy||fatal.Mode<1||fatal.Mode>5||observed==null||observed.OwnerPhase==4&&!HasSettledPaidOutcome(original)||
            !TryDeleteInput(original,observed.OwnerPhase,observed.Input,out var before)||observed.Selected?.Length!=(before.Length+original.OriginalOwnerBelt.Length+13+7)/8)return false;
        var zero=new byte[observed.Selected.Length];
        if(observed.BeforeDigest!=PhysicalDigest(original,fatal,1,null,zero,before,original.OriginalOwnerBelt,observed.Equipment))return false;
        ItemStack[] bag=before,belt=original.OriginalOwnerBelt;byte[] equipment=observed.Equipment;
        if(observed.Stage>=2)
        {
            if(!RebirthLogisticsPhysicalLossWitness.TryProject(before,original.OriginalOwnerBelt,observed.Equipment,observed.Selected,fatal.Mode,fatal.PhysicalLossMode,fatal.PhysicalMinimum,fatal.PhysicalMaximum,
                fatal.PhysicalDegradeType,fatal.PhysicalDegradeAmount,out projection))return false;
            bag=observed.Stage==2?projection.RemovedBag:projection.DegradedBag;belt=observed.Stage==2?projection.RemovedBelt:projection.DegradedBelt;
            equipment=observed.Stage==2?projection.RemovedEquipment:observed.Stage==5?projection.FinalEquipment:projection.DegradedEquipment;
            if(observed.Stage>=4)
            {
                if(!RebirthLogisticsPhysicalLossWitness.TryBuilderMaterial(projection,observed.Inputs,before.Length+original.OriginalOwnerBelt.Length+13,out _,out _))return false;
                bag=observed.DropBag;belt=observed.DropBelt;
                if(bag==null||bag.Length<1||bag.Length>256||belt?.Length!=original.OriginalOwnerBelt.Length||bag.Concat(belt).Any(row=>row==null||row.count<0||row.count>ushort.MaxValue||row.IsEmpty()&&row.count!=0))return false;
            }
        }
        else if(observed.Selected.Any(value=>value!=0))return false;
        if(observed.LatestDigest!=PhysicalDigest(original,fatal,observed.Stage,observed.Stage==1?null:observed.BeforeDigest,observed.Selected,bag,belt,equipment))return false;
        if(observed.Stage<5)return fatal.Phase<0;
        return fatal.Phase==observed.OwnerPhase&&RebirthLogisticsDeathPartition.Same(fatal.RemainingBag,bag)&&RebirthLogisticsDeathPartition.Same(fatal.RemainingBelt,belt)&&fatal.OwnerFileProof==PhysicalProof(fatal);
    }
    private sealed class PendingPhysical
    {internal PlayerDataFile Data;internal int Phase,Bytes;internal string Digest;}
    private static readonly Dictionary<string,SortedDictionary<int,PendingPhysical>> physicalPending=new Dictionary<string,SortedDictionary<int,PendingPhysical>>(StringComparer.Ordinal);
    private static World physicalWorld;private static int physicalBytes;
    internal static void ResetWorld(World world)
    {if(ReferenceEquals(physicalWorld,world))return;physicalPending.Clear();physicalBytes=0;physicalWorld=world;}

    internal static bool CanObserveDetachedBytes(string owner,Guid operation,int stage,int bytes)
    {
        if(!ThreadManager.IsMainThread()||bytes<1||bytes>6*1024*1024+32||owner?.Length!=64||operation==Guid.Empty)return false;
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(physicalWorld,world)){physicalPending.Clear();physicalBytes=0;physicalWorld=world;}
        string key=owner+":"+operation.ToString("N")+(stage==6?":delete":string.Empty);
        return (physicalPending.ContainsKey(key)||physicalPending.Count<8)&&physicalBytes<=32*1024*1024-bytes;
    }
    internal static void ObserveDetachedPhysical(EntityPlayer actor,Guid operation,int stage,PlayerDataFile detached)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(actor?.world,world)||!ReferenceEquals(world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||
                original.World!=RebirthLogisticsTransferJournal.WorldKey(world)||original.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                !TryRead(original,out var fatal)||fatal.OwnerEntity!=actor.entityId||detached?.id!=actor.entityId||detached.deaths!=original.Deaths+1||!detached.bDead||
                !RebirthLogisticsOwnerReceipt.TryReadDetachedOperation(detached,out var op,out int phase,out float held)||op!=operation||held!=1f||phase>4||
                (stage==6?(!RebirthLogisticsDeleteWitness.TryRead(detached,out _,out int deleteStage)||deleteStage!=1):
                (!RebirthLogisticsPhysicalLossWitness.TryReadMarker(detached,out var marker)||marker.Stage!=stage||stage<1||stage>4)))return;
            if(stage==6)StageDelete(original,fatal,detached,phase,false);
            else StagePhysical(original,fatal,detached,phase,false);
            if(TryRead(original,out var published))
            {
                int observed=stage==6?(published.DeleteMarker==null?0:6):(published.Physical?.Stage??0);
                if(observed>0)RebirthLogisticsOriginalTransport.SendPhysicalAck(actor,original,observed);
            }
        }
        catch{/* Readonly evidence never publishes native player data or grants an item effect. */}
    }
    private static void StageDelete(RebirthLogisticsTransferJournal.Record original,Record fatal,PlayerDataFile data,int phase,bool primary)
    {
        if(fatal.Mode!=6||!RebirthLogisticsDeleteWitness.TryRead(data,out var digest,out int stage)||primary!=(stage==2)||fatal.Phase>=0)return;
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(physicalWorld,world)){physicalPending.Clear();physicalBytes=0;physicalWorld=world;}
        string key=original.Owner+":"+original.Operation.ToString("N")+":delete";
        var images=new[]{data.bagData,data.inventoryData,data.equipmentData,data.buffData};
        long size=images.Sum(image=>image?.Length??long.MaxValue/8);
        if(size<1||size>6*1024*1024||images.Any(image=>image==null)||data.bagData.Length>2*1024*1024||data.inventoryData.Length>2*1024*1024||data.equipmentData.Length>1048576||data.buffData.Length>1048576)return;
        if(!physicalPending.TryGetValue(key,out var rows)){if(physicalPending.Count>=8)return;rows=new SortedDictionary<int,PendingPhysical>();physicalPending.Add(key,rows);}
        if(rows.ContainsKey(stage)||physicalBytes>32*1024*1024-size)return;
        var copy=new PlayerDataFile{id=data.id,deaths=data.deaths,bDead=data.bDead,bagData=new MemoryStream(data.bagData.ToArray()),inventoryData=new MemoryStream(data.inventoryData.ToArray()),equipmentData=new MemoryStream(data.equipmentData.ToArray()),buffData=new MemoryStream(data.buffData.ToArray())};
        rows.Add(stage,new PendingPhysical{Data=copy,Phase=phase,Bytes=(int)size,Digest=digest});physicalBytes+=(int)size;
        for(int i=0;i<2;i++)
        {
            if(!TryRead(original,out fatal))return;int next=fatal.DeleteMarker==null?1:2;
            if(fatal.Phase>=0||!rows.TryGetValue(next,out var pending))break;
            ObserveDeleteSaved(original,fatal,pending.Data,pending.Phase);
            if(!TryRead(original,out var published)||(next==1?published.DeleteMarker==null:published.Phase<0))break;
            rows.Remove(next);physicalBytes-=pending.Bytes;
        }
        if(rows.Count==0)physicalPending.Remove(key);
    }
    private static void StagePhysical(RebirthLogisticsTransferJournal.Record original,Record fatal,PlayerDataFile data,int phase,bool primary)
    {
        if(!fatal.PhysicalExpanded||!RebirthLogisticsPhysicalLossWitness.TryReadMarker(data,out var marker)||primary!=(marker.Stage==5))return;
        var world=GameManager.Instance?.World;
        if(!ReferenceEquals(physicalWorld,world)){physicalPending.Clear();physicalBytes=0;physicalWorld=world;}
        string key=original.Owner+":"+original.Operation.ToString("N");
        if(marker.Stage<=(fatal.Physical?.Stage??0))return;
        var images=new[]{data.bagData,data.inventoryData,data.equipmentData,data.buffData};
        long size=images.Sum(image=>image?.Length??long.MaxValue/8);
        if(size<1||size>6*1024*1024||images.Any(image=>image==null)||
            data.bagData.Length>2*1024*1024||data.inventoryData.Length>2*1024*1024||data.equipmentData.Length>1048576||data.buffData.Length>1048576)return;
        if(!physicalPending.TryGetValue(key,out var rows))
        {if(physicalPending.Count>=8)return;rows=new SortedDictionary<int,PendingPhysical>();physicalPending.Add(key,rows);}
        if(rows.TryGetValue(marker.Stage,out var prior))return; // First actual stage cannot be replaced by a conflicting retry.
        if(physicalBytes>32*1024*1024-size)return; // Admission BEFORE detached image allocation.
        var copy=new PlayerDataFile{id=data.id,deaths=data.deaths,bDead=data.bDead,bagData=new MemoryStream(data.bagData.ToArray()),inventoryData=new MemoryStream(data.inventoryData.ToArray()),equipmentData=new MemoryStream(data.equipmentData.ToArray()),buffData=new MemoryStream(data.buffData.ToArray())};
        rows.Add(marker.Stage,new PendingPhysical{Data=copy,Phase=phase,Bytes=(int)size,Digest=marker.Digest});physicalBytes+=(int)size;
        for(int count=0;count<5;count++)
        {
            if(!TryRead(original,out fatal))return;fatal=RecoverPhysicalObservation(original,fatal);
            int next=(fatal.Physical?.Stage??0)+1;if(!rows.TryGetValue(next,out var pending))break;
            ObservePhysicalSaved(original,fatal,pending.Data,pending.Phase);
            if(!TryRead(original,out var published)||(published.Physical?.Stage??0)<next)break;
            rows.Remove(next);physicalBytes-=pending.Bytes;
            if(next==5)RebirthLogisticsDeathCustody.ReconcilePublishedPhysical(original);
        }
        if(rows.Count==0)physicalPending.Remove(key);
    }
    private static void ObservePhysicalSaved(RebirthLogisticsTransferJournal.Record original,Record fatal,PlayerDataFile saved,int phase)
    {
        if(!fatal.PhysicalExpanded||fatal.Mode<1||fatal.Mode>5||phase>4||phase==4&&!HasSettledPaidOutcome(original)||fatal.Phase>=0||
            !RebirthLogisticsPhysicalLossWitness.TryReadMarker(saved,out var marker))return;
        var old=Write(fatal);var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);var equipment=saved.equipmentData?.ToArray();
        if(marker.BagWidth!=original.OriginalOwnerBag.Length||marker.BeltWidth!=original.OriginalOwnerBelt.Length||equipment==null||equipment.Length>1048576)return;
        if(fatal.Physical==null)
        {
            if(marker.Stage!=1||!RebirthLogisticsDeathPartition.Same(belt,original.OriginalOwnerBelt)||!TryCaptureDeleteInput(original,phase,bag,out var input))return;
            fatal.Physical=new PhysicalObservation{Stage=1,OwnerPhase=phase,BeforeDigest=marker.Digest,LatestDigest=marker.Digest,Input=input,Selected=marker.Selected,Equipment=equipment};
        }
        else
        {
            var observed=fatal.Physical;if(observed.OwnerPhase!=phase||marker.Stage!=observed.Stage+1)return;
            if(observed.Stage>=2&&!observed.Selected.SequenceEqual(marker.Selected))return;
            observed.Stage=marker.Stage;observed.LatestDigest=marker.Digest;observed.Selected=marker.Selected;
            if(marker.Stage>=4)
            {
                if(marker.Stage==5&&(!RebirthLogisticsPhysicalLossWitness.SameInputs(observed.Inputs,marker.Inputs)||observed.BodyByteRefused!=marker.BodyByteRefused||observed.Builders!=marker.Builders||observed.Requests!=marker.Requests||observed.BodyBagHash!=marker.BodyBagHash||observed.BodyEntityHash!=marker.BodyEntityHash||observed.BodyClass!=marker.BodyClass||!observed.BodyPosition.Equals(marker.BodyPosition)))return;
                observed.BodyByteRefused=marker.BodyByteRefused;observed.Builders=marker.Builders;observed.Requests=marker.Requests;observed.BodyBagHash=marker.BodyBagHash;observed.BodyEntityHash=marker.BodyEntityHash;observed.BodyClass=marker.BodyClass;observed.BodyPosition=marker.BodyPosition;observed.Inputs=marker.Inputs;
                if(marker.Stage==5&&(!RebirthLogisticsDeathPartition.Same(observed.DropBag,bag)||!RebirthLogisticsDeathPartition.Same(observed.DropBelt,belt)))return;
                observed.DropBag=ItemStack.Clone(bag);observed.DropBelt=ItemStack.Clone(belt);
            }
            if(marker.Stage==5){fatal.Phase=phase;fatal.RemainingBag=ItemStack.Clone(bag);fatal.RemainingBelt=ItemStack.Clone(belt);fatal.OwnerFileProof=PhysicalProof(fatal);}
        }
        if(!ValidatePhysical(original,fatal,out var projection))return;
        var actual=fatal.Physical;
        ItemStack[] expectedBag;ItemStack[] expectedBelt;byte[] expectedEquipment;
        if(actual.Stage==1){if(!TryDeleteInput(original,phase,actual.Input,out expectedBag))return;expectedBelt=original.OriginalOwnerBelt;expectedEquipment=actual.Equipment;}
        else
        {
            expectedBag=actual.Stage==2?projection.RemovedBag:actual.Stage==3?projection.DegradedBag:actual.DropBag;
            expectedBelt=actual.Stage==2?projection.RemovedBelt:actual.Stage==3?projection.DegradedBelt:actual.DropBelt;
            expectedEquipment=actual.Stage==2?projection.RemovedEquipment:actual.Stage==5?projection.FinalEquipment:projection.DegradedEquipment;
        }
        if(!RebirthLogisticsDeathPartition.Same(expectedBag,bag)||!RebirthLogisticsDeathPartition.Same(expectedBelt,belt)||!expectedEquipment.SequenceEqual(equipment))return;
        ReplaceObservation(original,old,fatal);
    }
    private static bool CanAdvancePhysical(Record before,Record after)
    {
        if(!before.PhysicalExpanded||!after.PhysicalExpanded||after.Physical==null||before.Phase>=0)return false;
        var a=before.Physical;var b=after.Physical;
        if(a==null){if(b.Stage!=1)return false;}
        else if(b.Stage!=a.Stage+1||b.OwnerPhase!=a.OwnerPhase||b.BeforeDigest!=a.BeforeDigest||!b.Input.SequenceEqual(a.Input)||!b.Equipment.SequenceEqual(a.Equipment)||
            a.Stage>=2&&!b.Selected.SequenceEqual(a.Selected)||a.Stage>=4&&(!RebirthLogisticsPhysicalLossWitness.SameInputs(a.Inputs,b.Inputs)||a.BodyByteRefused!=b.BodyByteRefused||a.Builders!=b.Builders||a.Requests!=b.Requests||a.BodyBagHash!=b.BodyBagHash||a.BodyEntityHash!=b.BodyEntityHash||a.BodyClass!=b.BodyClass||!a.BodyPosition.Equals(b.BodyPosition)||!RebirthLogisticsDeathPartition.Same(a.DropBag,b.DropBag)||!RebirthLogisticsDeathPartition.Same(a.DropBelt,b.DropBelt)))return false;
        var old=Write(before);var next=Write(after);
        foreach(string key in new[]{"phase","ownerFileProof"})next.SetAttributeValue(key,old.Attribute(key).Value);
        next.Elements("bag").Remove();next.Elements("belt").Remove();next.Element("physicalObservation").Remove();
        if(old.Element("physicalObservation")!=null)next.Add(new XElement(old.Element("physicalObservation")));
        return XNode.DeepEquals(old,next);
    }
    private static Record RecoverPhysicalObservation(RebirthLogisticsTransferJournal.Record original,Record current)
    {
        // Stages1-4 are authenticated detached native codec observations; stage5
        // requires actual native primary publication after equipment clear. The whole typed sequence
        // remains incomplete until final native5. This temp is an
        // effect observation, never a prepared source/owner mutation intent.
        string path=PathFor(original);
        if(!File.Exists(path+".tmp")||File.Exists(path+".bak")||!TryRead(original,out var next,true)||!CanAdvancePhysical(current,next))return current;
        if(!TryRead(original,out var actual)||!XNode.DeepEquals(Write(current),Write(actual)))return current;
        File.Replace(path+".tmp",path,null);return TryRead(original,out var saved)&&XNode.DeepEquals(Write(next),Write(saved))?saved:current;
    }
    private static bool ReplaceObservation(RebirthLogisticsTransferJournal.Record original,XElement before,Record next)
    {
        string path=PathFor(original);var document=Write(next);
        if(next.Physical!=null&&(!TryRead(original,out var predecessor)||!CanAdvancePhysical(predecessor,next)))return false;
        if(File.Exists(path+".bak")||!TryRead(original,out var current)||!XNode.DeepEquals(before,Write(current)))return false;
        if(File.Exists(path+".tmp")){if(!TryRead(original,out var temporary,true)||!XNode.DeepEquals(document,Write(temporary)))return false;}
        else
        {
            var bytes=Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        }
        File.Replace(path+".tmp",path,null);return TryRead(original,out var saved)&&XNode.DeepEquals(document,Write(saved));
    }
    private static void ObserveDeleteSaved(RebirthLogisticsTransferJournal.Record original,Record fatal,PlayerDataFile saved,int phase)
    {
        if(fatal.Mode!=6||!fatal.DeletePolicy||phase>4||phase==4&&!HasSettledPaidOutcome(original)||fatal.Phase>=0||
            !RebirthLogisticsDeleteWitness.TryRead(saved,out var marker,out int state))return;
        var before=Write(fatal);var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
        var equipment=saved.equipmentData?.ToArray();if(equipment==null||equipment.Length>1048576)return;
        if(state==1)
        {
            if(fatal.DeleteMarker!=null||!RebirthLogisticsDeathPartition.Same(belt,original.OriginalOwnerBelt)||!TryCaptureDeleteInput(original,phase,bag,out var input)||
                !RebirthLogisticsDeleteWitness.TryEquipmentProjection(equipment,out var projected,fatal.DeleteDegradeType,fatal.DeleteDegradeAmount)||
                marker!=RebirthLogisticsDeleteWitness.Digest(original.Operation,original.Owner,original.World,original.Creation,original.Deaths,phase,bag,belt,equipment,projected,fatal.DeleteDegradeType,fatal.DeleteDegradeAmount))return;
            fatal.DeletePhase=phase;fatal.DeleteMarker=marker;fatal.DeleteInputMask=input;fatal.DeleteEquipmentBefore=equipment;fatal.DeleteEquipmentAfter=projected;
            fatal.DeleteBeforeProof=RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":delete-before:"+marker);
        }
        else
        {
            if(fatal.DeleteMarker!=marker||fatal.DeletePhase!=phase||bag==null||belt==null||bag.Any(item=>item==null||!item.IsEmpty()||item.count!=0)||
                belt.Any(item=>item==null||!item.IsEmpty()||item.count!=0)||!equipment.SequenceEqual(fatal.DeleteEquipmentAfter))return;
            fatal.Phase=phase;fatal.RemainingBag=ItemStack.Clone(bag);fatal.RemainingBelt=ItemStack.Clone(belt);fatal.OwnerFileProof=DeletedProof(fatal);
        }
        if(ValidateDelete(original,fatal))ReplaceObservation(original,before,fatal);
    }
    internal static bool TryRead(RebirthLogisticsTransferJournal.Record original,out Record value,bool pending=false)
    {
        value=null;try
        {
            if(original==null||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance?.World))return false;
            var file=new FileInfo(PathFor(original)+(pending?".tmp":string.Empty));if(!file.Exists||file.Length<1||file.Length>4194304||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            XElement root;using(var reader=XmlReader.Create(file.FullName,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304}))root=XElement.Load(reader);
            if(root.Name!="nativeLogisticsDeath"||root.Attributes().Count()!=(root.Attribute("degradeType")==null?14:16)||(string)root.Attribute("version")!="1")return false;
            int phase=(int)root.Attribute("phase");if(root.Elements().Count()!=(phase<0?0:2)+(root.Element("delete")==null?0:1)+(root.Element("physicalPolicy")==null?0:1)+(root.Element("physicalObservation")==null?0:1))return false;
            var record=new Record{Operation=Guid.ParseExact((string)root.Attribute("operation"),"N"),Owner=(string)root.Attribute("owner"),World=(string)root.Attribute("world"),Creation=(string)root.Attribute("creation"),
                Binding=(string)root.Attribute("binding"),Deaths=(int)root.Attribute("deaths"),OwnerEntity=(int)root.Attribute("ownerEntity"),Mode=(int)root.Attribute("mode"),X=(float)root.Attribute("x"),Y=(float)root.Attribute("y"),Z=(float)root.Attribute("z"),Phase=phase,
                OwnerFileProof=phase<0?null:(string)root.Attribute("ownerFileProof"),RemainingBag=phase<0?null:RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)root.Element("bag"))),
                RemainingBelt=phase<0?null:RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)root.Element("belt")))};
            if(root.Attribute("degradeType")!=null){record.DeletePolicy=true;record.DeleteDegradeType=(int)root.Attribute("degradeType");record.DeleteDegradeAmount=(float)root.Attribute("degradeAmount");}
            var physical=root.Element("physicalPolicy");if(physical!=null)
            {
                record.PhysicalExpanded=physical.Attribute("lossMode")!=null;
                if(physical.Attributes().Count()!=(record.PhysicalExpanded?5:2)||physical.HasElements)return false;
                record.PhysicalPolicy=true;record.PhysicalDegradeType=(int)physical.Attribute("degradeType");record.PhysicalDegradeAmount=(float)physical.Attribute("degradeAmount");
                if(record.PhysicalExpanded){record.PhysicalLossMode=(int)physical.Attribute("lossMode");record.PhysicalMinimum=(int)physical.Attribute("minimum");record.PhysicalMaximum=(int)physical.Attribute("maximum");}
            }
            var observation=root.Element("physicalObservation");if(observation!=null)
            {
                int stage=(int)observation.Attribute("stage");if(observation.Attributes().Count()!=(stage>=4?12:4)+(observation.Attribute("bodyByteRefused")==null?0:1)||observation.Elements().Count()!=(stage==4?6:stage==5?4:3))return false;
                record.Physical=new PhysicalObservation{BodyByteRefused=observation.Attribute("bodyByteRefused")!=null&&(int)observation.Attribute("bodyByteRefused")==1,Stage=stage,Inputs=stage<4?new RebirthLogisticsPhysicalLossWitness.BuilderEntry[0]:RebirthLogisticsPhysicalLossWitness.DecodeInputs(Convert.FromBase64String((string)observation.Element("builder"))),BodyClass=stage<4?0:(int)observation.Attribute("bodyClass"),BodyPosition=stage<4?UnityEngine.Vector3.zero:new UnityEngine.Vector3((float)observation.Attribute("bodyX"),(float)observation.Attribute("bodyY"),(float)observation.Attribute("bodyZ")),Builders=stage<4?0:(int)observation.Attribute("builders"),Requests=stage<4?0:(int)observation.Attribute("requests"),
                    BodyBagHash=stage<4||string.IsNullOrEmpty((string)observation.Attribute("bodyBag"))?null:(string)observation.Attribute("bodyBag"),BodyEntityHash=stage<4||string.IsNullOrEmpty((string)observation.Attribute("bodyEntity"))?null:(string)observation.Attribute("bodyEntity"),OwnerPhase=(int)observation.Attribute("ownerPhase"),BeforeDigest=(string)observation.Attribute("before"),LatestDigest=(string)observation.Attribute("latest"),
                    Input=Convert.FromBase64String((string)observation.Element("input")),Selected=Convert.FromBase64String((string)observation.Element("selected")),Equipment=Convert.FromBase64String((string)observation.Element("equipment")),
                    DropBag=stage<4?null:stage==5?ItemStack.Clone(record.RemainingBag):RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)observation.Element("dropBag"))),DropBelt=stage<4?null:stage==5?ItemStack.Clone(record.RemainingBelt):RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String((string)observation.Element("dropBelt")))};
            }
            var deletion=root.Element("delete");if(deletion!=null)
            {
                if(deletion.Attributes().Count()!=3||deletion.Elements().Count()!=(deletion.Element("input")==null?2:3))return false;
                record.DeletePhase=(int)deletion.Attribute("phase");record.DeleteMarker=(string)deletion.Attribute("marker");record.DeleteBeforeProof=(string)deletion.Attribute("beforeProof");
                record.DeleteInputMask=deletion.Element("input")==null?null:Convert.FromBase64String((string)deletion.Element("input"));
                record.DeleteEquipmentBefore=Convert.FromBase64String((string)deletion.Element("equipmentBefore"));record.DeleteEquipmentAfter=Convert.FromBase64String((string)deletion.Element("equipmentAfter"));
            }
            if(record.Operation!=original.Operation||record.Owner!=original.Owner||record.World!=original.World||record.Creation!=original.Creation||record.Deaths!=original.Deaths||
                record.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original)||!XNode.DeepEquals(root,Write(record)))return false;
            if(record.Mode==6){if(!ValidateDelete(original,record))return false;}
            else if(record.Physical!=null){if(!ValidatePhysical(original,record,out _))return false;}
            else if(phase>=0&&(record.Mode!=0||phase==4&&!HasSettledPaidOutcome(original)||
                !RebirthLogisticsDeathPartition.SurvivedUnchanged(original,phase==4?3:phase,record.RemainingBag,record.RemainingBelt)))return false;
            value=record;return true;
        }
        catch{return false;}
    }
    // Monotonic original-life closure derives the existing canonical durable
    // fatal receipt. Native Died rollback can never revive this admitted life.
    internal static bool IncarnationEnded(RebirthLogisticsTransferJournal.Record original)
        =>original!=null&&TryRead(original,out _);
    internal static void ObserveFatal(EntityPlayer player)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(player?.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||
                !player.IsDead()||player.Health>0||!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||
                !RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var operation,out int phase,out float held,out var saved)||held!=1f||phase>4||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||original.Deaths==int.MaxValue||
                player.Died<original.Deaths||player.Died>original.Deaths+1||saved.deaths<original.Deaths||saved.deaths>original.Deaths+1||
                saved.id!=player.entityId||RebirthCamperCharacterAccess.CurrentCreation(player)!=original.Creation)return;
            string path=PathFor(original);if(File.Exists(path)||File.Exists(path+".bak"))return;
            var position=player.position;var record=new Record{Operation=operation,Owner=original.Owner,World=original.World,Creation=original.Creation,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),
                Deaths=original.Deaths,OwnerEntity=player.entityId,Mode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DropOnDeath),X=position.x,Y=position.y,Z=position.z};
            if(record.Mode>=1&&record.Mode<=5)
            {record.PhysicalPolicy=true;record.PhysicalExpanded=true;record.PhysicalLossMode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType);record.PhysicalMinimum=EntityPlayerLocal.LostItemOnDeathMin;record.PhysicalMaximum=EntityPlayerLocal.LostItemOnDeathMax;
                record.PhysicalDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);record.PhysicalDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            if(record.Mode==6){record.DeletePolicy=true;record.DeleteDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);record.DeleteDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            if(File.Exists(path+".tmp"))
            {
                if(!TryRead(original,out var temporary,true)||!XNode.DeepEquals(Write(record),Write(temporary)))return;
                File.Move(path+".tmp",path);if(record.Mode>=1&&record.Mode<=6)ObserveSaved(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId));return;
            }
            string text=Write(record).ToString(SaveOptions.DisableFormatting);Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(path+".tmp",path);if(record.Mode>=1&&record.Mode<=6)ObserveSaved(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId));
        }
        catch{/* Missing fatal receipt never changes native death. */}
    }
    // Called only from the same actual SetDead observer, after its positive
    // saved phase-zero pre-admission receipt has been validated.
    internal static void CapturePreAdmissionFatal(RebirthLogisticsTransferJournal.Record original,EntityPlayer player)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(player?.world,world)||!ReferenceEquals(world.GetEntity(player.entityId),player)||!player.IsDead()||player.Health>0||original.World!=RebirthLogisticsTransferJournal.WorldKey(world))return;
            string path=PathFor(original);if(File.Exists(path)||File.Exists(path+".bak"))return;
            var position=player.position;var record=new Record{Operation=original.Operation,Owner=original.Owner,World=original.World,Creation=original.Creation,Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original),
                Deaths=original.Deaths,OwnerEntity=player.entityId,Mode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DropOnDeath),X=position.x,Y=position.y,Z=position.z};
            if(record.Mode>=1&&record.Mode<=5)
            {record.PhysicalPolicy=true;record.PhysicalExpanded=true;record.PhysicalLossMode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType);record.PhysicalMinimum=EntityPlayerLocal.LostItemOnDeathMin;record.PhysicalMaximum=EntityPlayerLocal.LostItemOnDeathMax;
                record.PhysicalDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);record.PhysicalDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            if(record.Mode==6){record.DeletePolicy=true;record.DeleteDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);record.DeleteDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            if(File.Exists(path+".tmp"))
            {
                if(!TryRead(original,out var temporary,true)||!XNode.DeepEquals(Write(record),Write(temporary)))return;
                File.Move(path+".tmp",path);if(record.Mode>=1&&record.Mode<=6)ObserveSaved(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId));return;
            }
            string text=Write(record).ToString(SaveOptions.DisableFormatting);Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Move(path+".tmp",path);if(record.Mode>=1&&record.Mode<=6)ObserveSaved(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(player.entityId));
        }
        catch{/* Original remains unknown if actual fatal publication fails. */}
    }
    // Actual SetDead policy attestation for an authenticated never-Admitting
    // header. No owner inventory is present and no native publication claimed.
    internal static void CaptureHistoricalHeaderFatal(RebirthLogisticsNativeBodyIntent.Command command,string nonce,EntityPlayer actor)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!actor.IsDead()||actor.Health>0||actor.Died<command.Deaths||actor.Died>command.Deaths+1||!RebirthLogisticsHeaderAdmission.TryHistoricalHeader(actor,command.Operation,nonce,out var actualHeader,out int headerEntity)||headerEntity!=actor.entityId||actualHeader.Binding!=command.Binding)return;
            var position=actor.position;var r=new Record{Operation=command.Operation,Owner=command.Owner,World=command.World,Creation=command.Creation,Binding=command.Binding,Deaths=command.Deaths,OwnerEntity=actor.entityId,
                Mode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DropOnDeath),X=position.x,Y=position.y,Z=position.z};
            if(r.Mode>=1&&r.Mode<=5){r.PhysicalPolicy=true;r.PhysicalExpanded=true;r.PhysicalLossMode=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.LoseItemsOnDeathType);r.PhysicalMinimum=EntityPlayerLocal.LostItemOnDeathMin;r.PhysicalMaximum=EntityPlayerLocal.LostItemOnDeathMax;r.PhysicalDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);r.PhysicalDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            if(r.Mode==6){r.DeletePolicy=true;r.DeleteDegradeType=SandboxOptions.SandboxOptionManager.GetInt(global::SandboxOptions.SandboxOptions.DegradeItemsOnDeath);r.DeleteDegradeAmount=SandboxOptions.SandboxOptionManager.GetFloat(global::SandboxOptions.SandboxOptions.DegradeAmountOnDeath);}
            string path=HistoricalFatalPath(command.Owner,command.Operation);var xml=new XElement("logisticsHistoricalFatal",new XAttribute("version",1),new XAttribute("header",nonce),Write(r));
            if(File.Exists(path)||File.Exists(path+".tmp")||File.Exists(path+".bak"))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] bytes=Encoding.UTF8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));if(bytes.Length>16384)return;
            using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}File.Move(path+".tmp",path);
        }
        catch{/* Unknown actual fatal cannot be reconstructed later. */}
    }
    private static string HistoricalFatalPath(string owner,Guid operation)=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthLogisticsHistoricalFatal",owner,operation.ToString("N")+".xml");
    internal static bool HasHistoricalFatalCandidate(string owner,Guid operation)=>File.Exists(HistoricalFatalPath(owner,operation))||File.Exists(HistoricalFatalPath(owner,operation)+".tmp")||File.Exists(HistoricalFatalPath(owner,operation)+".bak");
    internal static bool TryImportHistoricalFatal(RebirthLogisticsTransferJournal.Record original,string nonce)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase!=0||original.Legs.Count!=0||original.Npcs.Count!=0||original.BeforeOwner==null||original.DebitedOwner==null||original.AfterOwner==null||original.BeforeOwner.Any(x=>x==null||!x.IsEmpty())||original.DebitedOwner.Any(x=>x==null||!x.IsEmpty())||original.AfterOwner.Any(x=>x==null||!x.IsEmpty()))return false;
            string path=HistoricalFatalPath(original.Owner,original.Operation),source=File.Exists(path)?path:path+".tmp";if(!File.Exists(source)||File.Exists(path)&&File.Exists(path+".tmp")||File.Exists(path+".bak")||new FileInfo(source).Length>16384||(File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)return false;
            XElement xml;using(var reader=XmlReader.Create(source,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))xml=XElement.Load(reader);
            if(xml.Name!="logisticsHistoricalFatal"||xml.Attributes().Count()!=2||(string)xml.Attribute("version")!="1"||(string)xml.Attribute("header")!=nonce||xml.Elements().Count()!=1)return false;
            var n=xml.Element("nativeLogisticsDeath");if(n==null)return false;
            var r=new Record{Operation=Guid.ParseExact((string)n.Attribute("operation"),"N"),Owner=(string)n.Attribute("owner"),World=(string)n.Attribute("world"),Creation=(string)n.Attribute("creation"),Binding=(string)n.Attribute("binding"),Deaths=(int)n.Attribute("deaths"),OwnerEntity=(int)n.Attribute("ownerEntity"),Mode=(int)n.Attribute("mode"),Phase=(int)n.Attribute("phase"),X=(float)n.Attribute("x"),Y=(float)n.Attribute("y"),Z=(float)n.Attribute("z")};
            var physical=n.Element("physicalPolicy");if(physical!=null){r.PhysicalPolicy=true;r.PhysicalExpanded=true;r.PhysicalLossMode=(int)physical.Attribute("lossMode");r.PhysicalMinimum=(int)physical.Attribute("minimum");r.PhysicalMaximum=(int)physical.Attribute("maximum");r.PhysicalDegradeType=(int)physical.Attribute("degradeType");r.PhysicalDegradeAmount=(float)physical.Attribute("degradeAmount");}
            if(r.Mode==6){r.DeletePolicy=true;r.DeleteDegradeType=(int)n.Attribute("degradeType");r.DeleteDegradeAmount=(float)n.Attribute("degradeAmount");}
            if(r.Operation!=original.Operation||r.Owner!=original.Owner||r.World!=original.World||r.Creation!=original.Creation||r.Deaths!=original.Deaths||r.Binding!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding||r.Phase!=-1||!XNode.DeepEquals(n,Write(r)))return false;
            if(!(GameManager.Instance.World.GetEntity(r.OwnerEntity) is EntityPlayer actor)||!RebirthLogisticsHeaderAdmission.TryHistoricalHeader(actor,original.Operation,nonce,out var header,out int entity)||entity!=r.OwnerEntity||header.Binding!=RebirthLogisticsNativeBodyIntent.Command.From(original).Binding)return false;
            if(source!=path)File.Move(source,path);r.Binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);string target=PathFor(original);var expected=Write(r);
            if(File.Exists(target))return TryRead(original,out var actual)&&XNode.DeepEquals(Write(actual),expected);
            if(File.Exists(target+".bak"))return false;
            if(File.Exists(target+".tmp")){if(!TryRead(original,out var pending,true)||!XNode.DeepEquals(Write(pending),expected))return false;File.Move(target+".tmp",target);return TryRead(original,out _);}
            Directory.CreateDirectory(Path.GetDirectoryName(target));byte[] body=Encoding.UTF8.GetBytes(expected.ToString(SaveOptions.DisableFormatting));
            using(var stream=new FileStream(target+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(body,0,body.Length);stream.Flush(true);}File.Move(target+".tmp",target);return TryRead(original,out _);
        }
        catch{return false;}
    }
    internal static void ObserveSaved(ClientInfo sender)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||sender==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||
                !RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var owner)||!RebirthLogisticsOwnerReceipt.TrySavedOperation(owner,out var operation,out int phase,out float held,out var saved)||
                held!=1f||phase>4||!RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out var original)||original.Phase>=4||!TryRead(original,out var fatal)||
                saved.id!=fatal.OwnerEntity||saved.id!=sender.entityId||saved.deaths!=original.Deaths+1||
                RebirthCamperCharacterAccess.CurrentCreation(GameManager.Instance.World.GetEntity(sender.entityId) as EntityPlayer)!=original.Creation)return;
            if(fatal.Mode==6){StageDelete(original,fatal,saved,phase,true);return;}
            if(fatal.PhysicalExpanded){StagePhysical(original,fatal,saved,phase,true);return;}
            if(fatal.Mode!=0)return;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            if(phase==4&&!HasSettledPaidOutcome(original)||!RebirthLogisticsDeathPartition.SurvivedUnchanged(original,phase==4?3:phase,bag,belt))return;
            string proof=RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":"+saved.deaths+":"+phase+":"+RebirthLogisticsDeathJournal.Hash(bag)+":"+RebirthLogisticsDeathJournal.Hash(belt));
            if(fatal.Phase>=0)return; // First positively saved original partition is immutable.
            string path=PathFor(original);var before=Write(fatal);fatal.Phase=phase;fatal.OwnerFileProof=proof;fatal.RemainingBag=ItemStack.Clone(bag);fatal.RemainingBelt=ItemStack.Clone(belt);
            string text=Write(fatal).ToString(SaveOptions.DisableFormatting);
            if(File.Exists(path+".bak")||!TryRead(original,out var current)||!XNode.DeepEquals(before,Write(current)))return;
            if(File.Exists(path+".tmp"))
            {if(!TryRead(original,out var temporary,true)||!XNode.DeepEquals(Write(fatal),Write(temporary)))return;}
            else using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            File.Replace(path+".tmp",path,null);
        }
        catch{/* No native/file proof means retained unknown, never assumed loss. */}
    }
    internal static void CloseSurvivingOriginal(RebirthLogisticsTransferJournal.Record original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original==null||original.Phase>=4||
                !TryRead(original,out var fatal)||fatal.Phase<0)return;
            if(fatal.Mode==6){CloseDeletedOriginal(original,fatal);return;}
            if(fatal.Physical!=null){ClosePhysicalUnpaid(original,fatal);ClosePhysicalPaidNoOwed(original,fatal);return;}
            if(fatal.Mode!=0)return;
            if(fatal.Phase==3||fatal.Phase==4){ClosePaidAfter(original,fatal);return;}
            var world=GameManager.Instance.World;string binding=fatal.Binding,proof=fatal.OwnerFileProof;
            bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&
                TryRead(original,out var saved)&&saved.Binding==binding&&saved.OwnerFileProof==proof&&saved.Phase==fatal.Phase&&
                RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)==binding;
            if(fatal.Phase>1||original.Phase>1||!string.IsNullOrEmpty(original.OwnerDebitProof)||original.Outcome!=0||
                original.Legs.Any(value=>value.Attempted!=0||value.Published!=0||value.Restored)||original.Npcs.Any(value=>value.Attempted!=0||value.Published!=0))return;
            // Native SetDead + the exact final None-mode BEFORE partition
            // prove the ended incarnation never received a paid debit. Do not
            // require an invented backpack to close this no-effect original.
            original.Phase=1;original.Rejecting=true;
            if(!RebirthLogisticsTransferJournal.TryPublish(original,Current))return;
            original.OwnerNoEffectProof=RebirthLogisticsOriginalTransport.Digest(binding+":"+proof+":native-death-before");original.Outcome=3;original.Phase=2;
            RebirthLogisticsTransferJournal.TryPublish(original,Current);
        }
        catch{/* A failed publication retains its exact original. */}
    }

    private static void ClosePhysicalPaidNoOwed(RebirthLogisticsTransferJournal.Record original,Record fatal)
    {
        if(original.Phase<2||original.Phase>3||!HasSettledPaidOutcome(original)||fatal.Physical?.Stage!=5||fatal.Physical.Builders!=0||fatal.Physical.Requests!=0||
            !TryPhysicalOwed(original,out var owed)||owed.Any(row=>!row.IsEmpty())||!ConservedPhysical(original,fatal.Phase,new ItemStack[0],fatal.RemainingBag,fatal.RemainingBelt))return;
        var world=GameManager.Instance?.World;string binding=fatal.Binding,proof=fatal.OwnerFileProof;
        bool Current()
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world?.IsRemote()!=false||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||!HasSettledPaidOutcome(retained)||retained.Outcome!=original.Outcome||
                retained.OwnerDebitProof!=original.OwnerDebitProof||RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)!=binding||!TryRead(retained,out var saved)||saved.OwnerFileProof!=proof||!XNode.DeepEquals(Write(saved),Write(fatal)))return false;
            var actor=world.GetEntity(fatal.OwnerEntity) as EntityPlayer;
            return actor!=null&&ReferenceEquals(actor.world,world)&&ReferenceEquals(world.GetEntity(actor.entityId),actor)&&retained.Deaths<int.MaxValue&&actor.Died==retained.Deaths+1&&
                RebirthCamperCharacterAccess.CurrentCreation(actor)==retained.Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==retained.Owner;
        }
        // The retained receipt is an actual authenticated SAVED old-life effect,
        // not intent. A later legitimate new-life file need not resemble lost
        // old-life material. This branch performs terminal metadata only.
        if(!Current())return;original.Phase++;RebirthLogisticsTransferJournal.TryPublish(original,Current);
    }
    private static void ClosePhysicalUnpaid(RebirthLogisticsTransferJournal.Record original,Record fatal)
    {
        if(fatal.Physical?.Stage!=5||fatal.Phase>1||!ValidatePhysical(original,fatal,out _)||!TryPhysicalOwed(original,out var owed)||
            original.Phase>1||original.Outcome!=0||!string.IsNullOrEmpty(original.OwnerDebitProof)||original.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||original.Npcs.Any(npc=>npc.Attempted!=0||npc.Published!=0))return;
        var world=GameManager.Instance.World;string binding=fatal.Binding,proof=fatal.OwnerFileProof;
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)&&
            RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)==binding&&TryRead(retained,out var saved)&&XNode.DeepEquals(Write(saved),Write(fatal));
        if(!Current())return;original.Phase=1;original.Rejecting=true;if(!RebirthLogisticsTransferJournal.TryPublish(original,Current))return;
        original.OwnerNoEffectProof=RebirthLogisticsOriginalTransport.Digest(binding+":"+proof+":native-physical-unpaid");original.OwnerEscrowRecovery=owed.Any(item=>!item.IsEmpty());
        original.Outcome=3;original.Phase=2;RebirthLogisticsTransferJournal.TryPublish(original,Current);
    }
    private static void CloseDeletedOriginal(RebirthLogisticsTransferJournal.Record original,Record fatal)
    {
        if(!ValidateDelete(original,fatal)||fatal.Phase<0||fatal.DeleteMarker==null)return;
        // Unknown original custody is never equivalent to an empty obligation.
        if(!TryDeleteOwed(original,out var owed))return;
        bool hasOwed=owed.Any(item=>!item.IsEmpty());
        var world=GameManager.Instance.World;string binding=fatal.Binding,proof=fatal.OwnerFileProof;
        bool PaidActorCurrent()
        {
            var actor=world.GetEntity(fatal.OwnerEntity) as EntityPlayer;
            return actor!=null&&!actor.IsDead()&&original.Deaths<int.MaxValue&&actor.Died==original.Deaths+1&&RebirthCamperCharacterAccess.CurrentCreation(actor)==original.Creation&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&owner.StorageKey==original.Owner;
        }
        bool Current()=>ReferenceEquals(GameManager.Instance?.World,world)&&world.IsRemote()==false&&(fatal.Phase<2||PaidActorCurrent())&&
            RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)==binding&&
            TryRead(retained,out var saved)&&saved.OwnerFileProof==proof&&XNode.DeepEquals(Write(saved),Write(fatal));
        if(fatal.Phase<=1)
        {
            if(original.Phase>1||original.Outcome!=0||!string.IsNullOrEmpty(original.OwnerDebitProof)||
                original.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||original.Npcs.Any(npc=>npc.Attempted!=0||npc.Published!=0)||!Current())return;
            original.Phase=1;original.Rejecting=true;if(!RebirthLogisticsTransferJournal.TryPublish(original,Current))return;
            original.OwnerNoEffectProof=RebirthLogisticsOriginalTransport.Digest(binding+":"+fatal.DeleteBeforeProof+":"+proof+":native-delete-unpaid");
            original.OwnerEscrowRecovery=hasOwed;
            original.Outcome=3;original.Phase=2;RebirthLogisticsTransferJournal.TryPublish(original,Current);return;
        }
        // Native deletion governs only positively witnessed physical cells.
        // Undelivered/refund custody follows the original native recovery plan;
        // terminal metadata is allowed here only when no owed custody remains.
        if(hasOwed||!TryDeleteOwed(original,out _)||original.Phase<2||original.Phase>3||!HasSettledPaidOutcome(original)||!Current())return;
        original.Phase++;RebirthLogisticsTransferJournal.TryPublish(original,Current);
    }
    // Readonly export of existing positively saved old-life settlement. This
    // grants terminal hold metadata only, never a native/source item setter.
    internal static bool TryTerminalPhysicalProof(RebirthLogisticsTransferJournal.Record original,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||original?.Phase!=4||original.World!=RebirthLogisticsTransferJournal.WorldKey(GameManager.Instance.World)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||RebirthLogisticsTransferJournal.Encode(retained)!=RebirthLogisticsTransferJournal.Encode(original)||!TryRead(original,out var fatal)||fatal.OwnerFileProof?.Length!=64||
                (original.Outcome==3?original.OwnerNoEffectProof?.Length!=64:!HasSettledPaidOutcome(original)))return false;
            if(RebirthLogisticsDeathJournal.TryRead(original,out var death)&&death.State>=3&&death.Outcome==original.Outcome&&death.BeforeProof?.Length==64&&death.FinalProof?.Length==64&&death.Parcels.All(x=>x.State>=3&&x.BeforeProof?.Length==64&&x.FinalProof?.Length==64))
            {proof=RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":"+fatal.OwnerFileProof+":"+death.BeforeProof+":"+death.FinalProof+":"+string.Join(":",death.Parcels.Select(x=>x.Incarnation.ToString("N")+":"+x.BeforeProof+":"+x.FinalProof)));return true;}
            if(original.OwnerEscrowRecovery)return false;
            bool none=fatal.Mode==0&&((fatal.Phase==3||fatal.Phase==4)&&HasSettledPaidOutcome(original)&&RebirthLogisticsDeathPartition.SurvivedUnchanged(original,3,fatal.RemainingBag,fatal.RemainingBelt)||original.Outcome==3&&fatal.Phase<=1&&fatal.Phase>=0&&RebirthLogisticsDeathPartition.SurvivedUnchanged(original,0,fatal.RemainingBag,fatal.RemainingBelt));
            bool deleted=fatal.Mode==6&&fatal.DeleteMarker!=null&&ValidateDelete(original,fatal)&&TryDeleteOwed(original,out var deletedOwed)&&deletedOwed.All(x=>x.IsEmpty());
            bool noBody=fatal.Physical?.Stage==5&&fatal.Physical.Requests==0&&ValidatePhysical(original,fatal,out _)&&ConservedPhysical(original,fatal.Phase,new ItemStack[0],fatal.RemainingBag,fatal.RemainingBelt)&&TryPhysicalOwed(original,out var physicalOwed)&&physicalOwed.All(x=>x.IsEmpty());
            if(none||deleted||noBody){proof=RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":"+fatal.OwnerFileProof+":terminal-native:"+fatal.Mode+":"+fatal.Phase);return true;}
            if(RebirthLogisticsAcceptedBodyJournal.TryTerminalNoEffectPublication(original,out var published)){proof=RebirthLogisticsOriginalTransport.Digest(fatal.Binding+":"+fatal.OwnerFileProof+":"+published);return true;}
            return false;
        }
        catch{return false;}
    }
    private static bool HasSettledPaidOutcome(RebirthLogisticsTransferJournal.Record value)
        {
            if(value.Phase<2||value.Phase>4||string.IsNullOrEmpty(value.OwnerDebitProof))return false;
            if(value.Outcome==1)return value.Legs.All(leg=>leg.Attempted==2&&leg.Published==2&&!leg.Restored)&&value.Npcs.All(npc=>npc.Attempted==2&&npc.Published==2);
            return value.Outcome==2&&value.Npcs.All(npc=>npc.Attempted==0&&npc.Published==0)&&value.Legs.All(leg=>leg.Kind==RemoteResourceSourceKind.NpcStorage?
                leg.Attempted==0&&leg.Published==0&&!leg.Restored:leg.Attempted<=1&&leg.Published<=1&&(leg.Attempted==0?!leg.Restored:leg.Restored));
        }
    private static void ClosePaidAfter(RebirthLogisticsTransferJournal.Record original,Record fatal)
    {
        // Native phase4/Held1 is the explicit saved final ACK authored by
        // OwnerReceipt.Advance after phase3 +serverPhase3, before terminal4.
        // It has no additional item meaning. Never accept arbitrary >=3.
        var world=GameManager.Instance?.World;string binding=fatal.Binding,paid=original.OwnerDebitProof;
        bool Current()
        {
            if(!ThreadManager.IsMainThread()||!ReferenceEquals(GameManager.Instance?.World,world)||world?.IsRemote()!=false||fatal.Mode!=0||(fatal.Phase!=3&&fatal.Phase!=4)||
                !RebirthLogisticsTransferJournal.TryRead(original.Owner,original.Operation,out var retained)||!HasSettledPaidOutcome(retained)||retained.Outcome!=original.Outcome||retained.OwnerDebitProof!=paid||
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)!=binding||!TryRead(retained,out var receipt)||!XNode.DeepEquals(Write(receipt),Write(fatal)))return false;
            var actor=world.GetEntity(fatal.OwnerEntity) as EntityPlayer;
            if(actor==null||actor.IsDead()||retained.Deaths==int.MaxValue||actor.Died!=retained.Deaths+1||RebirthCamperCharacterAccess.CurrentCreation(actor)!=retained.Creation||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=retained.Owner||
                !RebirthLogisticsOwnerReceipt.TrySaved(owner,retained.Operation,fatal.Phase,out var saved)||saved.id!=fatal.OwnerEntity||saved.deaths!=retained.Deaths+1)return false;
            var bag=RebirthGearPlayerDataInventory.ReadSlots(saved,true);var belt=RebirthGearPlayerDataInventory.ReadSlots(saved,false);
            return RebirthLogisticsDeathPartition.SurvivedUnchanged(retained,3,bag,belt)&&RebirthLogisticsDeathPartition.Same(bag,fatal.RemainingBag)&&
                RebirthLogisticsDeathPartition.Same(belt,fatal.RemainingBelt)&&fatal.OwnerFileProof==RebirthLogisticsOriginalTransport.Digest(binding+":"+saved.deaths+":"+fatal.Phase+":"+
                RebirthLogisticsDeathJournal.Hash(bag)+":"+RebirthLogisticsDeathJournal.Hash(belt));
        }
        if(original.Phase<2||original.Phase>3||!HasSettledPaidOutcome(original)||!Current())return;
        original.Phase++;RebirthLogisticsTransferJournal.TryPublish(original,Current);
    }

}
[HarmonyPatch(typeof(NetPackageRequestToSpawnEntity),nameof(NetPackageRequestToSpawnEntity.ProcessPackage))]
internal static class RebirthLogisticsDeathPacketScope
{
    private sealed class State{internal object Logistics,Placement;}
    private static bool Prefix(NetPackageRequestToSpawnEntity __instance,World _world,out State __state)
    {
        __state=new State();
        // B classifies its own authenticated original and persists its own
        // invocation before native effects. Unexpected outer failure must not
        // turn a positively classified refusal into spawn permission.
        bool logisticsPurpose=RebirthLogisticsNativeInvocationJournal.IsExecutingPrepared(__instance,_world);
        bool placementPurpose=RebirthPlacedOriginalDeathPartition.IsExecutingPreparedBody(__instance,_world);
        // Exact typed native invocation selects one independent original. An
        // unscoped native request still passes BOTH original classifiers.
        if(logisticsPurpose&&placementPurpose)return false; // Conflicting scopes never choose arbitrary authority.
        bool allowNative=true,allowLogistics=true;
        if(!logisticsPurpose)__state.Placement=RebirthPlacedOriginalDeathPartition.EnterRequest(__instance,_world,out allowNative);
        if(!placementPurpose)__state.Logistics=RebirthLogisticsDeathCustody.Enter(__instance,_world,out allowLogistics);
        return allowNative&&allowLogistics;
    }
    private static Exception Finalizer(Exception __exception,State __state)
    {
        RebirthLogisticsDeathCustody.Leave(__state?.Logistics);
        try{RebirthPlacedOriginalDeathPartition.LeaveRequest(__state?.Placement);}catch{/* Preserve native exception. */}
        return __exception;
    }
}
[HarmonyPatch(typeof(GameManager),nameof(GameManager.SpawnEntityServer))]
internal static class RebirthLogisticsDeathSpawnObservation
{
    private static void Postfix(Entity __result)
    {try{RebirthPlacedOriginalDeathPartition.ObserveAccepted(__result);}catch{/* Independent actual accepted placement observation. */}try{RebirthLogisticsDeathCustody.Observe(__result);}catch{/* Preserve an accepted native spawn; custody remains unknown. */}}
}

