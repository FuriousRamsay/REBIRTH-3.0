# Load actual ownership helpers only; no game launch, save reset, or HTTP call.
$ErrorActionPreference='Stop'
$tokens=$null; $parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'gamebridge.ps1'),[ref]$tokens,[ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
foreach ($name in @('Get-OwnedGameProcess','Get-Session')) {
    $node=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true)
    Invoke-Expression $node.Extent.Text
}
$tempDir=Join-Path $PSScriptRoot ('out/ownership-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
$GameExe='C:\fixture\7DaysToDie.exe'
$LaunchFile=Join-Path $tempDir 'launch.json'
$SessionPath=$null
$SessionFile=Join-Path $tempDir 'session.json'
$started=[datetime]::UtcNow
$fakeProcess=[pscustomobject]@{Id=1234;Path=$GameExe;StartTime=$started}
function Get-Process { param($Id,$ErrorAction) if ($Id -eq $fakeProcess.Id) { return $fakeProcess } }
$owner=@{pid=1234;processStartUtc=$started.ToString('o')}
$owner | ConvertTo-Json | Set-Content -LiteralPath $LaunchFile
$session=@{pid=1234;processStartUtc=$started.ToString('o');startedUtc=$started.ToString('o');token='fixture';port=8765;outputDir=$tempDir}
$session | ConvertTo-Json | Set-Content -LiteralPath $SessionFile
$checks=0
if (-not (Get-OwnedGameProcess) -or -not (Get-Session)) { throw 'matching owned process rejected' }; $checks++
$fakeProcess.StartTime=$started.AddSeconds(1)
if ((Get-OwnedGameProcess) -or (Get-Session)) { throw 'reused PID accepted' }; $checks++
$fakeProcess.StartTime=$started; $fakeProcess.Path='C:\other\7DaysToDie.exe'
if ((Get-OwnedGameProcess) -or (Get-Session)) { throw 'other folder accepted' }; $checks++
$fakeProcess.Path=$GameExe
Remove-Item -LiteralPath $LaunchFile
if ((Get-OwnedGameProcess) -or (Get-Session)) { throw 'unowned default discovery accepted' }; $checks++
$SessionPath=$SessionFile
if (-not (Get-Session)) { throw 'explicit valid pinned session rejected' }; $checks++
$session.processStartUtc=$started.AddSeconds(2).ToString('o')
$session | ConvertTo-Json | Set-Content -LiteralPath $SessionFile
if (Get-Session) { throw 'explicit stale session accepted' }; $checks++
[Console]::WriteLine('{"passed":true,"ownershipChecks":' + $checks + '}')
