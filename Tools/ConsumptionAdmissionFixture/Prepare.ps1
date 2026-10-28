$ErrorActionPreference='Stop'
$fixtureRoot=$PSScriptRoot
$source=Get-Content -LiteralPath (Join-Path $fixtureRoot '../../Scripts/Metabolism/RebirthMetabolismService.cs') -Raw
function Exact-Slice([string]$start,[string]$end) {
    $first=$source.IndexOf($start,[StringComparison]::Ordinal)
    if($first -lt 0 -or $source.IndexOf($start,$first+1,[StringComparison]::Ordinal) -ge 0){throw "Nonunique source boundary: $start"}
    $last=$source.IndexOf($end,$first,[StringComparison]::Ordinal)
    if($last -lt 0){throw "Missing source end boundary: $end"}
    return $source.Substring($first,$last-$first)
}
$parts=@(
    (Exact-Slice '    public static bool IsServerAuthority' '    public static void OnPlayerLoaded'),
    (Exact-Slice '    public static RebirthConsumeResult ConsumeMatchingInventoryItem' '    public static RebirthConsumeResult ConsumeHydrationSlot'),
    (Exact-Slice '    private static RebirthConsumeResult ConsumeAt(' '    private static bool IsGodModeActive'),
    (Exact-Slice '    private static void CleanupEntries(' '    private static void AdvanceTimedEffects'),
    (Exact-Slice '    private static bool HasTimedEffect(' '    private static void TryAutoSip'),
    (Exact-Slice '    public static float GetFullnessMl(' '    public static float GetIntestinalFluidMl'),
    (Exact-Slice '    private static bool TryReserveIngestionCapacity(' '    public static float GetPendingEnergyUnits'),
    (Exact-Slice '    private static RebirthConsumeResult Fail(' '    public static void StartDebugTrace')
)
$generated='using System;'+[Environment]::NewLine+'using System.Globalization;'+[Environment]::NewLine+'using UnityEngine;'+[Environment]::NewLine+'public static partial class RebirthMetabolismService {'+[Environment]::NewLine+($parts -join [Environment]::NewLine)+[Environment]::NewLine+'}'
$output=Join-Path $fixtureRoot 'generated'
New-Item -ItemType Directory -Path $output -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $output 'ActualConsumptionService.cs'),$generated,(New-Object Text.UTF8Encoding($false)))
$manifest=@{source='Scripts/Metabolism/RebirthMetabolismService.cs';sourceHash=(Get-FileHash -LiteralPath (Join-Path $fixtureRoot '../../Scripts/Metabolism/RebirthMetabolismService.cs') -Algorithm SHA256).Hash;generatedHash=(Get-FileHash -LiteralPath (Join-Path $output 'ActualConsumptionService.cs') -Algorithm SHA256).Hash;extraction='unchanged method slices with unique checked start/end boundaries';core='ConsumeMatchingInventoryItem,ConsumeAt,TryReserveIngestionCapacity,MergeOrAddEntry,TryFindMatchingStack,Matches,DecrementSource,FirePreservedUseEffects,CleanupEntries,GetFullnessMl,GetFluidMl,GetSolidMl,ApplySupplement,AddTimedEffect,GetMealEnzymeSnapshot,HasTimedEffect,Fail,IsServerAuthority'}
[IO.File]::WriteAllText((Join-Path $output 'source_manifest.json'),($manifest|ConvertTo-Json -Depth 4),(New-Object Text.UTF8Encoding($false)))
