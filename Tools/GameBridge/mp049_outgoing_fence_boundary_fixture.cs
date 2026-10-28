using System;
using System.Collections.Generic;
// Explicit adapters: native World/manager, original capture, writer registry,
// lifecycle owner transition and directory lookup. No native lineage is minted.
public class World { public bool Remote; public bool IsRemote()=>Remote; }
public class ConnectionManager { public bool IsServer; }
public static class SingletonMonoBehaviour<T> { public static T Instance; }
public class GameManager { public static GameManager Instance=new GameManager(); public World World; }
public static class ThreadManager { public static bool Main=true; public static bool IsMainThread()=>Main; }
internal sealed class RebirthNpcSaveQuarantineScope { internal World World; internal bool Unchanged=true; internal bool IsUnchangedOriginal()=>Unchanged; }
internal static class RebirthNpcSaveQuarantineCapture { internal sealed class Snapshot { internal RebirthNpcSaveQuarantineScope Scope; internal bool Unchanged=true; internal Action OnRead; internal bool IsUnchangedOriginal(out string error){error="capture";OnRead?.Invoke();return Unchanged&&Scope.IsUnchangedOriginal();} } }
internal static class RebirthNpcSaveQuarantineBundle { internal static bool TryStageIncomingServerEpoch(World old,string oldPath,World incoming,string incomingPath)=>false; internal static bool AdmissionEnabled=true,Staged;internal static World Old,Incoming;internal static string OldPath,IncomingPath;internal static int Queries;internal static bool CanInstallIncomingClientEpoch(World old,string oldPath,World incoming,string incomingPath){Queries++;return Staged&&ThreadManager.Main&&incoming.Remote&&ReferenceEquals(old,Old)&&ReferenceEquals(incoming,Incoming)&&ReferenceEquals(GameManager.Instance.World,incoming)&&oldPath==OldPath&&incomingPath==IncomingPath;} }
public static partial class RebirthNpcPersistenceFile {
 private static readonly object FailureSync=new object(); private static readonly List<int> Writers=new List<int>();
 // ACTUAL_FENCE_MEMBERS
 internal static void Run(Action<bool> check) {
  var outgoing=new World();var incoming=new World();var scope=new RebirthNpcSaveQuarantineScope{World=outgoing};var capture=new RebirthNpcSaveQuarantineCapture.Snapshot{Scope=scope};
  Action reset=()=>{quarantineWriterFence?.Dispose();Writers.Clear();ThreadManager.Main=true;GameManager.Instance.World=incoming;incoming.Remote=false;scope.Unchanged=capture.Unchanged=true;capture.OnRead=null;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};};
  QuarantineWriterFence fence;string error;
  reset();check(TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error)&&fence.IsCurrent);fence.Dispose();check(!fence.IsCurrent&&!HasQuarantineWriterFence);
  reset();incoming.Remote=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;check(TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error)&&fence.IsCurrent);fence.Dispose();
  reset();ThreadManager.Main=false;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error)&&fence==null);
  reset();check(!TryFenceCapturedOutgoingServer(null,incoming,out fence,out error));
  reset();scope.World.Remote=true;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));scope.World.Remote=false;
  reset();check(!TryFenceCapturedOutgoingServer(capture,outgoing,out fence,out error));
  reset();check(!TryFenceCapturedOutgoingServer(capture,null,out fence,out error));
  reset();SingletonMonoBehaviour<ConnectionManager>.Instance=null;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));
  reset();incoming.Remote=true;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));
  reset();capture.Unchanged=false;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));
  reset();scope.Unchanged=false;check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));
  reset();Writers.Add(1);check(!TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));
  reset();check(TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));QuarantineWriterFence second;check(!TryFenceCapturedOutgoingServer(capture,incoming,out second,out error)&&second==null&&fence.IsCurrent);fence.Dispose();
  reset();check(TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));GameManager.Instance.World=new World();check(!fence.IsCurrent);fence.Dispose();
  reset();check(TryFenceCapturedOutgoingServer(capture,incoming,out fence,out error));scope.Unchanged=false;check(!fence.IsCurrent);fence.Dispose();
 }
}
public static class RebirthNpcLifecycle {
 private static readonly object Sync=new object();private static World activeWorldReference;private static string activeSaveDirectory;
 private static string currentDirectory="B";private static int calls;private static Action transition;private static bool lookupFails;
 private static string RequireCurrentWorldDirectory(World world){if(lookupFails)throw new Exception();return currentDirectory;}
 private static bool SameShutdownSave(string left,string right)=>string.Equals(left,right,StringComparison.OrdinalIgnoreCase);
 private static void EnsureCurrentWorldScope(){calls++;transition?.Invoke();}
 // ACTUAL_NATIVE_EPOCH_BOUNDARY
 internal static void Run(Action<bool> check){
  var old=new World();var current=new World();Action reset=()=>{ThreadManager.Main=true;current.Remote=false;RebirthNpcSaveQuarantineBundle.AdmissionEnabled=true;RebirthNpcSaveQuarantineBundle.Staged=false;RebirthNpcSaveQuarantineBundle.Queries=0;RebirthNpcSaveQuarantineBundle.Old=old;RebirthNpcSaveQuarantineBundle.Incoming=current;RebirthNpcSaveQuarantineBundle.OldPath="A";RebirthNpcSaveQuarantineBundle.IncomingPath="B";GameManager.Instance.World=current;activeWorldReference=old;activeSaveDirectory="A";currentDirectory="B";calls=0;lookupFails=false;nativeEpochBoundary=false;transition=()=>{activeWorldReference=current;activeSaveDirectory="B";};};
  reset();ThreadManager.Main=false;check(!PrepareNativeEpochBoundary()&&calls==0);
  reset();RebirthNpcSaveQuarantineBundle.AdmissionEnabled=false;check(!PrepareNativeEpochBoundary()&&calls==0);
  reset();RebirthNpcSaveQuarantineBundle.AdmissionEnabled=false;activeWorldReference=null;check(PrepareNativeEpochBoundary()&&calls==0);
  reset();RebirthNpcSaveQuarantineBundle.AdmissionEnabled=false;activeWorldReference=current;activeSaveDirectory="B";check(PrepareNativeEpochBoundary()&&calls==0);
  reset();GameManager.Instance.World=null;check(!PrepareNativeEpochBoundary()&&calls==0);
  reset();activeWorldReference=null;check(PrepareNativeEpochBoundary()&&calls==0);
  reset();activeWorldReference=current;activeSaveDirectory="b";check(PrepareNativeEpochBoundary()&&calls==0);
  reset();lookupFails=true;check(!PrepareNativeEpochBoundary()&&calls==0);
  reset();check(PrepareNativeEpochBoundary()&&calls==1&&!nativeEpochBoundary);
  reset();activeWorldReference=current;check(PrepareNativeEpochBoundary()&&calls==1);
  reset();transition=()=>{};check(!PrepareNativeEpochBoundary()&&calls==1&&!nativeEpochBoundary);
  reset();transition=()=>{throw new Exception();};check(!PrepareNativeEpochBoundary()&&!nativeEpochBoundary);
  reset();transition=()=>{check(!PrepareNativeEpochBoundary());activeWorldReference=current;activeSaveDirectory="B";};check(PrepareNativeEpochBoundary()&&calls==1);
  reset();transition=()=>{activeWorldReference=current;activeSaveDirectory="B";GameManager.Instance.World=new World();};check(!PrepareNativeEpochBoundary()&&!nativeEpochBoundary);
  reset();current.Remote=true;RebirthNpcSaveQuarantineBundle.Staged=true;transition=()=>{throw new Exception();};check(PrepareNativeEpochBoundary()&&calls==1&&ReferenceEquals(activeWorldReference,old)&&activeSaveDirectory=="A"&&!nativeEpochBoundary&&RebirthNpcSaveQuarantineBundle.Queries==1);
  reset();RebirthNpcSaveQuarantineBundle.Staged=true;transition=()=>{throw new Exception();};check(!PrepareNativeEpochBoundary());
  reset();current.Remote=true;transition=()=>{throw new Exception();};check(!PrepareNativeEpochBoundary());
  reset();current.Remote=true;RebirthNpcSaveQuarantineBundle.Staged=true;RebirthNpcSaveQuarantineBundle.OldPath="foreign";transition=()=>{throw new Exception();};check(!PrepareNativeEpochBoundary());
  reset();current.Remote=true;RebirthNpcSaveQuarantineBundle.Staged=true;transition=()=>{GameManager.Instance.World=new World{Remote=true};throw new Exception();};check(!PrepareNativeEpochBoundary());
  reset();current.Remote=true;RebirthNpcSaveQuarantineBundle.Staged=true;transition=()=>{activeWorldReference=current;activeSaveDirectory="B";throw new Exception();};check(PrepareNativeEpochBoundary()&&RebirthNpcSaveQuarantineBundle.Queries==1);
  reset();RebirthNpcSaveQuarantineBundle.Staged=true;check(PrepareNativeEpochBoundary()&&RebirthNpcSaveQuarantineBundle.Queries==0);
 }
}
public static class Program { static int count;static void Check(bool value){count++;if(!value)throw new Exception("assertion "+count);}public static void Main(){RebirthNpcPersistenceFile.Run(Check);RebirthNpcLifecycle.Run(Check);Console.WriteLine("PASS actual outgoing fence/native epoch boundary assertions="+count);} }
