using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public enum RebirthProgressionGraphNodeType
{
    Background,
    Skill,
    Knowledge,
    Action,
    Recipe,
    Discipline
}

public enum RebirthProgressionGraphEdgeType
{
    RequiresKnowledge,
    RequiresSkill,
    UnlocksAction,
    UnlocksRecipe,
    TrainsSkill,
    GrantedByBackground,
    RelatedTo,
    RequiresDiscipline,
    UnlocksDiscipline
}

public sealed class RebirthProgressionGraphNode
{
    public string Id { get; private set; }
    public RebirthProgressionGraphNodeType Type { get; private set; }
    public string NameKey { get; private set; }
    public string DisplayName { get; private set; }
    public string Description { get; private set; }
    public string Category { get; private set; }
    public string Source { get; private set; }

    public RebirthProgressionGraphNode(string id, RebirthProgressionGraphNodeType type, string nameKey, string displayName, string description, string category, string source)
    {
        Id=(id??string.Empty).Trim(); Type=type; NameKey=(nameKey??string.Empty).Trim(); DisplayName=(displayName??string.Empty).Trim();
        Description=(description??string.Empty).Trim(); Category=(category??string.Empty).Trim(); Source=(source??string.Empty).Trim();
    }
}

public sealed class RebirthProgressionGraphEdge
{
    public string FromId { get; private set; }
    public string ToId { get; private set; }
    public RebirthProgressionGraphEdgeType Type { get; private set; }
    public float Minimum { get; private set; }
    public float Recommended { get; private set; }
    public bool HasMinimum { get; private set; }
    public bool HasRecommended { get; private set; }
    public string Source { get; private set; }
    public string Note { get; private set; }
    public string RequirementGroupPath { get; private set; }
    public string RequirementGroupMode { get; private set; }

    public RebirthProgressionGraphEdge(string fromId,string toId,RebirthProgressionGraphEdgeType type,float minimum,bool hasMinimum,float recommended,bool hasRecommended,string source,string note)
        : this(fromId,toId,type,minimum,hasMinimum,recommended,hasRecommended,source,note,string.Empty,string.Empty)
    {
    }

    public RebirthProgressionGraphEdge(string fromId,string toId,RebirthProgressionGraphEdgeType type,float minimum,bool hasMinimum,float recommended,bool hasRecommended,string source,string note,string requirementGroupPath,string requirementGroupMode)
    {
        FromId=(fromId??string.Empty).Trim(); ToId=(toId??string.Empty).Trim(); Type=type; Minimum=minimum; Recommended=recommended;
        HasMinimum=hasMinimum; HasRecommended=hasRecommended; Source=(source??string.Empty).Trim(); Note=(note??string.Empty).Trim();
        RequirementGroupPath=(requirementGroupPath??string.Empty).Trim(); RequirementGroupMode=(requirementGroupMode??string.Empty).Trim().ToLowerInvariant();
    }
}

public sealed class RebirthProgressionGraphSnapshot
{
    public ReadOnlyCollection<RebirthProgressionGraphNode> Nodes { get; private set; }
    public ReadOnlyCollection<RebirthProgressionGraphEdge> Edges { get; private set; }
    public string SemanticHash { get; private set; }

    public RebirthProgressionGraphSnapshot(IList<RebirthProgressionGraphNode> nodes,IList<RebirthProgressionGraphEdge> edges,string semanticHash)
    {
        Nodes=new ReadOnlyCollection<RebirthProgressionGraphNode>(new List<RebirthProgressionGraphNode>(nodes??new List<RebirthProgressionGraphNode>()));
        Edges=new ReadOnlyCollection<RebirthProgressionGraphEdge>(new List<RebirthProgressionGraphEdge>(edges??new List<RebirthProgressionGraphEdge>()));
        SemanticHash=semanticHash??string.Empty;
    }
}
