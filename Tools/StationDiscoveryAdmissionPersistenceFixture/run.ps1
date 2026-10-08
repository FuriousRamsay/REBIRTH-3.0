$ErrorActionPreference='Stop'
$root=Resolve-Path "$PSScriptRoot/../.."
$m=[IO.File]::ReadAllText("$root/Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs")
$declaration=[regex]::Match($m,'public readonly Dictionary<string,RebirthStationDiscoveryAdmissionBinding> StationDiscoveryAdmissions[^;]+;').Value
$clone=[regex]::Match($m,'foreach\(var pair in StationDiscoveryAdmissions\) copy.StationDiscoveryAdmissions.Add\(pair.Key,pair.Value.Clone\(\)\);').Value
if(!$declaration -or !$clone){throw 'Actual admission model/clone boundaries missing'}
[IO.File]::WriteAllText("$PSScriptRoot/ActualCloneSlice.cs",'using System;using System.Collections.Generic;class CloneSlice{'+$declaration+'public CloneSlice Clone(){var copy=new CloneSlice();'+$clone+'return copy;}}')
$r=[IO.File]::ReadAllText("$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs")
$lines=$r -split '\r?\n'
$writer=@($lines|Where-Object {$_ -like '*progressionNode.Add(RebirthStationDiscoveryAdmissionPersistence.Write(record.Progression.StationDiscoveryAdmissions*'})
$reader=@($lines|Where-Object {$_ -like '*if(!RebirthStationDiscoveryAdmissionPersistence.TryRead(root.Element("progression")*'})
$populate=@($lines|Where-Object {$_ -like '*foreach(var pair in discoveryAdmissions)progression.StationDiscoveryAdmissions.Add*'})
if($writer.Count -ne 1 -or $reader.Count -ne 1 -or $populate.Count -ne 1){throw 'Expected unique actual outer hooks'}
$body='using System;using System.IO;using System.Xml.Linq;class Origin{public string CreationId;}class ShellRecord{public CloneSlice Progression;public Origin Origin;public string StablePlayerKey;}static class ActualRepositoryHooks{static string A(XElement n,string key)=>(string)n.Attribute(key);public static XElement Write(ShellRecord record){var progressionNode=new XElement("progression");'+$writer[0]+'return new XElement("character",new XAttribute("stablePlayerKey",record.StablePlayerKey),progressionNode);}public static bool Read(XElement root,Origin origin,out CloneSlice published,out string error){published=null;error=null;var progression=new CloneSlice();'+$reader[0]+$populate[0]+'published=progression;return true;}}'
[IO.File]::WriteAllText("$PSScriptRoot/ActualRepositoryHooks.cs",$body)
dotnet run --project "$PSScriptRoot/Fixture.csproj" -c Release
if($LASTEXITCODE -ne 0){throw 'Discovery admission integrated-hook fixture failed'}
