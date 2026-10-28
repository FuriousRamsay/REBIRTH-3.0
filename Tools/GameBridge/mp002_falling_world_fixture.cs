// AUTHORED_NOT_RUN. Actual falling-block prefix; native/service APIs doubled.
using System;
struct Vector3i {}
class World {internal bool Remote;internal bool IsRemote()=>Remote;}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
class ConnectionManager {internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class RebirthFireService {internal static RebirthFireService Instance=new RebirthFireService();internal int Removes;internal void Remove(Vector3i pos){Removes++;}}
static class RebirthFireVisualManager {internal static int Removes;internal static void RemovePositionImmediately(Vector3i pos){Removes++;}}
static class RebirthFireSleeperActivation {internal static int Calls;internal static void OnFallingBlock(World world,Vector3i pos){Calls++;}}
class Actual {
 // ACTUAL_FALLING_PREFIX
 internal static void Run(){
  var a=new World();var b=new World();var position=new Vector3i();GameManager.Instance.World=b;
  WorldAddFallingBlockPrefix(a,position);WorldAddFallingBlockPrefix(null,position);
  b.Remote=true;WorldAddFallingBlockPrefix(b,position);b.Remote=false;
  ThreadManager.Main=false;WorldAddFallingBlockPrefix(b,position);ThreadManager.Main=true;
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;WorldAddFallingBlockPrefix(b,position);
  Require(RebirthFireService.Instance.Removes==0&&RebirthFireVisualManager.Removes==0&&RebirthFireSleeperActivation.Calls==0,"foreign callback mutated current world");
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;WorldAddFallingBlockPrefix(b,position);
  Require(RebirthFireService.Instance.Removes==1&&RebirthFireVisualManager.Removes==1&&RebirthFireSleeperActivation.Calls==1,"current native callback lost original effects");
  Console.WriteLine("PASS actual falling-block world guard with native/service doubles; not native physics");
 }
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
class Program {static void Main()=>Actual.Run();}
