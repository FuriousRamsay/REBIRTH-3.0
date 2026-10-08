using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public interface IRebirthNpcExternalCapability
{
    string CapabilityId { get; }
    Version ContractVersion { get; }
    bool IsServerSafe { get; }
    string ProviderId { get; }
}

public static class RebirthNpcThirdPartyCapabilityRegistry
{
    private static readonly object Sync = new object();
    private sealed class RegisteredCapability : IRebirthNpcExternalCapability
    {
        public string CapabilityId { get; private set; }
        public Version ContractVersion { get; private set; }
        public bool IsServerSafe { get; private set; }
        public string ProviderId { get; private set; }
        public RegisteredCapability(IRebirthNpcExternalCapability source)
        {
            CapabilityId = source.CapabilityId.Trim(); ProviderId = source.ProviderId.Trim();
            ContractVersion = new Version(source.ContractVersion.ToString()); IsServerSafe = source.IsServerSafe;
        }
    }
    private static readonly SortedDictionary<string, RegisteredCapability> Providers = new SortedDictionary<string, RegisteredCapability>(StringComparer.OrdinalIgnoreCase);
    private static long registrations, duplicateRejects, incompatibleRejects;

    public static bool TryRegister(IRebirthNpcExternalCapability provider, out string error)
    {
        error = null;
        if (provider == null || string.IsNullOrEmpty(provider.CapabilityId) || string.IsNullOrEmpty(provider.ProviderId)) { error = "invalid-provider"; return false; }
        if (provider.ContractVersion == null || provider.ContractVersion.Major != 1) { lock (Sync) incompatibleRejects++; error = "unsupported-contract-major"; return false; }
        if (!provider.IsServerSafe) { lock (Sync) incompatibleRejects++; error = "provider-not-server-safe"; return false; }
        lock (Sync)
        {
            if (Providers.ContainsKey(provider.CapabilityId)) { duplicateRejects++; error = "capability-already-registered"; return false; }
            RegisteredCapability captured = new RegisteredCapability(provider);
            Providers.Add(captured.CapabilityId, captured); registrations++; return true;
        }
    }

    public static bool TryGet(string capabilityId, out IRebirthNpcExternalCapability provider)
    {
        lock (Sync)
        {
            RegisteredCapability captured;
            bool found = Providers.TryGetValue(capabilityId ?? string.Empty, out captured);
            provider = captured;
            return found;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Third Party Capabilities] registered=").Append(Providers.Count)
                .Append(" successfulRegistrations=").Append(registrations).Append(" duplicateRejects=").Append(duplicateRejects).Append(" incompatibleRejects=").Append(incompatibleRejects);
            foreach (KeyValuePair<string, RegisteredCapability> pair in Providers)
                b.AppendLine().Append("  ").Append(pair.Key).Append(" provider=").Append(pair.Value.ProviderId).Append(" version=").Append(pair.Value.ContractVersion).Append(" serverSafe=").Append(pair.Value.IsServerSafe);
            return b.ToString();
        }
    }

    public static void ResetForWorldChange() { lock (Sync) { Providers.Clear(); registrations = duplicateRejects = incompatibleRejects = 0; } }
}
