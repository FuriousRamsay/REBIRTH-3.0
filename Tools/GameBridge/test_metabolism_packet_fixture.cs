using System;using System.IO;using System.Text;using System.Reflection;
namespace UnityEngine { public static class Mathf { public static float Clamp01(float v){return Math.Max(0,Math.Min(1,v));} } }
[AttributeUsage(AttributeTargets.Class)]public class PreserveAttribute:Attribute{}
public enum NetPackageDirection {ToClient}
public class World{}public class GameManager{}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
public abstract class NetPackage { public virtual NetPackageDirection PackageDirection{get{return NetPackageDirection.ToClient;}}public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){} }
public static class RebirthMetabolismClientState{public static RebirthMetabolismSnapshot Last;public static void Receive(RebirthMetabolismSnapshot s){Last=s;}}
public static class RebirthSurvivorNetworkProtocol{public const int MaxIdLength=128;}
// PRODUCTION_MODEL
// PRODUCTION_CODEC
// PRODUCTION_PACKETS
class Checks {
static void A(bool b,string m){if(!b)throw new Exception(m);}
static byte[] Write(NetPackage p){using(var ms=new MemoryStream()){var w=new PooledBinaryWriter(ms);p.write(w);w.Flush();return ms.ToArray();}}
static void Read(NetPackage p,byte[] data){using(var ms=new MemoryStream(data)){p.read(new PooledBinaryReader(ms));A(ms.Position==ms.Length,"exact consumed length");}}
static void Main(){object boxed=new RebirthMetabolismSnapshot();int i=1;foreach(var f in typeof(RebirthMetabolismSnapshot).GetFields()){if(f.FieldType==typeof(float))f.SetValue(boxed,i++*1.25f);else if(f.FieldType==typeof(int))f.SetValue(boxed,i++);else if(f.FieldType==typeof(long))f.SetValue(boxed,(long)i++);else if(f.FieldType==typeof(bool))f.SetValue(boxed,true);else if(f.FieldType==typeof(string))f.SetValue(boxed,"sample_"+f.Name);}
var s=(RebirthMetabolismSnapshot)boxed;s.CreationId=Guid.NewGuid().ToString("N");
var scoped=new NetPackageRebirthMetabolismScopedState().Setup(s);var data=Write(scoped);A(data.Length<=scoped.GetLength(),"length bound");var restored=new NetPackageRebirthMetabolismScopedState();Read(restored,data);restored.ProcessPackage(null,null);var got=RebirthMetabolismClientState.Last;
foreach(var f in typeof(RebirthMetabolismSnapshot).GetFields())A(object.Equals(f.GetValue(s),f.GetValue(got)),"roundtrip "+f.Name);
var legacy=new NetPackageRebirthMetabolismState().Setup(s);var old=Write(legacy);A(data.Length-old.Length==33,"only creation extension");for(i=0;i<old.Length;i++)A(data[i]==old[i],"legacy prefix");Read(legacy,old);legacy.ProcessPackage(null,null);A(RebirthMetabolismClientState.Last.CreationId==null,"pooled legacy clears scope");
bool failed=false;try{var shortData=new byte[data.Length-1];Array.Copy(data,shortData,shortData.Length);Read(restored,shortData);}catch(EndOfStreamException){failed=true;}A(failed,"truncated scope fails");
using(var ms=new MemoryStream()){ms.Write(old,0,old.Length);var w=new BinaryWriter(ms);w.Write(new string('x',129));w.Flush();failed=false;try{Read(restored,ms.ToArray());}catch(InvalidDataException){failed=true;}A(failed,"oversize scope fails");}
Console.WriteLine("PASS: actual snapshot/packet/string codec; every-field roundtrip, length estimate, legacy prefix/reset, truncation and oversized scope. Native packet framing/transport doubled.");}
}
