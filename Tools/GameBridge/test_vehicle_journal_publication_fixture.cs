using System;
using System.Collections.Generic;
using System.Threading;
public class RebirthVehicleRequestResult {}
class GameIO {public static string GetSaveGameDir(){return "A";}}
class Test {
 static long generation=1;
 static readonly object Sync=new object();static readonly Dictionary<Guid,RebirthVehicleRequestResult> Completed=new Dictionary<Guid,RebirthVehicleRequestResult>();
 static readonly HashSet<Guid> InFlight=new HashSet<Guid>();static readonly Queue<Guid> Order=new Queue<Guid>();
 static bool loadFailed;static string loadedDirectory="A";const int MaxEntries=512;static int saves;
 static void EnsureLoaded(){}static RebirthVehicleRequestResult CloneResult(RebirthVehicleRequestResult result){return new RebirthVehicleRequestResult();}
 static void Persist(string directory){if(!Monitor.IsEntered(Sync)||directory!="A")throw new Exception("unlocked/wrong-directory write");saves++;}
 // SOURCE
 static void Main(){
  var id=Guid.NewGuid();InFlight.Add(id);Remember(id,new RebirthVehicleRequestResult(),1);
  if(saves!=1||Completed.Count!=1||InFlight.Contains(id))throw new Exception("publication failed");
  Remember(id,new RebirthVehicleRequestResult(),1);if(saves!=1)throw new Exception("duplicate write");
  loadFailed=true;Remember(Guid.NewGuid(),new RebirthVehicleRequestResult(),1);if(saves!=1||Completed.Count!=1)throw new Exception("failed store published");
  Console.WriteLine("PASS actual Remember: persistence holds publication lock and receives loaded directory; duplicate and unavailable store do not write. Loader/persistence substituted.");
 }
}
