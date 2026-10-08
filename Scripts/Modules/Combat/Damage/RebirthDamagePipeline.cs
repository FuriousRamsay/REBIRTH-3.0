using System;
using System.Text;
using HarmonyLib;

#nullable disable

public enum RebirthDamagePipelineStage
{
    Unknown,
    BuildContext,
    RelevanceGate,
    AuthorityGate,
    RelationshipGate,
    EquipmentSnapshot,
    EntitySpecificPolicy,
    VehicleBlockPolicy,
    ScalarApplication,
    PostResultEvents,
    Diagnostics
}

public enum RebirthDamageStageStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired
}

public enum RebirthDamagePolicyKind
{
    Unknown,
    Difficulty,
    Headshot,
    PlayerDamageOption,
    ZombieDamageOption,
    FriendlyFire,
    OwnedFriendlyPassthrough,
    NpcDamage,
    VehicleDamage,
    ExplosiveDamage,
    Stagger,
    ArmorResistance,
    BuffResistance,
    SpecialZombieEffect,
    Diagnostics
}

public sealed class RebirthDamageStageDecl
{
    public RebirthDamagePipelineStage Stage;
    public RebirthDamageStageStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthDamagePolicyDecl
{
    public RebirthDamagePolicyKind PolicyKind;
    public RebirthDamageStageStatus Status;
    public string OwnerModuleId;
    public bool ServerAuthoritative;
    public bool HotPath;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthDamageContext
{
    public EntityAlive Target;
    public EntityAlive Attacker;
    public bool HasTarget;
    public bool HasAttacker;
    public bool ShouldFastExit;
    public string Reason;

    public static RebirthDamageContext Empty(string reason)
    {
        return new RebirthDamageContext
        {
            Target = null,
            Attacker = null,
            HasTarget = false,
            HasAttacker = false,
            ShouldFastExit = true,
            Reason = reason
        };
    }
}

/// <summary>
/// Governed DamageEntity tenant pipeline and shared scalar adapter.
/// The shared scalar adapter owns migrated, allocation-free scalar policies. Other stages remain explicitly scaffolded until migrated.
/// </summary>
public static class RebirthDamagePipeline
{
    private static readonly RebirthDamageStageDecl[] s_stages = new[]
    {
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.BuildContext,
            Status = RebirthDamageStageStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "combat.damage",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / P2",
            Notes = "Build minimal DamageContext. No allocations or string work in future adapter."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.RelevanceGate,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.damage",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Reject non-REBIRTH-relevant damage paths as early as possible."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.AuthorityGate,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "network.authority",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior dedicated server parity issues",
            Notes = "Damage decisions must be server-authoritative where relevant."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.RelationshipGate,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.relationship",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior friendly passthrough work",
            Notes = "Friendly fire and owned-friendly passthrough belong before expensive policy resolution."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.EquipmentSnapshot,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "items.equipment",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior ItemValue/armor performance findings",
            Notes = "Use cached equipment/stat snapshots. Do not scan items or build tooltips here."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.EntitySpecificPolicy,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "entities.combat",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Zombie/NPC/player/entity-specific rules register here."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.VehicleBlockPolicy,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "vehicles.blocks",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior vehicle damage issues",
            Notes = "Vehicle/block damage rules must not duplicate sound/animation behavior."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.ScalarApplication,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.damage",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Final scalar stage should be deterministic and ordered."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.PostResultEvents,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.events",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "Only emit events after vanilla result when required."
        },
        new RebirthDamageStageDecl
        {
            Stage = RebirthDamagePipelineStage.Diagnostics,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / debug policy",
            Notes = "Diagnostics must compile out or be gated before string construction."
        }
    };

    private static readonly RebirthDamagePolicyDecl[] s_policies = new[]
    {
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.Headshot,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.headshot",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "P6 option triage / prior headshot multiplier work",
            Notes = "CustomHeadshotMultiplier likely maps to native 3.0 HeadshotMultiplier."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.PlayerDamageOption,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.playerDamage",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "Blueprint v2 / CustomPlayerDamage",
            Notes = "Option value must be cached. No GamePrefs reads in damage path."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.ZombieDamageOption,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.zombieDamage",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "Blueprint v2 / CustomZombieDamage",
            Notes = "Option value must be cached. No GamePrefs reads in damage path."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.FriendlyFire,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.relationship",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "Blueprint v2 / prior friendly passthrough fixes",
            Notes = "Relationship gate should happen before equipment/stat logic."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.OwnedFriendlyPassthrough,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.relationship",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "prior ranged/melee owned friendly passthrough work",
            Notes = "Ranged and melee classification must be indexed and not string-scanned per hit."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.NpcDamage,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "npc.combat",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "Blueprint v2 / prior NPC damage/pathing issues",
            Notes = "EntityNPCRebirth damage behavior must register as policy, not independent DamageEntity patch."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.VehicleDamage,
            Status = RebirthDamageStageStatus.ManualReviewRequired,
            OwnerModuleId = "vehicles.damage",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "prior fat zombie vehicle damage/sound sync issues",
            Notes = "Do not migrate until vehicle hit timing/sound behavior is separately designed."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.Stagger,
            Status = RebirthDamageStageStatus.BehaviorMigrated,
            OwnerModuleId = "combat.stagger",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "REBIRTH 2.6 Always Stagger source analysis / 3.0 implementation",
            Notes = "Typed three-mode policy; forces qualifying enemy pain responses, excludes animals, and cancels stale melee impact timing."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.ArmorResistance,
            Status = RebirthDamageStageStatus.ManualReviewRequired,
            OwnerModuleId = "items.armor",
            ServerAuthoritative = true,
            HotPath = true,
            Evidence = "prior ItemValue/armor performance findings",
            Notes = "Armor stats need cached snapshots; no ModifyValue sprawl in damage path."
        },
        new RebirthDamagePolicyDecl
        {
            PolicyKind = RebirthDamagePolicyKind.Diagnostics,
            Status = RebirthDamageStageStatus.Planned,
            OwnerModuleId = "combat.diagnostics",
            ServerAuthoritative = false,
            HotPath = true,
            Evidence = "debug policy",
            Notes = "No logging/string formatting unless compile-time or bool-gated before construction."
        }
    };

    public static RebirthDamageStageDecl[] GetStagesSnapshot()
    {
        RebirthDamageStageDecl[] copy = new RebirthDamageStageDecl[s_stages.Length];
        Array.Copy(s_stages, copy, copy.Length);
        return copy;
    }

    public static RebirthDamagePolicyDecl[] GetPoliciesSnapshot()
    {
        RebirthDamagePolicyDecl[] copy = new RebirthDamagePolicyDecl[s_policies.Length];
        Array.Copy(s_policies, copy, copy.Length);
        return copy;
    }

    public static RebirthDamageContext BuildNoBehaviorContext(EntityAlive target, EntityAlive attacker)
    {
        if (target == null)
            return RebirthDamageContext.Empty("target is null");

        return new RebirthDamageContext
        {
            Target = target,
            Attacker = attacker,
            HasTarget = true,
            HasAttacker = attacker != null,
            ShouldFastExit = false,
            Reason = "scaffold only; no damage policy execution yet"
        };
    }

    public static void OnDamageNoBehavior(EntityAlive target, EntityAlive attacker)
    {
        // Intentional no-op.
        // Future EntityAlive.DamageEntity adapter target only.
        BuildNoBehaviorContext(target, attacker);
    }

    public static string GetStageReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthDamage] governed DamageEntity tenant pipeline.");
        sb.AppendLine("  No Harmony patch is installed in this phase.");
        sb.AppendLine("  No damage behavior is changed in this phase.");
        sb.AppendLine("stage | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_stages.Length; i++)
        {
            RebirthDamageStageDecl s = s_stages[i];
            sb.Append(s.Stage).Append(" | ")
              .Append(s.Status).Append(" | ")
              .Append(s.OwnerModuleId).Append(" | ")
              .Append(s.HotPath).Append(" | ")
              .Append(s.MayAllocate).Append(" | ")
              .Append(s.Evidence).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetPolicyReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthDamage] planned damage policy ledger.");
        sb.AppendLine("policy | status | owner | server | hot | evidence | notes");

        for (int i = 0; i < s_policies.Length; i++)
        {
            RebirthDamagePolicyDecl p = s_policies[i];
            sb.Append(p.PolicyKind).Append(" | ")
              .Append(p.Status).Append(" | ")
              .Append(p.OwnerModuleId).Append(" | ")
              .Append(p.ServerAuthoritative).Append(" | ")
              .Append(p.HotPath).Append(" | ")
              .Append(p.Evidence).Append(" | ")
              .AppendLine(p.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthDamage] safety rules:");
        sb.AppendLine("  1. Only one future REBIRTH patch may own EntityAlive.DamageEntity.");
        sb.AppendLine("  2. Individual features register damage policies with the dispatcher.");
        sb.AppendLine("  3. No GamePrefs reads in DamageEntity.");
        sb.AppendLine("  4. No item scans, tooltip construction, string formatting, or stack traces in DamageEntity.");
        sb.AppendLine("  5. Server-authoritative policies must be explicit.");
        sb.AppendLine("  6. Vehicle damage/sound timing is manual-review before migration.");
        sb.AppendLine("  7. Diagnostics must compile out or be bool-gated before construction.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthDamage] DamageEntity tenant scaffold stages: " + s_stages.Length
            + "; planned policies: " + s_policies.Length
            + "; sharedScalarAdapterInstalled=" + RebirthDamageAdapterInstaller.Installed + ".";
    }
}


/// <summary>
/// Single-owner hot-path adapter for REBIRTH damage scalar tenants that are already
/// represented by allocation-free runtime caches. Boss Events is the first migrated
/// scalar tenant. Additional features must register here rather than installing a
/// separate ProcessDamageResponseLocal or DamageEntity scalar patch.
/// </summary>
public static class RebirthDamageScalarAdapter
{
    public static void ProcessDamageResponsePrefix(ref DamageResponse _dmResponse)
    {
        DamageSource source = _dmResponse.Source;
        if (source == null)
            return;

        float multiplier;
        if (!RebirthBossEventRuntimePolicy.TryGetDamageMultiplier(source.getEntityId(), out multiplier))
            return;
        if (multiplier <= 0f || Math.Abs(multiplier - 1f) < 0.0001f)
            return;

        int original = _dmResponse.Strength;
        if (original <= 0)
            return;

        long scaled = (long)Math.Round(original * (double)multiplier, MidpointRounding.AwayFromZero);
        if (scaled < 1L) scaled = 1L;
        if (scaled > int.MaxValue) scaled = int.MaxValue;
        _dmResponse.Strength = (int)scaled;
    }
}

public static class RebirthDamageAdapterInstaller
{
    public const string HarmonyId = "rebirth.3.0.damage.dispatcher";
    private static readonly Harmony s_harmony = new Harmony(HarmonyId);
    private static bool s_installed;

    public static bool Installed { get { return s_installed; } }

    public static void Install()
    {
        if (s_installed)
            return;

        RebirthHarmonyBootstrap.PatchClassOnce(s_harmony, typeof(RebirthDamageResponsePatch));
        s_installed = true;
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[RebirthDamage] Installed the shared ProcessDamageResponseLocal scalar dispatcher."); }
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthDamageResponsePatch
{
    [HarmonyPriority(Priority.High)]
    private static void Prefix(ref DamageResponse _dmResponse)
    {
        RebirthDamageScalarAdapter.ProcessDamageResponsePrefix(ref _dmResponse);
    }
}
