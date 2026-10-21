using System;
using System.Text;

/// <summary>Explicit read-only visual inspection behind rbnpcadmin permissions.
/// Never runs automatically or requests a mesh rebuild.</summary>
internal static class RebirthSdcsVisualDiagnostics
{
    internal static bool Inspect(string entityClassName, out string detail)
    {
        var world = GameManager.Instance?.World;
        var report = new StringBuilder();
        int count = 0;
        bool allReady = world != null;
        foreach (var state in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out int entityId)) continue;
            var npc = world?.GetEntity(entityId) as EntityRebirthHumanoidNPC;
            if (npc == null || npc.IsDead() || !string.Equals(
                EntityClass.list[npc.entityClass].entityClassName, entityClassName, StringComparison.OrdinalIgnoreCase)) continue;
            count++;
            var model = npc.emodel as EModelRebirthSdcsNpc;
            var animator = model != null ? model.avatarController?.GetAnimator() : null;
            bool visualReady = GameManager.IsDedicatedServer || (model != null && model.baseRig != null &&
                animator != null && animator.enabled && animator.avatar != null && animator.avatar.isValid && animator.layerCount > 0);
            // Native PhysicsInit reparents this outside RootTransform. Inspect
            // its resolved field rather than searching the pre-Init hierarchy.
            bool blocker = npc.physicsRBT != null && npc.physicsRBT.tag == "LargeEntityBlocker";
            int physicsLayer = npc.PhysicsTransform != null ? npc.PhysicsTransform.gameObject.layer : -1;
            bool ready = model != null && model.RebirthAppearanceApplied && visualReady && blocker && physicsLayer == 15;
            allReady &= ready;
            report.Append(" entity=").Append(entityId).Append(" ready=").Append(ready)
                .Append(" appearance=").Append(model != null && model.RebirthAppearanceApplied)
                .Append(" builds=").Append(model?.RebirthMeshBuildCount ?? 0)
                .Append(" blocker=").Append(blocker).Append(" physicsLayer=").Append(physicsLayer)
                .Append(" animatorLayers=").Append(animator != null ? animator.layerCount : 0);
        }
        // Require an unambiguous actor for a repeatable ready assertion.
        bool success = allReady && count == 1;
        detail = "[REBIRTH NPC SDCS] " + (success ? "PASS" : "FAIL") + " class=" + entityClassName +
            " count=" + count + " dedicated=" + GameManager.IsDedicatedServer + report;
        return success;
    }
}
