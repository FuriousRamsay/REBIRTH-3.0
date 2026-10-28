param([Parameter(Mandatory=$true)][string]$ProductionRoot,[string]$GameRoot)
$ErrorActionPreference='Stop'
$packageRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$m=Get-Content (Join-Path $PSScriptRoot 'source-manifest.json') -Raw | ConvertFrom-Json
if(!$GameRoot){$GameRoot=Split-Path (Split-Path $ProductionRoot -Parent) -Parent}
if((Get-FileHash -LiteralPath (Join-Path $GameRoot '7DaysToDie_Data/Managed/Assembly-CSharp.dll')).Hash -ne $m.nativeAssemblySha256){throw 'Native compatibility contract differs'}
foreach($ref in $m.references.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $ProductionRoot $ref.Name)).Hash -ne $ref.Value){throw 'Referenced pickup policy differs; review required'}}
$pending=@()
foreach($entry in $m.sources){
 $source=Join-Path $packageRoot $entry.path;$target=Join-Path $ProductionRoot $entry.path
 if((Get-FileHash -LiteralPath $source).Hash -ne $entry.proposed){throw 'Package source differs'}
 if(Test-Path -LiteralPath $target){$actual=(Get-FileHash -LiteralPath $target).Hash;if($actual -eq $entry.proposed){continue};if(!$entry.baseline -or $actual -ne $entry.baseline){throw 'Target differs; no files copied'}}
 elseif($entry.baseline){throw 'Existing target missing; no files copied'}
 $pending+=@{source=$source;target=$target;expected=$entry.proposed;path=$entry.path}
}
foreach($entry in ($pending | Sort-Object @{Expression={if($_.path.EndsWith('.cs')){0}else{1}}})){
 $parent=Split-Path $entry.target -Parent;if(!(Test-Path -LiteralPath $parent)){New-Item -ItemType Directory -Path $parent -Force | Out-Null}
 Copy-Item -LiteralPath $entry.source -Destination $entry.target
 if((Get-FileHash -LiteralPath $entry.target).Hash -ne $entry.expected){throw 'Installed hash differs'}
}
if($pending.Count -eq 0){'Already integrated in source'}else{'Source integrated only; lead full build and installed-class/XML verification required before user reload'}
