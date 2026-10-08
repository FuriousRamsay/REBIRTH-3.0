using System;
using System.Text;

#nullable disable

public enum RebirthPatchRegistryDecision
{
    Unknown,
    AllowedFutureOwner,
    StructuralOnly,
    BehaviorDeferred,
    DiagnosticBuildOnly,
    DoNotPatchGlobally,
    ExternalRiskDeferred
}

public sealed class RebirthPatchRegistryEntry
{
    public string TargetTypeName;
    public string TargetMethodName;
    public string OwnerModuleId;
    public RebirthPatchSafetyKind SafetyKind;
    public RebirthPatchRegistryDecision Decision;
    public string Evidence;
    public string Notes;

    public string TargetDisplayName
    {
        get { return (TargetTypeName ?? "<unknown>") + "." + (TargetMethodName ?? "<unknown>"); }
    }
}

/// <summary>
/// Read-only patch ownership registry for the fresh architecture project.
/// This is a ledger only. It does not patch, unpatch, call Harmony, or alter gameplay.
/// </summary>
public static class RebirthPatchRegistry
{
    private static RebirthPatchRegistryEntry[] s_entries;
    private static bool s_registered;

    public static void EnsureRegistered()
    {
        if (s_registered)
            return;

        s_registered = true;
        s_entries = new[]
        {
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "PlayerMoveController",
                TargetMethodName = "Update",
                OwnerModuleId = "player.movement",
                SafetyKind = RebirthPatchSafetyKind.Behavioral,
                Decision = RebirthPatchRegistryDecision.BehaviorDeferred,
                Evidence = "P2/P5/P6.5",
                Notes = "First real tenant later. Phase 1C installs no adapter and no patch."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "EntityAlive",
                TargetMethodName = "DamageEntity",
                OwnerModuleId = "combat.damage",
                SafetyKind = RebirthPatchSafetyKind.Behavioral,
                Decision = RebirthPatchRegistryDecision.BehaviorDeferred,
                Evidence = "P2",
                Notes = "Needs future single dispatcher. Phase 1C installs no patch."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "EntityFactory",
                TargetMethodName = "GetEntityType",
                OwnerModuleId = "entityfactory.typeResolver",
                SafetyKind = RebirthPatchSafetyKind.Structural,
                Decision = RebirthPatchRegistryDecision.StructuralOnly,
                Evidence = "P5",
                Notes = "Method-level drift stable; structural patch protection concept preserved, not applied."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "EntityMoveHelper",
                TargetMethodName = "UpdateMoveHelper",
                OwnerModuleId = "pathing.vanilla",
                SafetyKind = RebirthPatchSafetyKind.Behavioral,
                Decision = RebirthPatchRegistryDecision.BehaviorDeferred,
                Evidence = "P4/P5/V8",
                Notes = "Large drift and external PathSmoothing risk; no Phase 1C patch."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "Entity",
                TargetMethodName = "OnUpdatePosition",
                OwnerModuleId = "none",
                SafetyKind = RebirthPatchSafetyKind.Behavioral,
                Decision = RebirthPatchRegistryDecision.DoNotPatchGlobally,
                Evidence = "V8/P4",
                Notes = "DroneLockToPlayer behavior must never be absorbed as a universal Entity.OnUpdatePosition patch."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "GUIUtils",
                TargetMethodName = "DrawLine",
                OwnerModuleId = "none",
                SafetyKind = RebirthPatchSafetyKind.Behavioral,
                Decision = RebirthPatchRegistryDecision.DoNotPatchGlobally,
                Evidence = "V8/P4",
                Notes = "Morecrosshairs behavior must not be absorbed through a global GUIUtils.DrawLine patch."
            },
            new RebirthPatchRegistryEntry
            {
                TargetTypeName = "LayerScanner_OnUpdateLive_Patch",
                TargetMethodName = "EntityPlayerLocal.OnUpdateLive",
                OwnerModuleId = "visuals.shaders",
                SafetyKind = RebirthPatchSafetyKind.Diagnostic,
                Decision = RebirthPatchRegistryDecision.DiagnosticBuildOnly,
                Evidence = "P3",
                Notes = "DayCustomShaders renderer scan must be discarded or diagnostic-build only."
            }
        };
    }

    public static RebirthPatchRegistryEntry[] GetEntriesSnapshot()
    {
        EnsureRegistered();
        RebirthPatchRegistryEntry[] copy = new RebirthPatchRegistryEntry[s_entries.Length];
        Array.Copy(s_entries, copy, copy.Length);
        return copy;
    }

    public static string GetReport()
    {
        EnsureRegistered();
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPatchRegistry] read-only patch registry; no Harmony patches installed by this registry.");

        for (int i = 0; i < s_entries.Length; i++)
        {
            RebirthPatchRegistryEntry e = s_entries[i];
            sb.Append("  ")
              .Append(e.TargetDisplayName)
              .Append(" owner=").Append(e.OwnerModuleId)
              .Append(" safety=").Append(e.SafetyKind)
              .Append(" decision=").Append(e.Decision)
              .Append(" evidence=").Append(e.Evidence)
              .Append(" note=").AppendLine(e.Notes);
        }

        return sb.ToString();
    }
}
