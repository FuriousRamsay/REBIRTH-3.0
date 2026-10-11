using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Separate physical audiobook custody; playback order is the saved list order.
public sealed class RebirthAudiobookCassetteState
{
    public string SlotId = string.Empty;
    public string ItemId = string.Empty;
    public string ItemData = string.Empty;
    public RebirthAudiobookCassetteState Clone() => (RebirthAudiobookCassetteState)MemberwiseClone();
}

public static class RebirthAudiobookLibraryPersistence
{
    // 152 authored audiobooks x 1.5; byte-sized network count remains compatible.
    public const int Capacity = 228;
    private const int MaxEncodedItemLength = 131072;
    public static XElement Write(long revision, IList<RebirthAudiobookCassetteState> entries)
    {
        var node = new XElement("audiobooks",new XAttribute("version",1),new XAttribute("revision",revision));
        if(entries == null)throw new InvalidOperationException("Missing audiobook custody.");
        foreach(var entry in entries)
        {
            if(entry == null)throw new InvalidOperationException("Null audiobook custody entry.");
            node.Add(new XElement("cassette",new XAttribute("slotId",entry.SlotId ?? ""),
                new XAttribute("itemId",entry.ItemId ?? ""),new XAttribute("itemData",entry.ItemData ?? "")));
        }
        long checkedRevision; List<RebirthAudiobookCassetteState> checkedEntries;
        if(!TryRead(node,out checkedRevision,out checkedEntries))
            throw new InvalidOperationException("Invalid audiobook custody; refusing to save.");
        return node;
    }
    public static bool TryRead(XElement node,out long revision,out List<RebirthAudiobookCassetteState> entries)
    {
        revision=0; entries=new List<RebirthAudiobookCassetteState>();
        // Existing saves have no audio section; never infer ownership from carried tapes.
        if(node == null)return true;
        long parsed;
        if(node.Name != "audiobooks" || (string)node.Attribute("version") != "1" ||
            !long.TryParse((string)node.Attribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out parsed) || parsed < 0 ||
            node.Elements().Any(x => x.Name != "cassette"))return false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loaded = new List<RebirthAudiobookCassetteState>();
        foreach(var cassette in node.Elements("cassette"))
        {
            Guid id; string slot=(string)cassette.Attribute("slotId");
            string item=(string)cassette.Attribute("itemId"); string data=(string)cassette.Attribute("itemData");
            if(loaded.Count >= Capacity || !Guid.TryParseExact(slot,"N",out id) || id == Guid.Empty ||
                !seen.Add(id.ToString("N")) || string.IsNullOrWhiteSpace(item) || item.Length > 256 ||
                string.IsNullOrEmpty(data) || data.Length > MaxEncodedItemLength || cassette.HasElements)return false;
            try { if(Convert.FromBase64String(data).Length == 0)return false; }
            catch(FormatException) { return false; }
            loaded.Add(new RebirthAudiobookCassetteState { SlotId=id.ToString("N"),ItemId=item,ItemData=data });
        }
        revision=parsed; entries=loaded; return true;
    }
}