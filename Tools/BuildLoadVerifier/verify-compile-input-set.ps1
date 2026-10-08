param([Parameter(Mandatory=$true)][string]$ExpectedInventory,[string]$Project='RebirthUtils.csproj')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$raw = & dotnet msbuild $Project -getItem:Compile
if($LASTEXITCODE -ne 0){throw 'Compile input evaluation failed'}
$actual = ($raw -join [Environment]::NewLine) | ConvertFrom-Json
$expected = Get-Content -LiteralPath $ExpectedInventory -Raw | ConvertFrom-Json
$actualPaths=@($actual.Items.Compile | ForEach-Object {[IO.Path]::GetFullPath($_.FullPath).ToUpperInvariant()} | Sort-Object -Unique)
$expectedPaths=@($expected.Items.Compile | ForEach-Object {[IO.Path]::GetFullPath($_.FullPath).ToUpperInvariant()} | Sort-Object -Unique)
if(!$actualPaths.Count -or !$expectedPaths.Count){throw 'Empty compile inventory refused'}
$difference=@(Compare-Object $expectedPaths $actualPaths)
if($difference.Count){$difference | ConvertTo-Json -Depth 3 | Write-Output; throw 'Compile input set changed: added or removed source files'}
[pscustomobject]@{status='PASS';compileInputs=$actualPaths.Count;added=0;removed=0} | ConvertTo-Json
