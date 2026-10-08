using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Data binding only. No completed output, authenticated authority, storage, or migration is inferred.
public sealed class RebirthStationRecipeDiscoveryRecord
{
    private readonly XElement image;
    public string CanonicalRecipe => (string)image.Attribute("recipe");
    public string JobId => (string)image.Attribute("job");
    private RebirthStationRecipeDiscoveryRecord(XElement node) { image = new XElement(node); }
    public XElement Write() => new XElement(image);
    public RebirthStationRecipeDiscoveryRecord Clone() => new RebirthStationRecipeDiscoveryRecord(image);
    private static bool Terminal(string value) => value != null && value.Length == 64
        && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
    private static bool RecipeName(string value)
    {
        if (string.IsNullOrEmpty(value) || value != value.Trim() || value.Any(char.IsControl)) return false;
        try { return new UTF8Encoding(false, true).GetByteCount(value) <= 1024; }
        catch { return false; }
    }
    // Terminal digest is opaque pending original producer qualification; hashes never prove authority.
    public static bool TryCreate(RebirthStationDiscoveryWitness witness, string canonicalRecipe,
        string terminalDigest, out RebirthStationRecipeDiscoveryRecord record)
    {
        record = null;
        if (witness == null) return false;
        try
        {
            var w = witness.Write();
            var node = new XElement("stationRecipeDiscovery", new XAttribute("version", "1"),
                new XAttribute("recipe", canonicalRecipe ?? ""), new XAttribute("terminal", terminalDigest ?? ""),
                new XAttribute("job", (string)w.Attribute("job")), new XAttribute("creation", (string)w.Attribute("creation")),
                new XAttribute("owner", (string)w.Attribute("owner")), new XAttribute("definition", (string)w.Attribute("definition")),
                new XAttribute("admission", (string)w.Attribute("admission")), w);
            return TryRead(node, witness, canonicalRecipe, terminalDigest, out record);
        }
        catch { return false; }
    }
    public static bool TryRead(XElement node, RebirthStationDiscoveryWitness expectedWitness,
        string expectedCanonicalRecipe, string expectedTerminalDigest, out RebirthStationRecipeDiscoveryRecord record)
    {
        record = null;
        if (node == null || expectedWitness == null || node.Name != "stationRecipeDiscovery"
            || node.Attributes().Count() != 8 || node.Elements().Count() != 1
            || node.Nodes().Any(n => !(n is XElement) && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)))) return false;
        try
        {
            var w = expectedWitness.Write();
            if (!RecipeName(expectedCanonicalRecipe) || !Terminal(expectedTerminalDigest)
                || (string)node.Attribute("version") != "1" || (string)node.Attribute("recipe") != expectedCanonicalRecipe
                || (string)node.Attribute("terminal") != expectedTerminalDigest
                || !XNode.DeepEquals(node.Element("stationDiscoveryWitness"), w)) return false;
            foreach (string key in new[] { "job", "creation", "owner", "definition", "admission" })
                if ((string)node.Attribute(key) != (string)w.Attribute(key)) return false;
            // Bound the entire UTF8 record; never truncate a recipe, identity, or receipt.
            if (new UTF8Encoding(false, true).GetByteCount(node.ToString(SaveOptions.DisableFormatting)) > 8192) return false;
            record = new RebirthStationRecipeDiscoveryRecord(node);
            return true;
        }
        catch { return false; }
    }
    // Detached syntax and parent-child binding only; no active queue admission is required or inferred.
    public static bool TryReadStored(XElement node, out RebirthStationRecipeDiscoveryRecord record)
    {
        record = null;
        if (node == null || node.Name != "stationRecipeDiscovery" || node.Attributes().Count() != 8
            || node.Elements().Count() != 1
            || node.Nodes().Any(n => !(n is XElement) && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)))) return false;
        try
        {
            RebirthStationDiscoveryWitness witness;
            if ((string)node.Attribute("version") != "1" || !RecipeName((string)node.Attribute("recipe"))
                || !Terminal((string)node.Attribute("terminal"))
                || !RebirthStationDiscoveryWitness.TryReadStored(node.Element("stationDiscoveryWitness"), out witness)) return false;
            var w = witness.Write();
            foreach (string key in new[] { "job", "creation", "owner", "definition", "admission" })
                if ((string)node.Attribute(key) != (string)w.Attribute(key)) return false;
            if (new UTF8Encoding(false, true).GetByteCount(node.ToString(SaveOptions.DisableFormatting)) > 8192) return false;
            record = new RebirthStationRecipeDiscoveryRecord(node);
            return true;
        }
        catch { return false; }
    }
}
