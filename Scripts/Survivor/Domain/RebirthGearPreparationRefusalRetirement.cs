using System;
using System.Linq;
using System.Xml.Linq;

// A durable SERVER acknowledgment of original owner-file retirement, not an offer.
// Codec validity alone is not proof that native owner retirement happened.
public static class RebirthGearPreparationRefusalRetirement
{
    public static bool MatchesSupport(RebirthGearPreparationRefusal retired,string creation,long revision,
        RebirthGearPreparationRefusal pending,string settledTransaction)
        =>retired!=null&&retired.MatchesSupport(creation,revision,false,settledTransaction)&&
            (pending==null||pending.TransactionId!=retired.TransactionId);
    public static XElement Write(RebirthGearPreparationRefusal retired)
        =>retired==null?null:new XElement("gearPreparationRefusalRetirement",new XAttribute("version",1),retired.Write());
    public static bool TryRead(XElement support,out RebirthGearPreparationRefusal retired)
    {
        retired=null;
        if(support==null||support.Elements("gearPreparationRefusalRetirement").Count()>1)return false;
        var node=support.Element("gearPreparationRefusalRetirement");if(node==null)return true;
        if(node.Attributes().Count()!=1||(string)node.Attribute("version")!="1"||
            node.Elements().Count()!=1||node.Elements("gearPreparationRefusal").Count()!=1||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        return RebirthGearPreparationRefusal.TryRead(node,out retired)&&retired!=null;
    }
}