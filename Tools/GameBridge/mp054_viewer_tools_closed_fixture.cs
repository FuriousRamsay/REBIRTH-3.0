using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
// AUTHORED_NOT_RUN. Actual compact viewer-tool receipt metadata qualifiers.
// Canonical stage/native source bytes are fixture-only I/O, not native proofs.
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner?.Length==64&&creation=="creation";}
static class RebirthSurvivorNetworkCodec{internal static string ReadBoundedString(BinaryReader reader,int maximum){string text=reader.ReadString();if(text.Length>maximum)throw new InvalidDataException();return text;}}
// ACTUAL_CLOSED_CODEC
static class RebirthProductiveEarnedRemoteClient
{
 static bool Safe(string path)=>!string.IsNullOrEmpty(path);static byte[] Read(string path,int max){if(new FileInfo(path).Length>max)throw new InvalidDataException();return File.ReadAllBytes(path);}
 static string HashOriginalFile(string path){if(!Safe(path)||File.Exists(path+".tmp"))throw new InvalidDataException();return Hash(Read(path,6*1024*1024));}
 internal static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 // ACTUAL_STAGE_HELPERS
 // ACTUAL_VIEWER_METADATA
 internal static byte[] Stage(RebirthProductiveEarnedRemoteClosedRecord.Description value,string name)=>NativeCloseStage(value,name);
 internal static bool Closed(string path,RebirthProductiveEarnedRemoteClosedRecord.Description value)=>ReadViewerToolClosed(path,value.World,value.Owner,value.Creation,out _);
 internal static bool Scope(string path,RebirthProductiveEarnedRemoteClosedRecord.Description value)=>ViewerToolMetadataCurrent(path,value);
}
static class Program
{
 static int count;static void Check(bool value){count++;if(!value)throw new Exception("Actual viewer receipt assertion "+count);}
 static void Main(){string folder=Path.Combine(Path.GetTempPath(),"rebirth-viewer-receipt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var value=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=Guid.NewGuid(),Birth=Guid.NewGuid(),Nonce=Guid.NewGuid(),World="world",Owner=new string('a',64),Creation="creation",Deaths=12,Kind=1};string path=Path.Combine(folder,value.Operation.ToString("N")+".rew");try{
 var original=new byte[]{1,2,3};var scope=new byte[]{4,5};var source=new byte[]{6,7};var plan=new byte[]{8,9};File.WriteAllBytes(path,original);File.WriteAllBytes(path+".scope",scope);File.WriteAllBytes(path+".nativeViewerSource",source);File.WriteAllBytes(path+".nativeTools",plan);
 value.OriginalDigest=RebirthProductiveEarnedRemoteClient.Hash(original);value.HeaderDigest=RebirthProductiveEarnedRemoteClient.Hash(scope);value.HoldDigest=RebirthProductiveEarnedRemoteClient.Hash(source);value.TerminalDigest=RebirthProductiveEarnedRemoteClient.Hash(plan);
 Check(!RebirthProductiveEarnedRemoteClient.Scope(path,value));foreach(string name in new[]{"nativeToolsOwnerUpload","nativeToolsSyncAttempt"})File.WriteAllBytes(path+"."+name,RebirthProductiveEarnedRemoteClient.Stage(value,name));Check(RebirthProductiveEarnedRemoteClient.Scope(path,value));Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));
 File.WriteAllBytes(path+".viewerToolsClosed",RebirthProductiveEarnedRemoteClosedRecord.Encode(value));Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.WriteAllBytes(path+".nativeViewerMetadataClosed",RebirthProductiveEarnedRemoteClient.Stage(value,"nativeViewerMetadataClosed"));Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.WriteAllBytes(path+".nativeViewerReleased",RebirthProductiveEarnedRemoteClient.Stage(value,"nativeViewerReleased"));Check(RebirthProductiveEarnedRemoteClient.Closed(path,value));
 File.WriteAllBytes(path+".nativeTools",new byte[]{0});Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.WriteAllBytes(path+".nativeTools",plan);File.WriteAllBytes(path+".nativeViewerSource",new byte[]{0});Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.WriteAllBytes(path+".nativeViewerSource",source);
 File.WriteAllBytes(path+".nativeToolsSyncAttempt",RebirthProductiveEarnedRemoteClient.Stage(value,"accepted"));Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.WriteAllBytes(path+".nativeToolsSyncAttempt",RebirthProductiveEarnedRemoteClient.Stage(value,"nativeToolsSyncAttempt"));File.WriteAllBytes(path+".nativeTools.tmp",new byte[]{0});Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));File.Delete(path+".nativeTools.tmp");
 value.Owner=new string('b',64);Check(!RebirthProductiveEarnedRemoteClient.Closed(path,value));value.Owner=new string('a',64);Check(RebirthProductiveEarnedRemoteClient.Closed(path,value));Console.WriteLine(count+" actual viewer tool receipt assertions passed");
 }finally{if(!Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception();Directory.Delete(folder,true);}}
}
