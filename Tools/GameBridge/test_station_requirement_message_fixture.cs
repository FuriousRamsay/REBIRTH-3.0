using System;
class Recipe {public bool Heat;public string GetName()=>"recipe";}
class Requirement {public bool Allowed=true;public bool HasRequirement(Recipe r)=>Allowed;}
class PlayerUI {public object entityPlayer;}
class XUi {public PlayerUI playerUI=new PlayerUI();}
class BaseStation {public virtual string CraftingRequirementsInvalidMessage(Recipe recipe)=>"native-fuel-reason";}
static class RebirthRecipeDiscoveryRules {public static bool Allowed=true;public static bool Allows(object p,string name)=>Allowed;}
class Evaluation {public bool IsAllowed;public string FirstMissingReason="skill-required";}
static class RebirthCapabilityService {public static bool Allowed=true;public static Evaluation EvaluateRecipeForDiscovery(object p,string name)=>new Evaluation{IsAllowed=Allowed};}
static class RebirthCookingBatch {public static bool NeedsHeat(Recipe r)=>r.Heat;}
static class Localization {public static string Get(string key)=>key;}
class Station:BaseStation {internal XUi xui=new XUi();internal Requirement toolWindow=new Requirement(),inputWindow=new Requirement(),outputWindow=new Requirement();
// PRODUCTION_CLASS
}
class Check {static void Assert(bool v,string reason){if(!v)throw new Exception(reason);}static void Main(){var s=new Station();var r=new Recipe();s.outputWindow.Allowed=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="xuiRebirthStationOutputSpaceRequired","Cold station output reason");r.Heat=true;Assert(s.CraftingRequirementsInvalidMessage(r)=="xuiRebirthStationOutputSpaceRequired","Hot station output reason");s.toolWindow.Allowed=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="ttMissingCraftingTools","Tools retain priority");s.toolWindow.Allowed=true;s.inputWindow.Allowed=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="ttMissingCraftingResources","Input retains priority");s.inputWindow.Allowed=true;s.outputWindow.Allowed=true;r.Heat=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="","Cold craft has no fuel reason");r.Heat=true;Assert(s.CraftingRequirementsInvalidMessage(r)=="native-fuel-reason","Hot craft retains native fuel message");RebirthCapabilityService.Allowed=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="skill-required","Capability reason retained");RebirthRecipeDiscoveryRules.Allowed=false;Assert(s.CraftingRequirementsInvalidMessage(r)=="xuiRebirthRecipeReadingRequired","Reading priority retained");Console.WriteLine("PASS actual station requirement messages with native-window/policy doubles: hot/cold output failure, tool/input priority, fuel distinction, capability and reading requirements");}}
