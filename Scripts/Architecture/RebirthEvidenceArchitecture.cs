using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthEvidenceStatus
{
    NotStarted,
    IndexedOnly,
    StructuralSkim,
    FullBodyRead,
    MethodLevelDiff,
    RuntimeMeasured
}

public enum RebirthBudgetStatus
{
    UnmeasuredPendingAttribution,
    DerivedFromPriorFinding,
    RuntimeMeasured,
    NotApplicableDesignTimeOnly
}

public enum RebirthHotMethodRisk
{
    Unknown,
    Low,
    Moderate,
    High,
    Critical,
    DoNotPatchGlobally
}

public sealed class RebirthHotMethodDecl
{
    public string VanillaTypeName;
    public string MethodName;
    public string OwnerModuleId;
    public RebirthHotMethodRisk Risk;
    public RebirthPatchSafetyKind PatchSafetyKind;
    public string DriftEvidence;
    public string AdapterClassName;
    public string Notes;

    public string DisplayName
    {
        get { return (VanillaTypeName ?? "<unknown>") + "." + (MethodName ?? "<unknown>"); }
    }
}

public sealed class RebirthPatchOwnerDecl
{
    public string TargetTypeName;
    public string TargetMethodName;
    public string OwnerModuleId;
    public RebirthPatchSafetyKind SafetyKind;
    public string Evidence;
    public string Notes;

    public string DisplayName
    {
        get { return (TargetTypeName ?? "<unknown>") + "." + (TargetMethodName ?? "<unknown>"); }
    }
}

public sealed class RebirthPatchConflictDecl
{
    public string ConflictId;
    public string Target;
    public string[] Participants;
    public string Risk;
    public string Evidence;
    public string RequiredDecision;
}

public sealed class RebirthEvidenceDecl
{
    public string CategoryId;
    public string CategoryName;
    public RebirthEvidenceStatus Status;
    public string[] EvidenceSources;
    public string Notes;
    public string[] OpenQuestions;
}

/// <summary>
/// Read-only evidence and ownership registry for the fresh architecture project.
/// This intentionally does not install patches, change gameplay, or register scheduler callbacks.
/// </summary>
public static class RebirthArchitectureEvidenceRegistry
{
    private static readonly RebirthHotMethodDecl[] s_hotMethods = new[]
    {
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "PlayerMoveController",
            MethodName = "Update",
            OwnerModuleId = "player.movement",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P2/P5/P6.5: 1,483-line prefix in current project; near-complete 2.6->3.0 rewrite; collision risk with vendored patches.",
            AdapterClassName = "Harmony_RebirthPlayerMoveControllerUpdateAdapter",
            Notes = "First real architecture tenant after foundation. Must start no-behavior-change routing only."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "EntityAlive",
            MethodName = "DamageEntity",
            OwnerModuleId = "combat.damage",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P2: method-level extraction found 9 Rebirth patch files touching DamageEntity.",
            AdapterClassName = "Harmony_RebirthEntityAliveDamageEntityAdapter",
            Notes = "Needs a single damage dispatcher with registered policies."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "EntityMoveHelper",
            MethodName = "UpdateMoveHelper",
            OwnerModuleId = "pathing.vanilla",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P4/P5/V8: PathSmoothing patches this; EntityMoveHelper has large 2.6->3.0 drift.",
            AdapterClassName = "RebirthEntityMoveHelperAdapter",
            Notes = "Pathing adapter must isolate 2.6 vanilla access from stable REBIRTH policy."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "EntityAlive",
            MethodName = "FindPath",
            OwnerModuleId = "pathing.vanilla",
            Risk = RebirthHotMethodRisk.High,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P4: PathSmoothing patches FindPath.",
            AdapterClassName = "RebirthFindPathAdapter",
            Notes = "Do not absorb A* internals until pathing parity harness exists."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "BlockShapeNew",
            MethodName = "renderFace",
            OwnerModuleId = "paint.render",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "Prior paint/render performance findings; mesh/render path is ultra-hot.",
            AdapterClassName = "Harmony_RebirthRenderFaceAdapter",
            Notes = "No-data chunks must return before coordinate/tint work."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "ItemValue",
            MethodName = "ModifyValue",
            OwnerModuleId = "items.stats",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "Prior armor/item stat pipeline performance findings.",
            AdapterClassName = "Harmony_RebirthItemValueModifyValueAdapter",
            Notes = "First gate must be relevance table/bitmask; no tooltip work."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "Prefab",
            MethodName = "CopyIntoLocal",
            OwnerModuleId = "paint.copyblock",
            Risk = RebirthHotMethodRisk.High,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P5A2: moderate method-level drift around 34%.",
            AdapterClassName = "RebirthPaintPrefabCopyAdapter",
            Notes = "Separate from stable Prefab.copyFromWorld."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "WorldBuilder",
            MethodName = "GenerateData",
            OwnerModuleId = "world.caves",
            Risk = RebirthHotMethodRisk.Critical,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "P5A2: severe 2.6->3.0 drift; likely task-based worldgen refactor.",
            AdapterClassName = "RebirthCaveWorldGenCompat26",
            Notes = "TheDescent worldgen must not be treated as mechanical migration."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "Entity",
            MethodName = "OnUpdatePosition",
            OwnerModuleId = "none",
            Risk = RebirthHotMethodRisk.DoNotPatchGlobally,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "V8/P4: DroneLockToPlayer patches universal per-entity position update.",
            AdapterClassName = "none",
            Notes = "Drone follow-lock must use drone-specific hook or active set, never global Entity.OnUpdatePosition."
        },
        new RebirthHotMethodDecl
        {
            VanillaTypeName = "GUIUtils",
            MethodName = "DrawLine",
            OwnerModuleId = "none",
            Risk = RebirthHotMethodRisk.DoNotPatchGlobally,
            PatchSafetyKind = RebirthPatchSafetyKind.Behavioral,
            DriftEvidence = "V8/P4: Morecrosshairs patches low-level drawing primitive.",
            AdapterClassName = "none",
            Notes = "Crosshair rendering must hook crosshair-specific drawing or own reticle rendering."
        }
    };

    private static readonly RebirthPatchOwnerDecl[] s_patchOwners = new[]
    {
        new RebirthPatchOwnerDecl
        {
            TargetTypeName = "PlayerMoveController",
            TargetMethodName = "Update",
            OwnerModuleId = "player.movement",
            SafetyKind = RebirthPatchSafetyKind.Behavioral,
            Evidence = "P2/P5/P6.5",
            Notes = "Primary Tier A tenant; no-behavior-change routing first."
        },
        new RebirthPatchOwnerDecl
        {
            TargetTypeName = "EntityAlive",
            TargetMethodName = "DamageEntity",
            OwnerModuleId = "combat.damage",
            SafetyKind = RebirthPatchSafetyKind.Behavioral,
            Evidence = "P2",
            Notes = "Consolidate nine current patch surfaces into dispatcher policies."
        },
        new RebirthPatchOwnerDecl
        {
            TargetTypeName = "EntityFactory",
            TargetMethodName = "GetEntityType",
            OwnerModuleId = "entityfactory.typeResolver",
            SafetyKind = RebirthPatchSafetyKind.Structural,
            Evidence = "P5: method stable despite file-level drift.",
            Notes = "Structural patch protection concept must be preserved."
        },
        new RebirthPatchOwnerDecl
        {
            TargetTypeName = "EntityMoveHelper",
            TargetMethodName = "UpdateMoveHelper",
            OwnerModuleId = "pathing.vanilla",
            SafetyKind = RebirthPatchSafetyKind.Behavioral,
            Evidence = "P4/P5",
            Notes = "Requires compat seam and pathing parity harness."
        }
    };

    private static readonly RebirthPatchConflictDecl[] s_conflicts = new[]
    {
        new RebirthPatchConflictDecl
        {
            ConflictId = "player-move-update-multiowner",
            Target = "PlayerMoveController.Update",
            Participants = new[] { "REBIRTH", "SCore/NPCv2", "possible Dayuppy/other contributors" },
            Risk = "Critical",
            Evidence = "P6.5",
            RequiredDecision = "Declare one owner/adapter before behavior migration."
        },
        new RebirthPatchConflictDecl
        {
            ConflictId = "bloodmoon-aidirector-multiowner",
            Target = "AIDirectorBloodMoonParty",
            Participants = new[] { "Dayuppy", "TheDescent" },
            Risk = "High",
            Evidence = "P6.5",
            RequiredDecision = "Resolve patch ownership and terminology migration before Horde/BloodMoon architecture."
        },
        new RebirthPatchConflictDecl
        {
            ConflictId = "fire-manager-ownership",
            Target = "CustomFireManager / SCore Fire",
            Participants = new[] { "REBIRTH", "SCore/Fire" },
            Risk = "Unresolved",
            Evidence = "P6.5",
            RequiredDecision = "Decide owner before Fire/Heatmap architecture."
        }
    };

    private static readonly RebirthEvidenceDecl[] s_evidence = new[]
    {
        new RebirthEvidenceDecl
        {
            CategoryId = "player.movement",
            CategoryName = "Player Movement / Crawl / Camera / Input",
            Status = RebirthEvidenceStatus.MethodLevelDiff,
            EvidenceSources = new[] { "P1", "P2", "P5", "P6.5" },
            Notes = "Highest-evidence architecture target. PlayerMoveController.Update must be the first real tenant.",
            OpenQuestions = new[] { "Do [RebirthPlayerTimer] logs exist from SectionTimingEnabled?" }
        },
        new RebirthEvidenceDecl
        {
            CategoryId = "combat.damage",
            CategoryName = "DamageEntity / Combat Damage Dispatcher",
            Status = RebirthEvidenceStatus.StructuralSkim,
            EvidenceSources = new[] { "P2" },
            Notes = "DamageEntity has broader patch sprawl than OnUpdateLive.",
            OpenQuestions = new[] { "Full body review of all nine DamageEntity patch contributors." }
        },
        new RebirthEvidenceDecl
        {
            CategoryId = "pathing",
            CategoryName = "EntityMoveHelper / Local Route Guard / PathSmoothing",
            Status = RebirthEvidenceStatus.FullBodyRead,
            EvidenceSources = new[] { "P2", "P2.5", "P4", "P5" },
            Notes = "Local route guard is bounded; A* internals are high-risk and should not be absorbed by default.",
            OpenQuestions = new[] { "Remaining body sections of ZombieVerticalDropObstaclePathGuard are not fully read." }
        },
        new RebirthEvidenceDecl
        {
            CategoryId = "options",
            CategoryName = "Custom Game Options / 3.0 Migration",
            Status = RebirthEvidenceStatus.FullBodyRead,
            EvidenceSources = new[] { "P6", "V8" },
            Notes = "127 options read; HordeNight/BloodMoon is key semantic mapping trap.",
            OpenQuestions = new[] { "Produce OPTIONS_TRIAGE_127_v2.md." }
        },
        new RebirthEvidenceDecl
        {
            CategoryId = "xui.content",
            CategoryName = "XUi Content Ownership",
            Status = RebirthEvidenceStatus.StructuralSkim,
            EvidenceSources = new[] { "P6", "V7" },
            Notes = "3.0 splits XUi into Common/InGame/Menu and adds templates.xml.",
            OpenQuestions = new[] { "Full workstation/XUiC_WorkstationWindowGroup body read remains undone." }
        },
        new RebirthEvidenceDecl
        {
            CategoryId = "external.absorption",
            CategoryName = "External Project Absorption",
            Status = RebirthEvidenceStatus.FullBodyRead,
            EvidenceSources = new[] { "P3", "P4" },
            Notes = "External exclusions identified for DroneLockToPlayer, Morecrosshairs, DayCustomShaders, PathSmoothing.",
            OpenQuestions = new[] { "Is DayCustomShaders active in the user's live game?" }
        }
    };

    public static RebirthHotMethodDecl[] GetHotMethodsSnapshot()
    {
        RebirthHotMethodDecl[] copy = new RebirthHotMethodDecl[s_hotMethods.Length];
        Array.Copy(s_hotMethods, copy, copy.Length);
        return copy;
    }

    public static RebirthPatchOwnerDecl[] GetPatchOwnersSnapshot()
    {
        RebirthPatchOwnerDecl[] copy = new RebirthPatchOwnerDecl[s_patchOwners.Length];
        Array.Copy(s_patchOwners, copy, copy.Length);
        return copy;
    }

    public static RebirthPatchConflictDecl[] GetConflictsSnapshot()
    {
        RebirthPatchConflictDecl[] copy = new RebirthPatchConflictDecl[s_conflicts.Length];
        Array.Copy(s_conflicts, copy, copy.Length);
        return copy;
    }

    public static RebirthEvidenceDecl[] GetEvidenceSnapshot()
    {
        RebirthEvidenceDecl[] copy = new RebirthEvidenceDecl[s_evidence.Length];
        Array.Copy(s_evidence, copy, copy.Length);
        return copy;
    }

    public static string GetHotMethodsReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthArchitecture] hot-method ownership ledger:");
        for (int i = 0; i < s_hotMethods.Length; i++)
        {
            RebirthHotMethodDecl h = s_hotMethods[i];
            sb.Append("  ").Append(h.DisplayName)
              .Append(" owner=").Append(h.OwnerModuleId)
              .Append(" risk=").Append(h.Risk)
              .Append(" adapter=").Append(h.AdapterClassName)
              .Append(" evidence=").Append(h.DriftEvidence)
              .Append(" note=").AppendLine(h.Notes);
        }
        return sb.ToString();
    }

    public static string GetPatchOwnersReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthArchitecture] patch-owner ledger:");
        for (int i = 0; i < s_patchOwners.Length; i++)
        {
            RebirthPatchOwnerDecl p = s_patchOwners[i];
            sb.Append("  ").Append(p.DisplayName)
              .Append(" owner=").Append(p.OwnerModuleId)
              .Append(" safety=").Append(p.SafetyKind)
              .Append(" evidence=").Append(p.Evidence)
              .Append(" note=").AppendLine(p.Notes);
        }
        return sb.ToString();
    }

    public static string GetConflictsReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthArchitecture] known patch/ownership conflicts:");
        for (int i = 0; i < s_conflicts.Length; i++)
        {
            RebirthPatchConflictDecl c = s_conflicts[i];
            sb.Append("  ").Append(c.ConflictId)
              .Append(" target=").Append(c.Target)
              .Append(" risk=").Append(c.Risk)
              .Append(" participants=").Append(Join(c.Participants))
              .Append(" evidence=").Append(c.Evidence)
              .Append(" decision=").AppendLine(c.RequiredDecision);
        }
        return sb.ToString();
    }

    public static string GetEvidenceReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthArchitecture] evidence status ledger:");
        for (int i = 0; i < s_evidence.Length; i++)
        {
            RebirthEvidenceDecl e = s_evidence[i];
            sb.Append("  ").Append(e.CategoryId)
              .Append(" status=").Append(e.Status)
              .Append(" sources=").Append(Join(e.EvidenceSources))
              .Append(" name=").Append(e.CategoryName)
              .Append(" notes=").AppendLine(e.Notes);
            if (e.OpenQuestions != null && e.OpenQuestions.Length > 0)
                sb.Append("    open=").AppendLine(Join(e.OpenQuestions));
        }
        return sb.ToString();
    }

    private static string Join(string[] values)
    {
        if (values == null || values.Length == 0)
            return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i] ?? "");
        }
        return sb.ToString();
    }
}
