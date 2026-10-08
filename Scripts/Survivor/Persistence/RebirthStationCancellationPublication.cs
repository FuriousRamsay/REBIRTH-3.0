using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Exact cancelled native publication evidence only; never evidence of refund delivery.
internal sealed class RebirthStationCancellationPublication
{
    private readonly XElement image;
    private static readonly string[] Spans={"input","queue","output","completion"};
    private RebirthStationCancellationPublication(XElement node){image=new XElement(node);}
    internal string JobId=>(string)image.Attribute("job");
    internal XElement Write()=>new XElement(image);
    internal RebirthStationCancellationPublication Clone()=>new RebirthStationCancellationPublication(image);
    private static string Digest(byte[] bytes)=>RebirthStationPublicationRecord.Digest(bytes);
    private static string Binding(RebirthStationCancellationAttempt attempt)=>Digest(Encoding.UTF8.GetBytes(attempt.Write().ToString(SaveOptions.DisableFormatting)));
    private static bool IsDigest(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F');
    internal static bool TryCreate(string saveRoot,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,RebirthStationCancellationExpectation expectation,
        RebirthStationSnapshotEvidence.Publication proof,out RebirthStationCancellationPublication result)
    {
        result=null;
        try
        {
            if(proof==null||expectation==null||!RebirthStationCancellationAttempt.TryRead(attempt?.Write(),admission,intent,refund,out _))return false;
            string root=Root(saveRoot),path=Path.GetFullPath(proof.Path);
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
            var node=new XElement("stationCancellationPublication",new XAttribute("version",1),new XAttribute("phase","nativeCancelledPublished"),
                new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),new XAttribute("attempt",Binding(attempt)),
                new XAttribute("region",path.Substring(root.Length).Replace('\\','/')),new XAttribute("payload",proof.PayloadDigest),
                new XAttribute("chunkX",proof.ChunkX),new XAttribute("chunkZ",proof.ChunkZ));
            int[] starts={proof.InputStart,proof.QueueStart,proof.OutputStart,proof.CompletionStart};
            int[] lengths={proof.InputLength,proof.QueueLength,proof.OutputLength,proof.CompletionLength};
            for(int i=0;i<4;i++){node.Add(new XAttribute(Spans[i]+"Start",starts[i]));node.Add(new XAttribute(Spans[i]+"Length",lengths[i]));}
            if(!ValidSpans(node)||!TryReadSpans(saveRoot,node,out var bytes)||
                !expectation.MatchesStation(bytes[0],bytes[1])||!expectation.MatchesTerminal(bytes[2],bytes[3]))return false;
            for(int i=0;i<4;i++)node.Add(new XAttribute(Spans[i]+"Digest",Digest(bytes[i])));
            return TryRead(node,admission,intent,refund,attempt,out result);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,out RebirthStationCancellationPublication result)
    {
        result=null;
        try
        {
            if(node==null||admission==null||node.Name!="stationCancellationPublication"||node.HasElements||node.Attributes().Count()!=21||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="nativeCancelledPublished"||
                !RebirthStationCancellationAttempt.TryRead(attempt?.Write(),admission,intent,refund,out _)||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("attempt")!=Binding(attempt)||!IsDigest((string)node.Attribute("payload"))||!ValidSpans(node))return false;
            string region=(string)node.Attribute("region");
            if(string.IsNullOrEmpty(region)||region.Length>512||Path.IsPathRooted(region)||region.Contains(":")||
                region.Split('/','\\').Any(p=>p==".."||p=="."||p.Length==0))return false;
            var a=admission.Write();
            if((int)node.Attribute("chunkX")!=((int)a.Attribute("x")>>4)||(int)node.Attribute("chunkZ")!=((int)a.Attribute("z")>>4)||
                Spans.Any(s=>!IsDigest((string)node.Attribute(s+"Digest"))))return false;
            result=new RebirthStationCancellationPublication(node);return true;
        }
        catch{return false;}
    }
    private static bool ValidSpans(XElement node)
    {
        for(int i=0;i<4;i++)
        {
            long start=(int)node.Attribute(Spans[i]+"Start"),length=(int)node.Attribute(Spans[i]+"Length");
            if(start<8||length<1||length>256*1024||start+length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return false;
            for(int j=0;j<i;j++)
            {long other=(int)node.Attribute(Spans[j]+"Start"),size=(int)node.Attribute(Spans[j]+"Length");if(!(start+length<=other||other+size<=start))return false;}
        }
        return true;
    }
    private static string Root(string saveRoot)
    {if(string.IsNullOrEmpty(saveRoot))throw new InvalidDataException("Missing save root");return Path.GetFullPath(saveRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;}
    private static bool TryReadSpans(string saveRoot,XElement node,out byte[][] spans)
    {
        spans=null;string root=Root(saveRoot),path=Path.GetFullPath(Path.Combine(root,((string)node.Attribute("region")).Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
        byte[] payload;using(var stream=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            if(!RebirthStationRegionPayload.TryRead(stream,(int)node.Attribute("chunkX")&31,(int)node.Attribute("chunkZ")&31,out payload))return false;
        if(Digest(payload)!=(string)node.Attribute("payload")||!RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var decoded))return false;
        spans=new byte[4][];
        for(int i=0;i<4;i++)
        {
            int start=(int)node.Attribute(Spans[i]+"Start")-8,length=(int)node.Attribute(Spans[i]+"Length");
            if(start<0||length<1||(long)start+length>decoded.Length)return false;
            spans[i]=new byte[length];Buffer.BlockCopy(decoded,start,spans[i],0,length);
        }
        return true;
    }
    internal bool Revalidate(string saveRoot,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt)
    {
        try
        {
            if(!TryRead(image,admission,intent,refund,attempt,out _)||!TryReadSpans(saveRoot,image,out var bytes))return false;
            for(int i=0;i<4;i++)if(Digest(bytes[i])!=(string)image.Attribute(Spans[i]+"Digest"))return false;
            return true;
        }
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthStationCancellationPublication> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationCancellationRefund> refunds,IDictionary<string,RebirthStationCancellationAttempt> attempts)
    {
        if(records==null||admissions==null||intents==null||refunds==null||attempts==null||records.Count>64)throw new InvalidDataException("Invalid cancelled publications");
        var section=new XElement("stationCancellationPublications",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||
                !intents.TryGetValue(pair.Key,out var i)||!refunds.TryGetValue(pair.Key,out var r)||!attempts.TryGetValue(pair.Key,out var t)||
                !TryRead(pair.Value.image,a,i,r,t,out var valid))throw new InvalidDataException("Invalid cancelled publication binding");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationCancellationRefund> refunds,
        IDictionary<string,RebirthStationCancellationAttempt> attempts,out Dictionary<string,RebirthStationCancellationPublication> records)
    {
        records=new Dictionary<string,RebirthStationCancellationPublication>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("stationCancellationPublications").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||
                    !intents.TryGetValue(job,out var i)||!refunds.TryGetValue(job,out var r)||!attempts.TryGetValue(job,out var t)||
                    !TryRead(node,a,i,r,t,out var record))return false;
                records.Add(job,record);
            }
            return true;
        }
        catch{return false;}
    }
}