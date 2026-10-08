using System;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Xml;
using System.Text;
using System.Xml.Linq;

// Composed immutable appearance choices, not another NPC identity or catalogue owner.
// This component records qualified choices; it never generates or repairs an appearance.
internal sealed class RebirthNpcResolvedAppearance : IEquatable<RebirthNpcResolvedAppearance>
{
    internal const int SchemaVersion = 1;
    internal const int MaximumIdentifierBytes = 256;
    internal const int MaximumPayloadCharacters = 8192;
    private static readonly string[] Fields = { "version", "pipeline", "legacySeed", "archetype", "catalogue", "generator", "model", "sex", "race", "variant", "eye", "hair", "hairColour", "mustache", "chops", "beard" };
    private readonly XElement image;
    private RebirthNpcResolvedAppearance(XElement node) { image = new XElement("resolvedAppearance", Fields.Select(k => new XAttribute(k, (string)node.Attribute(k)))); }
    internal RebirthHumanNpcModelPipeline Pipeline => (string)image.Attribute("pipeline") == "SDCS" ? RebirthHumanNpcModelPipeline.SDCS : RebirthHumanNpcModelPipeline.Custom;
    internal int LegacySeed => int.Parse((string)image.Attribute("legacySeed"), CultureInfo.InvariantCulture);
    internal string Archetype => Value("archetype");
    internal string CatalogueId => Value("catalogue");
    internal string GeneratorId => Value("generator");
    internal string ModelId => Value("model");
    internal bool IsMale => Value("sex") == "male";
    internal string Race => Value("race");
    internal int Variant => int.Parse(Value("variant"), CultureInfo.InvariantCulture);
    internal string Eye => Value("eye");
    internal string Hair => Value("hair");
    internal string HairColour => Value("hairColour");
    internal string Mustache => Value("mustache");
    internal string Chops => Value("chops");
    internal string Beard => Value("beard");
    private string Value(string key) => (string)image.Attribute(key);
    internal XElement Write() => new XElement(image);

    internal static bool TryCreate(RebirthHumanNpcModelPipeline pipeline, int legacySeed, string archetype,
        string catalogueId, string generatorId, string modelId, bool isMale, string race, int variant,
        string eye, string hair, string hairColour, string mustache, string chops, string beard,
        out RebirthNpcResolvedAppearance descriptor)
    {
        descriptor = null;
        if (pipeline != RebirthHumanNpcModelPipeline.Custom && pipeline != RebirthHumanNpcModelPipeline.SDCS) return false;
        string[] values = { "1", pipeline.ToString(), legacySeed.ToString(CultureInfo.InvariantCulture), archetype,
            catalogueId, generatorId, modelId, isMale ? "male" : "female", race,
            variant.ToString(CultureInfo.InvariantCulture), eye, hair, hairColour, mustache, chops, beard };
        if (values.Any(v => v == null)) return false;
        var node = new XElement("resolvedAppearance");
        for (int i = 0; i < Fields.Length; i++) node.Add(new XAttribute(Fields[i], values[i]));
        return TryRead(node, out descriptor);
    }
    internal static bool TryRead(XElement node, out RebirthNpcResolvedAppearance descriptor)
    {
        descriptor = null;
        if (node == null || node.Name != "resolvedAppearance" || node.HasElements ||
            node.Attributes().Count() != Fields.Length || node.Attributes().Any(a => !Fields.Contains(a.Name.ToString())) ||
            node.Nodes().Any()) return false;
        if ((string)node.Attribute("version") != "1") return false;
        string mode = (string)node.Attribute("pipeline"), sex = (string)node.Attribute("sex");
        if ((mode != "SDCS" && mode != "Custom") || (sex != "male" && sex != "female")) return false;
        if (!CanonicalInt(node, "legacySeed", out _) || !CanonicalInt(node, "variant", out var variant)) return false;
        for (int i = 3; i < Fields.Length; i++)
            if (!Identifier((string)node.Attribute(Fields[i]))) return false;
        if (string.IsNullOrEmpty((string)node.Attribute("archetype")) || string.IsNullOrEmpty((string)node.Attribute("model")) ||
            string.IsNullOrEmpty((string)node.Attribute("generator"))) return false;
        if (mode == "SDCS")
        {
            if (variant <= 0 || string.IsNullOrEmpty((string)node.Attribute("catalogue")) ||
                string.IsNullOrEmpty((string)node.Attribute("race")) || string.IsNullOrEmpty((string)node.Attribute("eye"))) return false;
        }
        else if (variant != 0 || new[] { "catalogue", "race", "eye", "hair", "hairColour", "mustache", "chops", "beard" }
            .Any(k => !string.IsNullOrEmpty((string)node.Attribute(k)))) return false;
        var canonical = new RebirthNpcResolvedAppearance(node);
        if (canonical.Encode().Length > MaximumPayloadCharacters) return false;
        descriptor = canonical;
        return true;
    }
    internal string Encode() => image.ToString(SaveOptions.DisableFormatting);
    internal static bool TryDecode(string payload, out RebirthNpcResolvedAppearance descriptor)
    {
        descriptor = null;
        if (string.IsNullOrEmpty(payload) || payload.Length > MaximumPayloadCharacters) return false;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaximumPayloadCharacters };
            XDocument document;
            using (var input = new StringReader(payload))
            using (var reader = XmlReader.Create(input, settings)) document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Nodes().Any(n => n != document.Root && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)))) return false;
            return TryRead(document.Root, out descriptor);
        }
        catch (XmlException) { return false; }
        catch (ArgumentException) { return false; }
    }
    private static bool CanonicalInt(XElement node, string field, out int value)
    {
        string s = (string)node.Attribute(field);
        return int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && value.ToString(CultureInfo.InvariantCulture) == s;
    }
    private static bool Identifier(string value)
    {
        if (value == null || value != value.Trim() || value.Any(char.IsControl)) return false;
        try { XmlConvert.VerifyXmlChars(value); return new UTF8Encoding(false, true).GetByteCount(value) <= MaximumIdentifierBytes; }
        catch (EncoderFallbackException) { return false; }
        catch (XmlException) { return false; }
    }
    // Missing old-save component remains unresolved. Invalid or duplicate components never become a new random person.
    internal static bool TryReadOptional(XElement parent, out RebirthNpcResolvedAppearance descriptor)
    {
        descriptor = null;
        if (parent == null) return false;
        var nodes = parent.Elements().Where(e => e.Name.LocalName == "resolvedAppearance").Take(2).ToArray();
        return nodes.Length == 0 || (nodes.Length == 1 && TryRead(nodes[0], out descriptor));
    }
    // Caller must supply actual authority-qualified choices. Existing resolved choices always win and cannot reroll.
    internal static bool TryResolveOnce(RebirthNpcResolvedAppearance current, RebirthHumanNpcModelPipeline pipeline,
        int legacySeed, string archetype, RebirthNpcResolvedAppearance candidate, out RebirthNpcResolvedAppearance selected)
    {
        selected = null;
        var original = current ?? candidate;
        if (original == null || original.Pipeline != pipeline || original.LegacySeed != legacySeed ||
            !string.Equals(original.Archetype, archetype, StringComparison.Ordinal)) return false;
        selected = original; return true;
    }
    public bool Equals(RebirthNpcResolvedAppearance other) => other != null && Fields.All(k => string.Equals(Value(k), other.Value(k), StringComparison.Ordinal));
    public override bool Equals(object obj) => Equals(obj as RebirthNpcResolvedAppearance);
    public override int GetHashCode()
    {
        unchecked { int hash = 17; foreach (string field in Fields) foreach (char c in Value(field)) hash = hash * 31 + c; return hash; }
    }
}