$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Support/RebirthSurvivorBackpackCapacityPatches.cs'))
$stubs=@"
namespace HarmonyLib { public class HarmonyPatch:System.Attribute { public HarmonyPatch(System.Type t){} public HarmonyPatch(string s){} } public class HarmonyPrefix:System.Attribute{} }
public class Bag { public int SlotCount=72; }
public class EntityPlayer { public Bag bag=new Bag(); public bool isEntityRemote; public void UpdateBagpackSize(){} public int CalcCurrentBackpackSize(bool equipment){return 48;} }
public static class RebirthSurvivorMode { public static bool Enabled; public static bool IsEnabledForCurrentWorld(){return Enabled;} }
public static class RebirthSurvivorGearService { public static bool Known; public static int Calls; public static int Desired=60; public static bool TryGetDesiredPhysicalBagSlots(EntityPlayer p,out int d){d=Desired;return Known;} public static bool ReconcilePhysicalBagCapacity(EntityPlayer p,int d,bool notify){Calls++;return false;} }
public static class RebirthTraitGameplayModifierService { public static void SyncNativeCarryCapacityForBag(EntityPlayer p){} }
public static class CapacityMigrationFixture {
static void Check(bool value,string label){if(!value)throw new System.Exception(label);}
public static void Run(){
var p=new EntityPlayer(); int result=999;
Check(RebirthSurvivorBackpackCapacityPatches.UpdateBagpackSizePrefix(p),"native mode update preserved");
Check(RebirthSurvivorBackpackCapacityPatches.CalcCurrentBackpackSizePrefix(p,ref result)&&result==999,"native size preserved");
RebirthSurvivorMode.Enabled=true;
Check(!RebirthSurvivorBackpackCapacityPatches.UpdateBagpackSizePrefix(p)&&RebirthSurvivorGearService.Calls==0,"unknown projection cannot resize");
Check(!RebirthSurvivorBackpackCapacityPatches.CalcCurrentBackpackSizePrefix(p,ref result)&&result==72,"unknown projection retains received size");
RebirthSurvivorGearService.Known=true;
Check(!RebirthSurvivorBackpackCapacityPatches.UpdateBagpackSizePrefix(p)&&RebirthSurvivorGearService.Calls==1,"known gear uses safe reconciliation even if shrink refuses");
Check(!RebirthSurvivorBackpackCapacityPatches.CalcCurrentBackpackSizePrefix(p,ref result)&&result==60,"gear capacity replaces equipment bonus");
p.isEntityRemote=true;
Check(!RebirthSurvivorBackpackCapacityPatches.UpdateBagpackSizePrefix(p)&&RebirthSurvivorGearService.Calls==1,"remote not locally resized");
Check(!RebirthSurvivorBackpackCapacityPatches.CalcCurrentBackpackSizePrefix(p,ref result)&&result==-1,"remote sentinel preserved");
}
}
"@
Add-Type -TypeDefinition ($source+[Environment]::NewLine+$stubs)
[CapacityMigrationFixture]::Run()
Write-Output 'PASS actual installed-capacity prefixes; game, gear reconciliation and Harmony adapters doubled. No native validation.'