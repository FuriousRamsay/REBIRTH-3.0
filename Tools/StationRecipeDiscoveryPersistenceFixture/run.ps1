$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../.."
$m=Get-Content -Raw "$root/Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs"
$declaration=[regex]::Match($m,'public readonly Dictionary<string,RebirthStationRecipeDiscoveryRecord> RecipeDiscoveries[^;]+;').Value
$clone=[regex]::Match($m,'foreach\(var pair in RecipeDiscoveries\) copy.RecipeDiscoveries.Add\(pair.Key,pair.Value.Clone\(\)\);').Value
if(!$declaration -or !$clone){throw 'model extraction failed'}
[IO.File]::WriteAllText("$PSScriptRoot/ActualCloneSlice.cs","using System;using System.Collections.Generic;class CloneSlice{"+$declaration+"public CloneSlice Clone(){var copy=new CloneSlice();"+$clone+"return copy;}}")
$r=Get-Content -Raw "$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs"
$writeGate=($r -split "\r?\n"|Where-Object {$_ -like '*MatchesCreation(record.Progression.RecipeDiscoveries*'}) -join ''
$readGate=($r -split "\r?\n"|Where-Object {$_ -like '*MatchesCreation(progression.RecipeDiscoveries*'}) -join ''
$write=($r -split "\r?\n"|Where-Object {$_ -like '*node.Add(RebirthStationRecipeDiscoveryPersistence.Write*'}) -join ''
$a=$r.IndexOf('        Dictionary<string,RebirthStationRecipeDiscoveryRecord> discoveries;');$b=$r.IndexOf('        XElement knowledge=', $a)
if(!$writeGate -or !$readGate -or !$write -or $a -lt 0 -or $b -le $a){throw 'repository hook boundary'}
$read=$r.Substring($a,$b-$a)
[IO.File]::WriteAllText("$PSScriptRoot/ActualRepositorySlices.cs",'using System;using System.IO;using System.Collections.Generic;using System.Xml.Linq;class Origin{public string CreationId;}class ShellRecord{public CloneSlice Progression;public Origin Origin;}static class RepoSlices{public static void CheckWrite(ShellRecord record){'+$writeGate+'}public static bool CheckRead(CloneSlice progression,Origin origin,out string error){error=null;'+$readGate+'return true;}public static XElement Write(CloneSlice state,string ownerKey){var node=new XElement("progression");'+$write+'return node;}public static bool Read(XElement node,string ownerKey,out CloneSlice state,out string error){state=new CloneSlice();'+$read+'return true;}}')
dotnet run --project "$PSScriptRoot/Fixture.csproj"
if($LASTEXITCODE){throw 'fixture failed'}
