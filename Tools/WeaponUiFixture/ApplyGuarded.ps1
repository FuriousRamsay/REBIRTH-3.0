param([Parameter(Mandatory=$true)][string]$TargetRoot)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$target=(Resolve-Path -LiteralPath $TargetRoot).Path
$entries=Get-Content "$root/_Documentation/WeaponUi_20261029/source-hashes.json" -Raw|ConvertFrom-Json
# Validate every source and destination before writing any file.
foreach($e in $entries){
 if((Get-FileHash -LiteralPath "$root/$($e.path)").Hash -ne $e.proposal){throw "Package source changed: $($e.path)"}
 $dest=Join-Path $target $e.path
 if($e.baseline -eq 'ABSENT'){if(Test-Path -LiteralPath $dest){throw "Target already exists: $($e.path)"}}
 elseif(!(Test-Path -LiteralPath $dest)-or(Get-FileHash -LiteralPath $dest).Hash -ne $e.baseline){throw "Baseline drift: $($e.path); merge/review required"}
}
foreach($e in $entries){$dest=Join-Path $target $e.path;New-Item -ItemType Directory -Force (Split-Path $dest)|Out-Null;Copy-Item -LiteralPath "$root/$($e.path)" -Destination $dest}
& "$PSScriptRoot/AppendLocalization.ps1" -ConfigRoot (Join-Path $target 'Config')
'Applied guarded source/XML package. Build and native validation remain separate.'
