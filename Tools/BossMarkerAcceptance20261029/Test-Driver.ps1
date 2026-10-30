param([string]$ScratchDirectory=(Join-Path $env:TEMP ('boss-marker-driver-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $ScratchDirectory -Force|Out-Null
$mock=@'
param([string]$SessionPath,[Parameter(Position=0)][string]$Command,[Parameter(ValueFromRemainingArguments)][string[]]$Rest)
if($Command -eq 'call'){$o=@{ok=$true;world=@{gameName='CodexTest';gameWorld='Fixture';paused=$false};player=@{dead=$false}}}
elseif($Command -eq 'entities'){$o=@{ok=$true;count=0;entities=@()}}
elseif($Command -eq 'console' -and $Rest[0] -eq 'rbevents status'){$o=@{ok=$true;hadError=$false;results=@(@{output=@();log=@(@{msg=' event state=Completed reward=True rewardPlaced=True'})})}}
elseif($Command -eq 'console' -and $Rest[0] -eq 'rbsupplytestobserve 7'){
 $nav=@{schema=1;observationOnly=$true;entityId=7;world='world';navIndexAvailable=$true;entityNavPresent=$true;navTrackType='Position';navDisplayName='Owner'}|ConvertTo-Json -Compress
 $o=@{ok=$true;hadError=$false;results=@(@{output=@($nav)})}
}else{throw 'Unexpected driver command in explicit offline double'}
$o|ConvertTo-Json -Depth 12 -Compress
'@
$bridge=Join-Path $ScratchDirectory 'offline-bridge-double.ps1';$mock|Set-Content $bridge
$pp=Join-Path $ScratchDirectory 'plan.json'
@{schema=1;gameName='CodexTest';gameWorld='Fixture';nativeWorldGuid='world';eventId='event';rewardEntityId=7;ownerDisplayName='Owner';loadedNativeDisplayName='Owner'}|ConvertTo-Json|Set-Content $pp
$output=& "$PSScriptRoot/Capture-Phase.ps1" -BridgePath $bridge -SessionPath 'offline-only-no-session' -PlanPath $pp -OutputDirectory (Join-Path $ScratchDirectory 'captures') -Phase unloaded
$verdict=($output -join "`n")|ConvertFrom-Json
if($verdict.result -cne 'PASS_LIMITED_PROJECTION'){throw 'Driver did not preserve limited verdict'}
if(@(Get-ChildItem (Join-Path $ScratchDirectory 'captures') -Filter *.json).Count -ne 1){throw 'Capture not persisted'}
'PASS offline driver JSON/CLI argument/console-output/capture/oracle wiring; no real Bridge contacted'
