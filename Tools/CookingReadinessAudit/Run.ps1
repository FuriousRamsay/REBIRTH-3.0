$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText("$PSScriptRoot/CookingWorkspace.before_preflight_20261006.cs.txt")
$line=@($source -split "`n"|Where-Object{$_.Contains('Text("outcomeStatus",')})[0]
$full=[regex]::Match($source,'if\((inputs\.Any\(i=>!RebirthCookingItemStats\.FullIngredient\(i\.itemValue\)\))\)').Groups[1].Value
$duplicates=[regex]::Match($source,'if\((slots\.Any\(s=>!s\.ItemStack\.IsEmpty\(\)&&s\.ItemStack\.count<batch\).*?)\)\{status=').Groups[1].Value
if(!$full -or !$duplicates){throw 'Cook extraction changed'}
$code=@"
using System;using System.Linq;using System.Collections.Generic;
public class ItemValue { public int type;public bool Full=true; }
public class ItemStack{public ItemValue itemValue=new ItemValue();public int count=3;public bool IsEmpty(){return count<=0;}}
public class Slot{public ItemStack ItemStack=new ItemStack();}
public class Recipe{}
public class Prep{public bool IsPreparing;}
public class Station{public bool CraftingRequirementsValid(Recipe r){return true;}public string CraftingRequirementsInvalidMessage(Recipe r){return "native failure";}}
public static class Localization{public static string Get(string s){return s;}}
public static class RebirthSurvivorUiText{public static string L(string k,string f){return f;}}
public static class RebirthCookingItemStats{public static bool FullIngredient(ItemValue i){return i.Full;}}
public static class ActualReadiness{
public static string status,missing;public static Prep Preparation=new Prep();public static Station station=new Station();public static Recipe result=new Recipe();public static bool loadedIngredients=true,milling;public static int batch=3;public static Slot[] slots=new[]{new Slot()};public static List<ItemStack> inputs;static void Text(string k,string v){status=v;}
public static string Render(){ $line return status;}
public static bool InputAdmission(){ inputs=slots.Select(s=>s.ItemStack).ToList();return !($full) && !($duplicates); }
public static bool LibraryPending,Submitting,PreparingRequest,MillingExact=true;
public static string CandidatePreflight(){
 if(LibraryPending)return "xuiRebirthLibraryTransferPending";
 if(Preparation.IsPreparing||PreparingRequest)return "xuiRebirthCookingPreparing";
 if(Submitting)return "xuiRebirthCookingSubmitting";
 if(result==null)return "xuiRebirthCookingAddIngredients";
 if(!loadedIngredients)return "xuiRebirthCookingIngredientsNotLoaded";
 inputs=slots.Where(s=>!s.ItemStack.IsEmpty()).Select(s=>s.ItemStack).ToList();
 if(inputs.Count==0)return "xuiRebirthCookingPullOrAddFirst";
 if($full)return "xuiRebirthCookingFullContainersOnly";
 if($duplicates)return "xuiRebirthCookingOneSlotPerIngredient";
 if(milling&&!MillingExact)return "xuiRebirthStationExactMaterialsRequired";
 return null;
}
}
"@
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){$script:count++;if(!$ok){throw $label}}
Check ([ActualReadiness]::InputAdmission()) 'baseline actual Cook input guards admit';Check ([ActualReadiness]::Render().Contains('Ready to cook')) 'baseline actual Render ready'
[ActualReadiness]::slots[0].ItemStack.itemValue.Full=$false;Check (![ActualReadiness]::InputAdmission()) 'partial container actual Cook refuses';Check ([ActualReadiness]::Render().Contains('Ready to cook')) 'DEFECT actual Render still ready partial container'
[ActualReadiness]::slots[0].ItemStack.itemValue.Full=$true;[ActualReadiness]::slots[0].ItemStack.count=2;Check (![ActualReadiness]::InputAdmission()) 'insufficient batch actual Cook refuses';Check ([ActualReadiness]::Render().Contains('Ready to cook')) 'DEFECT actual Render still ready insufficient batch'
[ActualReadiness]::slots=@([Slot]::new(),[Slot]::new());Check (![ActualReadiness]::InputAdmission()) 'duplicate ingredient type actual Cook refuses';Check ([ActualReadiness]::Render().Contains('Ready to cook')) 'DEFECT actual Render still ready duplicate ingredient'
Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthCookingOneSlotPerIngredient') 'candidate duplicate exact blocker'
[ActualReadiness]::slots=@([Slot]::new());Check ($null -eq [ActualReadiness]::CandidatePreflight()) 'candidate normal admission ready'
[ActualReadiness]::slots[0].ItemStack.itemValue.Full=$false;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthCookingFullContainersOnly') 'candidate partial drink blocker';[ActualReadiness]::slots[0].ItemStack.itemValue.Full=$true
[ActualReadiness]::slots[0].ItemStack.count=2;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthCookingOneSlotPerIngredient') 'candidate servings blocker';[ActualReadiness]::slots[0].ItemStack.count=3
[ActualReadiness]::LibraryPending=$true;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthLibraryTransferPending') 'candidate library custody blocker';[ActualReadiness]::LibraryPending=$false
[ActualReadiness]::Submitting=$true;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthCookingSubmitting') 'candidate original request pending';[ActualReadiness]::Submitting=$false
[ActualReadiness]::PreparingRequest=$true;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthCookingPreparing') 'candidate preparation request pending';[ActualReadiness]::PreparingRequest=$false
[ActualReadiness]::milling=$true;[ActualReadiness]::MillingExact=$false;Check ([ActualReadiness]::CandidatePreflight() -eq 'xuiRebirthStationExactMaterialsRequired') 'candidate failed native exact-plan adapter';[ActualReadiness]::MillingExact=$true;Check ($null -eq [ActualReadiness]::CandidatePreflight()) 'candidate valid native exact-plan adapter'
"PASS $count historical before-fix extracted display and Cook predicate observations; explicit slot/native station adapters, no game or paid submission."