using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Strict bound terminal payload only; does not authenticate delivery.
public static class RebirthGearTerminalWireCodec
{
    public const int MaximumBytes=1024;
    private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    public static bool TryEncode(RebirthGearSettlement value,out byte[] bytes)
    {
        bytes=null;
        try
        {
            if(value?.PreparationRequestDigest==null||
                !RebirthGearSettlement.TryRead(new XElement("support",value.Write()),value.GearRevision,out var copy)||copy==null)return false;
            var encoded=Utf8.GetBytes(copy.Write().ToString(SaveOptions.DisableFormatting));
            if(encoded.Length==0||encoded.Length>MaximumBytes)return false;
            bytes=encoded;return true;
        }
        catch{return false;}
    }
    public static bool TryDecode(byte[] bytes,out RebirthGearSettlement value)
    {
        value=null;
        if(bytes==null||bytes.Length==0||bytes.Length>MaximumBytes)return false;
        try
        {
            var text=Utf8.GetString(bytes);
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{
                DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
            {
                var node=XElement.Load(reader);
                if(node.Name!="gearSettlement"||
                    !RebirthGearSettlement.TryRead(new XElement("support",node),long.MaxValue,out var candidate)||
                    !TryEncode(candidate,out var canonical)||canonical.Length!=bytes.Length)return false;
                for(int i=0;i<bytes.Length;i++)if(bytes[i]!=canonical[i])return false;
                value=candidate;return true;
            }
        }
        catch{return false;}
    }
}