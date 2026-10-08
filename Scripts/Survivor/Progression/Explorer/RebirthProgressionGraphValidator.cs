using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public sealed class RebirthProgressionGraphValidationReport
{
    public readonly List<string> Errors=new List<string>();
    public readonly List<string> Warnings=new List<string>();
    public bool IsValid { get { return Errors.Count==0; } }
    public int ErrorCount { get { return Errors.Count; } }
    public int WarningCount { get { return Warnings.Count; } }
    public string BuildSummary(){return "valid="+IsValid+" errors="+Errors.Count+" warnings="+Warnings.Count;}
    public string BuildText()
    {
        StringBuilder b=new StringBuilder("[REBIRTH ProgressionGraph] "+BuildSummary());
        for(int i=0;i<Errors.Count;i++)b.Append("\n  ERROR: ").Append(Errors[i]);
        for(int i=0;i<Warnings.Count;i++)b.Append("\n  WARN: ").Append(Warnings[i]);
        return b.ToString();
    }
}

public static class RebirthProgressionGraphValidator
{
    public static RebirthProgressionGraphValidationReport ValidateCurrent()
    {
        RebirthProgressionGraphValidationReport r=new RebirthProgressionGraphValidationReport();
        RebirthProgressionGraphNode[] nodes=RebirthProgressionGraphRegistry.GetNodesSnapshot();
        RebirthProgressionGraphEdge[] edges=RebirthProgressionGraphRegistry.GetEdgesSnapshot();
        Dictionary<string,RebirthProgressionGraphNode> byId=new Dictionary<string,RebirthProgressionGraphNode>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<nodes.Length;i++)
        {
            RebirthProgressionGraphNode n=nodes[i];
            if(string.IsNullOrEmpty(n.Id)){r.Errors.Add("node has empty ID");continue;}
            if(byId.ContainsKey(n.Id))r.Errors.Add("duplicate node ID: "+n.Id); else byId.Add(n.Id,n);
        }
        HashSet<string> skillsWithTraining=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> knowledgeWithGateOrUnlock=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> knowledgeWithAssociation=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> disciplinesWithInitiation=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<edges.Length;i++)
        {
            RebirthProgressionGraphEdge e=edges[i]; RebirthProgressionGraphNode from,to;
            if(!byId.TryGetValue(e.FromId,out from)){r.Errors.Add("edge source is missing: "+e.FromId+" -> "+e.ToId+" type="+e.Type);continue;}
            if(!byId.TryGetValue(e.ToId,out to)){r.Errors.Add("edge target is missing: "+e.FromId+" -> "+e.ToId+" type="+e.Type);continue;}
            if(e.Type==RebirthProgressionGraphEdgeType.TrainsSkill)
            {
                if(to.Type!=RebirthProgressionGraphNodeType.Skill)r.Errors.Add("TRAINS_SKILL target must be Skill: "+e.ToId);
                else skillsWithTraining.Add(to.Id);
            }
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresKnowledge && from.Type!=RebirthProgressionGraphNodeType.Knowledge)
                r.Errors.Add("REQUIRES_KNOWLEDGE source must be Knowledge: "+e.FromId);
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresSkill && from.Type!=RebirthProgressionGraphNodeType.Skill)
                r.Errors.Add("REQUIRES_SKILL source must be Skill: "+e.FromId);
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresSkill && to.Type==RebirthProgressionGraphNodeType.Skill)
                r.Errors.Add("Skill-to-Skill prerequisite is prohibited: "+e.FromId+" -> "+e.ToId);
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresDiscipline && from.Type!=RebirthProgressionGraphNodeType.Discipline)
                r.Errors.Add("REQUIRES_DISCIPLINE source must be Discipline: "+e.FromId);
            if(e.Type==RebirthProgressionGraphEdgeType.UnlocksDiscipline)
            {
                if(from.Type!=RebirthProgressionGraphNodeType.Action||to.Type!=RebirthProgressionGraphNodeType.Discipline)
                    r.Errors.Add("UNLOCKS_DISCIPLINE must be Action -> Discipline: "+e.FromId+" -> "+e.ToId);
                else disciplinesWithInitiation.Add(to.Id);
            }
            if(from.Type==RebirthProgressionGraphNodeType.Knowledge)
            {
                if(e.Type==RebirthProgressionGraphEdgeType.RelatedTo) knowledgeWithAssociation.Add(from.Id);
                else knowledgeWithGateOrUnlock.Add(from.Id);
            }
        }
        foreach(KeyValuePair<string,RebirthProgressionGraphNode> pair in byId)
        {
            if(pair.Value.Type==RebirthProgressionGraphNodeType.Skill && !skillsWithTraining.Contains(pair.Key))r.Errors.Add("Skill has no player-facing training route: "+pair.Key);
            if(pair.Value.Type==RebirthProgressionGraphNodeType.Discipline && !disciplinesWithInitiation.Contains(pair.Key))r.Errors.Add("Discipline has no initiation Action: "+pair.Key);
            if(pair.Value.Type==RebirthProgressionGraphNodeType.Knowledge && !knowledgeWithGateOrUnlock.Contains(pair.Key))
            {
                if(knowledgeWithAssociation.Contains(pair.Key)) r.Warnings.Add("Knowledge has an Explorer domain association but no current gameplay gate/unlock: "+pair.Key);
                else r.Warnings.Add("Knowledge currently has no authored dependency/unlock or domain association: "+pair.Key);
            }
        }
        // Audit warning, not a failure: the current vehicle service runtime awards Mechanics from vehicle work,
        // but no general Knowledge gate currently exists around those service actions. The Explorer must describe
        // current implementation until a later gameplay change deliberately adds such a gate.
        if(byId.ContainsKey("knowledge.vehicle.service") && byId.ContainsKey("skill.mechanics"))
        {
            bool hasVehicleActionGate=false;
            for(int i=0;i<edges.Length;i++) if(edges[i].Type==RebirthProgressionGraphEdgeType.RequiresKnowledge && string.Equals(edges[i].FromId,"knowledge.vehicle.service",StringComparison.OrdinalIgnoreCase) && edges[i].ToId.StartsWith("action.",StringComparison.OrdinalIgnoreCase)){hasVehicleActionGate=true;break;}
            if(!hasVehicleActionGate)r.Warnings.Add("Vehicle Service Knowledge does not currently gate a vehicle service Action in runtime graph; Mechanics vehicle work remains trainable through existing vehicle hooks. This is a design/runtime gap, not silently invented by PE-01.");
        }
        return r;
    }
}
