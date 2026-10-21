using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

// Streaming immutable artifact materialization only. A serialized body is not
// admission, payment or publication authority; its durable reader must rederive
// the original native/owner/source proof before any continuation.
internal static class RebirthRemoteResourcePlacedStationArtifact
{
    internal const int MaximumBytes=64*1024*1024;
    // One artifact buffer plus bounded parsed/source/native workspace. This is
    // a domain allocation budget, independent of the native sector limit.
    internal const long MaximumOperationWorkspace=256L*1024*1024;
    internal sealed class Base64Sink:Stream
    {
        private readonly XmlWriter writer;private readonly long maximum;private long count;
        internal Base64Sink(XmlWriter target,long limit){writer=target;maximum=limit;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int length)
        {if(length<0||count>maximum-length)throw new InvalidDataException();writer.WriteBase64(buffer,offset,length);count+=length;}
        public override void WriteByte(byte value){Write(new[]{value},0,1);}
        public override void Flush(){}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private sealed class Counter:Stream
    {
        private long count;private readonly long maximum;
        internal Counter(long limit=MaximumBytes){maximum=limit;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] b,int o,int c){if(c<0||count>maximum-c)throw new InvalidDataException();count+=c;}
        public override void WriteByte(byte value){if(count>=maximum)throw new InvalidDataException();count++;}
        public override void Flush(){}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private static void Write(Stream destination,RebirthRemoteResourceClientOriginal.Intent original,string birth,long sourcePreparedBytes,long sourceRevision,
        RebirthRemoteResourcePlacedStationPlan.Plan plan,RebirthRemoteResourcePlacedStationBefore.Evidence before)
    {
        using(var writer=XmlWriter.Create(destination,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,
            Indent=false,NewLineHandling=NewLineHandling.None,ConformanceLevel=ConformanceLevel.Fragment,CloseOutput=false}))
        {
            writer.WriteStartElement("placedFetchedOriginal");writer.WriteAttributeString("version","2");writer.WriteAttributeString("birth",birth);
            writer.WriteAttributeString("sourcePreparedBytes",XmlConvert.ToString(sourcePreparedBytes));writer.WriteAttributeString("sourceRevision",XmlConvert.ToString(sourceRevision));
            writer.WriteAttributeString("sourceGraphBytes",XmlConvert.ToString(plan.Allocation.SourceGraphBytes));
            writer.WriteAttributeString("sourceReadWorkspace",XmlConvert.ToString(plan.Allocation.PlanningBytes));
            writer.WriteStartElement("original");original.WriteDetachedTo(writer);writer.WriteEndElement();plan.WriteArtifact(writer);before.WriteArtifact(writer);
            writer.WriteEndElement();writer.Flush();
        }
    }
    internal static bool TryCountSource(RebirthRemoteResourceDurableJournal.Record root,out long bytes)
    {
        bytes=0;
        try
        {
            if(root==null)return false;
            using(var counter=new Counter(128L*1024*1024))
            {
                using(var writer=XmlWriter.Create(counter,new XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,Indent=false,NewLineHandling=NewLineHandling.None,CloseOutput=false}))
                {root.WriteDetachedTo(writer);writer.Flush();}bytes=counter.Length;
            }
            return bytes>0;
        }
        catch{bytes=0;return false;}
    }
    internal static bool TryEncode(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string birth,
        RebirthRemoteResourcePlacedStationPlan.Plan plan,RebirthRemoteResourcePlacedStationBefore.Evidence before,out byte[] artifact)
    {
        artifact=null;
        try
        {
            if(root==null||original==null||plan==null||before==null||root.Phase!=1||
                !Guid.TryParseExact(birth,"N",out var token)||token==Guid.Empty||token.ToString("N")!=birth||
                plan.Source!=root.Operation||plan.Binding!=root.Binding||plan.Job!=original.Operation||before.Source!=plan.Source||before.Binding!=plan.Binding||before.Job!=plan.Job||
                root.HasOwnerAction||!root.SourcesUnattempted)return false;
            // The first pass counts exact UTF8 escaping/base64/native actor
            // bytes into a nonmaterializing bounded sink before any artifact
            // allocation or full native snapshot cloning.
            if(!TryCountSource(root,out long sourceLength))return false;
            long length;using(var counter=new Counter()){Write(counter,original,birth,sourceLength,root.Revision,plan,before);length=counter.Length;}
            if(length<1||length>MaximumBytes||plan.Allocation==null||plan.Allocation.Source!=root.Operation||plan.Allocation.Binding!=root.Binding||
                plan.Allocation.Original!=original.Digest||plan.Allocation.SourceBytes!=sourceLength||
                checked(length*3+plan.Allocation.PlanningBytes)>MaximumOperationWorkspace||!plan.Allocation.ReserveArtifact(length))return false;
            // Only after the complete source+artifact workspace preflight may
            // historical readers materialize/revalidate retained originals.
            if(!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)||
                !RebirthRemoteResourcePlacedStationOriginal.TryReadAdmission(root,out var admitted)||admitted.Digest!=original.Digest)return false;
            using(var stream=new MemoryStream(checked((int)length)))
            {
                Write(stream,original,birth,sourceLength,root.Revision,plan,before);if(stream.Length!=length)return false;
                artifact=stream.ToArray();return true;
            }
        }
        catch{artifact=null;return false;}
    }
}
