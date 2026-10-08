using System;
using System.Collections.Generic;
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class EntityPlayer {public World world=new World();public RebirthStablePlayerIdentity Identity=new RebirthStablePlayerIdentity();}
class RebirthStablePlayerIdentity {public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity id){id=p.Identity;return id!=null;}}
class Progression {public HashSet<string> KnowledgeIds=new HashSet<string>();}
class RebirthWorldCharacterRecord {public Progression Progression=new Progression();}
class RebirthWorldCharacterRepository {public static bool IsServerAuthority=true;public static Dictionary<RebirthStablePlayerIdentity,RebirthWorldCharacterRecord> Records=new Dictionary<RebirthStablePlayerIdentity,RebirthWorldCharacterRecord>();public static bool TryGet(RebirthStablePlayerIdentity id,out RebirthWorldCharacterRecord r){return Records.TryGetValue(id,out r);}}
class RebirthSurvivorOwnerScalars {public HashSet<string> Knowledge=new HashSet<string>();public bool HasKnowledge(string id){return Knowledge.Contains(id);}}
class RebirthSurvivorClientState {public static EntityPlayer Owner;public static RebirthSurvivorOwnerScalars Scalars;public static bool TryGetOwnerScalars(EntityPlayer p,out RebirthSurvivorOwnerScalars s){s=ReferenceEquals(p,Owner)?Scalars:null;return s!=null;}}
class Recipe {public string Name;public string GetName(){return Name;}}
class RebirthRecipeKnowledgeRule {public string KnowledgeId,LegacyKnowledgeId;}
class RebirthSurvivorMode {public static bool IsEnabledForCurrentWorld(){return true;}}
class RebirthProgressionRuntimeConfig {public static Dictionary<string,RebirthRecipeKnowledgeRule> Rules=new Dictionary<string,RebirthRecipeKnowledgeRule>();public static bool TryGetRecipeRule(string name,out RebirthRecipeKnowledgeRule r){return Rules.TryGetValue(name,out r);}}
class Dish {public List<string> Cards=new List<string>{"unread-card"};}
class RebirthKnowledgeService {
// HAS
// CAN
}
class RebirthCookingCatalogue {static HashSet<string> BasicRecipes=new HashSet<string>();static Dish Get(string name){return new Dish();}static bool Studied(EntityPlayer p,string card){return false;}
// KNOWN
}
class Check {static void A(bool b,string label){if(!b)throw new Exception(label);}static void Main(){var rows=
// DATA
;var host=new EntityPlayer();var other=new EntityPlayer();var client=new EntityPlayer{world=new World{Remote=true}};var hostRecord=new RebirthWorldCharacterRecord();RebirthWorldCharacterRepository.Records[host.Identity]=hostRecord;RebirthWorldCharacterRepository.Records[other.Identity]=new RebirthWorldCharacterRecord();RebirthSurvivorClientState.Owner=client;RebirthSurvivorClientState.Scalars=new RebirthSurvivorOwnerScalars();
foreach(var row in rows){hostRecord.Progression.KnowledgeIds.Clear();RebirthSurvivorClientState.Scalars.Knowledge.Clear();RebirthProgressionRuntimeConfig.Rules[row[0]]=new RebirthRecipeKnowledgeRule{KnowledgeId=row[1]};var recipe=new Recipe{Name=row[0]};string required;A(!RebirthKnowledgeService.CanCraft(host,row[0],out required)&&required==row[1]&&!RebirthKnowledgeService.CanCraft(client,row[0],out required),"ungranted general crafting knowledge gate");A(!RebirthCookingCatalogue.Known(host,recipe)&&!RebirthCookingCatalogue.Known(client,recipe),"ungranted recipe hidden");hostRecord.Progression.KnowledgeIds.Add(row[1]);A(RebirthKnowledgeService.CanCraft(host,row[0],out required)&&!RebirthKnowledgeService.CanCraft(other,row[0],out required),"host profile general crafting ownership");A(RebirthCookingCatalogue.Known(host,recipe),"profile host recipe known without reading card");A(!RebirthCookingCatalogue.Known(other,recipe)&&!RebirthCookingCatalogue.Known(client,recipe),"host knowledge cannot leak to other owner");RebirthSurvivorClientState.Scalars.Knowledge.Add(row[1]);A(RebirthKnowledgeService.CanCraft(client,row[0],out required),"client profile general crafting knowledge gate");A(RebirthCookingCatalogue.Known(client,recipe),"profile client recipe known from owner snapshot without card read");}
Console.WriteLine("PASS actual recipe Known + HasKnowledge + CanCraft: "+rows.Length+" all-background recipe mappings, host/client personal knowledge, no card reread; native repository/snapshot/config adapters doubled");}}