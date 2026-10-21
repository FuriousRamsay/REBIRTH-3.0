using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private const int ExpandedHydrationMetadataMaximum=4*RebirthWorkstationCompletionPublication.MaximumBytes+65536;
    private const int PackedHydrationMetadataMaximum=NetPackageRebirthUiDeferredHydrationV1.Maximum-NetPackageRebirthUiDeferredHydrationV1.NativeEnvelopeMaximum;
    private static readonly XNamespace hydrationBlobNamespace="urn:rebirth:completion:hydration:metadata:1";
    private sealed class HydrationMetadataCounter:Stream
    {
        internal long Size;public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>Size;public override long Position{get=>Size;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int count){if(count<0||Size>ExpandedHydrationMetadataMaximum-count)throw new InvalidDataException();Size+=count;}
        public override void WriteByte(byte value){if(Size>=ExpandedHydrationMetadataMaximum)throw new InvalidDataException();Size++;}
        public override void Flush(){}public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long value,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    internal static long MeasureHydrationMetadataElement(XElement image)
    {
        using(var stream=new HydrationMetadataCounter())
        {using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,CloseOutput=false}))image.WriteTo(writer);return stream.Size;}
    }
    private static bool ValidHydrationDigest(string digest)=>digest?.Length==64&&digest.All(value=>value>='0'&&value<='9'||value>='a'&&value<='f');
    private static bool PackHydrationMetadata(HydrationDispatch source,XElement header,out byte[] selector,out byte[] metadata)
    {
        selector=metadata=null;long measured=MeasureHydrationMetadataElement(header);
        if(measured>ExpandedHydrationMetadataMaximum||!source.ReserveMetadata(measured))return false;
        var full=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting));
        if(full.Length<=65536){selector=full;return true;}
        if(full.Length>ExpandedHydrationMetadataMaximum)return false;
        var template=new XElement(header);var values=new Dictionary<string,string>(StringComparer.Ordinal);var blobs=new XElement("blobs");
        foreach(var leaf in template.DescendantsAndSelf().Where(value=>!value.HasElements).ToArray())
        {
            if(leaf.Attributes().Any(value=>value.Name.Namespace==hydrationBlobNamespace||value.IsNamespaceDeclaration&&value.Value==hydrationBlobNamespace.NamespaceName))return false;
            string text=leaf.Value;if(Encoding.UTF8.GetByteCount(text)<256)continue;
            if(!values.TryGetValue(text,out string digest))
            {digest=HydrationHash(Encoding.UTF8.GetBytes(text));values.Add(text,digest);blobs.Add(new XElement("blob",new XAttribute("digest",digest),text));}
            leaf.Value="";leaf.Add(new XAttribute(hydrationBlobNamespace+"ref",digest));
        }
        var packed=new XElement("hydrationMetadata",new XAttribute("version",1),new XAttribute("group",source.Group.ToString("N")),
            new XAttribute("expanded",full.Length),new XAttribute("digest",HydrationHash(full)),new XElement("header",template),blobs);
        metadata=Encoding.UTF8.GetBytes(packed.ToString(SaveOptions.DisableFormatting));if(metadata.Length>PackedHydrationMetadataMaximum)return false;
        selector=Encoding.UTF8.GetBytes(new XElement("deferredHydrationMetadataSelector",new XAttribute("version",1),
            new XAttribute("group",source.Group.ToString("N")),new XAttribute("digest",HydrationHash(metadata)),
            new XAttribute("expanded",full.Length),new XAttribute("header",HydrationHash(full))).ToString(SaveOptions.DisableFormatting));return true;
    }
    private sealed class HydrationMetadataReady:IDisposable
    {
        internal Guid Group;internal World World;internal INetConnection Connection;internal string Digest,HeaderDigest;
        internal XElement Header;internal int Expanded,Total;internal IDisposable Budget;internal double Created;
        public void Dispose(){Header=null;Budget?.Dispose();Budget=null;}
    }
    private static readonly Dictionary<Guid,ClientAssembly> assemblingHydrationMetadata=new Dictionary<Guid,ClientAssembly>();
    private static readonly Dictionary<Guid,HydrationMetadataReady> readyHydrationMetadata=new Dictionary<Guid,HydrationMetadataReady>();
    private sealed class WaitingMetadataNative:IDisposable
    {
        internal ClientAssembly Original;internal byte[] Image;internal GameManager Callbacks;internal string MetadataDigest;
        public void Dispose(){Image=null;Callbacks=null;if(Original!=null){Original.Parts=null;Original.Budget?.Dispose();Original.Budget=null;Original=null;}}
    }
    private static readonly Dictionary<Guid,WaitingMetadataNative> waitingMetadataNative=new Dictionary<Guid,WaitingMetadataNative>();
    private static bool WaitForHydrationMetadata(ClientAssembly source,byte[] image,GameManager callbacks)
    {
        using(var stream=new MemoryStream(image,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            if(reader.ReadUInt32()!=0x31484455)throw new InvalidDataException();
            var selector=ReadHydrationMetadataXml(Exact(reader,65536),65536);
            if(selector.Name!="deferredHydrationMetadataSelector")return false;
            if(selector.Attributes().Count()!=5||selector.HasElements||(int?)selector.Attribute("version")!=1||ReadGuid(selector,"group")!=source.Group||
                !ValidHydrationDigest((string)selector.Attribute("digest"))||!ValidHydrationDigest((string)selector.Attribute("header"))||
                (int?)selector.Attribute("expanded") is not int expanded||expanded<65537||expanded>ExpandedHydrationMetadataMaximum)throw new InvalidDataException();
            if(readyHydrationMetadata.ContainsKey(source.Group))return false;
            if(waitingMetadataNative.Count>=4||ReadGuid(selector,"group")!=source.Group||
                waitingMetadataNative.Values.Any(value=>ReferenceEquals(value.Original.Connection,source.Connection)))throw new InvalidDataException();
            waitingMetadataNative.Add(source.Group,new WaitingMetadataNative{Original=source,Image=image,Callbacks=callbacks,MetadataDigest=(string)selector.Attribute("digest")});return true;
        }
    }
    private static void SweepHydrationMetadata(World world)
    {
        double now=UnityEngine.Time.realtimeSinceStartup;
        foreach(var pair in assemblingHydrationMetadata.Where(value=>!ReferenceEquals(value.Value.World,world)||value.Value.Connection.IsDisconnected()||now-value.Value.Created>30d).ToArray())
        {assemblingHydrationMetadata.Remove(pair.Key);pair.Value.Parts=null;pair.Value.Budget?.Dispose();pair.Value.Budget=null;}
        foreach(var pair in readyHydrationMetadata.Where(value=>!ReferenceEquals(value.Value.World,world)||value.Value.Connection.IsDisconnected()||now-value.Value.Created>30d).ToArray())
        {readyHydrationMetadata.Remove(pair.Key);pair.Value.Dispose();}
        foreach(var pair in waitingMetadataNative.Where(value=>!ReferenceEquals(value.Value.Original.World,world)||value.Value.Original.Connection.IsDisconnected()||now-value.Value.Original.Created>30d).ToArray())
        {waitingMetadataNative.Remove(pair.Key);pair.Value.Dispose();}
    }
    private static XElement ReadHydrationMetadataXml(byte[] image,int maximum)
    {
        if(image==null||image.Length<1||image.Length>maximum)throw new InvalidDataException();
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maximum,MaxCharactersFromEntities=0};
        using(var stream=new MemoryStream(image,false))
        {
            int nodes=0;using(var reader=XmlReader.Create(stream,settings))while(reader.Read())if(reader.Depth>64||++nodes>4*262144+2*(ExpandedHydrationMetadataMaximum/256)+8192)throw new InvalidDataException();
            stream.Position=0;using(var reader=XmlReader.Create(stream,settings))return XElement.Load(reader);
        }
    }
    private static void ReceiveHydrationMetadata(NetPackageRebirthUiDeferredHydrationV1 packet,World world,INetConnection connection,GameManager callbacks)
    {
        if(packet.Component!=1||packet.Total>PackedHydrationMetadataMaximum||clientHydrations.ContainsKey(packet.Group)||queueOwnerHydrationsProcessed.ContainsKey(packet.Group))return;
        SweepHydrationMetadata(world);
        if(readyHydrationMetadata.TryGetValue(packet.Group,out var ready))return;
        if(!assemblingHydrationMetadata.TryGetValue(packet.Group,out var assembly))
        {
            if(assemblingHydrationMetadata.Count+readyHydrationMetadata.Count>=4||
                assemblingHydrationMetadata.Values.Any(value=>ReferenceEquals(value.Connection,connection)&&value.Group!=packet.Group)||
                readyHydrationMetadata.Values.Any(value=>ReferenceEquals(value.Connection,connection)&&value.Group!=packet.Group)||
                waitingMetadataNative.Values.Any(value=>ReferenceEquals(value.Original.Connection,connection)&&value.Original.Group!=packet.Group)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*packet.Total+65536),out var budget))return;
            assembly=new ClientAssembly{Group=packet.Group,World=world,Connection=connection,Hash=packet.Digest,Total=packet.Total,Parts=new byte[packet.Count][],Budget=budget,Created=UnityEngine.Time.realtimeSinceStartup};
            assemblingHydrationMetadata.Add(packet.Group,assembly);
        }
        if(!ReferenceEquals(assembly.World,world)||!ReferenceEquals(assembly.Connection,connection)||assembly.Hash!=packet.Digest||assembly.Total!=packet.Total||assembly.Parts.Length!=packet.Count)return;
        if(assembly.Parts[packet.Index]!=null){if(!assembly.Parts[packet.Index].SequenceEqual(packet.Part))throw new InvalidDataException();return;}
        assembly.Parts[packet.Index]=(byte[])packet.Part.Clone();if(++assembly.Received!=assembly.Parts.Length)return;
        try
        {
            byte[] image=new byte[assembly.Total];int start=0;foreach(var part in assembly.Parts){Buffer.BlockCopy(part,0,image,start,part.Length);start+=part.Length;}assembly.Parts=null;
            if(HydrationHash(image)!=assembly.Hash)throw new InvalidDataException();
            var header=ExpandHydrationMetadata(image,packet.Group,assembly.Budget,out int expanded,out string headerDigest);
            ready=new HydrationMetadataReady{Group=packet.Group,World=world,Connection=connection,Digest=assembly.Hash,HeaderDigest=headerDigest,
                Expanded=expanded,Total=assembly.Total,Header=header,Budget=assembly.Budget,Created=assembly.Created};assembly.Budget=null;readyHydrationMetadata.Add(packet.Group,ready);
        }
        finally{assemblingHydrationMetadata.Remove(packet.Group);assembly.Parts=null;assembly.Budget?.Dispose();assembly.Budget=null;}
    }
    // Pure bounded metadata expansion. Its caller supplies actual connection
    // provenance and retains the same measured account; this creates no native
    // reader, grant, current body, owner identity or action authority.
    private static XElement ExpandHydrationMetadata(byte[] image,Guid group,IDisposable budget,out int expanded,out string headerDigest)
    {
            var packed=ReadHydrationMetadataXml(image,PackedHydrationMetadataMaximum);
            if(packed.Name!="hydrationMetadata"||(int?)packed.Attribute("version")!=1||ReadGuid(packed,"group")!=group||packed.Attributes().Count()!=4||
                packed.Elements().Count()!=2||packed.Element("header")?.Elements().Count()!=1||packed.Element("blobs")==null)throw new InvalidDataException();
            expanded=(int)packed.Attribute("expanded");headerDigest=(string)packed.Attribute("digest");if(expanded<65537||expanded>ExpandedHydrationMetadataMaximum||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(budget,checked(16L*(image.Length+expanded)+65536)))throw new InvalidDataException();
            var blobs=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var blob in packed.Element("blobs").Elements())
            {
                string digest=(string)blob.Attribute("digest");if(blob.Name!="blob"||blob.HasElements||blob.Attributes().Count()!=1||
                    digest==null||digest!=HydrationHash(Encoding.UTF8.GetBytes(blob.Value))||blobs.ContainsKey(digest))throw new InvalidDataException();blobs.Add(digest,blob.Value);
            }
            var header=packed.Element("header").Elements().Single();long growth=MeasureHydrationMetadataElement(header);
            foreach(var leaf in header.DescendantsAndSelf().Where(value=>value.Attribute(hydrationBlobNamespace+"ref")!=null).ToArray())
            {
                string digest=(string)leaf.Attribute(hydrationBlobNamespace+"ref");if(leaf.HasElements||leaf.Value.Length!=0||!blobs.TryGetValue(digest,out string text))throw new InvalidDataException();
                growth=checked(growth+Encoding.UTF8.GetByteCount(text));if(growth>2L*expanded)throw new InvalidDataException();
                leaf.Attribute(hydrationBlobNamespace+"ref").Remove();leaf.Value=text;
            }
            foreach(var declaration in header.DescendantsAndSelf().Attributes().Where(value=>value.IsNamespaceDeclaration&&value.Value==hydrationBlobNamespace.NamespaceName).ToArray())declaration.Remove();
            header.Remove();byte[] exact=Encoding.UTF8.GetBytes(header.ToString(SaveOptions.DisableFormatting));
            if(exact.Length!=expanded||HydrationHash(exact)!=headerDigest||ReadGuid(header,"group")!=group)throw new InvalidDataException();return header;
    }
    private static void PumpReadyHydrationMetadata(World world)
    {
        // Run after the metadata parser returned: its transient XML/string
        // aliases must die before a queue-only native Process can close the
        // transferred metadata account. This resumes only the SAME retained
        // complete native component; it never selects a replacement packet.
        foreach(var pair in waitingMetadataNative.Where(value=>readyHydrationMetadata.ContainsKey(value.Key)).ToArray())
        {
            var waiting=pair.Value;var metadata=readyHydrationMetadata[pair.Key];
            if(!ReferenceEquals(waiting.Original.World,world)||!ReferenceEquals(metadata.World,world)||
                !ReferenceEquals(waiting.Original.Connection,metadata.Connection)||metadata.Connection.IsDisconnected())continue;
            waitingMetadataNative.Remove(pair.Key);
            IDisposable transport=null;
            try{transport=ProcessHydrationImage(waiting.Original,waiting.Image,waiting.Callbacks);}
            finally{waiting.Dispose();transport?.Dispose();}
        }
    }
    private static bool TryUseHydrationMetadata(ClientHydration original,XElement selector)
    {
        if(selector.Name!="deferredHydrationMetadataSelector")return true;
        if((int?)selector.Attribute("version")!=1||selector.HasElements||selector.Attributes().Count()!=5||ReadGuid(selector,"group")!=original.Group||
            !readyHydrationMetadata.TryGetValue(original.Group,out var metadata)||!ReferenceEquals(metadata.World,original.World)||!ReferenceEquals(metadata.Connection,original.Connection)||
            metadata.Digest!=(string)selector.Attribute("digest")||metadata.HeaderDigest!=(string)selector.Attribute("header")||metadata.Expanded!=(int?)selector.Attribute("expanded"))return false;
        readyHydrationMetadata.Remove(original.Group);original.MetadataSelector=new XElement(selector);original.Header=metadata.Header;metadata.Header=null;original.MetadataBudget=metadata.Budget;metadata.Budget=null;metadata.Dispose();return true;
    }
}
