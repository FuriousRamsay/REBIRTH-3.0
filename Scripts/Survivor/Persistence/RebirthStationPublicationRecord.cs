using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Durable phase: nativeQueuedPublished. It never means output delivered or XP awarded.
public sealed class RebirthStationPublicationRecord
{
    private readonly XElement image;
    public string JobId => (string)image.Attribute("job");
    private RebirthStationPublicationRecord(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthStationPublicationRecord Clone()=>new RebirthStationPublicationRecord(image);
    public static string Digest(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    private static string AdmissionDigest(RebirthStationGridAdmission a)=>Digest(Encoding.UTF8.GetBytes(a.Write().ToString(SaveOptions.DisableFormatting)));
    public static bool TryCreate(string saveRoot,RebirthStationGridAdmission admission,int queuedOwner,
        RebirthStationSnapshotEvidence.Publication proof,out RebirthStationPublicationRecord result)
    {
        result=null;if(admission==null||proof==null||string.IsNullOrEmpty(saveRoot))return false;
        try{string root=Path.GetFullPath(saveRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(proof.Path);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
            var node=new XElement("stationPublication",new XAttribute("version",1),new XAttribute("phase","nativeQueuedPublished"),
                new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),new XAttribute("admission",AdmissionDigest(admission)),
                new XAttribute("region",path.Substring(root.Length).Replace('\\','/')),new XAttribute("payload",proof.PayloadDigest),
                new XAttribute("chunkX",proof.ChunkX),new XAttribute("chunkZ",proof.ChunkZ),new XAttribute("inputStart",proof.InputStart),
                new XAttribute("inputLength",proof.InputLength),new XAttribute("queueStart",proof.QueueStart),new XAttribute("queueLength",proof.QueueLength),new XAttribute("queuedOwner",queuedOwner));
            return TryRead(node,admission,out result);
        }catch{return false;}
    }
    public static bool TryRead(XElement node,RebirthStationGridAdmission admission,out RebirthStationPublicationRecord result)
    {
        result=null;if(node==null||admission==null||node.Name!="stationPublication"||node.HasElements||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||node.Attributes().Count()!=14||(string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="nativeQueuedPublished"||
            (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
            (string)node.Attribute("admission")!=AdmissionDigest(admission))return false;
        try{
            string region=(string)node.Attribute("region"),digest=(string)node.Attribute("payload");
            if(string.IsNullOrEmpty(region)||region.Length>512||Path.IsPathRooted(region)||region.Contains(":")||region.Split('/','\\').Any(p=>p==".."||p=="."||p.Length==0)||
                digest==null||digest.Length!=64||digest.Any(c=>!Uri.IsHexDigit(c)))return false;
            int x=(int)node.Attribute("chunkX"),z=(int)node.Attribute("chunkZ");var a=admission.Write();
            if(x!=((int)a.Attribute("x")>>4)||z!=((int)a.Attribute("z")>>4)||(int)node.Attribute("queuedOwner")<=0)return false;
            int input=(int)node.Attribute("inputStart"),inputLength=(int)node.Attribute("inputLength"),queue=(int)node.Attribute("queueStart"),queueLength=(int)node.Attribute("queueLength");
            if(input<8||queue<8||inputLength<1||queueLength<1||inputLength>256*1024||queueLength>256*1024||
                (long)input+inputLength>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||(long)queue+queueLength>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||
                !((long)input+inputLength<=queue||(long)queue+queueLength<=input))return false;
            result=new RebirthStationPublicationRecord(node);return true;
        }catch{return false;}
    }
    // Recovery checks exact previously published compressed bytes before invoking native codecs.
    // A later legitimate region rewrite defers recovery; it never authorizes refund or replay.
    public bool Revalidate(string saveRoot,RebirthStationGridAdmission admission)
    {
        if(!TryRead(image,admission,out var ignored))return false;
        try{string root=Path.GetFullPath(saveRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,((string)image.Attribute("region")).Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
            byte[] payload;using(var stream=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                if(!RebirthStationRegionPayload.TryRead(stream,(int)image.Attribute("chunkX")&31,(int)image.Attribute("chunkZ")&31,out payload))return false;
            if(Digest(payload)!=(string)image.Attribute("payload")||!RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var decoded))return false;
            int input=(int)image.Attribute("inputStart")-8,il=(int)image.Attribute("inputLength"),queue=(int)image.Attribute("queueStart")-8,ql=(int)image.Attribute("queueLength");
            if((long)input+il>decoded.Length||(long)queue+ql>decoded.Length)return false;
            var inputBytes=new byte[il];var queueBytes=new byte[ql];Buffer.BlockCopy(decoded,input,inputBytes,0,il);Buffer.BlockCopy(decoded,queue,queueBytes,0,ql);
            var a=admission.Write();return RebirthStationSerializedContents.Matches(inputBytes,queueBytes,admission,admission.CreationId,
                (int)a.Attribute("x"),(int)a.Attribute("y"),(int)a.Attribute("z"),(string)a.Attribute("block"),(int)image.Attribute("queuedOwner"));
        }catch{return false;}
    }
    public static XElement WriteAll(IDictionary<string,RebirthStationPublicationRecord> records,IDictionary<string,RebirthStationGridAdmission> admissions)
    {
        if(records==null||records.Count>64)throw new InvalidDataException("Invalid station publication count");
        var section=new XElement("stationPublications",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal)){
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||!TryRead(pair.Value.image,a,out var valid))throw new InvalidDataException("Invalid station publication binding");
            section.Add(pair.Value.Write());}
        return section;
    }
    public static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,out Dictionary<string,RebirthStationPublicationRecord> records)
    {
        records=new Dictionary<string,RebirthStationPublicationRecord>(StringComparer.Ordinal);var sections=progression.Elements("stationPublications").ToArray();
        if(sections.Length==0)return true;if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1")return false;
        if(sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        foreach(var node in sections[0].Elements()){
            string job=(string)node.Attribute("job");if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!TryRead(node,a,out var record))return false;
            records.Add(job,record);}
        return true;
    }
}