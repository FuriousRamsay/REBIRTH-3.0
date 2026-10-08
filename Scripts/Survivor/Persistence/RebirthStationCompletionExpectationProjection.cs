using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Durable frozen native wire. Capture requires the retained authentic event; cold read is not grant authority.
internal sealed class RebirthStationCompletionExpectationProjection
{
    internal const int MaximumRecords=64,MaximumSectionBytes=8*1024*1024;
    private const int MaximumSpanBytes=256*1024,MaximumSpanText=4*((MaximumSpanBytes+2)/3);
    private static readonly string[] Names={"input","queue","output","completion"};
    private readonly XElement image;
    private readonly byte[][] spans;
    private RebirthStationCompletionExpectationProjection(XElement node,byte[][] bytes)
    {image=new XElement(node);spans=bytes.Select(b=>(byte[])b.Clone()).ToArray();}
    internal string JobId=>(string)image.Attribute("job");
    internal XElement Write()=>new XElement(image);
    internal RebirthStationCompletionExpectationProjection Clone()=>new RebirthStationCompletionExpectationProjection(image,spans);
    private static string Binding(XElement node)=>RebirthStationCompletionPublication.Binding(node);
    internal static bool TryCapture(RebirthStationCompletionCapture.SuccessfulOutput completed,
        RebirthStationCompletionExpectation originalExpectation,RebirthStationTerminalIntent intent,
        RebirthStationCompletionPublication publication,RebirthStationSnapshotEvidence.Publication proof,
        out RebirthStationCompletionExpectationProjection result)
    {
        result=null;
        try
        {
            if(completed==null||originalExpectation==null||publication==null||proof==null||!completed.MatchesReceipt())return false;
            var original=completed.Original;
            if(original==null||!original.IsCurrent()||!originalExpectation.IsBound(original.Admission,intent,original.Queued)||
                !originalExpectation.MatchesLive(original.Station)||
                !RebirthStationCompletionPublication.TryCreate(original.SaveRoot,original.Admission,intent,original.Queued,
                    originalExpectation,proof,out var fromProof)||!XNode.DeepEquals(fromProof.Write(),publication.Write())||
                !publication.Revalidate(original.SaveRoot,original.Admission,intent,original.Queued,originalExpectation)||
                !ReadNative(original.SaveRoot,publication.Write(),out var bytes)||
                !originalExpectation.MatchesStation(bytes[0],bytes[1])||!originalExpectation.MatchesTerminal(bytes[2],bytes[3]))return false;
            var node=new XElement("stationCompletionExpectationProjection",new XAttribute("version",1),
                new XAttribute("job",original.Admission.JobId),new XAttribute("creation",original.Admission.CreationId),
                new XAttribute("admission",Binding(original.Admission.Write())),new XAttribute("intent",Binding(intent.Write())),
                new XAttribute("queued",Binding(original.Queued.Write())),new XAttribute("definition",original.Admission.DefinitionId),
                new XAttribute("completed",Binding(publication.Write())));
            for(int i=0;i<Names.Length;i++)node.Add(new XElement(Names[i],Convert.ToBase64String(bytes[i])));
            if(!TryRead(node,original.Admission,intent,original.Queued,publication,XUiM_Recipes.GetRecipes(),out var captured)||
                !original.IsCurrent()||!completed.MatchesReceipt()||!originalExpectation.MatchesLive(original.Station)||!original.IsCurrent())return false;
            result=captured;return true;
        }
        catch{return false;}
    }
    private static bool Preparse(XElement node)
    {
        if(node==null||node.Name!="stationCompletionExpectationProjection"||node.Attributes().Count()!=8||
            (string)node.Attribute("version")!="1"||node.Elements().Count()!=4||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        foreach(var attr in node.Attributes())if(attr.Value.Length>256)return false;
        for(int i=0;i<Names.Length;i++)
        {
            var children=node.Elements(Names[i]).ToArray();
            if(children.Length!=1||children[0].HasAttributes||children[0].HasElements||
                children[0].Nodes().Any(n=>!(n is XText))||children[0].Value.Length<4||children[0].Value.Length>MaximumSpanText)return false;
        }
        return true;
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,IList<Recipe> definitions,
        out RebirthStationCompletionExpectationProjection result)
    {
        result=null;
        try
        {
            if(!Preparse(node)||!RebirthStationCompletionPublication.ValidOriginals(admission,intent,queued)||completed==null||
                !RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _)||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("admission")!=Binding(admission.Write())||(string)node.Attribute("intent")!=Binding(intent.Write())||
                (string)node.Attribute("queued")!=Binding(queued.Write())||(string)node.Attribute("definition")!=admission.DefinitionId||
                (string)node.Attribute("completed")!=Binding(completed.Write()))return false;
            var bytes=new byte[4][];var published=completed.Write();
            for(int i=0;i<Names.Length;i++)
            {
                string encoded=node.Element(Names[i]).Value;bytes[i]=Convert.FromBase64String(encoded);
                if(bytes[i].Length<1||bytes[i].Length>MaximumSpanBytes||Convert.ToBase64String(bytes[i])!=encoded||
                    bytes[i].Length!=(int)published.Attribute(Names[i]+"Length")||
                    RebirthStationPublicationRecord.Digest(bytes[i])!=(string)published.Attribute(Names[i]+"Digest"))return false;
            }
            if(!Semantic(bytes,admission,intent,definitions))return false;
            result=new RebirthStationCompletionExpectationProjection(node,bytes);return true;
        }
        catch{return false;}
    }
    internal static bool TryReadStored(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,
        out RebirthStationCompletionExpectationProjection result)
    {
        result=null;
        try
        {
            if(!Preparse(node)||!RebirthStationCompletionPublication.ValidOriginals(admission,intent,queued)||completed==null||
                !RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _)||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("admission")!=Binding(admission.Write())||(string)node.Attribute("intent")!=Binding(intent.Write())||
                (string)node.Attribute("queued")!=Binding(queued.Write())||(string)node.Attribute("definition")!=admission.DefinitionId||
                (string)node.Attribute("completed")!=Binding(completed.Write()))return false;
            var bytes=new byte[4][];var published=completed.Write();
            for(int i=0;i<Names.Length;i++)
            {
                string encoded=node.Element(Names[i]).Value;bytes[i]=Convert.FromBase64String(encoded);
                if(bytes[i].Length<1||bytes[i].Length>MaximumSpanBytes||Convert.ToBase64String(bytes[i])!=encoded||
                    bytes[i].Length!=(int)published.Attribute(Names[i]+"Length")||
                    RebirthStationPublicationRecord.Digest(bytes[i])!=(string)published.Attribute(Names[i]+"Digest"))return false;
            }
            result=new RebirthStationCompletionExpectationProjection(node,bytes);return true;
        }
        catch{return false;}
    }
    private static bool Semantic(byte[][] bytes,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,IList<Recipe> definitions)
    {
        if(!RebirthStationTerminalContents.TryReadOutput(bytes[0],bytes[0][0],out _)||
            !RebirthStationTerminalContents.TryReadOutput(bytes[2],bytes[2][0],out var output)||
            !RebirthNativeItemConformanceReader.TryDecodeStationQueue(bytes[1],out var queue)||
            !RebirthNativeItemConformanceReader.TryDecodeStationCompletions(bytes[3],out var receipts)||
            !admission.TryMaterialize(definitions,out var recipe,out _,out _))return false;
        foreach(var row in queue)
        {
            if(row.Recipe==null)continue;
            bool reserved=false;foreach(var fragment in row.Recipe.ingredients)
                if(fragment?.itemValue?.Metadata!=null)foreach(var key in fragment.itemValue.Metadata.Keys)
                    if(key.StartsWith(RebirthStationGridQueue.Prefix,StringComparison.Ordinal)){reserved=true;break;}
            if(reserved&&!RebirthStationGridQueue.IsMarked(row.Recipe))return false;
            if(RebirthStationGridQueue.IsMarked(row.Recipe))
            {
                if(!RebirthStationGridQueue.HasValidMultiplier(row.Recipe,row.Multiplier)||
                    !RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)||job==admission.JobId||
                    !RebirthStationGridQueue.TryGetDefinitionBinding(row.Recipe,definitions,out _))return false;
            }
            // Unmarked ordinary/repair/scrap neighbors may be natively adjusted or dynamically generated.
            // Canonical wire/current registry and original frozen bytes qualify them, not an invented authored identity.
        }
        CraftCompleteData expected=null;
        foreach(var row in receipts)
        {
            if(!RebirthStationCompletionReceipt.HasReservedMarker(row))continue;
            if(!RebirthStationCompletionReceipt.TryReadIdentity(row,out var identity))return false;
            if(identity.Job!=admission.JobId)continue;
            if(expected!=null||row.CrafterEntityID!=(int)intent.Write().Attribute("actor")||
                !RebirthStationCompletionReceipt.MatchesAdmission(admission,recipe,row))return false;
            expected=row;
        }
        return expected!=null&&RebirthStationTerminalContents.Matches(bytes[2],bytes[3],admission,recipe,expected,output);
    }
    private static bool ReadNative(string saveRoot,XElement published,out byte[][] bytes)
    {
        bytes=null;
        if(string.IsNullOrEmpty(saveRoot))return false;
        string root=Path.GetFullPath(saveRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        string path=Path.GetFullPath(Path.Combine(root,((string)published.Attribute("region")).Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))return false;
        byte[] payload;
        using(var stream=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            if(!RebirthStationRegionPayload.TryRead(stream,(int)published.Attribute("chunkX")&31,(int)published.Attribute("chunkZ")&31,out payload))return false;
        if(RebirthStationPublicationRecord.Digest(payload)!=(string)published.Attribute("payload")||
            !RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var decoded))return false;
        var spans=new byte[4][];
        for(int i=0;i<Names.Length;i++)
        {
            int start=(int)published.Attribute(Names[i]+"Start")-8,length=(int)published.Attribute(Names[i]+"Length");
            if(start<0||length<1||length>MaximumSpanBytes||(long)start+length>decoded.Length)return false;
            spans[i]=new byte[length];Buffer.BlockCopy(decoded,start,spans[i],0,length);
            if(RebirthStationPublicationRecord.Digest(spans[i])!=(string)published.Attribute(Names[i]+"Digest"))return false;
        }
        bytes=spans;return true;
    }
    // Compares original frozen wire and current catalogue semantics; never reconstructs omitted live queue fields.
    internal bool CompareCold(string saveRoot,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,IList<Recipe> definitions)
    {
        try
        {
            if(!TryRead(image,admission,intent,queued,completed,definitions,out _)||!ReadNative(saveRoot,completed.Write(),out var bytes))return false;
            for(int i=0;i<Names.Length;i++)if(!spans[i].SequenceEqual(bytes[i]))return false;
            return Semantic(bytes,admission,intent,definitions);
        }
        catch{return false;}
    }
    private static bool SectionPreparse(XElement section)
    {
        if(section==null||section.Name!="stationCompletionExpectationProjections"||section.Attributes().Count()!=1||
            (string)section.Attribute("version")!="1"||section.Elements().Count()>MaximumRecords||
            section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        // Bound every payload before Base64 allocation, then the exact serialized UTF8 section globally.
        var jobs=new HashSet<string>(StringComparer.Ordinal);
        foreach(var node in section.Elements())if(!Preparse(node)||(string)node.Attribute("job")==null||
            !jobs.Add((string)node.Attribute("job")))return false;
        long text=0;foreach(var node in section.Elements())foreach(var child in node.Elements())
        {text+=child.Value.Length;if(text>MaximumSectionBytes)return false;}
        return Encoding.UTF8.GetByteCount(section.ToString(SaveOptions.DisableFormatting))<=MaximumSectionBytes;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationPublicationRecord> queued,
        IDictionary<string,RebirthStationCompletionPublication> completed,IList<Recipe> definitions,
        out Dictionary<string,RebirthStationCompletionExpectationProjection> result)
    {
        result=null;
        try
        {
            if(progression==null||admissions==null||intents==null||queued==null||completed==null)return false;
            var sections=progression.Elements("stationCompletionExpectationProjections").ToArray();
            var parsed=new Dictionary<string,RebirthStationCompletionExpectationProjection>(StringComparer.Ordinal);
            if(sections.Length==0){result=parsed;return true;}
            if(sections.Length!=1||!SectionPreparse(sections[0]))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(job==null||parsed.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!intents.TryGetValue(job,out var i)||
                    !queued.TryGetValue(job,out var q)||!completed.TryGetValue(job,out var c)||!TryRead(node,a,i,q,c,definitions,out var projection))return false;
                parsed.Add(job,projection);
            }
            result=parsed;return true;
        }
        catch{return false;}
    }
    internal static bool ReadAllStored(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationPublicationRecord> queued,
        IDictionary<string,RebirthStationCompletionPublication> completed,
        out Dictionary<string,RebirthStationCompletionExpectationProjection> result)
    {
        result=null;
        try
        {
            if(progression==null||admissions==null||intents==null||queued==null||completed==null)return false;
            var sections=progression.Elements("stationCompletionExpectationProjections").ToArray();
            var parsed=new Dictionary<string,RebirthStationCompletionExpectationProjection>(StringComparer.Ordinal);
            if(sections.Length==0){result=parsed;return true;}
            if(sections.Length!=1||!SectionPreparse(sections[0]))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(job==null||parsed.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!intents.TryGetValue(job,out var i)||
                    !queued.TryGetValue(job,out var q)||!completed.TryGetValue(job,out var c)||!TryReadStored(node,a,i,q,c,out var projection))return false;
                parsed.Add(job,projection);
            }
            result=parsed;return true;
        }
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthStationCompletionExpectationProjection> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationPublicationRecord> queued,IDictionary<string,RebirthStationCompletionPublication> completed,IList<Recipe> definitions)
    {
        if(records==null||records.Count>MaximumRecords)throw new InvalidDataException("Invalid completion projections");
        var section=new XElement("stationCompletionExpectationProjections",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {if(pair.Value==null||pair.Key!=pair.Value.JobId)throw new InvalidDataException("Invalid completion projection job");section.Add(pair.Value.Write());}
        var wrapper=new XElement("progression",section);
        if(!ReadAll(wrapper,admissions,intents,queued,completed,definitions,out _))throw new InvalidDataException("Invalid completion projection section");
        return section;
    }
    internal static XElement WriteAllStored(IDictionary<string,RebirthStationCompletionExpectationProjection> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationPublicationRecord> queued,IDictionary<string,RebirthStationCompletionPublication> completed)
    {
        if(records==null||records.Count>MaximumRecords)throw new InvalidDataException("Invalid completion projections");
        var section=new XElement("stationCompletionExpectationProjections",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {if(pair.Value==null||pair.Key!=pair.Value.JobId)throw new InvalidDataException("Invalid completion projection job");section.Add(pair.Value.Write());}
        var wrapper=new XElement("progression",section);
        if(!ReadAllStored(wrapper,admissions,intents,queued,completed,out _))throw new InvalidDataException("Invalid completion projection section");
        return section;
    }
}
