using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

public enum RebirthBackpackLibraryPhase { Prepared, OwnerApplied, BackpackCommitted }
public static class RebirthBackpackLibraryPersistence
{
    private static bool Valid(long gearRevision,RebirthBackpackLibraryReceipt pending,RebirthBackpackLibraryPhase phase)
    {
        if(gearRevision<0||phase<RebirthBackpackLibraryPhase.Prepared||phase>RebirthBackpackLibraryPhase.BackpackCommitted)return false;
        if(pending==null)return phase==RebirthBackpackLibraryPhase.Prepared;
        return gearRevision==pending.ExpectedGearRevision+(phase==RebirthBackpackLibraryPhase.BackpackCommitted?1:0);
    }
    public static XElement Write(long gearRevision,RebirthBackpackLibraryReceipt pending,RebirthBackpackLibraryPhase phase)
    {
        if(!Valid(gearRevision,pending,phase))throw new InvalidOperationException("Invalid library custody phase/revision.");
        return new XElement("libraryTransfers",new XAttribute("version",1),new XAttribute("phase",(int)phase),pending?.ToXml());
    }
    public static bool TryRead(XElement support,long gearRevision,out RebirthBackpackLibraryReceipt pending,out RebirthBackpackLibraryPhase phase)
    {
        pending=null;phase=RebirthBackpackLibraryPhase.Prepared;
        if(support==null||support.Elements("libraryTransfers").Count()>1)return false;
        var node=support.Element("libraryTransfers");if(node==null)return gearRevision>=0;
        if(node.Attributes().Count()!=2||(string)node.Attribute("version")!="1"||
            !int.TryParse((string)node.Attribute("phase"),NumberStyles.Integer,CultureInfo.InvariantCulture,out int stage)||
            node.Elements().Count()>1||node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var candidatePhase=(RebirthBackpackLibraryPhase)stage;var child=node.Elements().FirstOrDefault();
        RebirthBackpackLibraryReceipt candidate=null;
        if(child!=null&&!RebirthBackpackLibraryReceipt.TryRead(child,out candidate))return false;
        if(!Valid(gearRevision,candidate,candidatePhase))return false;
        pending=candidate;phase=candidatePhase;return true;
    }
}
