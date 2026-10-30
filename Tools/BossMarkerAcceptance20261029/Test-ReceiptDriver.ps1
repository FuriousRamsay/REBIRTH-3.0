param([string]$ScratchDirectory=(Join-Path $env:TEMP ('boss-receipt-driver-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop';New-Item -ItemType Directory -Path $ScratchDirectory -Force|Out-Null
$null=& "$PSScriptRoot/Test-ReceiptOracle.ps1" -ScratchDirectory $ScratchDirectory
$sample=Get-Content (Join-Path $ScratchDirectory 'baseline.json') -Raw|ConvertFrom-Json
$session=Join-Path $ScratchDirectory 'session.json';$sample.session|ConvertTo-Json|Set-Content $session
$reply=@{ok=$true;hadError=$false;results=@(@{output=@($sample.observation|ConvertTo-Json -Depth 30 -Compress)})}
$response=Join-Path $ScratchDirectory 'offline-response.json';$reply|ConvertTo-Json -Depth 35|Set-Content $response
$mock=@'
param([string]$SessionPath,[Parameter(Position=0)][string]$Command,[Parameter(ValueFromRemainingArguments)][string[]]$Rest)
if($Command -cne 'console' -or $Rest[0] -cne 'rbbossrewardtestobserve 22222222222222222222222222222222 7'){throw 'Unexpected command in explicit offline double'}
Get-Content (Join-Path $PSScriptRoot 'offline-response.json') -Raw
'@
$bridge=Join-Path $ScratchDirectory 'offline-bridge-double.ps1';$mock|Set-Content $bridge
$capture=Join-Path $ScratchDirectory 'driver-capture.json'
$null=& "$PSScriptRoot/Capture-Receipt.ps1" -BridgePath $bridge -SessionPath $session -EventId '22222222222222222222222222222222' -RewardId 7 -OutPath $capture
$result=(& "$PSScriptRoot/Assert-Receipt.ps1" -CapturePath $capture -BaselinePath $capture -Phase loaded)|ConvertFrom-Json
if($result.result -cne 'PASS_HOST_RECEIPT_AND_PROJECTION'){throw 'Driver result differs'}
'PASS exact observer command/JSON/session envelope/persisted capture/strict oracle wiring via explicit offline double'
$reply.hadError=$true;$reply|ConvertTo-Json -Depth 35|Set-Content $response
$failed=$false
try{$null=& "$PSScriptRoot/Capture-Receipt.ps1" -BridgePath $bridge -SessionPath $session -EventId '22222222222222222222222222222222' -RewardId 7 -OutPath (Join-Path $ScratchDirectory 'rejected.json')}catch{$failed=$true}
if(!$failed){throw 'Missing/rejected observer did not fail capture'}
'PASS rejected observer command is not native acceptance; no real Bridge contacted'
