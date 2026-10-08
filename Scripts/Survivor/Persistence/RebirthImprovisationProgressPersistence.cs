using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

// Optional save-owned practical category progress, separate from ordinary Skills and Theory.
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
