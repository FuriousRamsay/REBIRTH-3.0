using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

#nullable disable

// B09 executable saved Organize policy (D-19). One optional element per NPC in the existing NPC aggregate record
// (same mechanism as RebirthP8NpcWaterPolicy): absent / disabled = never organizes, so existing saves are unchanged.
//   <storageOrganizePolicy version="1" enabled="1" sort="2" merge="1" distant="1" sortWithin="0" default="x,y,z">
//     <container pos="x,y,z" inc="guidN" tags="wood;tools" deposits="1" withdrawals="1"/>
//     <pin item="resourceWood" pos="x,y,z"/>          per-item pin: exact item name -> container (beats tags and defaults)
//     <tagRule key="resourceWood" tag="wood"/>        item name or group name -> container tag
//     <preserve item="meleeToolTorch"/>               never moved
//   </storageOrganizePolicy>
// U4 read/write API: TryRead / TryWrite below (server main thread; clients go through the existing authenticated NPC session transport).
internal static class RebirthNpcB09PolicyStore
{
    private const string Element = "storageOrganizePolicy";
    internal const int MaxContainers = 32, MaxPins = 64, MaxTagRules = 64, MaxPreserve = 64, MaxText = 64;

    private static string Pos(Vector3i p)=>p.x.ToString(CultureInfo.InvariantCulture)+","+p.y.ToString(CultureInfo.InvariantCulture)+","+p.z.ToString(CultureInfo.InvariantCulture);
    private static bool TryPos(string text, out Vector3i p)
    {
        p = default(Vector3i);
        var f = (text ?? string.Empty).Split(',');
        if (f.Length != 3 || !int.TryParse(f[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(f[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y) ||
            !int.TryParse(f[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int z)) return false;
        p = new Vector3i(x, y, z); return true;
    }
    private static bool Text(string s)=>!string.IsNullOrWhiteSpace(s) && s.Length <= MaxText && s.IndexOfAny(new[]{';','<','>','"'}) < 0;

    private static XElement Find(RebirthNpcPersistentRecord person)
    {
        XElement result = null;
        foreach (var raw in person.UnknownOptionalElements ?? Array.Empty<string>())
        {
            var row = XElement.Parse(raw);
            if (row.Name != Element) continue;
            if (result != null) throw new System.IO.InvalidDataException("Duplicate organize policy.");
            result = row;
        }
        return result;
    }

    // enabled=false and an empty policy when nothing is saved. Containers whose saved incarnation no longer matches are
    // still returned; the planner drops them (replaced container), so a stale policy never targets a new container.
    internal static bool TryRead(RebirthNpcStableId npc, out RebirthNpcB09Policy policy, out bool enabled)
    {
        policy = new RebirthNpcB09Policy(); enabled = false;
        try
        {
            if (npc.IsEmpty || !RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var person)) return true;
            var row = Find(person);
            if (row == null) return true;
            if ((string)row.Attribute("version") != "1") return false;
            enabled = (string)row.Attribute("enabled") == "1";
            int sort = (int?)row.Attribute("sort") ?? 0;
            policy.Sort = sort >= 0 && sort <= 2 ? (RebirthNpcB09SortOrder)sort : RebirthNpcB09SortOrder.None;
            policy.Merge = (string)row.Attribute("merge") != "0";
            policy.AllowDistant = (string)row.Attribute("distant") != "0";
            policy.SortWithinContainers = (string)row.Attribute("sortWithin") == "1";
            if (TryPos((string)row.Attribute("default"), out var def)) { policy.HasDefaultContainer = true; policy.DefaultContainer = def; }
            var containers = new List<RebirthNpcB09ContainerSpec>();
            foreach (var c in row.Elements("container").Take(MaxContainers))
            {
                if (!TryPos((string)c.Attribute("pos"), out var p)) continue;
                var tags = ((string)c.Attribute("tags") ?? string.Empty).Split(new[]{';'}, StringSplitOptions.RemoveEmptyEntries);
                containers.Add(new RebirthNpcB09ContainerSpec{Position=p,Incarnation=(string)c.Attribute("inc"),PlayerTags=tags,
                    AcceptsDeposits=(string)c.Attribute("deposits") != "0",AllowsWithdrawals=(string)c.Attribute("withdrawals") != "0"});
            }
            policy.Containers = containers.ToArray();
            foreach (var x in row.Elements("pin").Take(MaxPins))
                if (TryPos((string)x.Attribute("pos"), out var pp) && Text((string)x.Attribute("item"))) policy.ItemRules[(string)x.Attribute("item")] = pp;
            foreach (var x in row.Elements("tagRule").Take(MaxTagRules))
                if (Text((string)x.Attribute("key")) && Text((string)x.Attribute("tag"))) policy.TagRules[(string)x.Attribute("key")] = (string)x.Attribute("tag");
            foreach (var x in row.Elements("preserve").Take(MaxPreserve))
                if (Text((string)x.Attribute("item"))) policy.PreserveItems.Add((string)x.Attribute("item"));
            return true;
        }
        catch { policy = new RebirthNpcB09Policy(); enabled = false; return false; }
    }

    // Server only. Validates against the live world: every container must be a named container the owner exactly owns
    // (busy-by-owner is allowed while editing); its CURRENT incarnation is stored. Pins must target listed containers.
    internal static bool TryWrite(EntityPlayer owner, RebirthNpcStableId npc, RebirthNpcB09Policy policy, bool enabled, out string error)
    {
        error = "Organize policy rejected.";
        try
        {
            var world = GameManager.Instance?.World;
            if (npc.IsEmpty || policy == null || owner == null || world == null || world.IsRemote() || !ThreadManager.IsMainThread()) return false;
            if ((policy.Containers?.Length ?? 0) > MaxContainers || policy.ItemRules.Count > MaxPins ||
                policy.TagRules.Count > MaxTagRules || policy.PreserveItems.Count > MaxPreserve) { error = "Too many policy entries."; return false; }
            var listed = new HashSet<Vector3i>();
            var image = new XElement(Element, new XAttribute("version", 1), new XAttribute("enabled", enabled ? "1" : "0"),
                new XAttribute("sort", (int)policy.Sort), new XAttribute("merge", policy.Merge ? "1" : "0"),
                new XAttribute("distant", policy.AllowDistant ? "1" : "0"), new XAttribute("sortWithin", policy.SortWithinContainers ? "1" : "0"),
                policy.HasDefaultContainer ? new XAttribute("default", Pos(policy.DefaultContainer)) : null);
            foreach (var spec in policy.Containers ?? new RebirthNpcB09ContainerSpec[0])
            {
                var feature = (world.GetTileEntity(spec.Position) as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>();
                if (feature == null || !RebirthNpcStorageOrganizationWork.Named(spec.Position, owner, feature.Incarnation, true) || !listed.Add(spec.Position))
                { error = "A container is not a named, owned, loaded container (or is listed twice)."; return false; }
                var tags = (spec.PlayerTags ?? new string[0]).Where(t => t != null).Select(t => t.Trim()).ToArray();
                if (tags.Any(t => !Text(t))) { error = "Invalid tag text."; return false; }
                image.Add(new XElement("container", new XAttribute("pos", Pos(spec.Position)), new XAttribute("inc", feature.Incarnation),
                    new XAttribute("tags", string.Join(";", tags)), new XAttribute("deposits", spec.AcceptsDeposits ? "1" : "0"),
                    new XAttribute("withdrawals", spec.AllowsWithdrawals ? "1" : "0")));
            }
            if (policy.HasDefaultContainer && !listed.Contains(policy.DefaultContainer)) { error = "Default container is not listed."; return false; }
            foreach (var pin in policy.ItemRules)
            {
                if (!Text(pin.Key) || !listed.Contains(pin.Value)) { error = "A pin names an unlisted container or invalid item."; return false; }
                image.Add(new XElement("pin", new XAttribute("item", pin.Key), new XAttribute("pos", Pos(pin.Value))));
            }
            foreach (var rule in policy.TagRules)
            {
                if (!Text(rule.Key) || !Text(rule.Value)) { error = "Invalid tag rule."; return false; }
                image.Add(new XElement("tagRule", new XAttribute("key", rule.Key), new XAttribute("tag", rule.Value)));
            }
            foreach (var item in policy.PreserveItems)
            {
                if (!Text(item)) { error = "Invalid preserve entry."; return false; }
                image.Add(new XElement("preserve", new XAttribute("item", item)));
            }
            string raw = image.ToString(SaveOptions.DisableFormatting);
            if (!RebirthNpcAggregatePersistenceStore.Mutate(npc, record =>
            {
                var rows = (record.UnknownOptionalElements ?? Array.Empty<string>()).Where(x => XElement.Parse(x).Name != Element);
                record.UnknownOptionalElements = rows.Concat(new[]{raw}).ToArray();
            })) { error = "NPC record unavailable."; return false; }
            RebirthNpcAggregatePersistenceStore.Save();
            bool ok = RebirthNpcAggregatePersistenceStore.TryReadSavedPerson(npc, out var saved) && XNode.DeepEquals(Find(saved), image);
            error = ok ? string.Empty : "Policy was not read back from the save.";
            return ok;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
}
