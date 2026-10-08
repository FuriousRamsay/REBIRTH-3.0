using System.Text;

#nullable disable

/// <summary>
/// Compile-time audit description for the fixed 3.1 attack-harvest target.
/// Target existence and patch parameters are validated by the build audit, not runtime reflection.
/// </summary>
public static class RebirthAttackHarvestTargetAudit
{
    public static string GetReport()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthAttackHarvestTargetAudit]");
        sb.AppendLine("  target: GameUtils.HarvestOnAttack(ItemActionData, Dictionary<string, ItemActionAttack.Bonuses>)");
        sb.AppendLine("  binding: explicit HarmonyPatch attribute");
        sb.AppendLine("  targetVerifiedAgainst: 3.1.0 b14 base source");
        sb.AppendLine("  installed: " + RebirthHarvestSystemsManualPatchInstaller.IsAttackHarvestPatchInstalled);
        sb.AppendLine(RebirthAttackHarvestPolicy.GetSummaryReport());
        sb.AppendLine(RebirthAttackHarvestCounters.GetSummaryReport());
        return sb.ToString();
    }
}
