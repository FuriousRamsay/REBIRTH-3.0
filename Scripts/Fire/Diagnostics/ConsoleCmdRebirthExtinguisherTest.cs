#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthExtinguisherTest : ConsoleCmdAbstract
{
    private static RebirthExtinguisherTestRunner runner;

    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbexttest" };
    }

    public override string getDescription()
    {
        return "Traces the dedicated REBIRTH extinguisher ranged action for one timed test.";
    }

    public override string getHelp()
    {
        return "Usage: rbexttest [seconds]\n"
             + "Example: rbexttest 20\n"
             + "Equip the fire extinguisher, aim at one block, run the command, then spray that block repeatedly.\n"
             + "Toolbelt notifications announce start, halfway, and completion.";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo sender)
    {
        int seconds = 20;
        if (parameters != null && parameters.Count > 0)
        {
            int parsed;
            if (int.TryParse(parameters[0], out parsed))
                seconds = Mathf.Clamp(parsed, 5, 120);
        }

        if (runner != null)
        {
            runner.Abort();
            UnityEngine.Object.Destroy(runner.gameObject);
            runner = null;
        }

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null)
        {
            Log.Warning("[RBExtTest] Cannot start: local world/player unavailable.");
            RebirthFireTestRunner.NotifyToolbelt("Extinguisher diagnostic could not start: local world/player unavailable.");
            return;
        }

        Vector3i target;
        if (!TryResolveLookTarget(world, player, out target))
            target = World.worldToBlockPos(player.position) + Vector3i.down;

        GameObject go = new GameObject("RebirthExtinguisherTestRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<RebirthExtinguisherTestRunner>();
        runner.Configure(seconds, target, delegate(RebirthExtinguisherTestRunner finished)
        {
            if (runner == finished)
                runner = null;
        });
    }

    private static bool TryResolveLookTarget(World world, EntityPlayerLocal player, out Vector3i target)
    {
        target = Vector3i.zero;
        try
        {
            Ray ray = player.GetLookRay();
            ray.origin += ray.direction.normalized * 0.5f;
            if (!Voxel.Raycast(world, ray, Constants.cDigAndBuildDistance, -538480645, 4095, 0f))
                return false;
            if (!Voxel.voxelRayHitInfo.bHitValid)
                return false;
            target = Voxel.voxelRayHitInfo.hit.blockPos;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[RBExtTest] look ray failed: " + ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }
}

public sealed class RebirthExtinguisherTestRunner : MonoBehaviour
{
    private int durationSeconds;
    private Vector3i target;
    private Action<RebirthExtinguisherTestRunner> finished;
    private bool aborted;
    private int startDamage;
    private int startType;
    private string startBlockName;
    private string heldItemAtStart;

    public void Configure(int seconds, Vector3i targetPosition, Action<RebirthExtinguisherTestRunner> callback)
    {
        durationSeconds = seconds;
        target = targetPosition;
        finished = callback;
        StartCoroutine(Run());
    }

    public void Abort()
    {
        aborted = true;
        RebirthExtinguisherHitTrace.End();
    }

    private IEnumerator Run()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (world == null || player == null)
        {
            Finish("Extinguisher diagnostic stopped: world/player unavailable.");
            yield break;
        }

        BlockValue startBlock = world.GetBlock(target);
        startDamage = startBlock.damage;
        startType = startBlock.type;
        startBlockName = SafeBlockName(startBlock);
        heldItemAtStart = SafeHeldItemName(player);

        RebirthExtinguisherHitTrace.Begin();

        Log.Out("[RBExtTest] === BEGIN durationSeconds=" + durationSeconds + " ===");
        Log.Out("[RBExtTest] setup target=" + target
            + " block=" + startBlockName
            + " type=" + startType
            + " damage=" + startDamage
            + " heldItem=" + heldItemAtStart
            + " patchInstalled=" + RebirthFirePatchInstaller.IsInstalled
            + " patchMethods=" + RebirthFirePatchInstaller.PatchedMethods
            + " patchFailures=" + RebirthFirePatchInstaller.FailedMethods
            + " dedicatedAction=" + IsDedicatedAction(player));

        RebirthFireTestRunner.NotifyToolbelt(
            "Extinguisher diagnostic started for " + durationSeconds
            + "s. Spray the aimed block repeatedly.");

        float start = Time.realtimeSinceStartup;
        bool halfwayShown = false;
        while (!aborted && Time.realtimeSinceStartup - start < durationSeconds)
        {
            float elapsed = Time.realtimeSinceStartup - start;
            if (!halfwayShown && elapsed >= durationSeconds * 0.5f)
            {
                halfwayShown = true;
                RebirthFireTestRunner.NotifyToolbelt(
                    "Extinguisher diagnostic halfway: hitCalls=" + RebirthExtinguisherHitTrace.HitCalls
                    + " suppressed=" + RebirthExtinguisherHitTrace.SuppressionMatched);
            }
            yield return null;
        }

        RebirthExtinguisherHitTrace.End();
        if (aborted)
            yield break;

        BlockValue endBlock = world.GetBlock(target);
        int damageDelta = endBlock.type == startType ? endBlock.damage - startDamage : int.MinValue;
        string conclusion = BuildConclusion(damageDelta);

        Log.Out("[RBExtTest] result hitCalls=" + RebirthExtinguisherHitTrace.HitCalls
            + " blockTerrainCalls=" + RebirthExtinguisherHitTrace.BlockTerrainCalls
            + " suppressionMatched=" + RebirthExtinguisherHitTrace.SuppressionMatched
            + " suppressionMissed=" + RebirthExtinguisherHitTrace.SuppressionMissed
            + " damagingAmmoMatches=" + RebirthExtinguisherHitTrace.DamagingAmmoMatches
            + " magazineAmmoMatches=" + RebirthExtinguisherHitTrace.MagazineAmmoMatches
            + " targetStartType=" + startType
            + " targetEndType=" + endBlock.type
            + " targetStartDamage=" + startDamage
            + " targetEndDamage=" + endBlock.damage
            + " targetDamageDelta=" + (damageDelta == int.MinValue ? "typeChanged" : damageDelta.ToString()));

        Log.Out("[RBExtTest] last hitTag=" + RebirthExtinguisherHitTrace.LastHitTag
            + " attackerEntityId=" + RebirthExtinguisherHitTrace.LastAttackerEntityId
            + " heldItem=" + RebirthExtinguisherHitTrace.LastHeldItem
            + " heldAction=" + RebirthExtinguisherHitTrace.LastHeldAction
            + " selectedAmmoIndex=" + RebirthExtinguisherHitTrace.LastSelectedAmmoIndex
            + " magazineNames=" + RebirthExtinguisherHitTrace.LastMagazineNames
            + " damagingItem=" + RebirthExtinguisherHitTrace.LastDamagingItem
            + " resolvedAmmo=" + RebirthExtinguisherHitTrace.LastResolvedAmmo
            + " resolutionSource=" + RebirthExtinguisherHitTrace.LastResolutionSource
            + " suppressPropertyPresent=" + RebirthExtinguisherHitTrace.LastPropertyPresent
            + " suppressPropertyValue=" + RebirthExtinguisherHitTrace.LastPropertyValue
            + " incomingBlockDamage=" + RebirthExtinguisherHitTrace.LastIncomingBlockDamage
            + " outgoingBlockDamage=" + RebirthExtinguisherHitTrace.LastOutgoingBlockDamage
            + " incomingFlags=" + RebirthExtinguisherHitTrace.LastIncomingFlags
            + " outgoingFlags=" + RebirthExtinguisherHitTrace.LastOutgoingFlags
            + " outgoingFlag8=" + ((RebirthExtinguisherHitTrace.LastOutgoingFlags & 8) != 0)
            + " failureReason=" + RebirthExtinguisherHitTrace.LastFailureReason);

        Log.Out("[RBExtTest] conclusion=" + conclusion);
        Log.Out("[RBExtTest] === END ===");

        Finish("Extinguisher diagnostic complete. " + conclusion + " Send the [RBExtTest] log section.");
    }

    private string BuildConclusion(int damageDelta)
    {
        if (RebirthExtinguisherHitTrace.HitCalls == 0)
            return "Dedicated extinguisher action did not record a ray result; verify the item loaded the custom action class.";
        if (RebirthExtinguisherHitTrace.BlockTerrainCalls == 0)
            return "Dedicated extinguisher action ran, but no block/terrain ray hit was recorded.";
        if (damageDelta != int.MinValue && damageDelta > 0)
            return "Dedicated action bypassed ItemActionAttack.Hit, but the target still took damage through another system.";
        return "Dedicated extinguisher action is active: ray-hit event fired with no native damage/impact path.";
    }

    private void Finish(string message)
    {
        RebirthFireTestRunner.NotifyToolbelt(message);
        if (finished != null)
            finished(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    private static bool IsDedicatedAction(EntityPlayerLocal player)
    {
        try
        {
            if (player == null || player.inventory == null || player.inventory.holdingItem == null || player.inventory.holdingItem.Actions == null)
                return false;
            return player.inventory.holdingItem.Actions.Length > 0
                && player.inventory.holdingItem.Actions[0] is ItemActionRangedRebirthExtinguisher;
        }
        catch
        {
            return false;
        }
    }

    private static string SafeHeldItemName(EntityPlayerLocal player)
    {
        try
        {
            ItemValue value = player.inventory.holdingItemItemValue;
            return value != null && value.ItemClass != null ? value.ItemClass.GetItemName() : "<none>";
        }
        catch { return "<error>"; }
    }

    private static string SafeBlockName(BlockValue value)
    {
        try { return value.Block != null ? value.Block.GetBlockName() : "<null>"; }
        catch { return "<error>"; }
    }
}
#endif
