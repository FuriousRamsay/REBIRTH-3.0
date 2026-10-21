using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    private sealed class PublicationCounter:Stream
    {
        internal long Size;public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>Size;public override long Position{get=>Size;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int count){if(count<0||Size>24L*1024*1024-count)throw new InvalidDataException();Size+=count;}
        public override void WriteByte(byte value){if(Size>=24L*1024*1024)throw new InvalidDataException();Size++;}
        public override void Flush(){}public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long value,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
    }
    internal static long MeasurePublicationElement(XElement image)
    {
        using(var stream=new PublicationCounter())
        {using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,CloseOutput=false}))image.WriteTo(writer);return stream.Size;}
    }
    // In addition to the retained input/frame account, publication can hold a
    // cloned envelope, XmlWriter buffer+ToArray, identity UTF16/UTF8 strings,
    // parsed exact-readback graphs, and old/staged/new byte images together.
    // Sixteen measured payload copies bound that synchronous workspace for
    // these fixed-topology owner frames/reports. Failed staged candidates have
    // the separate explicit 64 MiB aggregate cap in CompletionPublication.
    internal static bool WriteWithPublicationWorkspace(RecipientScope scope,string path,long utf8Bound,Func<XElement> factory)
        =>WriteWithPublicationWorkspace(scope?.TryEnterPublicationWorkspace(utf8Bound),path,factory);
    private static bool WriteWithPublicationWorkspace(ClientHydration scope,string path,long utf8Bound,Func<XElement> factory)
        =>WriteWithPublicationWorkspace(scope?.TryEnterPublicationWorkspace(utf8Bound),path,factory);
    private static bool WriteWithPublicationWorkspace(IDisposable workspace,string path,Func<XElement> factory)
    {
        if(workspace==null)return false;XElement envelope=null;
        try{envelope=factory();return RebirthWorkstationCompletionPublication.Write(path,envelope);}
        finally{envelope=null;workspace.Dispose();}
    }
}
