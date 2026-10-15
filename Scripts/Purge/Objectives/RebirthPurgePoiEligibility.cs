using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// Shared authored policy. Runtime room readiness is intentionally not an exclusion.
internal static class RebirthPurgePoiEligibility
{
    private static HashSet<string> excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    internal static bool Available { get; private set; }
    internal static bool Allows(string name, bool trader, bool rwgOnly, bool streetTile)
        => Available && !string.IsNullOrWhiteSpace(name) && !trader && !(rwgOnly && streetTile) && !excluded.Contains(name.Trim());
    internal static void Load(string modRoot)
    {
        Available = false;
        var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var reader = XmlReader.Create(Path.Combine(modRoot,"Config","_purge_eligibility.xml"),
                new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=65536 }))
            {
                var root = XElement.Load(reader);
                if (root.Name != "purge_eligibility" || (string)root.Attribute("version") != "1") throw new InvalidDataException("Invalid eligibility definitions.");
                foreach (var node in root.Elements())
                {
                    string name = (string)node.Attribute("name");
                    if (node.Name != "exclude" || string.IsNullOrWhiteSpace(name) || name.Length > 256 || next.Count >= 4096)
                        throw new InvalidDataException("Invalid excluded prefab name.");
                    next.Add(name.Trim());
                }
            }
            excluded = next; Available = true;
        }
        catch (Exception error) { Log.Warning("[REBIRTH Purge] POI eligibility unavailable: " + error.Message); }
    }
}
