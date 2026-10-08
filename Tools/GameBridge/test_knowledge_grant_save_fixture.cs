using System;using System.Collections.Generic;
class EntityPlayer{}
class RebirthKnowledgeDefinition{}
class RebirthStablePlayerIdentity{}
class Progression {public HashSet<string> KnowledgeIds=new HashSet<string>();}
class RebirthWorldCharacterRecord {public Progression Progression=new Progression();public string Reason;public void Touch(string s){Reason=s;}}
class RebirthSurvivorDefinitionRegistry {public static bool TryGetKnowledge(string s,out RebirthKnowledgeDefinition d){d=new RebirthKnowledgeDefinition();return true;}}
class RebirthSkillAwardService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static int Queued;public static bool TryGetEligible(EntityPlayer p,out RebirthStablePlayerIdentity i,out RebirthWorldCharacterRecord r){i=new RebirthStablePlayerIdentity();r=Record;return true;}public static void QueueOwnerPublication(EntityPlayer p){Queued++;}}
class RebirthWorldCharacterRepository {public static bool Save;public static bool SaveIfDirty(RebirthStablePlayerIdentity i,string s){return Save;}}
class RebirthSurvivorNetworkService {public static bool Success;public static int Sent;public static bool SendOwnerState(EntityPlayer p,long rev,bool force,string s){Sent++;return Success;}}
class RebirthStatisticsService {public static int Count;public static void RecordKnowledgeDiscovered(EntityPlayer p,string s){Count++;}}
class Actual {// SOURCE
}
class Check {static void Assert(bool v,string s){if(!v)throw new Exception(s);}static void Main(){var p=new EntityPlayer();var r=RebirthSkillAwardService.Record;Assert(!Actual.Grant(p,"recipe","experiment")&&!r.Progression.KnowledgeIds.Contains("recipe")&&RebirthSurvivorNetworkService.Sent==0&&RebirthStatisticsService.Count==0,"save failure rollback");RebirthWorldCharacterRepository.Save=true;Assert(Actual.Grant(p,"recipe","experiment")&&r.Progression.KnowledgeIds.Contains("recipe")&&RebirthSkillAwardService.Queued==1&&RebirthStatisticsService.Count==1,"saved grant pending publication");Assert(!Actual.Grant(p,"recipe","repeat")&&RebirthSurvivorNetworkService.Sent==1&&RebirthStatisticsService.Count==1,"duplicate no reaward");Console.WriteLine("PASS actual knowledge Grant: failed-save rollback, saved grant failed-send retry queue, duplicate no reaward. Persistence/network/registry/player adapters doubled.");}}