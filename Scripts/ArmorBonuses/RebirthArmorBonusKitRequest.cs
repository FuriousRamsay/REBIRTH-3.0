using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#nullable disable

/// <summary>What to do to each existing bonus (spec section 6). Retuning uses Keep / Reroll / Replace; Breakdown uses Keep / Remove.</summary>
public enum RebirthArmorBonusRowAction : byte { Keep = 0, Reroll = 1, Replace = 2, Remove = 3 }

/// <summary>
/// The player's choices at the Modifier Station (spec sections 6 and 8): one focus per new-bonus row, and one action
/// (plus a focus for Replace) per existing bonus. The client sends it; the server checks every part again and rolls.
/// An empty request means "no focus anywhere", which gives the same result as before the focus system,
/// except that Breakdown then removes everything and Retuning replaces every bonus.
/// </summary>
public sealed class RebirthArmorBonusKitRequest
{
    public const byte Version = 1;
    private const int MaxRows = 4, MaxText = 160;

    /// <summary>The kit item the client saw (empty = not checked).</summary>
    public string Kit = string.Empty;
    /// <summary>The bonus ids the client saw on the item, comma separated (null = not checked). Stops a request made for an older state.</summary>
    public string ItemBonuses;
    public readonly List<RebirthArmorBonusFocus> Adds = new List<RebirthArmorBonusFocus>();
    public readonly List<RebirthArmorBonusRowAction> Rows = new List<RebirthArmorBonusRowAction>();
    public readonly List<RebirthArmorBonusFocus> RowFocus = new List<RebirthArmorBonusFocus>();

    public static string BonusSignature(ItemValue item) => string.Join(",", RebirthArmorBonusStorage.Read(item).Select(b => b.Definition.Id));

    /// <summary>The bonuses as stored on the item (id:value;...), for the before/after result card.</summary>
    public static string BonusText(ItemValue item) =>
        item != null && item.Metadata != null && item.TryGetMetadata(RebirthArmorBonusStorage.MetadataKey, out string raw) ? raw ?? string.Empty : string.Empty;

    public RebirthArmorBonusKitRequest Add(RebirthArmorBonusFocus focus) { Adds.Add(focus); return this; }
    public RebirthArmorBonusKitRequest Row(RebirthArmorBonusRowAction action, RebirthArmorBonusFocus focus = default(RebirthArmorBonusFocus)) { Rows.Add(action); RowFocus.Add(focus); return this; }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Version);
        writer.Write(Kit ?? string.Empty);
        writer.Write(ItemBonuses != null);
        if (ItemBonuses != null) writer.Write(ItemBonuses);
        writer.Write((byte)Math.Min(MaxRows, Adds.Count));
        for (int i = 0; i < Math.Min(MaxRows, Adds.Count); i++) writer.Write(Adds[i].ToString());
        writer.Write((byte)Math.Min(MaxRows, Rows.Count));
        for (int i = 0; i < Math.Min(MaxRows, Rows.Count); i++) { writer.Write((byte)Rows[i]); writer.Write(RowFocus[i].ToString()); }
    }

    /// <summary>Reads a request; returns null (refused) when it is malformed, too large or names unknown focuses.</summary>
    public static RebirthArmorBonusKitRequest Read(BinaryReader reader)
    {
        if (reader.ReadByte() != Version) return null;
        var request = new RebirthArmorBonusKitRequest { Kit = Bounded(reader.ReadString()) };
        if (reader.ReadBoolean()) request.ItemBonuses = Bounded(reader.ReadString(), 2048);
        int adds = reader.ReadByte();
        if (adds > MaxRows) return null;
        for (int i = 0; i < adds; i++)
        {
            if (!RebirthArmorBonusFocus.TryParse(Bounded(reader.ReadString()), out var focus)) return null;
            request.Adds.Add(focus);
        }
        int rows = reader.ReadByte();
        if (rows > MaxRows) return null;
        for (int i = 0; i < rows; i++)
        {
            byte action = reader.ReadByte();
            if (action > (byte)RebirthArmorBonusRowAction.Remove) return null;
            if (!RebirthArmorBonusFocus.TryParse(Bounded(reader.ReadString()), out var focus)) return null;
            request.Rows.Add((RebirthArmorBonusRowAction)action);
            request.RowFocus.Add(focus);
        }
        return request;
    }

    private static string Bounded(string text, int max = MaxText) => text == null ? string.Empty : text.Length > max ? text.Substring(0, max) : text;

    /// <summary>Console form: add=FOCUS (repeat) and row=keep|reroll|remove|replace[:FOCUS] (one per existing bonus, in order).</summary>
    public static bool TryParseConsole(IEnumerable<string> words, out RebirthArmorBonusKitRequest request, out string error)
    {
        request = new RebirthArmorBonusKitRequest();
        error = null;
        foreach (string word in words)
        {
            if (word.StartsWith("add=", StringComparison.Ordinal))
            {
                if (!RebirthArmorBonusFocus.TryParse(word.Substring(4), out var focus)) { error = "Unknown focus " + word.Substring(4); return false; }
                request.Add(focus);
            }
            else if (word.StartsWith("row=", StringComparison.Ordinal))
            {
                string value = word.Substring(4);
                int colon = value.IndexOf(':');
                string verb = colon < 0 ? value : value.Substring(0, colon);
                var focus = RebirthArmorBonusFocus.Any;
                if (colon >= 0 && !RebirthArmorBonusFocus.TryParse(value.Substring(colon + 1), out focus)) { error = "Unknown focus " + value.Substring(colon + 1); return false; }
                RebirthArmorBonusRowAction action;
                switch (verb)
                {
                    case "keep": action = RebirthArmorBonusRowAction.Keep; break;
                    case "reroll": action = RebirthArmorBonusRowAction.Reroll; break;
                    case "replace": action = RebirthArmorBonusRowAction.Replace; break;
                    case "remove": action = RebirthArmorBonusRowAction.Remove; break;
                    default: error = "Unknown row action " + verb; return false;
                }
                request.Row(action, focus);
            }
            else { error = "Unknown word " + word; return false; }
        }
        return true;
    }

    public override string ToString() =>
        "kit=" + Kit + " adds=[" + string.Join(", ", Adds) + "] rows=[" + string.Join(", ", Rows.Select((r, i) => r + (r == RebirthArmorBonusRowAction.Replace ? " " + RowFocus[i] : ""))) + "]";
}
