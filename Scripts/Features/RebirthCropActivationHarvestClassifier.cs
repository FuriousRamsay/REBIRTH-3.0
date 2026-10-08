using System;
using System.Text;

#nullable disable

public enum RebirthCropActivationKind
{
    Unknown = 0,
    WildOrNatural = 1,
    PlayerGrown = 2
}

/// <summary>
/// Crop activation classifier backed by a generated static table from ConfigsDump(50).zip.
/// No runtime XML lookup is performed in the patch.
/// </summary>
public static class RebirthCropActivationHarvestClassifier
{
    public static RebirthCropActivationKind ClassifyBlockName(string blockName)
    {
        return RebirthCropActivationHarvestGeneratedCropTable.Classify(blockName);
    }

    public static bool IsPlayerGrown(string blockName)
    {
        return ClassifyBlockName(blockName) == RebirthCropActivationKind.PlayerGrown;
    }

    public static bool IsKnownCrop(string blockName)
    {
        RebirthCropActivationKind kind = ClassifyBlockName(blockName);
        return kind == RebirthCropActivationKind.WildOrNatural || kind == RebirthCropActivationKind.PlayerGrown;
    }

    public static string GetPreviewReport(string blockName)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthCropActivationHarvestClassifier] preview:");
        sb.AppendLine("  blockName: " + (blockName ?? "<null>"));
        sb.AppendLine("  kind: " + ClassifyBlockName(blockName));
        sb.AppendLine("  isKnownCrop: " + IsKnownCrop(blockName));
        sb.AppendLine("  source: " + RebirthCropActivationHarvestGeneratedCropTable.Source);
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthCropActivationHarvestClassifier] safety:");
        sb.AppendLine("  1. No runtime XML lookup.");
        sb.AppendLine("  2. Static generated source table.");
        sb.AppendLine("  3. No dictionary allocation.");
        sb.AppendLine("  4. No LINQ.");
        sb.AppendLine("  5. Unknown stays unknown.");
        sb.AppendLine("  6. Table source: " + RebirthCropActivationHarvestGeneratedCropTable.Source);
        return sb.ToString();
    }
}
