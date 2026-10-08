using HarmonyLib;

#nullable disable

/// <summary>
/// Guaranteed downstream spawn trace/commit hook. The failing V3.2 run created EntityPlayerLocal
/// after the spawn click without producing any coordinator update trace, so the staged Survivor
/// commit is also driven directly from the native player-spawn method.
/// </summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerSpawnedInWorld))]
public static class RebirthSurvivorNativePlayerSpawnedPatch
{
    [HarmonyPostfix]
    public static void Postfix(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos, int _entityId)
    {
        RebirthSurvivorFirstEntryUiService.NotifyNativePlayerSpawned(_cInfo, _respawnReason, _pos, _entityId);
    }
}
