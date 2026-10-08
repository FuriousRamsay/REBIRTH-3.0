using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// Immutable single-student completion intent. Never proves lesson timing or evidence eligibility.
public sealed class RebirthTheoryStudyOutcome
{
    private readonly XElement image;
    public string Id=>(string)image.Attribute("id");
    public string CreationId=>(string)image.Attribute("creation");
    public string SkillId=>(string)image.Attribute("skill");
    public string Mode=>(string)image.Attribute("mode");
    public string SourceId=>(string)image.Attribute("source");
    public float Before=>(float)image.Attribute("before");
    public float Target=>(float)image.Attribute("target");
    public int HistoryCount=>(int)image.Attribute("historyCount");
    public long CompletedTicks=>(long)image.Attribute("completedTicks");
    public float LessonSeconds=>(float)image.Attribute("seconds");
    public string HistoryKey=>Mode+"|"+SourceId+"|"+SkillId;
    public string Receipt=>"theory-study:"+Id;
    private RebirthTheoryStudyOutcome(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthTheoryStudyOutcome Clone()=>new RebirthTheoryStudyOutcome(image);
    public static bool TryCreate(Guid id,string creation,string skill,string mode,string source,
        float before,float target,int historyCount,long completedTicks,float seconds,out RebirthTheoryStudyOutcome result)
    {
        result=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalizedCreation))return false;
        try
        {
            return TryRead(new XElement("theoryStudyOutcome",new XAttribute("version",1),
                new XAttribute("id",id.ToString("N")),new XAttribute("creation",normalizedCreation),
                new XAttribute("skill",skill??""),new XAttribute("mode",mode??""),new XAttribute("source",source??""),
                new XAttribute("before",before.ToString("R",CultureInfo.InvariantCulture)),
                new XAttribute("target",target.ToString("R",CultureInfo.InvariantCulture)),
                new XAttribute("historyCount",historyCount),new XAttribute("completedTicks",completedTicks),
                new XAttribute("seconds",seconds.ToString("R",CultureInfo.InvariantCulture))),out result);
        }
        catch{return false;}
    }
    private static bool Text(string value,int maximum)
        =>!string.IsNullOrWhiteSpace(value)&&value.Length<=maximum&&!value.Any(char.IsControl)&&!value.Contains("|");
    public static bool TryRead(XElement node,out RebirthTheoryStudyOutcome result)
    {
        result=null;
        if(node==null||node.Name!="theoryStudyOutcome"||node.HasElements||node.Attributes().Count()!=11||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            (string)node.Attribute("version")!="1"||!Guid.TryParseExact((string)node.Attribute("id"),"N",out var id)||id==Guid.Empty||
            !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
            creation!=(string)node.Attribute("creation")||!Text((string)node.Attribute("skill"),128)||
            !((string)node.Attribute("skill")).StartsWith("skill.",StringComparison.Ordinal)||
            ((string)node.Attribute("mode")!="npc"&&(string)node.Attribute("mode")!="solo")||
            !Text((string)node.Attribute("source"),256)||
            !float.TryParse((string)node.Attribute("before"),NumberStyles.Float,CultureInfo.InvariantCulture,out var before)||
            !float.TryParse((string)node.Attribute("target"),NumberStyles.Float,CultureInfo.InvariantCulture,out var target)||
            float.IsNaN(before)||float.IsInfinity(before)||float.IsNaN(target)||float.IsInfinity(target)||before<0||target<=before||target>100||
            !int.TryParse((string)node.Attribute("historyCount"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var count)||count<1||
            !long.TryParse((string)node.Attribute("completedTicks"),NumberStyles.Integer,CultureInfo.InvariantCulture,out var ticks)||ticks<=0||ticks>DateTime.MaxValue.Ticks||
            !float.TryParse((string)node.Attribute("seconds"),NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)||
            float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0||seconds>86400)return false;
        result=new RebirthTheoryStudyOutcome(node);return true;
    }
}