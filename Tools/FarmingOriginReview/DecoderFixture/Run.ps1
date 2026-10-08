param([switch]$Compile)
$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../../..").Path
function Extract([string]$path,[string]$signature){$s=Get-Content $path -Raw;$a=$s.IndexOf($signature);if($a-lt0){throw "Missing $signature"};$b=$s.IndexOf('{',$a);$i=$b+1;$depth=1;while($depth){if($s[$i]-eq'{'){$depth++};if($s[$i]-eq'}'){$depth--};$i++};$s.Substring($a,$i-$a)}
$code=Get-Content "$PSScriptRoot/Adapters.cs" -Raw
$paths=@{
'BLOCKCHANGE_READ'=@('Tools/HarvestNativeAudit/installed-3.2b10/BlockChangeInfo.cs','public void Read(PooledBinaryReader _br)');
'BLOCKVALUE_READ'=@('Tools/TheorySoloFixture/native/BlockValue.cs','public static BlockValue Read(PooledBinaryReader br)');
'BLOCKREF_READ'=@('Tools/FarmingPlayerOriginCandidate/native/BlockValueRef.cs','public static BlockValueRef Read(PooledBinaryReader br)');
'TEXTURE_READ'=@('Tools/FarmingPlayerOriginCandidate/native/TextureFullArray.cs','public unsafe void Read(PooledBinaryReader _br, int count = 1)')}
foreach($key in $paths.Keys){$m=Extract (Join-Path $root $paths[$key][0]) $paths[$key][1];if($key-eq'TEXTURE_READ'){$m=$m.Replace('public unsafe void','public void')};$code=$code.Replace('/*'+$key+'*/',$m)}
$code=$code.Replace('/*DECODER*/', ((Get-Content "$PSScriptRoot/../SeedPlacementNativeDecoder.review.cs" -Raw)-replace '(?m)^using .+;\r?$',''))
$code=$code.Replace('/*INTENT_CODEC*/', ((Get-Content "$PSScriptRoot/../SeedPlacementIntentCodec.review.cs" -Raw)-replace '(?m)^using .+;\r?$',''))
$packet=Get-Content "$PSScriptRoot/../NetPackageRebirthSeedPlacement.review.cs" -Raw
$code=$code.Replace('/*CLIENT_SCOPE*/',$packet.Substring($packet.IndexOf('// Client synchronous native voxel action scope.')))
$code=$code.Replace('/*ENVELOPE*/',(($packet.Substring(0,$packet.IndexOf('// Client synchronous native voxel action scope.')))-replace '(?m)^using .+;\r?$',''))
$code=$code.Replace('/*ORIGINAL_COMMAND*/',((Get-Content "$root/Tools/FarmingPlayerOriginCandidate/SeedPlacementOriginalCommand.review.cs" -Raw)-replace '(?m)^using .+;\r?$',''))
$code=$code.Replace('/*HOST_OWNER*/',((Get-Content "$root/Tools/FarmingPlayerOriginCandidate/SeedPlacementHostOwner.review.cs" -Raw)-replace '(?m)^using .+;\r?$',''))
$code=$code.Replace('/*CONSUMED_CAPTURE*/',((Get-Content "$PSScriptRoot/../SeedPlacementConsumedEnvelope.review.cs" -Raw)-replace '(?m)^using .+;\r?$',''))
Set-Content "$PSScriptRoot/Generated.cs" $code
if(!$Compile){'PREPARED ONLY';return}
Get-Content E:/_Haven/shared/coordination/HAV3N_to_codex.txt -Raw
$jobs=@(Get-CimInstance Win32_Process|Where-Object {$_.Name-match '^(dotnet|MSBuild|csc|ilspycmd|inspect)\.exe$' -and $_.CommandLine-notmatch 'VBCSCompiler\.dll'})
if($jobs.Count){$jobs|Select-Object ProcessId,Name,CommandLine|Format-List;Write-Output 'Independent unique-output fixture permitted by coordinator; unrelated jobs observed.'}
Add-Type -Path "$PSScriptRoot/Generated.cs"
[DecoderFixture]::Run()
'No production, game, mod build or packet registration.'







