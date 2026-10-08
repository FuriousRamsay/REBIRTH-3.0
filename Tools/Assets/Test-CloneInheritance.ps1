param([string]$MergedBlocks)
$ErrorActionPreference = 'Stop'
$modRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('RebirthCloneInheritance-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testOutput | Out-Null
$sources = @('Scripts\BlockPickup\RebirthBlockPickupEmptyVariantGenerator.cs', 'Scripts\BlockPickup\RebirthBlockPickupLegacyTargets.cs')
$testSources = foreach ($source in $sources) {
    $destination = Join-Path $testOutput ([IO.Path]::GetFileName($source))
    (Get-Content -Raw -LiteralPath (Join-Path $modRoot $source)).Replace('#nullable disable', '') | Set-Content -LiteralPath $destination
    $destination
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$executable = Join-Path $testOutput 'CloneInheritanceHarness.exe'
& $compiler /nologo /r:System.Xml.Linq.dll "/out:$executable" (Join-Path $PSScriptRoot 'CloneInheritanceHarness.cs') @testSources
if ($LASTEXITCODE -ne 0) { throw 'Offline harness compilation failed.' }
& $executable (Join-Path $PSScriptRoot 'clone_inheritance_fixture.xml') (Join-Path $testOutput 'fixture_after.xml')
if ($LASTEXITCODE -ne 0) { throw 'Inheritance fixture failed.' }
if ($MergedBlocks) {
    & $executable (Resolve-Path -LiteralPath $MergedBlocks).Path (Join-Path $testOutput 'blocks_after.xml')
    if ($LASTEXITCODE -ne 0) { throw 'Merged block inheritance checks failed.' }
}
Write-Output "Offline results: $testOutput"
