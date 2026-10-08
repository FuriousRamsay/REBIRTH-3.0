using System;using System.Collections.Generic;using System.Globalization;
class World {public bool Remote;public bool IsRemote(){return Remote;}}
class EntityPlayer {public World world=new World();}
class RebirthRecipeKnowledgeRule {public string KnowledgeId;}
static class RebirthProgressionRuntimeConfig {public static bool Mapped=true;public static bool TryGetRecipeRule(string r,out RebirthRecipeKnowledgeRule v){v=new RebirthRecipeKnowledgeRule{KnowledgeId="recipe.own"};return Mapped;}}
static class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
class RebirthCraftingProgressionDefinition {public bool IsUniversal,IsDisabled;public string CapabilityId="cap",Family="fixture";}
static class RebirthCraftingProgressionRegistry {public static bool IsReady=true;public static RebirthCraftingProgressionDefinition Policy=new RebirthCraftingProgressionDefinition();public static bool TryGetRecipe(string n,out RebirthCraftingProgressionDefinition p){p=Policy;return p!=null;}public static void NoteExternalCompatibilityRecipe(string a,string b){}}
static class RebirthCapabilityRegistry {public static RebirthCapabilityDefinition Definition;public static bool TryGet(string id,out RebirthCapabilityDefinition d){d=Definition;return d!=null;}public static bool TryGetRecipe(string id,out RebirthCapabilityDefinition d){d=Definition;return d!=null;}}
static class RebirthKnowledgeService {public static HashSet<string> Owned=new HashSet<string>();public static bool HasKnowledge(EntityPlayer p,string id){return Owned.Contains(id);}public static string GetDisplayName(string id){return id;}}
class RebirthAdvancedDisciplineDefinition {public string NameKey;}
static class RebirthAdvancedDisciplineRegistry {public static bool TryGetDefinition(string id,out RebirthAdvancedDisciplineDefinition d){d=new RebirthAdvancedDisciplineDefinition{NameKey=id};return true;}}
class Progression {public HashSet<string> AcquiredDisciplineIds=new HashSet<string>();}
class RebirthWorldCharacterRecord {public Progression Progression=new Progression();}
static class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record=new RebirthWorldCharacterRecord();public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return true;}}
class RebirthSurvivorOwnerScalars {}
static class RebirthSurvivorClientState {public static Progression Snapshot=new Progression();public static Progression GetOwnerStateSnapshot(){return Snapshot;}public static bool TryGetOwnerScalars(EntityPlayer p,out RebirthSurvivorOwnerScalars s){s=new RebirthSurvivorOwnerScalars();return true;}}
static class RebirthBlueprintService {public static bool Owned;public static bool HasBlueprint(EntityPlayer p,string id){return Owned;}}
static class RebirthServiceCraftSkillService {public static float Level;public static bool Resolved=true;public static bool TryGetPracticalSkillValue(EntityPlayer p,string id,out float v){v=Level;return Resolved;}}
class RebirthSkillDefinition {public string NameKey;}
static class RebirthSurvivorDefinitionRegistry {public static bool TryGetSkill(string id,out RebirthSkillDefinition d){d=new RebirthSkillDefinition{NameKey=id};return true;}}
static class Localization {public static string Get(string key){return key;}}
static class Subject {
// RECIPE_METHODS
// EVALUATOR_METHODS
}
class Check {
 static RebirthCapabilityRequirement Leaf(string kind,string id,float min=0,bool hard=false,float recommended=0){return new RebirthCapabilityRequirement(kind,id,min,hard,recommended,recommended>0,null);}
 static RebirthCapabilityRequirement Group(string kind,params RebirthCapabilityRequirement[] leaves){return new RebirthCapabilityRequirement(kind,"",0,false,0,false,leaves);}
 static void Set(RebirthCapabilityRequirement r){RebirthCapabilityRegistry.Definition=new RebirthCapabilityDefinition("cap","fixture","recipe","test","visible",false,r);}
 static void Assert(bool v,string message){if(!v)throw new Exception(message);}
 static void Main(){var p=new EntityPlayer();var own=Leaf("knowledge","recipe.own");var skill=Leaf("skill","skill.crafting",10,true,20);var extra=Leaf("knowledge","technique.other");var blueprint=Leaf("blueprint","plan");var discipline=Leaf("discipline","discipline");
 Set(Group("all",own,skill));RebirthServiceCraftSkillService.Level=0;
 Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Own recipe exemption must not waive hard skill");
 RebirthServiceCraftSkillService.Level=10;Assert(Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Exact own knowledge can be discovered after hard skill met");Assert(!Subject.EvaluateRecipe(p,"test").IsAllowed,"Ordinary recipe evaluation must still require knowledge");Assert(Subject.EvaluateRecipeForDiscovery(p,"test").Warnings.Count==1,"Recommended skill remains warning");
 Set(Group("all",own,extra));Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Unrelated technique knowledge retained");RebirthKnowledgeService.Owned.Add("technique.other");Assert(Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Other knowledge met");
 Set(Group("all",own,blueprint,discipline));Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Blueprint and discipline retained");RebirthBlueprintService.Owned=true;Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Discipline retained");RebirthWorldCharacterService.Record.Progression.AcquiredDisciplineIds.Add("discipline");Assert(Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"All other prerequisites met");
 Set(Group("any",Group("all",own,skill),Group("all",blueprint,extra)));RebirthBlueprintService.Owned=false;RebirthServiceCraftSkillService.Level=0;Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Nested OR cannot flatten and discard remaining gates");RebirthServiceCraftSkillService.Level=10;Assert(Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Nested successful branch retained");
 RebirthCraftingProgressionRegistry.Policy.IsDisabled=true;Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Disabled recipes cannot be discovered");RebirthCraftingProgressionRegistry.Policy.IsDisabled=false;RebirthCapabilityRegistry.Definition=null;Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Missing gated capability denies");Set(own);RebirthProgressionRuntimeConfig.Mapped=false;Assert(!Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Unmapped knowledge must not be waived");RebirthProgressionRuntimeConfig.Mapped=true;RebirthCraftingProgressionRegistry.IsReady=false;Assert(Subject.EvaluateRecipeForDiscovery(p,"test").IsAllowed,"Legacy mapping supports discovery");Assert(!Subject.EvaluateRecipe(p,"test").IsAllowed,"Legacy ordinary knowledge unchanged");
 Console.WriteLine("PASS: actual recipe policy and recursive capability evaluator; exact own knowledge exemption; hard/recommended skill, unrelated knowledge, blueprint, discipline, nested AND/OR, disabled/missing capability, ordinary and legacy gates");}
}