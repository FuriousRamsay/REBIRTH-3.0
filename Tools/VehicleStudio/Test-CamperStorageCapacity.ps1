$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
Add-Type -Path (Join-Path $PSScriptRoot '..\..\Scripts\Vehicles\Camper\RebirthCamperStorageCapacity.cs')
$examples=@(50,20,0,0,0,0,0,0,0,0)
if([RebirthCamperStorageCapacity]::Total($examples) -ne 70){throw 'Safe plus fridge must provide exactly70 slots.'}
if([RebirthCamperStorageCapacity]::Total(@(50,50,50,50,50,50,50,50,50,50)) -ne 500){throw 'Ten large containers must not hit a false255-slot limit.'}
if([RebirthCamperStorageCapacity]::Total(@(0,0,0,0,0,0,0,0,0,0)) -ne 0){throw 'Empty-container baseline must be zero unless explicitly supplied.'}
if([RebirthCamperStorageCapacity]::Total($examples,30) -ne 100){throw 'Optional native baseline must be explicit.'}
$destinations=$null
if(-not [RebirthCamperStorageCapacity]::TryPlanCargoPositions(3,@(0,69,2),[ref]$destinations)){throw 'Sparse cargo should fit after safe compaction.'}
if(($destinations -join ',') -ne '0,1,2'){throw 'Existing low positions must remain; move high cargo into a free low slot.'}
if([RebirthCamperStorageCapacity]::TryPlanCargoPositions(2,@(0,1,69),[ref]$destinations)){throw 'Unsafe shrink must be rejected.'}
if(-not [RebirthCamperStorageCapacity]::TryPlanCargoPositions(0,@(),[ref]$destinations)){throw 'Empty cargo must permit zero capacity.'}
$rejected=$false
try{[RebirthCamperStorageCapacity]::Total(@(32767,1,0,0,0,0,0,0,0,0)) | Out-Null}catch{$rejected=$true}
if(-not $rejected){throw 'Native serialization overflow must be rejected.'}
Write-Output 'PASS: exact additive capacity,500 slots, explicit baseline, sparse safe shrink, overflow rejection and zero capacity.'
