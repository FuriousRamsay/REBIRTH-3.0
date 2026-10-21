using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// Syntax only. A receipt body cannot issue permission or complete an exchange;
// its writers separately require the actual response-scoped terminal/close.
internal static class RebirthNpcTradeOwnerReceipt
{
    internal static string WriteUnused(Guid operation,string original,string unused)
    {
        if(operation==Guid.Empty||!Hash(original)||!Hash(unused))throw new InvalidDataException();
        return new XElement("npcTradeOwnerUnused",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
            new XAttribute("originalDigest",original),new XAttribute("unusedDigest",unused)).ToString(SaveOptions.DisableFormatting);
    }
    internal static bool TryReadUnused(string image,out Guid operation,out string original,out string unused)
    {
        operation=Guid.Empty;original=unused=null;
        try
        {
            if(image==null||image.Length<1||image.Length>4096)return false;
            using var text=new StringReader(image);
            using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096});
            var root=XElement.Load(reader);
            if(root.Name!="npcTradeOwnerUnused"||!Guid.TryParseExact((string)root.Attribute("operation"),"N",out operation))return false;
            original=(string)root.Attribute("originalDigest");unused=(string)root.Attribute("unusedDigest");
            return image==WriteUnused(operation,original,unused);
        }
        catch{operation=Guid.Empty;original=unused=null;return false;}
    }
    internal static string Write(Guid operation,string original,string terminal,bool closed)
    {
        if(operation==Guid.Empty||!Hash(original)||!Hash(terminal))throw new InvalidDataException();
        return new XElement("npcTradeOwnerReceipt",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
            new XAttribute("originalDigest",original),new XAttribute("terminalDigest",terminal),new XAttribute("closed",closed?1:0)).ToString(SaveOptions.DisableFormatting);
    }
    internal static bool TryRead(string image,out Guid operation,out string original,out string terminal,out bool closed)
    {
        operation=Guid.Empty;original=terminal=null;closed=false;
        try
        {
            if(image==null||image.Length<1||image.Length>4096)return false;
            using var text=new StringReader(image);
            using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096});
            var root=XElement.Load(reader);
            if(root.Name!="npcTradeOwnerReceipt"||(int?)root.Attribute("version")!=1||root.Attributes().Count()!=5||root.Nodes().Any()||
                !Guid.TryParseExact((string)root.Attribute("operation"),"N",out operation)||operation==Guid.Empty||
                (string)root.Attribute("closed")!="0"&&(string)root.Attribute("closed")!="1")return false;
            original=(string)root.Attribute("originalDigest");terminal=(string)root.Attribute("terminalDigest");closed=(string)root.Attribute("closed")=="1";
            return Hash(original)&&Hash(terminal)&&image==Write(operation,original,terminal,closed);
        }
        catch{operation=Guid.Empty;original=terminal=null;closed=false;return false;}
    }
    private static bool Hash(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
}
