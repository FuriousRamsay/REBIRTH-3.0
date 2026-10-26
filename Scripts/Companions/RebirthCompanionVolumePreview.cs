using System;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

// Client-only, read-only outline of one SAVED Desk volume (Farm/Structure/Mining/Logging). It shows the exact block envelope the owner
// saved. A camper-bound volume is mapped through the camper's live transform with the matrix stored at definition time, so the outline
// follows the camper. Nothing is sent or saved; the server still validates every real use of the volume.
[Preserve]
public sealed class RebirthCompanionVolumePreview : MonoBehaviour
{
    private static readonly int[] Path = { 0, 1, 3, 2, 0, 4, 5, 1, 5, 7, 3, 7, 6, 2, 6, 4 };
    private EntityPlayerLocal player;
    private World world;
    private Action finished;
    private LineRenderer line;
    private Material material;
    private XElement row;
    private Guid camper;
    private float endAt;

    // Returns false (nothing started) when the row is malformed. `done` runs once when the preview ends (timeout, Escape/Return, leaving the world).
    public static bool Begin(EntityPlayerLocal actor, XElement targetRow, Action done)
    {
        if (actor == null || targetRow == null || !TryBox(targetRow, out _, out _, out Guid camperId)) return false;
        var preview = new GameObject("REBIRTH Companion volume preview").AddComponent<RebirthCompanionVolumePreview>();
        preview.player = actor; preview.world = actor.world; preview.row = targetRow; preview.camper = camperId;
        preview.finished = done; preview.endAt = Time.unscaledTime + 20f;
        preview.line = preview.gameObject.AddComponent<LineRenderer>();
        preview.line.useWorldSpace = true; preview.line.positionCount = Path.Length;
        preview.line.startWidth = preview.line.endWidth = .035f;
        preview.line.startColor = preview.line.endColor = new Color(.35f, .9f, .45f, .95f);
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) { preview.material = new Material(shader); preview.line.material = preview.material; }
        return true;
    }

    private static bool Int(XElement x, string name, out int value) =>
        int.TryParse((string)x.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= -100000 && value <= 100000;

    private static bool TryBox(XElement x, out Vector3 low, out Vector3 high, out Guid camperId)
    {
        low = high = default(Vector3); camperId = Guid.Empty;
        int[] c = new int[6]; string[] f = { "ax", "ay", "az", "bx", "by", "bz" };
        for (int i = 0; i < 6; i++) if (!Int(x, f[i], out c[i])) return false;
        low = Vector3.Min(new Vector3(c[0], c[1], c[2]), new Vector3(c[3], c[4], c[5]));
        high = Vector3.Max(new Vector3(c[0], c[1], c[2]), new Vector3(c[3], c[4], c[5])) + Vector3.one;
        string text = (string)x.Attribute("camper");
        return string.IsNullOrEmpty(text) || (Guid.TryParseExact(text, "N", out camperId) && camperId != Guid.Empty);
    }

    private EntityVehicle FindCamper()
    {
        foreach (var entity in world.Entities.list)
        {
            if (entity is EntityVehicle v && RebirthCamperRadial.IsCamper(v) && !v.IsDead() && v.RootTransform != null &&
                v.vehicle?.GetUpdatedItemValue() != null &&
                v.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity", out string raw) &&
                Guid.TryParse(raw, out var found) && found == camper) return v;
        }
        return null;
    }

    // Original block corners -> current world positions (identity for a fixed volume).
    private bool TryCorners(Vector3[] corners)
    {
        TryBox(row, out Vector3 low, out Vector3 high, out _);
        for (int i = 0; i < 8; i++)
            corners[i] = new Vector3((i & 1) == 0 ? low.x : high.x, (i & 2) == 0 ? low.y : high.y, (i & 4) == 0 ? low.z : high.z);
        if (camper == Guid.Empty) return true;
        var vehicle = FindCamper();
        if (vehicle == null) return false;
        var m = new Matrix4x4();
        for (int r = 0; r < 3; r++)
            for (int col = 0; col < 4; col++)
                if (!float.TryParse((string)row.Attribute("m" + r + col), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ||
                    float.IsNaN(v) || float.IsInfinity(v)) return false;
                else m[r, col] = v;
        m[3, 3] = 1f;
        // m maps camper-local (now) -> original absolute world; invert it to map an original point back to camper-local.
        var inverse = m.inverse;
        for (int i = 0; i < 8; i++)
            corners[i] = vehicle.RootTransform.TransformPoint(inverse.MultiplyPoint3x4(corners[i])) + Origin.position;
        return true;
    }

    private void Update()
    {
        if (player == null || player.IsDead() || !ReferenceEquals(world, GameManager.Instance?.World) || Time.unscaledTime >= endAt ||
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { End(); return; }
        var corners = new Vector3[8];
        line.enabled = TryCorners(corners);
        if (!line.enabled) return;
        for (int i = 0; i < Path.Length; i++) line.SetPosition(i, corners[Path[i]] - Origin.position);
    }

    private void End()
    {
        var callback = finished; finished = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}
