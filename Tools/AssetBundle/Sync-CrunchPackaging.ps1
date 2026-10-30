$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$canonical = Join-Path $PSScriptRoot 'Unity/RebirthAssetBundlePackaging.cs'
$inventory = Get-Content -LiteralPath (Join-Path $root '_Documentation/AssetPackaging/routed-builders.json') -Raw | ConvertFrom-Json
foreach ($directory in @($inventory.Path | ForEach-Object { Split-Path (Join-Path $root $_) } | Sort-Object -Unique)) {
    Copy-Item -LiteralPath $canonical -Destination (Join-Path $directory 'RebirthAssetBundlePackaging.cs')
}
