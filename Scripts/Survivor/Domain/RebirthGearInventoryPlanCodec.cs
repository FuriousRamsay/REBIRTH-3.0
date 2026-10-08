using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Versioned plan payload only; transaction identity/revision and custody journal
// belong to the pending-transfer envelope. No live save path uses this yet.
public static class RebirthGearInventoryPlanCodec
{
    public static XElement Write(RebirthGearInventoryPlan plan)
    {
        if (plan == null || !plan.IsConserved()) throw new ArgumentException("Invalid gear inventory plan.");
        return new XElement("gearPlan", new XAttribute("version", 1),
            new XAttribute("bagBefore", plan.BagSlotsBefore), new XAttribute("bagAfter", plan.BagSlotsAfter),
            new XAttribute("beltBefore", plan.BeltSlotsBefore), new XAttribute("beltAfter", plan.BeltSlotsAfter),
            Item("gearBefore", plan.GearBefore), Item("gearAfter", plan.GearAfter),
            new XElement("changes", plan.Changes.Select(c => new XElement("change",
                new XAttribute("bag", c.IsBag), new XAttribute("index", c.Index),
                Item("before", c.Before), Item("after", c.After)))),
            new XElement("recovery", plan.Recovery.Select(r => new XElement("entry",
                new XAttribute("origin", (int)r.Origin), new XAttribute("index", r.SourceIndex), Item("item", r.Item)))));
    }

    public static bool TryRead(XElement xml, out RebirthGearInventoryPlan plan)
    {
        plan = null;
        if (!Shape(xml, "gearPlan", "version,bagBefore,bagAfter,beltBefore,beltAfter", "gearBefore,gearAfter,changes,recovery")) return false;
        int version;
        var candidate = new RebirthGearInventoryPlan();
        if (!Number(xml, "version", out version) || version != 1
            || !Number(xml, "bagBefore", out candidate.BagSlotsBefore) || !Number(xml, "bagAfter", out candidate.BagSlotsAfter)
            || !Number(xml, "beltBefore", out candidate.BeltSlotsBefore) || !Number(xml, "beltAfter", out candidate.BeltSlotsAfter)
            || !ReadItem(xml.Element("gearBefore"), "gearBefore", out candidate.GearBefore)
            || !ReadItem(xml.Element("gearAfter"), "gearAfter", out candidate.GearAfter)) return false;
        XElement changes = xml.Element("changes"), recovery = xml.Element("recovery");
        if (changes.HasAttributes || recovery.HasAttributes
            || changes.Elements().Take(190).Count() > 189 || recovery.Elements().Take(190).Count() > 189
            || HasNonElements(changes) || HasNonElements(recovery)) return false;
        foreach (XElement element in changes.Elements())
        {
            var change = new RebirthGearInventoryPlan.Change();
            if (!Shape(element, "change", "bag,index", "before,after")
                || !bool.TryParse((string)element.Attribute("bag"), out change.IsBag)
                || !Number(element, "index", out change.Index)
                || !ReadItem(element.Element("before"), "before", out change.Before)
                || !ReadItem(element.Element("after"), "after", out change.After)) return false;
            candidate.Changes.Add(change);
        }
        foreach (XElement element in recovery.Elements())
        {
            var entry = new RebirthGearInventoryPlan.RecoveryEntry();
            int origin;
            if (!Shape(element, "entry", "origin,index", "item") || !Number(element, "origin", out origin)
                || !Number(element, "index", out entry.SourceIndex)
                || !ReadItem(element.Element("item"), "item", out entry.Item)) return false;
            entry.Origin = (RebirthGearInventoryPlan.RecoveryOrigin)origin;
            candidate.Recovery.Add(entry);
        }
        if (!candidate.IsConserved()) return false;
        plan = candidate;
        return true;
    }

    private static XElement Item(string name, RebirthGearInventoryPlan.Stack item)
    {
        return new XElement(name, new XAttribute("count", item.Count), new XAttribute("data", item.ItemData));
    }

    private static bool ReadItem(XElement xml, string name, out RebirthGearInventoryPlan.Stack item)
    {
        item = null;
        int count;
        if (!Shape(xml, name, "count,data", "") || !Number(xml, "count", out count)) return false;
        string data = (string)xml.Attribute("data");
        if (data.Length > 262144) return false;
        item = new RebirthGearInventoryPlan.Stack { Count = count, ItemData = data };
        return true;
    }

    private static bool Number(XElement xml, string name, out int value)
    {
        return int.TryParse((string)xml.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool Shape(XElement xml, string name, string attributes, string children)
    {
        if (xml == null || xml.Name != name || HasNonElements(xml)) return false;
        string[] attrs = attributes.Length == 0 ? new string[0] : attributes.Split(',');
        string[] elements = children.Length == 0 ? new string[0] : children.Split(',');
        return xml.Attributes().Count() == attrs.Length && attrs.All(a => xml.Attribute(a) != null)
            && xml.Elements().Count() == elements.Length && elements.All(e => xml.Elements(e).Count() == 1);
    }

    private static bool HasNonElements(XElement xml)
    {
        return xml.Nodes().Any(n => !(n is XElement) && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)));
    }
}
