using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

#nullable disable

public enum RebirthSandboxOptionRandomizerMode
{
    Unlocked = 0,
    Locked = 1,
    Minimum = 2,
    Maximum = 3
}

/// <summary>
/// Local randomizer lock/minimum/maximum state for REBIRTH-owned sandbox options.
/// This is intentionally separate from RebirthSandboxCode and is never networked.
/// It mirrors the existing base-game sandbox randomizer lock behavior while using
/// REBIRTH option IDs and a separate persistence file.
/// </summary>
public static class RebirthSandboxRandomizerLockState
{
    private static readonly Dictionary<RebirthSandboxOptionId, RebirthSandboxOptionRandomizerMode> OptionModes =
        new Dictionary<RebirthSandboxOptionId, RebirthSandboxOptionRandomizerMode>();

    private static readonly Dictionary<RebirthSandboxOptionId, int> OptionAnchorIndices =
        new Dictionary<RebirthSandboxOptionId, int>();

    private static readonly object Sync = new object();
    private static bool loaded;

    public static RebirthSandboxOptionRandomizerMode GetMode(RebirthSandboxOptionId option)
    {
        EnsureLoaded();

        lock (Sync)
        {
            RebirthSandboxOptionRandomizerMode mode;
            if (OptionModes.TryGetValue(option, out mode))
                return mode;
        }

        return RebirthSandboxOptionRandomizerMode.Unlocked;
    }

    public static int GetAnchorIndex(RebirthSandboxOptionId option, int fallbackIndex)
    {
        EnsureLoaded();

        lock (Sync)
        {
            int anchorIndex;
            if (OptionAnchorIndices.TryGetValue(option, out anchorIndex) && anchorIndex >= 0)
                return anchorIndex;
        }

        return fallbackIndex;
    }

    public static void CycleMode(RebirthSandboxOptionId option, int selectedIndex)
    {
        EnsureLoaded();

        lock (Sync)
        {
            RebirthSandboxOptionRandomizerMode currentMode;
            if (!OptionModes.TryGetValue(option, out currentMode))
                currentMode = RebirthSandboxOptionRandomizerMode.Unlocked;

            RebirthSandboxOptionRandomizerMode nextMode;
            switch (currentMode)
            {
                case RebirthSandboxOptionRandomizerMode.Unlocked:
                    nextMode = RebirthSandboxOptionRandomizerMode.Locked;
                    break;
                case RebirthSandboxOptionRandomizerMode.Locked:
                    nextMode = RebirthSandboxOptionRandomizerMode.Minimum;
                    break;
                case RebirthSandboxOptionRandomizerMode.Minimum:
                    nextMode = RebirthSandboxOptionRandomizerMode.Maximum;
                    break;
                default:
                    nextMode = RebirthSandboxOptionRandomizerMode.Unlocked;
                    break;
            }

            if (nextMode == RebirthSandboxOptionRandomizerMode.Unlocked)
            {
                OptionModes.Remove(option);
                OptionAnchorIndices.Remove(option);
            }
            else
            {
                OptionModes[option] = nextMode;

                if ((nextMode == RebirthSandboxOptionRandomizerMode.Minimum ||
                     nextMode == RebirthSandboxOptionRandomizerMode.Maximum) && selectedIndex >= 0)
                {
                    OptionAnchorIndices[option] = selectedIndex;
                }
                else if (nextMode == RebirthSandboxOptionRandomizerMode.Locked)
                {
                    OptionAnchorIndices.Remove(option);
                }
            }
        }

        Save();
    }

    private static string GetFilePath()
    {
        string rootPath = Application.persistentDataPath;
        if (string.IsNullOrEmpty(rootPath))
            rootPath = Directory.GetCurrentDirectory();

        return Path.Combine(
            Path.Combine(Path.Combine(rootPath, "RebirthUtils"), "Randomizer"),
            "rebirth_sandbox_randomizer_locks.txt");
    }

    private static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (loaded)
                return;

            loaded = true;
            OptionModes.Clear();
            OptionAnchorIndices.Clear();

            string filePath = GetFilePath();
            if (!File.Exists(filePath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int formatVersion = 1;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                        continue;

                    line = line.Trim();
                    if (line.StartsWith("# FormatVersion=", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsedVersion;
                        if (int.TryParse(line.Substring("# FormatVersion=".Length).Trim(), out parsedVersion) && parsedVersion > 0)
                            formatVersion = parsedVersion;
                        continue;
                    }

                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        continue;

                    ParseLine(line, formatVersion);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[Rebirth] Failed to load REBIRTH sandbox randomizer mode state: " + ex.Message);
            }
        }
    }

    private static void ParseLine(string line, int formatVersion)
    {
        int separatorIndex = line.IndexOf('=');
        if (separatorIndex < 0)
            separatorIndex = line.IndexOf(',');

        string optionName = separatorIndex >= 0 ? line.Substring(0, separatorIndex).Trim() : line.Trim();
        string modeName = separatorIndex >= 0 ? line.Substring(separatorIndex + 1).Trim() : string.Empty;
        int anchorIndex = -1;

        RebirthSandboxOptionId option;
        if (!Enum.TryParse(optionName, true, out option) || !Enum.IsDefined(typeof(RebirthSandboxOptionId),option))
            return;

        RebirthSandboxOptionRandomizerMode mode = RebirthSandboxOptionRandomizerMode.Locked;
        if (!string.IsNullOrEmpty(modeName))
        {
            int anchorSeparatorIndex = modeName.IndexOf('|');
            if (anchorSeparatorIndex >= 0)
            {
                string anchorText = modeName.Substring(anchorSeparatorIndex + 1).Trim();
                modeName = modeName.Substring(0, anchorSeparatorIndex).Trim();
                if (!int.TryParse(anchorText, out anchorIndex))
                    anchorIndex = -1;
            }

            // v69 inserts Fog Intensity=None at combo index 0. Files written before
            // the format marker stored Very Low..Very High as indices 0..4.
            if (formatVersion < 2 && option == RebirthSandboxOptionId.WeatherFogIntensity &&
                anchorIndex >= 0 && anchorIndex <= 4)
            {
                anchorIndex++;
            }

            // v74 removes the custom MaxJobs=0 combo entry. Format v1/v2 stored
            // 0,5,13,20 at indices 0..3. Preserve the low/middle/high anchor slots by
            // folding old 0/5 to new index 0 and shifting the old middle/high slots down.
            // v76 retunes those same three visible slots to 5/11/17, so no further
            // randomizer-anchor index migration is required.
            if (formatVersion < 3 && option == RebirthSandboxOptionId.MaxJobs &&
                anchorIndex >= 0 && anchorIndex <= 3)
            {
                anchorIndex = anchorIndex <= 1 ? 0 : anchorIndex - 1;
            }

            if (!Enum.TryParse(modeName, true, out mode) || !Enum.IsDefined(typeof(RebirthSandboxOptionRandomizerMode),mode))
                mode = RebirthSandboxOptionRandomizerMode.Locked;
        }

        if (mode == RebirthSandboxOptionRandomizerMode.Unlocked)
            return;

        OptionModes[option] = mode;
        if ((mode == RebirthSandboxOptionRandomizerMode.Minimum ||
             mode == RebirthSandboxOptionRandomizerMode.Maximum) && anchorIndex >= 0)
        {
            OptionAnchorIndices[option] = anchorIndex;
        }
    }

    private static void Save()
    {
        lock (Sync)
        {
            try
            {
                string filePath = GetFilePath();
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                List<RebirthSandboxOptionId> options = new List<RebirthSandboxOptionId>(OptionModes.Keys);
                options.Sort();

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# REBIRTH sandbox randomizer option modes");
                builder.AppendLine("# FormatVersion=3");
                builder.AppendLine("# Format: RebirthSandboxOptionId=Locked|Minimum|Maximum, with optional |anchorIndex for Minimum/Maximum");

                for (int i = 0; i < options.Count; i++)
                {
                    RebirthSandboxOptionId option = options[i];
                    RebirthSandboxOptionRandomizerMode mode;
                    if (!OptionModes.TryGetValue(option, out mode) || mode == RebirthSandboxOptionRandomizerMode.Unlocked)
                        continue;

                    builder.Append(option.ToString());
                    builder.Append('=');
                    builder.Append(mode.ToString());

                    int anchorIndex;
                    if ((mode == RebirthSandboxOptionRandomizerMode.Minimum ||
                         mode == RebirthSandboxOptionRandomizerMode.Maximum) &&
                        OptionAnchorIndices.TryGetValue(option, out anchorIndex) && anchorIndex >= 0)
                    {
                        builder.Append('|');
                        builder.Append(anchorIndex);
                    }

                    builder.AppendLine();
                }

                File.WriteAllText(filePath, builder.ToString());
            }
            catch (Exception ex)
            {
                Log.Warning("[Rebirth] Failed to save REBIRTH sandbox randomizer mode state: " + ex.Message);
            }
        }
    }
}
