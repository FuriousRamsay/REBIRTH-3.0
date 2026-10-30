param([Parameter(Mandatory)][string]$BridgePath,[Parameter(Mandatory)][string]$SessionPath,
 [Parameter(Mandatory)][string]$PlanPath,[Parameter(Mandatory)][string]$OutputDirectory,
 [Parameter(Mandatory)][ValidateSet('loaded','unloaded','returned','rejoined','withdrawn')][string]$Phase,
 [switch]$WalkRoute)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$p=Get-Content -LiteralPath $PlanPath -Raw|ConvertFrom-Json
if($p.schema -ne 1 -or $p.gameName -cne 'CodexTest' -or $p.rewardEntityId -le 0){throw 'Populate a real staged CodexTest plan first'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
function Bridge([string[]]$Arguments){
 $raw=& pwsh -NoProfile -File $BridgePath -SessionPath $SessionPath @Arguments
 if($LASTEXITCODE -ne 0){throw 'Bridge command failed; no acceptance'}
 $value=($raw -join "`n")|ConvertFrom-Json
 if($value.ok -ne $true){throw 'Bridge response unsuccessful'}
 return $value
}
$before=Bridge @('call','GET','/state','sections=player,world')
if($before.world.gameName -cne 'CodexTest' -or $before.world.gameWorld -cne $p.gameWorld){throw 'Wrong disposable world/save'}
if($WalkRoute){
 if($Phase -notin @('unloaded','returned')){throw 'WalkRoute only supports unloaded/returned'}
 $route=if($Phase -eq 'unloaded'){@($p.awayRoute)}else{@($p.returnRoute)}
 if($route.Count -eq 0){throw 'Stage a safe real walking route first'}
 foreach($point in $route){
  $null=Bridge @('surroundings','radius=30')
  $null=Bridge @('walkto',"x=$($point.x)","z=$($point.z)",'maxSeconds=60','radius=1.5')
 }
}
# No automatic launch/quit/save/reset/teleport/time changes. Lead stages rejoin/removal/expiry.
Start-Sleep -Seconds 3
$c=[ordered]@{schema=1;phase=$Phase;entityId=$p.rewardEntityId;capturedUtc=[datetime]::UtcNow.ToString('o');before=$before}
$a=Bridge @('console',"rbsupplytestobserve $($p.rewardEntityId)")
function Nav($response){
 if($response.hadError){throw 'Native nav observation command failed'}
 $values=@($response.results|ForEach-Object {$_.output}|Where-Object {$_ -match '^\s*\{'}|ForEach-Object {$_|ConvertFrom-Json})
 if($values.Count -ne 1){throw 'Expected one actual observer JSON output'}
 return $values[0]
}
$c.navBefore=Nav $a
# Existing generic entity route, NOT entity=ID (which only supports zombies).
$c.entities=Bridge @('entities','radius=10000000','limit=10000','contains=RB_EventLootContainer')
$b=Bridge @('console',"rbsupplytestobserve $($p.rewardEntityId)")
$c.navAfter=Nav $b
$c.eventStatus=Bridge @('console','rbevents status')
$c.after=Bridge @('call','GET','/state','sections=player,world')
$c.rawNavBefore=$a;$c.rawNavAfter=$b
$path=Join-Path $OutputDirectory ($Phase+'-'+[guid]::NewGuid().ToString('N')+'.json')
$c|ConvertTo-Json -Depth 50|Set-Content -LiteralPath $path
& (Join-Path $PSScriptRoot 'Assert-Capture.ps1') -CapturePath $path -PlanPath $PlanPath -Phase $Phase
