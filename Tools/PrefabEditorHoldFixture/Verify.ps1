# Source guard regression for the Prefab Editor creation-hold bypass.
# Native movement and UI rendering still require an editor run.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$hold = Get-Content -LiteralPath (Join-Path $root 'Scripts/Survivor/Creation/RebirthCharacterCreationHoldService.cs') -Raw
$ui = Get-Content -LiteralPath (Join-Path $root 'Scripts/Survivor/UI/RebirthSurvivorFirstEntryUiService.cs') -Raw
$network = Get-Content -LiteralPath (Join-Path $root 'Scripts/Survivor/Network/RebirthSurvivorNetworkService.cs') -Raw
$purge = Get-Content -LiteralPath (Join-Path $root 'Scripts/Purge/Runtime/RebirthPurgeTruckStart.cs') -Raw

function Assert-Guard([string]$source, [string]$signature, [string]$guard) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $brace = $source.IndexOf('{', $start)
    if ($brace -lt 0) { throw "Missing body: $signature" }
    $head = $source.Substring($brace + 1, [Math]::Min(350, $source.Length - $brace - 1))
    if (-not $head.Contains($guard)) { throw "Missing early edit guard: $signature" }
}

foreach ($signature in @(
    'bool IsHeld(EntityPlayer player)',
    'bool IsHeldEntityId(int entityId)',
    'bool TryGetAnchor(int entityId, out Vector3 anchor)',
    'void OnCharacterCommitted(EntityPlayer player, RebirthStablePlayerIdentity identity)',
    'void OnGameUpdate(ref ModEvents.SGameUpdateData data)',
    'void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)',
    'void Enter(EntityPlayer player, RebirthStablePlayerIdentity identity, RespawnType reason)'
)) { Assert-Guard $hold $signature 'SkipForEditMode()' }

foreach ($className in @(
    'RebirthCharacterCreationHoldMovePatch',
    'RebirthCharacterCreationHoldNetworkMovePatch',
    'RebirthCharacterCreationHoldUseItemPatch',
    'RebirthCharacterCreationHoldPlayerDamagePatch',
    'RebirthCharacterCreationHoldLocalDamagePatch',
    'RebirthCharacterCreationHoldBlockActivationPatch',
    'RebirthCharacterCreationHoldAttackTargetPatch',
    'RebirthCharacterCreationHoldNativeXpPatch',
    'RebirthCharacterCreationHoldDisconnectPatch'
)) {
    $start = $hold.IndexOf('class ' + $className, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing patch: $className" }
    Assert-Guard $hold.Substring($start) 'Prefix(' 'IsEditModeActive()'
}

foreach ($signature in @(
    'bool ShouldShowSpawnProfileSelector()',
    'void RegisterNativeSpawnSelectionHost(XUi xui)',
    'bool ShouldShowEmbeddedPreSpawnChooser()',
    'void OnOwnerStateChanged(RebirthSurvivorOwnerStateSnapshot snapshot)',
    'void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)',
    'void OnGameUpdate(ref ModEvents.SGameUpdateData data)',
    'void NotifyNativePlayerSpawned(ClientInfo clientInfo, RespawnType reason, Vector3i pos, int entityId)',
    'void LockLocalPlayer()'
)) { Assert-Guard $ui $signature 'IsEditModeActive()' }

Assert-Guard $network 'void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)' 'IsEditModeActive()'
if (-not $purge.Contains('GameManager.Instance?.IsEditMode()!=true')) { throw 'Purge startup lacks edit guard' }
if (-not $hold.Contains('creation hold skipped because edit mode is active')) { throw 'Missing SpawnFlow skip log' }
Write-Output 'PASS: editor bypass covers hold queries, gameplay patches, spawn UI, and Purge startup.'
