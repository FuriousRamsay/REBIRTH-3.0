using System.Linq;
using System.Xml.Linq;

// Last terminal original plan only. Never a pending transfer or replay authority.
public static class RebirthGearTerminalOriginalPersistence
{
    public static bool Matches(RebirthGearSettlement terminal,RebirthGearTransferState original)
        => terminal!=null&&original!=null&&terminal.PreparationRequestDigest!=null&&
            terminal.PreparationRequestDigest==original.PreparationRequestDigest&&
            terminal.TransactionId==original.TransactionId&&terminal.CreationId==original.CreationId&&
            terminal.GearRevision==original.ExpectedRevision+1&&
            (!original.HasRecoveryAttempts||terminal.Applied)&&
            (!terminal.Applied||original.HasAllRecoveryPublicationReceipts);
    public static XElement Write(RebirthGearSettlement terminal,RebirthGearTransferState original)
    {
        if(original==null)return null;
        if(!Matches(terminal,original)||!RebirthGearTransferState.TryRead(original.ToXml(),out _))
            throw new System.InvalidOperationException("Invalid retained terminal gear original.");
        return new XElement("gearSettlementOriginal",new XAttribute("version",1),original.ToXml());
    }
    public static bool TryRead(XElement support,RebirthGearSettlement terminal,out RebirthGearTransferState original)
    {
        original=null;
        if(support==null||support.Elements("gearSettlementOriginal").Count()>1)return false;
        var node=support.Element("gearSettlementOriginal");if(node==null)return true; // Legacy absence grants no plan.
        if(node.Attributes().Count()!=1||(string)node.Attribute("version")!="1"||node.Elements().Count()!=1||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
            !RebirthGearTransferState.TryRead(node.Elements().Single(),out var copy)||!Matches(terminal,copy))return false;
        original=copy;return true;
    }
}