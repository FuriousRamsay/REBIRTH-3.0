using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
internal sealed class MixedNativePublicationCandidate
{
    private static bool Bound(TypedMixedWatchExpectation expected,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationPublicationRecord queued)
    {
        try {
            var original=expected?.OriginalEvent?.Original;
            return original!=null&&expected.NativeExpectation!=null&&expected.NativeProjection!=null&&ValidOriginals(admission,intent,queued)&&
                !original.Progression.StationCompletionPublications.ContainsKey(admission.JobId)&&!original.Progression.StationCompletionExpectationProjections.ContainsKey(admission.JobId)&&XNode.DeepEquals(admission.Write(),original.Admission.Write())&&XNode.DeepEquals(queued.Write(),original.Queued.Write())&&
                original.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var retained)&&ReferenceEquals(retained,intent)&&
                (int)intent.Write().Attribute("actor")==original.Actor&&expected.MatchesLive()&&original.IsCurrent();
        }catch{return false;}
    }
    private readonly XElement image;
    private static readonly string[] Spans={"input","queue","output","completion"};
    private MixedNativePublicationCandidate(XElement node){image=new XElement(node);}
    internal string JobId=>(string)image.Attribute("job");
    internal XElement Write()=>new XElement(image);
    internal MixedNativePublicationCandidate Clone()=>new MixedNativePublicationCandidate(image);
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
        RebirthStationPublicationRecord queuedPublication,TypedMixedWatchExpectation expectation,
        RebirthStationSnapshotEvidence.Publication proof,out MixedNativePublicationCandidate result)
    {
        result=null;
        try
        {
            if(proof==null||expectation==null||!Bound(expectation,admission,intent,queuedPublication)||!string.Equals(Path.GetFullPath(saveRoot),expectation.OriginalEvent.Original.SaveRoot,RebirthStationCompletionOriginalScope.PathComparison))return false;
            string root=Root(saveRoot),path=Path.GetFullPath(proof.Path);
            if(!path.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))return false;
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
                !expectation.NativeProjection.MatchesCurrentAndNative(bytes)||!Bound(expectation,admission,intent,queuedPublication))return false;
            for(int i=0;i<4;i++)node.Add(new XAttribute(Spans[i]+"Digest",Digest(bytes[i])));
            return TryRead(node,admission,intent,queuedPublication,out result);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queuedPublication,out MixedNativePublicationCandidate result)
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
            result=new MixedNativePublicationCandidate(node);return true;
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
        if(!path.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))return false;
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
        RebirthStationPublicationRecord queuedPublication,TypedMixedWatchExpectation expectation)
    {
        try{
            if(expectation==null||!Bound(expectation,admission,intent,queuedPublication)||!string.Equals(Path.GetFullPath(saveRoot),expectation.OriginalEvent.Original.SaveRoot,RebirthStationCompletionOriginalScope.PathComparison)||
                !TryRead(image,admission,intent,queuedPublication,out _)||!TryReadSpans(saveRoot,image,out var bytes))return false;
            for(int i=0;i<4;i++)if(Digest(bytes[i])!=(string)image.Attribute(Spans[i]+"Digest"))return false;
            return expectation.NativeProjection.MatchesCurrentAndNative(bytes)&&Bound(expectation,admission,intent,queuedPublication);
        }
        catch{return false;}
    }
    internal bool TryCopyValidated(string root,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationPublicationRecord queued,TypedMixedWatchExpectation expectation,out byte[][] spans)
    {
        spans=null;try{if(!Revalidate(root,admission,intent,queued,expectation)||!TryReadSpans(root,image,out var native)||!expectation.NativeProjection.MatchesCurrentAndNative(native)||!Bound(expectation,admission,intent,queued))return false;spans=native.Select(b=>(byte[])b.Clone()).ToArray();return true;}catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,MixedNativePublicationCandidate> records,
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
        out Dictionary<string,MixedNativePublicationCandidate> records)
    {
        records=null;var parsed=new Dictionary<string,MixedNativePublicationCandidate>(StringComparer.Ordinal);
        try{
            if(progression==null||admissions==null||intents==null||queuedPublications==null)return false;
            var sections=progression.Elements("stationCompletionPublications").ToArray();if(sections.Length==0){records=parsed;return true;}
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements()){
                string job=(string)node.Attribute("job");
                if(parsed.Count>=64||job==null||parsed.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||
                    !intents.TryGetValue(job,out var i)||!queuedPublications.TryGetValue(job,out var q)||
                    !TryRead(node,a,i,q,out var record))return false;
                parsed.Add(job,record);}
            records=parsed;return true;
        }
        catch{return false;}
    }
}

