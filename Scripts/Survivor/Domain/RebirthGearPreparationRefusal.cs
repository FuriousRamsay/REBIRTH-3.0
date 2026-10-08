using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Data-only refusal of an ORIGINAL request that has never acquired a server offer.
// Factory/XML validity supplies NO sender, saved-file or custody-release authority.
public sealed class RebirthGearPreparationRefusal
{
    public string OriginalMarker {get;private set;}
    public Guid SavedWorld {get;private set;}
    public string CreationId {get;private set;}
    public Guid TransactionId {get;private set;}
    public long ExpectedRevision {get;private set;}
    public long ObservedRevision {get;private set;}
    public string RequestDigest {get;private set;}
    private RebirthGearPreparationRefusal(){}
    public static bool TryCreateStale(string marker,long observedRevision,out RebirthGearPreparationRefusal refusal)
    {
        refusal=null;
        if(!RebirthGearPreparationMarker.TryRead(marker,1f,out var world,out _,out var intent)||
            observedRevision<=intent.ExpectedRevision)return false;
        string digest;
        using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(marker))).Replace("-",string.Empty).ToLowerInvariant();
        refusal=new RebirthGearPreparationRefusal{OriginalMarker=marker,SavedWorld=world,CreationId=intent.CreationId,
            TransactionId=intent.TransactionId,ExpectedRevision=intent.ExpectedRevision,ObservedRevision=observedRevision,RequestDigest=digest};return true;
    }
    public bool MatchesOriginal(string marker)=>marker==OriginalMarker;
    public bool MatchesSupport(string creation,long gearRevision,bool hasPendingCustody,string settledTransaction)
        =>RebirthSurvivorRequestScope.Matches(CreationId,creation)&&ObservedRevision<=gearRevision&&
            !hasPendingCustody&&settledTransaction!=TransactionId.ToString("N");
    public XElement Write()=>new XElement("gearPreparationRefusal",new XAttribute("version",1),
        new XAttribute("reason","stale_revision"),new XAttribute("marker",OriginalMarker),
        new XAttribute("observedRevision",ObservedRevision.ToString(CultureInfo.InvariantCulture)));
    // Optional backward-readable child; absent is valid old state, never a refusal.
    public static bool TryRead(XElement support,out RebirthGearPreparationRefusal refusal)
    {
        refusal=null;if(support==null||support.Elements("gearPreparationRefusal").Count()>1)return false;
        var node=support.Element("gearPreparationRefusal");if(node==null)return true;
        string revision=(string)node.Attribute("observedRevision");
        if(node.Attributes().Count()!=4||(string)node.Attribute("version")!="1"||
            (string)node.Attribute("reason")!="stale_revision"||node.Elements().Any()||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            !long.TryParse(revision,NumberStyles.None,CultureInfo.InvariantCulture,out var observed)||
            revision!=observed.ToString(CultureInfo.InvariantCulture))return false;
        return TryCreateStale((string)node.Attribute("marker"),observed,out refusal);
    }
}