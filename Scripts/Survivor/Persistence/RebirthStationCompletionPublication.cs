using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Frozen post-completion state only. Caller authenticates the original native event, station and save.
internal sealed class RebirthStationCompletionExpectation
{
    private readonly RebirthStationNativeInputSnapshot input;
    private readonly RebirthStationNativeQueueSnapshot queue;
    private readonly RebirthStationTerminalSnapshot terminal;
    private readonly RebirthStationTerminalExpectation semantics;
    private readonly string admissionBinding,intentBinding,queuedBinding;
    private RebirthStationCompletionExpectation(RebirthStationNativeInputSnapshot i,RebirthStationNativeQueueSnapshot q,
        RebirthStationTerminalSnapshot t,RebirthStationTerminalExpectation s,string a,string intent,string published)
    {input=i;queue=q;terminal=t;semantics=s;admissionBinding=a;intentBinding=intent;queuedBinding=published;}
    internal static bool TryCreate(TileEntityWorkstation station,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,RebirthStationPublicationRecord queuedPublication,IList<Recipe> definitions,
        out RebirthStationCompletionExpectation expectation)
    {
        expectation=null;
        try
        {
            if(station==null||!RebirthStationCompletionPublication.ValidOriginals(admission,intent,queuedPublication)||
                station.Queue==null||station.CraftCompleteList==null)return false;
            foreach(var entry in station.Queue)
            {
                if(entry==null)return false;
                if(!RebirthStationGridQueue.IsMarked(entry.Recipe))continue;
                if(!RebirthStationGridQueue.TryGetJobId(entry.Recipe,out var job)||job==admission.JobId)return false;
            }
            CraftCompleteData receipt=null;
            foreach(var data in station.CraftCompleteList)
            {
                if(!RebirthStationCompletionReceipt.HasReservedMarker(data))continue;
                if(!RebirthStationCompletionReceipt.TryReadIdentity(data,out var identity))return false;
                if(identity.Job!=admission.JobId)continue;
                if(receipt!=null||data.CrafterEntityID!=(int)intent.Write().Attribute("actor"))return false;
                receipt=data;
            }
            if(receipt==null||!RebirthStationTerminalExpectation.TryCreate(admission,definitions,receipt,station.Output,out var semantic)||
                !RebirthStationNativeInputSnapshot.TryCapture(station.Input,out var inputs)||
                !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out var queued)||
                !RebirthStationTerminalSnapshot.TryCapture(station,out var contents))return false;
            expectation=new RebirthStationCompletionExpectation(inputs,queued,contents,semantic,
                RebirthStationCompletionPublication.Binding(admission.Write()),
                RebirthStationCompletionPublication.Binding(intent.Write()),
                RebirthStationCompletionPublication.Binding(queuedPublication.Write()));
            return true;
        }
        catch{return false;}
    }
    internal bool IsBound(RebirthStationGridAdmission a,RebirthStationTerminalIntent i,RebirthStationPublicationRecord q)
    {return RebirthStationCompletionPublication.ValidOriginals(a,i,q)&&admissionBinding==RebirthStationCompletionPublication.Binding(a.Write())&&
        intentBinding==RebirthStationCompletionPublication.Binding(i.Write())&&queuedBinding==RebirthStationCompletionPublication.Binding(q.Write());}
    internal bool MatchesLive(TileEntityWorkstation station)
    {try{return station!=null&&input.Matches(station.Input)&&queue.Matches(station.Queue)&&terminal.Matches(station);}catch{return false;}}
    internal bool MatchesStation(byte[] inputs,byte[] queued)
    {return input.MatchesSerializedInput(inputs)&&queue.MatchesSerializedQueue(queued);}
    internal bool MatchesTerminal(byte[] output,byte[] completion)
    {return terminal.MatchesSerializedTerminal(output,completion)&&semantics.Matches(output,completion);}
}

// Detached exact final-region completed publication. No payment/event/save/grant/installer activation.
internal sealed class RebirthStationCompletionPublication
{
    private readonly XElement image;
    private static readonly string[] Spans={"input","queue","output","completion"};
    private RebirthStationCompletionPublication(XElement node){image=new XElement(node);}
    internal string JobId=>(string)image.Attribute("job");
    internal XElement Write()=>new XElement(image);
    internal RebirthStationCompletionPublication Clone()=>new RebirthStationCompletionPublication(image);
    private static string Digest(byte[] bytes)=>RebirthStationPublicationRecord.Digest(bytes);
    internal static string Binding(XElement node)=>Digest(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)));
    private static bool IsDigest(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F');
    internal static bool ValidOriginals(RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationPublicationRecord queued)
    {
        try{return admission!=null&&intent!=null&&intent.IsCompletion&&queued!=null&&
            RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)&&
            RebirthStationPublicationRecord.TryRead(queued.Write(),admission,out _)&&
            (int)queued.Write().Attribute("queuedOwner")==(int)intent.Write().Attribute("actor")&&IsDigest(admission.DefinitionId);}
        catch{return false;}
    }
    internal static bool TryCreate(string saveRoot,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queuedPublication,RebirthStationCompletionExpectation expectation,
        RebirthStationSnapshotEvidence.Publication proof,out RebirthStationCompletionPublication result)
    {
        result=null;
        try
        {
            if(proof==null||expectation==null||!expectation.IsBound(admission,intent,queuedPublication))return false;
            string root=Root(saveRoot),path=Path.GetFullPath(proof.Path);
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))return false;
            var node=new XElement("stationCompletionPublication",new XAttribute("version",1),new XAttribute("phase","nativeCompletedPublished"),
                new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),
                new XAttribute("admission",Binding(admission.Write())),new XAttribute("intent",Binding(intent.Write())),
                new XAttribute("queued",Binding(queuedPublication.Write())),new XAttribute("definition",admission.DefinitionId),
                new XAttribute("region",path.Substring(root.Length).Replace('\\','/')),new XAttribute("payload",proof.PayloadDigest),
                new XAttribute("chunkX",proof.ChunkX),new XAttribute("chunkZ",proof.ChunkZ));
            int[] starts={proof.InputStart,proof.QueueStart,proof.OutputStart,proof.CompletionStart};
            int[] lengths={proof.InputLength,proof.QueueLength,proof.OutputLength,proof.CompletionLength};
            for(int i=0;i<4;i++){node.Add(new XAttribute(Spans[i]+"Start",starts[i]));node.Add(new XAttribute(Spans[i]+"Length",lengths[i]));}
            if(!ValidSpans(node)||!TryReadSpans(saveRoot,node,out var bytes)||
                !expectation.MatchesStation(bytes[0],bytes[1])||!expectation.MatchesTerminal(bytes[2],bytes[3]))return false;
            for(int i=0;i<4;i++)node.Add(new XAttribute(Spans[i]+"Digest",Digest(bytes[i])));
            return TryRead(node,admission,intent,queuedPublication,out result);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queuedPublication,out RebirthStationCompletionPublication result)
    {
        result=null;
        try
        {
            if(node==null||node.Name!="stationCompletionPublication"||node.HasElements||node.Attributes().Count()!=24||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="nativeCompletedPublished"||
                !ValidOriginals(admission,intent,queuedPublication)||(string)node.Attribute("job")!=admission.JobId||
                (string)node.Attribute("creation")!=admission.CreationId||(string)node.Attribute("admission")!=Binding(admission.Write())||
                (string)node.Attribute("intent")!=Binding(intent.Write())||(string)node.Attribute("queued")!=Binding(queuedPublication.Write())||
                (string)node.Attribute("definition")!=admission.DefinitionId||!IsDigest((string)node.Attribute("payload"))||!ValidSpans(node))return false;
            string region=(string)node.Attribute("region");
            if(string.IsNullOrEmpty(region)||region.Length>512||Path.IsPathRooted(region)||region.Contains(":")||
                region.Split('/','\\').Any(p=>p==".."||p=="."||p.Length==0))return false;
            var a=admission.Write();
            if((int)node.Attribute("chunkX")!=((int)a.Attribute("x")>>4)||(int)node.Attribute("chunkZ")!=((int)a.Attribute("z")>>4)||
                Spans.Any(s=>!IsDigest((string)node.Attribute(s+"Digest"))))return false;
            result=new RebirthStationCompletionPublication(node);return true;
        }
        catch{return false;}
    }
    private static bool ValidSpans(XElement node)
    {
        for(int i=0;i<4;i++){
            long start=(int)node.Attribute(Spans[i]+"Start"),length=(int)node.Attribute(Spans[i]+"Length");
            if(start<8||length<1||length>256*1024||start+length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return false;
            for(int j=0;j<i;j++){long other=(int)node.Attribute(Spans[j]+"Start"),size=(int)node.Attribute(Spans[j]+"Length");if(!(start+length<=other||other+size<=start))return false;}
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
        for(int i=0;i<4;i++){int start=(int)node.Attribute(Spans[i]+"Start")-8,length=(int)node.Attribute(Spans[i]+"Length");
            if(start<0||length<1||(long)start+length>decoded.Length)return false;
            spans[i]=new byte[length];Buffer.BlockCopy(decoded,start,spans[i],0,length);}
        return true;
    }
    internal bool Revalidate(string saveRoot,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queuedPublication,RebirthStationCompletionExpectation expectation)
    {
        try{
            if(expectation==null||!expectation.IsBound(admission,intent,queuedPublication)||
                !TryRead(image,admission,intent,queuedPublication,out _)||!TryReadSpans(saveRoot,image,out var bytes))return false;
            for(int i=0;i<4;i++)if(Digest(bytes[i])!=(string)image.Attribute(Spans[i]+"Digest"))return false;
            return expectation.MatchesStation(bytes[0],bytes[1])&&expectation.MatchesTerminal(bytes[2],bytes[3]);
        }
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthStationCompletionPublication> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationPublicationRecord> queuedPublications)
    {
        if(records==null||admissions==null||intents==null||queuedPublications==null||records.Count>64)throw new InvalidDataException("Invalid completed publications");
        var section=new XElement("stationCompletionPublications",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal)){
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||
                !intents.TryGetValue(pair.Key,out var i)||!queuedPublications.TryGetValue(pair.Key,out var q)||
                !TryRead(pair.Value.image,a,i,q,out var valid))throw new InvalidDataException("Invalid completed publication binding");
            section.Add(valid.Write());}
        return section;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationPublicationRecord> queuedPublications,
        out Dictionary<string,RebirthStationCompletionPublication> records)
    {
        records=new Dictionary<string,RebirthStationCompletionPublication>(StringComparer.Ordinal);
        try{
            if(progression==null||admissions==null||intents==null||queuedPublications==null)return false;
            var sections=progression.Elements("stationCompletionPublications").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements()){
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||
                    !intents.TryGetValue(job,out var i)||!queuedPublications.TryGetValue(job,out var q)||
                    !TryRead(node,a,i,q,out var record))return false;
                records.Add(job,record);}
            return true;
        }
        catch{return false;}
    }
}

