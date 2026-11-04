param([Parameter(Mandatory=$true)][ValidateSet('server','client')][string]$Role,[ValidateSet('help','purge')][string]$Fixture='help')
$ErrorActionPreference='Stop'
$mod=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$stage=Join-Path $mod ("Tools/Profiler/out/"+$Fixture+'-remote-20261102')
$owner=Get-Content -LiteralPath (Join-Path $stage ($Role+'.owner.json')) -Raw|ConvertFrom-Json
$controlPath=Join-Path $stage ($Role+'.private.json')
$control=Get-Content -LiteralPath $controlPath -Raw|ConvertFrom-Json
$session=Get-Content -LiteralPath $control.session -Raw|ConvertFrom-Json
$process=Get-Process -Id $owner.Pid
if($owner.Role -ne $Role -or $session.pid -ne $owner.Pid -or $process.StartTime.ToUniversalTime() -ne [datetime]$owner.StartUtc -or
   $process.Path -ne $owner.Executable -or $process.Path -ne (Join-Path (Resolve-Path (Join-Path $mod '../..')) '7DaysToDie.exe')){throw 'Exact peer ownership mismatch; no action taken.'}
$headers=@{'X-Bridge-Token'=$session.token;'X-Bridge-Run'=$control.runToken}
foreach($endpoint in @('/stop','/quit')){
    $result=Invoke-RestMethod -Method Post -Uri ("http://127.0.0.1:"+$session.port+$endpoint) -Headers $headers -TimeoutSec 30
    if(-not $result.ok){throw 'Owned cleanup refused; lease retained.'}
}
if(-not $process.WaitForExit(60000)){throw 'Exact owned process still live; lease retained. Recheck the same PID.'}
Remove-Item -LiteralPath $controlPath
[pscustomobject]@{ok=$true;role=$Role;pid=$owner.Pid;terminal=$true}|ConvertTo-Json -Compress
