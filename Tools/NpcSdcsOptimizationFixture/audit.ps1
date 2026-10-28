$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sdk = (Get-ChildItem 'C:/Program Files/dotnet/sdk' -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
Add-Type -Path (Join-Path $sdk 'Roslyn/bincore/Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $sdk 'Roslyn/bincore/Microsoft.CodeAnalysis.CSharp.dll')
$files = @(
    'Scripts/NPC/Animation/AvatarSdcsNpcController.cs',
    'Scripts/NPC/Animation/RebirthSdcsLocomotionSampler.cs',
    'Scripts/NPC/Models/EModelRebirthSdcsNpc.cs',
    'Scripts/NPC/Models/RebirthSdcsMeshRefreshGate.cs',
    'Scripts/NPC/Models/RebirthSdcsVisualDiagnostics.cs',
    'Scripts/NPC/Foundation/RebirthHumanNpcModelPipeline.cs',
    'Scripts/NPC/Administration/RebirthNpcAdministrationSupport.cs'
)
$results = foreach ($relative in $files) {
    $path = Join-Path $root $relative
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($path), [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default,
        $path, [Text.Encoding]::UTF8, [Threading.CancellationToken]::None)
    $errors = @($tree.GetDiagnostics() | Where-Object { $_.Severity.ToString() -eq 'Error' } | ForEach-Object { $_.ToString() })
    [pscustomobject]@{ file=$relative; sha256=(Get-FileHash -LiteralPath $path).Hash; syntaxErrors=$errors }
}
$scenarioPath = Join-Path $root 'Tools/GameBridge/tests/npc_sdcs_visual_runtime.json'
$scenario = Get-Content -LiteralPath $scenarioPath -Raw | ConvertFrom-Json
if ($scenario.steps.Count -lt 1) { throw 'No scenario steps' }
$result = [pscustomobject]@{ kind='Roslyn syntax only; not native semantic compilation'; files=@($results); scenarioSteps=$scenario.steps.Count }
$result | ConvertTo-Json -Depth 6
if (@($results | Where-Object { $_.syntaxErrors.Count -gt 0 }).Count -gt 0) { throw 'Source syntax validation failed' }
