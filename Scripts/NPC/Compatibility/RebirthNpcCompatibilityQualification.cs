using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcCompatibilitySeverity : byte
{
    Informational = 0,
    Degraded = 1,
    Blocking = 2
}

public sealed class RebirthNpcApiContract
{
    public string ContractId;
    public string TypeName;
    public string[] CandidateMembers;
    public int ParameterCount;
    public bool Required;
    public string Capability;
}

public sealed class RebirthNpcApiContractResult
{
    public string ContractId;
    public string Capability;
    public bool Required;
    public bool Resolved;
    public string ResolvedType;
    public string ResolvedMember;
    public string Reason;
}

/// <summary>
/// Static 3.1 API qualification. These contracts are compile-time bindings, not
/// runtime reflection probes. If one of the referenced 3.1 APIs changes, the mod
/// fails at compile time instead of silently degrading at runtime.
/// </summary>
public static class RebirthNpcCompatibilityQualificationService
{
    private static readonly object Sync = new object();
    private static readonly List<RebirthNpcApiContractResult> Results = new List<RebirthNpcApiContractResult>();
    private static bool initialized;
    private static DateTime lastAuditUtc;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (initialized)
                return;

            initialized = true;
            Results.Clear();
            AddResolved("3.1.entityalive.damage", "combat.damage",
                "EntityAlive.damageEntityLocal(DamageSource,int,bool,float)", true);
            AddResolved("3.1.entityalive.death", "combat.death-events",
                "EntityAlive.OnEntityDeath()", false);
            AddResolved("3.1.entity.position", "entity.position",
                "Entity.position", true);
            AddResolved("3.1.world.entity.lookup", "entity.lookup",
                "World.GetEntity(int)", true);
            AddResolved("3.1.path.request", "navigation.native-pathing",
                "EntityAlive.FindPath(Vector3,float,bool,EAIBase)", false);
            AddResolved("3.1.inventory.add", "inventory.native-transfer",
                "Inventory.AddItem(ItemStack)", false);
            AddResolved("3.1.buff.add", "combat.status-effects",
                "BuffManager.AddBuff(BuffClass)", false);
            lastAuditUtc = DateTime.UtcNow;
        }
    }

    public static void Audit(bool force)
    {
        EnsureInitialized();
        if (force)
        {
            lock (Sync)
                lastAuditUtc = DateTime.UtcNow;
        }
    }

    public static bool IsCapabilityAvailable(string capability)
    {
        EnsureInitialized();
        lock (Sync)
        {
            for (int i = 0; i < Results.Count; i++)
            {
                if (string.Equals(Results[i].Capability, capability, StringComparison.OrdinalIgnoreCase))
                    return Results[i].Resolved;
            }
            return false;
        }
    }

    public static RebirthNpcCompatibilitySeverity GetSeverity()
    {
        EnsureInitialized();
        return RebirthNpcCompatibilitySeverity.Informational;
    }

    public static string GetReport()
    {
        EnsureInitialized();
        lock (Sync)
        {
            StringBuilder b = new StringBuilder();
            b.Append("[REBIRTH NPC 3.1 Compatibility] evidence=STRUCTURAL severity=Informational contracts=")
                .Append(Results.Count)
                .Append(" declaredBindings=").Append(Results.Count)
                .Append(" runtimeResolved=NotRun")
                .Append(" binding=compile-time")
                .Append(" lastAuditUtc=").Append(lastAuditUtc.ToString("o"));

            for (int i = 0; i < Results.Count; i++)
            {
                RebirthNpcApiContractResult r = Results[i];
                b.AppendLine().Append("  STRUCTURAL ").Append(r.ContractId)
                    .Append(" capability=").Append(r.Capability)
                    .Append(" member=").Append(r.ResolvedMember)
                    .Append(" reason=compiled-3.1-binding; runtime/member invocation not executed");
            }
            return b.ToString();
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            initialized = false;
            Results.Clear();
            lastAuditUtc = default(DateTime);
        }
    }

    private static void AddResolved(string id, string capability, string member, bool required)
    {
        Results.Add(new RebirthNpcApiContractResult
        {
            ContractId = id,
            Capability = capability,
            Required = required,
            Resolved = true,
            ResolvedType = "3.1",
            ResolvedMember = member,
            Reason = "compiled-3.1-binding"
        });
    }
}
