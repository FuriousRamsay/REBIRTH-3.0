using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Description transport only. Callers authenticate the actual native peer and
// derive its scope before assembly; no complete body grants item/source rights.
internal static class RebirthHydrationOriginalTransport
{
    internal const int MaximumBytes=1024*1024;
    internal static RebirthRemoteResourceFragments.Body Create(RebirthHydrationTransferOriginal original)
    {
        if(original==null)throw new ArgumentNullException(nameof(original));
        byte[] bytes=Bytes(original);if(bytes.Length<1||bytes.Length>MaximumBytes)throw new InvalidDataException("Hydration original exceeds wire bound");
        return new RebirthRemoteResourceFragments.Body(bytes);
    }
    internal static bool TryReceive(string actualScope,Guid operation,string digest,int total,int count,int index,string part,double now,
        out RebirthHydrationTransferOriginal original)
    {
        original=null;
        if(total<1||total>MaximumBytes||!RebirthRemoteResourceFragments.TryAdd(actualScope,operation,digest,total,count,index,part,now,out var bytes))return false;
        if(bytes==null)return true; // Incomplete framing is not a domain original.
        if(!TryDecode(bytes,out var candidate)||candidate.Operation!=operation||candidate.Digest!=digest)return false;
        original=candidate;return true;
    }
    internal static bool TryDecode(byte[] bytes,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            if(bytes==null||bytes.Length<1||bytes.Length>MaximumBytes)return false;
            string xml=new UTF8Encoding(false,true).GetString(bytes);
            using(var text=new StringReader(xml))using(var reader=XmlReader.Create(text,new XmlReaderSettings{
                DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
            {
                if(!RebirthHydrationTransferOriginal.TryRead(XElement.Load(reader),out var candidate)||!bytes.SequenceEqual(Bytes(candidate)))return false;
                original=candidate;return true;
            }
        }
        catch{return false;}
    }
    private static byte[] Bytes(RebirthHydrationTransferOriginal original)=>Encoding.UTF8.GetBytes(original.Write().ToString(SaveOptions.DisableFormatting));
}
