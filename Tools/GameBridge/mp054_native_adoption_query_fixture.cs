using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual metadata-only owner candidate/receipt helpers.
// Native RowProof decode/admission, output codec and budget are explicit
// adapters. No station, original row, native fence, setter or source is supplied.
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class World{} class EntityPlayerLocal{internal int Died;internal World world=new World();}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner?.Length==64&&creation=="creation";}
static class RebirthSurvivorNetworkCodec{internal static string ReadBoundedString(BinaryReader reader,int maximum){string text=reader.ReadString();if(text.Length>maximum)throw new InvalidDataException();return text;}}
static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumArtifactBytes=6*1024*1024;internal class Frame{internal bool HasNativeBodies=true;internal string World="world";internal Guid Operation=Guid.NewGuid(),Birth=Guid.NewGuid();}
 internal static Frame Value=new Frame();internal static Frame Decode(byte[] bytes)=>Value;
 internal static bool TryDecodeNative(Frame frame,out object a,out object b,out object c,out object d){a=b=c=d=null;return true;}
}
class RebirthProductiveEarnedObservationBudget
{
 internal class Decoder:IDisposable{internal bool Admit(RebirthProductiveEarnedRowProof.Frame frame,int size)=>true;public void Dispose(){Active=false;}}
 class Memory:IDisposable{public void Dispose(){}}
 static bool Active;internal static Decoder TryEnterDecoder(){if(Active)return null;Active=true;return new Decoder();}internal static bool TryRetainDetached(long size,out IDisposable memory){memory=new Memory();return true;}
}
static class RebirthProductiveEarnedNativeOutputPlan{internal static bool Ready=true;internal static bool TryRead(byte[] bytes,RebirthProductiveEarnedRowProof.Frame frame,Guid nonce,string digest,out object plan){plan=null;return Ready;}}
// ACTUAL_CLOSED_CODEC
static class RebirthProductiveEarnedRemoteClient
{
 class ClosingNative{internal EntityPlayerLocal Actor;internal World World;internal string Root,Path;internal RebirthProductiveEarnedRemoteClosedRecord.Description Record;}
 static Dictionary<Guid,ClosingNative> nativeClosures=new Dictionary<Guid,ClosingNative>();static bool coldObserved;
 static bool Safe(string path)=>!string.IsNullOrEmpty(path);static byte[] Read(string path,int maximum){if(new FileInfo(path).Length>maximum)throw new InvalidDataException();return File.ReadAllBytes(path);}
 static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 // ACTUAL_QUERY_HELPERS
 internal static bool Recover(string path,EntityPlayerLocal actor,string owner)=>RecoverNativeAdoptionQuery(path,actor,"world",owner,"creation",System.IO.Path.GetDirectoryName(path));
 internal static RebirthProductiveEarnedRemoteClosedRecord.Description Candidate=>nativeClosures.Values.Single().Record;
 internal static byte[] Stage(string name)=>NativeCloseStage(Candidate,name);internal static bool Adopted(string path)=>SavedNativeAdoption(path,Candidate);internal static void Clear()=>nativeClosures.Clear();
 internal static bool Nested(string path,EntityPlayerLocal actor,string owner){using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())return RecoverNativeAdoptionQuery(path,actor,"world",owner,"creation",System.IO.Path.GetDirectoryName(path),decoder,RebirthProductiveEarnedRowProof.Value,File.ReadAllBytes(path));}
}
static class Program
{
 static int count;static string folder,path,owner=new string('a',64);static EntityPlayerLocal actor=new EntityPlayerLocal();static Guid nonce=Guid.NewGuid();
 static void Check(bool value){count++;if(!value)throw new Exception("Actual metadata adoption query "+count);}
 static byte[] Bytes(Action<BinaryWriter> write){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true))){write(writer);writer.Flush();return stream.ToArray();}}
 static string Digest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 static void Seed(){RebirthProductiveEarnedRemoteClient.Clear();File.WriteAllBytes(path,new byte[]{1,2,3});string digest=Digest(File.ReadAllBytes(path));File.WriteAllBytes(path+".scope",Bytes(w=>{w.Write((byte)1);w.Write(owner);w.Write("creation");w.Write(0);w.Write("world");w.Write(1);w.Write(2);w.Write(3);w.Write(digest);}));File.WriteAllBytes(path+".accepted",Bytes(w=>{w.Write((byte)1);w.Write(RebirthProductiveEarnedRowProof.Value.Operation.ToByteArray());w.Write(nonce.ToByteArray());w.Write(digest);w.Write("accepted");}));}
 static bool Recover()=>RebirthProductiveEarnedRemoteClient.Recover(path,actor,owner);
 static void Main(){folder=Path.Combine(Path.GetTempPath(),"rebirth-metadata-query-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);path=Path.Combine(folder,RebirthProductiveEarnedRowProof.Value.Operation.ToString("N")+".rew");try{
 Seed();Check(Recover());Check(!File.Exists(path+".closeRequested")&&!File.Exists(path+".closed")&&!File.Exists(path+".nativeOutputSaved")&&!File.Exists(path+".nativeStationAdopted"));
 Check(RebirthProductiveEarnedRemoteClient.Candidate.Nonce==nonce&&RebirthProductiveEarnedRemoteClient.Candidate.Position.z==3);Check(!RebirthProductiveEarnedRemoteClient.Adopted(path));
 File.WriteAllBytes(path+".nativeStationAdopted",RebirthProductiveEarnedRemoteClient.Stage("nativeStationAdopted"));Check(RebirthProductiveEarnedRemoteClient.Adopted(path));File.WriteAllBytes(path+".nativeStationAdopted",RebirthProductiveEarnedRemoteClient.Stage("accepted"));Check(!RebirthProductiveEarnedRemoteClient.Adopted(path));Check(!Recover());File.Delete(path+".nativeStationAdopted");
 Seed();Check(RebirthProductiveEarnedRemoteClient.Nested(path,actor,owner));Seed();actor.Died=1;Check(!Recover());actor.Died=0;Seed();Check(!RebirthProductiveEarnedRemoteClient.Recover(path,actor,new string('b',64)));
 Seed();File.WriteAllBytes(path+".nativeOutput",new byte[]{9});RebirthProductiveEarnedNativeOutputPlan.Ready=false;Check(!Recover());RebirthProductiveEarnedNativeOutputPlan.Ready=true;Check(Recover());
 File.WriteAllBytes(path+".published",new byte[]{0});Check(!Recover());Console.WriteLine(count+" actual metadata-only adoption assertions passed");
 }finally{if(!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture cleanup");Directory.Delete(folder,true);}}
}
