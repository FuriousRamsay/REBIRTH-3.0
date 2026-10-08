$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$parent=Join-Path $root 'Tools/FourthTeam/ScrollbarPaging/paired_native_abi/3ab8f5869293413b96d01d9a1841abc7/PairedNativeAbi.csproj'
$parentManifest=Join-Path ([IO.Path]::GetDirectoryName($parent)) 'manifest.json'
$candidate=Join-Path $PSScriptRoot 'Capture.candidate.txt'
$expectedCandidate='579DA416F3BB9E50F05E5E7E03A7AB756215372C82DC0CF43F60B95BD412398D'
$expectedRootDll='4582911012D8B1DB38579BA8C450C34B74C0FFE01011B5062B1DF346FFA3D993'
if((Get-FileHash $candidate).Hash-ne$expectedCandidate){throw 'Held Capture candidate changed'}
if((Get-FileHash (Join-Path $root 'RebirthUtils.dll')).Hash-ne$expectedRootDll){throw 'RootDLL baseline changed; review lineage'}
$parentSHA=(Get-FileHash $parent).Hash;$parentManifestSHA=(Get-FileHash $parentManifest).Hash
$lineage=Get-Content -LiteralPath $parentManifest -Raw|ConvertFrom-Json
foreach($record in $lineage.Sources){if((Get-FileHash -LiteralPath $record.File).Hash-ne$record.SHA){throw ('Immutable closure source changed '+$record.Relative)}}
[xml]$xml=Get-Content -LiteralPath $parent -Raw
if($xml.SelectNodes('//Exec').Count){throw 'No Exec targets allowed'}
$session=Join-Path $PSScriptRoot ('capture_native_abi/'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Force $session|Out-Null
$bin=Join-Path $session 'bin/';$obj=Join-Path $session 'obj/'
foreach($name in @('OutDir','OutputPath','BaseIntermediateOutputPath','IntermediateOutputPath','MSBuildProjectExtensionsPath')){
 $nodes=$xml.SelectNodes('//'+$name);if(!$nodes.Count){throw ('Expected native isolated output property missing '+$name)}
 foreach($n in $nodes){$n.InnerText=if($name-in@('OutDir','OutputPath')){$bin}else{$obj};$n.RemoveAttribute('Condition')}
}
foreach($n in $xml.SelectNodes('//RebirthProfilerEnableInstrumentation')){$n.InnerText='false'}
$frozen=Join-Path $session 'RebirthStationCompletionCapture.cs';[IO.File]::WriteAllBytes($frozen,[IO.File]::ReadAllBytes($candidate))
$changed=0;$chosen=@()
foreach($node in $xml.SelectNodes('//Compile[@Include]')){
 $original=[IO.Path]::GetFullPath($node.Include);$record=$lineage.Sources|Where-Object File -eq $original|Select-Object -First 1
 if(!$record){throw ('Compile absent from immutable manifest '+$original)}
 $selected=$original;$isCapture=$record.Relative.Replace('\','/')-eq'Scripts/Crafting/UI/RebirthStationCompletionCapture.cs'
 if($isCapture){$changed++;$selected=$frozen;$node.SetAttribute('Include',$frozen)}
 $chosen+=[pscustomobject]@{Relative=$record.Relative;ParentFile=$original;ParentSHA=$record.SHA;ChosenFile=$selected;ChosenSHA=(Get-FileHash -LiteralPath $selected).Hash;CaptureSubstituted=$isCapture}
}
if($changed-ne1-or$chosen.Count-ne1367){throw 'Exact immutable1367 graph and only1Capture substitution required'}
$refs=@();foreach($hint in $xml.SelectNodes('//Reference/HintPath')){
 $r=$hint.InnerText;if($r.Contains('$(')-or![IO.Path]::IsPathRooted($r)-or!(Test-Path -LiteralPath $r -PathType Leaf)){throw ('Invalid immutable reference '+$r)}
 if([IO.Path]::GetFileName($r)-eq'RebirthUtils.dll'){throw 'No baseline DLL reference permitted'}
 $refs+=[pscustomobject]@{Path=$r;SHA=(Get-FileHash -LiteralPath $r).Hash}
}
$project=Join-Path $session 'CaptureNativeAbi.csproj';$xml.Save($project)
$raw=& dotnet msbuild $project -p:Configuration=Debug -getProperty:OutDir,OutputPath,BaseIntermediateOutputPath,IntermediateOutputPath,MSBuildProjectExtensionsPath,TargetPath,DefineConstants,AssemblyName -getItem:Compile,EmbeddedResource,Using
if($LASTEXITCODE){throw 'Evaluation failed'};$eval=($raw-join "`n")|ConvertFrom-Json
foreach($name in @('OutDir','OutputPath','BaseIntermediateOutputPath','IntermediateOutputPath','MSBuildProjectExtensionsPath','TargetPath')){$value=[IO.Path]::GetFullPath($eval.Properties.$name);if(!$value.StartsWith($session+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw ('Output escaped unique child '+$name+'='+$value)}}
if($eval.Items.Compile.Count-ne1367-or$eval.Items.EmbeddedResource.Count-ne0){throw 'Evaluated closure changed'}
foreach($record in $chosen){if((Get-FileHash -LiteralPath $record.ChosenFile).Hash-ne$record.ChosenSHA-or(Get-FileHash -LiteralPath $record.ParentFile).Hash-ne$record.ParentSHA){throw 'Immutable source changed during prepare'}}
if((Get-FileHash $parent).Hash-ne$parentSHA-or(Get-FileHash $parentManifest).Hash-ne$parentManifestSHA-or(Get-FileHash $candidate).Hash-ne$expectedCandidate-or(Get-FileHash (Join-Path $root 'RebirthUtils.dll')).Hash-ne$expectedRootDll){throw 'Lineage changed during preparation'}
$manifest=Join-Path $session 'manifest.json'
[pscustomobject]@{PreparedOnly=$true;ParentProject=$parent;ParentProjectSHA=$parentSHA;ParentManifest=$parentManifest;ParentManifestSHA=$parentManifestSHA;RootDllSHA=$expectedRootDll;CaptureCandidate=$candidate;CaptureSHA=$expectedCandidate;Project=$project;ProjectSHA=(Get-FileHash $project).Hash;Evaluation=$eval.Properties;GlobalUsings=$eval.Items.Using;References=$refs;Sources=$chosen;CompileCount=1367;CaptureSubstitutions=1}|ConvertTo-Json -Depth 8|Set-Content $manifest
Write-Output ('PROJECT '+$project);Write-Output ('MANIFEST '+$manifest);Write-Output ('PROJECT SHA '+(Get-FileHash $project).Hash);Write-Output ('MANIFEST SHA '+(Get-FileHash $manifest).Hash)
$eval.Properties|ConvertTo-Json -Compress|Write-Output
Write-Output 'PREPARED ONLY; no compile/run/rootDLL overwrite'
