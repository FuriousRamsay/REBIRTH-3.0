using System;
using System.Collections.Generic;
partial class RebirthVehicleTuningSession {
    internal sealed class Setting
    {
        internal string Name, Original, Value, Xml;
        internal bool ReadOnly, ResolvedXml, Tuned;
        internal float Min,Max;
        internal Action<string> Write;
        internal Action Restore;
        internal Func<string> Read;
    }
 internal readonly List<Setting> Settings=new();
    internal void ReapplyTunedValues()
    {
        // Native vehicle updates can restore values from the vehicle definition while driving.
        // Keep only settings the player changed in this debug session at their chosen values.
        foreach(var row in Settings)
            if(row.Tuned&&row.Write!=null&&row.Read()!=row.Value)row.Write(row.Value);
    }
}
