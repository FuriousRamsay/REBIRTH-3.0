using System;
using System.Text;

#nullable disable

public enum RebirthItemStatTenantArea
{
    Unknown,
    ModifyValueDispatcher,
    RelevanceGate,
    EquipmentSnapshotCache,
    ArmorResistanceSnapshot,
    QualityRollSnapshot,
    TooltipTextCache,
    OptionValueCache,
    Diagnostics
}

public enum RebirthItemStatTenantStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired
}

public enum RebirthItemStatHotRule
{
    Unknown,
    NoGamePrefsRead,
    NoXmlLookup,
    NoTooltipConstruction,
    NoItemScan,
    NoLinq,
    NoReflection,
    NoStringFormatting,
    CachedSnapshotOnly
}

public sealed class RebirthItemStatTenantDecl
{
    public RebirthItemStatTenantArea Area;
    public RebirthItemStatTenantStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthItemStatHotRuleDecl
{
    public RebirthItemStatHotRule Rule;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthItemStatContext
{
    public ItemValue ItemValue;
    public bool HasItemValue;
    public bool ShouldFastExit;
    public string Reason;

    public static RebirthItemStatContext Empty(string reason)
    {
        return new RebirthItemStatContext
        {
            ItemValue = null,
            HasItemValue = false,
            ShouldFastExit = true,
            Reason = reason
        };
    }
}

/// <summary>
/// No-behavior ItemValue.ModifyValue tenant scaffold.
/// This intentionally does not alter item stats, armor, quality rolls, or tooltips.
/// </summary>
public static class RebirthItemStatPipeline
{
    private static readonly RebirthItemStatTenantDecl[] s_tenants = new[]
    {
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.ModifyValueDispatcher,
            Status = RebirthItemStatTenantStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "items.stats",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior ItemValue performance findings",
            Notes = "Future single owner for ItemValue.ModifyValue. No behavior in this phase."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.RelevanceGate,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "items.stats",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "First gate should use indexed stat relevance/bitmask before any feature policy runs."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.EquipmentSnapshotCache,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "items.equipment",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / DamageEntity policy plan",
            Notes = "Equipment state must be cached outside ModifyValue and DamageEntity hot paths."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.ArmorResistanceSnapshot,
            Status = RebirthItemStatTenantStatus.ManualReviewRequired,
            OwnerModuleId = "items.armor",
            HotPath = true,
            MayAllocate = false,
            Evidence = "prior armor/ItemValue performance findings",
            Notes = "Only Physical/Elemental/General/Explosion/BuffResistance-style stats should participate if indexed."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.QualityRollSnapshot,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "items.quality",
            HotPath = false,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior quality-roll design",
            Notes = "Quality randomness belongs at roll/create time and persists rolled values; not per ModifyValue call."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.TooltipTextCache,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "ui.tooltip",
            HotPath = false,
            MayAllocate = true,
            Evidence = "Blueprint v2 / debug policy",
            Notes = "Tooltip strings may be built only in UI/cache paths, never in stat/combat hot paths."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.OptionValueCache,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "options.cache",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / P6",
            Notes = "Custom option values feeding stats must be cached; no GamePrefs reads in ModifyValue."
        },
        new RebirthItemStatTenantDecl
        {
            Area = RebirthItemStatTenantArea.Diagnostics,
            Status = RebirthItemStatTenantStatus.Planned,
            OwnerModuleId = "items.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "debug policy",
            Notes = "Diagnostics must compile out or be gated before string construction."
        }
    };

    private static readonly RebirthItemStatHotRuleDecl[] s_rules = new[]
    {
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoGamePrefsRead,
            OwnerModuleId = "items.stats",
            Evidence = "Blueprint v2",
            Notes = "Option values must be cached outside ItemValue.ModifyValue."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoXmlLookup,
            OwnerModuleId = "items.stats",
            Evidence = "Blueprint v2 / XML ledgers",
            Notes = "XML-derived item/block/buff metadata must be indexed during load/reload, not read in hot path."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoTooltipConstruction,
            OwnerModuleId = "items.stats",
            Evidence = "Blueprint v2 / prior perf issues",
            Notes = "Tooltip text belongs to UI cache layer only."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoItemScan,
            OwnerModuleId = "items.stats",
            Evidence = "Blueprint v2",
            Notes = "Use snapshot caches; do not scan inventory/equipment every ModifyValue call."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoLinq,
            OwnerModuleId = "items.stats",
            Evidence = "debug/performance policy",
            Notes = "Avoid LINQ allocations and enumerator churn in ultra-hot stat paths."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoReflection,
            OwnerModuleId = "items.stats",
            Evidence = "debug/performance policy",
            Notes = "No reflection in ModifyValue or damage/stat hot paths."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.NoStringFormatting,
            OwnerModuleId = "items.diagnostics",
            Evidence = "debug policy",
            Notes = "No string interpolation/formatting unless compile-time or bool-gated before construction."
        },
        new RebirthItemStatHotRuleDecl
        {
            Rule = RebirthItemStatHotRule.CachedSnapshotOnly,
            OwnerModuleId = "items.stats",
            Evidence = "Blueprint v2",
            Notes = "ModifyValue should read precomputed compact snapshots/bitmasks only."
        }
    };

    public static RebirthItemStatTenantDecl[] GetTenantsSnapshot()
    {
        RebirthItemStatTenantDecl[] copy = new RebirthItemStatTenantDecl[s_tenants.Length];
        Array.Copy(s_tenants, copy, copy.Length);
        return copy;
    }

    public static RebirthItemStatContext BuildNoBehaviorContext(ItemValue itemValue)
    {
        if (itemValue == null)
            return RebirthItemStatContext.Empty("itemValue is null");

        return new RebirthItemStatContext
        {
            ItemValue = itemValue,
            HasItemValue = true,
            ShouldFastExit = false,
            Reason = "scaffold only; no stat policy execution yet"
        };
    }

    public static void OnModifyValueNoBehavior(ItemValue itemValue)
    {
        // Intentional no-op.
        // Future ItemValue.ModifyValue adapter target only.
        BuildNoBehaviorContext(itemValue);
    }

    public static string GetTenantReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthItemStats] no-behavior ItemValue.ModifyValue tenant scaffold.");
        sb.AppendLine("  No Harmony patch is installed in this phase.");
        sb.AppendLine("  No item/stat/armor/quality behavior is changed in this phase.");
        sb.AppendLine("area | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_tenants.Length; i++)
        {
            RebirthItemStatTenantDecl t = s_tenants[i];
            sb.Append(t.Area).Append(" | ")
              .Append(t.Status).Append(" | ")
              .Append(t.OwnerModuleId).Append(" | ")
              .Append(t.HotPath).Append(" | ")
              .Append(t.MayAllocate).Append(" | ")
              .Append(t.Evidence).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetRuleReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthItemStats] hot-path stat rules.");
        sb.AppendLine("rule | owner | evidence | notes");

        for (int i = 0; i < s_rules.Length; i++)
        {
            RebirthItemStatHotRuleDecl r = s_rules[i];
            sb.Append(r.Rule).Append(" | ")
              .Append(r.OwnerModuleId).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthItemStats] safety rules:");
        sb.AppendLine("  1. Only one future REBIRTH patch may own ItemValue.ModifyValue.");
        sb.AppendLine("  2. No GamePrefs reads in ModifyValue.");
        sb.AppendLine("  3. No XML lookups in ModifyValue.");
        sb.AppendLine("  4. No inventory/equipment scans in ModifyValue.");
        sb.AppendLine("  5. No tooltip text construction in ModifyValue.");
        sb.AppendLine("  6. No LINQ/reflection/string formatting in stat hot paths.");
        sb.AppendLine("  7. Quality randomness is roll-time persisted data, not call-time randomness.");
        sb.AppendLine("  8. Diagnostics must compile out or be bool-gated before construction.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthItemStats] ItemValue.ModifyValue tenant entries: " + s_tenants.Length
            + "; hot-path rules: " + s_rules.Length
            + ". This phase is no-behavior and installs no Harmony patches.";
    }
}
