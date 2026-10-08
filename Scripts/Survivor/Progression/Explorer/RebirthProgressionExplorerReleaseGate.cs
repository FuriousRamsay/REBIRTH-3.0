using System;
using System.Security.Cryptography;
using System.Text;

#nullable disable

/// <summary>
/// PE-12 read-only release/parity gate for the Progression Explorer.
/// It deliberately does not manufacture live multiplayer approval.  The composite fingerprint
/// lets host, P2P clients and dedicated clients prove that the same definition/capability/graph
/// authority was loaded before visual/runtime acceptance is recorded.
/// </summary>
public static class RebirthProgressionExplorerReleaseGate
{
    public static string BuildAuthorityFingerprint()
    {
        string defs=RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty;
        string caps=RebirthCapabilityRegistry.SemanticHash??string.Empty;
        string graph=RebirthProgressionGraphRegistry.SemanticHash??string.Empty;
        string payload="defs="+defs+"\ncaps="+caps+"\ngraph="+graph+"\nprotocol="+RebirthSurvivorNetworkProtocol.Version;
        using(SHA256 sha=SHA256.Create())
        {
            byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            StringBuilder b=new StringBuilder(hash.Length*2);
            for(int i=0;i<hash.Length;i++)b.Append(hash[i].ToString("x2"));
            return b.ToString();
        }
    }

    public static RebirthProgressionExplorerReleaseGateReport Evaluate()
    {
        RebirthProgressionExplorerReleaseGateReport r=new RebirthProgressionExplorerReleaseGateReport();
        r.DefinitionsReady=RebirthSurvivorDefinitionRegistry.IsReady;
        r.CapabilitiesReady=RebirthCapabilityRegistry.IsReady;
        r.GraphReady=RebirthProgressionGraphRegistry.IsReady;
        r.DefinitionHash=RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty;
        r.CapabilityHash=RebirthCapabilityRegistry.SemanticHash??string.Empty;
        r.GraphHash=RebirthProgressionGraphRegistry.SemanticHash??string.Empty;
        r.AuthorityFingerprint=BuildAuthorityFingerprint();

        if(r.GraphReady)
        {
            RebirthProgressionGraphValidationReport graphReport=RebirthProgressionGraphValidator.ValidateCurrent();
            r.GraphValid=graphReport!=null&&graphReport.IsValid;
            r.GraphWarnings=graphReport!=null?graphReport.WarningCount:0;
        }

        RebirthSurvivorOwnerStateSnapshot owner=RebirthSurvivorClientState.GetOwnerStateSnapshot();
        r.OwnerSnapshotPresent=owner!=null;
        r.OwnerDefinitionsCompatible=owner==null||owner.DefinitionsCompatible;

        r.StaticReady=r.DefinitionsReady&&r.CapabilitiesReady&&r.GraphReady&&r.GraphValid
            &&r.DefinitionHash.Length>0&&r.CapabilityHash.Length>0&&r.GraphHash.Length>0;

        // Runtime release approval remains intentionally false here.  PE-12 requires manual
        // host/P2P/dedicated + visual/controller/performance evidence; code must not self-approve it.
        r.RuntimeApproved=false;
        return r;
    }
}

public sealed class RebirthProgressionExplorerReleaseGateReport
{
    public bool DefinitionsReady;
    public bool CapabilitiesReady;
    public bool GraphReady;
    public bool GraphValid;
    public int GraphWarnings;
    public bool OwnerSnapshotPresent;
    public bool OwnerDefinitionsCompatible;
    public bool StaticReady;
    public bool RuntimeApproved;
    public string DefinitionHash=string.Empty;
    public string CapabilityHash=string.Empty;
    public string GraphHash=string.Empty;
    public string AuthorityFingerprint=string.Empty;

    public string BuildText()
    {
        StringBuilder b=new StringBuilder();
        b.Append("[REBIRTH ProgressionExplorer PE-12] staticReady=").Append(StaticReady)
            .Append(" runtimeApproved=").Append(RuntimeApproved)
            .Append(" graphValid=").Append(GraphValid)
            .Append(" graphWarnings=").Append(GraphWarnings).AppendLine();
        b.Append("  authorityFingerprint=").Append(AuthorityFingerprint).AppendLine();
        b.Append("  definitions=").Append(Short(DefinitionHash)).Append(" capabilities=").Append(Short(CapabilityHash)).Append(" graph=").Append(Short(GraphHash)).AppendLine();
        b.Append("  ownerSnapshot=").Append(OwnerSnapshotPresent).Append(" ownerDefinitionsCompatible=").Append(OwnerDefinitionsCompatible).AppendLine();
        b.Append("  release gate: compare the full authorityFingerprint on host/listen client, joined P2P client and dedicated client; then execute the PE-12 live acceptance matrix. This diagnostic never self-approves runtime release.");
        return b.ToString();
    }

    private static string Short(string value)
    {
        return string.IsNullOrEmpty(value)?string.Empty:(value.Length<=12?value:value.Substring(0,12));
    }
}
