using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Last durable terminal outcome. No item images, and never permission to repeat a move.
public sealed class RebirthGearSettlement
{
    public string CreationId {get;private set;}
    public string TransactionId {get;private set;}
    public long GearRevision {get;private set;}
    public bool Applied {get;private set;}
    public string PreparationRequestDigest {get;private set;}
    private RebirthGearSettlement(string creation,Guid transaction,long revision,bool applied,string digest)
    {CreationId=creation;TransactionId=transaction.ToString("N");GearRevision=revision;Applied=applied;PreparationRequestDigest=digest;}
    public static RebirthGearSettlement Create(RebirthGearTransferState receipt,bool applied)
    {
        if(receipt==null||receipt.ExpectedRevision<0||receipt.ExpectedRevision==long.MaxValue)throw new ArgumentException("Invalid gear settlement receipt.");
        return new RebirthGearSettlement(receipt.CreationId,Guid.Parse(receipt.TransactionId),receipt.ExpectedRevision+1,applied,receipt.PreparationRequestDigest);
    }
    public XElement Write()
    {
        var node=new XElement("gearSettlement",new XAttribute("version",PreparationRequestDigest==null?1:2),
            new XAttribute("creation",CreationId),new XAttribute("transaction",TransactionId),
            new XAttribute("revision",GearRevision),new XAttribute("applied",Applied));
        if(PreparationRequestDigest!=null)node.Add(new XAttribute("requestDigest",PreparationRequestDigest));
        return node;
    }
    // Data comparison only; current saved-world/owner/session authentication and
    // final terminal-file witness must be established independently by caller.
    public bool MatchesOriginalMarker(string marker)
    {
        if(PreparationRequestDigest==null||!RebirthGearPreparationMarker.TryRead(marker,1f,out _,out _,out var intent)||
            intent.TransactionId.ToString("N")!=TransactionId||intent.CreationId!=CreationId||
            intent.ExpectedRevision+1!=GearRevision)return false;
        using(var hash=System.Security.Cryptography.SHA256.Create())
            return PreparationRequestDigest==BitConverter.ToString(hash.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(marker))).Replace("-",string.Empty).ToLowerInvariant();
    }
    private static bool IsDigest(string digest)
    {
        if(digest==null||digest.Length!=64)return false;
        foreach(char c in digest)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;
        return true;
    }
    public static bool TryRead(XElement support,long gearRevision,out RebirthGearSettlement result)
    {
        result=null;if(support==null||gearRevision<0||support.Elements("gearSettlement").Count()>1)return false;
        var node=support.Element("gearSettlement");if(node==null)return true;
        string version=(string)node.Attribute("version"),digest=(string)node.Attribute("requestDigest");
        if((version!="1"&&version!="2")||node.Attributes().Count()!=(version=="1"?5:6)||
            (version=="1"?digest!=null:!IsDigest(digest))||node.Elements().Any()||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
            !Guid.TryParse((string)node.Attribute("transaction"),out var transaction)||transaction==Guid.Empty||
            !long.TryParse((string)node.Attribute("revision"),NumberStyles.Integer,CultureInfo.InvariantCulture,out long revision)||revision<=0||revision>gearRevision||
            !bool.TryParse((string)node.Attribute("applied"),out bool applied))return false;
        result=new RebirthGearSettlement(creation,transaction,revision,applied,digest);return true;
    }
}