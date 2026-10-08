using System;
public class World{}
public class EntityPlayer {public World world=new World();}
public class RebirthWorldCharacterRecord{}
public class RebirthStationGridAdmission{}
public static class RebirthWorldCharacterService {
 public static RebirthWorldCharacterRecord Current;
 public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Current;return r!=null;}
}
public static class RebirthWorldCharacterRepository {
 public static bool Cached=true;
 public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>Cached;
}
public static class RebirthStationObservationDispatcher {
 public static int Calls;public static bool Result=true;public static Action During;
 public static bool TrySavePreparation(EntityPlayer p,RebirthStationGridAdmission a){Calls++;During?.Invoke();return Result;}
}
class Test {
// SOURCE
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
 static RebirthWorldCharacterRecord Setup(){
  RebirthStationObservationDispatcher.Calls=0;RebirthStationObservationDispatcher.Result=true;RebirthStationObservationDispatcher.During=null;
  RebirthWorldCharacterRepository.Cached=true;return RebirthWorldCharacterService.Current=new RebirthWorldCharacterRecord();
 }
 static void Main(){
  var p=new EntityPlayer();var a=new RebirthStationGridAdmission();var owner=Setup();
  Check(Save(p,owner,a)&&RebirthStationObservationDispatcher.Calls==1,"exact owner routes once");
  owner=Setup();RebirthWorldCharacterService.Current=new RebirthWorldCharacterRecord();
  Check(!Save(p,owner,a)&&RebirthStationObservationDispatcher.Calls==0,"wrong owner refuses before save");
  owner=Setup();RebirthWorldCharacterRepository.Cached=false;
  Check(!Save(p,owner,a)&&RebirthStationObservationDispatcher.Calls==0,"uncached owner refuses before save");
  owner=Setup();RebirthStationObservationDispatcher.Result=false;Check(!Save(p,owner,a),"dispatcher refusal propagates");
  owner=Setup();RebirthStationObservationDispatcher.During=()=>RebirthWorldCharacterService.Current=new RebirthWorldCharacterRecord();
  Check(!Save(p,owner,a),"replacement during save refuses");
  owner=Setup();RebirthStationObservationDispatcher.During=()=>p.world=new World();
  Check(!Save(p,owner,a),"world migration during save refuses");
  owner=Setup();RebirthStationObservationDispatcher.During=()=>RebirthWorldCharacterRepository.Cached=false;
  Check(!Save(p,owner,a),"cache reset during save refuses");
  owner=Setup();Check(!Save(null,owner,a)&&!Save(p,owner,null),"missing arguments refuse");
  Console.WriteLine("PASS "+passed+" actual producer Save routing checks; dispatcher/repository/native adapters doubled.");
 }
}