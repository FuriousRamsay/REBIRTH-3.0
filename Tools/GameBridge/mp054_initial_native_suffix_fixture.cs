// AUTHORED_NOT_RUN. Actual complete outer scope, initial suffix reader/writer and immutable record writer.
// Native schema/capture/invocation, artifact identity, budget and native publication are explicit adapters.
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Security.Cryptography;
class World{internal bool IsRemote()=>false;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
class TileEntityWorkstation{internal byte[] Body=new byte[24];}
class RebirthCamperStationState:TileEntityWorkstation{}
static class RebirthRecyclerNativeLoop{internal static TileEntityWorkstation Station;internal static object Token=new object();internal static bool IsCurrentInvocation(TileEntityWorkstation value)=>ReferenceEquals(value,Station);internal static object InvocationToken(TileEntityWorkstation value)=>IsCurrentInvocation(value)?Token:null;}
static class RebirthProductiveEarnedNativeBody{internal const int MaximumBodyBytes=2048;internal static bool TryRead(byte[] body,int offset,out object image,out ulong elapsed){image=null;elapsed=0;return body!=null&&body.Length==24&&offset==13;}internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] body,int offset)=>offset==13&&station.Body.SequenceEqual(body);internal static bool TryCaptureCurrent(TileEntityWorkstation station,out byte[] body,out int offset){body=station.Body.ToArray();offset=13;return true;}}
static class RebirthProductiveEarnedRowProof{internal const int MaximumFrameBytes=2048;internal class Frame{internal byte[] AfterNative=new byte[24];internal int AfterElapsedOffset=13;}}
static class RebirthProductiveEarnedArtifact{internal static bool SafePath(string path)=>!string.IsNullOrEmpty(path);internal class Prepared{internal string OriginalPath;internal byte[] Image=new byte[]{1,2,3};internal bool Current=true;internal byte[] CopyImage()=>Image.ToArray();internal byte[] OriginalDigest(){using(var sha=SHA256.Create())return sha.ComputeHash(Image);}internal bool MatchesAfter(TileEntityWorkstation station)=>Current;}}
static class RebirthProductiveEarnedObservationBudget{internal static IDisposable TryEnterDecoder()=>new Lease();class Lease:IDisposable{public void Dispose(){}}}
static class RebirthProductiveEarnedPublication{internal class Observation:IDisposable{public void Dispose(){}}internal static int Begins;internal static Observation BeginAfterNativeWork(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact){Begins++;return new Observation();}}
// ACTUAL_RECORD
// ACTUAL_SCOPE
// ACTUAL_SUFFIX
internal static partial class RebirthProductiveEarnedCoordinator{
 internal class Attempt{internal TileEntityWorkstation Station;internal RebirthProductiveEarnedRowProof.Frame Frame=new RebirthProductiveEarnedRowProof.Frame();internal RebirthProductiveEarnedArtifact.Prepared Artifact;internal RebirthProductiveEarnedPublication.Observation Observation;internal object NativeOutput;internal bool OutputTimerObserved,NativeTickCustody,NativeSuffixRequired;}
 static bool Enabled=true;static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();static void CaptureReturnedNativeTerminal(Attempt original){}
 static int checks;static string root;static TileEntityWorkstation station;static Attempt original;static object scope;
 static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static void Fresh(){retained.Clear();nativeTerminalOuterTick=null;station=new TileEntityWorkstation();RebirthRecyclerNativeLoop.Station=station;RebirthRecyclerNativeLoop.Token=new object();original=new Attempt{Station=station,Artifact=new RebirthProductiveEarnedArtifact.Prepared{OriginalPath=Path.Combine(root,Guid.NewGuid()+".rew")}};File.WriteAllBytes(original.Artifact.OriginalPath,original.Artifact.Image);retained.Add(station,original);scope=EnterNativeTerminalOuterTick(station,true);RebirthProductiveEarnedPublication.Begins=0;}
 static void Apply(){ObserveInitialNativeApplied(original);}
 static void Return(){ObserveNativeTerminalOuterTickReturned(station,true,scope);}
 public static void Main(){root=Path.Combine(Path.GetTempPath(),"rebirth-initial-suffix-"+Guid.NewGuid());Directory.CreateDirectory(root);
 Fresh();Apply();Need(original.NativeSuffixRequired&&original.NativeTickCustody,"entered original not held before native suffix");Need(!EffectiveNativeAfter(original,out _,out _),"entered suffix fell back to pre-suffix body");station.Body[0]=7;Return();Need(RebirthProductiveEarnedPublication.Begins==1,"actual outer return did not start saved observer");Need(EffectiveNativeAfter(original,out var body,out int offset)&&body[0]==7&&offset==13,"actual material suffix not retained");Need(File.ReadAllBytes(original.Artifact.OriginalPath).SequenceEqual(new byte[]{1,2,3}),"immutable original rewritten");Need(!TryReadInitialNativeSuffix(original.Artifact.OriginalPath,new byte[]{9},out _,out _),"foreign original digest qualified");var record=File.ReadAllBytes(original.Artifact.OriginalPath+".initial-native-suffix");station.Body[0]=8;Return();Need(File.ReadAllBytes(original.Artifact.OriginalPath+".initial-native-suffix").SequenceEqual(record)&&RebirthProductiveEarnedPublication.Begins==1,"second return replaced immutable suffix");
 Fresh();Apply();ObserveNativeTerminalOuterTickReturned(station,false,scope);Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"skipped outer return qualified");
 Fresh();Apply();GameManager.Instance.World=new World();Return();Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"world replacement qualified");
 Fresh();Apply();retained[station]=new Attempt();Return();Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"replacement original qualified");
 Fresh();Apply();RebirthRecyclerNativeLoop.Token=new object();Apply();Return();Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"ambiguous earning invocation qualified");
 Fresh();station.Body[0]=1;Apply();Need(!original.NativeSuffixRequired,"nonmatching earned native body admitted");Return();Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"unobserved earning became suffix");
 Fresh();Apply();original.OutputTimerObserved=true;Return();Need(!File.Exists(original.Artifact.OriginalPath+".initial-native-suffix"),"terminal output became initial source");
 Fresh();Need(EffectiveNativeAfter(original,out body,out offset)&&body[0]==0,"suffix-free immutable child lost original body");File.WriteAllBytes(original.Artifact.OriginalPath+".initial-native-suffix.tmp",new byte[]{1});Need(!EffectiveNativeAfter(original,out _,out _),"staged unknown suffix silently fell back");
 Console.WriteLine("PASS "+checks+" actual initial suffix assertions; native schema/invocation/save adapters explicitly excluded");}
}
