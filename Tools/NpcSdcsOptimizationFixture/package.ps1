param(
    [string] $ArchiveName = 'NPC_SDCS_Optimization_20261020_changed-files.zip',
    [switch] $IncludeBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$doc = '_Documentation/NPC SDCS Optimization/20261020'
$files = @(
    'Scripts/NPC/Animation/AvatarSdcsNpcController.cs',
    'Scripts/NPC/Animation/RebirthSdcsLocomotionSampler.cs',
    'Scripts/NPC/Models/EModelRebirthSdcsNpc.cs',
    'Scripts/NPC/Models/RebirthSdcsMeshRefreshGate.cs',
    'Scripts/NPC/Models/RebirthSdcsVisualDiagnostics.cs',
    'Scripts/NPC/Foundation/RebirthHumanNpcModelPipeline.cs',
    'Scripts/NPC/Administration/RebirthNpcAdministrationSupport.cs',
    'Tools/GameBridge/tests/npc_sdcs_visual_runtime.json'
)
$files += @(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object { 'Tools/NpcSdcsOptimizationFixture/' + $_.Name })
if ($IncludeBuild) { $files += @('RebirthUtils.dll', 'RebirthUtils.pdb') }
$files += @(Get-ChildItem -LiteralPath (Join-Path $root $doc) -Recurse -File |
    Where-Object { $_.Name -ne 'PACKAGE_MANIFEST.tsv' } |
    ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
$files = @($files | Sort-Object -Unique)
$rows = foreach ($relative in $files) {
    $path = Join-Path $root $relative
    [pscustomobject]@{ path=$relative; bytes=(Get-Item -LiteralPath $path).Length; sha256=(Get-FileHash -LiteralPath $path).Hash }
}
$manifest = "$doc/PACKAGE_MANIFEST.tsv"
$rows | Export-Csv -LiteralPath (Join-Path $root $manifest) -Delimiter "`t" -NoTypeInformation -Encoding utf8
Add-Type -AssemblyName System.IO.Compression.FileSystem
if ([IO.Path]::GetFileName($ArchiveName) -ne $ArchiveName -or -not $ArchiveName.EndsWith('.zip')) { throw 'ArchiveName must be a zip filename.' }
$archivePath = Join-Path $root (Join-Path '_Deliverables' $ArchiveName)
if (Test-Path -LiteralPath $archivePath) { throw 'Archive already exists; choose a new delivery name before replacing it.' }
$archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in @($files) + @($manifest)) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $root $relative), $relative,
            [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($archive.Entries.Count -ne $files.Count + 1) { throw 'Archive entry count mismatch' }
    foreach ($row in $rows) {
        $entry = $archive.GetEntry($row.path)
        if ($null -eq $entry -or $entry.Length -ne $row.bytes) { throw "Missing or wrong-size entry: $($row.path)" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($hash -ne $row.sha256) { throw "Archive content mismatch: $($row.path)" }
    }
    $entry = $archive.GetEntry($manifest)
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $manifestText = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifestText -ne [IO.File]::ReadAllText((Join-Path $root $manifest))) { throw 'Manifest content mismatch' }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $archivePath).Hash
"$hash  $([IO.Path]::GetFileName($archivePath))" | Set-Content -LiteralPath "$archivePath.sha256.txt"
$verification = [pscustomobject]@{ archive=$archivePath; payloadFiles=$files.Count; manifestFiles=1;
    verifiedPayloadHashes=$rows.Count; sha256=$hash; updatedDllIncluded=[bool]$IncludeBuild; fullCumulativeIncluded=$false }
$verification | ConvertTo-Json | Set-Content -LiteralPath "$archivePath.verification.json"
$verification | ConvertTo-Json
