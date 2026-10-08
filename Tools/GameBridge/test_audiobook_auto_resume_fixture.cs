using System;using System.Collections.Generic;
public class EntityPlayer{public int entityId=1;public bool IsDead(){return false;}}
class Session{public float Progress=.25f,EffectiveSeconds=100;public bool Paused=true,CompletedAwaitingSave,CancelAwaitingSave;public string SourceLiteratureId="book";}
class Cassette{public string ItemId="audio",SlotId="slot";}
class Support{public List<Cassette>AudiobookCassettes=new List<Cassette>();}
class RebirthWorldCharacterRecord{public Support Support=new Support();public bool IsComplete=true;}
class RebirthLiteratureDefinition{}
class RebirthAudiobookDefinition{public string SourceLiteratureId="book";}
static class RebirthWorldCharacterRepository{public static bool IsServerAuthority=true;}
static class RebirthWorldCharacterService{public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return true;}}
static class RebirthProgressionRuntimeConfig{public static bool TryGetLiterature(string id,out RebirthLiteratureDefinition value){value=new RebirthLiteratureDefinition();return true;}public static bool TryGetAudiobook(string id,out RebirthAudiobookDefinition value){value=new RebirthAudiobookDefinition();return true;}}
static class RebirthLiteratureService{public static bool Completed;public static bool IsAlreadyCompleted(EntityPlayer p,RebirthLiteratureDefinition d){return Completed;}}
static class Localization{public static string Get(string key){return key;}}
static class RebirthSurvivorGearService{public static bool Equipped=true;public static bool HasEquippedWalkman(EntityPlayer p){return Equipped;}}
static class RebirthStudyHudNetworkService{public static int Sends;public static void SendAudiobook(EntityPlayer p,string id,float progress,float seconds){if(progress!=.25f||seconds!=75)throw new Exception("HUD progress mismatch");Sends++;}}
static class Mathf{public static float Max(float a,float b){return Math.Max(a,b);}}
public class AudioResumeFixture{
 static Dictionary<int,Session>Active=new Dictionary<int,Session>();static int Starts;
 static bool HasSessionCassette(EntityPlayer p,RebirthWorldCharacterRecord r,Session s){return true;}
 // RESUME
 static bool TryBeginStored(EntityPlayer p,string slot,out string message){Starts++;message="start";return true;}
 // METHOD
 public static string Run(){int checks=0;Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};var player=new EntityPlayer();string message;
 Active[1]=new Session();check(TryBeginNextPending(player,out message)&&!Active[1].Paused&&Starts==0&&RebirthStudyHudNetworkService.Sends==1,"existing pending audio actually resumes");
 Active[1].Paused=true;RebirthSurvivorGearService.Equipped=false;check(!TryBeginNextPending(player,out message)&&Active[1].Paused&&Starts==0&&RebirthStudyHudNetworkService.Sends==1,"resume refusal propagated without starting another");
 Active[1].CompletedAwaitingSave=true;check(!TryBeginNextPending(player,out message)&&RebirthStudyHudNetworkService.Sends==1,"completion hold does not resume");Active[1].CompletedAwaitingSave=false;Active[1].CancelAwaitingSave=true;check(!TryBeginNextPending(player,out message)&&RebirthStudyHudNetworkService.Sends==1,"cancellation hold does not resume");
 RebirthSurvivorGearService.Equipped=true;Active.Clear();RebirthWorldCharacterService.Record.Support.AudiobookCassettes.Add(new Cassette());check(TryBeginNextPending(player,out message)&&Starts==1,"no active session starts stored pending audio");
 RebirthLiteratureService.Completed=true;check(!TryBeginNextPending(player,out message)&&Starts==1,"completed library does not autoplay");
 return "PASS "+checks+" actual next-pending audiobook method checks; actual Resume/HUD invocation; catalogue/ownership/equipment/HUD adapters doubled";
 }
}