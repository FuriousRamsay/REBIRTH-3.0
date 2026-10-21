using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

// Pre-clone planning admission only. Full retained-history/action/refund and
// artifact reader admission are separate; this never supplies source Ready.
internal static class RebirthRemoteResourcePlacedStationAllocation
{
    internal const long SharedReaderScratchBytes=64L*1024*1024;
    internal enum ReadPhase{Planning,HistoricalSource,Artifact,NativeBefore,OwnerEnvelope,WorldApply,Completion}
    private static readonly System.Collections.Generic.Dictionary<object,Scratch> scratchOwners=new System.Collections.Generic.Dictionary<object,Scratch>();
    internal sealed class Scratch:IDisposable
    {
        private readonly object owner;private readonly Func<bool> current;private bool closed;
        internal readonly ReadPhase Phase;
        internal Scratch(object value,ReadPhase phase,Func<bool> live){owner=value;Phase=phase;current=live;}
        internal bool Current=>!closed&&ThreadManager.IsMainThread()&&current()&&scratchOwners.TryGetValue(owner,out var actual)&&ReferenceEquals(actual,this);
        internal bool IsFor(object expected)=>ReferenceEquals(owner,expected)&&Current;
        public void Dispose(){if(closed)return;closed=true;if(scratchOwners.TryGetValue(owner,out var actual)&&ReferenceEquals(actual,this))scratchOwners.Remove(owner);}
    }
    internal static bool HasScratch(object owner,ReadPhase phase)=>owner!=null&&ThreadManager.IsMainThread()&&scratchOwners.TryGetValue(owner,out var scratch)&&scratch.Phase==phase&&scratch.IsFor(owner);
    private static bool TryScratch(object owner,ReadPhase phase,Func<bool> current,out Scratch scratch)
    {
        scratch=null;if(owner==null||!ThreadManager.IsMainThread()||!current()||scratchOwners.ContainsKey(owner))return false;
        var value=new Scratch(owner,phase,current);scratchOwners.Add(owner,value);scratch=value;return true;
    }
    internal static bool TryBeginScratch(Admission admission,ReadPhase phase,out Scratch scratch)
        =>TryScratch(admission,phase,()=>admission!=null&&admission.PlanningBytes>=SharedReaderScratchBytes&&RebirthRemoteResourcePlacedStationPreparation.OwnsAllocation(admission),out scratch);
    internal static bool TryBeginScratch(RebirthRemoteResourcePlacedStationPreparation.ColdRead cold,ReadPhase phase,out Scratch scratch)
        =>TryScratch(cold,phase,()=>cold?.Current()==true&&cold.Header.SourceWorkspace>=SharedReaderScratchBytes,out scratch);
    internal static bool TryBeginScratch(RebirthRemoteResourcePlacedStationPreparation.UploadRead upload,ReadPhase phase,out Scratch scratch)
        =>TryScratch(upload,phase,()=>upload?.Current==true&&upload.ReservedBytes>=SharedReaderScratchBytes,out scratch);
    internal sealed class Admission
    {
        internal readonly Guid Source;internal readonly string Binding,Original;
        internal readonly long SourceBytes,OriginalBytes,SourceGraphBytes,OriginalGraphBytes,GraphBytes,ParserScratch,PlanningBytes;
        internal long ReservedBytes;
        internal Admission(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
            long source,long intent,long sourceGraph,long intentGraph,long graph,long scratch,long planning)
        {Source=root.Operation;Binding=root.Binding;Original=original.Digest;SourceBytes=source;OriginalBytes=intent;SourceGraphBytes=sourceGraph;OriginalGraphBytes=intentGraph;GraphBytes=graph;ParserScratch=scratch;PlanningBytes=planning;ReservedBytes=planning;}
        internal bool ReserveArtifact(long bytes)=>RebirthRemoteResourcePlacedStationPreparation.TryGrowArtifactReservation(this,bytes);
        internal bool ReserveReadWorkspace(long bytes)=>RebirthRemoteResourcePlacedStationPreparation.TryGrowReadReservation(this,bytes);
    }
    private static void MeasureXml(Action<XmlWriter> write,long maximum,out long bytes,out long graph)
    {
        using(var count=new RebirthRemoteResourcePlacedStationStreams.Counter(maximum))
        using(var native=XmlWriter.Create(count,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,Indent=false,NewLineHandling=NewLineHandling.None,CloseOutput=false}))
        using(var meter=new RebirthRemoteResourcePlacedStationStreams.XmlGraphCounter(native))
        {write(meter);meter.Flush();bytes=count.Length;graph=meter.GraphBytes;}
    }
    private sealed class Meter
    {
        internal long Graph,Scratch,PaidNativeBytes;
        internal void Item(string image,long retainedFrame=0)
        {
            if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9Structure(image,out var measured))throw new InvalidDataException();
            Graph=checked(Graph+measured.CloneAllocationBound);Scratch=Math.Max(Scratch,checked(measured.ParserScratchBound+2L*image.Length+retainedFrame));
        }
        internal void Stack(ItemStack stack)
        {
            if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException();
            ValidateLiveTopology(stack.itemValue);
            if(stack.itemValue.IsEmpty())
            {
                if(stack.count!=0||stack.itemValue.Metadata?.Count>0||stack.itemValue.Stats?.Length>0||
                    stack.itemValue.ModificationCount>0||stack.itemValue.CosmeticModCount>0)throw new InvalidDataException();
                Graph=checked(Graph+512);return;
            }
            Item(RebirthNativeItemCodec.Encode(stack.itemValue));
        }
        private static void ValidateLiveTopology(ItemValue root)
        {
            // Serialized empty children and modifier children omit fields which
            // native Clone would otherwise retain. Refuse hidden graphs before
            // cloning rather than charging only their smaller wire image.
            var pending=new System.Collections.Generic.Stack<System.Collections.Generic.KeyValuePair<ItemValue,int>>();
            pending.Push(new System.Collections.Generic.KeyValuePair<ItemValue,int>(root,0));int nodes=0,slots=0;
            while(pending.Count>0)
            {
                var entry=pending.Pop();var item=entry.Key;if(item==null)continue;
                if(++nodes>RebirthNativeItemConformanceReader.MaximumNodes||entry.Value>RebirthNativeItemConformanceReader.MaximumDepth)throw new InvalidDataException();
                int mods=item.ModificationCount,cosmetics=item.CosmeticModCount;
                if(mods<0||cosmetics<0||mods>255||cosmetics>255||slots>RebirthNativeItemConformanceReader.MaximumChildSlots-mods-cosmetics)throw new InvalidDataException();
                slots+=mods+cosmetics;
                if(item.IsEmpty()&&(item.Metadata?.Count>0||item.Stats?.Length>0||mods!=0||cosmetics!=0)||
                    item.ItemClass is ItemClassModifier&&(mods!=0||cosmetics!=0))throw new InvalidDataException();
                for(int group=0;group<2;group++)for(int index=0;index<(group==0?mods:cosmetics);index++)
                    pending.Push(new System.Collections.Generic.KeyValuePair<ItemValue,int>(group==0?item.GetModification(index):item.GetCosmeticMod(index),entry.Value+1));
            }
        }
        internal void Grid(ItemStack[] slots)
        {
            if(slots==null||slots.Length>short.MaxValue)throw new InvalidDataException();
            Graph=checked(Graph+32L+8L*slots.Length);foreach(var stack in slots)Stack(stack);
        }
        internal void Snapshot(string text,bool paid)
        {
            if(text==null||text.Length>4L*((8L*1024*1024+2)/3))throw new InvalidDataException();
            byte[] frame=Convert.FromBase64String(text);
            if(frame.Length<8||frame.Length>8*1024*1024||Convert.ToBase64String(frame)!=text)throw new InvalidDataException();
            Scratch=Math.Max(Scratch,frame.LongLength*4);
            using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=0x31534352)throw new InvalidDataException();
                int count=reader.ReadInt32();if(count<0||count>short.MaxValue||count>(stream.Length-stream.Position)/8)throw new InvalidDataException();
                Graph=checked(Graph+32L+8L*count);
                byte[] empty=Convert.FromBase64String(RebirthNativeItemCodec.Encode(ItemValue.None));
                for(int index=0;index<count;index++)
                {
                    int amount=reader.ReadInt32(),size=reader.ReadInt32();
                    if(amount<0||size<1||size>196608||size>stream.Length-stream.Position)throw new InvalidDataException();
                    byte[] native=reader.ReadBytes(size);
                    if(native.SequenceEqual(empty)){if(amount!=0)throw new InvalidDataException();Graph=checked(Graph+512);}
                    else{if(paid&&amount<1)throw new InvalidDataException();Item(Convert.ToBase64String(native),frame.LongLength+native.LongLength);if(paid)PaidNativeBytes=checked(PaidNativeBytes+size);}
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException();
            }
        }
        private static string ItemText(BinaryReader reader,long end)
        {
            uint length=0;int shift=0,groups=0;byte current;
            do
            {
                if(reader.BaseStream.Position>=end||groups==5)throw new InvalidDataException();current=reader.ReadByte();
                if(groups==4&&(current&0xf8)!=0)throw new InvalidDataException();length|=(uint)(current&127)<<shift;shift+=7;groups++;
            }while((current&128)!=0);
            if(groups>1&&(current&127)==0||length>262144||length>end-reader.BaseStream.Position)throw new InvalidDataException();
            return new UTF8Encoding(false,true).GetString(reader.ReadBytes((int)length));
        }
        internal void Actor(string text)
        {
            if(text==null||text.Length>8*1024*1024)throw new InvalidDataException();byte[] frame=Convert.FromBase64String(text);
            if(frame.Length<1||frame.Length>6*1024*1024||Convert.ToBase64String(frame)!=text)throw new InvalidDataException();Scratch=Math.Max(Scratch,frame.LongLength*4);
            using(var stream=new MemoryStream(frame,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                int bag=0;
                for(int group=0;group<3;group++)
                {
                    int size=reader.ReadInt32();if(size<4||size>stream.Length-stream.Position)throw new InvalidDataException();long end=stream.Position+size;
                    int count=reader.ReadInt32();if(count<0||count>(group==0?169:group==1?20:1)||group==2&&count!=1)throw new InvalidDataException();if(group==0)bag=count;
                    Graph=checked(Graph+32L+8L*count);string empty=RebirthNativeItemCodec.Encode(ItemValue.None);
                    for(int index=0;index<count;index++)
                    {
                        if(stream.Position>end-4)throw new InvalidDataException();int amount=reader.ReadInt32();string item=ItemText(reader,end);
                        if(amount<0)throw new InvalidDataException();if(item==empty){if(amount!=0)throw new InvalidDataException();Graph=checked(Graph+512);}else Item(item,frame.LongLength);
                    }
                    if(stream.Position!=end)throw new InvalidDataException();
                }
                byte locks=reader.ReadByte();if(locks>1)throw new InvalidDataException();
                if(locks==1){int count=reader.ReadInt32();if(count!=bag)throw new InvalidDataException();Graph=checked(Graph+32L+count);for(int i=0;i<count;i++)if(reader.ReadByte()>1)throw new InvalidDataException();}
                if(stream.Position!=stream.Length)throw new InvalidDataException();
            }
        }
        internal void Recipe(string text,long retainedFrame=0)
        {
            if(text==null||text.Length>6*1024*1024)throw new InvalidDataException();byte[] bytes=Convert.FromBase64String(text);
            if(bytes.Length<1||bytes.Length>4*1024*1024||Convert.ToBase64String(bytes)!=text)throw new InvalidDataException();Scratch=Math.Max(Scratch,bytes.LongLength*4+retainedFrame);
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true),true))
            {
                if(reader.ReadUInt16()!=1)throw new InvalidDataException();reader.ReadInt32();int output=reader.ReadInt32();byte scrap=reader.ReadByte();float time=reader.ReadSingle();reader.ReadInt32();
                if(output<1||scrap>1||float.IsNaN(time)||float.IsInfinity(time)||time<0)throw new InvalidDataException();
                string area=ItemText(reader,stream.Length);if(Encoding.UTF8.GetByteCount(area)>512)throw new InvalidDataException();Graph=checked(Graph+512+2L*area.Length);
                int count=reader.ReadInt32();if(count<1||count>4096)throw new InvalidDataException();Graph=checked(Graph+32L+8L*count);
                for(int i=0;i<count;i++)
                {
                    int amount=reader.ReadUInt16();
                    if(amount==0){Graph=checked(Graph+512);continue;}
                    if(!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(bytes,checked((int)stream.Position),out var measured,out int consumed)||measured.Nodes<1)throw new InvalidDataException();
                    Graph=checked(Graph+measured.CloneAllocationBound);Scratch=Math.Max(Scratch,measured.ParserScratchBound+bytes.LongLength+retainedFrame);stream.Position+=consumed;
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException();
            }
        }
        internal void Queue(string text)
        {
            if(text==null||Encoding.UTF8.GetByteCount(text)>RebirthRemoteResourcePlacedStationQueue.MaximumBytes)throw new InvalidDataException();
            // Exact sidecar grammar has root/native/recipes and <=255 rows.
            // Reject node explosions BEFORE constructing its XElement graph.
            using(var input=new StringReader(text))using(var scan=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=RebirthRemoteResourcePlacedStationQueue.MaximumBytes}))
            {
                int elements=0;
                while(scan.Read())
                {
                    if(scan.Depth>3||scan.NodeType==XmlNodeType.Comment||scan.NodeType==XmlNodeType.ProcessingInstruction||scan.NodeType==XmlNodeType.CDATA||scan.NodeType==XmlNodeType.XmlDeclaration)throw new InvalidDataException();
                    if(scan.NodeType==XmlNodeType.Element&& (++elements>3+byte.MaxValue||scan.AttributeCount>17))throw new InvalidDataException();
                }
            }
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
            if(body.Name!="placedNativeQueue"||body.Elements().Count()!=2||body.Element("native")==null||body.Element("recipes")==null)throw new InvalidDataException();
            MeasureXml(body.WriteTo,RebirthRemoteResourcePlacedStationQueue.MaximumBytes,out _,out long graph);Graph=checked(Graph+graph);
            string native=body.Element("native").Value;if(native.Length>1400000)throw new InvalidDataException();byte[] wire=Convert.FromBase64String(native);
            if(wire.Length<1||wire.Length>1024*1024||Convert.ToBase64String(wire)!=native)throw new InvalidDataException();Scratch=Math.Max(Scratch,4L*wire.Length+2L*text.Length);
            var rows=body.Element("recipes").Elements().ToArray();if(rows.Length<1||rows.Length>byte.MaxValue||wire[0]!=rows.Length)throw new InvalidDataException();
            foreach(var row in rows)
            {
                if(row.Name!="recipe"||row.Nodes().Any(node=>node is not System.Xml.Linq.XText))throw new InvalidDataException();Graph=checked(Graph+512);
                string repair=(string)row.Attribute("repair");if(repair==null||repair.Length>262144)throw new InvalidDataException();if(repair!="")Item(repair,wire.LongLength+2L*text.Length);
                if(row.Value!="")Recipe(row.Value,wire.LongLength+2L*text.Length);
            }
        }
    }
    internal static bool TryAdmitIntentBeforeHydration(System.Xml.Linq.XElement original)
        =>TryAdmitIntentBeforeHydration(original,out _);
    internal static bool TryAdmitIntentBeforeHydration(System.Xml.Linq.XElement original,out long workspace)
    {
        workspace=0;
        try
        {
            var action=original?.Element("action");if((int?)action?.Attribute("placed")!=1)return false;
            long bytes,graph;MeasureXml(original.WriteTo,RebirthRemoteResourceClientOriginal.MaximumOriginalBytes,out bytes,out graph);
            if(checked(8*graph+64L*1024*1024)>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            var meter=new Meter();meter.Actor(original.Element("actorBefore").Value);
            meter.Snapshot(original.Element("requirements").Value,false);meter.Snapshot(original.Element("remoteRequirements").Value,true);
            meter.Snapshot(action.Element("input").Value,false);meter.Snapshot(action.Element("outputImage").Value,false);
            meter.Recipe(action.Element("authored").Value);meter.Recipe(action.Element("template").Value);
            meter.Queue(action.Element("queue").Value);
            workspace=checked(8*graph+12*meter.Graph+32*meter.PaidNativeBytes+meter.Scratch+64L*1024*1024);
            return workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{return false;}
    }
    internal static bool TryAdmitArtifactBeforeHydration(System.Xml.Linq.XElement artifact,long retainedBytes,out long workspace)
    {
        workspace=0;
        try
        {
            if(artifact==null||retainedBytes<1||retainedBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumBytes)return false;
            MeasureXml(artifact.WriteTo,RebirthRemoteResourcePlacedStationArtifact.MaximumBytes,out long bytes,out long xmlGraph);
            if(bytes!=retainedBytes)return false;
            var original=artifact.Element("original")?.Element("fetchClientOriginal");var action=original?.Element("action");
            var plan=artifact.Element("plan");var saved=artifact.Element("savedBefore");
            if((int?)action?.Attribute("placed")!=1||plan==null||saved==null)return false;
            long text=checked(3*retainedBytes+8*xmlGraph);
            if(text+64L*1024*1024>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            var meter=new Meter();meter.Actor(original.Element("actorBefore").Value);meter.Actor(plan.Element("actorAfter").Value);
            meter.Snapshot(original.Element("requirements").Value,false);meter.Snapshot(original.Element("remoteRequirements").Value,true);
            meter.Snapshot(action.Element("input").Value,false);meter.Snapshot(action.Element("outputImage").Value,false);
            meter.Recipe(action.Element("authored").Value);meter.Recipe(action.Element("template").Value);
            meter.Queue(action.Element("queue").Value);meter.Queue(plan.Element("queue").Value);
            string snapshotText=saved.Element("snapshot").Value,afterText=plan.Element("afterTile").Value;
            const int maximum=8*1024*1024;
            if(snapshotText.Length>4L*((maximum+2L)/3)||afterText.Length>4L*((maximum+2L)/3))return false;
            byte[] snapshot=Convert.FromBase64String(snapshotText),after=Convert.FromBase64String(afterText);
            long offset=(long)saved.Attribute("tileOffset");int length=(int)saved.Attribute("tileLength");
            if(snapshot.Length>maximum||after.Length>maximum||offset<8||length<1||length>maximum||offset>snapshot.LongLength-length||
                Convert.ToBase64String(snapshot)!=snapshotText||Convert.ToBase64String(after)!=afterText)return false;
            var before=new byte[length];Buffer.BlockCopy(snapshot,(int)offset,before,0,length);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(before,out long beforeGraph,out long beforeScratch)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(after,out long afterGraph,out long afterScratch))return false;
            meter.Graph=checked(meter.Graph+beforeGraph+afterGraph);meter.Scratch=Math.Max(meter.Scratch,Math.Max(beforeScratch,afterScratch)+snapshot.LongLength+after.LongLength+length);
            // Same twelve detached graph-equivalent allowance as fresh planning;
            // source journal/history remains separately charged by its reader.
            workspace=checked(text+12*meter.Graph+32*meter.PaidNativeBytes+meter.Scratch+64L*1024*1024);
            return workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{workspace=0;return false;}
    }
    internal static bool TryAdmitUploadTargetBeforeCapture(TileEntityWorkstation station,long originalWorkspace,out long workspace)
    {
        workspace=0;
        try
        {
            if(station==null||originalWorkspace<SharedReaderScratchBytes)return false;
            var meter=new Meter();meter.Grid(station.Input);meter.Grid(station.Output);meter.Grid(station.Tools);meter.Grid(station.Fuel);meter.Grid(station.lastInput);
            if(station.Queue==null||station.Queue.Length>byte.MaxValue)return false;
            foreach(var row in station.Queue)
            {
                if(row==null)return false;meter.Graph=checked(meter.Graph+512);
                if(row.RepairItem!=null&&!row.RepairItem.IsEmpty())meter.Stack(new ItemStack(row.RepairItem,1));
                if(row.Recipe?.ingredients!=null)foreach(var item in row.Recipe.ingredients)meter.Stack(item);
            }
            if(station.CraftCompleteList!=null)foreach(var completed in station.CraftCompleteList){if(completed==null)return false;meter.Stack(completed.CraftedItemStack);meter.Graph=checked(meter.Graph+512);}
            workspace=checked(originalWorkspace+6L*RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes+12*meter.Graph+Math.Max(0,meter.Scratch-SharedReaderScratchBytes));
            return workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{return false;}
    }
    internal static bool TryMeasureSourceGraph(RebirthRemoteResourceDurableJournal.Record source,out long graph)
    {
        graph=0;try{if(source==null)return false;MeasureXml(source.WriteDetachedTo,128L*1024*1024,out _,out graph);return true;}catch{graph=0;return false;}
    }
    internal static bool TryAdmitHistoricalSourceImage(System.Xml.Linq.XElement image,long maximum,long retainedGraph)
    {
        try
        {
            if(image==null||maximum<1||maximum>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||retainedGraph<0)return false;
            MeasureXml(image.WriteTo,128L*1024*1024,out long bytes,out long graph);
            if(checked(retainedGraph+4*bytes+6*graph)>maximum)return false;
            var meter=new Meter();meter.Snapshot(image.Element("requirements").Value,false);
            if(image.Element("removed")!=null)meter.Snapshot(image.Element("removed").Value,false);
            var sources=image.Element("sources").Elements("source").ToArray();if(sources.Length>RebirthRemoteResourceDurableJournal.MaximumSources)return false;
            foreach(var row in sources){meter.Snapshot(row.Element("before").Value,false);meter.Snapshot(row.Element("after").Value,false);meter.Snapshot(row.Element("paid").Value,true);}
            return checked(retainedGraph+4*bytes+6*graph+12*meter.Graph+32*meter.PaidNativeBytes+meter.Scratch)<=maximum;
        }
        catch{return false;}
    }
    internal static bool TryAdmitCancellationBeforeHydration(System.Xml.Linq.XElement header,System.Xml.Linq.XElement permission,out long workspace)
    {
        workspace=0;try
        {
            MeasureXml((permission??header).WriteTo,12L*1024*1024,out long bytes,out long xmlGraph);
            var meter=new Meter();meter.Actor(header.Element("actorBefore").Value);meter.Queue(header.Element("queue").Value);meter.Snapshot(header.Element("input").Value,false);
            if(permission!=null)
            {
                meter.Actor(permission.Element("afterActor").Value);meter.Queue(permission.Element("afterQueue").Value);meter.Snapshot(permission.Element("refund").Value,true);
                foreach(string name in new[]{"beforeTile","afterTile"})
                {
                    string text=permission.Element(name).Value;if(text.Length>4L*((8L*1024*1024+2)/3))return false;
                    byte[] frame=Convert.FromBase64String(text);
                    if(Convert.ToBase64String(frame)!=text||!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(frame,out long graph,out long scratch))return false;
                    meter.Graph=checked(meter.Graph+graph);meter.Scratch=Math.Max(meter.Scratch,scratch+frame.LongLength);
                }
            }
            workspace=checked(12*bytes+8*xmlGraph+12*meter.Graph+32*meter.PaidNativeBytes+Math.Max(SharedReaderScratchBytes,meter.Scratch));
            return workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{return false;}
    }
    internal static bool TryAdmitCancellationOwnerBeforeCapture(EntityPlayerLocal actor,out long workspace)
    {
        workspace=0;try
        {
            if(actor?.bag?.ItemGrid.items==null||actor.inventory?.ItemGrid.items==null||actor.bag.ItemGrid.items.Length>169||actor.inventory.ItemGrid.items.Length>20)return false;
            var meter=new Meter();meter.Grid(actor.bag.ItemGrid.items);meter.Grid(actor.inventory.ItemGrid.items);meter.Stack(actor.DragAndDropItem);
            workspace=checked(12*meter.Graph+Math.Max(0,meter.Scratch-SharedReaderScratchBytes));return workspace<RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{return false;}
    }
    internal static bool TryAdmitOwnerReplyBeforeHydration(System.Xml.Linq.XElement reply)
        =>TryAdmitOwnerReplyBeforeHydration(reply,out _);
    internal static bool TryAdmitOwnerReplyBeforeHydration(System.Xml.Linq.XElement reply,out long workspace)
    {
        workspace=0;
        try
        {
            if(reply==null)return false;MeasureXml(reply.WriteTo,12L*1024*1024,out long bytes,out long xmlGraph);
            if(checked(4*bytes+8*xmlGraph+64L*1024*1024)>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            var meter=new Meter();meter.Actor(reply.Element("beforeActor").Value);meter.Actor(reply.Element("afterActor").Value);
            meter.Snapshot(reply.Element("paid").Value,true);meter.Queue(reply.Element("queue").Value);
            string encoded=reply.Element("afterTile").Value;if(encoded.Length>4L*((8L*1024*1024+2)/3))return false;
            byte[] tile=Convert.FromBase64String(encoded);if(Convert.ToBase64String(tile)!=encoded||
                !RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(tile,out long graph,out long scratch))return false;
            meter.Graph=checked(meter.Graph+graph);meter.Scratch=Math.Max(meter.Scratch,scratch+tile.LongLength);
            workspace=checked(4*bytes+8*xmlGraph+12*meter.Graph+32*meter.PaidNativeBytes+meter.Scratch+64L*1024*1024);
            return workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{workspace=0;return false;}
    }
    // Future native publication is admitted against the existing supported
    // WHOLE snapshot/payload bounds: unrelated native chunk work can change
    // compression/size after Before, so a queue-only byte delta is insufficient.
    internal static bool TryCompletionWorkspace(RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,out long workspace,out long evidenceBytes)
    {
        workspace=0;evidenceBytes=0;
        try
        {
            if(artifact==null||!artifact.HasCapturedAllocation)return false;
            var body=artifact.Copy();string actor=body.Element("plan").Element("actorAfter").Value;
            var meter=new Meter();meter.Actor(actor);
            byte[] tile=Convert.FromBase64String(body.Element("plan").Element("afterTile").Value);
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(tile,out long graph,out long scratch))return false;
            long markers=0;int count=0;
            foreach(var marker in body.Element("savedBefore").Element("markers").Elements())
            {
                if(++count>RebirthRemoteResourceDurableJournal.MaximumSources+1)return false;
                string key=(string)marker.Attribute("key");if(key==null||key.Length>1024)return false;
                markers=checked(markers+new UTF8Encoding(false,true).GetByteCount(key)+32);
            }
            const long buffs=1024*1024; // Existing NativePhase reader support, NOT a native blob limit.
            long raw=RebirthStationSnapshotEvidence.MaximumSnapshotBytes,payload=RebirthStationRegionPayload.MaximumPayloadBytes;
            long actorBytes=new UTF8Encoding(false,true).GetByteCount(actor);
            evidenceBytes=checked(raw+payload+buffs+actorBytes+markers+2*4096+256); // Ref/path UTF8 maxima plus all scalar/length headers.
            // Request/raw copies, native readback/inflate, bounded binary
            // evidence streaming+readback, and detached owner/native graphs.
            workspace=checked(6*raw+6*payload+4*buffs+6*actorBytes+4*markers+12*(meter.Graph+graph)+Math.Max(meter.Scratch,scratch));
            return evidenceBytes<=32L*1024*1024&&workspace<=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace;
        }
        catch{workspace=0;evidenceBytes=0;return false;}
    }
    internal static bool TryAdmitPlanning(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        TileEntityWorkstation station,out Admission admission,long availableBytes=RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)
    {
        admission=null;
        try
        {
            if(!ThreadManager.IsMainThread()||availableBytes<1||availableBytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace||root==null||original==null||station==null||root.Phase!=1||root.HasOwnerAction||!root.SourcesUnattempted)return false;
            MeasureXml(root.WriteDetachedTo,128L*1024*1024,out long sourceBytes,out long sourceGraph);
            MeasureXml(original.WriteDetachedTo,RebirthRemoteResourceClientOriginal.MaximumOriginalBytes,out long originalBytes,out long originalGraph);
            // Bound source/original XElement copy workspace before Copy() or
            // native graph materialization, using the existing operation cap.
            long textWorkspace=checked(8*sourceGraph+8*originalGraph);
            if(textWorkspace+64L*1024*1024>availableBytes)return false;
            var meter=new Meter();var source=root.Copy();var intent=original.Copy();meter.Actor(intent.Element("actorBefore").Value);
            meter.Snapshot(source.Element("requirements").Value,false);
            var sources=source.Element("sources").Elements("source").ToArray();if(sources.Length<1||sources.Length>RebirthRemoteResourceDurableJournal.MaximumSources)return false;
            foreach(var row in sources){meter.Snapshot(row.Element("before").Value,false);meter.Snapshot(row.Element("after").Value,false);meter.Snapshot(row.Element("paid").Value,true);meter.Snapshot(row.Element("paid").Value,false);}
            meter.Grid(station.Input);meter.Grid(station.Output);meter.Grid(station.Tools);meter.Grid(station.Fuel);
            if(station.Queue==null||station.Queue.Length>byte.MaxValue)return false;
            foreach(var row in station.Queue)
            {
                if(row==null)throw new InvalidDataException();meter.Graph=checked(meter.Graph+512);
                if(row.RepairItem!=null&&!row.RepairItem.IsEmpty())meter.Item(RebirthNativeItemCodec.Encode(row.RepairItem));
                var recipe=row.Recipe;if(recipe==null)continue;
                if(recipe.ingredients==null||recipe.ingredients.Count>short.MaxValue)throw new InvalidDataException();
                meter.Graph=checked(meter.Graph+512+8L*recipe.ingredients.Count+2L*((recipe.tooltip?.Length??0)+(recipe.craftingArea?.Length??0)));
                foreach(var item in recipe.ingredients)meter.Stack(item);
            }
            // Twelve complete graph equivalents cover retained source stages,
            // exact current reread, native watchers, input-fragment planner,
            // detached row/queue hydration and paired Before/After validation.
            // Paid native bytes additionally cover full-original ImageKey
            // metadata strings across these detached row copies (base64/UTF16).
            long planning=checked(textWorkspace+12*meter.Graph+32*meter.PaidNativeBytes+meter.Scratch+64L*1024*1024);
            if(planning>availableBytes)return false;
            admission=new Admission(root,original,sourceBytes,originalBytes,sourceGraph,originalGraph,meter.Graph,meter.Scratch,planning);return true;
        }
        catch{admission=null;return false;}
    }
}
