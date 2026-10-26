using System;
using UnityEngine;
using UnityEngine.Scripting;

// Short-lived client interaction. The server still validates every submitted coordinate.
[Preserve]
public sealed class RebirthCompanionWorldSelection : MonoBehaviour
{
    private EntityPlayerLocal player;
    private World world;
    private Action<Vector3i?> completed;
    private LineRenderer marker;
    private Material material;
    private float promptAt;
    private bool finished;
    private Vector3i? anchor;
    // With an anchor (the other saved corner) the marker shows the whole inclusive block envelope between it and the aimed block.
    public static void Begin(EntityPlayerLocal actor, Action<Vector3i?> callback, Vector3i? previewAnchor = null)
    {
        var selection = new GameObject("REBIRTH Companion target selection").AddComponent<RebirthCompanionWorldSelection>();
        selection.player = actor; selection.world = actor.world; selection.completed = callback; selection.anchor = previewAnchor;
        selection.marker = selection.gameObject.AddComponent<LineRenderer>();
        selection.marker.useWorldSpace = true; selection.marker.positionCount = 16;
        selection.marker.startWidth = selection.marker.endWidth = .035f;
        selection.marker.startColor = selection.marker.endColor = new Color(.7f,.25f,1f,.95f);
        var shader = Shader.Find("Sprites/Default");
        if(shader != null) { selection.material = new Material(shader); selection.marker.material = selection.material; }
    }
    private void Update()
    {
        if(finished) return;
        if(player == null || player.IsDead() || !ReferenceEquals(world, GameManager.Instance?.World)) { Finish(null, false); return; }
        if(Input.GetKeyDown(KeyCode.Escape)) { Finish(null, true); return; }
        if(Time.unscaledTime >= promptAt)
        {
            promptAt = Time.unscaledTime + 3f;
            GameManager.ShowTooltip(player, RebirthCompanionDeskStore.L("SelectWorldBlock"), true, false, 3f);
        }
        Ray ray = player.GetLookRay(); ray.origin += ray.direction.normalized * .5f;
        bool hit = Voxel.Raycast(world, ray, Constants.cDigAndBuildDistance, -538480645, 4095, 0f) && Voxel.voxelRayHitInfo.bHitValid;
        marker.enabled = hit;
        if(!hit) return;
        Vector3i block = Voxel.voxelRayHitInfo.hit.blockPos;
        // Voxel block coordinates are absolute; LineRenderer positions use
        // Unity's shifted transform space.
        Vector3 low = block.ToVector3(), high = low + Vector3.one;
        if(anchor.HasValue)
        {
            Vector3 other = anchor.Value.ToVector3();
            low = Vector3.Min(low, other); high = Vector3.Max(high, other + Vector3.one);
        }
        Vector3 a = low - Origin.position, b = high - Origin.position;
        marker.SetPositions(new[] { a,new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z),new Vector3(a.x,a.y,b.z),a,
            new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),b,
            new Vector3(b.x,a.y,b.z),b,new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z) });
        if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Finish(block, true);
    }
    private void Finish(Vector3i? position, bool restore)
    {
        finished = true;
        var callback = completed; completed = null;
        Destroy(gameObject);
        if(restore) callback?.Invoke(position);
    }
    private void OnDestroy() { if(material != null) Destroy(material); }
}
