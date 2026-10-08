using System;
using System.Collections.Generic;

#nullable disable

public enum RebirthHudTrackType
{
    Skill = 0,
    Attribute = 1,
    Knowledge = 2,
    Summary = 3
}

/// <summary>
/// Stable typed identity for one HUD-trackable progression item. The type and stable ID are
/// presentation identifiers only; they never contain authoritative progression values.
/// </summary>
public sealed class RebirthHudTrackingId : IEquatable<RebirthHudTrackingId>
{
    public RebirthHudTrackType Type { get; private set; }
    public string StableId { get; private set; }

    public RebirthHudTrackingId(RebirthHudTrackType type, string stableId)
    {
        Type = type;
        StableId = NormalizeStableId(stableId);
    }

    public string Key
    {
        get { return Type.ToString().ToLowerInvariant() + ":" + StableId.ToLowerInvariant(); }
    }

    public bool Equals(RebirthHudTrackingId other)
    {
        return other != null && Type == other.Type &&
            string.Equals(StableId, other.StableId, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object obj) { return Equals(obj as RebirthHudTrackingId); }
    public override int GetHashCode()
    {
        unchecked { return ((int)Type * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(StableId ?? string.Empty); }
    }
    public override string ToString() { return Key; }

    public static string NormalizeStableId(string stableId)
    {
        return (stableId ?? string.Empty).Trim();
    }

    public static string ToTypeToken(RebirthHudTrackType type)
    {
        switch (type)
        {
            case RebirthHudTrackType.Skill: return "skill";
            case RebirthHudTrackType.Attribute: return "attribute";
            case RebirthHudTrackType.Knowledge: return "knowledge";
            case RebirthHudTrackType.Summary: return "summary";
            default: return type.ToString().ToLowerInvariant();
        }
    }

    public static bool TryParseTypeToken(string token, out RebirthHudTrackType type)
    {
        string value = (token ?? string.Empty).Trim();
        if (string.Equals(value, "skill", StringComparison.OrdinalIgnoreCase)) { type = RebirthHudTrackType.Skill; return true; }
        if (string.Equals(value, "attribute", StringComparison.OrdinalIgnoreCase)) { type = RebirthHudTrackType.Attribute; return true; }
        if (string.Equals(value, "knowledge", StringComparison.OrdinalIgnoreCase)) { type = RebirthHudTrackType.Knowledge; return true; }
        if (string.Equals(value, "summary", StringComparison.OrdinalIgnoreCase)) { type = RebirthHudTrackType.Summary; return true; }
        type = RebirthHudTrackType.Skill;
        return false;
    }
}

public sealed class RebirthHudTrackingEntryPreference
{
    public RebirthHudTrackType Type;
    public string StableId = string.Empty;
    public int Order;
    public bool Enabled = true;

    public RebirthHudTrackingId TrackingId { get { return new RebirthHudTrackingId(Type, StableId); } }

    public RebirthHudTrackingEntryPreference Clone()
    {
        return new RebirthHudTrackingEntryPreference { Type = Type, StableId = StableId ?? string.Empty, Order = Order, Enabled = Enabled };
    }
}

/// <summary>
/// Forward-compatible persisted row whose type token is not understood by this build. It is
/// retained and reserialized but never rendered until a future provider understands it.
/// </summary>
public sealed class RebirthHudTrackingUnknownEntry
{
    public string TypeToken = string.Empty;
    public string StableId = string.Empty;
    public int Order;
    public bool Enabled = true;

    public RebirthHudTrackingUnknownEntry Clone()
    {
        return new RebirthHudTrackingUnknownEntry { TypeToken = TypeToken ?? string.Empty, StableId = StableId ?? string.Empty, Order = Order, Enabled = Enabled };
    }
}

public sealed class RebirthHudTrackingPreferences
{
    public int SchemaVersion = RebirthHudTrackingPreferenceStore.CurrentSchemaVersion;
    public bool Enabled = true;
    public bool ShowContextualActiveSkill;
    public readonly List<RebirthHudTrackingEntryPreference> Entries = new List<RebirthHudTrackingEntryPreference>();
    public readonly List<RebirthHudTrackingUnknownEntry> UnknownEntries = new List<RebirthHudTrackingUnknownEntry>();

    public RebirthHudTrackingPreferences Clone()
    {
        RebirthHudTrackingPreferences copy = new RebirthHudTrackingPreferences
        {
            SchemaVersion = SchemaVersion,
            Enabled = Enabled,
            ShowContextualActiveSkill = ShowContextualActiveSkill
        };
        for (int i = 0; i < Entries.Count; i++) if (Entries[i] != null) copy.Entries.Add(Entries[i].Clone());
        for (int i = 0; i < UnknownEntries.Count; i++) if (UnknownEntries[i] != null) copy.UnknownEntries.Add(UnknownEntries[i].Clone());
        return copy;
    }

    public bool Contains(RebirthHudTrackType type, string stableId)
    {
        string id = RebirthHudTrackingId.NormalizeStableId(stableId);
        for (int i = 0; i < Entries.Count; i++)
        {
            RebirthHudTrackingEntryPreference row = Entries[i];
            if (row != null && row.Type == type && string.Equals(row.StableId, id, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool Add(RebirthHudTrackType type, string stableId)
    {
        string id = RebirthHudTrackingId.NormalizeStableId(stableId);
        if (id.Length == 0 || Contains(type, id)) return false;
        int nextOrder = -1;
        for (int i = 0; i < Entries.Count; i++) if (Entries[i] != null) nextOrder = Math.Max(nextOrder, Entries[i].Order);
        for (int i = 0; i < UnknownEntries.Count; i++) if (UnknownEntries[i] != null) nextOrder = Math.Max(nextOrder, UnknownEntries[i].Order);
        Entries.Add(new RebirthHudTrackingEntryPreference { Type = type, StableId = id, Order = nextOrder + 1, Enabled = true });
        NormalizeOrder();
        return true;
    }

    public bool Remove(RebirthHudTrackType type, string stableId)
    {
        string id = RebirthHudTrackingId.NormalizeStableId(stableId);
        for (int i = 0; i < Entries.Count; i++)
        {
            RebirthHudTrackingEntryPreference row = Entries[i];
            if (row != null && row.Type == type && string.Equals(row.StableId, id, StringComparison.OrdinalIgnoreCase))
            {
                Entries.RemoveAt(i);
                NormalizeOrder();
                return true;
            }
        }
        return false;
    }

    public bool Move(RebirthHudTrackType type, string stableId, int delta)
    {
        if (delta == 0) return false;
        NormalizeOrder();
        string id = RebirthHudTrackingId.NormalizeStableId(stableId);
        int index = -1;
        for (int i = 0; i < Entries.Count; i++)
        {
            RebirthHudTrackingEntryPreference row = Entries[i];
            if (row != null && row.Type == type && string.Equals(row.StableId, id, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
        }
        if (index < 0) return false;
        int target = Math.Max(0, Math.Min(Entries.Count - 1, index + delta));
        if (target == index) return false;
        int swapOrder = Entries[target].Order;
        Entries[target].Order = Entries[index].Order;
        Entries[index].Order = swapOrder;
        NormalizeOrder();
        return true;
    }

    public void ClearKnownEntries()
    {
        Entries.Clear();
    }

    public static int GridTarget(int index, int count, int dx, int dy)
    {
        if (index < 0 || index >= Math.Min(count, 15)) return -1;
        int x = index % 3 + dx, y = index / 3 + dy;
        int target = y * 3 + x;
        return x >= 0 && x < 3 && y >= 0 && y < 5 && target < Math.Min(count, 15) ? target : -1;
    }

    public bool MoveGrid(RebirthHudTrackType type, string id, int dx, int dy)
    {
        NormalizeOrder();
        int index = Entries.FindIndex(e => e != null && e.Type == type &&
            string.Equals(e.StableId, id, StringComparison.OrdinalIgnoreCase));
        int target = GridTarget(index, Entries.Count, dx, dy);
        if (target < 0 || target == index) return false;
        int swapOrder = Entries[target].Order;
        Entries[target].Order = Entries[index].Order;
        Entries[index].Order = swapOrder;
        NormalizeOrder();
        return true;
    }

    public void NormalizeOrder()
    {
        List<Tuple<int, string, object>> combined = new List<Tuple<int, string, object>>();
        for (int i = 0; i < Entries.Count; i++)
        {
            RebirthHudTrackingEntryPreference row = Entries[i];
            if (row == null) continue;
            combined.Add(Tuple.Create(row.Order, "0:" + row.TrackingId.Key, (object)row));
        }
        for (int i = 0; i < UnknownEntries.Count; i++)
        {
            RebirthHudTrackingUnknownEntry row = UnknownEntries[i];
            if (row == null) continue;
            combined.Add(Tuple.Create(row.Order, "1:" + (row.TypeToken ?? string.Empty).ToLowerInvariant() + ":" + (row.StableId ?? string.Empty).ToLowerInvariant(), (object)row));
        }
        combined.Sort(delegate(Tuple<int, string, object> a, Tuple<int, string, object> b)
        {
            int order = a.Item1.CompareTo(b.Item1);
            return order != 0 ? order : string.CompareOrdinal(a.Item2, b.Item2);
        });
        for (int i = 0; i < combined.Count; i++)
        {
            RebirthHudTrackingEntryPreference known = combined[i].Item3 as RebirthHudTrackingEntryPreference;
            if (known != null) known.Order = i;
            else ((RebirthHudTrackingUnknownEntry)combined[i].Item3).Order = i;
        }
        Entries.Sort(delegate(RebirthHudTrackingEntryPreference a, RebirthHudTrackingEntryPreference b)
        { return (a != null ? a.Order : int.MaxValue).CompareTo(b != null ? b.Order : int.MaxValue); });
        UnknownEntries.Sort(delegate(RebirthHudTrackingUnknownEntry a, RebirthHudTrackingUnknownEntry b)
        { return (a != null ? a.Order : int.MaxValue).CompareTo(b != null ? b.Order : int.MaxValue); });
    }
}

public sealed class RebirthHudTrackDisplay
{
    public RebirthHudTrackingId Id;
    public string Atlas = string.Empty;
    public string Icon = string.Empty;
    public string Name = string.Empty;
    public string ValueText = string.Empty;
    public string SecondaryText = string.Empty;
    public bool HasProgress;
    public float Progress01;
    public bool Complete;
    public long SourceRevision;
}
