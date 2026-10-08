using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Bounded gear offer payload only; caller authenticates its sender before accepting an offer.
public static class RebirthGearOfferWireCodec
{
    public const int MaxBytes = 8388608;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false,true);
    public static bool TryEncode(RebirthGearTransferState receipt,out byte[] bytes)
    {
        bytes=null;
        if(receipt==null)return false;
        try
        {
            string xml=receipt.ToXml().ToString(SaveOptions.DisableFormatting);
            if(Utf8.GetByteCount(xml)>MaxBytes)return false;
            bytes=Utf8.GetBytes(xml);return true;
        }
        catch{return false;}
    }
    public static bool TryDecode(byte[] bytes,out RebirthGearTransferState receipt)
    {
        receipt=null;
        if(bytes==null||bytes.Length==0||bytes.Length>MaxBytes)return false;
        try
        {
            string xml=Utf8.GetString(bytes);
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
                MaxCharactersInDocument=MaxBytes,ConformanceLevel=ConformanceLevel.Document};
            using(var input=new StringReader(xml))using(var reader=XmlReader.Create(input,settings))
            {
                var document=XDocument.Load(reader,LoadOptions.None);
                return document.Root!=null&&RebirthGearTransferState.TryRead(document.Root,out receipt);
            }
        }
        catch{return false;}
    }
}