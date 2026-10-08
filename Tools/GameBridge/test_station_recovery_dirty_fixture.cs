using System;
using System.Collections.Generic;
public class World{}
public class EntityPlayer {public World world=new World();}
public class Identity {public string StorageKey="key",CanonicalId="id";}
public class Admission{}
public class Publication {public bool Valid=true;public bool Revalidate(string path,Admission a)=>Valid;}
public class Progression {public Dictionary<string,Admission> StationPreparations=new Dictionary<string,Admission>{{"job",new Admission()}};public Dictionary<string,Publication> StationPublications=new Dictionary<string,Publication>{{"job",new Publication()}};}
public class Record {public bool Dirty=true,IsComplete=true;public Progression Progression=new Progression();public string StablePlayerKey="key",StablePlayerId="id";}
public static class RebirthWorldCharacterService {
 public static Record Current;public static Identity Identity;
 public static bool TryGet(EntityPlayer p,out Record r){r=Current;return r!=null;}
 public static bool TryGetIdentity(EntityPlayer p,out Identity i){i=Identity;return i!=null;}
}
public static class RebirthWorldCharacterRepository {
 public static bool IsServerAuthority=true,Cached=true,Exclusive=true,Saved=true;
 public static bool IsCurrentCachedRecord(Record r)=>Cached;
 public static bool HasExclusiveStationPreparation(Record r,Admission a){Reads++;return Exclusive;}
 public static int Reads; public static bool HasSavedStationPublication(Identity i,Admission a,Publication p){Reads++;return Saved;}
}
public static class GameIO {public static string GetSaveGameDir()=>"root";}
class Test {
 static bool Authority=true,LiveQueue=true;
 static bool HasLivePublishedQueue(EntityPlayer p,Admission a)=>LiveQueue;
 static bool IsCurrentAuthorityThread(World w)=>Authority&&w!=null;
// SOURCE
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;}
 static Record Setup(){
  Authority=true;LiveQueue=true;RebirthWorldCharacterRepository.Reads=0;RebirthWorldCharacterRepository.IsServerAuthority=true;RebirthWorldCharacterRepository.Cached=true;
  RebirthWorldCharacterRepository.Exclusive=true;RebirthWorldCharacterRepository.Saved=true;
  RebirthWorldCharacterService.Identity=new Identity();return RebirthWorldCharacterService.Current=new Record();
 }
 static void Main(){
  var p=new EntityPlayer();var r=Setup();Check(r.Dirty&&TryRevalidatePublishedPreparation(p,"job"),"unrelated dirty record can recover exact saved publication");
  r=Setup();RebirthWorldCharacterRepository.Saved=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"missing exact final witness refused");
  r=Setup();r.Progression.StationPublications["job"].Valid=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"native region evidence failure refused");
  r=Setup();RebirthWorldCharacterRepository.Cached=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"stale record refused");
  r=Setup();RebirthWorldCharacterService.Identity.StorageKey="other";Check(!TryRevalidatePublishedPreparation(p,"job"),"storage remap refused");
  r=Setup();RebirthWorldCharacterService.Identity.CanonicalId="other";Check(!TryRevalidatePublishedPreparation(p,"job"),"canonical remap refused");
  r=Setup();Authority=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"authority thread refused");
  r=Setup();RebirthWorldCharacterRepository.Exclusive=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"conflicting owner preparation refused");
  r=Setup();r.IsComplete=false;Check(!TryRevalidatePublishedPreparation(p,"job"),"incomplete character refused");
  r=Setup();LiveQueue=false;Check(!TryRevalidatePublishedPreparation(p,"job")&&RebirthWorldCharacterRepository.Reads==0,"unloaded or changed queue refuses before filesystem witness");
  Console.WriteLine("PASS "+passed+" actual saved-publication recovery checks; native region/repository/identity adapters doubled.");
 }
}