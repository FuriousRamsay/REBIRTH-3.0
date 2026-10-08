$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
function Member([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a -lt 0){throw "Missing $sig"};$b=$s.IndexOf('{',$a);$i=$b+1;$d=1;while($d){if($s[$i] -eq '{'){$d++};if($s[$i] -eq '}'){$d--};$i++};$s.Substring($a,$i-$a)}
$surface=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/XUiC_RebirthQuestTurnInWorkspace.cs'))
$layout=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthScreenLayout.cs'))
$code='using System;using System.Runtime.CompilerServices;using UnityEngine;'+[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'SurfaceAdapters.cs'))
$fields=([regex]::Matches($surface,'private Vector2i last[^;]+;')|ForEach-Object {$_.Value}) -join ''
$code+='public class Surface{'+$fields+'public XUi xui;public View ViewComponent=new View();public void Tick(){Layout();}'+(Member $surface 'private void Layout()')+'}'
$code+='public static class RebirthScreenLayout{'+(Member $layout 'private sealed class HudBounds')+'private static readonly ConditionalWeakTable<XUi,HudBounds> hudBounds=new ConditionalWeakTable<XUi,HudBounds>();'+(Member $layout 'public static void GetScreenBounds(')+'}'
$out=Join-Path $PSScriptRoot out;[IO.File]::WriteAllText((Join-Path $out 'SurfaceCheck.cs'),$code)
$refs=@('mscorlib','System','System.Core')|ForEach-Object {'/r:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/'+$_+'.dll'}
& 'C:/Program Files/dotnet/dotnet.exe' 'C:/Program Files/dotnet/sdk/9.0.301/Roslyn/bincore/csc.dll' /nologo /target:exe /main:SurfaceCheck ('/out:'+(Join-Path $out 'SurfaceCheck.exe')) @refs (Join-Path $out 'SurfaceCheck.cs')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& (Join-Path $out 'SurfaceCheck.exe')|Tee-Object (Join-Path $out 'SURFACE_RESULT.txt');if($LASTEXITCODE -ne 0){throw 'Observation failed'}