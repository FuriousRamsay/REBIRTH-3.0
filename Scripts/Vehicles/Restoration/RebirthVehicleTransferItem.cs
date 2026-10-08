using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Receipt identity belongs to one immutable operation, not a UI click or slot.
public sealed class RebirthVehicleTransferItem
{
    public Guid ReceiptId { get; private set; }
    public bool Debit { get; private set; }
    public string ItemData { get; private set; }
    public int Count { get; private set; }
    public RebirthVehiclePartSourceLocation Source { get; private set; }
    public int Slot { get; private set; }

    public RebirthVehicleTransferItem(Guid receiptId, bool debit, string itemData, int count,
        RebirthVehiclePartSourceLocation source, int slot)
    {
        ItemValue decoded;
        if (receiptId == Guid.Empty || count <= 0 || !RebirthNativeItemCodec.TryDecode(itemData, out decoded))
            throw new InvalidDataException("Invalid vehicle transfer item.");
        if (debit ? (slot < 0 || (source != RebirthVehiclePartSourceLocation.Backpack && source != RebirthVehiclePartSourceLocation.Toolbelt))
            : (source != RebirthVehiclePartSourceLocation.None || slot != -1))
            throw new InvalidDataException("Invalid vehicle transfer inventory source.");
        ReceiptId = receiptId; Debit = debit; ItemData = itemData; Count = count; Source = source; Slot = slot;
    }

    public XElement ToXml()
    {
        return new XElement("item", new XAttribute("receipt", ReceiptId.ToString("N")),
            new XAttribute("debit", Debit), new XAttribute("count", Count),
            new XAttribute("source", (byte)Source), new XAttribute("slot", Slot), new XAttribute("data", ItemData));
    }

    public static RebirthVehicleTransferItem Read(XElement element)
    {
        Guid receipt; bool debit; int count, slot; byte source;
        if (element == null || element.Name != "item" || element.HasElements || element.Attributes().Count() != 6
            || !Guid.TryParse((string)element.Attribute("receipt"), out receipt)
            || !bool.TryParse((string)element.Attribute("debit"), out debit)
            || !int.TryParse((string)element.Attribute("count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
            || !byte.TryParse((string)element.Attribute("source"), NumberStyles.Integer, CultureInfo.InvariantCulture, out source)
            || !int.TryParse((string)element.Attribute("slot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
            throw new InvalidDataException("Invalid vehicle transfer item record.");
        return new RebirthVehicleTransferItem(receipt, debit, (string)element.Attribute("data"), count,
            (RebirthVehiclePartSourceLocation)source, slot);
    }
}
