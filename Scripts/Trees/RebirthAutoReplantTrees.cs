using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Authoritative per-world switch for the 2.6 tree auto-replant contract.
/// </summary>
public static class RebirthAutoReplantTreesRuntimePolicy
{
    private static bool enabled;

    public static bool Enabled { get { return enabled; } }

    public static void SetEnabled(bool value)
    {
        enabled = value;
        RebirthAutoReplantTreeRegistry.ApplyEnabledState();
    }
}

public sealed class RebirthTreeReplantDefinition
{
    public readonly string SourceBlockName;
    public readonly string ReplacementBlockName;

    public int SourceBlockType;
    public int ReplacementBlockType;
    public Block RuntimeBlock;
    public BlockValue OriginalDowngrade;
    public bool OriginalFallOver;
    public bool OriginalStateCaptured;
    public BlockValue LastWrittenDowngrade;
    public bool LastWrittenFallOver;
    public bool HasOwnedRuntimeWrite;

    public RebirthTreeReplantDefinition(string sourceBlockName, string replacementBlockName)
    {
        SourceBlockName = sourceBlockName;
        ReplacementBlockName = replacementBlockName;
    }
}

/// <summary>
/// Exact 2.6 source-to-sapling target map with native 3.1 fall completion.
///
/// The 2.6 implementation did not decide eligibility from "planted", "mature",
/// or growth-stage naming. Its blocks XML assigned explicit DowngradeBlock mappings
/// to a fixed set of wild and planted trees. 2.6 also forced FallOver=false, which
/// caused an immediate downgrade and skipped the falling-tree animation.
///
/// This 3.1 port preserves the exact 2.6 target map but deliberately preserves each
/// tree's native bFallOver value. Normal trees therefore spawn EntityFallingTree,
/// complete their physics/fade sequence, and are replanted from DestroyTree. The
/// DowngradeBlock remains bound as a fallback for trees that cannot start falling.
/// </summary>
public static class RebirthAutoReplantTreeRegistry
{
    public sealed class BindingReport
    {
        public bool BlocksLoaded;
        public bool Enabled;
        public int Targets;
        public int ResolvedTargets;
        public int BoundTargets;
        public int RestoredTargets;
        public int MissingSource;
        public int MissingReplacement;
        public int WrongClass;
        public int DuplicateSource;
        public int WrongRuntimeState;

        public override string ToString()
        {
            return "blocksLoaded=" + BlocksLoaded
                + " enabled=" + Enabled
                + " targets=" + Targets
                + " resolvedTargets=" + ResolvedTargets
                + " boundTargets=" + BoundTargets
                + " restoredTargets=" + RestoredTargets
                + " missingSource=" + MissingSource
                + " missingReplacement=" + MissingReplacement
                + " wrongClass=" + WrongClass
                + " duplicateSource=" + DuplicateSource
                + " wrongRuntimeState=" + WrongRuntimeState;
        }
    }

    private static readonly RebirthTreeReplantDefinition[] definitions =
    {
        new RebirthTreeReplantDefinition("treePlantedWinterPine6m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treeWinterEverGreen", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treePlantedMountainPine12m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeJuniper4m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeOakSml01", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treePlantedOak08m", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeMountainPine12m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedWinterPine13m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treeWinterPine13m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treeMountainPine19m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedOak16m", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeOakMed01", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treePlantedMountainPine19m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedWinterPine19m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treeWinterPine19m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treeMountainPine31m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeMountainPine27m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedOak27m", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeOakMed02", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeMountainPineDry21m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedMountainPine27m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeMountainPine41m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedOak41m", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeOakLrg01", "treePlantedOak1m"),
        new RebirthTreeReplantDefinition("treeMountainPine48m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeFirLrg01", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treePlantedMountainPine41m", "treePlantedMountainPine1m"),
        new RebirthTreeReplantDefinition("treeWinterPine28m", "treePlantedWinterPine1m"),
        new RebirthTreeReplantDefinition("treePlantedWinterPine28m", "treePlantedWinterPine1m")
    };

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthTreeReplantDefinition> bySourceType =
        new Dictionary<int, RebirthTreeReplantDefinition>();

    private static BindingReport lastReport = new BindingReport
    {
        Targets = definitions.Length
    };

    public static RebirthTreeReplantDefinition[] Definitions { get { return definitions; } }

    public static BindingReport LastReport
    {
        get
        {
            lock (Sync)
                return lastReport;
        }
    }

    public static BindingReport ApplyEnabledState()
    {
        lock (Sync)
        {
            BindingReport report = new BindingReport
            {
                BlocksLoaded = Block.BlocksLoaded,
                Enabled = RebirthAutoReplantTreesRuntimePolicy.Enabled,
                Targets = definitions.Length
            };

            if (!Block.BlocksLoaded)
            {
                lastReport = report;
                return report;
            }

            bySourceType.Clear();

            for (int i = 0; i < definitions.Length; i++)
            {
                RebirthTreeReplantDefinition definition = definitions[i];
                BlockValue sourceValue = Block.GetBlockValue(definition.SourceBlockName);
                BlockValue replacementValue = Block.GetBlockValue(definition.ReplacementBlockName);

                definition.SourceBlockType = sourceValue.isair ? 0 : sourceValue.type;
                definition.ReplacementBlockType = replacementValue.isair ? 0 : replacementValue.type;

                Block sourceBlock = sourceValue.isair ? null : sourceValue.Block;
                Block replacementBlock = replacementValue.isair ? null : replacementValue.Block;

                if (sourceBlock == null)
                {
                    report.MissingSource++;
                    continue;
                }

                if (replacementBlock == null)
                {
                    report.MissingReplacement++;
                    continue;
                }

                BlockModelTree runtimeTree = sourceBlock as BlockModelTree;
                if (runtimeTree == null)
                {
                    report.WrongClass++;
                    continue;
                }

                bool sameRuntimeObject = ReferenceEquals(definition.RuntimeBlock, sourceBlock);
                bool stillOwnsLastWrite = sameRuntimeObject
                    && definition.HasOwnedRuntimeWrite
                    && sourceBlock.DowngradeBlock.type == definition.LastWrittenDowngrade.type
                    && runtimeTree.bFallOver == definition.LastWrittenFallOver;

                // Native definition reloads can reuse the same Block instance. If
                // the live fields no longer equal the exact values REBIRTH last
                // wrote, treat them as a fresh native/third-party baseline rather
                // than restoring stale state captured from the previous generation.
                if (!definition.OriginalStateCaptured
                    || !sameRuntimeObject
                    || (definition.HasOwnedRuntimeWrite && !stillOwnsLastWrite))
                {
                    definition.RuntimeBlock = sourceBlock;
                    definition.OriginalDowngrade = sourceBlock.DowngradeBlock;
                    definition.OriginalFallOver = runtimeTree.bFallOver;
                    definition.OriginalStateCaptured = true;
                    definition.HasOwnedRuntimeWrite = false;
                }

                if (bySourceType.ContainsKey(definition.SourceBlockType))
                {
                    report.DuplicateSource++;
                    continue;
                }

                bySourceType.Add(definition.SourceBlockType, definition);
                report.ResolvedTargets++;

                if (RebirthAutoReplantTreesRuntimePolicy.Enabled)
                {
                    // Preserve normal 3.1 falling-tree animation. The downgrade is
                    // used only when the native tree cannot start falling; successful
                    // falls are replanted by EntityFallingTree.DestroyTree.
                    sourceBlock.DowngradeBlock = replacementValue;
                    runtimeTree.bFallOver = definition.OriginalFallOver;
                    report.BoundTargets++;

                    if (sourceBlock.DowngradeBlock.type != replacementValue.type
                        || runtimeTree.bFallOver != definition.OriginalFallOver)
                    {
                        report.WrongRuntimeState++;
                    }
                }
                else
                {
                    sourceBlock.DowngradeBlock = definition.OriginalDowngrade;
                    runtimeTree.bFallOver = definition.OriginalFallOver;
                    report.RestoredTargets++;

                    if (sourceBlock.DowngradeBlock.type != definition.OriginalDowngrade.type
                        || runtimeTree.bFallOver != definition.OriginalFallOver)
                    {
                        report.WrongRuntimeState++;
                    }
                }

                definition.LastWrittenDowngrade = sourceBlock.DowngradeBlock;
                definition.LastWrittenFallOver = runtimeTree.bFallOver;
                definition.HasOwnedRuntimeWrite = true;
            }

            lastReport = report;
            return report;
        }
    }

    public static bool TryGet(BlockValue value, out RebirthTreeReplantDefinition definition)
    {
        lock (Sync)
        {
            if (Block.BlocksLoaded && bySourceType.Count == 0)
                ApplyEnabledState();

            return bySourceType.TryGetValue(value.type, out definition);
        }
    }


    /// <summary>
    /// Replants an eligible tree only after the native falling-tree entity reaches
    /// its completion/removal point. This keeps the full 3.1 fall and fade animation.
    /// </summary>
    public static bool TryReplantAfterFall(EntityFallingTree fallingTree, string reason)
    {
        if (!RebirthAutoReplantTreesRuntimePolicy.Enabled
            || fallingTree == null
            || fallingTree.world == null
            || fallingTree.isEntityRemote)
        {
            return false;
        }

        RebirthTreeReplantDefinition definition;
        if (!TryGet(fallingTree.treeBV, out definition))
            return false;

        Vector3i blockPos = fallingTree.treeBlockPos;
        BlockValue current = fallingTree.world.GetBlock(blockPos);

        // Never overwrite a block placed while the tree was falling. A second
        // completion/unload callback also becomes harmless once the sapling exists.
        if (!current.isair)
            return current.type == definition.ReplacementBlockType;

        BlockValue replacement = Block.GetBlockValue(definition.ReplacementBlockName);
        if (replacement.isair || replacement.Block == null)
            return false;

        fallingTree.world.SetBlockRPC((BlockValueRef)blockPos, replacement);

#if DEBUG && REBIRTH_DEBUG
        Log.Out("[REBIRTH Auto Replant Trees][Debug] fall-complete reason="
            + reason
            + " source=" + definition.SourceBlockName
            + " replacement=" + definition.ReplacementBlockName
            + " pos=" + blockPos);
#endif

        return true;
    }

    /// <summary>
    /// Exact 2.6 harvest-item rule: while auto-replant is enabled, suppress any
    /// harvested item whose internal item name contains "treePlanted".
    /// </summary>
    public static bool ShouldSuppressSapling(ItemValue itemValue)
    {
        if (!RebirthAutoReplantTreesRuntimePolicy.Enabled
            || itemValue == null
            || itemValue.ItemClass == null)
        {
            return false;
        }

        string itemName = itemValue.ItemClass.GetItemName();
        return !string.IsNullOrEmpty(itemName)
            && itemName.IndexOf("treePlanted", StringComparison.Ordinal) >= 0;
    }

    public static string BuildInfoReport()
    {
        BindingReport report = ApplyEnabledState();
        StringBuilder b = new StringBuilder();
        b.AppendLine("[rbtrees] option="
            + (RebirthAutoReplantTreesRuntimePolicy.Enabled ? "On" : "Off")
            + " " + report);

        for (int i = 0; i < definitions.Length; i++)
        {
            RebirthTreeReplantDefinition definition = definitions[i];
            string currentDowngrade = definition.RuntimeBlock == null
                || definition.RuntimeBlock.DowngradeBlock.isair
                || definition.RuntimeBlock.DowngradeBlock.Block == null
                ? "air"
                : definition.RuntimeBlock.DowngradeBlock.Block.blockName;

            BlockModelTree runtimeTree = definition.RuntimeBlock as BlockModelTree;
            b.AppendLine("  source=" + definition.SourceBlockName
                + "(" + definition.SourceBlockType + ")"
                + " -> " + definition.ReplacementBlockName
                + "(" + definition.ReplacementBlockType + ")"
                + " class=" + (definition.RuntimeBlock == null
                    ? "null"
                    : definition.RuntimeBlock.GetType().Name)
                + " runtimeDowngrade=" + currentDowngrade
                + " fallOver=" + (runtimeTree != null && runtimeTree.bFallOver)
                + " originalFallOver=" + definition.OriginalFallOver);
        }

        return b.ToString();
    }

    public static string BuildValidationReport()
    {
        BindingReport report = ApplyEnabledState();
        StringBuilder b = new StringBuilder();
        b.AppendLine("[rbtrees validate] " + report);

        int errors = report.MissingSource
            + report.MissingReplacement
            + report.WrongClass
            + report.DuplicateSource
            + report.WrongRuntimeState;

        for (int i = 0; i < definitions.Length; i++)
        {
            RebirthTreeReplantDefinition definition = definitions[i];

            if (definition.SourceBlockType == 0)
                b.AppendLine("ERROR missing source " + definition.SourceBlockName);

            if (definition.ReplacementBlockType == 0)
                b.AppendLine("ERROR missing replacement "
                    + definition.ReplacementBlockName
                    + " for " + definition.SourceBlockName);

            if (definition.RuntimeBlock != null
                && !(definition.RuntimeBlock is BlockModelTree))
            {
                b.AppendLine("ERROR wrong class "
                    + definition.SourceBlockName
                    + " class=" + definition.RuntimeBlock.GetType().Name);
            }
        }

        if (errors == 0)
        {
            b.AppendLine("OK: exact 2.6 source map is valid for all "
                + definitions.Length
                + " wild and planted tree targets; native fall animation is preserved.");
        }
        else
        {
            b.AppendLine("ERRORS=" + errors);
        }

        return b.ToString();
    }
}

public static class RebirthAutoReplantTreesInstaller
{
    private static bool installed;

    public static void Install()
    {
        if (installed)
            return;

        installed = true;
        Harmony harmony = new Harmony("rebirth.auto-replant-trees.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthAutoReplantTreeHarvestPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthAutoReplantTreeFallingCompletionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(
            harmony,
            typeof(RebirthAutoReplantTreeFallingUnloadPatch));

        // GameStartDone performs the authoritative bind after Block.LateInitAll.
        RebirthAutoReplantTreeRegistry.ApplyEnabledState();
    }
}

[Preserve]
public sealed class RebirthAutoReplantTreesModApi : IModApi
{
    private static bool initialized;

    public void InitMod(Mod modInstance)
    {
        if (initialized)
            return;

        initialized = true;
        ModEvents.GameStartDone.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartDoneData>(
                OnGameStartDone));
    }

    private static void OnGameStartDone(ref ModEvents.SGameStartDoneData data)
    {
        RebirthAutoReplantTreeRegistry.BindingReport report =
            RebirthAutoReplantTreeRegistry.ApplyEnabledState();

        if (!report.BlocksLoaded || report.MissingSource > 0 || report.MissingReplacement > 0 ||
            report.WrongClass > 0 || report.DuplicateSource > 0 || report.WrongRuntimeState > 0)
            Log.Warning("[REBIRTH Auto Replant Trees] 2.6 target map with native fall completion " + report);
        else if (RebirthLogSettings.RuntimeInstallLoggingEnabled)
            Log.Out("[REBIRTH Auto Replant Trees] 2.6 target map with native fall completion " + report);
    }
}

/// <summary>
/// Exact 2.6 sapling-drop suppression rule.
/// </summary>
[HarmonyPatch(typeof(GameUtils), "collectHarvestedItem")]
internal static class RebirthAutoReplantTreeHarvestPatch
{
    private static bool Prefix(ItemValue _iv)
    {
        return !RebirthAutoReplantTreeRegistry.ShouldSuppressSapling(_iv);
    }
}

/// <summary>
/// Normal completion path: called after the tree has fallen, settled, and faded.
/// Prefix placement is safe because vanilla DestroyTree only removes the original
/// tree type; the original block position is already air after CreateMesh.
/// </summary>
[HarmonyPatch(typeof(EntityFallingTree), nameof(EntityFallingTree.DestroyTree))]
internal static class RebirthAutoReplantTreeFallingCompletionPatch
{
    private static void Prefix(EntityFallingTree __instance)
    {
        RebirthAutoReplantTreeRegistry.TryReplantAfterFall(__instance, "DestroyTree");
    }
}

/// <summary>
/// Unload fallback so leaving the chunk during a fall cannot permanently lose the
/// replacement. This must be a Postfix because vanilla MarkToUnload clears the tree
/// block position unconditionally.
/// </summary>
[HarmonyPatch(typeof(EntityFallingTree), nameof(EntityFallingTree.MarkToUnload))]
internal static class RebirthAutoReplantTreeFallingUnloadPatch
{
    private static void Postfix(EntityFallingTree __instance)
    {
        RebirthAutoReplantTreeRegistry.TryReplantAfterFall(__instance, "MarkToUnload");
    }
}

