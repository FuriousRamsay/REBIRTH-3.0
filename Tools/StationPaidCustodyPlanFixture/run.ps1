$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$s=Get-Content (Join-Path $root 'Scripts/Crafting/UI/RebirthStationGridIngredients.cs') -Raw
$a=$s.IndexOf('public static bool IsSameStackSnapshot');$b=$s.IndexOf('    /// <summary>',$a)
if($a-lt0-or$b-lt$a){throw 'Comparator extraction failed'}
Set-Content (Join-Path $PSScriptRoot 'ActualComparator.cs') ('using System;using System.IO;static class ActualComparator{'+$s.Substring($a,$b-$a)+'}')
dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj')
if($LASTEXITCODE-ne0){throw 'Fixture failed'}
