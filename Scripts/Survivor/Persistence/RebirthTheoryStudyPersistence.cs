using System.Linq;
using System.Xml.Linq;

public static class RebirthTheoryStudyPersistence
{
    public static bool MatchesOwner(RebirthTheoryStudyOutcome outcome,string creation)
        =>outcome==null||RebirthSurvivorRequestScope.Matches(outcome.CreationId,creation);
    public static XElement Write(RebirthTheoryStudyOutcome outcome)
    {
        var node=new XElement("theoryStudy",new XAttribute("version",1));
        if(outcome!=null)node.Add(outcome.Write());
        return node;
    }
    public static bool TryRead(XElement progression,out RebirthTheoryStudyOutcome outcome,out string error)
    {
        outcome=null;error=null;
        var sections=progression.Elements("theoryStudy").ToArray();
        if(sections.Length==0)return true;
        if(sections.Length!=1){error="Duplicate Theory study section";return false;}
        var node=sections[0];
        var children=node.Elements().ToArray();
        if(node.Attributes().Count()!=1||(string)node.Attribute("version")!="1"||children.Length>1||
            node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))
        {error="Invalid Theory study section";return false;}
        if(children.Length==0)return true;
        if(!RebirthTheoryStudyOutcome.TryRead(children[0],out outcome))
        {error="Invalid Theory study outcome";return false;}
        return true;
    }
}