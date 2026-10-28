// Actual parser; native binary-reader pool and ECD payload codec are doubles.
// Exercises prefix structure/strictness, not actual game ECD or region writing.
public static class Chunk{public static int CurrentSaveVersion=47;}
public static class RebirthStationSnapshotEvidence{public const int MaximumSnapshotBytes=8*1024*1024;}
public enum StreamModeRead{Persistency}
public sealed class SwitchStream:Stream
{
 internal Stream Source=Stream.Null;public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
 public override long Length=>Source.Length;public override long Position{get=>Source.Position;set=>Source.Position=value;}
 public override void Flush(){}public override int Read(byte[] b,int o,int n)=>Source.Read(b,o,n);public override long Seek(long n,SeekOrigin o)=>Source.Seek(n,o);
 public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();
}
public sealed class PooledReader:BinaryReader
{
 private readonly SwitchStream source;private PooledReader(SwitchStream s):base(s){source=s;}public PooledReader():this(new SwitchStream()){}
 public void SetBaseStream(Stream stream){source.Source=stream;}
}
public static class MemoryPools{public static class poolBinaryReader{public static PooledReader AllocSync(bool value)=>new PooledReader();}}
public sealed class EntityCreationData
{
 public int id,entityClass;public MemoryStream bagData,entityData;
 public void read(BinaryReader reader,StreamModeRead mode)
 {id=reader.ReadInt32();entityClass=reader.ReadInt32();bagData=Blob(reader);entityData=Blob(reader);}
 private static MemoryStream Blob(BinaryReader reader){int n=reader.ReadUInt16();var bytes=reader.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();return new MemoryStream(bytes);}
}
public static class Fixture
{
 static readonly byte[] Bag={1,2,3},Entity={4,5};
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static void Channel(BinaryWriter writer,int bytes){for(int i=0;i<64;i++){writer.Write((byte)1);writer.Write(new byte[bytes]);}}
 static byte[] Snapshot(int count=1,bool custom=false)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {
   writer.Write(new byte[]{116,116,99,0});writer.Write((uint)47);writer.Write(3);writer.Write(0);writer.Write(-2);writer.Write((long)123);
   for(int i=0;i<64;i++)writer.Write((byte)0);Channel(writer,1);writer.Write(new byte[256+256+32+256+1536+2]);
   writer.Write((ushort)(custom?1:0));if(custom){writer.Write("identity");writer.Write((long)0);writer.Write(false);writer.Write((ushort)19);writer.Write(new byte[19]);}
   writer.Write(new byte[768]);Channel(writer,1);Channel(writer,1);Channel(writer,2);Channel(writer,6);Channel(writer,2);writer.Write(false);writer.Write(count);
   for(int i=0;i<count;i++){writer.Write(17);writer.Write(21);writer.Write((ushort)Bag.Length);writer.Write(Bag);writer.Write((ushort)Entity.Length);writer.Write(Entity);}
   writer.Flush();return stream.ToArray();
  }
 }
 public static void Main()
 {
  var good=Snapshot();Check(RebirthLogisticsEntity47.Contains(good,3,-2,17,21,Bag,Entity),"exact native47 prefix");
  Check(RebirthLogisticsEntity47.Contains(Snapshot(custom:true),3,-2,17,21,Bag,Entity),"custom data span");
  Check(!RebirthLogisticsEntity47.Contains(good,3,-2,17,22,Bag,Entity),"class mismatch");
  Check(!RebirthLogisticsEntity47.Contains(good,3,-2,17,21,new byte[]{9},Entity),"bag mismatch");
  Check(!RebirthLogisticsEntity47.Contains(Snapshot(2),3,-2,17,21,Bag,Entity),"duplicate native entity");
  Check(!RebirthLogisticsEntity47.Contains(Snapshot(0),3,-2,17,21,Bag,Entity),"missing original entity");
  for(int i=0;i<good.Length;i++)Check(!RebirthLogisticsEntity47.Contains(good.Take(i).ToArray(),3,-2,17,21,Bag,Entity),"truncated prefix "+i);
  var flags=(byte[])good.Clone();flags[32]=2;Check(!RebirthLogisticsEntity47.Contains(flags,3,-2,17,21,Bag,Entity),"noncanonical bool");
  Chunk.CurrentSaveVersion=48;Check(!RebirthLogisticsEntity47.Contains(good,3,-2,17,21,Bag,Entity),"target version changed");
  Console.WriteLine("PASS actual bounded Entity47 parser fixture (native ECD/pool doubles)");
 }
}
