using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// One native persisted CVar key stores the complete original request. Lowercase
// hex preserves bytes in the game's case-insensitive CVar dictionary. Its value
// must be exactly1. This codec alone does not authenticate or save the marker.
public static class RebirthGearPreparationMarker
{
    public const string Prefix="_rbgearintent_v1_";
    public const int MaximumKeyLength=4096;
    private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    public static bool TryEncode(Guid world,int ownedBeltSlots,RebirthGearPreparationIntent intent,out string key)
    {
        key=null;
        if(world==Guid.Empty||ownedBeltSlots<4||ownedBeltSlots>18||intent==null||
            !RebirthGearPreparationIntent.TryRead(intent.Write(),out var validated))return false;
        try
        {
            var xml=new XElement("originalGearIntent",new XAttribute("version",1),new XAttribute("world",world.ToString("N")),
                new XAttribute("ownedBelt",ownedBeltSlots),validated.Write());
            byte[] bytes=Utf8.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length==0||bytes.Length>(MaximumKeyLength-Prefix.Length)/2)return false;
            key=Prefix+BitConverter.ToString(bytes).Replace("-",string.Empty).ToLowerInvariant();return true;
        }
        catch {return false;}
    }
    public static bool TryRead(string key,float value,out Guid world,out int ownedBeltSlots,out RebirthGearPreparationIntent intent)
    {
        world=Guid.Empty;ownedBeltSlots=0;intent=null;
        if(value!=1f||key==null||key.Length>MaximumKeyLength||!key.StartsWith(Prefix,StringComparison.Ordinal)||
            key.Length==Prefix.Length||(key.Length-Prefix.Length)%2!=0)return false;
        try
        {
            int length=(key.Length-Prefix.Length)/2;var bytes=new byte[length];
            for(int i=0;i<length;i++)
            {
                int hi=Hex(key[Prefix.Length+i*2]),lo=Hex(key[Prefix.Length+i*2+1]);
                if(hi<0||lo<0)return false;bytes[i]=(byte)(hi*16+lo);
            }
            string text=Utf8.GetString(bytes);
            using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{
                DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2048}))
            {
                var xml=XElement.Load(reader);
                if(xml.Name!="originalGearIntent"||xml.Attributes().Count()!=3||(string)xml.Attribute("version")!="1"||
                    xml.Elements().Count()!=1||xml.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                    !Guid.TryParseExact((string)xml.Attribute("world"),"N",out var parsedWorld)||parsedWorld==Guid.Empty||
                    !int.TryParse((string)xml.Attribute("ownedBelt"),NumberStyles.None,CultureInfo.InvariantCulture,out var owned)||
                    !RebirthGearPreparationIntent.TryRead(xml.Elements().Single(),out var parsed)||
                    !TryEncode(parsedWorld,owned,parsed,out var canonical)||canonical!=key)return false;
                world=parsedWorld;ownedBeltSlots=owned;intent=parsed;return true;
            }
        }
        catch {return false;}
    }
    private static int Hex(char c)=>c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:-1;
}