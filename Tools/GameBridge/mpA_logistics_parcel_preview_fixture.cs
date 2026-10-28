// PREPARED, NOT RUN. Actual production detached parser/rebuilder. The ECD and
// compressor below are disclosed framing doubles, NOT native codec validation.
public static class MpALogisticsParcelPreviewFixture
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static byte[] Entity(int id,int type,float y=0){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(id);w.Write(type);w.Write(y);return s.ToArray();}}
    static byte[] Marker(string key,byte data){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(key);w.Write(ulong.MaxValue);w.Write(false);w.Write((ushort)1);w.Write(data);return s.ToArray();}}
    static byte[] Baseline(byte[] marker,byte[] entity)
    {
        using(var s=new MemoryStream())using(var w=new BinaryWriter(s))
        {
            w.Write(new byte[]{116,116,99,0,47,0,0,0});w.Write(2);w.Write(0);w.Write(3);w.Write(new byte[8+64]);
            for(int i=0;i<64;i++){w.Write(true);w.Write((byte)0);}w.Write(new byte[256+256+32+256+1536+2]);
            w.Write((ushort)(marker==null?0:1));if(marker!=null)w.Write(marker);w.Write(new byte[768]);
            foreach(int size in new[]{1,1,2,6,2})for(int i=0;i<64;i++){w.Write(true);w.Write(new byte[size]);}
            w.Write(false);w.Write(entity==null?0:1);if(entity!=null)w.Write(entity);w.Write(new byte[]{7,8,9,10});return s.ToArray();
        }
    }
    public static void Main()
    {
        var operation=Guid.ParseExact("c4297e9671b6488bb4ca94c943a59448","N");
        var edit=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=11,EntityClass=12,AfterEntity=Entity(11,12)};
        edit.AfterMarker=Marker(edit.Key,2);var baseline=Baseline(null,null);
        var markerStage=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=11,EntityClass=12,MarkerOnly=true,AfterMarker=Marker(edit.Key,2)};
        Check(RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{markerStage},out var markerOnlyBytes,out _),"actual extra parcel marker-before-spawn intermediate");
        Check(!RebirthLogisticsParcelPreview.TryReadOriginal(markerOnlyBytes,2,3,operation,11,12,out _,out _),"marker-only stage cannot claim a physical original body");
        var entityStage=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=11,EntityClass=12,EntityOnly=true,AfterEntity=Entity(11,12)};
        Check(RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{entityStage},out var entityOnlyBytes,out _),"actual recovery root spawn-before-marker intermediate");
        Check(!RebirthLogisticsParcelPreview.TryReadOriginal(entityOnlyBytes,2,3,operation,11,12,out _,out _),"ECD-only stage cannot claim native identity marker proof");
        entityStage.MarkerOnly=true;
        Check(!RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{entityStage},out _,out _),"contradictory intermediate kinds refuse");
        Check(RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{edit},out var added,out var length),"addition to exact native-layout baseline");
        Check(added.Skip(added.Length-4).SequenceEqual(new byte[]{7,8,9,10}),"opaque unrelated suffix preserved");
        Check(RebirthLogisticsParcelPreview.TryReadOriginal(added,2,3,operation,11,12,out var originalFrame,out var originalMarker)&&originalFrame.SequenceEqual(edit.AfterEntity)&&originalMarker.SequenceEqual(edit.AfterMarker),"exact detached original spans readable");
        originalMarker[originalMarker.Length-1]=99;
        Check(RebirthLogisticsParcelPreview.TryReadOriginal(added,2,3,operation,11,12,out _,out var unchanged)&&unchanged.Last()==2,"reader does not expose mutable baseline spans");
        Check(!RebirthLogisticsParcelPreview.TryBuild(baseline,3,2,new[]{edit},out _,out _),"wrong original chunk rejected");
        Check(!RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{edit,edit},out _,out _),"duplicate original id/key rejected");
        Check(!RebirthLogisticsParcelPreview.TryBuild(added,2,3,new[]{edit},out _,out _),"addition cannot replace an existing body");
        edit.BeforeEntity=Entity(11,12);edit.BeforeMarker=Marker(edit.Key,2);edit.AfterMarker=Marker(edit.Key,3);
        Check(RebirthLogisticsParcelPreview.TryBuild(added,2,3,new[]{edit},out var replaced,out _),"exact original full-span replacement");
        edit.BeforeMarker=Marker(edit.Key,1);
        Check(!RebirthLogisticsParcelPreview.TryBuild(added,2,3,new[]{edit},out _,out _),"changed baseline original marker rejected");
        edit.BeforeMarker=Marker(edit.Key,2);edit.AfterEntity=Entity(11,13);
        Check(!RebirthLogisticsParcelPreview.TryBuild(added,2,3,new[]{edit},out _,out _),"wrong native class rejected");
        edit.AfterEntity=Entity(11,12);
        var parcel=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=21,EntityClass=12,EmptyEntity=Entity(21,12),AfterEntity=Entity(21,12)};
        parcel.EmptyMarker=Marker(parcel.Key,0);parcel.AfterMarker=Marker(parcel.Key,1);
        Check(RebirthLogisticsParcelPreview.TryBuildStage(added,2,3,edit,new[]{parcel},1,out var emptyStage,out _)&&
            RebirthLogisticsParcelPreview.TryReadOriginal(emptyStage,2,3,operation,21,12,out _,out var emptyMarker)&&emptyMarker.Last()==0,"native spawn stage uses empty original marker");
        Check(RebirthLogisticsParcelPreview.TryBuildStage(added,2,3,edit,new[]{parcel},2,out var parcelStage,out _)&&
            RebirthLogisticsParcelPreview.TryReadOriginal(parcelStage,2,3,operation,11,12,out _,out var rootBefore)&&rootBefore.Last()==2,"root unchanged until all parcels filled");
        Check(RebirthLogisticsParcelPreview.TryBuildStage(added,2,3,edit,new[]{parcel},3,out var finalStage,out _)&&
            RebirthLogisticsParcelPreview.TryReadOriginal(finalStage,2,3,operation,11,12,out _,out var rootAfter)&&rootAfter.Last()==3,"root final actual stage");
        Check(!RebirthLogisticsParcelPreview.TryBuildStage(added,2,3,edit,new[]{parcel},4,out _,out _),"out-of-sequence stage refused");
        var markerOnly=Baseline(parcel.EmptyMarker,null);
        parcel.BeforeMarker=parcel.EmptyMarker;
        Check(RebirthLogisticsParcelPreview.TryBuild(markerOnly,2,3,new[]{parcel},out var resumedMarker,out _)&&
            RebirthLogisticsParcelPreview.TryReadOriginal(resumedMarker,2,3,operation,21,12,out _,out _),"exact original marker-only preview can add body without duplicating marker");
        parcel.BeforeMarker=Marker(parcel.Key,99);
        Check(!RebirthLogisticsParcelPreview.TryBuild(markerOnly,2,3,new[]{parcel},out _,out _),"foreign marker-only state refused");
        parcel.BeforeMarker=null;
        var upper=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=31,EntityClass=12,AfterEntity=Entity(31,12,64)};upper.AfterMarker=Marker(upper.Key,1);
        var lower=new RebirthLogisticsParcelPreview.Edit{Operation=operation,EntityId=32,EntityClass=12,AfterEntity=Entity(32,12,0)};lower.AfterMarker=Marker(lower.Key,1);
        Check(RebirthLogisticsParcelPreview.TryBuild(baseline,2,3,new[]{upper},out var upperOnly,out _)&&
            RebirthLogisticsParcelPreview.TryBuild(upperOnly,2,3,new[]{lower},out var mixedBuckets,out _)&&
            RebirthLogisticsParcelPreview.TryReadOriginal(mixedBuckets,2,3,operation,31,12,out _,out _),"new lower bucket inserted before existing higher bucket, preserving native order");
        Check(!RebirthLogisticsParcelPreview.TryCompressedLength(new byte[RebirthLogisticsParcelPreview.MaximumRawBytes+1],out _),"raw observation budget before allocation");
        Check(!RebirthLogisticsParcelPreview.TryCompressedLength(new byte[RebirthLogisticsParcelPreview.MaximumCompressedBytes+1],out _),"sector bound using disclosed identity compressor double");
        Console.WriteLine("PASS detached parcel span/framing rules; native compression NOT VALIDATED");
    }
}
public static class Chunk{public const int CurrentSaveVersion=47;}
public enum StreamModeRead{Persistency}
public sealed class PooledBinaryReader:BinaryReader
{public PooledBinaryReader():base(new MemoryStream()){}public void SetBaseStream(Stream stream){input=stream;}private Stream input;public override int ReadInt32(){return new BinaryReader(input,Encoding.UTF8,true).ReadInt32();}public override float ReadSingle(){return new BinaryReader(input,Encoding.UTF8,true).ReadSingle();}public override byte ReadByte(){return (byte)input.ReadByte();}public override ushort ReadUInt16(){return new BinaryReader(input,Encoding.UTF8,true).ReadUInt16();}public override string ReadString(){return new BinaryReader(input,Encoding.UTF8,true).ReadString();}}
public static class MemoryPools{public static readonly ReaderPool poolBinaryReader=new ReaderPool();public sealed class ReaderPool{public PooledBinaryReader AllocSync(bool reset){return new PooledBinaryReader();}}}
public struct FixturePosition{public float y;}
public sealed class EntityCreationData{public int id,entityClass;public FixturePosition pos;public MemoryStream entityData=new MemoryStream(),bagData;public void read(PooledBinaryReader reader,StreamModeRead mode){id=reader.ReadInt32();entityClass=reader.ReadInt32();pos.y=reader.ReadSingle();}}
// Native backpack pointer serialization is a separate cohort. Empty native
// class registration and throwing Encode cannot authorize a pointer rewrite.
public sealed class EntityBackpack{}
// The ECD adapter is exactly int id + int class + float y (12 bytes).
// This validates that toy frame, not the game's native entity body grammar.
internal static partial class RebirthLogisticsNativeBodyPreflight{internal static void Validate(byte[] bytes,bool requireId){if(bytes==null||bytes.Length!=12||requireId&&BitConverter.ToInt32(bytes,0)<0||float.IsNaN(BitConverter.ToSingle(bytes,8))||float.IsInfinity(BitConverter.ToSingle(bytes,8)))throw new InvalidDataException();}}
public sealed class EntityClass{public Type classname;public static readonly Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>();}
internal static class RebirthLogisticsNativeInvocationJournal{internal static byte[] Encode(EntityCreationData data)=>throw new InvalidOperationException("Native pointer serializer excluded from detached preview fixture");}
namespace Noemax.GZip
{
    public sealed class DeflateOutputStream:Stream
    {
        private readonly Stream output;public DeflateOutputStream(Stream output,int level,bool leaveOpen){this.output=output;}
        public void Restart(){}public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>output.Length;public override long Position{get=>output.Position;set=>throw new NotSupportedException();}
        public override void Flush()=>output.Flush();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>output.Write(b,o,c);
    }
}
