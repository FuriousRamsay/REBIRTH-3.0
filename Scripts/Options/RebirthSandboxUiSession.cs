using System;

#nullable disable

/// <summary>
/// Pending menu state. It is separate from the live authoritative runtime manager so merely
/// viewing or editing a menu never changes an active game session.
/// </summary>
public static class RebirthSandboxUiSession
{
    private static bool initialized;
    private static string contextKey = string.Empty;
    private static string presetName = RebirthSandboxOptionManager.DefaultPresetName;
    private static string code = RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
    private static int revision;
    private static bool contextWasExistingSave;

    public static event Action Changed;

    public static string PresetName { get { EnsureInitialized(); return presetName; } }
    public static string Code { get { EnsureInitialized(); return code; } }
    public static int Revision { get { EnsureInitialized(); return revision; } }
    public static string ContextIdentity { get { EnsureInitialized(); EnsureContextCurrent(); return contextKey + "|existing=" + (contextWasExistingSave ? "1" : "0"); } }
    public static bool IsExistingSaveContext { get { EnsureInitialized(); EnsureContextCurrent(); return contextWasExistingSave; } }

    public static string CharacterProgressionDebugContext
    {
        get
        {
            EnsureInitialized();
            EnsureContextCurrent();
            string saveDir = string.Empty;
            bool saveDirExists = false;
            bool mainTtwExists = false;
            bool worldOptionsExists = false;
            try
            {
                saveDir = GameIO.GetSaveGameDir() ?? string.Empty;
                saveDirExists = !string.IsNullOrEmpty(saveDir) && System.IO.Directory.Exists(saveDir);
                mainTtwExists = RebirthSandboxPersistence.CurrentWorldHasStarted();
                worldOptionsExists = !string.IsNullOrEmpty(RebirthSandboxPersistence.GetCurrentWorldPath())
                    && System.IO.File.Exists(RebirthSandboxPersistence.GetCurrentWorldPath());
            }
            catch { }

            RebirthSandboxState state;
            string progression = RebirthSandboxOptionManager.TryDecode(code, out state) && state != null
                ? state.PlayerProgression.ToString()
                : "decode-failed";
            return "context='" + contextKey + "' existingSave=" + contextWasExistingSave
                + " saveDirExists=" + saveDirExists + " mainTtw=" + mainTtwExists
                + " worldOptions=" + worldOptionsExists + " saveDir='" + saveDir + "'"
                + " preset='" + presetName + "' revision=" + revision
                + " sessionProgression=" + progression + " code='" + code + "'";
        }
    }

    public static bool TryGetLockedPlayerProgression(out RebirthPlayerProgressionMode mode)
    {
        EnsureInitialized();
        EnsureContextCurrent();
        mode = RebirthPlayerProgressionMode.Rebirth;
        if (!contextWasExistingSave) return false;
        RebirthSandboxState state;
        if (!RebirthSandboxOptionManager.TryDecode(code, out state) || state == null) return false;
        mode = state.PlayerProgression;
        return true;
    }

    /// <summary>
    /// Returns the exact pending new-world snapshot currently shown in the menu without
    /// reloading or rebinding it. GameStarting uses this when the new save has not yet
    /// written its per-world Rebirth options file.
    /// </summary>
    public static bool TryGetPendingSnapshot(out RebirthSandboxSaveData data)
    {
        data = null;
        if (!initialized || contextWasExistingSave)
            return false;

        data = new RebirthSandboxSaveData
        {
            PresetName = presetName,
            Code = code,
            Revision = revision
        };
        return true;
    }

    public static void EnsureInitialized()
    {
        if (!initialized)
            ReloadForCurrentContext();
    }

    public static void ReloadForCurrentContext()
    {
        RebirthSandboxOptionManager.Current.ReloadPresets();
        string newContext = BuildContextKey();
        bool existingSave = !string.IsNullOrEmpty(newContext) && RebirthSandboxPersistence.CurrentWorldHasStarted();
        RebirthSandboxSaveData data = existingSave
            ? RebirthSandboxPersistence.LoadCurrentWorldOrDefault()
            : RebirthSandboxPersistence.LoadLastUsedOrDefault();

        // Existing worlds own an immutable snapshot of the options they were created with.
        // New-world setup, however, owns a reference to a named preset. If that preset has
        // since been edited, its current saved code must win over a stale last-used copy.
        // Otherwise the UI can display the preset name while loading different option values.
        if (!existingSave)
            data = RebindNewWorldSnapshotToCurrentPreset(data);

        if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled)
        {
            RebirthSandboxState debugState;
            string progression = data != null && RebirthSandboxOptionManager.TryDecode(data.Code, out debugState) && debugState != null
                ? debugState.PlayerProgression.ToString()
                : "decode-failed";
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                "session-reload newContext='" + newContext + "' existingSave=" + existingSave
                + " mainTtw=" + RebirthSandboxPersistence.CurrentWorldHasStarted()
                + " source=" + (existingSave ? "world" : "last-used/preset")
                + " loadedProgression=" + progression
                + " preset='" + (data != null ? data.PresetName : string.Empty) + "'"); }
        }

        contextKey = newContext;
        contextWasExistingSave = existingSave;
        initialized = true;
        SetInternal(data.PresetName, data.Code, data.Revision, true);
    }

    public static void EnsureContextCurrent()
    {
        string current = BuildContextKey();
        if (!initialized)
        {
            ReloadForCurrentContext();
            return;
        }

        bool existingSave = !string.IsNullOrEmpty(current) && RebirthSandboxPersistence.CurrentWorldHasStarted();
        bool sameContext = string.Equals(current, contextKey, StringComparison.Ordinal);
        if (sameContext && existingSave == contextWasExistingSave)
            return;

        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "session-context-change old='" + contextKey + "' new='" + current
            + "' sameContext=" + sameContext + " existingSaveNow=" + existingSave
            + " existingSaveBefore=" + contextWasExistingSave
            + " mainTtw=" + RebirthSandboxPersistence.CurrentWorldHasStarted()); }

        // Reload when an actual world save appears or disappears even if the world/game key did
        // not change. The base New Game UI may create an empty target directory before START;
        // that directory is deliberately ignored so Base Game and Rebirth remain selectable.
        // This still covers deleting a world and immediately creating another world with the
        // same name, as well as the moment a pending new world becomes an established save.
        if (sameContext || existingSave || contextWasExistingSave)
        {
            ReloadForCurrentContext();
            return;
        }

        // A new game's directory does not exist yet. Preserve pending selections when the
        // player edits the game name or changes another pre-generation field.
        contextKey = current;
        contextWasExistingSave = false;
    }

    public static void SetFromPreset(RebirthSandboxPreset preset)
    {
        if (preset == null) return;
        SetCode(preset.Name, preset.Code);
    }

    public static void SetCode(string requestedPresetName, string newCode)
    {
        EnsureInitialized();
        EnsureContextCurrent();

        RebirthSandboxState requested;
        if (!RebirthSandboxOptionManager.TryDecode(newCode, out requested) || requested == null)
        {
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression("session-set-code decode-failed requestedPreset='" + (requestedPresetName ?? string.Empty) + "'"); }
            return;
        }

        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "session-set-code request progression=" + requested.PlayerProgression
            + " existingSave=" + contextWasExistingSave
            + " requestedPreset='" + (requestedPresetName ?? string.Empty) + "'"); }

        // Character Progression changes the meaning of the entire save. Once a save exists,
        // preserve the world-authored progression mode even when the user chooses another
        // preset, pastes an option code, or changes other sandbox settings.
        if (contextWasExistingSave)
        {
            RebirthSandboxState locked;
            if (RebirthSandboxOptionManager.TryDecode(code, out locked) && locked != null)
            {
                { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                    "session-set-code LOCK-OVERRIDE requested=" + requested.PlayerProgression
                    + " locked=" + locked.PlayerProgression); }
                requested.PlayerProgression = locked.PlayerProgression;
                newCode = RebirthSandboxOptionManager.Encode(requested);
            }
        }

        SetInternal(requestedPresetName, newCode, revision + 1, true);
        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "session-set-code applied progression=" + requested.PlayerProgression
            + " existingSave=" + contextWasExistingSave + " revision=" + revision); }
    }


    public static void Commit(bool saveAsLastUsed)
    {
        EnsureInitialized();
        // Defense in depth: if the selected context is an existing save, re-read the
        // persisted world snapshot and preserve its Character Progression mode at commit.
        if (contextWasExistingSave)
        {
            RebirthSandboxSaveData persisted;
            if (RebirthSandboxPersistence.TryLoadCurrentWorld(out persisted) && persisted != null)
            {
                RebirthSandboxState liveState, persistedState;
                if (RebirthSandboxOptionManager.TryDecode(code, out liveState) && liveState != null &&
                    RebirthSandboxOptionManager.TryDecode(persisted.Code, out persistedState) && persistedState != null)
                {
                    liveState.PlayerProgression = persistedState.PlayerProgression;
                    code = RebirthSandboxOptionManager.Encode(liveState);
                }
            }
        }

        RebirthSandboxSaveData data = new RebirthSandboxSaveData
        {
            PresetName = presetName,
            Code = code,
            Revision = revision
        };

        RebirthSandboxPersistence.SaveCurrentWorld(data);
        // Continuing an existing save must not overwrite the defaults used for a future new
        // world. In particular, a locked Base/Rebirth progression choice belongs to this save.
        if (saveAsLastUsed && !contextWasExistingSave)
            RebirthSandboxPersistence.SaveLastUsed(data);
    }


    private static RebirthSandboxSaveData RebindNewWorldSnapshotToCurrentPreset(RebirthSandboxSaveData data)
    {
        if (data == null)
            return CreateDefaultSnapshot();

        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(data.PresetName);
        if (preset == null)
            return data;

        if (string.Equals(data.Code, preset.Code, StringComparison.Ordinal))
        {
            data.PresetName = preset.Name;
            return data;
        }

        { if (RebirthLogSettings.UiRouteLoggingEnabled) Log.Out("[RebirthSandbox] Refreshed new-world preset '" + preset.Name
            + "' from its current saved definition instead of a stale last-used option code."); }

        return new RebirthSandboxSaveData
        {
            PresetName = preset.Name,
            Code = preset.Code,
            Revision = IncrementRevision(data.Revision)
        };
    }

    private static RebirthSandboxSaveData CreateDefaultSnapshot()
    {
        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(
            RebirthSandboxOptionManager.DefaultPresetName);
        return new RebirthSandboxSaveData
        {
            PresetName = preset != null ? preset.Name : RebirthSandboxOptionManager.DefaultPresetName,
            Code = preset != null ? preset.Code : RebirthSandboxOptionManager.Encode(new RebirthSandboxState()),
            Revision = 0
        };
    }

    private static int IncrementRevision(int value)
    {
        value = Math.Max(0, value);
        return value < int.MaxValue ? value + 1 : value;
    }

    private static void SetInternal(string newPresetName, string newCode, int newRevision, bool notify)
    {
        RebirthSandboxState ignored;
        if (!RebirthSandboxOptionManager.TryDecode(newCode, out ignored))
        {
            RebirthSandboxPreset fallback = RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);
            newCode = fallback != null ? fallback.Code : RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
            newPresetName = RebirthSandboxOptionManager.DefaultPresetName;
            newRevision = 0;
        }

        RebirthSandboxPreset resolved = RebirthSandboxOptionManager.Current.ResolvePreset(newPresetName, newCode);
        presetName = resolved != null ? resolved.Name : RebirthSandboxOptionManager.DefaultPresetName;
        code = newCode;
        revision = Math.Max(0, newRevision);

        if (notify)
        {
            Action handler = Changed;
            if (handler != null)
                handler();
        }
    }

    private static string BuildContextKey()
    {
        try
        {
            string world = GamePrefs.GetString(EnumGamePrefs.GameWorld) ?? string.Empty;
            string game = GamePrefs.GetString(EnumGamePrefs.GameName) ?? string.Empty;
            int storage = GamePrefs.GetInt(EnumGamePrefs.GameSaveStorageType);
            if (world.Length == 0 || game.Length == 0)
                return string.Empty;
            return storage + "|" + world + "|" + game;
        }
        catch
        {
            return string.Empty;
        }
    }
}
