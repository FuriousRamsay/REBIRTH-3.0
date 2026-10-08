using System;using System.IO;using System.Linq;
public static class EntityBuffs {public const int Version=2;}
public class BuffValue {public void Read(Reader r,int version){r.ReadInt32();}}
public class Reader:IDisposable {BinaryReader r;public void SetBaseStream(Stream s){r=new BinaryReader(s,System.Text.Encoding.UTF8,true);}public byte ReadByte()=>r.ReadByte();public ushort ReadUInt16()=>r.ReadUInt16();public int ReadInt32()=>r.ReadInt32();public string ReadString()=>r.ReadString();public float ReadSingle()=>r.ReadSingle();public void Dispose()=>r?.Dispose();}
public class Pool {public Reader AllocSync(bool b)=>new Reader();}
public static class MemoryPools {public static Pool poolBinaryReader=new Pool();}
class Program {
 static int checks;static string tx="11111111111111111111111111111111",key="rbGear_"+tx;
 static byte[] Blob(params (string,float)[] vars){using var s=new MemoryStream();using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true)){w.Write((byte)EntityBuffs.Version);w.Write((ushort)1);w.Write(123);w.Write((ushort)vars.Length);foreach(var v in vars){w.Write(v.Item1);w.Write(v.Item2);}}return s.ToArray();}
 static bool Has(byte[] b,RebirthGearOwnerReceipt stage=RebirthGearOwnerReceipt.Applied,string id=null)=>RebirthGearReceiptReader.Contains(b,id??tx,stage);
 static void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
 static void Main(){
 Check(Has(Blob((key,1))),"applied absent");
 Check(Has(Blob((key,2)),RebirthGearOwnerReceipt.Applying),"applying absent");
 Check(Has(Blob((key,-1)),RebirthGearOwnerReceipt.Rejected),"rejected absent");
 Check(!Has(Blob((key,2)))&&!Has(Blob((key,-1))),"wrong phase accepted");
 Check(!Has(Blob()),"missing marker accepted");
 Check(!Has(Blob((key,1),(key,1)))&&!Has(Blob((key,1),(key,2))),"duplicate accepted");
 Check(!Has(Blob(("rbMusic_"+tx,1))),"foreign prefix accepted");
 Check(!Has(Blob((key,1)),id:"22222222222222222222222222222222"),"foreign transaction accepted");
 Check(!Has(Blob((key,float.NaN)))&&!Has(Blob((key,float.PositiveInfinity)))&&!Has(Blob((key,0))),"invalid value accepted");
 var valid=Blob(("unrelated",7),(key,1));Check(Has(valid),"unrelated cvar rejected");
 Check(!Has(valid.Concat(new byte[]{0}).ToArray()),"trailing bytes accepted");
 Check(!Has(valid.Take(valid.Length-1).ToArray()),"truncated blob accepted");
 valid[0]=1;Check(!Has(valid),"wrong native version accepted");
 Check(!Has(new byte[1024*1024+1])&&!Has(null),"unbounded blob accepted");
 Check(!Has(Blob((key,1)),(RebirthGearOwnerReceipt)3),"unknown stage accepted");
 Check(!Has(Blob((key,1)),id:Guid.Empty.ToString())&&!Has(Blob((key,1)),id:"bad"),"invalid transaction accepted");
 Console.WriteLine("PASS "+checks+" actual gear receipt reader checks; native reader pool and BuffValue format are doubles. No owner file or native save verification.");
 }
}