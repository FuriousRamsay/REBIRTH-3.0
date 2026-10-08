using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal enum RebirthNpcStructuralEvidenceKind : byte
{ TargetPreimage=0, FullInputCustody=1, FullOutputCustody=2, NativeOutcome=3, OriginalDurableWitness=4 }

// Opaque producer-schema envelope, not a native/full-item decoder or proof validator.
// A valid payload may still lie or omit fields: no producer schema is qualified here.
internal sealed class RebirthNpcStructuralEvidence
{
    internal const int MaxBytes=65536;
    private readonly byte[] bytes;
    internal RebirthNpcStructuralEvidenceKind Kind { get; }
    internal string ProducerSchema { get; }
    internal uint ProducerVersion { get; }
    internal RebirthNpcStructuralOperationIdentity Original { get; }
    internal bool IsQualifiedProof=>false;
    internal int ByteLength=>bytes.Length;
    internal byte[] CopyBytes()=>(byte[])bytes.Clone();
    private RebirthNpcStructuralEvidence(RebirthNpcStructuralEvidenceKind kind,string schema,uint version,
        RebirthNpcStructuralOperationIdentity original,byte[] data)
    {Kind=kind;ProducerSchema=schema;ProducerVersion=version;Original=original;bytes=(byte[])data.Clone();}
    internal static bool TryCreate(RebirthNpcStructuralEvidenceKind kind,string schema,uint version,
        RebirthNpcStructuralOperationIdentity original,byte[] data,out RebirthNpcStructuralEvidence evidence)
    {
        evidence=null;
        if(!Enum.IsDefined(typeof(RebirthNpcStructuralEvidenceKind),kind)||!RebirthNpcStructuralOperationIdentity.Text(schema,128)||version==0||version>65535||
            original==null||data==null||data.Length==0||data.Length>MaxBytes)return false;
        evidence=new RebirthNpcStructuralEvidence(kind,schema,version,original,data);return true;
    }
    internal XElement Write()=>new XElement("structuralEvidence",new XAttribute("version",1),
        new XAttribute("kind",(byte)Kind),new XAttribute("schema",ProducerSchema),new XAttribute("producerVersion",ProducerVersion),
        new XAttribute("payload",Convert.ToBase64String(bytes)),Original.Write());
    internal static bool TryRead(XElement node,out RebirthNpcStructuralEvidence evidence)
    {
        evidence=null;
        if(node==null||node.Name!="structuralEvidence"||node.Attributes().Count()!=5||
            node.Attributes().Any(a=>a.Name.NamespaceName.Length!=0)||node.Elements().Count()!=1||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
            (string)node.Attribute("version")!="1"||
            !RebirthNpcStructuralOperationIdentity.U32((string)node.Attribute("kind"),out var kind)||kind>4||
            !RebirthNpcStructuralOperationIdentity.U32((string)node.Attribute("producerVersion"),out var version)||
            !RebirthNpcStructuralOperationIdentity.TryRead(node.Elements().Single(),out var original))return false;
        string payload=(string)node.Attribute("payload");
        if(string.IsNullOrEmpty(payload)||payload.Length>87384)return false;
        try { var data=Convert.FromBase64String(payload);if(Convert.ToBase64String(data)!=payload)return false;
            return TryCreate((RebirthNpcStructuralEvidenceKind)kind,(string)node.Attribute("schema"),version,original,data,out evidence); }
        catch(FormatException){return false;}
    }
}

// Immutable fragment candidate for existing aggregate/journal integration. No file or store.
// Phase labels describe unverified producer claims; they never release custody or credit work.
internal enum RebirthNpcStructuralJournalPhase : byte
{ Prepared=0, InputsReserved=1, NativeActionPending=2, OutcomeObserved=3, CustodyCommitted=4, Settled=5 }
internal sealed class RebirthNpcStructuralWorkJournalRecord
{
    internal const int MaxEvidence=5;
    internal const int MaxTotalBytes=262144;
    private readonly RebirthNpcStructuralEvidence[] evidence;
    internal RebirthNpcStructuralOperationIdentity Original { get; }
    internal RebirthNpcStructuralJournalPhase ClaimedPhase { get; }
    internal bool RequiresReconciliation=>true;
    internal bool CanAuthorizeStroke=>false;
    internal RebirthNpcStructuralAdmissionResult Admission=>RebirthNpcStructuralAdmissionResult.Inactive;
    internal RebirthNpcStructuralEvidence[] CopyEvidence()=>(RebirthNpcStructuralEvidence[])evidence.Clone();
    private RebirthNpcStructuralWorkJournalRecord(RebirthNpcStructuralOperationIdentity original,
        RebirthNpcStructuralJournalPhase phase,RebirthNpcStructuralEvidence[] records)
    {Original=original;ClaimedPhase=phase;evidence=(RebirthNpcStructuralEvidence[])records.Clone();}
    internal static bool TryCreate(RebirthNpcStructuralOperationIdentity original,RebirthNpcStructuralJournalPhase phase,
        RebirthNpcStructuralEvidence[] records,out RebirthNpcStructuralWorkJournalRecord record)
    {
        record=null;
        if(original==null||!Enum.IsDefined(typeof(RebirthNpcStructuralJournalPhase),phase)||records==null||records.Length>MaxEvidence)return false;
        var kinds=new HashSet<RebirthNpcStructuralEvidenceKind>();int total=0;
        foreach(var item in records){if(item==null||!original.Matches(item.Original)||!kinds.Add(item.Kind))return false;
            total+=item.ByteLength;if(total>MaxTotalBytes)return false;}
        record=new RebirthNpcStructuralWorkJournalRecord(original,phase,records.OrderBy(r=>(byte)r.Kind).ToArray());return true;
    }
    internal XElement Write()=>new XElement("structuralWorkJournal",new XAttribute("version",1),
        new XAttribute("phase",(byte)ClaimedPhase),Original.Write(),evidence.Select(e=>e.Write()));
    // A wholly absent optional component is readable legacy state, but yields no record/proof.
    internal static bool TryReadOptional(XElement node,out RebirthNpcStructuralWorkJournalRecord record)
    {
        record=null;if(node==null)return true;
        if(node.Name!="structuralWorkJournal"||node.Attributes().Count()!=2||
            node.Attributes().Any(a=>a.Name.NamespaceName.Length!=0)||(string)node.Attribute("version")!="1"||
            !RebirthNpcStructuralOperationIdentity.U32((string)node.Attribute("phase"),out var phase)||phase>5||
            node.Elements().Count()>MaxEvidence+1||node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        var children=node.Elements().ToArray();if(children.Length==0||
            !RebirthNpcStructuralOperationIdentity.TryRead(children[0],out var original))return false;
        var parsed=new List<RebirthNpcStructuralEvidence>();
        foreach(var child in children.Skip(1)){if(!RebirthNpcStructuralEvidence.TryRead(child,out var item))return false;parsed.Add(item);}
        return TryCreate(original,(RebirthNpcStructuralJournalPhase)phase,parsed.ToArray(),out record);
    }
}

