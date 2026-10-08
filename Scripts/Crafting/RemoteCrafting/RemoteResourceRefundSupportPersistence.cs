using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Optional support attachment; world-key authentication belongs to the world adapter.
// Owner and character are supplied by the enclosing authoritative character record.
internal static class RemoteResourceRefundSupportPersistence
{
    internal static bool TryRead(XElement support,string owner,string creation,out XElement image)
    {
        image=null;
        if(support==null||support.Elements("remoteResourceRefunds").Count()>1)return false;
        var node=support.Element("remoteResourceRefunds");if(node==null)return true;
        if(!RemoteResourceRefundJournal.TryRead(node,(string)node.Attribute("world"),owner,creation,out var journal))return false;
        image=journal.ToXml();return true;
    }
    internal static XElement Write(XElement image,string owner,string creation)
    {
        if(image==null)return null;
        if(!RemoteResourceRefundJournal.TryRead(image,(string)image.Attribute("world"),owner,creation,out var journal))
            throw new InvalidDataException("Invalid remote resource refund custody; refusing to discard retained items.");
        return journal.ToXml();
    }
}