$ErrorActionPreference='Stop'
if(Get-Process -Name '7DaysToDie*' -ErrorAction SilentlyContinue){throw 'Close game before offline validation'}
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Provenance/RebirthPlacedOriginalGuards.cs'))
$start=$source.IndexOf('internal static class RebirthPlacedOriginalActivationPatch')
if($start -lt 0){throw 'Activation class missing'}
$start=$source.IndexOf('private static IEnumerable<MethodBase> TargetMethods()',$start)
$open=$source.IndexOf('{',$start);$end=$open+1;$depth=1
while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
if($start -lt 0 -or $depth -ne 0){throw 'Target method extraction failed'}
$method=$source.Substring($start,$end-$start).Replace('private static','internal static')
$types=@('BlockLight','BlockPowered','BlockPowerSource','BlockSwitch','BlockRanged','BlockLauncher','BlockPoweredTrap','BlockTimerRelay','BlockPressurePlate','BlockMotionSensor','BlockElectricWire','BlockSolarPanel','BlockGenerator','BlockBatteryBank','BlockBladeTrap')
$definitions=($types|ForEach-Object {
 if($_ -in @('BlockLight','BlockPowered')){"class $_ : Block {public override bool OnBlockActivated(string command,WorldBase world,Vector3i position,BlockValue value,EntityPlayerLocal player)=>true;}"}
 else{"class $_ : Block {}"}
}) -join "`n"
$code=@"
using System;using System.Linq;using System.Reflection;using System.Collections.Generic;
class WorldBase {} struct Vector3i {} struct BlockValue {} class EntityPlayerLocal {}
class Block {public virtual bool OnBlockActivated(string command,WorldBase world,Vector3i position,BlockValue value,EntityPlayerLocal player)=>true;public bool OnBlockActivated(int wrongSignature)=>true;}
$definitions
static class ActualTargets {$method}
public static class DeclaredTargetFixture {
 public static string Run(){var targets=ActualTargets.TargetMethods().ToArray();
  if(targets.Length!=3||targets.Select(x=>x.DeclaringType).Distinct().Count()!=3||targets.Any(x=>x.GetParameters().Length!=5))throw new Exception("declared override target selection changed");
  if(!targets.Any(x=>x.DeclaringType==typeof(Block))||!targets.Any(x=>x.DeclaringType==typeof(BlockLight))||!targets.Any(x=>x.DeclaringType==typeof(BlockPowered)))throw new Exception("declared target omitted");
  return "PASS actual declared activation selector: base and declared overrides included once, inherited-only types and wrong overload excluded. Explicit reflection type adapters; native Harmony execution NOT RUN.";
 }
}
"@
Add-Type -TypeDefinition $code
[DeclaredTargetFixture]::Run()
