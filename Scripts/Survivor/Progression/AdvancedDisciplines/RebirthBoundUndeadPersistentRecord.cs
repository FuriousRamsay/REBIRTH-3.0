using System;
using System.Globalization;
using System.Xml;
using UnityEngine;

#nullable disable

/// <summary>
/// PC009 durable Black Magic state stored inside the canonical NPC aggregate.
/// Conditioning may exist before binding; only IsBound records consume Bound Undead Capacity.
/// </summary>
public sealed class RebirthBoundUndeadPersistentRecord
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion = CurrentSchemaVersion;
    public string SourceEntityClass = string.Empty;
    public string TierId = "normal";
    public int CapacityCost = 1;
    public float ConditioningProgress;
    public string ConditioningStage = "Resistance Broken";
    public bool IsBound;
    public bool IsSummoned;
    public string Lifecycle = "Conditioning"; // Conditioning / Active / AwaitingReturn
    public int LastEntityId = -1;
    public int KillCount;
    public float Training;
    public float BindingStability = 100f;
    public long BoundUtcTicks;
    public long LastConditioningUtcTicks;
    public byte BoundFactionId;
    public byte BoundFactionRank;
    public bool AttackStopped;
    public uint Revision;

    public static XmlElement WriteXml(XmlDocument document, RebirthBoundUndeadPersistentRecord value)
    {
        value = value ?? new RebirthBoundUndeadPersistentRecord();
        XmlElement e = document.CreateElement("boundUndead");
        e.SetAttribute("schema", value.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("sourceEntityClass", value.SourceEntityClass ?? string.Empty);
        e.SetAttribute("tier", value.TierId ?? "normal");
        e.SetAttribute("capacity", Math.Max(0, value.CapacityCost).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("conditioning", Mathf.Clamp(value.ConditioningProgress, 0f, 100f).ToString("R", CultureInfo.InvariantCulture));
        e.SetAttribute("conditioningStage", value.ConditioningStage ?? string.Empty);
        e.SetAttribute("bound", value.IsBound ? "1" : "0");
        e.SetAttribute("summoned", value.IsSummoned ? "1" : "0");
        e.SetAttribute("lifecycle", value.Lifecycle ?? string.Empty);
        e.SetAttribute("lastEntityId", value.LastEntityId.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("kills", Math.Max(0, value.KillCount).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("training", Mathf.Clamp(value.Training, 0f, 100f).ToString("R", CultureInfo.InvariantCulture));
        e.SetAttribute("bindingStability", Mathf.Clamp(value.BindingStability, 0f, 100f).ToString("R", CultureInfo.InvariantCulture));
        e.SetAttribute("boundUtcTicks", Math.Max(0L, value.BoundUtcTicks).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("lastConditioningUtcTicks", Math.Max(0L, value.LastConditioningUtcTicks).ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("boundFactionId", value.BoundFactionId.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("boundFactionRank", value.BoundFactionRank.ToString(CultureInfo.InvariantCulture));
        e.SetAttribute("attackStopped", value.AttackStopped ? "1" : "0");
        e.SetAttribute("revision", value.Revision.ToString(CultureInfo.InvariantCulture));
        return e;
    }

    public static RebirthBoundUndeadPersistentRecord ReadXml(XmlElement e)
    {
        RebirthBoundUndeadPersistentRecord r = new RebirthBoundUndeadPersistentRecord();
        if (e == null) return r;
        int schema;
        if (!int.TryParse(e.GetAttribute("schema"), NumberStyles.Integer, CultureInfo.InvariantCulture, out schema) || schema < 1) schema = CurrentSchemaVersion;
        if (schema > CurrentSchemaVersion) throw new InvalidOperationException("Unsupported bound-undead schema " + schema.ToString(CultureInfo.InvariantCulture));
        r.SchemaVersion = CurrentSchemaVersion;
        r.SourceEntityClass = e.GetAttribute("sourceEntityClass") ?? string.Empty;
        r.TierId = string.IsNullOrEmpty(e.GetAttribute("tier")) ? "normal" : e.GetAttribute("tier");
        int i; long l; uint u; float f; byte b;
        if (int.TryParse(e.GetAttribute("capacity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) r.CapacityCost = Math.Max(0, i);
        if (float.TryParse(e.GetAttribute("conditioning"), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) r.ConditioningProgress = Mathf.Clamp(f, 0f, 100f);
        r.ConditioningStage = e.GetAttribute("conditioningStage") ?? string.Empty;
        r.IsBound = e.GetAttribute("bound") == "1";
        r.IsSummoned = e.GetAttribute("summoned") == "1";
        string lifecycle = e.GetAttribute("lifecycle");
        r.Lifecycle = string.IsNullOrWhiteSpace(lifecycle) ? (r.IsBound ? "Active" : "Conditioning") : lifecycle;
        if (int.TryParse(e.GetAttribute("lastEntityId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) r.LastEntityId = i;
        if (int.TryParse(e.GetAttribute("kills"), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) r.KillCount = Math.Max(0, i);
        if (float.TryParse(e.GetAttribute("training"), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) r.Training = Mathf.Clamp(f, 0f, 100f);
        if (float.TryParse(e.GetAttribute("bindingStability"), NumberStyles.Float, CultureInfo.InvariantCulture, out f) && !float.IsNaN(f) && !float.IsInfinity(f)) r.BindingStability = Mathf.Clamp(f, 0f, 100f);
        if (long.TryParse(e.GetAttribute("boundUtcTicks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) r.BoundUtcTicks = Math.Max(0L, l);
        if (long.TryParse(e.GetAttribute("lastConditioningUtcTicks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) r.LastConditioningUtcTicks = Math.Max(0L, l);
        if (byte.TryParse(e.GetAttribute("boundFactionId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) r.BoundFactionId = b;
        if (byte.TryParse(e.GetAttribute("boundFactionRank"), NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) r.BoundFactionRank = b;
        r.AttackStopped = e.GetAttribute("attackStopped") == "1";
        if (uint.TryParse(e.GetAttribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out u)) r.Revision = u;
        if (r.IsBound && r.CapacityCost < 1) r.CapacityCost = 1;
        if (!string.Equals(r.Lifecycle,"Conditioning",StringComparison.OrdinalIgnoreCase) && !string.Equals(r.Lifecycle,"Active",StringComparison.OrdinalIgnoreCase) && !string.Equals(r.Lifecycle,"AwaitingReturn",StringComparison.OrdinalIgnoreCase)) r.Lifecycle = r.IsBound ? "Active" : "Conditioning";
        r.ConditioningStage = StageFor(r.ConditioningProgress);
        return r;
    }

    public static string StageFor(float progress)
    {
        progress = Mathf.Clamp(progress, 0f, 100f);
        if (progress >= 100f) return "Ready for Binding";
        if (progress >= 80f) return "Subjugated";
        if (progress >= 60f) return "Conditioned";
        if (progress >= 40f) return "Receptive";
        return "Resistance Broken";
    }
}
