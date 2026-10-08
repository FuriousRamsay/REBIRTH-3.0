using System;using System.Collections.Generic;
public class EntityPlayer{}
public class RebirthStablePlayerIdentity{}
public class Progression {public HashSet<string> KnowledgeIds=new HashSet<string>();}
public class RebirthWorldCharacterRecord {public Progression Progression=new Progression();}
public class RebirthLiteratureDefinition {public string Kind="discovery",KnowledgeId="knowledge.recipe",SkillId,MarkerId;public float Amount;}
static class RebirthSkillAwardService {public static bool Eligible=true;public static bool TryGetEligible(EntityPlayer p,out RebirthStablePlayerIdentity i,out RebirthWorldCharacterRecord r){i=new RebirthStablePlayerIdentity();r=RebirthWorldCharacterService.Record;return p!=null&&Eligible;}}
static class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool Save=true;public static int Saves;public static void MarkDirty(RebirthWorldCharacterRecord r,string reason){}public static bool FlushPlayer(EntityPlayer p,string reason){Saves++;return Save;}public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return p!=null;}}
static class RebirthSurvivorNetworkService {public static int Sends;public static void SendOwnerState(EntityPlayer p,long id,bool success,string reason){Sends++;}}
static class RebirthKnowledgeService {public static int Grants;public static bool HasKnowledge(EntityPlayer p,string id){return p!=null&&RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Contains(id);}public static bool Grant(EntityPlayer p,string id,string source){Grants++;return RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Add(id);}public static string GetDisplayName(string id){return id;}}
static class RebirthSkillKnowledgeService {public static bool TryStudyLiterature(EntityPlayer p,string skill,float amount,string marker,string source,out float applied,out bool already){applied=amount;already=false;return true;}}
static class Localization {public static string Get(string key){return key;}}
static class Subject {
// MARKER_METHODS
// APPLY_METHOD
// COMPLETION_METHODS
static string GetSkillDisplayName(string id){return id;}
public static bool Apply(EntityPlayer p,RebirthLiteratureDefinition d,out string message){return TryApplyDefinition(p,d,"literature:test","book",out message);}
}
class Check {
static void Assert(bool v,string m){if(!v)throw new Exception(m);}
static void Main(){var p=new EntityPlayer();var d=new RebirthLiteratureDefinition();string message;string marker=Subject.RecipeReadMarker(d.KnowledgeId);
RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Add(d.KnowledgeId);Assert(!Subject.IsAlreadyCompleted(p,d),"Experiment/background knowledge must not prove reading");
Assert(Subject.Apply(p,d,out message),"Known recipe can actually be read");Assert(Subject.IsAlreadyCompleted(p,d)&&RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Contains(marker),"Read evidence recorded");Assert(RebirthKnowledgeService.Grants==0&&RebirthSurvivorNetworkService.Sends==1,"No repeat knowledge award; owner sees marker after save");int saves=RebirthWorldCharacterService.Saves;Assert(Subject.Apply(p,d,out message)&&RebirthWorldCharacterService.Saves==saves&&RebirthSurvivorNetworkService.Sends==1,"Duplicate read evidence is idempotent");
RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthWorldCharacterService.Save=false;Assert(!Subject.Apply(p,d,out message),"Failed marker save reports failure");Assert(!Subject.IsAlreadyCompleted(p,d)&&!RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Contains(marker),"Failed save cannot open read-only recipe in memory");Assert(RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Contains(d.KnowledgeId),"Existing successful knowledge grant is retained for retry");RebirthWorldCharacterService.Save=true;Assert(Subject.Apply(p,d,out message)&&Subject.IsAlreadyCompleted(p,d),"Read retry completes provenance without repeating knowledge grant");Assert(RebirthKnowledgeService.Grants==1,"One knowledge grant");
RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthSkillAwardService.Eligible=false;Assert(!Subject.Apply(p,d,out message)&&!Subject.IsAlreadyCompleted(p,d),"Held/unavailable character cannot record read evidence");RebirthSkillAwardService.Eligible=true;
Assert(Subject.IsInternalReadMarker(marker)&&Subject.IsRecipeReadMarker(marker)&&!Subject.IsRecipeReadMarker("cooking.recipe.example"),"Hidden read marker classification");Assert(Subject.RecipeReadMarker("")=="","Empty ID cannot manufacture read marker");d.Kind="theory";d.MarkerId="literature.read.theory";RebirthWorldCharacterService.Record.Progression.KnowledgeIds.Add(d.MarkerId);Assert(Subject.IsAlreadyCompleted(p,d),"Existing theory completion unchanged");Console.WriteLine("PASS actual literature methods: experiment knowledge is not reading; known-recipe read, save failure/retry, eligible-character gate, idempotence, marker classification and theory compatibility");}
}