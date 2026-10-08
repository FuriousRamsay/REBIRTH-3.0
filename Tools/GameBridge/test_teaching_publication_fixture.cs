using System;
class EntityPlayer {}
class RebirthWorldCharacterRecord {public OriginState Origin=new OriginState();}
class OriginState {public string CreationId;}
static class Localization {public static string Get(string key){return key;}}
// SCOPE
static class RebirthSurvivorNetworkService {public static int Mode,Calls;public static bool SendOwnerState(EntityPlayer p,long revision,bool force,string reason){Calls++;if(revision!=0||!force||reason!="lesson")throw new Exception("arguments");if(Mode==2)throw new InvalidOperationException();return Mode==0;}}
static class RebirthSkillAwardService {public static int Queued;public static EntityPlayer Owner;public static void QueueOwnerPublication(EntityPlayer p){Queued++;Owner=p;}}
class Check {
 static void TestCharacters(){
  string[] ids={Guid.NewGuid().ToString("N"),"legacy-"+new string('a',64)};
  foreach(string id in ids){
   var a=new RebirthWorldCharacterRecord();var b=new RebirthWorldCharacterRecord();
   a.Origin.CreationId=id;b.Origin.CreationId=id;
   string reason;
   if(!MatchesLessonCharacters(id,id,a,b,out reason)||reason!="")throw new Exception("original characters refused");
   a.Origin.CreationId=Guid.NewGuid().ToString("N");
   if(MatchesLessonCharacters(id,id,a,b,out reason)||reason!="xuiRebirthTeachingCharacterChanged")throw new Exception("replacement instructor accepted");
   a.Origin.CreationId=id;b.Origin.CreationId=Guid.NewGuid().ToString("N");
   if(MatchesLessonCharacters(id,id,a,b,out reason))throw new Exception("replacement student accepted");
   b.Origin.CreationId=id;
   if(MatchesLessonCharacters("",id,a,b,out reason)||MatchesLessonCharacters(id,"",a,b,out reason))throw new Exception("unbound legacy outcome accepted");
   if(MatchesLessonCharacters(id,id,null,b,out reason))throw new Exception("missing record accepted");
   a.Origin=null;if(MatchesLessonCharacters(id,id,a,b,out reason))throw new Exception("missing origin accepted");
  }
  Console.WriteLine("PASS actual teaching character guard/shared scope: GUID and legacy, replacement participants, unbound and missing records; record/localization adapters doubled");
 }
 // SOURCE
 static void Main(){TestCharacters();var p=new EntityPlayer();PublishSavedOutcome(null,"lesson");if(RebirthSurvivorNetworkService.Calls!=0)throw new Exception("null publish");for(int mode=0;mode<3;mode++){RebirthSurvivorNetworkService.Mode=mode;RebirthSkillAwardService.Queued=0;PublishSavedOutcome(p,"lesson");if(RebirthSkillAwardService.Queued!=(mode==0?0:1))throw new Exception("retry count");if(mode!=0&&!ReferenceEquals(p,RebirthSkillAwardService.Owner))throw new Exception("owner identity");}Console.WriteLine("PASS actual teaching publication: success, false result and exception; same-owner retry; native transport doubled");}
}