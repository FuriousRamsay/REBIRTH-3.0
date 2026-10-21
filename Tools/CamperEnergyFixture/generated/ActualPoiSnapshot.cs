using System;using System.IO;using System.Linq;using System.Text;
public sealed class RebirthCamperEnergyPoiSnapshot
{
    public bool InRange {get;internal set;}
    public string PoiName {get;internal set;}
    public int SupportedLights {get;internal set;}
    public bool IsOn {get;internal set;}
    public float Range {get;internal set;}
    public int DemandWatts {get;internal set;}
    // Prospective load is distinct from currently powered/metered demand.
    // Older observation packets leave this unknown and cannot enable ON.
    public int RequiredWatts {get;internal set;} = -1;
}

// Authoritative native electrical consumers and existing circuit roots. Never
// reparent wires, simulate a source bank, replace ammunition or change targeting.
