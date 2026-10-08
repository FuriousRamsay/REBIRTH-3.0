using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Last durable terminal outcome. No item images, and never permission to repeat a move.
public sealed class RebirthBackpackLibrarySettlement
{
    public string CreationId {get;private set;}
    public string TransactionId {get;private set;}
    public long GearRevision {get;private set;}
    public bool Applied {get;private set;}
    private RebirthBackpackLibrarySettlement(string creation,Guid transaction,long revision,bool applied)
    {CreationId=creation;TransactionId=transaction.ToString("N");GearRevision=revision;Applied=applied;}
    public static RebirthBackpackLibrarySettlement Create(RebirthBackpackLibraryReceipt receipt,bool applied)
    {
        if(receipt==null||receipt.ExpectedGearRevision<0||receipt.ExpectedGearRevision==long.MaxValue)throw new ArgumentException("Invalid library settlement receipt.");
        return new RebirthBackpackLibrarySettlement(receipt.CreationId,Guid.Parse(receipt.TransactionId),receipt.ExpectedGearRevision+1,applied);
    }
    public XElement Write()=>new XElement("librarySettlement",new XAttribute("version",1),new XAttribute("creation",CreationId),
        new XAttribute("transaction",TransactionId),new XAttribute("revision",GearRevision),new XAttribute("applied",Applied));
    public static bool TryRead(XElement support,long gearRevision,out RebirthBackpackLibrarySettlement result)
    {
        result=null;if(support==null||gearRevision<0||support.Elements("librarySettlement").Count()>1)return false;
        var node=support.Element("librarySettlement");if(node==null)return true;
        if(node.Attributes().Count()!=5||(string)node.Attribute("version")!="1"||node.Elements().Any()||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
            !Guid.TryParse((string)node.Attribute("transaction"),out var transaction)||transaction==Guid.Empty||
            !long.TryParse((string)node.Attribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out long revision)||revision<=0||revision>gearRevision||
            !bool.TryParse((string)node.Attribute("applied"),out bool applied))return false;
        result=new RebirthBackpackLibrarySettlement(creation,transaction,revision,applied);return true;
    }
}