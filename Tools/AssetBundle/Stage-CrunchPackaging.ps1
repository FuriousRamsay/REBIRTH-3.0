param([Parameter(Mandatory=$true)][string]$ProjectPath)
$ErrorActionPreference = 'Stop'
$existing = @(Get-ChildItem -LiteralPath (Join-Path $ProjectPath 'Assets') -Filter 'RebirthAssetBundlePackaging.cs' -Recurse -File -ErrorAction SilentlyContinue)
if ($existing.Count -gt 1) { throw 'Multiple Crunch policy copies in Unity project; retain exactly one editor copy before building.' }
$target = if ($existing.Count -eq 1) { $existing[0].FullName } else { Join-Path $ProjectPath 'Assets/Editor/RebirthAssetBundlePackaging.cs' }
New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Unity/RebirthAssetBundlePackaging.cs') -Destination $target
