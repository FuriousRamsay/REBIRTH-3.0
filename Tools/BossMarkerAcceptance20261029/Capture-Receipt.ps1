param([Parameter(Mandatory)][string]$BridgePath,[Parameter(Mandatory)][string]$SessionPath,
 [Parameter(Mandatory)][guid]$EventId,[Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$RewardId,
 [Parameter(Mandatory)][string]$OutPath)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
if($EventId -eq [guid]::Empty -or (Test-Path -LiteralPath $OutPath)){throw 'Use actual nonempty event and a fresh capture path'}
$s=Get-Content -LiteralPath $SessionPath -Raw|ConvertFrom-Json
# Existing client validates immutable session, process and lease; never bypass it.
$raw=& pwsh -NoProfile -File $BridgePath -SessionPath $SessionPath console "rbbossrewardtestobserve $($EventId.ToString('N')) $RewardId"
if($LASTEXITCODE -ne 0){throw 'Bridge failed; observer may not be installed'}
$reply=($raw -join "`n")|ConvertFrom-Json
if($reply.ok -ne $true -or $reply.hadError -ne $false){throw 'Observer command failed'}
$observations=@($reply.results|ForEach-Object {$_.output}|Where-Object {$_ -match '^\s*\{'}|ForEach-Object {$_|ConvertFrom-Json})
if($observations.Count -ne 1){throw 'Expected exactly one real observer JSON reply'}
[ordered]@{captureSchema=1;capturedUtc=[datetime]::UtcNow.ToString('o');session=@{pid=$s.pid;processStartUtc=$s.processStartUtc};observation=$observations[0];rawReply=$reply}|ConvertTo-Json -Depth 50|Set-Content -LiteralPath $OutPath
"Capture saved; NOT acceptance. Run Assert-Receipt.ps1 against a qualified loaded baseline."
