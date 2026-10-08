using System;
using System.Text;

#nullable disable

public static class RebirthProgressionGraphDebug
{
    public static string BuildSummary()
    {
        RebirthProgressionGraphValidationReport report=RebirthProgressionGraphValidator.ValidateCurrent();
        return "[REBIRTH ProgressionGraph] ready="+RebirthProgressionGraphRegistry.IsReady+" nodes="+RebirthProgressionGraphRegistry.NodeCount+" edges="+RebirthProgressionGraphRegistry.EdgeCount+" hash="+RebirthProgressionGraphRegistry.SemanticHash+" "+report.BuildSummary();
    }

    public static string BuildFocus(string id)
    {
        RebirthProgressionGraphNode node;
        if(!RebirthProgressionGraphRegistry.TryGetNode(Normalize(id),out node)||node==null)return "[REBIRTH ProgressionGraph] node not found: "+(id??string.Empty);
        StringBuilder b=new StringBuilder();
        b.Append("[REBIRTH ProgressionGraph] focus id=").Append(node.Id).Append(" type=").Append(node.Type).Append(" category=").Append(node.Category).Append(" name=").Append(GetName(node));
        if(!string.IsNullOrEmpty(node.Description))b.Append("\n  description=").Append(node.Description);
        RebirthProgressionGraphEdge[] incoming=RebirthProgressionGraphRegistry.GetIncoming(node.Id);
        RebirthProgressionGraphEdge[] outgoing=RebirthProgressionGraphRegistry.GetOutgoing(node.Id);
        b.Append("\n  incoming=").Append(incoming.Length);
        for(int i=0;i<incoming.Length;i++)b.Append("\n    <- ").Append(incoming[i].FromId).Append(" [").Append(FormatEdge(incoming[i])).Append(']');
        b.Append("\n  outgoing=").Append(outgoing.Length);
        for(int i=0;i<outgoing.Length;i++)b.Append("\n    -> ").Append(outgoing[i].ToId).Append(" [").Append(FormatEdge(outgoing[i])).Append(']');
        return b.ToString();
    }

    public static string BuildTypeReport(RebirthProgressionGraphNodeType type,string id)
    {
        if(!string.IsNullOrEmpty(id))return BuildFocus(NormalizeWithType(type,id));
        StringBuilder b=new StringBuilder("[REBIRTH ProgressionGraph] type="+type);
        RebirthProgressionGraphNode[] nodes=RebirthProgressionGraphRegistry.GetNodesSnapshot();int count=0;
        for(int i=0;i<nodes.Length;i++)if(nodes[i].Type==type){b.Append("\n  ").Append(nodes[i].Id).Append(" name=").Append(GetName(nodes[i]));count++;}
        b.Insert(b.ToString().IndexOf('\n')>=0?b.ToString().IndexOf('\n'):b.Length," count="+count);
        return b.ToString();
    }

    private static string NormalizeWithType(RebirthProgressionGraphNodeType type,string id)
    {
        string v=(id??string.Empty).Trim();
        if(type==RebirthProgressionGraphNodeType.Skill && !v.StartsWith("skill.",StringComparison.OrdinalIgnoreCase))return "skill."+v;
        if(type==RebirthProgressionGraphNodeType.Knowledge && !v.StartsWith("knowledge.",StringComparison.OrdinalIgnoreCase))return "knowledge."+v;
        if(type==RebirthProgressionGraphNodeType.Recipe && !v.StartsWith("recipe.",StringComparison.OrdinalIgnoreCase))return "recipe."+v;
        if(type==RebirthProgressionGraphNodeType.Discipline && !v.StartsWith("discipline.",StringComparison.OrdinalIgnoreCase))return "discipline."+v;
        return v;
    }
    private static string Normalize(string id){return (id??string.Empty).Trim();}
    private static string GetName(RebirthProgressionGraphNode n){if(n==null)return string.Empty;if(!string.IsNullOrEmpty(n.DisplayName))return n.DisplayName;if(!string.IsNullOrEmpty(n.NameKey)){string x=Localization.Get(n.NameKey);if(!string.IsNullOrEmpty(x)&&x!=n.NameKey)return x;}return n.Id;}
    private static string FormatEdge(RebirthProgressionGraphEdge e){string x=e.Type.ToString();if(e.HasMinimum)x+=" min="+e.Minimum;if(e.HasRecommended)x+=" recommended="+e.Recommended;if(!string.IsNullOrEmpty(e.RequirementGroupPath))x+=" group="+e.RequirementGroupMode+":"+e.RequirementGroupPath;if(!string.IsNullOrEmpty(e.Note))x+=" note="+e.Note;return x;}
}
