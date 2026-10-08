using System;
using System.IO;
using System.Xml;

#nullable disable

public sealed class RebirthSandboxSaveData
{
    public string PresetName = RebirthSandboxOptionManager.DefaultPresetName;
    public string Code = RebirthSandboxOptionManager.CodePrefix + RebirthSandboxOptionManager.CurrentVersion;
    public int Revision;
}

public static class RebirthSandboxPersistence
{
    public const string WorldFileName = "RebirthSandboxOptions.xml";
    public const string LastUsedFileName = "newRebirthSandboxOptions.xml";

    public static RebirthSandboxSaveData LoadCurrentWorldOrDefault()
    {
        RebirthSandboxSaveData data;
        if (TryLoad(GetCurrentWorldPath(), out data))
            return data;
        return CurrentWorldHasStarted() ? CreateLegacyWorldDefault() : CreateDefault();
    }

    /// <summary>
    /// Resolves the authoritative snapshot before base XML and biome definitions load.
    /// Existing saves use their per-world file. A brand-new save may not have written
    /// that file yet, so it uses the exact pending menu snapshot (or last-used snapshot
    /// on a dedicated server) and seeds the new save before static-data initialization.
    /// </summary>
    public static RebirthSandboxSaveData LoadForGameStarting(out string source)
    {
        RebirthSandboxSaveData data;
        RebirthSandboxSaveData currentWorld;
        bool hasCurrentWorld = TryLoadCurrentWorld(out currentWorld);

        // Dedicated servers cannot add an unknown direct <property> to serverconfig.xml because
        // base 3.1 validates those properties before mods are loaded and aborts on unknown names.
        // REBIRTH therefore reads its nested <RebirthSettings> section independently after the
        // native config has been accepted. That server-owned code overrides the persisted world
        // snapshot on every dedicated-server start, matching the authority model of base prefs.
        if (GameManager.IsDedicatedServer)
        {
            string configuredCode;
            string configuredSource;
            if (RebirthDedicatedServerSandboxConfig.TryLoadCode(out configuredCode, out configuredSource))
            {
                int revision = hasCurrentWorld ? Math.Max(0, currentWorld.Revision) : 0;
                if (!hasCurrentWorld || !string.Equals(currentWorld.Code, configuredCode, StringComparison.Ordinal))
                    revision = IncrementRevision(revision);

                RebirthSandboxPreset exactPreset = RebirthSandboxOptionManager.Current.GetPresetByCode(configuredCode);
                data = new RebirthSandboxSaveData
                {
                    PresetName = exactPreset != null ? exactPreset.Name : "DedicatedServer",
                    Code = configuredCode,
                    Revision = revision
                };

                try
                {
                    SaveCurrentWorld(data);
                }
                catch (Exception ex)
                {
                    Log.Warning("[RebirthSandbox] Failed to persist dedicated-server option snapshot: "
                        + ex.GetType().Name + ": " + ex.Message);
                }

                source = configuredSource;
                return data;
            }
        }

        if (hasCurrentWorld)
        {
            source = "current-world";
            return currentWorld;
        }

        string saveDirectory = GameIO.GetSaveGameDir();
        string mainWorldPath = string.IsNullOrEmpty(saveDirectory)
            ? string.Empty
            : Path.Combine(saveDirectory, "main.ttw");
        bool existingWorld = !string.IsNullOrEmpty(mainWorldPath) && File.Exists(mainWorldPath);

        if (!existingWorld)
        {
            if (RebirthSandboxUiSession.TryGetPendingSnapshot(out data))
            {
                source = "new-save-pending-menu";
            }
            else
            {
                data = RebindSnapshotToCurrentPreset(LoadLastUsedOrDefault());
                source = "new-save-last-used";
            }

            try
            {
                SaveCurrentWorld(data);
            }
            catch (Exception ex)
            {
                Log.Warning("[RebirthSandbox] Failed to seed new save options before XML load: "
                    + ex.GetType().Name + ": " + ex.Message);
            }

            return data;
        }

        source = "legacy-world-base-game";
        return CreateLegacyWorldDefault();
    }

    private static RebirthSandboxSaveData RebindSnapshotToCurrentPreset(RebirthSandboxSaveData data)
    {
        if (data == null)
            return CreateDefault();

        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(data.PresetName);
        if (preset == null || string.Equals(data.Code, preset.Code, StringComparison.Ordinal))
            return data;

        return new RebirthSandboxSaveData
        {
            PresetName = preset.Name,
            Code = preset.Code,
            Revision = data.Revision < int.MaxValue
                ? Math.Max(0, data.Revision) + 1
                : int.MaxValue
        };
    }

    public static RebirthSandboxSaveData LoadLastUsedOrDefault()
    {
        RebirthSandboxSaveData data;
        return TryLoad(GetLastUsedPath(), out data) ? data : CreateDefault();
    }

    public static bool TryLoadCurrentWorld(out RebirthSandboxSaveData data)
    {
        return TryLoad(GetCurrentWorldPath(), out data);
    }

    public static void SaveCurrentWorld(RebirthSandboxSaveData data)
    {
        Save(GetCurrentWorldPath(), data);
    }

    public static void SaveLastUsed(RebirthSandboxSaveData data)
    {
        Save(GetLastUsedPath(), data);
    }

    public static bool CurrentSaveDirectoryExists()
    {
        string path = GameIO.GetSaveGameDir();
        return !string.IsNullOrEmpty(path) && Directory.Exists(path);
    }

    public static string GetCurrentWorldPath()
    {
        return Path.Combine(GameIO.GetSaveGameDir(), WorldFileName);
    }

    public static string GetLastUsedPath()
    {
        return Path.Combine(GameIO.GetSaveGameRootDir(), LastUsedFileName);
    }

    private static int IncrementRevision(int value)
    {
        value = Math.Max(0, value);
        return value < int.MaxValue ? value + 1 : value;
    }

    /// <summary>
    /// True only after the base game has actually created the world save. The New Game UI
    /// can create the target save directory before START is pressed, so directory existence
    /// must never be used as the progression-lock signal.
    /// </summary>
    public static bool CurrentWorldHasStarted()
    {
        string saveDirectory = GameIO.GetSaveGameDir();
        return !string.IsNullOrEmpty(saveDirectory)
            && File.Exists(Path.Combine(saveDirectory, "main.ttw"));
    }

    private static RebirthSandboxSaveData CreateLegacyWorldDefault()
    {
        RebirthSandboxState state = new RebirthSandboxState
        {
            PlayerProgression = RebirthPlayerProgressionMode.BaseGame
        };
        return new RebirthSandboxSaveData
        {
            PresetName = "LegacyWorld",
            Code = RebirthSandboxOptionManager.Encode(state),
            Revision = 0
        };
    }

    private static RebirthSandboxSaveData CreateDefault()
    {
        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);
        return new RebirthSandboxSaveData
        {
            PresetName = RebirthSandboxOptionManager.DefaultPresetName,
            Code = preset != null ? preset.Code : RebirthSandboxOptionManager.Encode(new RebirthSandboxState()),
            Revision = 0
        };
    }

    private static bool TryLoad(string path, out RebirthSandboxSaveData data)
    {
        data=null;
        if(string.IsNullOrEmpty(path))return false;
        string error;
        if(TryLoadOne(path,out data,out error))return true;
        string backup=path+".bak";
        RebirthSandboxSaveData recovered;string backupError;
        if(TryLoadOne(backup,out recovered,out backupError))
        {
            data=recovered;
            Log.Warning("[RebirthSandbox] Recovered options from backup after primary load failure: "+(error??"missing primary"));
            return true;
        }
        if(!string.IsNullOrEmpty(error))Log.Warning("[RebirthSandbox] Failed to load options file '"+path+"': "+error);
        return false;
    }
    private static bool TryLoadOne(string path,out RebirthSandboxSaveData data,out string error)
    {
        data=null;error=null;
        try
        {
            if(string.IsNullOrEmpty(path)||!File.Exists(path))return false;
            XmlDocument document=new XmlDocument();document.Load(path);XmlElement root=document.DocumentElement;
            if(root==null||root.Name!="rebirthSandbox"){error="invalid root";return false;}
            string code=root.GetAttribute("code");RebirthSandboxState ignored;
            if(!RebirthSandboxOptionManager.TryDecode(code,out ignored)){error="invalid option code";return false;}
            int revision;if(!int.TryParse(root.GetAttribute("revision"),out revision)||revision<0)revision=0;
            data=new RebirthSandboxSaveData{PresetName=root.GetAttribute("preset"),Code=code,Revision=revision};
            if(string.IsNullOrEmpty(data.PresetName))data.PresetName=RebirthSandboxOptionManager.DefaultPresetName;
            return true;
        }
        catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;return false;}
    }

    private static void Save(string path, RebirthSandboxSaveData data)
    {
        if (data == null)
            return;

        RebirthSandboxState ignored;
        if (!RebirthSandboxOptionManager.TryDecode(data.Code, out ignored))
            throw new InvalidDataException("Invalid RebirthSandboxCode.");

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthSandbox");
        root.SetAttribute("format", "1");
        root.SetAttribute("preset", data.PresetName ?? RebirthSandboxOptionManager.DefaultPresetName);
        root.SetAttribute("code", data.Code);
        root.SetAttribute("revision", Math.Max(0, data.Revision).ToString());
        document.AppendChild(root);

        string temporary=path+".tmp",backup=path+".bak";
        document.Save(temporary);
        RebirthSandboxSaveData verified;string verifyError;
        if(!TryLoadOne(temporary,out verified,out verifyError))throw new InvalidDataException("Staged sandbox options failed validation: "+verifyError);
        if(File.Exists(path))File.Copy(path,backup,true);
        File.Copy(temporary,path,true);
        File.Delete(temporary);
    }
}
