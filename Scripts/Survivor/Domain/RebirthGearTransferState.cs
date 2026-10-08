using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable pending-operation payload. Journal state must retain this until owner
// inventory and world recovery custody have both been durably resolved.
public sealed class RebirthGearTransferState
{
    public bool HasRecoveryAttempts { get { return recoveryAttemptsXml!=null; } }
    public string TransactionId { get; private set; }
    public string CreationId { get; private set; }
    public string SlotId { get; private set; }
    public long ExpectedRevision { get; private set; }
    public string PreparationRequestDigest { get; private set; }
    private readonly XElement planXml;
    private readonly XElement recoveryManifestXml;
    private readonly XElement recoveryAttemptsXml;
    private readonly XElement recoveryReceiptsXml;

    private RebirthGearTransferState(string transaction, string creation, string slot, long revision, XElement plan, XElement manifest, XElement attempts, XElement receipts, string preparationDigest)
    {
        TransactionId = transaction; CreationId = creation; SlotId = slot;
        ExpectedRevision = revision; PreparationRequestDigest=preparationDigest; planXml = new XElement(plan);
        recoveryManifestXml=manifest==null?null:new XElement(manifest);
        recoveryAttemptsXml=attempts==null?null:new XElement(attempts);
        recoveryReceiptsXml=receipts==null?null:new XElement(receipts);
    }

    public XElement ToXml()
    {
        return new XElement("pendingGearTransfer", new XAttribute("version", PreparationRequestDigest==null?1:2),
            new XAttribute("transactionId", TransactionId), new XAttribute("creationId", CreationId),
            new XAttribute("slot", SlotId), new XAttribute("revision", ExpectedRevision), new XElement(planXml),
            recoveryManifestXml==null?null:new XElement(recoveryManifestXml),
            recoveryAttemptsXml==null?null:new XElement(recoveryAttemptsXml),recoveryReceiptsXml==null?null:new XElement(recoveryReceiptsXml),
            PreparationRequestDigest==null?null:new XElement("originalRequest",new XAttribute("version",1),new XAttribute("digest",PreparationRequestDigest)));
    }

    // Pure initializer only: caller must attach before journal preparation. Digest
    // is SHA256 of the exact authenticated original marker, not a new request ID.
    public bool TryBindPreparationRequest(string digest,out RebirthGearTransferState bound)
    {
        bound=null;
        if(!ValidRequestDigest(digest)||HasRecoveryAttempts)return false;
        if(PreparationRequestDigest!=null){if(PreparationRequestDigest!=digest)return false;bound=this;return true;}
        var xml=ToXml();xml.SetAttributeValue("version",2);
        xml.Add(new XElement("originalRequest",new XAttribute("version",1),new XAttribute("digest",digest)));
        return TryRead(xml,out bound);
    }
    private static bool ValidRequestDigest(string digest)
        =>digest!=null&&digest.Length==64&&digest.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    // Caller must independently verify original native disk publication before saving.
    public bool HasRecoveryPublicationReceipt(Guid publication){return recoveryReceiptsXml?.Elements().Any(e=>(string)e.Attribute("id")==publication.ToString("N"))==true;}
    public bool HasAllRecoveryPublicationReceipts
    {
        get{if(!TryGetPlan(out var plan))return false;if(plan.Recovery.Count==0)return true;if(!TryGetRecoveryManifest(out var manifest))return false;return manifest.ToXml().Elements().All(e=>HasRecoveryPublicationReceipt(Guid.Parse((string)e.Attribute("id"))));}
    }
    public bool TryAppendRecoveryPublicationReceipt(Guid publication,out RebirthGearTransferState updated)
    {
        updated=null;if(!TryGetRecoveryAttempt(publication,out var attempt)||attempt.OriginalOwnerEntityId<=0||HasRecoveryPublicationReceipt(publication))return false;
        var xml=ToXml();var receipts=xml.Element("recoveryReceipts");if(receipts==null){receipts=new XElement("recoveryReceipts",new XAttribute("version",1));xml.Add(receipts);}receipts.Add(new XElement("publication",new XAttribute("id",publication.ToString("N"))));return TryRead(xml,out updated);
    }
    public bool TryGetPlan(out RebirthGearInventoryPlan plan)
    {
        return RebirthGearInventoryPlanCodec.TryRead(planXml, out plan);
    }

    public bool TryGetRecoveryManifest(out RebirthGearRecoveryManifest manifest)
    {
        manifest=null;return recoveryManifestXml!=null&&TryGetPlan(out var plan)&&RebirthGearRecoveryManifest.TryRead(recoveryManifestXml,plan,out manifest);
    }

    public bool TryAppendRecoveryAttempt(RebirthGearRecoveryAttempt attempt,out RebirthGearTransferState updated)
    {
        updated=null;if(attempt==null||!TryGetRecoveryManifest(out _))return false;
        var value=ToXml();var attempts=value.Element("recoveryAttempts");
        if(attempts==null){attempts=new XElement("recoveryAttempts",new XAttribute("version",1));value.Add(attempts);}
        if(attempts.Elements().Any(e=>(string)e.Attribute("id")==attempt.PublicationId.ToString("N")))return false;
        attempts.Add(attempt.ToXml());return TryRead(value,out updated);
    }
    public bool TryGetRecoveryAttempt(Guid publication,out RebirthGearRecoveryAttempt attempt)
    {
        attempt=null;var value=recoveryAttemptsXml?.Elements().SingleOrDefault(e=>(string)e.Attribute("id")==publication.ToString("N"));
        return value!=null&&RebirthGearRecoveryAttempt.TryRead(value,out attempt);
    }
    public static bool TryCreate(string transaction, string creation, string slot, long revision,
        RebirthGearInventoryPlan plan, out RebirthGearTransferState state)
    {
        state = null;
        if (plan == null || !plan.IsConserved()) return false;
        RebirthGearRecoveryManifest manifest=null;
        if(plan.Recovery.Count>0&&!RebirthGearRecoveryManifest.TryCreate(plan,out manifest))return false;
        return TryRead(new XElement("pendingGearTransfer", new XAttribute("version", 1),
            new XAttribute("transactionId", transaction ?? ""), new XAttribute("creationId", creation ?? ""),
            new XAttribute("slot", slot ?? ""), new XAttribute("revision", revision),
            RebirthGearInventoryPlanCodec.Write(plan),manifest?.ToXml()), out state);
    }

    public static bool TryRead(XElement xml, out RebirthGearTransferState state)
    {
        state = null;
        if (xml == null || xml.Name != "pendingGearTransfer" || xml.Attributes().Count() != 5
            || ((string)xml.Attribute("version") != "1" && (string)xml.Attribute("version") != "2") || xml.Elements("gearPlan").Count()!=1
            || xml.Elements("recoveryManifest").Count()>1 || xml.Elements("recoveryAttempts").Count()>1 || xml.Elements("recoveryReceipts").Count()>1
            || xml.Elements("originalRequest").Count()>1
            || xml.Elements().Any(e=>e.Name!="originalRequest"&&e.Name!="gearPlan"&&e.Name!="recoveryManifest"&&e.Name!="recoveryAttempts"&&e.Name!="recoveryReceipts")
            || xml.Element("gearPlan") == null
            || xml.Nodes().Any(n => !(n is XElement) && (!(n is XText) || !string.IsNullOrWhiteSpace(((XText)n).Value)))) return false;
        var binding=xml.Element("originalRequest");string preparationDigest=null;
        if((string)xml.Attribute("version")=="2")
        {
            if(binding==null||binding.Attributes().Count()!=2||(string)binding.Attribute("version")!="1"||binding.Nodes().Any()||
                !ValidRequestDigest((string)binding.Attribute("digest")))return false;
            preparationDigest=(string)binding.Attribute("digest");
        }
        else if(binding!=null)return false; // Old unbound format cannot carry hidden intent.
        Guid transaction;
        string creation;
        long revision;
        string slot = (string)xml.Attribute("slot");
        if (!Guid.TryParse((string)xml.Attribute("transactionId"), out transaction) || transaction == Guid.Empty
            || !RebirthSurvivorRequestScope.TryNormalize((string)xml.Attribute("creationId"), out creation)
            || !long.TryParse((string)xml.Attribute("revision"), NumberStyles.Integer, CultureInfo.InvariantCulture, out revision)
            || revision < 0 || revision == long.MaxValue
            || (slot != "backpack" && slot != "belt" && slot != "support" && slot != "walkman")) return false;
        RebirthGearInventoryPlan plan;
        if (!RebirthGearInventoryPlanCodec.TryRead(xml.Element("gearPlan"), out plan)
            || (plan.GearBefore.Count == 0 && plan.GearAfter.Count == 0)) return false;
        var manifestXml=xml.Element("recoveryManifest");
        RebirthGearRecoveryManifest manifest=null;
        if(manifestXml!=null&&!RebirthGearRecoveryManifest.TryRead(manifestXml,plan,out manifest))return false;
        var attemptsXml=xml.Element("recoveryAttempts");
        if(!RebirthGearRecoveryAttempt.ValidateSet(attemptsXml,manifest))return false;
        state = new RebirthGearTransferState(transaction.ToString("N"), creation, slot, revision,
            RebirthGearInventoryPlanCodec.Write(plan),manifestXml,attemptsXml,xml.Element("recoveryReceipts"),preparationDigest);
        var receiptsXml=xml.Element("recoveryReceipts");
        if(receiptsXml!=null)
        {
            if(receiptsXml.Attributes().Count()!=1||(string)receiptsXml.Attribute("version")!="1"||receiptsXml.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))){state=null;return false;}
            var receipts=receiptsXml.Elements().Take(190).ToArray();var seen=new System.Collections.Generic.HashSet<Guid>();
            foreach(var receipt in receipts){Guid id;if(receipt.Name!="publication"||receipt.Attributes().Count()!=1||receipt.Nodes().Any()||!Guid.TryParseExact((string)receipt.Attribute("id"),"N",out id)||(string)receipt.Attribute("id")!=id.ToString("N")||!seen.Add(id)||!state.TryGetRecoveryAttempt(id,out var attempt)||attempt.OriginalOwnerEntityId<=0){state=null;return false;}}
            if(receipts.Length==0||receipts.Length>189){state=null;return false;}
        }
        return true;
    }
}

public enum RebirthGearTransferPhase { Prepared, OwnerApplied, GearCommitted }
