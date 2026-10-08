using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Data-only composition. Parsing this record proves neither payment/publication nor discovery eligibility.
// Authority must independently authenticate owner/save/policy and verify native publication before use.
public sealed class RebirthStationDiscoveryWitness
{
    private readonly XElement image;
    public string JobId => (string)image.Attribute("job");
    public string KnowledgeId => (string)image.Attribute("knowledge");
    private RebirthStationDiscoveryWitness(XElement node) { image = new XElement(node); }
    public XElement Write() => new XElement(image);
    public RebirthStationDiscoveryWitness Clone() => new RebirthStationDiscoveryWitness(image);
    private static string Digest(XElement node)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-", "");
    }
    private static bool Hex(string value, bool upper = false)
        => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || (upper ? c >= 'A' && c <= 'F' : c >= 'a' && c <= 'f'));
    private static bool Id(string value)
        => !string.IsNullOrEmpty(value) && value.Length <= 256 && value == value.Trim()
            && !value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c));
    // Digests must be supplied by current authenticated authority, never accepted as request identity.
    public static bool TryCreate(RebirthStationGridAdmission admission, string ownerKey, string saveScope,
        string policyDigest, string knowledgeId, out RebirthStationDiscoveryWitness witness)
    {
        witness = null;
        if (admission == null) return false;
        try
        {
            var node = new XElement("stationDiscoveryWitness", new XAttribute("version", "1"),
                new XAttribute("job", admission.JobId), new XAttribute("creation", admission.CreationId),
                new XAttribute("owner", ownerKey ?? ""), new XAttribute("save", saveScope ?? ""),
                new XAttribute("policy", policyDigest ?? ""), new XAttribute("knowledge", knowledgeId ?? ""),
                new XAttribute("definition", admission.DefinitionId), new XAttribute("admission", Digest(admission.Write())));
            return TryRead(node, admission, ownerKey, saveScope, policyDigest, knowledgeId, out witness);
        }
        catch { return false; }
    }
    public static bool TryRead(XElement node, RebirthStationGridAdmission admission, string ownerKey,
        string saveScope, string policyDigest, string knowledgeId, out RebirthStationDiscoveryWitness witness)
    {
        witness = null;
        if (node == null || admission == null || node.Name != "stationDiscoveryWitness" || node.HasElements
            || node.Attributes().Count() != 9 || node.Nodes().Any(n => !(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value))) return false;
        try
        {
            string creation;
            Guid job;
            if (!Hex(ownerKey) || !Hex(saveScope) || !Hex(policyDigest) || !Hex(admission.DefinitionId, true) || !Id(knowledgeId)
                || !Guid.TryParseExact(admission.JobId, "N", out job) || job == Guid.Empty || job.ToString("N") != admission.JobId
                || !RebirthSurvivorRequestScope.TryNormalize(admission.CreationId, out creation) || creation != admission.CreationId
                || (string)node.Attribute("version") != "1" || (string)node.Attribute("job") != admission.JobId
                || (string)node.Attribute("creation") != creation || (string)node.Attribute("owner") != ownerKey
                || (string)node.Attribute("save") != saveScope || (string)node.Attribute("policy") != policyDigest
                || (string)node.Attribute("knowledge") != knowledgeId || (string)node.Attribute("definition") != admission.DefinitionId
                || (string)node.Attribute("admission") != Digest(admission.Write())) return false;
            witness = new RebirthStationDiscoveryWitness(node);
            return true;
        }
        catch { return false; }
    }
    // Detached syntax validation only; callers must bind persisted data to current authority separately.
    public static bool TryReadStored(XElement node, out RebirthStationDiscoveryWitness witness)
    {
        witness = null;
        if (node == null || node.Name != "stationDiscoveryWitness" || node.HasElements
            || node.Attributes().Count() != 9 || node.Nodes().Any(n => !(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value))) return false;
        try
        {
            Guid job;
            string creation;
            string jobId = (string)node.Attribute("job"), creationId = (string)node.Attribute("creation");
            if ((string)node.Attribute("version") != "1"
                || !Guid.TryParseExact(jobId, "N", out job) || job == Guid.Empty || job.ToString("N") != jobId
                || !RebirthSurvivorRequestScope.TryNormalize(creationId, out creation) || creation != creationId
                || !Hex((string)node.Attribute("owner")) || !Hex((string)node.Attribute("save"))
                || !Hex((string)node.Attribute("policy")) || !Id((string)node.Attribute("knowledge"))
                || !Hex((string)node.Attribute("definition"), true) || !Hex((string)node.Attribute("admission"), true)
                || new UTF8Encoding(false, true).GetByteCount(node.ToString(SaveOptions.DisableFormatting)) > 8192) return false;
            witness = new RebirthStationDiscoveryWitness(node);
            return true;
        }
        catch { return false; }
    }
}
