$ErrorActionPreference='Stop'
$testRoot=Join-Path $env:TEMP ('rebirth-legacy-pallet-guard-'+[guid]::NewGuid().ToString('N'))
$prodRoot='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die/Mods/zzz_REBIRTH__3_0'
$gameRoot=Split-Path (Split-Path $prodRoot -Parent) -Parent
$m=Get-Content (Join-Path $PSScriptRoot 'source-manifest.json') -Raw | ConvertFrom-Json
foreach($ref in $m.references.PSObject.Properties){$dest=Join-Path $testRoot $ref.Name;New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $prodRoot $ref.Name) -Destination $dest}
$xml=Join-Path $testRoot 'Config/_LootConversions/blocks.xml';$cs=Join-Path $testRoot 'Scripts/BlockPickup/BlockRebirthLegacyMilitaryPalletBase.cs'
New-Item -ItemType Directory -Path (Split-Path $xml -Parent) -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'blocks.xml.baseline.txt') $xml
& (Join-Path $PSScriptRoot 'install-guarded.ps1') -ProductionRoot $testRoot -GameRoot $gameRoot
& (Join-Path $PSScriptRoot 'install-guarded.ps1') -ProductionRoot $testRoot -GameRoot $gameRoot
Copy-Item (Join-Path $PSScriptRoot 'blocks.xml.baseline.txt') $xml
Set-Content $cs 'foreign source'
$before=(Get-FileHash $xml).Hash
try{& (Join-Path $PSScriptRoot 'install-guarded.ps1') -ProductionRoot $testRoot -GameRoot $gameRoot;throw 'Guard failed'}catch{if($_ -notmatch 'Target differs'){throw};if((Get-FileHash $xml).Hash -ne $before){throw 'Partial XML write'};'PASS foreign new source refused before XML write'}
$source=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Scripts/BlockPickup/BlockRebirthLegacyMilitaryPalletBase.cs'
Copy-Item $source $cs
& (Join-Path $PSScriptRoot 'install-guarded.ps1') -ProductionRoot $testRoot -GameRoot $gameRoot
Add-Content (Join-Path $testRoot 'Scripts/BlockPickup/RebirthBlockPickupClassifier.cs') '// different policy'
try{& (Join-Path $PSScriptRoot 'install-guarded.ps1') -ProductionRoot $testRoot -GameRoot $gameRoot;throw 'Guard failed'}catch{if($_ -notmatch 'Referenced pickup policy differs'){throw};'PASS unknown policy reference refused'}
'PASS 5 guard cases in disposable temp tree; no production writes'
