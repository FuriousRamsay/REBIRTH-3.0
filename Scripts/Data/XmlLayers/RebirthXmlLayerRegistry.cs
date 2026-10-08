using System;
using System.Text;

#nullable disable

public enum RebirthXmlLayerKind
{
    Unknown,
    BaseGame,
    RebirthCore,
    RebirthUtils,
    ExternalOwnedProject,
    ThirdPartyCompatibility,
    CleanupFinalizer
}

public enum RebirthXmlOwnershipStatus
{
    Unknown,
    OwnedByRebirth,
    OwnedExternalAbsorptionCandidate,
    ThirdPartyCompatibilityOnly,
    CleanupOnly,
    ManualReviewRequired
}

public sealed class RebirthXmlLayerDecl
{
    public string LayerId;
    public string DisplayName;
    public RebirthXmlLayerKind Kind;
    public RebirthXmlOwnershipStatus OwnershipStatus;
    public string LoadOrderRole;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthXmlFileFamilyDecl
{
    public string FamilyId;
    public string FileName;
    public string OwnerModuleId;
    public string PrimaryRuntimeIndex;
    public bool AffectsHotPath;
    public bool RequiresReload;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthXmlCleanupDecl
{
    public string CleanupId;
    public string Target;
    public string OwnerModuleId;
    public RebirthXmlOwnershipStatus OwnershipStatus;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only XML layer and ownership registry.
/// This does not parse XML, generate XML, modify config, or affect load order.
/// </summary>
public static class RebirthXmlLayerRegistry
{
    private static readonly RebirthXmlLayerDecl[] s_layers = new[]
    {
        new RebirthXmlLayerDecl
        {
            LayerId = "base.game",
            DisplayName = "Base game XML",
            Kind = RebirthXmlLayerKind.BaseGame,
            OwnershipStatus = RebirthXmlOwnershipStatus.Unknown,
            LoadOrderRole = "Input / upstream baseline",
            OwnerModuleId = "external.basegame",
            Evidence = "Blueprint v2 / ordered ledger",
            Notes = "Never edit base files directly. All changes must be additive/patch-layered."
        },
        new RebirthXmlLayerDecl
        {
            LayerId = "rebirth.core",
            DisplayName = "REBIRTH Core content",
            Kind = RebirthXmlLayerKind.RebirthCore,
            OwnershipStatus = RebirthXmlOwnershipStatus.OwnedByRebirth,
            LoadOrderRole = "Primary authored gameplay/content layer",
            OwnerModuleId = "rebirth.core",
            Evidence = "Blueprint v2",
            Notes = "Owns main gameplay XML and should eventually be split by domain."
        },
        new RebirthXmlLayerDecl
        {
            LayerId = "rebirth.utils",
            DisplayName = "REBIRTH Utils support layer",
            Kind = RebirthXmlLayerKind.RebirthUtils,
            OwnershipStatus = RebirthXmlOwnershipStatus.OwnedByRebirth,
            LoadOrderRole = "Utility/UI/support layer",
            OwnerModuleId = "rebirth.utils",
            Evidence = "Blueprint v2",
            Notes = "Should remain separate from Core when the concern is utility/tooling rather than gameplay content."
        },
        new RebirthXmlLayerDecl
        {
            LayerId = "external.owned",
            DisplayName = "Source-owned external project XML",
            Kind = RebirthXmlLayerKind.ExternalOwnedProject,
            OwnershipStatus = RebirthXmlOwnershipStatus.OwnedExternalAbsorptionCandidate,
            LoadOrderRole = "Candidate absorption layer",
            OwnerModuleId = "external.absorption",
            Evidence = "P3/P4",
            Notes = "TheDescent/DayCustomShaders/Material Modifier/etc. must be evaluated as source-owned external projects."
        },
        new RebirthXmlLayerDecl
        {
            LayerId = "external.compatxml",
            DisplayName = "Third-party compatibility XML",
            Kind = RebirthXmlLayerKind.ThirdPartyCompatibility,
            OwnershipStatus = RebirthXmlOwnershipStatus.ThirdPartyCompatibilityOnly,
            LoadOrderRole = "Compatibility only; no source ownership",
            OwnerModuleId = "external.compatxml",
            Evidence = "V8",
            Notes = "IzayoGuns/SonjaArmor-style targets must not be treated as source-owned absorption."
        },
        new RebirthXmlLayerDecl
        {
            LayerId = "rebirth.cleanup",
            DisplayName = "REBIRTH Cleanup finalizer",
            Kind = RebirthXmlLayerKind.CleanupFinalizer,
            OwnershipStatus = RebirthXmlOwnershipStatus.CleanupOnly,
            LoadOrderRole = "Final reconciliation / final load-order layer",
            OwnerModuleId = "rebirth.cleanup",
            Evidence = "Blueprint v2 / V8",
            Notes = "Must stay separate and load last. It reconciles base, external, Core, and Utils."
        }
    };

    private static readonly RebirthXmlFileFamilyDecl[] s_fileFamilies = new[]
    {
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "custom.game.options",
            FileName = "CustomGameOptions.xml",
            OwnerModuleId = "options",
            PrimaryRuntimeIndex = "RebirthOptionMigrationRegistry / future OPTIONS_TRIAGE_127",
            AffectsHotPath = false,
            RequiresReload = true,
            Evidence = "P6",
            Notes = "127 custom options read. Runtime option reads must be cached; migration must map 3.0 native equivalents."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "xui.windows",
            FileName = "XUi/windows.xml + xui.xml + controls/templates",
            OwnerModuleId = "xui.content",
            PrimaryRuntimeIndex = "RebirthXUiMigrationRegistry / future xui-diff26to30",
            AffectsHotPath = false,
            RequiresReload = true,
            Evidence = "P6/V7",
            Notes = "XUi XML remains hand-authored. C# does not generate XML."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "entityclasses",
            FileName = "entityclasses.xml",
            OwnerModuleId = "entities.classification",
            PrimaryRuntimeIndex = "future RebirthEntityClassIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "Blueprint v2/P1",
            Notes = "Classification must be indexed once; no repeated XML/entityclass string scans in hot paths."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "entitygroups",
            FileName = "entitygroups.xml",
            OwnerModuleId = "spawning.groups",
            PrimaryRuntimeIndex = "future RebirthEntityGroupIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "Blueprint v2/P1",
            Notes = "Spawn composition should resolve through cached group graphs, not repeated group traversal."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "items",
            FileName = "items.xml",
            OwnerModuleId = "items.stats",
            PrimaryRuntimeIndex = "future RebirthItemStatIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "Blueprint v2/prior ItemValue findings",
            Notes = "Anything feeding ItemValue.ModifyValue must become indexed/cached before runtime hot paths."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "blocks",
            FileName = "blocks.xml",
            OwnerModuleId = "blocks.paint.pathing",
            PrimaryRuntimeIndex = "future RebirthBlockIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "Blueprint v2/prior crawl/paint findings",
            Notes = "Block classification must be pre-indexed for crawl, paint, pathing, and renderFace fast paths."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "buffs",
            FileName = "buffs.xml",
            OwnerModuleId = "effects.buffs",
            PrimaryRuntimeIndex = "future RebirthBuffIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "Blueprint v2",
            Notes = "Buff/effect lookups must not re-parse or scan XML-derived structures in combat/update paths."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "loot",
            FileName = "loot.xml",
            OwnerModuleId = "loot",
            PrimaryRuntimeIndex = "future RebirthLootIndex",
            AffectsHotPath = false,
            RequiresReload = true,
            Evidence = "Blueprint v2",
            Notes = "Loot reservation/marker systems should use explicit event/state records, not repeated loot XML lookups."
        },
        new RebirthXmlFileFamilyDecl
        {
            FamilyId = "biomes.spawn",
            FileName = "biomes.xml / spawnmanagerbiomes.xml / entitygroups.xml",
            OwnerModuleId = "spawning.biomes",
            PrimaryRuntimeIndex = "future RebirthSpawnPolicyIndex",
            AffectsHotPath = true,
            RequiresReload = true,
            Evidence = "P3/V2 blueprint",
            Notes = "Cave tunnel vs cave POI checks must remain separate."
        }
    };

    private static readonly RebirthXmlCleanupDecl[] s_cleanupTargets = new[]
    {
        new RebirthXmlCleanupDecl
        {
            CleanupId = "cleanup.thedescent",
            Target = "_descent_blockplaceholders.xml / _descent_items.xml / related cave XML",
            OwnerModuleId = "world.caves",
            OwnershipStatus = RebirthXmlOwnershipStatus.OwnedExternalAbsorptionCandidate,
            Evidence = "P3/V8",
            Notes = "Source-owned external project; split into cave generation/runtime/spawn/XML."
        },
        new RebirthXmlCleanupDecl
        {
            CleanupId = "cleanup.izayoguns",
            Target = "_izayoguns_items.xml / _izayoguns_loot.xml / _izayoguns_ui_display.xml",
            OwnerModuleId = "external.compatxml",
            OwnershipStatus = RebirthXmlOwnershipStatus.ThirdPartyCompatibilityOnly,
            Evidence = "V8",
            Notes = "Compatibility XML only; do not treat as owned source."
        },
        new RebirthXmlCleanupDecl
        {
            CleanupId = "cleanup.sonjaarmor",
            Target = "_sonjaarmor_items.xml",
            OwnerModuleId = "external.compatxml",
            OwnershipStatus = RebirthXmlOwnershipStatus.ThirdPartyCompatibilityOnly,
            Evidence = "V8",
            Notes = "Compatibility XML only; do not treat as owned source."
        },
        new RebirthXmlCleanupDecl
        {
            CleanupId = "cleanup.worldglobal",
            Target = "_worldglobal.xml",
            OwnerModuleId = "world.global",
            OwnershipStatus = RebirthXmlOwnershipStatus.ManualReviewRequired,
            Evidence = "V8",
            Notes = "Needs explicit owner before migration because world/global patches can affect many systems."
        }
    };

    public static string GetLayerReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthXml] XML layer ownership ledger.");
        sb.AppendLine("layer | kind | ownership | load role | owner | evidence | notes");
        for (int i = 0; i < s_layers.Length; i++)
        {
            RebirthXmlLayerDecl l = s_layers[i];
            sb.Append(l.LayerId).Append(" | ")
              .Append(l.Kind).Append(" | ")
              .Append(l.OwnershipStatus).Append(" | ")
              .Append(l.LoadOrderRole).Append(" | ")
              .Append(l.OwnerModuleId).Append(" | ")
              .Append(l.Evidence).Append(" | ")
              .AppendLine(l.Notes);
        }
        return sb.ToString();
    }

    public static string GetFileFamilyReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthXml] XML file-family ownership ledger.");
        sb.AppendLine("family | file | owner | hotpath | reload | index | evidence | notes");
        for (int i = 0; i < s_fileFamilies.Length; i++)
        {
            RebirthXmlFileFamilyDecl x = s_fileFamilies[i];
            if (!string.IsNullOrEmpty(f)
                && (x.FamilyId == null || x.FamilyId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (x.FileName == null || x.FileName.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (x.OwnerModuleId == null || x.OwnerModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            sb.Append(x.FamilyId).Append(" | ")
              .Append(x.FileName).Append(" | ")
              .Append(x.OwnerModuleId).Append(" | ")
              .Append(x.AffectsHotPath).Append(" | ")
              .Append(x.RequiresReload).Append(" | ")
              .Append(x.PrimaryRuntimeIndex).Append(" | ")
              .Append(x.Evidence).Append(" | ")
              .AppendLine(x.Notes);
        }
        return sb.ToString();
    }

    public static string GetCleanupReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthXml] Cleanup ownership ledger.");
        sb.AppendLine("cleanup | target | owner | ownership | evidence | notes");
        for (int i = 0; i < s_cleanupTargets.Length; i++)
        {
            RebirthXmlCleanupDecl c = s_cleanupTargets[i];
            sb.Append(c.CleanupId).Append(" | ")
              .Append(c.Target).Append(" | ")
              .Append(c.OwnerModuleId).Append(" | ")
              .Append(c.OwnershipStatus).Append(" | ")
              .Append(c.Evidence).Append(" | ")
              .AppendLine(c.Notes);
        }
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthXml] read-only XML/data ownership scaffold.");
        sb.AppendLine("  Layers: " + s_layers.Length);
        sb.AppendLine("  File families: " + s_fileFamilies.Length);
        sb.AppendLine("  Cleanup targets: " + s_cleanupTargets.Length);
        sb.AppendLine("  No XML parsing/generation/modification occurs in this phase.");
        sb.AppendLine("  Use: rbxmlfresh layers | files [filter] | cleanup | summary");
        return sb.ToString();
    }
}
