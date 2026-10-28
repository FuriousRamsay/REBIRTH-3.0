using System;using System.IO;using System.Text;
// Actual native marker selector; pooled reader and BuffValue adapters only.
// Native packet/join provenance is outside this cohort.
class PooledBinaryReader:IDisposable
{
 BinaryReader reader;internal void SetBaseStream(Stream stream){reader=new BinaryReader(stream,Encoding.UTF8,true);}
 internal byte ReadByte()=>reader.ReadByte();internal ushort ReadUInt16()=>reader.ReadUInt16();
 internal string ReadString()=>reader.ReadString();internal float ReadSingle()=>reader.ReadSingle();
 public void Dispose(){reader?.Dispose();}
}
static class MemoryPools{internal static Pool poolBinaryReader=new Pool();internal class Pool{internal PooledBinaryReader AllocSync(bool value)=>new PooledBinaryReader();}}
static class EntityBuffs{internal const int Version=2;}
class BuffValue{internal void Read(PooledBinaryReader reader,int version){throw new Exception("Buff decoding adapter intentionally unavailable");}}
static class Program
{
 // ACTUAL_HOLD_SELECTOR
 static int checks;
 static byte[] Image(params (string key,float value)[] fields)
 {
  using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
  {writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)fields.Length);foreach(var field in fields){writer.Write(field.key);writer.Write(field.value);}}
  return stream.ToArray();
 }
 static void Check(bool value){checks++;if(!value)throw new Exception("Native hold marker selector assertion "+checks);}
 static bool Read(byte[] bytes,out Guid id){try{return TryOperation(bytes,out id);}catch{id=Guid.Empty;return false;}}
 static void Main()
 {
  var id=Guid.NewGuid();string own="rbNpcTradeHold_"+id.ToString("N");
  Check(Read(Image((own,1)),out var found)&&found==id);
  Check(!Read(Image(),out _));Check(!Read(Image((own,0)),out _));
  Check(!Read(Image((own,1),(own,1)),out _));
  Check(!Read(Image((own,1),("rbNpcTradeHold_"+Guid.NewGuid().ToString("N"),1)),out _));
  Check(!Read(Image(("rbNpcTradeHold_bad",1)),out _));
  Check(!Read(Image(("rbNpcTradeHold_"+Guid.Empty.ToString("N"),1)),out _));
  Check(Read(Image(("otherPurpose",1),(own,1),("rbNpcTradeHold_"+Guid.NewGuid().ToString("N"),0)),out found)&&found==id);
  var bytes=Image((own,1));Array.Resize(ref bytes,bytes.Length+1);Check(!Read(bytes,out _));
  bytes=Image((own,1));Array.Resize(ref bytes,bytes.Length-1);Check(!Read(bytes,out _));
  bytes=Image((own,1));bytes[0]=1;Check(!Read(bytes,out _));
  Check(!Read(Image((own,float.NaN)),out _));
  Console.WriteLine(checks+" actual hold-selector assertions passed; native read/join lineage NOT certified");
 }
}
