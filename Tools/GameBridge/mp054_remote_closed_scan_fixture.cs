using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
// Actual Early metadata codec/scan plus closed-original validator. Temporary
// receipt files are real fsync-safe fixture data, never game or native proof.
// Native world/identity/manifest comparison-digest implementations are adapters.
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
class World{internal bool IsRemote()=>false;}class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner?.Length==64&&creation=="creation";}
static class RebirthSurvivorNetworkCodec{internal static string ReadBoundedString(BinaryReader reader,int maximum){string text=reader.ReadString();if(text.Length>maximum)throw new InvalidDataException();return text;}}
static class RebirthProductiveEarnedNativeBody{internal const int MaximumBodyBytes=1024*1024;}
static class RebirthProductiveEarnedArtifact{internal static string WorldIdentity(World world)=>"world";}
// ACTUAL_CLOSED_CODEC
static class RebirthProductiveEarnedRemoteHost
{
 private static int position;
 // Explicit adapter: inherited-child custody has its own actual native
 // ancestry cohort; this scanner cohort has no inherited child fixtures.
 private static bool TryNativeInheritedCustody(Early item)=>false;
 // ACTUAL_EARLY_MEMBERS
 // ACTUAL_CLOSED_EARLY
 private static bool Safe(string path)=>!string.IsNullOrEmpty(path)&&!File.Exists(path+".forbidden");
 private static byte[] Read(string path)=>ReadNativeOutputMetadata(path,8192);
 private static byte[] ReadNativeOutputMetadata(string path,int maximum){if(!Safe(path)||new FileInfo(path).Length>maximum)throw new InvalidDataException();return File.ReadAllBytes(path);}
 private static string Digest(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 internal static int Active=>coldEarly.Count;internal static bool Unknown=>earlyUnknown;
 internal static void Scan(){earlyWorld=null;ObserveEarly();}
 internal static string Make(Guid operation,bool close,bool release)
 {
  byte[] manifest;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)1);writer.Write(Guid.NewGuid().ToByteArray());writer.Write(13);writer.Write(40);writer.Write(new byte[32]);writer.Write(Enumerable.Repeat((byte)1,32).ToArray());manifest=stream.ToArray();}
  var hold=new Early{Operation=operation,Nonce=Guid.NewGuid(),World="world",Owner=new string('a',64),Creation="creation",Position=new Vector3i(++position,2,3),Manifest=manifest};Manifest(manifest,out hold.Birth,out _,out _,out _,out var original);
  string folder=Path.Combine(GameIO.Root,"RebirthProductiveEarned","remote",hold.Owner);Directory.CreateDirectory(folder);hold.Path=Path.Combine(folder,operation.ToString("N")+".hold");hold.Bytes=EarlyBytes(hold);File.WriteAllBytes(hold.Path,hold.Bytes);
  string header=hold.Path.Substring(0,hold.Path.Length-5)+".header";File.WriteAllBytes(header,new byte[]{1,2,3});File.WriteAllBytes(header+".nativeOutputSaved",new byte[]{4,5,6});
  if(close){var item=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=operation,Birth=hold.Birth,Nonce=hold.Nonce,World=hold.World,Owner=hold.Owner,Creation=hold.Creation,Deaths=0,Position=hold.Position,Kind=1,OriginalDigest=original,HeaderDigest=Digest(File.ReadAllBytes(header)),HoldDigest=Digest(hold.Bytes),TerminalDigest=Digest(File.ReadAllBytes(header+".nativeOutputSaved"))};byte[] bytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(item);File.WriteAllBytes(header+".closed",bytes);if(release)File.WriteAllBytes(header+".nativeReleased",Encoding.ASCII.GetBytes(Digest(bytes)));}return header;
 }
}
static class Program
{
 static int count;static void Check(bool value){count++;if(!value)throw new Exception("Actual closed hold scan assertion "+count);}
 static void Main(){string temp=Path.Combine(Path.GetTempPath(),"rebirth-closed-scan-"+Guid.NewGuid().ToString("N"));GameIO.Root=temp;Directory.CreateDirectory(temp);try{
  string first=RebirthProductiveEarnedRemoteHost.Make(Guid.NewGuid(),true,true);File.WriteAllBytes(first+".nativeOutput",new byte[20000]);RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==0);
  for(int i=0;i<6;i++)RebirthProductiveEarnedRemoteHost.Make(Guid.NewGuid(),true,true);RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==0);
  string pending=RebirthProductiveEarnedRemoteHost.Make(Guid.NewGuid(),true,false);RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==1);
  File.WriteAllBytes(pending+".nativeReleased",Encoding.ASCII.GetBytes(new string('a',64)));RebirthProductiveEarnedRemoteHost.Scan();Check(RebirthProductiveEarnedRemoteHost.Unknown);
  File.Delete(pending+".nativeReleased");File.WriteAllBytes(first+".nativeOutputSaved",new byte[]{7});RebirthProductiveEarnedRemoteHost.Scan();Check(RebirthProductiveEarnedRemoteHost.Unknown);File.WriteAllBytes(first+".nativeOutputSaved",new byte[]{4,5,6});
  File.WriteAllBytes(first+".nativeReleased.tmp",new byte[]{1});RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==2);File.Delete(first+".nativeReleased.tmp");
  File.WriteAllBytes(first+".nativeOutput",new byte[200000]);RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==1);
  File.WriteAllBytes(first+".tmp.unrelated",new byte[12000]);RebirthProductiveEarnedRemoteHost.Scan();Check(!RebirthProductiveEarnedRemoteHost.Unknown&&RebirthProductiveEarnedRemoteHost.Active==1);
  Console.WriteLine(count+" actual closed hold scan assertions passed");
 }finally{if(!Path.GetFullPath(temp).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(temp).StartsWith("rebirth-closed-scan-"))throw new Exception();Directory.Delete(temp,true);}}
}
