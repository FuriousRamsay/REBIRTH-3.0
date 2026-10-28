param()
$ErrorActionPreference = 'Stop'
$patternRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$patternSource = Get-Content -LiteralPath (Join-Path $patternRoot 'Scripts/Vehicles/Camper/RebirthCamperPattern.cs') -Raw
$patternDoubles = @"
using System;
public sealed class EntityVehicle { public Vehicle vehicle; public bool IsCamper=true; }
public sealed class Vehicle { public ItemValue Value; public ItemValue GetUpdatedItemValue()=>Value; }
public sealed class ItemClass { public string Name; public string GetItemName()=>Name; }
public sealed class ItemValue {
 public ItemClass ItemClass; public ItemValue[] Cosmetics=new ItemValue[0]; public bool Empty;
 public int CosmeticModCount=>Cosmetics.Length; public ItemValue GetCosmeticMod(int slot)=>Cosmetics[slot];
 public bool IsEmpty()=>Empty;
}
public static class RebirthCamperRadial { public static bool IsCamper(EntityVehicle v)=>v!=null&&v.IsCamper; }
public static class CamperPatternFixture {
 private static void Check(EntityVehicle v,string expected,string label) {
  if(RebirthCamperPattern.Current(v)!=expected)throw new Exception(label);
 }
 public static string Run() {
  Check(null,"none","Null vehicle");
  var item=new ItemValue();var v=new EntityVehicle{vehicle=new Vehicle{Value=item}};
  Check(v,"none","No cosmetic");
  item.Cosmetics=new[]{new ItemValue{ItemClass=new ItemClass{Name="modDyePurple"}}};
  Check(v,"none","Native dye uses original material");
  item.Cosmetics[0].ItemClass.Name="modVehiclePatternDisco";
  Check(v,"disco","Native Disco installed");
  item.Cosmetics[0].Empty=true;Check(v,"none","Removed modifier");
  item.Cosmetics[0].Empty=false;item.Cosmetics[0].ItemClass.Name="MODVEHICLEPATTERNDISCO";
  Check(v,"none","Exact definition only");
  item.Cosmetics[0].ItemClass.Name="modVehiclePatternDisco";v.IsCamper=false;
  Check(v,"none","Other vehicle excluded");
  return "PASS: seven actual-source recognition cases";
 }
}
"@
Add-Type -TypeDefinition ($patternDoubles + [Environment]::NewLine + ($patternSource -replace '(?m)^using System;\s*',''))
[CamperPatternFixture]::Run()
# Native GetBuyPrice truncates post-markup float before sandbox adjustment.
$patternValue = [single]166.66667
$patternPrice = [int][Math]::Truncate([single]($patternValue * [single]3))
if($patternPrice -ne 500){throw "Baseline native price mismatch: $patternPrice"}
'PASS: baseline default markup price 500'
