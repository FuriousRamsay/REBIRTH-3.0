using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
public static class RebirthImprovisationProgressPersistence
{
    public const int MaximumCategories=64;
    public static bool ValidCategory(string id)
    {
        if(string.IsNullOrEmpty(id)||id.Length>96)return false;
        foreach(char c in id)
            if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='.'||c=='-'))return false;
        return true;
    }
    private static bool ValidValue(float value)
        => !float.IsNaN(value)&&!float.IsInfinity(value)&&value>=0&&value<=100;

    public static XElement Write(IDictionary<string,float> values)
    {
        if(values==null||values.Count>MaximumCategories)throw new InvalidDataException("Invalid improvisation category progress");
        var node=new XElement("improvisation",new XAttribute("version","1"));
        var ids=new List<string>(values.Keys);ids.Sort(StringComparer.Ordinal);
        foreach(string id in ids)
        {
            float value=values[id];
            if(!ValidCategory(id)||!ValidValue(value))throw new InvalidDataException("Invalid improvisation category progress");
            node.Add(new XElement("category",new XAttribute("id",id),
                new XAttribute("value",value.ToString("R",CultureInfo.InvariantCulture))));
        }
        return node;
    }

    public static bool TryRead(XElement progression,out Dictionary<string,float> values,out string error)
    {
        values=null;error=string.Empty;
        if(progression==null){error="Missing progression for improvisation";return false;}
        var result=new Dictionary<string,float>(StringComparer.Ordinal);
        XElement section=null;
        foreach(XElement node in progression.Elements("improvisation"))
        { if(section!=null){error="Duplicate improvisation section";return false;}section=node; }
        // Old saves start with independent, untrained category proficiency.
        if(section==null){values=result;return true;}
        if((string)section.Attribute("version")!="1"){error="Unsupported improvisation version";return false;}
        foreach(XElement node in section.Elements())
        {
            string id=(string)node.Attribute("id");float value;
            if(node.Name!="category"||node.HasElements||result.Count>=MaximumCategories||
                !ValidCategory(id)||result.ContainsKey(id)||
                !float.TryParse((string)node.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||!ValidValue(value))
            {error="Invalid improvisation category progress";return false;}
            result.Add(id,value);
        }
        values=result;return true;
    }
}

public class Owner { public int ProtocolVersion=10;public Dictionary<string,float> ImprovisationProgress=new Dictionary<string,float>(); }
public static class Test {
 public static Owner Read(BinaryReader reader,int version){var s=new Owner{ProtocolVersion=version};
        if(s.ProtocolVersion>=10)
        {
            string xml=reader.ReadString();
            if(xml.Length>16384)throw new System.IO.InvalidDataException("Owner improvisation section too large");
            System.Xml.Linq.XElement node;
            using(var text=new System.IO.StringReader(xml))
            using(var xmlReader=System.Xml.XmlReader.Create(text,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))
                node=System.Xml.Linq.XElement.Load(xmlReader);
            if(node.Name!="improvisation")throw new System.IO.InvalidDataException("Invalid owner improvisation section");
            Dictionary<string,float> values;string error;
            if(!RebirthImprovisationProgressPersistence.TryRead(new System.Xml.Linq.XElement("progression",node),out values,out error))
                throw new System.IO.InvalidDataException(error);
            foreach(var pair in values)s.ImprovisationProgress[pair.Key]=pair.Value;
        }

return s;}
 static void A(bool ok,string name){if(!ok)throw new Exception(name);}
 static Owner Parse(string xml){using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write(xml);}m.Position=0;return Read(new BinaryReader(m),10);}}
 public static void Main(){
 var v=Parse("<improvisation version='1'><category id='building' value='80'/><category id='electrical' value='2.5'/></improvisation>");A(v.ImprovisationProgress["building"]==80&&v.ImprovisationProgress["electrical"]==2.5f,"owner isolated values");
 foreach(string bad in new[]{"<progression/>","<improvisation version='2'/>","<improvisation version='1'><category id='building' value='NaN'/></improvisation>","<!DOCTYPE improvisation [<!ENTITY e 'oops'>]><improvisation version='1'>&e;</improvisation>",new string('x',16385)}){bool refused=false;try{Parse(bad);}catch(Exception){refused=true;}A(refused,"bad extension refused");}
 using(var m=new MemoryStream()){using(var w=new BinaryWriter(m,System.Text.Encoding.UTF8,true)){w.Write(123456);}m.Position=0;var r=new BinaryReader(m);A(Read(r,9).ImprovisationProgress.Count==0&&r.ReadInt32()==123456,"protocol9 does not consume extension");}
 Console.WriteLine("PASS actual owner read extension: isolated values, version/DTD/numeric/size refusal and protocol9 cursor compatibility");
 }
}