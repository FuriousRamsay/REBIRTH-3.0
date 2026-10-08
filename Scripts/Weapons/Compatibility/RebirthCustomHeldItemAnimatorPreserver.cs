using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Preserves RuntimeAnimatorController instances embedded in selected custom held-item prefabs.
/// Vanilla SetInRightHand can replace those controllers while attaching the prefab, which leaves
/// the model visible but disables prefab-owned animation states and particle systems.
/// </summary>
public static class RebirthCustomHeldItemAnimatorPreserver
{
    public const string HarmonyId = "rebirth.custom-held-item-animator.3.1";
    public const string MarkerTagName = "preserveCustomAnimatorController";

    private static readonly FastTags<TagGroup.Global> MarkerTag =
        FastTags<TagGroup.Global>.GetTag(MarkerTagName);

    private static readonly Harmony Harmony = new Harmony(HarmonyId);
    private static readonly Dictionary<int, PendingRoot> PendingReapply =
        new Dictionary<int, PendingRoot>();
    private static readonly List<int> PendingRootScratch = new List<int>(8);

    private static bool installed;
    private static int patchedMethods;
    private static int failedMethods;

    public static bool IsInstalled { get { return installed; } }
    public static int PatchedMethods { get { return patchedMethods; } }
    public static int FailedMethods { get { return failedMethods; } }

    private sealed class CapturedAnimator
    {
        public Animator Animator;
        public int InstanceId;
        public RuntimeAnimatorController Controller;
    }

    private sealed class CaptureState
    {
        public int RootInstanceId;
        public List<CapturedAnimator> Animators = new List<CapturedAnimator>();
    }

    private sealed class PendingRoot
    {
        public Transform Root;
        public int RootInstanceId;
        public List<CapturedAnimator> Animators;
    }

    public static string Install()
    {
        if (installed)
            return "[REBIRTH HeldAnimator] already installed patches=" + patchedMethods + " failures=" + failedMethods;

        patchedMethods = 0;
        failedMethods = 0;

        Patch(
            AccessTools.Method(typeof(AvatarMultiBodyController), nameof(AvatarMultiBodyController.SetInRightHand), new[] { typeof(Transform) }),
            AccessTools.Method(typeof(RebirthCustomHeldItemAnimatorPreserver), nameof(MultiBodyPrefix)),
            AccessTools.Method(typeof(RebirthCustomHeldItemAnimatorPreserver), nameof(MultiBodyPostfix)));

        Patch(
            AccessTools.Method(typeof(LegacyAvatarController), nameof(LegacyAvatarController.SetInRightHand), new[] { typeof(Transform) }),
            AccessTools.Method(typeof(RebirthCustomHeldItemAnimatorPreserver), nameof(LegacyPrefix)),
            AccessTools.Method(typeof(RebirthCustomHeldItemAnimatorPreserver), nameof(LegacyPostfix)));

        Patch(
            AccessTools.Method(typeof(GameManager), nameof(GameManager.Update), Type.EmptyTypes),
            null,
            AccessTools.Method(typeof(RebirthCustomHeldItemAnimatorPreserver), nameof(GameManagerUpdatePostfix)));

        installed = failedMethods == 0 && patchedMethods == 3;
        if (!installed && patchedMethods > 0)
        {
            int partial = patchedMethods;
            Harmony.UnpatchSelf();
            patchedMethods = 0;
            Log.Error("[REBIRTH HeldAnimator] incomplete target set; rolled back " + partial + " owned patches.");
        }
        return "[REBIRTH HeldAnimator] installed patches=" + patchedMethods + " failures=" + failedMethods
            + " markerTag=" + MarkerTagName;
    }

    private static void Patch(System.Reflection.MethodInfo original, System.Reflection.MethodInfo prefix, System.Reflection.MethodInfo postfix)
    {
        if (original == null || (prefix == null && postfix == null))
        {
            failedMethods++;
            Log.Error("[REBIRTH HeldAnimator] patch target or patch method missing.");
            return;
        }

        try
        {
            Harmony.Patch(
                original,
                prefix == null ? null : new HarmonyMethod(prefix),
                postfix == null ? null : new HarmonyMethod(postfix));
            patchedMethods++;
        }
        catch (Exception ex)
        {
            failedMethods++;
            Log.Error("[REBIRTH HeldAnimator] patch failed target="
                + original.DeclaringType.FullName + "." + original.Name
                + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void MultiBodyPrefix(AvatarMultiBodyController __instance, Transform _transform, ref CaptureState __state)
    {
        EntityAlive entity = __instance == null ? null : __instance.entity;
        __state = HoldingMarkedItem(entity) ? Capture(_transform) : null;
    }

    private static void MultiBodyPostfix(AvatarMultiBodyController __instance, Transform _transform, CaptureState __state)
    {
        EntityAlive entity = __instance == null ? null : __instance.entity;
        RestoreAndQueue(entity, _transform, __state);
    }

    private static void LegacyPrefix(LegacyAvatarController __instance, Transform _transform, ref CaptureState __state)
    {
        EntityAlive entity = __instance == null ? null : __instance.Entity;
        __state = HoldingMarkedItem(entity) ? Capture(_transform) : null;
    }

    private static void LegacyPostfix(LegacyAvatarController __instance, Transform _transform, CaptureState __state)
    {
        EntityAlive entity = __instance == null ? null : __instance.Entity;
        RestoreAndQueue(entity, _transform, __state);
    }

    private static CaptureState Capture(Transform root)
    {
        CaptureState state = new CaptureState();
        if (root == null)
            return state;

        state.RootInstanceId = root.GetInstanceID();
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.runtimeAnimatorController == null)
                continue;

            state.Animators.Add(new CapturedAnimator
            {
                Animator = animator,
                InstanceId = animator.GetInstanceID(),
                Controller = animator.runtimeAnimatorController
            });
        }

        return state;
    }

    private static bool HoldingMarkedItem(EntityAlive entity)
    {
        ItemClass heldItem = entity == null || entity.inventory == null
            ? null
            : entity.inventory.holdingItem;

        return heldItem != null && heldItem.HasAnyTags(MarkerTag);
    }

    private static void RestoreAndQueue(EntityAlive entity, Transform root, CaptureState state)
    {
        if (!HoldingMarkedItem(entity) || root == null || state == null || state.Animators.Count == 0)
            return;

        Restore(root, state.Animators);
        PendingReapply[root.GetInstanceID()] = new PendingRoot
        {
            Root = root,
            RootInstanceId = root.GetInstanceID(),
            Animators = state.Animators
        };
    }

    private static void Restore(Transform root, List<CapturedAnimator> captured)
    {
        if (root == null || captured == null || captured.Count == 0) return;
        for (int i = 0; i < captured.Count; i++)
        {
            CapturedAnimator entry = captured[i];
            Animator animator = entry != null ? entry.Animator : null;
            if (animator == null || animator.transform == null || !BelongsToRoot(animator.transform, root.GetInstanceID())) continue;
            if (entry.Controller == null || animator.runtimeAnimatorController == entry.Controller) continue;
            animator.runtimeAnimatorController = entry.Controller;
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private static void GameManagerUpdatePostfix()
    {
        if (PendingReapply.Count == 0) return;

        PendingRootScratch.Clear();
        foreach (int rootId in PendingReapply.Keys) PendingRootScratch.Add(rootId);
        for (int r = 0; r < PendingRootScratch.Count; r++)
        {
            int rootId = PendingRootScratch[r];
            PendingRoot pending;
            if (!PendingReapply.TryGetValue(rootId, out pending)) continue;
            if (pending == null || pending.Root == null || pending.Root.GetInstanceID() != rootId)
            {
                PendingReapply.Remove(rootId);
                continue;
            }
            Restore(pending.Root, pending.Animators);
            PendingReapply.Remove(rootId);
        }
        PendingRootScratch.Clear();
    }

    private static bool BelongsToRoot(Transform transform, int rootInstanceId)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.GetInstanceID() == rootInstanceId)
                return true;
            current = current.parent;
        }
        return false;
    }
}
