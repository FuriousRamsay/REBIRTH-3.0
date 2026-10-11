using System;
using System.Globalization;
using System.Xml.Linq;

// Durable custody record for the owner-acknowledged music transfer protocol.
// A pending removal still occupies its library slot until owner acknowledgement.
public sealed class RebirthMusicTransferState
{
    public string TransactionId = string.Empty;
    public string CreationId = string.Empty;
    public int Operation;
    public bool IsAudiobook;
    public string AudiobookSlotId = string.Empty;
    public long ExpectedRevision;
    public int LibraryIndex;
    public bool SourceIsBag;
    public int SourceIndex;
    public string ItemId = string.Empty;
    public string ItemData = string.Empty;

    public RebirthMusicTransferState Clone()
    {
        return (RebirthMusicTransferState)MemberwiseClone();
    }

    public XElement ToXml()
    {
        return new XElement("pendingTransfer",
            new XAttribute("transactionId", TransactionId), new XAttribute("creationId", CreationId),
            new XAttribute("operation", Operation), new XAttribute("revision", ExpectedRevision),
            new XAttribute("libraryIndex", LibraryIndex), new XAttribute("sourceIsBag", SourceIsBag),
            new XAttribute("sourceIndex", SourceIndex), new XAttribute("itemId", ItemId),
            new XAttribute("itemData", ItemData), new XAttribute("isAudiobook",IsAudiobook),new XAttribute("audiobookSlotId",AudiobookSlotId));
    }

    public static bool TryRead(XElement element, out RebirthMusicTransferState state)
    {
        state = null;
        if (element == null) return true;
        var candidate = new RebirthMusicTransferState();
        Guid transaction;
        string creation;
        string id = (string)element.Attribute("transactionId");
        string origin = (string)element.Attribute("creationId");
        if (!Guid.TryParse(id, out transaction) || transaction == Guid.Empty
            || !RebirthSurvivorRequestScope.TryNormalize(origin, out creation)
            || !int.TryParse((string)element.Attribute("operation"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.Operation)
            || (candidate.Operation != 1 && candidate.Operation != 2)
            || !long.TryParse((string)element.Attribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.ExpectedRevision)
            || candidate.ExpectedRevision < 0
            || !int.TryParse((string)element.Attribute("libraryIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.LibraryIndex)
            || candidate.LibraryIndex < 0 || candidate.LibraryIndex >= RebirthAudiobookLibraryPersistence.Capacity
            || !bool.TryParse((string)element.Attribute("sourceIsBag"), out candidate.SourceIsBag)
            || !int.TryParse((string)element.Attribute("sourceIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out candidate.SourceIndex)
            || candidate.SourceIndex < 0) return false;
        string section=(string)element.Attribute("isAudiobook");
        if(section!=null && !bool.TryParse(section,out candidate.IsAudiobook))return false;
        if(!candidate.IsAudiobook && candidate.LibraryIndex>=RebirthMusicLibraryService.Capacity)return false;
        candidate.AudiobookSlotId=(string)element.Attribute("audiobookSlotId") ?? string.Empty;
        Guid slot;
        if(candidate.IsAudiobook)
        {
            if(!Guid.TryParseExact(candidate.AudiobookSlotId,"N",out slot)||slot==Guid.Empty)return false;
            candidate.AudiobookSlotId=slot.ToString("N");
        }
        else if(candidate.AudiobookSlotId.Length!=0)return false;
        candidate.TransactionId = transaction.ToString("N");
        candidate.CreationId = creation;
        candidate.ItemId = (string)element.Attribute("itemId") ?? string.Empty;
        candidate.ItemData = (string)element.Attribute("itemData") ?? string.Empty;
        if (candidate.ItemId.Length == 0 || candidate.ItemData.Length == 0 ||
            candidate.IsAudiobook && (candidate.ItemId.Length > 256 || candidate.ItemData.Length > 131072)) return false;
        try { if (Convert.FromBase64String(candidate.ItemData).Length == 0) return false; }
        catch (FormatException) { return false; }
        state = candidate;
        return true;
    }
}
