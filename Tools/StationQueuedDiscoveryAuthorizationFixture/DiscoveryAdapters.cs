using System;using System.Xml.Linq;using System.Collections.Generic;
static class RebirthCookingBatch{public static bool Cooking;public static bool IsBatch(Recipe r)=>Cooking;}
static class RebirthCraftingProgressionRegistry{public static string SemanticHash=new string('d',64);}
static class RebirthStationLiveAccess{public static bool TryResolve(EntityPlayer p,Vector3i pos,string c,out TileEntityWorkstation s,out RebirthWorldCharacterRecord o){s=null;o=null;return false;}}
static class RebirthStationDiscoveryCanonicalRecipe{
 public static bool Valid=true;public static string Canonical="nativeRecipe";public class Resolution{public string CanonicalRecipe,DefinitionId,JobId,KnowledgeId="knowledge";public bool Matches(string r,string d)=>r==CanonicalRecipe&&d==DefinitionId;}
 public static bool TryResolve(Recipe r,IList<Recipe> definitions,out Resolution resolution){resolution=new(){CanonicalRecipe=Canonical,DefinitionId=new string('A',64),JobId=r.Name};return Valid;}}
static class RebirthStationDiscoveryScope{public static bool Unlocked=true;public static bool IsUnlockedForDiscovery(Recipe r,EntityPlayer p)=>Unlocked;}
static class RebirthRecipeDiscoveryRules{public static bool AllowsDiscovery=true;public static bool Allows(EntityPlayer p,string r)=>AllowsDiscovery;}
class RebirthCapabilityEvaluation{public bool IsAllowed;public string FirstMissingReason="skill";}
static class RebirthCapabilityService{public static bool Skills=true,Known;public static int DiscoveryCalls,NormalCalls;public static Action OnDiscovery;public static RebirthCapabilityEvaluation EvaluateRecipe(EntityPlayer p,string r){NormalCalls++;return new(){IsAllowed=Skills&&Known};}public static RebirthCapabilityEvaluation EvaluateRecipeForDiscovery(EntityPlayer p,string r){DiscoveryCalls++;var call=OnDiscovery;OnDiscovery=null;call?.Invoke();return new(){IsAllowed=Skills};}}
