using System;
using System.Collections.Generic;
using System.Linq;

// Bounded evidence for "Native equipment write was retained by another owner": which fields
// the native SetItem/SetSlotItem path changed between the written and the read-back stack.
internal static class RebirthP2NativeWriteDiagnostics
{
    private const int MaxSignatures = 32, FrameExcerpt = 96;
    private static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);

    private static string Mods(ItemValue[] array) =>
        array == null ? "null" : array.Length + "[" + string.Join(",", array.Select(m => m == null || m.IsEmpty() ? "-" : m.type + "/" + m.Quality + "/" + m.UseTimes + "/" + m.Meta)) + "]";

    internal static string Diff(ItemStack want, ItemStack got)
    {
        var list = new List<string>();
        if (want == null || got == null) return "null-stack";
        if (want.count != got.count) list.Add("count:" + want.count + "->" + got.count);
        ItemValue a = want.itemValue, b = got.itemValue;
        if (a == null || b == null) { list.Add("itemValue:" + (a == null) + "->" + (b == null)); return string.Join(";", list); }
        if (a.type != b.type) list.Add("type:" + a.type + "->" + b.type);
        if (a.Quality != b.Quality) list.Add("quality:" + a.Quality + "->" + b.Quality);
        if (a.UseTimes != b.UseTimes) list.Add("useTimes:" + a.UseTimes + "->" + b.UseTimes);
        if (a.Meta != b.Meta) list.Add("meta:" + a.Meta + "->" + b.Meta);
        if (a.Activated != b.Activated) list.Add("activated:" + a.Activated + "->" + b.Activated);
        if (a.SelectedAmmoTypeIndex != b.SelectedAmmoTypeIndex) list.Add("ammoIdx:" + a.SelectedAmmoTypeIndex + "->" + b.SelectedAmmoTypeIndex);
        string ma = Mods(a.modifications), mb = Mods(b.modifications);
        if (ma != mb) list.Add("mods:" + ma + "->" + mb);
        string ca = Mods(a.cosmeticMods), cb = Mods(b.cosmeticMods);
        if (ca != cb) list.Add("cosmetic:" + ca + "->" + cb);
        if (list.Count == 0) list.Add("other(encoded-only)");
        return string.Join(";", list);
    }

    // One line per distinct (slot,item,fields) signature; at most MaxSignatures per session.
    internal static void Report(EntityRebirthHumanoidNPC npc, int slot, ItemStack want, ItemStack got, string outcome)
    {
        try
        {
            string item = want?.itemValue?.ItemClass?.GetItemName() ?? "?";
            string fields = Diff(want, got);
            string signature = slot + "|" + item + "|" + fields;
            lock (Seen) { if (Seen.Count >= MaxSignatures || !Seen.Add(signature)) return; }
            string wf = RebirthHumanEquipment.Frame(want), gf = RebirthHumanEquipment.Frame(got);
            Log.Warning("[REBIRTH NPC Equipment Diag] write-readback-mismatch outcome=" + outcome + " entity=" + (npc != null ? npc.entityId : -1) +
                " slot=" + slot + " item=" + item + " fields=" + fields + " wantLen=" + wf.Length + " gotLen=" + gf.Length +
                " want=" + wf.Substring(0, Math.Min(FrameExcerpt, wf.Length)) + " got=" + gf.Substring(0, Math.Min(FrameExcerpt, gf.Length)));
        }
        catch (Exception e) { Log.Warning("[REBIRTH NPC Equipment Diag] report failed " + e.GetType().Name); }
    }

    // The only normalization accepted for newly created birth gear: native hold/equip may
    // initialise the Meta (magazine/ammo) counter. Everything else must be byte-identical.
    internal static bool IsMetaOnly(ItemStack want, ItemStack got)
    {
        if (want == null || got == null || want.IsEmpty() || got.IsEmpty() || want.count != got.count) return false;
        ItemValue normalized = got.itemValue.Clone();
        normalized.Meta = want.itemValue.Meta;
        return RebirthNativeItemCodec.Encode(normalized) == RebirthNativeItemCodec.Encode(want.itemValue);
    }
}
