using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Globalization;

// Version two changes only this domain's disk representation. Public records
// remain the same fully validated original; native source/owner authority is
// unchanged. Artifacts precede stages and never independently grant effects.
internal static class RebirthRemoteResourceJournalFormat
{
    internal const uint StageMagic=0x324F5252;
    private const uint ArtifactMagic=0x32505252;
    internal const int MaximumPlan=64*1024*1024,MaximumStage=128*1024;
    internal const long MaximumArtifacts=144L*1024*1024;
    internal sealed class HistoricalReadBudget
    {
        internal readonly int PlanBytes,LegacyEnvelopeBytes;internal readonly long GraphWorkspace;
        internal HistoricalReadBudget(int planBytes,int legacyEnvelopeBytes,long graphWorkspace=0)
        {
            if(planBytes<1||planBytes>MaximumPlan||legacyEnvelopeBytes<planBytes||legacyEnvelopeBytes>64*1024*1024)throw new InvalidDataException();
            if(graphWorkspace<0||graphWorkspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)throw new InvalidDataException();
            PlanBytes=planBytes;LegacyEnvelopeBytes=legacyEnvelopeBytes;GraphWorkspace=graphWorkspace;
        }
    }
    private static readonly string[] RootStages={"phase","outcome","aborting","ownerProof","disposition","ownerRejecting","ownerNoUseProof"};
    private static readonly string[] SourceStages={"attempted","published","beforeProof","proof","restored","restoreProof","declined","refusalProof"};
    private static readonly string[] Fields={"removed","reason","ownerAction","ownerRefund"};
    private sealed class ReadSession:IDisposable
    {
        internal readonly ReadSession Previous;
        internal readonly System.Collections.Generic.Dictionary<string,XElement> Frames=new System.Collections.Generic.Dictionary<string,XElement>(StringComparer.Ordinal);
        internal readonly System.Collections.Generic.Dictionary<string,string> Bindings=new System.Collections.Generic.Dictionary<string,string>(StringComparer.Ordinal);
        internal readonly System.Collections.Generic.Dictionary<string,int> PlanBytes=new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
        internal readonly HistoricalReadBudget Budget;internal long RetainedGraph;
        internal readonly System.Collections.Generic.Dictionary<string,long> FrameGraphs=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
        internal ReadSession(HistoricalReadBudget budget){Previous=session;Budget=budget;session=this;}
        public void Dispose(){session=Previous;}
    }
    [ThreadStatic]private static ReadSession session;
    internal static IDisposable BeginRead(HistoricalReadBudget budget=null)=>new ReadSession(budget);
    internal static void ValidateHistoricalXmlAllocation(string text,int maximum,long rawBytes,HistoricalReadBudget budget)
    {
        if(budget==null||budget.GraphWorkspace<=0)return;
        long retained=session?.RetainedGraph??0,graph=0;
        if(checked(retained+4*rawBytes)>budget.GraphWorkspace)throw new InvalidDataException("Captured legacy frame workspace exceeded.");
        using(var input=new StringReader(text))using(var xml=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum}))
        {
            while(xml.Read())
            {
                if(xml.NodeType==XmlNodeType.Element)
                {
                    graph=checked(graph+256+2L*xml.Name.Length);
                    if(xml.MoveToFirstAttribute())do{graph=checked(graph+128+2L*(xml.Name.Length+xml.Value.Length));}while(xml.MoveToNextAttribute());xml.MoveToElement();
                }
                else if(xml.HasValue)graph=checked(graph+64+2L*xml.Value.Length);
                if(checked(retained+4*rawBytes+6*graph)>budget.GraphWorkspace)throw new InvalidDataException("Captured legacy XML workspace exceeded.");
            }
        }
    }
    private static FileStream OpenBounded(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||file.Length>MaximumPlan+128L)throw new InvalidDataException();
        var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length!=file.Length){stream.Dispose();throw new InvalidDataException();}return stream;
    }
    private static bool SameStreams(Stream left,Stream right)
    {
        if(left.Length!=right.Length)return false;
        var a=new byte[65536];var b=new byte[65536];long remaining=left.Length;
        while(remaining>0)
        {
            int count=(int)Math.Min(a.Length,remaining),readA=0,readB=0;
            while(readA<count){int n=left.Read(a,readA,count-readA);if(n==0)return false;readA+=n;}
            while(readB<count){int n=right.Read(b,readB,count-readB);if(n==0)return false;readB+=n;}
            for(int index=0;index<count;index++)if(a[index]!=b[index])return false;
            remaining-=count;
        }
        return true;
    }
    internal static bool SameFiles(string left,string right)
    {using(var a=OpenBounded(left))using(var b=OpenBounded(right))return SameStreams(a,b);}
    internal static bool MatchesFile(string path,byte[] bytes)
    {using(var a=OpenBounded(path))using(var b=new MemoryStream(bytes,false))return SameStreams(a,b);}
    private static string Text(XElement value)=>value.ToString(SaveOptions.DisableFormatting);
    private static bool Hex(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
    internal static int FieldLimit(string field)=>field=="ownerAction"||field=="ownerRefund"?24*1024*1024:field=="removed"?8*1024*1024+64:field=="reason"?4096:throw new InvalidDataException();
    private static string RootFor(string path,Guid operation)
    {
        string directory=Path.GetDirectoryName(path);
        if(Path.GetFileName(directory)==operation.ToString("N")&&Path.GetFileName(Path.GetDirectoryName(directory))=="completed")directory=Path.GetDirectoryName(Path.GetDirectoryName(directory));
        return directory;
    }
    private static byte[] Frame(uint magic,Guid operation,long revision,byte[] body)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(magic);writer.Write(operation.ToByteArray());writer.Write(revision);writer.Write(body.Length);writer.Write(body);writer.Flush();
            using(var hash=SHA256.Create())writer.Write(hash.ComputeHash(stream.ToArray()));writer.Flush();return stream.ToArray();
        }
    }
    private static XElement ReadFrame(string path,uint magic,Guid operation,long revision,int maximum,HistoricalReadBudget budget=null)
    {
        var readBudget=budget??session?.Budget;long retainedGraph=session?.RetainedGraph??0;
        string cacheKey=path+"|"+operation.ToString("N")+"|"+revision+"|"+maximum;
        if(magic==ArtifactMagic&&session!=null&&session.Frames.TryGetValue(cacheKey,out var cached))
        {
            if(readBudget?.GraphWorkspace>0&&checked(retainedGraph+6*session.FrameGraphs[cacheKey])>readBudget.GraphWorkspace)throw new InvalidDataException("Captured original graph workspace exceeded.");
            return new XElement(cached);
        }
        var info=new FileInfo(path);if(!info.Exists||(info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length<64||info.Length>maximum+128L)throw new InvalidDataException();
        if(readBudget?.GraphWorkspace>0&&checked(retainedGraph+4*info.Length)>readBudget.GraphWorkspace)throw new InvalidDataException("Captured original frame workspace exceeded.");
        byte[] bytes=File.ReadAllBytes(path);
        using(var hash=SHA256.Create())if(!hash.ComputeHash(bytes,0,bytes.Length-32).SequenceEqual(bytes.Skip(bytes.Length-32)))throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,0,bytes.Length-32,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(reader.ReadUInt32()!=magic||new Guid(reader.ReadBytes(16))!=operation||reader.ReadInt64()!=revision)throw new InvalidDataException();
            int count=reader.ReadInt32();if(count<1||count>maximum||count!=stream.Length-stream.Position)throw new InvalidDataException();
            string text=new UTF8Encoding(false,true).GetString(reader.ReadBytes(count));
            long graph=0;
            if(readBudget?.GraphWorkspace>0)
            {
                using(var scanInput=new StringReader(text))using(var scan=XmlReader.Create(scanInput,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum}))
                {
                    while(scan.Read())
                    {
                        if(scan.NodeType==XmlNodeType.Element)
                        {
                            graph=checked(graph+256+2L*scan.Name.Length);
                            if(scan.MoveToFirstAttribute())do{graph=checked(graph+128+2L*(scan.Name.Length+scan.Value.Length));}while(scan.MoveToNextAttribute());scan.MoveToElement();
                        }
                        else if(scan.HasValue)graph=checked(graph+64+2L*scan.Value.Length);
                        if(checked(retainedGraph+4*bytes.LongLength+6*graph)>readBudget.GraphWorkspace)throw new InvalidDataException("Captured original XML workspace exceeded.");
                    }
                }
            }
            using(var input=new StringReader(text))using(var xml=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum}))
            {var image=XElement.Load(xml,LoadOptions.PreserveWhitespace);if(Text(image)!=text)throw new InvalidDataException("Noncanonical original artifact.");if(magic==ArtifactMagic&&session!=null){session.Frames.Add(cacheKey,new XElement(image));session.FrameGraphs.Add(cacheKey,graph);session.RetainedGraph=checked(session.RetainedGraph+graph);}return image;}
        }
    }
    private static string ResolveArtifact(string stagePath,Guid operation,string name)
    {
        string root=RootFor(stagePath,operation),archive=Path.Combine(root,"completed",operation.ToString("N"));
        string primary=Path.Combine(root,name),completed=Path.Combine(archive,name);
        if(Directory.Exists(archive)&&(File.GetAttributes(archive)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        if(File.Exists(primary)&&File.Exists(completed)&&!SameFiles(primary,completed))throw new InvalidDataException("Conflicting original artifacts.");
        if(File.Exists(primary))return primary;if(File.Exists(completed))return completed;throw new InvalidDataException("Original artifact missing.");
    }
    private static string PlanName(Guid operation)=>operation.ToString("N")+".plan.rrp";
    private static string BlobName(Guid operation,string field,string digest)=>operation.ToString("N")+".blob."+field+"."+digest+".rrb";
    private static void PublishArtifact(string directory,string name,Guid operation,XElement image,int maximum,Func<bool> current,HistoricalReadBudget budget=null)
    {
        byte[] body=Encoding.UTF8.GetBytes(Text(image));if(body.Length>maximum||current?.Invoke()!=true)throw new InvalidDataException();
        byte[] bytes=Frame(ArtifactMagic,operation,-1,body);string path=Path.Combine(directory,name);
        if(image.Name=="ownerAction"||image.Name=="ownerRefund")
            foreach(string original in Directory.EnumerateFiles(directory,operation.ToString("N")+".blob."+image.Name.LocalName+".*"))
                if(!IsArtifact(Path.GetFileName(original),operation,out string canonical)||canonical!=name)throw new InvalidDataException("Immutable owner sub-intent already retained.");
        if(File.Exists(path)){if(!MatchesFile(path,bytes))throw new InvalidDataException();return;}
        // A failed artifact candidate is retained and can coexist only if its
        // canonical bytes exactly match the same immutable artifact.
        foreach(string candidate in Directory.EnumerateFiles(directory,name+".*.tmp"))if(!MatchesFile(candidate,bytes))throw new InvalidDataException();
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(current()!=true)throw new InvalidOperationException();File.Move(temporary,path);
        if(!XNode.DeepEquals(ReadFrame(path,ArtifactMagic,operation,-1,maximum,budget),image)||current()!=true)throw new InvalidDataException();
    }
    internal static byte[] Encode(RebirthRemoteResourceDurableJournal.Record record,string directory,Func<bool> current,bool publishArtifacts=true,HistoricalReadBudget budget=null)
    {
        var root=record.Copy();
        if(budget?.GraphWorkspace>0&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitHistoricalSourceImage(root,budget.GraphWorkspace,0))throw new InvalidDataException("Captured placed publication workspace exceeded.");
        RebirthRemoteResourceDurableJournal.ValidateImage(root);var plan=RebirthRemoteResourceDurableJournal.ImmutablePlan(root);
        if(publishArtifacts)PublishArtifact(directory,PlanName(record.Operation),record.Operation,plan,MaximumPlan,current,budget);
        var stage=new XElement("resourceStage",new XAttribute("version",2),new XAttribute("operation",record.Operation.ToString("N")),new XAttribute("binding",record.Binding));
        foreach(string name in RootStages)if(root.Attribute(name)!=null)stage.Add(new XAttribute(root.Attribute(name)));
        var sources=new XElement("sources");int index=0;
        foreach(var source in root.Element("sources").Elements("source"))
        {
            var value=new XElement("source",new XAttribute("index",index++));foreach(string name in SourceStages)if(source.Attribute(name)!=null)value.Add(new XAttribute(source.Attribute(name)));sources.Add(value);
        }
        stage.Add(sources);
        foreach(string field in Fields)
        {
            var image=root.Element(field);if(image==null)continue;string digest=RebirthRemoteResourceDurableJournal.Digest(Text(image));
            if(publishArtifacts)PublishArtifact(directory,BlobName(record.Operation,field,digest),record.Operation,image,FieldLimit(field),current,budget);
            stage.Add(new XElement("ref",new XAttribute("field",field),new XAttribute("digest",digest)));
        }
        byte[] body=Encoding.UTF8.GetBytes(Text(stage));if(body.Length>MaximumStage)throw new InvalidDataException("Original stage exceeds reserved bound.");
        return Frame(StageMagic,record.Operation,record.Revision,body);
    }
    internal static RebirthRemoteResourceDurableJournal.Record Read(string path,Guid operation,long revision,HistoricalReadBudget budget=null)
    {
        var stage=ReadFrame(path,StageMagic,operation,revision,MaximumStage,budget);
        if(stage.Name!="resourceStage"||(int?)stage.Attribute("version")!=2||(string)stage.Attribute("operation")!=operation.ToString("N")||!Hex((string)stage.Attribute("binding"))||
            stage.Attributes().Any(a=>a.Name.NamespaceName.Length!=0||!new[]{"version","operation","binding"}.Concat(RootStages).Contains(a.Name.LocalName))||
            stage.Nodes().Any(n=>n is not XElement)||stage.Elements().Any(e=>e.Name!="sources"&&e.Name!="ref")||stage.Elements("sources").Count()!=1)throw new InvalidDataException();
        var plan=ReadFrame(ResolveArtifact(path,operation,PlanName(operation)),ArtifactMagic,operation,-1,budget?.PlanBytes??MaximumPlan,budget);
        string bindingKey=RootFor(path,operation)+"|"+operation.ToString("N");
        string actualBinding;int planBytes;
        if(session==null||!session.Bindings.TryGetValue(bindingKey,out actualBinding))
        {
            if((string)plan.Attribute("operation")!=operation.ToString("N")||!XNode.DeepEquals(plan,RebirthRemoteResourceDurableJournal.ImmutablePlan(plan)))throw new InvalidDataException();
            string planText=Text(plan);actualBinding=RebirthRemoteResourceDurableJournal.Digest(planText);planBytes=Encoding.UTF8.GetByteCount(planText);
            if(session!=null){session.Bindings.Add(bindingKey,actualBinding);session.PlanBytes.Add(bindingKey,planBytes);}
        }
        else planBytes=session.PlanBytes[bindingKey];
        if(actualBinding!=(string)stage.Attribute("binding"))throw new InvalidDataException();
        var image=new XElement(plan);foreach(string name in RootStages)if(stage.Attribute(name)!=null)image.Add(new XAttribute(stage.Attribute(name)));
        var fixedSources=image.Element("sources").Elements("source").ToArray();var sourceStages=stage.Element("sources").Elements().ToArray();
        if(stage.Element("sources").Attributes().Any()||stage.Element("sources").Nodes().Any(n=>n is not XElement)||sourceStages.Length!=fixedSources.Length)throw new InvalidDataException();
        for(int index=0;index<sourceStages.Length;index++)
        {
            var value=sourceStages[index];if(value.Name!="source"||(int?)value.Attribute("index")!=index||value.Nodes().Any()||
                value.Attributes().Any(a=>a.Name.NamespaceName.Length!=0||a.Name.LocalName!="index"&&!SourceStages.Contains(a.Name.LocalName)))throw new InvalidDataException();
            foreach(string name in SourceStages)if(value.Attribute(name)!=null)fixedSources[index].Add(new XAttribute(value.Attribute(name)));
        }
        var seen=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var reference in stage.Elements("ref"))
        {
            string field=(string)reference.Attribute("field"),digest=(string)reference.Attribute("digest");
            if(reference.Attributes().Count()!=2||reference.Nodes().Any()||!Fields.Contains(field)||!seen.Add(field)||!Hex(digest))throw new InvalidDataException();
            var value=ReadFrame(ResolveArtifact(path,operation,BlobName(operation,field,digest)),ArtifactMagic,operation,-1,FieldLimit(field),budget);
            if(value.Name!=field||RebirthRemoteResourceDurableJournal.Digest(Text(value))!=digest)throw new InvalidDataException();image.Add(value);
        }
        if(budget?.GraphWorkspace>0&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitHistoricalSourceImage(image,budget.GraphWorkspace,session?.RetainedGraph??0))throw new InvalidDataException("Captured original native graph workspace exceeded.");
        RebirthRemoteResourceDurableJournal.ValidateImageFromVerifiedPlan(image,planBytes);return new RebirthRemoteResourceDurableJournal.Record(operation,revision,image,actualBinding);
    }
    internal static bool IsArtifact(string name,Guid operation,out string canonical)
    {
        canonical=name;
        if(name.EndsWith(".tmp",StringComparison.Ordinal))
        {
            if(name.Length<37||name[name.Length-37]!='.'||!Guid.TryParseExact(name.Substring(name.Length-36,32),"N",out _))return false;
            canonical=name.Substring(0,name.Length-37);
        }
        if(canonical==PlanName(operation))return true;
        foreach(string field in Fields)
        {
            string prefix=operation.ToString("N")+".blob."+field+".";
            if(canonical.StartsWith(prefix,StringComparison.Ordinal)&&canonical.EndsWith(".rrb",StringComparison.Ordinal)&&Hex(canonical.Substring(prefix.Length,canonical.Length-prefix.Length-4)))return true;
        }
        return false;
    }
    internal static void ValidateArtifact(string path,Guid operation,HistoricalReadBudget budget=null)
    {
        if(!IsArtifact(Path.GetFileName(path),operation,out string name))throw new InvalidDataException();
        if(name==PlanName(operation))
        {
            var plan=ReadFrame(path,ArtifactMagic,operation,-1,budget?.PlanBytes??MaximumPlan,budget);
            if(budget?.GraphWorkspace>0&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitHistoricalSourceImage(plan,budget.GraphWorkspace,session?.RetainedGraph??0))throw new InvalidDataException("Captured original plan graph workspace exceeded.");
            if((string)plan.Attribute("operation")!=operation.ToString("N")||!XNode.DeepEquals(plan,RebirthRemoteResourceDurableJournal.ImmutablePlan(plan)))throw new InvalidDataException();RebirthRemoteResourceDurableJournal.ValidateImmutablePlan(plan);return;
        }
        foreach(string field in Fields)
        {
            string prefix=operation.ToString("N")+".blob."+field+".";if(!name.StartsWith(prefix,StringComparison.Ordinal))continue;
            var image=ReadFrame(path,ArtifactMagic,operation,-1,FieldLimit(field),budget);
            if(image.Name!=field||RebirthRemoteResourceDurableJournal.Digest(Text(image))!=name.Substring(prefix.Length,64))throw new InvalidDataException();RebirthRemoteResourceDurableJournal.ValidateSettlementElement(image);return;
        }
        throw new InvalidDataException();
    }
    internal static void ValidateArtifactSet(System.Collections.Generic.IEnumerable<string> paths,Guid operation)
    {
        var names=paths.Select(path=>{if(!IsArtifact(Path.GetFileName(path),operation,out string name))throw new InvalidDataException();return name;}).Distinct(StringComparer.Ordinal).ToArray();
        foreach(string field in Fields)
        {
            int count=names.Count(name=>name.StartsWith(operation.ToString("N")+".blob."+field+".",StringComparison.Ordinal));
            int maximum=field=="reason"?4096:field=="removed"?2:1;if(count>maximum)throw new InvalidDataException("Conflicting immutable settlement artifacts.");
        }
    }
    internal static bool CanResumePrepared(string directory,RebirthRemoteResourceDurableJournal.Record original)
    {
        try
        {
            if(original.Revision!=0||original.Phase!=0||Directory.Exists(Path.Combine(directory,"completed",original.Operation.ToString("N"))))return false;
            var expected=new System.Collections.Generic.Dictionary<string,byte[]>(StringComparer.Ordinal);
            var root=original.Copy();var plan=RebirthRemoteResourceDurableJournal.ImmutablePlan(root);
            expected.Add(PlanName(original.Operation),Frame(ArtifactMagic,original.Operation,-1,Encoding.UTF8.GetBytes(Text(plan))));
            foreach(string field in Fields)
            {
                var value=root.Element(field);if(value==null)continue;string text=Text(value),digest=RebirthRemoteResourceDurableJournal.Digest(text);
                expected.Add(BlobName(original.Operation,field,digest),Frame(ArtifactMagic,original.Operation,-1,Encoding.UTF8.GetBytes(text)));
            }
            string stage=original.Operation.ToString("N")+"."+0L.ToString("D20",CultureInfo.InvariantCulture)+".rrj";
            expected.Add(stage,Encode(original,directory,()=>true,false));
            foreach(string path in Directory.EnumerateFiles(directory,original.Operation.ToString("N")+".*"))
            {
                string name=Path.GetFileName(path);if(name.EndsWith(".rrj",StringComparison.Ordinal))return false;
                if(name.EndsWith(".tmp",StringComparison.Ordinal))
                {if(name.Length<37||name[name.Length-37]!='.'||!Guid.TryParseExact(name.Substring(name.Length-36,32),"N",out _))return false;name=name.Substring(0,name.Length-37);}
                if(!expected.TryGetValue(name,out var bytes)||!MatchesFile(path,bytes))return false;
            }
            // No committed generation existed: no effect permit could have
            // escaped. Only the exact original/authenticated caller may finish
            // its identical stage-zero publication; nothing is deleted/adopted.
            return true;
        }
        catch{return false;}
    }
}
