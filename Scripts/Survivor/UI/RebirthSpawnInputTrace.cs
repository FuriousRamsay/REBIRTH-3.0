using System.Text;
using UnityEngine;

#nullable disable

/// <summary>
/// Owner 2026-11-08: on a dedicated server the mouse does not move after spawning. Debug builds only, after
/// `rbdebug on survivor` (or `rbdebug on`): logs the spawn-screen decision at game start and, for 30 seconds after
/// the local player spawns, every second, every input lock that can freeze the camera.
/// </summary>
internal static class RebirthSpawnInputTrace
{
    private const string Area = "survivor";
    private static float until, next;
    private static string last;

    internal static bool On => RebirthDebug.IsOn(Area);

    internal static void GameStarting(bool asServer, bool editMode, bool? existingSurvivor, bool skipBefore)
    {
        if (!On) return;
        RebirthDebug.Log(Area, "[REBIRTH SpawnTrace] game-starting asServer=" + asServer + " dedicated=" + GameManager.IsDedicatedServer +
            " editMode=" + editMode + " configuredMode=" + RebirthSurvivorMode.ConfiguredMode +
            " existingSurvivor=" + (existingSurvivor.HasValue ? existingSurvivor.Value.ToString() : "unknown") +
            " skipSpawnBefore=" + skipBefore + " skipSpawnAfter=" + GamePrefs.GetBool(EnumGamePrefs.SkipSpawnButton) +
            " saveDir=" + GameIO.GetSaveGameDir());
    }

    internal static void Note(string what)
    {
        if (!On) return;
        RebirthDebug.Log(Area, "[REBIRTH SpawnTrace] " + what + " " + Describe());
    }

    internal static void PlayerSpawned()
    {
        if (!On) return;
        until = Time.realtimeSinceStartup + 30f; next = 0f; last = null;
        Note("local-player-spawned");
        RebirthSandboxTrace.Client("options in effect at local spawn");
    }

    /// <summary>Called every game update; does nothing unless a trace window is open.</summary>
    internal static void Pump()
    {
        if (until <= 0f) return;
        float now = Time.realtimeSinceStartup;
        if (now > until || !On) { until = 0f; return; }
        if (now < next) return;
        next = now + 1f;
        string state = Describe();
        // Log every second while it changes, and at least every 5 seconds while it stays the same.
        if (state == last && ((int)(until - now)) % 5 != 0) return;
        last = state;
        RebirthDebug.Log(Area, "[REBIRTH SpawnTrace] t-" + (int)(until - now) + "s " + state);
    }

    private static string Describe()
    {
        var b = new StringBuilder();
        var gm = GameManager.Instance;
        var player = gm?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        var ui = player != null ? LocalPlayerUI.GetUIForPlayer(player) : null;
        var move = player != null ? player.GetComponent<PlayerMoveController>() : null;
        b.Append("player=").Append(player != null ? player.entityId.ToString() : "none");
        if (player != null)
        {
            b.Append(" spawned=").Append(player.IsSpawned()).Append(" dead=").Append(player.IsDead())
             .Append(" introAnim=").Append(player.bIntroAnimActive)
             .Append(" heldByCreation=").Append(RebirthCharacterCreationHoldService.IsHeld(player));
        }
        if (move != null) b.Append(" canControlOverride=").Append(move.bCanControlOverride);
        if (gm != null)
        {
            b.Append(" cursorVisible=").Append(gm.bCursorVisible)
             .Append(" cursorOverride=").Append(gm.bCursorVisibleOverride).Append('/').Append(gm.bCursorVisibleOverrideState)
             .Append(" modalOpen=").Append(gm.isAnyModalWindowOpen())
             .Append(" cursorWindowOpen=").Append(ui != null && gm.isAnyCursorWindowOpen(ui));
        }
        if (ui != null)
        {
            var cursor = ui.CursorController;
            if (cursor != null) b.Append(" cursorLocked=").Append(cursor.Locked);
            b.Append(" windows=[").Append(OpenWindows(ui.windowManager)).Append(']');
        }
        b.Append(" cursorLockState=").Append(Cursor.lockState).Append(" osCursorVisible=").Append(Cursor.visible);
        var owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (owner == null) b.Append(" owner=unknown");
        else b.Append(" owner.rebirth=").Append(owner.RebirthModeEnabled).Append(" owner.hasCharacter=").Append(owner.HasCharacter)
              .Append(" owner.creation=").Append(owner.CreationState);
        return b.ToString();
    }

    private static string OpenWindows(GUIWindowManager manager)
    {
        if (manager == null) return "?";
        var b = new StringBuilder();
        foreach (var window in manager.openWindows)
            if (window != null)
            { if (b.Length > 0) b.Append(','); b.Append(window.Id); }
        return b.ToString();
    }
}
