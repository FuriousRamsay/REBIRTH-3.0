using System.Text;

#nullable disable

/// <summary>
/// Documents the crop activation replacement flow so tests can compare intended order.
/// This is read-only and has no gameplay side effects.
/// </summary>
public static class RebirthCropActivationHarvestFlow
{
    public static string GetAwardBeforeMutationReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCropActivationHarvestFlow] Milestone 4I flow:");
        sb.AppendLine("  1. disabled gate returns to vanilla immediately");
        sb.AppendLine("  2. trader area denial exits before item/block mutation");
        sb.AppendLine("  3. harvest drop/count is computed");
        sb.AppendLine("  4. ItemStack is created");
        sb.AppendLine("  5. item is awarded to inventory/bag");
        sb.AppendLine("  6. only after successful item award, block is mutated");
        sb.AppendLine("  7. player-grown crops use optional DowngradeBlock path");
        sb.AppendLine("  8. wild/natural crops use setPlantBackToBaby + BlockPickedUp path");
        sb.AppendLine("  9. UI harvesting display is added");
        sb.AppendLine("  10. inventory/bag overflow drops the awarded stack on the ground, then crop mutation may proceed");
        return sb.ToString();
    }
}
