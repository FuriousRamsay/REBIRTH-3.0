using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Purpose-scoped write-ahead originals. This journal alone grants no source
// mutation, owner credit or saved-effect acknowledgement. Failed candidates are
// retained; a corrupt newest generation never falls back to an older outcome.
internal static class RebirthRemoteResourceDurableJournal
{
    private const uint Magic=0x314F5252;
    internal const int MaximumSources=128;
    private const int MaximumLegacyBytes=64*1024*1024,MaximumBytes=128*1024*1024,MaximumGenerations=4096;
    // Exact serialized UTF8 headroom, including base64/XML escaping. The
    // immutable plan budget is unchanged; settlement can never consume it.
    private const long SettlementReserve=2L*24*1024*1024+8L*1024*1024+192*1024;
    private static bool publishing;
    private static long mutationEpoch;
    internal static long MutationEpoch=>System.Threading.Interlocked.Read(ref mutationEpoch);
    internal static bool TransitionQuiescent=>!publishing;
    internal sealed class Record
    {
        private readonly XElement original;
        private readonly string binding;
        internal Guid Operation { get; }
        internal long Revision { get; }
        internal string World=>(string)original.Attribute("world");
        internal string Owner=>(string)original.Attribute("owner");
        internal string Creation=>(string)original.Attribute("creation");
        internal int Phase=>(int)original.Attribute("phase");
        internal string Binding=>binding;
        internal bool HasOwnerAction=>original.Element("ownerAction")!=null;
        internal bool SourcesUnattempted=>original.Element("sources")!=null&&original.Element("sources").Elements("source").Any()&&original.Element("sources").Elements("source").All(node=>(int?)node.Attribute("attempted")==0);
        internal bool HasSourceBeforeProof(string sourceId,string proof)=>proof?.Length==64&&
            (string)original.Element("sources")?.Elements("source").SingleOrDefault(node=>(string)node.Attribute("id")==sourceId)?.Attribute("beforeProof")==proof;
        internal bool HasSourceDebitProof(string sourceId,string proof)
        {
            var row=original.Element("sources")?.Elements("source").SingleOrDefault(node=>(string)node.Attribute("id")==sourceId);
            return proof?.Length==64&&row!=null&&(int?)row.Attribute("attempted")==1&&(int?)row.Attribute("published")==1&&
                ((int?)row.Attribute("restored")??0)==0&&(string)row.Attribute("proof")==proof;
        }
        internal void WriteDetachedTo(XmlWriter writer)=>original.WriteTo(writer);
        internal XElement Copy()=>new XElement(original);
        // Both transition predicates are read-only. Keeping validation inside
        // the immutable record avoids four full rich-plan graph copies for
        // every historical generation without exposing a mutable XElement.
        internal bool Follows(Record previous)=>previous!=null&&Binding==previous.Binding&&
            StateAdvances(previous.original,original)&&SourceStagesAdvance(previous.original,original);
        internal Record(Guid operation,long revision,XElement image,string verifiedBinding=null){Operation=operation;Revision=revision;original=new XElement(image);binding=verifiedBinding??Digest(Plan(original).ToString(SaveOptions.DisableFormatting));}
    }
    internal static string Digest(string value)
    {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    internal static Guid Operation(string world,string owner,string creation,ulong epoch,ulong request,RemoteResourceClientOperation kind)
    {
        if(!Hex(world,64)||!RebirthOriginalActionScope.Validate(owner,creation)||epoch==0||request==0||(int)kind<(int)RemoteResourceClientOperation.Craft||(int)kind>(int)RemoteResourceClientOperation.CookingPull)throw new InvalidDataException();
        string image=world+"|"+owner+"|"+creation+"|"+epoch.ToString(CultureInfo.InvariantCulture)+"|"+request.ToString(CultureInfo.InvariantCulture)+"|"+(byte)kind;
        using(var hash=SHA256.Create())return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(image)).Take(16).ToArray());
    }
    private static bool Hex(string value,int length)=>value?.Length==length&&value.All(character=>character>='0'&&character<='9'||character>='a'&&character<='f');
    private static string DirectoryFor(string owner)
    {
        if(!Hex(owner,64)||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)throw new InvalidOperationException();
        string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))throw new InvalidDataException();
        string root=Path.Combine(Path.GetFullPath(save),"RebirthData","RemoteResources","Outcomes"),directory=Path.Combine(root,owner);
        if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0||Directory.Exists(directory)&&(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
        return directory;
    }
    private static XElement Plan(XElement image)
    {
        var result=new XElement(image);result.Attribute("phase")?.Remove();result.Attribute("outcome")?.Remove();result.Attribute("aborting")?.Remove();result.Attribute("ownerProof")?.Remove();result.Attribute("disposition")?.Remove();result.Attribute("ownerRejecting")?.Remove();result.Attribute("ownerNoUseProof")?.Remove();result.Element("ownerAction")?.Remove();result.Element("ownerRefund")?.Remove();result.Element("reason")?.Remove();result.Element("removed")?.Remove();
        foreach(var source in result.Element("sources")?.Elements("source")??Enumerable.Empty<XElement>())
        {foreach(string name in new[]{"attempted","published","beforeProof","proof","restored","restoreProof","declined","refusalProof"})source.Attribute(name)?.Remove();}
        return result;
    }
    internal static XElement ImmutablePlan(XElement image)=>Plan(image);
    internal static void ValidateImage(XElement image)=>Validate(image);
    // Only V2 reconstruction may use this: the immutable plan digest and its
    // byte count were already checked, while the entire mutable stage is
    // <=128KiB and each serialized settlement artifact has its own exact cap.
    internal static void ValidateImageFromVerifiedPlan(XElement image,int planBytes)=>Validate(image,planBytes);
    internal static void ValidateImmutablePlan(XElement plan)
    {
        if(!XNode.DeepEquals(plan,Plan(plan)))throw new InvalidDataException();
        var image=new XElement(plan);image.SetAttributeValue("phase",0);image.SetAttributeValue("outcome",0);
        foreach(var source in image.Element("sources")?.Elements("source")??Enumerable.Empty<XElement>()){source.SetAttributeValue("attempted",0);source.SetAttributeValue("published",0);}
        image.Add(new XElement("removed",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]))),new XElement("reason",""));Validate(image);
    }
    internal static void ValidateSettlementElement(XElement element)
    {
        if(element==null||element.Name.NamespaceName.Length!=0||Encoding.UTF8.GetByteCount(element.ToString(SaveOptions.DisableFormatting))>RebirthRemoteResourceJournalFormat.FieldLimit(element.Name.LocalName))throw new InvalidDataException();
        bool Scalar(XElement value,int bound)=>value!=null&&!value.Attributes().Any()&&!value.Elements().Any()&&value.Nodes().All(n=>n.GetType()==typeof(XText))&&value.Value.Length<=bound;
        bool GuidValue(string text)=>Guid.TryParseExact(text,"N",out var value)&&value!=Guid.Empty;
        if(element.Name=="removed"||element.Name=="reason")
        {if(!Scalar(element,element.Name=="reason"?512:8*1024*1024))throw new InvalidDataException();return;}
        if(element.Name=="ownerAction")
        {
            if(!GuidValue((string)element.Attribute("child")))throw new InvalidDataException();
            string domain=(string)element.Attribute("domain");
            if(domain=="cooking-grid")
            {if(element.Attributes().Count()!=2||element.Elements().Count()!=1||!Scalar(element.Element("original"),8*1024*1024))throw new InvalidDataException();return;}
            if(domain=="native-craft"||domain=="native-block")
            {if(element.Attributes().Count()!=3||!Hex((string)element.Attribute("nativeBeforeProof"),64)||element.Elements().Count()!=1||!Scalar(element.Element("original"),24*1024*1024))throw new InvalidDataException();return;}
            if(domain=="native-mobile-station")
            {if(element.Attributes().Count()!=3||!Hex((string)element.Attribute("nativeBeforeProof"),64)||element.Elements().Count()!=1||!Scalar(element.Element("original"),4096))throw new InvalidDataException();return;}
            if(domain=="native-placed-station")
            {if(element.Attributes().Count()!=3||!Hex((string)element.Attribute("nativeBeforeProof"),64)||element.Elements().Count()!=1||!Scalar(element.Element("original"),4096)||
                !RebirthRemoteResourcePlacedStationReference.TryRead(element.Element("original").Value,out var placed)||placed.Job.ToString("N")!=(string)element.Attribute("child"))throw new InvalidDataException();return;}
            if(domain!="native-repair"||element.Attributes().Count()!=3||!Hex((string)element.Attribute("nativeBeforeProof"),64)||element.Elements().Count()!=5||
                !Scalar(element.Element("payment"),8192)||!Scalar(element.Element("repairInput"),262144)||!Scalar(element.Element("repairAmount"),5)||
                (int?)element.Element("repairAmount") is not int amount||amount<1||amount>ushort.MaxValue||
                !Scalar(element.Element("beforeActor"),8*1024*1024)||!Scalar(element.Element("afterActor"),8*1024*1024))throw new InvalidDataException();return;
        }
        if(element.Name=="ownerRefund")
        {
            var body=element.Element("repairRefund")??element.Element("blockRefund");
            var beltPolicy=body?.Element("beltPolicy");
            if(element.Attributes().Count()!=1||!Hex((string)element.Attribute("nativeBeforeProof"),64)||element.Elements().Count()!=1||body==null||body.Attributes().Count()!=(body.Attribute("deaths")==null?5:6)||
                body.Attribute("deaths")!=null&&((int?)body.Attribute("deaths") is not int refundDeaths||refundDeaths<0||body.Attribute("deaths").Value!=refundDeaths.ToString(CultureInfo.InvariantCulture))||
                !Hex((string)body.Attribute("original"),64)||!GuidValue((string)body.Attribute("source"))||!GuidValue((string)body.Attribute("child"))||
                !Hex((string)body.Attribute("binding"),64)||!Hex((string)body.Attribute("noUseProof"),64)||body.Elements().Count()!=(beltPolicy==null?2:3)||
                beltPolicy!=null&&(body.Name!="blockRefund"||!RebirthRemoteResourceBlockRefund.ReadBeltPolicy(beltPolicy,out _,out _,out _))||
                !Scalar(body.Element("beforeActor"),8*1024*1024)||!Scalar(body.Element("afterActor"),8*1024*1024))throw new InvalidDataException();return;
        }
        throw new InvalidDataException();
    }
    internal static long ReservedAllocation(Record record)=>checked((long)Encoding.UTF8.GetByteCount(Plan(record.Copy()).ToString(SaveOptions.DisableFormatting))+SettlementReserve);
    private static void Validate(XElement image,int? verifiedPlanBytes=null)
    {
        var rootAttributes=new[]{"version","operation","world","owner","creation","epoch","request","kind","deaths","phase","outcome","aborting","requirementsHash","ownerProof","disposition","ownerRejecting","ownerNoUseProof"};
        var rootElements=new[]{"requirements","sources","removed","reason","ownerAction","ownerRefund"};
        if(image==null||image.Attributes().Any(attribute=>!rootAttributes.Contains(attribute.Name.LocalName)||attribute.Name.NamespaceName.Length!=0)||
            image.Elements().Any(element=>!rootElements.Contains(element.Name.LocalName)||element.Name.NamespaceName.Length!=0)||
            image.Elements().GroupBy(element=>element.Name).Any(group=>group.Count()!=1))throw new InvalidDataException("Unknown or repeated original field.");
        if(image?.Name!="resourceOutcome"||(int?)image.Attribute("version")!=1||!Hex((string)image.Attribute("world"),64)||!Hex((string)image.Attribute("owner"),64)||
            !RebirthOriginalActionScope.Validate((string)image.Attribute("owner"),(string)image.Attribute("creation"))||
            !Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
            !ulong.TryParse((string)image.Attribute("epoch"),NumberStyles.None,CultureInfo.InvariantCulture,out var epoch)||epoch==0||
            !ulong.TryParse((string)image.Attribute("request"),NumberStyles.None,CultureInfo.InvariantCulture,out var request)||request==0||
            !int.TryParse((string)image.Attribute("kind"),out int kind)||kind<(int)RemoteResourceClientOperation.Craft||kind>(int)RemoteResourceClientOperation.CookingPull||
            Operation((string)image.Attribute("world"),(string)image.Attribute("owner"),(string)image.Attribute("creation"),epoch,request,(RemoteResourceClientOperation)kind)!=operation||
            (int?)image.Attribute("phase") is not int phase||phase<0||phase>4||(int?)image.Attribute("outcome") is not int outcome||outcome<0||outcome>2||phase<2&&outcome!=0||phase==2&&outcome!=1||phase==3&&outcome!=2||phase==4&&outcome==0||
            phase==4&&(!Hex((string)image.Attribute("ownerProof"),64)||!new[]{"used","refunded","grid-owned","unused-no-payment"}.Contains((string)image.Attribute("disposition")))||
            (int?)image.Attribute("deaths") is not int deaths||deaths<0||((int?)image.Attribute("aborting")??0)<0||((int?)image.Attribute("aborting")??0)>1||
            !Hex((string)image.Attribute("requirementsHash"),64)||image.Element("requirements")==null||image.Element("sources")==null||image.Element("removed")==null||
            image.Element("reason")?.Value.Length>512||image.Element("sources").Elements().Count()>MaximumSources)throw new InvalidDataException("Invalid retained resource original.");
        var sourceIds=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var source in image.Element("sources").Elements())
        {
            var sourceAttributes=new[]{"id","kind","incarnation","attempted","published","beforeProof","proof","restored","restoreProof","declined","refusalProof"};
            var sourceElements=new[]{"before","after","paid","locks","npc"};
            if(source.Attributes().Any(attribute=>!sourceAttributes.Contains(attribute.Name.LocalName)||attribute.Name.NamespaceName.Length!=0)||
                source.Elements().Any(element=>!sourceElements.Contains(element.Name.LocalName)||element.Name.NamespaceName.Length!=0)||source.Elements().GroupBy(element=>element.Name).Any(group=>group.Count()!=1)||
                (int?)source.Attribute("kind") is not int sourceKind||sourceKind<1||sourceKind>5||source.Element("before")==null||source.Element("after")==null||source.Element("paid")==null||source.Element("locks")==null||
                (sourceKind==5)!=(source.Element("npc")!=null)||sourceKind==5&&source.Attribute("incarnation")!=null||sourceKind!=5&&!Guid.TryParseExact((string)source.Attribute("incarnation"),"N",out _))throw new InvalidDataException("Unknown or repeated source field.");
            string id=(string)source.Attribute("id");int attempted=(int?)source.Attribute("attempted")??-1,published=(int?)source.Attribute("published")??-1;
            string beforeProof=(string)source.Attribute("beforeProof")??"",proof=(string)source.Attribute("proof")??"";
            int restored=(int?)source.Attribute("restored")??0,declined=(int?)source.Attribute("declined")??0;
            string restoreProof=(string)source.Attribute("restoreProof")??"",refusalProof=(string)source.Attribute("refusalProof")??"";
            if(source.Name!="source"||string.IsNullOrEmpty(id)||id.Length>512||!sourceIds.Add(id)||attempted<0||attempted>1||published<0||published>attempted||phase==0&&attempted!=0||outcome==1&&published!=1||
                beforeProof.Length!=0&&!Hex(beforeProof,64)||proof.Length!=0&&!Hex(proof,64)||(attempted>0||phase==1)&&beforeProof.Length==0||published>0&&proof.Length==0||published==0&&proof.Length!=0||
                restored<0||restored>1||declined<0||declined>1||restored+declined>1||restored==1&&(sourceKind==5||!Hex(restoreProof,64)||phase==0)||
                restored==0&&restoreProof.Length!=0||declined==1&&(sourceKind!=5||attempted!=1||published!=0||!Hex(refusalProof,64))||declined==0&&refusalProof.Length!=0||
                outcome==1&&(restored!=0||declined!=0)||phase>=2&&attempted==1&&published==0&&restored==0&&declined==0)
                throw new InvalidDataException("Invalid resource source stage.");
        }
        foreach(string name in new[]{"phase","outcome","aborting","ownerRejecting"})
            if(image.Attribute(name)!=null&&image.Attribute(name).Value!=((int)image.Attribute(name)).ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("Noncanonical stage scalar.");
        foreach(var source in image.Element("sources").Elements())foreach(string name in new[]{"attempted","published","restored","declined"})
            if(source.Attribute(name)!=null&&source.Attribute(name).Value!=((int)source.Attribute(name)).ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("Noncanonical source scalar.");
        if(image.Attribute("ownerProof")!=null&&!Hex((string)image.Attribute("ownerProof"),64)||image.Attribute("disposition")!=null&&
            !new[]{"used","refunded","grid-owned","unused-no-payment"}.Contains((string)image.Attribute("disposition")))throw new InvalidDataException();
        foreach(string field in new[]{"removed","reason","ownerAction","ownerRefund"})
            if(image.Element(field)!=null)ValidateSettlementElement(image.Element(field));
        if(((int?)image.Attribute("aborting")??0)==1&&(phase==0||outcome==1||image.Element("sources").Elements("source").Any(source=>(int)source.Attribute("kind")==5&&(int)source.Attribute("attempted")!=0)))throw new InvalidDataException("Invalid original source abort.");
        int rejecting=(int?)image.Attribute("ownerRejecting")??0;string noUse=(string)image.Attribute("ownerNoUseProof")??string.Empty;
        string disposition=(string)image.Attribute("disposition");
        bool partialPersonalRefund=(kind==(int)RemoteResourceClientOperation.Craft||kind==(int)RemoteResourceClientOperation.BlockRepair||kind==(int)RemoteResourceClientOperation.BlockUpgrade)&&outcome==2&&phase>=3&&image.Element("ownerAction")==null;
        if(rejecting<0||rejecting>1||rejecting==1&&((kind!=(int)RemoteResourceClientOperation.ItemRepair&&kind!=(int)RemoteResourceClientOperation.Craft&&kind!=(int)RemoteResourceClientOperation.BlockRepair&&kind!=(int)RemoteResourceClientOperation.BlockUpgrade)||phase<2||outcome!=1&&!partialPersonalRefund||phase==4&&disposition!="refunded"&&disposition!="used"||partialPersonalRefund&&phase==4&&disposition!="refunded")||
            noUse.Length!=0&&(!Hex(noUse,64)||rejecting!=1)||image.Element("ownerRefund")!=null&&(rejecting!=1||noUse.Length==0)||
            rejecting==1&&phase==4&&(disposition=="used"&&(noUse.Length!=0||image.Element("ownerRefund")!=null)||disposition=="refunded"&&(noUse.Length==0||image.Element("ownerRefund")==null)))throw new InvalidDataException("Invalid owner rejection/refund stage.");
        if((kind==(int)RemoteResourceClientOperation.BlockRepair||kind==(int)RemoteResourceClientOperation.BlockUpgrade)&&rejecting==1&&noUse.Length!=0&&image.Element("ownerAction")==null&&
            !RebirthRemoteResourceBlockRejection.ValidateUnissuedRootProof(image))throw new InvalidDataException("Invalid never-permitted Block proof.");
        if((kind==(int)RemoteResourceClientOperation.BlockRepair||kind==(int)RemoteResourceClientOperation.BlockUpgrade)&&rejecting==1&&noUse.Length!=0&&image.Element("ownerAction")!=null&&
            !RebirthRemoteResourceBlockRejection.ValidateDeclinedRootProof(image)&&!RebirthRemoteResourceBlockRejection.ValidateIssuedRootProof(image)&&!RebirthRemoteResourceBlockAdoption.ValidateEndedOwnerRoot(image))throw new InvalidDataException("Invalid pre-attempt declined Block proof.");
        if(image.Element("ownerRefund") is XElement refund&&
            ((kind==(int)RemoteResourceClientOperation.BlockRepair||kind==(int)RemoteResourceClientOperation.BlockUpgrade)?refund.Element("blockRefund")==null:refund.Element("repairRefund")==null))
            throw new InvalidDataException("Original refund purpose differs.");
        if(kind==(int)RemoteResourceClientOperation.Craft&&noUse.Length!=0&&
            RebirthRemoteResourceFetchedStationOriginal.IsMappedRoot(new Record(operation,0,image))&&
            !RebirthRemoteResourceFetchedStationOriginal.ValidateFetchedNoUseRoot(image))throw new InvalidDataException("Invalid fetched station no-use proof.");
        byte[] requirements=Convert.FromBase64String(image.Element("requirements").Value);
        using(var hash=SHA256.Create())if(BitConverter.ToString(hash.ComputeHash(requirements)).Replace("-","").ToLowerInvariant()!=(string)image.Attribute("requirementsHash"))throw new InvalidDataException("Original requirements hash differs.");
        var requested=RebirthCamperInventorySnapshot.Restore(requirements);if(requested.Length<1||requested.Length>4096||requested.Any(item=>item==null||item.IsEmpty()||item.count<1))throw new InvalidDataException();
        byte[] removedBytes=Convert.FromBase64String(image.Element("removed").Value);
        if(removedBytes.Length>6*1024*1024||Convert.ToBase64String(removedBytes)!=image.Element("removed").Value)throw new InvalidDataException();
        var removed=RebirthCamperInventorySnapshot.Restore(removedBytes);
        var desired=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
        void Add(System.Collections.Generic.Dictionary<string,long> totals,ItemStack[] stacks)
        {
            foreach(var item in stacks)
            {
                if(item==null||item.count<0)throw new InvalidDataException();if(item.IsEmpty())continue;
                string key=RebirthNativeItemCodec.Encode(item.itemValue);totals.TryGetValue(key,out long count);totals[key]=checked(count+item.count);
            }
        }
        if(phase>=2)foreach(var source in image.Element("sources").Elements("source").Where(node=>(int)node.Attribute("published")==1&&((int?)node.Attribute("restored")??0)==0))
            Add(desired,RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid")?.Value??throw new InvalidDataException())));
        var actual=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);Add(actual,removed);
        if(actual.Count!=desired.Count||desired.Any(pair=>!actual.TryGetValue(pair.Key,out long count)||count!=pair.Value))throw new InvalidDataException("Outcome differs from original saved source payments.");
        if(outcome==1)
        {
            System.Collections.Generic.Dictionary<int,long> Counts(ItemStack[] stacks)
            {
                var result=new System.Collections.Generic.Dictionary<int,long>();foreach(var item in stacks.Where(item=>!item.IsEmpty()))
                {result.TryGetValue(item.itemValue.type,out long count);result[item.itemValue.type]=checked(count+item.count);}return result;
            }
            var needed=Counts(requested);var paid=Counts(removed);
            if(needed.Count!=paid.Count||needed.Any(pair=>!paid.TryGetValue(pair.Key,out long count)||count!=pair.Value))throw new InvalidDataException("Successful original lacks complete source payment.");
        }
        int planBytes=verifiedPlanBytes??Encoding.UTF8.GetByteCount(Plan(image).ToString(SaveOptions.DisableFormatting));
        if(planBytes<1||planBytes>RebirthRemoteResourceJournalFormat.MaximumPlan||planBytes+SettlementReserve>MaximumBytes||
            !verifiedPlanBytes.HasValue&&Encoding.UTF8.GetByteCount(image.ToString(SaveOptions.DisableFormatting))>planBytes+SettlementReserve)throw new InvalidDataException();
    }
    private static byte[] Encode(Record record)
    {
        Validate(record.Copy());byte[] body=Encoding.UTF8.GetBytes(record.Copy().ToString(SaveOptions.DisableFormatting));
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(Magic);writer.Write(record.Operation.ToByteArray());writer.Write(record.Revision);writer.Write(body.Length);writer.Write(body);writer.Flush();
            using(var hash=SHA256.Create())writer.Write(hash.ComputeHash(stream.ToArray()));writer.Flush();return stream.ToArray();
        }
    }
    private static Record ReadFile(string path,Guid expected,long revision,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget=null)
    {
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var header=new BinaryReader(input))
            if(header.ReadUInt32()==RebirthRemoteResourceJournalFormat.StageMagic)return RebirthRemoteResourceJournalFormat.Read(path,expected,revision,budget);
        int legacyMaximum=budget?.LegacyEnvelopeBytes??MaximumLegacyBytes;
        var file=new FileInfo(path);if(!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<64||file.Length>legacyMaximum+128L)throw new InvalidDataException();
        if(budget?.GraphWorkspace>0&&checked(4*file.Length)>budget.GraphWorkspace)throw new InvalidDataException("Captured legacy frame workspace exceeded.");
        byte[] bytes=File.ReadAllBytes(path);
        using(var hash=SHA256.Create())if(!hash.ComputeHash(bytes,0,bytes.Length-32).SequenceEqual(bytes.Skip(bytes.Length-32)))throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,0,bytes.Length-32,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(reader.ReadUInt32()!=Magic||new Guid(reader.ReadBytes(16))!=expected||reader.ReadInt64()!=revision)throw new InvalidDataException();
            int count=reader.ReadInt32();if(count<1||count>legacyMaximum||count!=stream.Length-stream.Position)throw new InvalidDataException();
            string text=new UTF8Encoding(false,true).GetString(reader.ReadBytes(count));
            RebirthRemoteResourceJournalFormat.ValidateHistoricalXmlAllocation(text,legacyMaximum,bytes.LongLength,budget);
            using(var input=new StringReader(text))using(var xml=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=legacyMaximum}))
            {
                var image=XDocument.Load(xml,LoadOptions.None).Root;
                if(budget?.GraphWorkspace>0&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitHistoricalSourceImage(image,budget.GraphWorkspace,0))throw new InvalidDataException("Captured legacy native workspace exceeded.");
                Validate(image);
                if((string)image.Attribute("operation")!=expected.ToString("N"))throw new InvalidDataException();return new Record(expected,revision,image);
            }
        }
    }
    internal static string[] OrderCommitted(System.Collections.Generic.IEnumerable<string> files)=>files
        .Where(path=>Path.GetFileName(path).EndsWith(".rrj",StringComparison.Ordinal))
        .OrderBy(path=>Path.GetFileName(path),StringComparer.Ordinal).ToArray();
    internal static bool NeverPreparedOwnerAction(Record original)
        =>NeverPreparedOwnerActionCore(original,null);
    internal static bool NeverPreparedPlacedOwnerActionBounded(Record original,RebirthRemoteResourcePlacedStationArtifactStore.Header allocation)
    {
        try
        {
            if(original==null||allocation==null||allocation.Source!=original.Operation)return false;
            int legacy=checked((int)Math.Min(MaximumLegacyBytes,allocation.SourceBytes+SettlementReserve));
            return NeverPreparedOwnerActionCore(original,new RebirthRemoteResourceJournalFormat.HistoricalReadBudget((int)allocation.SourceBytes,legacy,allocation.SourceWorkspace));
        }
        catch{return false;}
    }
    private static bool NeverPreparedOwnerActionCore(Record original,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget)
    {
        try
        {
            if(original==null||!TryReadCore(original.Owner,original.Operation,budget,out var current,out _)||current==null||current.Binding!=original.Binding||current.HasOwnerAction)return false;
            string directory=DirectoryFor(original.Owner),prefix=original.Operation.ToString("N")+".";
            string archive=Path.Combine(directory,"completed",original.Operation.ToString("N"));
            var paths=Directory.EnumerateFiles(directory,prefix+"*").Concat(Directory.Exists(archive)?Directory.EnumerateFiles(archive):new string[0]).ToArray();
            foreach(string path in paths)
            {
                string name=Path.GetFileName(path);
                if(name.StartsWith(prefix+"blob.ownerAction.",StringComparison.Ordinal))return false;
                if(RebirthRemoteResourceJournalFormat.IsArtifact(name,original.Operation,out _))continue;
                if(name.Length<prefix.Length+20||!long.TryParse(name.Substring(prefix.Length,20),NumberStyles.None,CultureInfo.InvariantCulture,out long revision)||
                    ReadFile(path,original.Operation,revision,budget).HasOwnerAction)return false;
            }
            string failed=Path.Combine(directory,"failedCandidates");
            // Failed publication never issued permission, but an unknown
            // prepared owner-action image still excludes this special branch.
            return !Directory.Exists(failed)||(File.GetAttributes(failed)&FileAttributes.ReparsePoint)==0&&!Directory.EnumerateFiles(failed,prefix+"*").Any();
        }
        catch{return false;}
    }
    // Readonly canonical history, after the ordinary whole-original reader has
    // validated all generations/candidates. Never an older-outcome fallback.
    internal static bool TryReadRevision(Record original,long revision,out Record record)
        =>TryReadRevisionCore(original,revision,null,out record);
    internal static bool TryReadPreparedRevisionBounded(Record original,long revision,long originalPreparedBytes,out Record record)
        =>TryReadPreparedRevisionBounded(original,revision,originalPreparedBytes,0,out record);
    internal static bool TryReadPlacedHistoryBounded(string owner,Guid operation,RebirthRemoteResourcePlacedStationArtifactStore.Header allocation,out Record record,out bool unknown)
    {
        record=null;unknown=true;
        try
        {
            if(allocation==null||allocation.Source!=operation||allocation.SourceBytes<1||allocation.SourceBytes>RebirthRemoteResourceJournalFormat.MaximumPlan||
                allocation.SourceWorkspace<1||allocation.SourceWorkspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            int legacy=checked((int)Math.Min(MaximumLegacyBytes,allocation.SourceBytes+SettlementReserve));
            return TryReadCore(owner,operation,new RebirthRemoteResourceJournalFormat.HistoricalReadBudget((int)allocation.SourceBytes,legacy,allocation.SourceWorkspace),out record,out unknown);
        }
        catch{record=null;unknown=true;return false;}
    }
    internal static bool TryReadPreparedRevisionBounded(Record original,long revision,long originalPreparedBytes,long graphWorkspace,out Record record)
    {
        record=null;
        try
        {
            if(originalPreparedBytes<1||originalPreparedBytes>RebirthRemoteResourceJournalFormat.MaximumPlan)return false;
            // The immutable plan cannot exceed its positively retained full
            // prepared original. Legacy full envelopes separately reserve the
            // original domain's accepted settlement growth, not current size.
            int legacy=checked((int)Math.Min(MaximumLegacyBytes,originalPreparedBytes+SettlementReserve));
            return TryReadRevisionCore(original,revision,new RebirthRemoteResourceJournalFormat.HistoricalReadBudget((int)originalPreparedBytes,legacy,graphWorkspace),out record);
        }
        catch{record=null;return false;}
    }
    private static bool TryReadRevisionCore(Record original,long revision,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget,out Record record)
    {
        record=null;
        try
        {
            if(original==null||revision<0||!TryReadCore(original.Owner,original.Operation,budget,out var current,out _)||
                current==null||current.Binding!=original.Binding||revision>current.Revision)return false;
            string directory=DirectoryFor(original.Owner),name=original.Operation.ToString("N")+"."+revision.ToString("D20",CultureInfo.InvariantCulture)+".rrj";
            string primary=Path.Combine(directory,name),archived=Path.Combine(directory,"completed",original.Operation.ToString("N"),name);
            string path=File.Exists(primary)?primary:File.Exists(archived)?archived:null;if(path==null)return false;
            var value=ReadFile(path,original.Operation,revision,budget);
            if(value.Owner!=current.Owner||value.World!=current.World||value.Creation!=current.Creation||value.Binding!=current.Binding||value.Revision!=revision)return false;
            record=value;return true;
        }
        catch{return false;}
    }
    // Absence is stronger than a null primary read: failed initial publication
    // and interrupted artifacts also exclude a never-admitted refusal.
    internal static bool IsKnownAbsentIncludingCandidates(string owner,Guid operation)
    {
        try
        {
            if(operation==Guid.Empty||!TryRead(owner,operation,out var record,out bool unknown)||unknown||record!=null)return false;
            string directory=DirectoryFor(owner),prefix=operation.ToString("N");
            if(!Directory.Exists(directory))return true;
            if(Directory.EnumerateFileSystemEntries(directory,prefix+"*").Any())return false;
            foreach(string name in new[]{"completed","failedCandidates"})
            {
                string child=Path.Combine(directory,name);if(!Directory.Exists(child))continue;
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(child,prefix+"*").Any())return false;
            }
            return true;
        }
        catch{return false;}
    }
    internal static bool TryRead(string owner,Guid operation,out Record record,out bool unknown)
        =>TryReadCore(owner,operation,null,out record,out unknown);
    private static bool TryReadCore(string owner,Guid operation,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget,out Record record,out bool unknown)
    {
        record=null;unknown=false;
        try
        {
            using var artifactSession=RebirthRemoteResourceJournalFormat.BeginRead(budget);
            if(operation==Guid.Empty)throw new InvalidDataException();string directory=DirectoryFor(owner);if(!Directory.Exists(directory))return true;
            string prefix=operation.ToString("N")+".";string completed=Path.Combine(directory,"completed",operation.ToString("N"));
            if(Directory.Exists(Path.GetDirectoryName(completed))&&(File.GetAttributes(Path.GetDirectoryName(completed))&FileAttributes.ReparsePoint)!=0||
                Directory.Exists(completed)&&((File.GetAttributes(completed)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateDirectories(completed).Any()))throw new InvalidDataException();
            var candidates=Directory.EnumerateFiles(directory,prefix+"*").Concat(Directory.Exists(completed)?Directory.EnumerateFiles(completed):new string[0]).ToArray();
            if(candidates.Length>MaximumGenerations*8+64)throw new InvalidDataException();
            var files=candidates.GroupBy(Path.GetFileName,StringComparer.Ordinal).Select(group=>
            {
                var copies=group.ToArray();if(copies.Length>2||copies.Skip(1).Any(path=>!RebirthRemoteResourceJournalFormat.SameFiles(copies[0],path)))throw new InvalidDataException();
                return copies[0];
            }).ToArray();
            if(files.Length>MaximumGenerations*4+32)throw new InvalidDataException();
            var artifacts=files.Where(path=>RebirthRemoteResourceJournalFormat.IsArtifact(Path.GetFileName(path),operation,out _)).ToArray();
            RebirthRemoteResourceJournalFormat.ValidateArtifactSet(artifacts,operation);
            long artifactBytes=0;var artifactNames=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(string artifact in artifacts)
            {
                RebirthRemoteResourceJournalFormat.IsArtifact(Path.GetFileName(artifact),operation,out string name);
                if(artifactNames.Add(name))artifactBytes=checked(artifactBytes+new FileInfo(artifact).Length);
                if(artifactBytes>RebirthRemoteResourceJournalFormat.MaximumArtifacts||artifacts.Length>MaximumGenerations*2+16)throw new InvalidDataException();
                RebirthRemoteResourceJournalFormat.ValidateArtifact(artifact,operation,budget);
            }
            // A terminal archive may be interrupted between generations. The
            // union is ordered by the original generation name, never by its
            // current primary/archive directory.
            var committed=OrderCommitted(files);
            // An orphan candidate can be a failed initial publication, not absence.
            if(committed.Length==0){if(files.Length!=0||Directory.Exists(completed))throw new InvalidDataException();return true;}
            if(committed.Length>MaximumGenerations)throw new InvalidDataException();Record previous=null;
            for(int index=0;index<committed.Length;index++)
            {
                string expected=prefix+index.ToString("D20",CultureInfo.InvariantCulture)+".rrj";
                if(Path.GetFileName(committed[index])!=expected)throw new InvalidDataException();var current=ReadFile(committed[index],operation,index,budget);
                if(current.Owner!=owner||previous!=null&&!current.Follows(previous))throw new InvalidDataException();previous=current;
            }
            // Candidate files are never replay authority. Only an exact copy of
            // a validated committed generation may coexist without ambiguity.
            foreach(string path in files.Except(committed).Except(artifacts))
            {
                var candidate=Path.GetFileName(path);int separator=candidate.IndexOf('.',prefix.Length);
                if(separator<0||!long.TryParse(candidate.Substring(prefix.Length,separator-prefix.Length),NumberStyles.None,CultureInfo.InvariantCulture,out long revision)||revision<0||revision>previous.Revision+1)
                    throw new InvalidDataException();
                if(revision==previous.Revision+1)
                {
                    var staged=ReadFile(path,operation,revision,budget);
                    if(staged.Owner!=owner||!staged.Follows(previous))throw new InvalidDataException();
                    continue; // Staged state is never adopted or returned as authority.
                }
                string retained=committed.Single(path=>Path.GetFileName(path)==prefix+revision.ToString("D20",CultureInfo.InvariantCulture)+".rrj");
                if(!RebirthRemoteResourceJournalFormat.SameFiles(path,retained))throw new InvalidDataException();
            }
            if(Directory.Exists(completed)&&previous.Phase!=4)throw new InvalidDataException();
            record=previous;return true;
        }
        catch{record=null;unknown=true;return false;}
    }
    // Enumerate originals, not arbitrary snapshots. Any unidentifiable primary
    // or sidecar quarantines new admissions; known endpoint holds remain owned
    // by their already retained originals. No corrupt candidate is skipped.
    internal static bool TryReadPending(World world,out Record[] records)
        =>TryReadPendingCore(world,null,Guid.Empty,out records);
    private static bool TryReadPendingCore(World world,string ignoredOwner,Guid ignoredOperation,out Record[] records)
    {
        records=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
                !RemoteResourceWorldIdentity.TryGet(world,out string worldId))return false;
            string save=GameIO.GetSaveGameDir();if(string.IsNullOrEmpty(save))return false;
            string root=Path.Combine(Path.GetFullPath(save),"RebirthData","RemoteResources","Outcomes");
            if(!Directory.Exists(root)){records=new Record[0];return true;}
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFiles(root).Any())return false;
            var pending=new System.Collections.Generic.List<Record>();int owners=0;long reserved=0;
            foreach(string directory in Directory.EnumerateDirectories(root))
            {
                if(++owners>4096)return false;
                string owner=Path.GetFileName(directory);if(!Hex(owner,64)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)return false;
                // failedCandidates contains only this writer's pre-permit
                // failures; it never supplies replay or payment authority.
                foreach(string child in Directory.EnumerateDirectories(directory))
                    if(!new[]{"failedCandidates","completed"}.Contains(Path.GetFileName(child))||(File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)return false;
                var operations=new System.Collections.Generic.HashSet<Guid>();
                foreach(string path in Directory.EnumerateFiles(directory))
                {
                    var file=new FileInfo(path);string name=file.Name;
                    if((file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length<1||name.Length<33||name[32]!='.'||
                        !Guid.TryParseExact(name.Substring(0,32),"N",out Guid operation)||operation==Guid.Empty||name.Substring(0,32)!=operation.ToString("N"))return false;
                    operations.Add(operation);if(operations.Count>4096)return false;
                }
                foreach(Guid operation in operations)
                {
                    if(owner==ignoredOwner&&operation==ignoredOperation)continue;
                    if(!TryRead(owner,operation,out var record,out _)||record==null||record.World!=worldId)return false;
                    if(record.Phase<4){if(pending.Count>=4096||(reserved=checked(reserved+ReservedAllocation(record)))>256L*1024*1024)return false;pending.Add(record);}
                }
            }
            if(!ReferenceEquals(GameManager.Instance?.World,world)||GameIO.GetSaveGameDir()!=save||!RemoteResourceWorldIdentity.TryGet(world,out string current)||current!=worldId)return false;
            records=pending.ToArray();return true;
        }
        catch{records=null;return false;}
    }
    internal static bool TryPrepare(XElement original,Func<bool> authenticatedCurrent,out Record record)
    {
        record=null;if(publishing||authenticatedCurrent?.Invoke()!=true)return false;
        try
        {
            Validate(original);if((int)original.Attribute("phase")!=0)return false;Guid operation=Guid.ParseExact((string)original.Attribute("operation"),"N");string owner=(string)original.Attribute("owner");
            if(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(operation))return false;
            if(!TryRead(owner,operation,out var existing,out _)||authenticatedCurrent()!=true)
            {
                // Only an identical stage-zero writer can finish artifacts
                // that never yielded any committed effect permit.
                string directory=DirectoryFor(owner);
                var candidate=new Record(operation,0,original);
                if(authenticatedCurrent()!=true||!Directory.Exists(directory)||!RebirthRemoteResourceJournalFormat.CanResumePrepared(directory,candidate)||!CanReserveFresh(candidate))return false;
                return Publish(candidate,authenticatedCurrent,out record);
            }
            if(existing!=null){if(existing.Binding!=Digest(Plan(original).ToString(SaveOptions.DisableFormatting)))return false;record=existing;return true;}
            var fresh=new Record(operation,0,original);if(!CanReserveFresh(fresh))return false;
            return Publish(fresh,authenticatedCurrent,out record);
        }
        catch{return false;}
    }
    private static bool CanReserveFresh(Record original)
    {
        if(!TryReadPendingCore(GameManager.Instance?.World,original.Owner,original.Operation,out var pending))return false;
        long reservation=ReservedAllocation(original);
        foreach(var retained in pending)reservation=checked(reservation+ReservedAllocation(retained));
        return reservation<=256L*1024*1024;
    }
    internal static bool TryAdvance(Record expected,Action<XElement> change,Func<bool> authenticatedCurrent,out Record record)
        =>TryAdvanceCore(expected,change,authenticatedCurrent,null,out record);
    internal static bool TryAdvancePlacedBounded(Record expected,Action<XElement> change,Func<bool> authenticatedCurrent,
        RebirthRemoteResourcePlacedStationArtifactStore.Header allocation,out Record record)
    {
        record=null;
        try
        {
            if(expected==null||allocation==null||allocation.Source!=expected.Operation||allocation.SourceBytes<1||allocation.SourceBytes>RebirthRemoteResourceJournalFormat.MaximumPlan||
                allocation.SourceWorkspace<1||allocation.SourceWorkspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            int legacy=checked((int)Math.Min(MaximumLegacyBytes,allocation.SourceBytes+SettlementReserve));
            return TryAdvanceCore(expected,change,authenticatedCurrent,new RebirthRemoteResourceJournalFormat.HistoricalReadBudget((int)allocation.SourceBytes,legacy,allocation.SourceWorkspace),out record);
        }
        catch{return false;}
    }
    private static bool TryAdvanceCore(Record expected,Action<XElement> change,Func<bool> authenticatedCurrent,
        RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget,out Record record)
    {
        record=null;if(expected==null||change==null||publishing||authenticatedCurrent?.Invoke()!=true)return false;
        try
        {
            if(!TryReadCore(expected.Owner,expected.Operation,budget,out var current,out _)||current==null||current.Revision!=expected.Revision||current.Binding!=expected.Binding||current.Phase>=4)return false;
            var image=current.Copy();change(image);
            if(budget?.GraphWorkspace>0&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitHistoricalSourceImage(image,budget.GraphWorkspace,0))return false;
            Validate(image);if(Digest(Plan(image).ToString(SaveOptions.DisableFormatting))!=current.Binding||!StateAdvances(current.Copy(),image))return false;
            if(!SourceStagesAdvance(current.Copy(),image))return false;
            return Publish(new Record(current.Operation,checked(current.Revision+1),image),authenticatedCurrent,out record,budget);
        }
        catch{return false;}
    }
    private static bool SourceStagesAdvance(XElement before,XElement after)
    {
        foreach(var source in after.Element("sources").Elements("source"))
        {
            var old=before.Element("sources").Elements("source").Single(node=>(string)node.Attribute("id")== (string)source.Attribute("id"));
            if((int)source.Attribute("attempted")<(int)old.Attribute("attempted")||(int)source.Attribute("published")<(int)old.Attribute("published"))return false;
            if(((int?)source.Attribute("restored")??0)<((int?)old.Attribute("restored")??0)||((int?)source.Attribute("declined")??0)<((int?)old.Attribute("declined")??0))return false;
            foreach(string name in new[]{"beforeProof","proof","restoreProof","refusalProof"})
            {string previous=(string)old.Attribute(name)??"";if(previous.Length!=0&&previous!=((string)source.Attribute(name)??""))return false;}
        }
        return true;
    }
    private static bool StateAdvances(XElement before,XElement after)
    {
        int old=(int)before.Attribute("phase"),next=(int)after.Attribute("phase");if(old>=4||next<old)return false;
        if(((int?)after.Attribute("aborting")??0)<((int?)before.Attribute("aborting")??0))return false;
        if(((int?)after.Attribute("ownerRejecting")??0)<((int?)before.Attribute("ownerRejecting")??0))return false;
        string oldNoUse=(string)before.Attribute("ownerNoUseProof")??string.Empty;if(oldNoUse.Length!=0&&oldNoUse!=((string)after.Attribute("ownerNoUseProof")??string.Empty))return false;
        if(old>=2&&(next!=old&&next!=4||(int)before.Attribute("outcome")!=(int)after.Attribute("outcome")||
            !XNode.DeepEquals(before.Element("removed"),after.Element("removed"))||!XNode.DeepEquals(before.Element("reason"),after.Element("reason"))||
            !ExactXmlImageEquals(before.Element("sources"),after.Element("sources"))))return false;
        var previousAction=before.Element("ownerAction");if(previousAction!=null&&!XNode.DeepEquals(previousAction,after.Element("ownerAction")))return false;
        var previousRefund=before.Element("ownerRefund");if(previousRefund!=null&&!XNode.DeepEquals(previousRefund,after.Element("ownerRefund")))return false;
        return true;
    }
    private static bool ExactXmlImageEquals(XElement before,XElement after)
    {
        if(!XNode.DeepEquals(before,after))return false;
        var a=before.DescendantsAndSelf().SelectMany(element=>element.Attributes()).ToArray();
        var b=after.DescendantsAndSelf().SelectMany(element=>element.Attributes()).ToArray();
        return a.Length==b.Length&&a.Select((attribute,index)=>attribute.Name==b[index].Name&&attribute.Value==b[index].Value).All(value=>value);
    }
    private static bool Publish(Record candidate,Func<bool> current,out Record record,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget=null)
    {
        record=null;if(publishing||current?.Invoke()!=true||candidate.Revision>=MaximumGenerations)return false;publishing=true;string temporary=null;
        try
        {
            // Includes failed/candidate writes and immutable-plan publication.
            // A transition seal must never overlook a same-original retry.
            System.Threading.Interlocked.Increment(ref mutationEpoch);
            string directory=DirectoryFor(candidate.Owner);Directory.CreateDirectory(directory);string path=Path.Combine(directory,candidate.Operation.ToString("N")+"."+candidate.Revision.ToString("D20",CultureInfo.InvariantCulture)+".rrj");
            byte[] bytes=RebirthRemoteResourceJournalFormat.Encode(candidate,directory,current,true,budget);
            foreach(string retained in Directory.EnumerateFiles(directory,Path.GetFileName(path)+".*.tmp"))
                if(!RebirthRemoteResourceJournalFormat.MatchesFile(retained,bytes))return false;
            temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(current()!=true)return false;File.Move(temporary,path);
            if(!TryReadCore(candidate.Owner,candidate.Operation,budget,out var saved,out _)||saved==null||saved.Revision!=candidate.Revision||saved.Owner!=candidate.Owner||saved.Binding!=candidate.Binding||current()!=true)return false;
            record=saved;if(saved.Phase==4)ArchiveTerminal(saved,budget);return true;
        }
        catch
        {
            // This invocation never returned a saved-before-effect permit. Keep
            // its failed staging bytes recoverable without making a later exact
            // retry confuse this known failed write with a committed effect.
            try
            {
                if(temporary!=null&&File.Exists(temporary))
                {string failed=Path.Combine(Path.GetDirectoryName(temporary),"failedCandidates");Directory.CreateDirectory(failed);File.Move(temporary,Path.Combine(failed,Path.GetFileName(temporary)));}
            }
            catch{ /* Unknown staging remains in place and quarantines admission. */ }
            return false;
        }
        finally
        {
            try
            {
                if(temporary!=null&&File.Exists(temporary))
                {string failed=Path.Combine(Path.GetDirectoryName(temporary),"failedCandidates");Directory.CreateDirectory(failed);File.Move(temporary,Path.Combine(failed,Path.GetFileName(temporary)));}
            }
            catch{ /* Preserve unknown staging exactly where the failed move left it. */ }
            publishing=false;
        }
    }
    private static void ArchiveTerminal(Record terminal,RebirthRemoteResourceJournalFormat.HistoricalReadBudget budget=null)
    {
        // Only an already positively saved terminal original is moved. Exact
        // lookup reads the union during an interrupted move; no generation is
        // deleted and a conflict in either location remains unknown.
        try
        {
            if(terminal?.Phase!=4||!TryReadCore(terminal.Owner,terminal.Operation,budget,out var actual,out _)||actual?.Phase!=4||actual.Revision!=terminal.Revision||actual.Binding!=terminal.Binding)return;
            string directory=DirectoryFor(terminal.Owner),prefix=terminal.Operation.ToString("N")+".";
            string completed=Path.Combine(directory,"completed");if(Directory.Exists(completed)&&(File.GetAttributes(completed)&FileAttributes.ReparsePoint)!=0)return;
            string archive=Path.Combine(completed,terminal.Operation.ToString("N"));Directory.CreateDirectory(archive);
            if((File.GetAttributes(archive)&FileAttributes.ReparsePoint)!=0)return;
            foreach(string source in Directory.EnumerateFiles(directory,prefix+"*"))
            {
                string destination=Path.Combine(archive,Path.GetFileName(source));
                if(File.Exists(destination))return; // Retain both for exact reader validation.
                File.Move(source,destination);
            }
        }
        catch{/* Terminal proof remains in the root/archive union, never erased. */}
    }
}
