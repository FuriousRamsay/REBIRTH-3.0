using System.Collections.Generic;

#nullable disable

/// <summary>
/// Owns the option-scoped vehicle downgrade projection. Native Block definitions are
/// shared singletons, so REBIRTH captures the baseline it found and restores it when
/// respawning is disabled. A same-type definition reload that reuses the Block object
/// is detected by comparing the live field with the exact value REBIRTH last wrote.
/// </summary>
public static class RebirthVehicleRespawnRuntimeRegistry
{
    private sealed class BindingState
    {
        public Block Block;
        public BlockValue NativeDowngrade;
        public BlockValue LastWrittenDowngrade;
        public bool HasOwnedWrite;
    }

    public sealed class BindingReport
    {
        public bool MarkerResolved;
        public bool HelperResolved;
        public bool PlaceholderResolved;
        public int TargetEntries;
        public int UniqueTargetTypes;
        public int NewlyBound;
        public int AlreadyBound;
        public int Restored;
        public int BaselineRecaptured;
        public int MissingTargetBlocks;
        public int WrongAfterBinding;

        public override string ToString()
        {
            return "marker=" + MarkerResolved
                + " helper=" + HelperResolved
                + " placeholder=" + PlaceholderResolved
                + " entries=" + TargetEntries
                + " unique=" + UniqueTargetTypes
                + " newlyBound=" + NewlyBound
                + " alreadyBound=" + AlreadyBound
                + " restored=" + Restored
                + " recaptured=" + BaselineRecaptured
                + " missing=" + MissingTargetBlocks
                + " wrongAfterBinding=" + WrongAfterBinding;
        }
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, BindingState> Bindings =
        new Dictionary<int, BindingState>();

    public static BindingReport BindCarsRandomHelperTargets()
    {
        lock (Sync)
            return ApplyCurrentOptionStateUnsafe();
    }

    public static void ApplyCurrentOptionState()
    {
        lock (Sync)
            ApplyCurrentOptionStateUnsafe();
    }

    private static BindingReport ApplyCurrentOptionStateUnsafe()
    {
        BindingReport report = new BindingReport();
        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        BlockValue helper = Block.GetBlockValue("carsRandomHelper");
        report.MarkerResolved = !marker.isair && marker.Block != null;
        report.HelperResolved = !helper.isair && helper.Block != null;

        if (!report.MarkerResolved || !report.HelperResolved || BlockPlaceholderMap.Instance == null)
            return report;

        List<BlockPlaceholderMap.PlaceholderTarget> targets;
        if (!BlockPlaceholderMap.Instance.placeholders.TryGetValue(helper, out targets) || targets == null)
            return report;

        report.PlaceholderResolved = true;
        report.TargetEntries = targets.Count;
        HashSet<int> visitedTypes = new HashSet<int>();

        for (int i = 0; i < targets.Count; i++)
        {
            BlockValue targetValue = targets[i].blockValue;
            Block targetBlock = targetValue.Block;
            if (targetValue.isair || targetBlock == null)
            {
                report.MissingTargetBlocks++;
                continue;
            }

            if (!visitedTypes.Add(targetValue.type))
                continue;

            report.UniqueTargetTypes++;
            ApplyBindingUnsafe(targetBlock, marker, report);
        }

        return report;
    }

    private static void ApplyBindingUnsafe(Block targetBlock, BlockValue marker, BindingReport report)
    {
        BindingState state;
        if (!Bindings.TryGetValue(targetBlock.blockID, out state) || state == null)
        {
            state = new BindingState
            {
                Block = targetBlock,
                NativeDowngrade = targetBlock.DowngradeBlock
            };
            Bindings[targetBlock.blockID] = state;
        }
        else
        {
            bool sameObject = object.ReferenceEquals(state.Block, targetBlock);
            bool stillOwns = sameObject
                && state.HasOwnedWrite
                && targetBlock.DowngradeBlock.type == state.LastWrittenDowngrade.type;

            if (!sameObject || (state.HasOwnedWrite && !stillOwns))
            {
                state.Block = targetBlock;
                state.NativeDowngrade = targetBlock.DowngradeBlock;
                state.HasOwnedWrite = false;
                report.BaselineRecaptured++;
            }
        }

        if (RebirthVehicleBlockRespawnRuntimePolicy.Enabled)
        {
            bool already = targetBlock.DowngradeBlock.type == marker.type;
            targetBlock.DowngradeBlock = marker;
            state.LastWrittenDowngrade = marker;
            state.HasOwnedWrite = true;
            if (targetBlock.DowngradeBlock.type != marker.type)
                report.WrongAfterBinding++;
            else if (already)
                report.AlreadyBound++;
            else
                report.NewlyBound++;
        }
        else
        {
            // Restore only the baseline associated with the current generation.
            targetBlock.DowngradeBlock = state.NativeDowngrade;
            state.LastWrittenDowngrade = state.NativeDowngrade;
            state.HasOwnedWrite = true;
            if (targetBlock.DowngradeBlock.type != state.NativeDowngrade.type)
                report.WrongAfterBinding++;
            else
                report.Restored++;
        }
    }

    public static bool EnsureSpawnedBlockIsRepeatable(
        Block spawnedBlock,
        out string previousDowngrade)
    {
        previousDowngrade = "unavailable";
        if (spawnedBlock == null || !RebirthVehicleBlockRespawnRuntimePolicy.Enabled)
            return false;

        BlockValue marker = Block.GetBlockValue("carRespawner_FR");
        if (marker.isair || marker.Block == null)
            return false;

        lock (Sync)
        {
            previousDowngrade = spawnedBlock.DowngradeBlock.isair || spawnedBlock.DowngradeBlock.Block == null
                ? "air"
                : spawnedBlock.DowngradeBlock.Block.blockName;

            BindingReport ignored = new BindingReport();
            ApplyBindingUnsafe(spawnedBlock, marker, ignored);
            return spawnedBlock.DowngradeBlock.type == marker.type;
        }
    }
}
