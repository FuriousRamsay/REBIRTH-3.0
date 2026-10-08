// REBIRTH 3.0 Randomizer feature
// Source: adapted from zzz_RebirthModsLight sandbox option randomizer.
// Keeps the original single iconbutton selected-state visual method and extends it with anchored min/max modes.

using SandboxOptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

using SandboxOptionId = SandboxOptions.SandboxOptions;

public enum RebirthSandboxRandomizerMode
{
    Unlocked = 0,
    Locked = 1,
    Minimum = 2,
    Maximum = 3
}

[Preserve]
public class XUiC_SandBoxOptionEntryRandomizer : XUiC_SandBoxOptionEntry
{
    private const string LockedSprite = "ui_game_symbol_lock";
    private const string UnlockedSprite = "ui_game_symbol_unlock";

    private static readonly Dictionary<SandboxOptionId, RebirthSandboxRandomizerMode> OptionModes = new Dictionary<SandboxOptionId, RebirthSandboxRandomizerMode>();
    private static readonly Dictionary<SandboxOptionId, int> OptionAnchorIndices = new Dictionary<SandboxOptionId, int>();
    private static readonly Dictionary<SandboxOptionId, string> OptionAnchorValues = new Dictionary<SandboxOptionId, string>();
    private static readonly object LockStateSync = new object();
    private static readonly object PersistenceSync = new object();
    private static int lockStateLoadState; // 0=not attempted, 1=loaded/missing defaults, 2=failed/retryable
    private static float nextLoadRetryRealtime;
    private static string lastPersistenceError = string.Empty;
    public static bool HasPersistenceError { get { lock (LockStateSync) return lockStateLoadState == 2 || !string.IsNullOrEmpty(lastPersistenceError); } }
    public static string LastPersistenceError { get { lock (LockStateSync) return lastPersistenceError ?? string.Empty; } }

    [XuiBindComponent("btnRandomizerLock", true)]
    public readonly XUiC_Button btnRandomizerLock;

    [XuiXmlBinding("randomizer_locked")]
    public bool RandomizerLocked
    {
        get { return this.HasEntry && GetMode(this.sandboxOption) != RebirthSandboxRandomizerMode.Unlocked; }
    }

    [XuiXmlBinding("randomizer_lock_sprite")]
    public string RandomizerLockSprite
    {
        get { return this.RandomizerLocked ? LockedSprite : UnlockedSprite; }
    }

    [XuiXmlBinding("randomizer_lock_is_minimum")]
    public bool RandomizerLockIsMinimum
    {
        get { return this.HasEntry && GetMode(this.sandboxOption) == RebirthSandboxRandomizerMode.Minimum; }
    }

    [XuiXmlBinding("randomizer_lock_is_maximum")]
    public bool RandomizerLockIsMaximum
    {
        get { return this.HasEntry && GetMode(this.sandboxOption) == RebirthSandboxRandomizerMode.Maximum; }
    }

    public static bool IsLocked(SandboxOptionId option)
    {
        return GetMode(option) == RebirthSandboxRandomizerMode.Locked;
    }

    public static bool IsMinimumLocked(SandboxOptionId option)
    {
        return GetMode(option) == RebirthSandboxRandomizerMode.Minimum;
    }

    public static bool IsMaximumLocked(SandboxOptionId option)
    {
        return GetMode(option) == RebirthSandboxRandomizerMode.Maximum;
    }

    public static RebirthSandboxRandomizerMode GetMode(SandboxOptionId option)
    {
        if (option == SandboxOptionId.Max)
            return RebirthSandboxRandomizerMode.Unlocked;

        EnsureLockStateLoaded();

        lock (LockStateSync)
        {
            if (OptionModes.TryGetValue(option, out RebirthSandboxRandomizerMode mode))
                return mode;
        }

        return RebirthSandboxRandomizerMode.Unlocked;
    }

    public static int GetAnchorIndex(SandboxOptionId option, int fallbackIndex)
    {
        if (option == SandboxOptionId.Max) return fallbackIndex;
        EnsureLockStateLoaded();
        lock (LockStateSync)
        {
            int anchorIndex;
            if (OptionAnchorIndices.TryGetValue(option, out anchorIndex) && anchorIndex >= 0) return anchorIndex;
        }
        return fallbackIndex;
    }

    public static int GetAnchorIndex(SandboxOptionId option, XUiC_SandBoxOptionEntry entry, int fallbackIndex)
    {
        if (option == SandboxOptionId.Max || entry == null || entry.controlCombo == null || entry.controlCombo.Elements == null) return fallbackIndex;
        EnsureLockStateLoaded();
        string stableValue = null; int legacyIndex = -1;
        lock (LockStateSync)
        {
            OptionAnchorValues.TryGetValue(option, out stableValue);
            OptionAnchorIndices.TryGetValue(option, out legacyIndex);
        }
        if (!string.IsNullOrEmpty(stableValue))
        {
            for (int i=0;i<entry.controlCombo.Elements.Count;i++)
                if (string.Equals(entry.controlCombo.Elements[i].ToString(), stableValue, StringComparison.Ordinal)) return i;
            Log.Warning("[Rebirth] Randomizer anchor value no longer exists for " + option + "; using current value instead of a stale index.");
            return fallbackIndex;
        }
        if (legacyIndex >= 0 && legacyIndex < entry.controlCombo.Elements.Count) return legacyIndex;
        return fallbackIndex;
    }

    public static void SetMode(SandboxOptionId option, RebirthSandboxRandomizerMode mode)
    { SetMode(option, mode, -1, null); }

    public static void SetMode(SandboxOptionId option, RebirthSandboxRandomizerMode mode, int anchorIndex)
    { SetMode(option, mode, anchorIndex, null); }

    private static void SetMode(SandboxOptionId option, RebirthSandboxRandomizerMode mode, int anchorIndex, string anchorValue)
    {
        if (option == SandboxOptionId.Max || !Enum.IsDefined(typeof(SandboxOptionId), option) || !Enum.IsDefined(typeof(RebirthSandboxRandomizerMode), mode)) return;
        EnsureLockStateLoaded();
        lock (PersistenceSync)
        {
            Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> modes; Dictionary<SandboxOptionId,int> anchors; Dictionary<SandboxOptionId,string> values;
            lock (LockStateSync)
            {
                if (lockStateLoadState != 1) { Log.Warning("[Rebirth] Randomizer preference edit rejected because persisted state is not safely loaded."); return; }
                modes = new Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode>(OptionModes);
                anchors = new Dictionary<SandboxOptionId,int>(OptionAnchorIndices);
                values = new Dictionary<SandboxOptionId,string>(OptionAnchorValues);
            }
            RebirthSandboxRandomizerMode currentMode; modes.TryGetValue(option,out currentMode);
            int currentAnchor=-1; anchors.TryGetValue(option,out currentAnchor); string currentValue=null; values.TryGetValue(option,out currentValue);
            bool anchorMode=mode==RebirthSandboxRandomizerMode.Minimum||mode==RebirthSandboxRandomizerMode.Maximum;
            if(mode==RebirthSandboxRandomizerMode.Unlocked){modes.Remove(option);anchors.Remove(option);values.Remove(option);}
            else{modes[option]=mode;if(anchorMode&&anchorIndex>=0){anchors[option]=anchorIndex;if(!string.IsNullOrEmpty(anchorValue))values[option]=anchorValue;else values.Remove(option);}else{anchors.Remove(option);values.Remove(option);}}
            int newAnchor=-1;anchors.TryGetValue(option,out newAnchor);string newValue=null;values.TryGetValue(option,out newValue);
            if(currentMode==mode&&currentAnchor==newAnchor&&string.Equals(currentValue,newValue,StringComparison.Ordinal))return;
            string error;
            if(!TrySaveLockState(modes,anchors,values,out error)){lock(LockStateSync)lastPersistenceError=error;Log.Warning("[Rebirth] Randomizer preference change was not published because durable save failed: "+error);return;}
            lock(LockStateSync){OptionModes.Clear();foreach(var kv in modes)OptionModes[kv.Key]=kv.Value;OptionAnchorIndices.Clear();foreach(var kv in anchors)OptionAnchorIndices[kv.Key]=kv.Value;OptionAnchorValues.Clear();foreach(var kv in values)OptionAnchorValues[kv.Key]=kv.Value;lastPersistenceError=string.Empty;}
        }
    }

    public static void SetLocked(SandboxOptionId option, bool locked)
    {
        SetMode(option, locked ? RebirthSandboxRandomizerMode.Locked : RebirthSandboxRandomizerMode.Unlocked);
    }

    public static void ToggleLocked(SandboxOptionId option)
    {
        CycleMode(option, -1);
    }

    public static void CycleMode(SandboxOptionId option)
    {
        CycleMode(option, -1);
    }

    public static void CycleMode(SandboxOptionId option, int selectedIndex)
    { CycleMode(option, selectedIndex, null); }

    private static void CycleMode(SandboxOptionId option, int selectedIndex, string selectedValue)
    {
        if (option == SandboxOptionId.Max || !Enum.IsDefined(typeof(SandboxOptionId), option)) return;
        RebirthSandboxRandomizerMode currentMode=GetMode(option);
        RebirthSandboxRandomizerMode nextMode;
        switch(currentMode){case RebirthSandboxRandomizerMode.Unlocked:nextMode=RebirthSandboxRandomizerMode.Locked;break;case RebirthSandboxRandomizerMode.Locked:nextMode=RebirthSandboxRandomizerMode.Minimum;break;case RebirthSandboxRandomizerMode.Minimum:nextMode=RebirthSandboxRandomizerMode.Maximum;break;default:nextMode=RebirthSandboxRandomizerMode.Unlocked;break;}
        SetMode(option,nextMode,selectedIndex,selectedValue);
    }

    [XuiBindEvent("OnPress", "btnRandomizerLock")]
    public void BtnRandomizerLock_OnPressed(XUiController _sender, int _mouseButton)
    {
        if (!this.HasEntry || this.sandboxOption == SandboxOptionId.Max)
            return;

        int selectedIndex = GetCurrentSelectedIndex();
        string selectedValue = selectedIndex >= 0 && this.controlCombo != null && this.controlCombo.Elements != null && selectedIndex < this.controlCombo.Elements.Count ? this.controlCombo.Elements[selectedIndex].ToString() : null;
        CycleMode(this.sandboxOption, selectedIndex, selectedValue);

        if (_sender != null)
            _sender.IsDirty = true;

        this.IsDirty = true;
    }

    private int GetCurrentSelectedIndex()
    {
        if (this.controlCombo == null || this.controlCombo.Elements == null || this.controlCombo.Elements.Count <= 0)
            return -1;

        int selectedIndex = this.controlCombo.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < this.controlCombo.Elements.Count)
            return selectedIndex;

        if (this.Option == null)
            return -1;

        int defaultIndex = this.Option.GetDefaultIndex();
        return defaultIndex >= 0 && defaultIndex < this.controlCombo.Elements.Count ? defaultIndex : -1;
    }

    private static string GetLockStateFilePath()
    {
        string rootPath = Application.persistentDataPath;
        if (string.IsNullOrEmpty(rootPath))
            rootPath = Directory.GetCurrentDirectory();

        return Path.Combine(Path.Combine(Path.Combine(rootPath, "RebirthUtils"), "Randomizer"), "sandbox_randomizer_locks.txt");
    }

    private static void EnsureLockStateLoaded()
    {
        lock(LockStateSync){if(lockStateLoadState==1)return;if(lockStateLoadState==2&&Time.realtimeSinceStartup<nextLoadRetryRealtime)return;}
        lock(PersistenceSync)
        {
            lock(LockStateSync){if(lockStateLoadState==1)return;if(lockStateLoadState==2&&Time.realtimeSinceStartup<nextLoadRetryRealtime)return;}
            string filePath=GetLockStateFilePath();
            Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> modes=new Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode>();
            Dictionary<SandboxOptionId,int> anchors=new Dictionary<SandboxOptionId,int>();
            Dictionary<SandboxOptionId,string> values=new Dictionary<SandboxOptionId,string>();
            try
            {
                if(File.Exists(filePath))
                {
                    string[] lines=File.ReadAllLines(filePath);
                    for(int i=0;i<lines.Length;i++){string line=lines[i];if(string.IsNullOrWhiteSpace(line))continue;line=line.Trim();if(line.StartsWith("#",StringComparison.Ordinal))continue;ParseSavedModeLine(line,modes,anchors,values);}
                }
                lock(LockStateSync){OptionModes.Clear();foreach(var kv in modes)OptionModes[kv.Key]=kv.Value;OptionAnchorIndices.Clear();foreach(var kv in anchors)OptionAnchorIndices[kv.Key]=kv.Value;OptionAnchorValues.Clear();foreach(var kv in values)OptionAnchorValues[kv.Key]=kv.Value;lockStateLoadState=1;lastPersistenceError=string.Empty;nextLoadRetryRealtime=0f;}
            }
            catch(Exception ex)
            {
                string error=ex.GetType().Name+": "+ex.Message;lock(LockStateSync){lockStateLoadState=2;lastPersistenceError=error;nextLoadRetryRealtime=Time.realtimeSinceStartup+5f;}Log.Warning("[Rebirth] Failed to load sandbox randomizer mode state; will retry: "+error);
            }
        }
    }

    private static void ParseSavedModeLine(string line, Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> modes, Dictionary<SandboxOptionId,int> anchors, Dictionary<SandboxOptionId,string> values)
    {
        string optionName=line,modeName=null;int anchorIndex=-1;string anchorValue=null;int separatorIndex=line.IndexOf('=');if(separatorIndex<0)separatorIndex=line.IndexOf(',');if(separatorIndex>=0){optionName=line.Substring(0,separatorIndex).Trim();modeName=line.Substring(separatorIndex+1).Trim();}
        SandboxOptionId option;if(!Enum.TryParse(optionName,true,out option)||!Enum.IsDefined(typeof(SandboxOptionId),option)||option==SandboxOptionId.Max)return;
        RebirthSandboxRandomizerMode mode=RebirthSandboxRandomizerMode.Locked;
        if(!string.IsNullOrEmpty(modeName))
        {
            string[] parts=modeName.Split('|');modeName=parts[0].Trim();if(!Enum.TryParse(modeName,true,out mode)||!Enum.IsDefined(typeof(RebirthSandboxRandomizerMode),mode)){Log.Warning("[Rebirth] Ignoring invalid randomizer mode for "+option+": "+modeName);return;}
            if(parts.Length>1&&!int.TryParse(parts[1],out anchorIndex))anchorIndex=-1;
            if(parts.Length>2&&!string.IsNullOrEmpty(parts[2])){try{anchorValue=Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));}catch{Log.Warning("[Rebirth] Ignoring invalid randomizer anchor value for "+option);anchorValue=null;}}
        }
        if(mode==RebirthSandboxRandomizerMode.Unlocked){modes.Remove(option);anchors.Remove(option);values.Remove(option);return;}
        modes[option]=mode;bool anchorMode=mode==RebirthSandboxRandomizerMode.Minimum||mode==RebirthSandboxRandomizerMode.Maximum;if(anchorMode&&anchorIndex>=0){anchors[option]=anchorIndex;if(!string.IsNullOrEmpty(anchorValue))values[option]=anchorValue;}else{anchors.Remove(option);values.Remove(option);}
    }

    private static void SaveLockState()
    {
        EnsureLockStateLoaded();
        lock(PersistenceSync)
        {
            Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> modes;Dictionary<SandboxOptionId,int> anchors;Dictionary<SandboxOptionId,string> values;
            lock(LockStateSync){if(lockStateLoadState!=1)return;modes=new Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode>(OptionModes);anchors=new Dictionary<SandboxOptionId,int>(OptionAnchorIndices);values=new Dictionary<SandboxOptionId,string>(OptionAnchorValues);}
            string error;if(!TrySaveLockState(modes,anchors,values,out error)){lock(LockStateSync)lastPersistenceError=error;Log.Warning("[Rebirth] Failed to save sandbox randomizer mode state: "+error);}else lock(LockStateSync)lastPersistenceError=string.Empty;
        }
    }

    private static bool TrySaveLockState(Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> modes,Dictionary<SandboxOptionId,int> anchors,Dictionary<SandboxOptionId,string> values,out string error)
    {
        error=string.Empty;string filePath=GetLockStateFilePath();string temp=filePath+".tmp";
        try
        {
            string directory=Path.GetDirectoryName(filePath);if(!string.IsNullOrEmpty(directory)&&!Directory.Exists(directory))Directory.CreateDirectory(directory);if(File.Exists(temp))File.Delete(temp);
            List<SandboxOptionId> options=new List<SandboxOptionId>(modes.Keys);options.Sort();StringBuilder builder=new StringBuilder();builder.AppendLine("# REBIRTH 3.0 sandbox randomizer option modes v2");builder.AppendLine("# Format: Option=Mode|legacyIndex|base64StableChoice. Old Option=Mode|index lines remain readable.");
            for(int i=0;i<options.Count;i++){SandboxOptionId option=options[i];if(option==SandboxOptionId.Max||!Enum.IsDefined(typeof(SandboxOptionId),option))continue;RebirthSandboxRandomizerMode mode;if(!modes.TryGetValue(option,out mode)||mode==RebirthSandboxRandomizerMode.Unlocked||!Enum.IsDefined(typeof(RebirthSandboxRandomizerMode),mode))continue;builder.Append(option).Append('=').Append(mode);if(mode==RebirthSandboxRandomizerMode.Minimum||mode==RebirthSandboxRandomizerMode.Maximum){int anchor=-1;anchors.TryGetValue(option,out anchor);builder.Append('|').Append(anchor);string value;if(values.TryGetValue(option,out value)&&!string.IsNullOrEmpty(value))builder.Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));}builder.AppendLine();}
            using(FileStream stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))using(StreamWriter writer=new StreamWriter(stream,new UTF8Encoding(false))){writer.Write(builder.ToString());writer.Flush();stream.Flush();}
            string[] staged=File.ReadAllLines(temp);Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode> verifyM=new Dictionary<SandboxOptionId,RebirthSandboxRandomizerMode>();Dictionary<SandboxOptionId,int> verifyA=new Dictionary<SandboxOptionId,int>();Dictionary<SandboxOptionId,string> verifyV=new Dictionary<SandboxOptionId,string>();for(int i=0;i<staged.Length;i++){string line=staged[i].Trim();if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;ParseSavedModeLine(line,verifyM,verifyA,verifyV);}
            if(verifyM.Count!=modes.Count)throw new InvalidDataException("staged randomizer preference verification count mismatch");
            string publishError;if(!RebirthDurableFileCommit.TryPublish(temp,filePath,out publishError))throw new IOException("durable publish failed: "+publishError);return true;
        }catch(Exception ex){error=ex.GetType().Name+": "+ex.Message;try{if(File.Exists(temp))File.Delete(temp);}catch{}return false;}
    }
}
