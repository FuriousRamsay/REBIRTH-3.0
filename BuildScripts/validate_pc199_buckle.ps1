$ErrorActionPreference='Stop'
function Extract-Method($path,$signature) {
 $source=Get-Content -LiteralPath $path -Raw
 $start=$source.IndexOf($signature); if($start -lt 0){throw 'Missing production method'}
 $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 return $source.Substring($start,$end-$start)
}
$slot=Extract-Method 'Scripts/UI/XUiC_RebirthToolbeltLayout.cs' 'public static int SlotX'
$sample=Extract-Method 'Scripts/UI/XUiC_RebirthBuckleStat.cs' 'public static Color32 Sample'
$fixture=@'
using System;
public struct Color32 {public byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}}
public static class Mathf {public static float Clamp01(float v)=>Clamp(v,0,1);public static float Clamp(float v,float lo,float hi)=>Math.Min(hi,Math.Max(lo,v));}
public static class ProductionBuckle {public const int Pitch=54;
'@
Add-Type ($fixture+$slot+$sample+'}')
for($count=5;$count -le 18;$count++) {
 $left=[math]::Floor($count/2)
 for($i=0;$i -lt $count;$i++) {
  $x=[ProductionBuckle]::SlotX($i,$count)
  if($x -lt 0 -or $x+54 -gt 1206){throw 'Slot outside HUD'}
  if($i -lt $left -and $x+54 -gt 486){throw 'Left slot overlaps buckle'}
  if($i -ge $left -and $x -lt 720){throw 'Right slot overlaps buckle'}
  if($i -gt 0 -and $x -lt ([ProductionBuckle]::SlotX($i-1,$count)+54)){throw 'Slots overlap'}
 }
}
$active=[Color32]::new(186,115,115,255)
if([ProductionBuckle]::Sample(0.2,0.4,0.7,$active).r -ne 186){throw 'Current must be colored'}
if([ProductionBuckle]::Sample(0.55,0.4,0.7,$active).r -ne 106){throw 'Recoverable must be gray'}
if([ProductionBuckle]::Sample(0.9,0.4,0.7,$active).r -ne 0){throw 'Capped must be black'}
if([ProductionBuckle]::Sample(0.6,0.9,0.5,$active).r -ne 0){throw 'Current may not exceed cap'}
if([ProductionBuckle]::Sample(0.1,0,1,$active).r -ne 106){throw 'Empty uncapped stat must be gray'}
if([ProductionBuckle]::Sample(0.1,0,0,$active).r -ne 0){throw 'Fully capped stat must be black'}
Write-Output 'PASS: production slot placement for all capacities; current/unused/capped colors and boundaries.'
