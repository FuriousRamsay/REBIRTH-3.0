param([switch]$AllWritersHeld)
$ErrorActionPreference='Stop'
if(!$AllWritersHeld){throw 'Coordinator must confirm ALL production writers coherent HOLD before snapshot. This script never compiles.'}
if(Get-Process -Name '7DaysToDie','7DaysToDieServer' -ErrorAction SilentlyContinue){throw 'Game/server must be absent'}
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project=Join-Path $root 'RebirthUtils.csproj'
$projectHash=(Get-FileHash $project).Hash
$rootDll=Join-Path $root 'RebirthUtils.dll';$rootDllHash=if(Test-Path $rootDll){(Get-FileHash $rootDll).Hash}else{''}
$raw=& dotnet msbuild $project -p:Configuration=Debug -getItem:Compile,EmbeddedResource -getProperty:DefineConstants,RebirthGameRoot,RebirthManagedRoot
if($LASTEXITCODE){throw 'Native project evaluation failed'}
$evaluated=($raw-join "`n")|ConvertFrom-Json
$managed=[IO.Path]::GetFullPath($evaluated.Properties.RebirthManagedRoot)
$game=[IO.Path]::GetFullPath($evaluated.Properties.RebirthGameRoot)
$session=Join-Path $PSScriptRoot ('Shadow/'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $session|Out-Null
$sourceDir=Join-Path $session 'source';New-Item -ItemType Directory -Force $sourceDir|Out-Null
$substitutions=@{
'Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'='RuntimeModels.candidate.txt'
'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'='Repository.candidate.txt'
'Scripts/Survivor/Persistence/RebirthStationCompletionObservation.cs'='Observation.candidate.txt'
'Scripts/Survivor/Persistence/RebirthStationCompletionExpectationProjection.cs'='Projection.candidate.txt'
}
$approvedOriginalHashes=@{
'Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'='A3079CBB4099F839747B7AB7FB5F495DF0A3FC65C9044600F1DA26B8488A067D'
'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'='7382F091774E36DD759E3F3FA2AF05FFE02226EFC3434B0FB363D073B7279E8F'
'Scripts/Survivor/Persistence/RebirthStationCompletionObservation.cs'='D396F1BFB1303807BC986AC00631B9B8D00D158B815130954DE2D0EBD51F51DA'
'Scripts/Survivor/Persistence/RebirthStationCompletionExpectationProjection.cs'='53C3530A29243B83CC5AC0289959580377086EECB2C13E5CFCD4EBE4B6DF537E'
}
foreach($relative in $approvedOriginalHashes.Keys){if((Get-FileHash (Join-Path $root $relative)).Hash-ne$approvedOriginalHashes[$relative]){throw ('Stale candidate provenance: current original changed '+$relative)}}
$manifest=[Collections.Generic.List[object]]::new()
[xml]$xml=[IO.File]::ReadAllText($project)
# Preserve exact native references/globalusings/constants/targets; replace evaluated source/resource selections only.
foreach($node in @($xml.SelectNodes('//Compile|//EmbeddedResource|//None'))){$node.ParentNode.RemoveChild($node)|Out-Null}
$property=$xml.CreateElement('PropertyGroup');$xml.Project.AppendChild($property)|Out-Null
$properties=@{EnableDefaultItems='false';BaseIntermediateOutputPath=(Join-Path $session 'obj/');IntermediateOutputPath=(Join-Path $session 'obj/Debug/');OutDir=(Join-Path $session 'bin/');OutputPath=(Join-Path $session 'bin/');RebirthGameRoot=$game;RebirthManagedRoot=$managed;RebirthProfilerEnableInstrumentation='false'}
foreach($name in $properties.Keys){$n=$xml.CreateElement($name);$n.InnerText=$properties[$name];$property.AppendChild($n)|Out-Null}
$rootProp=$xml.SelectSingleNode('//RebirthGameRoot');$rootProp.InnerText=$game;$rootProp.RemoveAttribute('Condition');$managedProp=$xml.SelectSingleNode('//RebirthManagedRoot');$managedProp.InnerText=$managed;$managedProp.RemoveAttribute('Condition')
$items=$xml.CreateElement('ItemGroup');$xml.Project.AppendChild($items)|Out-Null
foreach($kind in @('Compile','EmbeddedResource')){foreach($item in $evaluated.Items.$kind){
 $original=[IO.Path]::GetFullPath($item.FullPath);$relative=[IO.Path]::GetRelativePath($root,$original).Replace('\','/')
 if($relative.StartsWith('../')-or$relative.StartsWith('Tools/')){throw 'Unexpected evaluated source outside native graph'}
 $originalHash=(Get-FileHash $original).Hash;$copy=$original;$candidate=''
 if($substitutions.ContainsKey($relative)){if($originalHash-ne$approvedOriginalHashes[$relative]){throw ('Stale original before substitution '+$relative)};$candidate=$substitutions[$relative];$copy=Join-Path $PSScriptRoot $candidate}
 $dest=Join-Path $sourceDir $relative;New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($dest))|Out-Null
 [IO.File]::WriteAllBytes($dest,[IO.File]::ReadAllBytes($copy));$copyHash=(Get-FileHash $copy).Hash
 if((Get-FileHash $original).Hash-ne$originalHash-or(Get-FileHash $dest).Hash-ne$copyHash){throw 'Writer changed source during snapshot'}
 $manifest.Add([pscustomobject]@{Kind=$kind;Relative=$relative;Original=$original;OriginalSHA=$originalHash;Candidate=$candidate;CopySource=$copy;SnapshotSHA=$copyHash})
 $node=$xml.CreateElement($kind);$node.SetAttribute('Include','source/'+$relative);$items.AppendChild($node)|Out-Null
 foreach($metadata in @('Link','LogicalName','ManifestResourceName','DependentUpon','Generator','LastGenOutput','CustomToolNamespace','WithCulture','Culture','Type','Access','SubType','CopyToOutputDirectory','CopyToPublishDirectory')){if($item.PSObject.Properties[$metadata] -and $item.$metadata){$m=$xml.CreateElement($metadata);$m.InnerText=[string]$item.$metadata;$node.AppendChild($m)|Out-Null}}
}}
if(@($manifest|Where-Object Candidate).Count-ne4){throw 'Exact four candidate substitutions required'}
foreach($hint in $xml.SelectNodes('//Reference/HintPath')){
 $resolved=$hint.InnerText.Replace('$(RebirthManagedRoot)',$managed).Replace('$(RebirthGameRoot)',$game)
 if($resolved.Contains('$(')){throw ('Unexpanded native reference '+$resolved)}
 if(![IO.Path]::IsPathRooted($resolved)){$resolved=Join-Path $root $resolved}
 $resolved=[IO.Path]::GetFullPath($resolved)
 if(!(Test-Path -LiteralPath $resolved -PathType Leaf)){throw ('Missing native reference '+$resolved)}
 $hint.InnerText=$resolved
}
$profiler=$xml.SelectSingleNode('//Target[@Name="RunRebirthProfilerInstrumentation"]');if($profiler){$profiler.ParentNode.RemoveChild($profiler)|Out-Null}
$shadowProject=Join-Path $session 'Shadow.csproj';$xml.Save($shadowProject)
# All files must still be the exact originals used for the snapshot after graph capture.
foreach($record in $manifest){if((Get-FileHash $record.Original).Hash-ne$record.OriginalSHA-or(Get-FileHash $record.CopySource).Hash-ne$record.SnapshotSHA){throw 'Writer changed coherent snapshot; do not compile'}}
if((Get-FileHash $project).Hash-ne$projectHash){throw 'Native project changed during snapshot'}
$refManifest=foreach($hint in $xml.SelectNodes('//Reference/HintPath')){$p=$hint.InnerText;if(![IO.Path]::IsPathRooted($p)-or$p.Contains('$(')-or!(Test-Path -LiteralPath $p -PathType Leaf)){throw ('Invalid required reference '+$p)};[pscustomobject]@{Path=$p;SHA=(Get-FileHash -LiteralPath $p).Hash}}
[pscustomobject]@{NativeProjectSHA=$projectHash;RootDllSHA=$rootDllHash;Defines=$evaluated.Properties.DefineConstants;NativeGameRoot=$game;NativeManagedRoot=$managed;Sources=$manifest;References=$refManifest}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $session 'manifest.json')
if($rootDllHash-and(Get-FileHash $rootDll).Hash-ne$rootDllHash){throw 'Root DLL changed during snapshot'}
Write-Output ('READY (NOT COMPILED): '+$shadowProject)
Write-Output ('CompileCount='+@($evaluated.Items.Compile).Count+' Resources='+@($evaluated.Items.EmbeddedResource).Count+' Substitutions=4')
Write-Output ('After separate root authorization: dotnet build "'+$shadowProject+'" -c Debug --nologo')
