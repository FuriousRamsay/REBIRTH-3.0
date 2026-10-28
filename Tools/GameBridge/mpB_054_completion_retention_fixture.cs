using System;
using System.IO;
using System.Linq;
// AUTHORED_NOT_RUN. Complete actual retention partial is injected.
// World/native identity, loaded-marker currency and marker read are explicit doubles.
// ACTUAL_COMPLETION_RETENTION_SOURCE
internal sealed class World {internal bool IsRemote()=>false;}
internal class TileEntityWorkstation {internal Guid Custody=Guid.NewGuid();}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal static class GameIO {internal static string Save;internal static string GetSaveGameDir()=>Save;}
internal static class RebirthProductiveEarnedRowProof
{internal static bool MatchesMarker(byte[] marker,byte[] image)=>marker.SequenceEqual(new byte[]{9});}
internal static class RebirthProductiveEarnedCoordinator
{
 internal static bool Present=true;internal static byte[] Marker=new byte[]{9};
 internal static bool TryNativeMarker(TileEntityWorkstation station,out byte[] bytes){bytes=Marker;return Present;}
}
internal static partial class RebirthProductiveEarnedArtifact
{
 internal static string Root;internal static TileEntityWorkstation CurrentStation;
 private static string WorldIdentity(World world)=>"world";
 private static bool NativeCurrent(World world,TileEntityWorkstation station,Guid birth)=>ReferenceEquals(CurrentStation,station)&&station.Custody==birth;
 private static bool SafePath(string path)=>Path.GetFullPath(path).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 private static bool FileMatches(string path,byte[] image)=>SafePath(path)&&!File.Exists(path+".tmp")&&File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(image);
 internal sealed partial class Prepared
 {
  private readonly World world;private readonly TileEntityWorkstation station;private readonly string save,path,worldIdentity;private readonly byte[] image;
  internal Guid CustodyBirth=>station.Custody;
  internal Prepared(string file)
  {world=GameManager.Instance.World;station=new TileEntityWorkstation();CurrentStation=station;save=GameIO.Save;worldIdentity="world";path=file;image=Enumerable.Repeat((byte)3,128).ToArray();File.WriteAllBytes(path,image);}
  internal bool Current()=>ReferenceEquals(world,GameManager.Instance.World)&&RebirthProductiveEarnedCoordinator.Present&&FileMatches(path,image);
 }
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthProductiveEarnedArtifact.Prepared Setup(string root,string name)
 {string folder=Path.Combine(root,name);Directory.CreateDirectory(folder);RebirthProductiveEarnedCoordinator.Present=true;RebirthProductiveEarnedCoordinator.Marker=new byte[]{9};return new RebirthProductiveEarnedArtifact.Prepared(Path.Combine(folder,"op.rew"));}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-completion-retention-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;GameIO.Save=root;
  try
  {
   var p=Setup(root,"normal");var token=p.CaptureCompletionRetention(new byte[]{9});Need(token!=null,"current exact marker did not capture retention");
   RebirthProductiveEarnedCoordinator.Present=false;
   Need(!p.Current()&&token.TryRetain()&&!File.Exists(Path.Combine(root,"normal","op.rew"))&&File.Exists(Path.Combine(root,"normal","retained","op.rew")),"old loaded marker prevented exact post-clear retention");
   Need(!token.TryRetain(),"retention token reused");
   p=Setup(root,"marker-returned");token=p.CaptureCompletionRetention(new byte[]{9});Need(!token.TryRetain()&&File.Exists(Path.Combine(root,"marker-returned","op.rew")),"present marker original moved");
   RebirthProductiveEarnedCoordinator.Present=false;Need(!token.TryRetain(),"refused token later reused");
   p=Setup(root,"incarnation");token=p.CaptureCompletionRetention(new byte[]{9});RebirthProductiveEarnedCoordinator.Present=false;RebirthProductiveEarnedArtifact.CurrentStation.Custody=Guid.NewGuid();
   Need(!token.TryRetain()&&File.Exists(Path.Combine(root,"incarnation","op.rew")),"changed incarnation archived original");
   p=Setup(root,"world");token=p.CaptureCompletionRetention(new byte[]{9});RebirthProductiveEarnedCoordinator.Present=false;GameManager.Instance.World=new World();Need(!token.TryRetain(),"changed world archived original");
   p=Setup(root,"altered");token=p.CaptureCompletionRetention(new byte[]{9});RebirthProductiveEarnedCoordinator.Present=false;File.WriteAllBytes(Path.Combine(root,"altered","op.rew"),new byte[]{1});Need(!token.TryRetain(),"altered original archived");
   p=Setup(root,"conflict");token=p.CaptureCompletionRetention(new byte[]{9});RebirthProductiveEarnedCoordinator.Present=false;string archive=Path.Combine(root,"conflict","retained");Directory.CreateDirectory(archive);File.WriteAllBytes(Path.Combine(archive,"op.rew"),new byte[]{7});
   Need(!token.TryRetain()&&File.Exists(Path.Combine(root,"conflict","op.rew"))&&File.ReadAllBytes(Path.Combine(archive,"op.rew")).SequenceEqual(new byte[]{7}),"archive conflict deleted/replaced evidence");
   p=Setup(root,"temporary");token=p.CaptureCompletionRetention(new byte[]{9});RebirthProductiveEarnedCoordinator.Present=false;archive=Path.Combine(root,"temporary","retained");Directory.CreateDirectory(archive);File.WriteAllBytes(Path.Combine(archive,"op.rew.tmp"),new byte[]{1});Need(!token.TryRetain(),"unknown archive temporary bypassed");
   p=Setup(root,"bad-marker");Need(p.CaptureCompletionRetention(new byte[]{1})==null,"malformed expected marker captured retention");
   Console.WriteLine("PASS extracted completion retention token/file behavior; disclosed identity/marker doubles; no saved-absence proof");
  }
  finally
  {
   foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.Delete(file);
   foreach(string folder in Directory.EnumerateDirectories(root,"*",SearchOption.AllDirectories).OrderByDescending(value=>value.Length))Directory.Delete(folder);
   Directory.Delete(root);
  }
 }
}
