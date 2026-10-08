using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public enum RebirthProgressionGraphTraversalDirection
{
    Incoming,
    Outgoing,
    Both
}

public sealed class RebirthProgressionGraphNeighborhood
{
    public string FocusId { get; private set; }
    public ReadOnlyCollection<RebirthProgressionGraphNode> Nodes { get; private set; }
    public ReadOnlyCollection<RebirthProgressionGraphEdge> Edges { get; private set; }
    public bool WasTruncated { get; private set; }

    public RebirthProgressionGraphNeighborhood(string focusId,IList<RebirthProgressionGraphNode> nodes,IList<RebirthProgressionGraphEdge> edges,bool truncated)
    {
        FocusId=focusId??string.Empty;
        Nodes=new ReadOnlyCollection<RebirthProgressionGraphNode>(new List<RebirthProgressionGraphNode>(nodes??new List<RebirthProgressionGraphNode>()));
        Edges=new ReadOnlyCollection<RebirthProgressionGraphEdge>(new List<RebirthProgressionGraphEdge>(edges??new List<RebirthProgressionGraphEdge>()));
        WasTruncated=truncated;
    }
}

public sealed class RebirthProgressionGraphPath
{
    public bool Found { get; private set; }
    public ReadOnlyCollection<string> NodeIds { get; private set; }
    public ReadOnlyCollection<RebirthProgressionGraphEdge> Edges { get; private set; }

    public RebirthProgressionGraphPath(bool found,IList<string> nodeIds,IList<RebirthProgressionGraphEdge> edges)
    {
        Found=found;
        NodeIds=new ReadOnlyCollection<string>(new List<string>(nodeIds??new List<string>()));
        Edges=new ReadOnlyCollection<RebirthProgressionGraphEdge>(new List<RebirthProgressionGraphEdge>(edges??new List<RebirthProgressionGraphEdge>()));
    }
}

/// <summary>
/// Read-only query layer for the canonical progression graph. This service never owns progression rules;
/// it only traverses the graph built from current REBIRTH authority.
/// </summary>
public static class RebirthProgressionGraphQueryService
{
    public static RebirthProgressionGraphNeighborhood GetFocusedNeighborhood(string focusId,int incomingDepth,int outgoingDepth,int maxNodes)
    {
        EnsureReady();
        focusId=(focusId??string.Empty).Trim();
        if(incomingDepth<0)incomingDepth=0;
        if(outgoingDepth<0)outgoingDepth=0;
        if(maxNodes<1)maxNodes=1;

        RebirthProgressionGraphNode focus;
        if(!RebirthProgressionGraphRegistry.TryGetNode(focusId,out focus)||focus==null)
            return new RebirthProgressionGraphNeighborhood(focusId,new RebirthProgressionGraphNode[0],new RebirthProgressionGraphEdge[0],false);

        Dictionary<string,int> selected=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        selected[focusId]=0;
        bool truncated=false;
        Traverse(focusId,incomingDepth,maxNodes,true,selected,ref truncated);
        Traverse(focusId,outgoingDepth,maxNodes,false,selected,ref truncated);

        List<RebirthProgressionGraphNode> nodes=new List<RebirthProgressionGraphNode>();
        foreach(string id in selected.Keys)
        {
            RebirthProgressionGraphNode node;
            if(RebirthProgressionGraphRegistry.TryGetNode(id,out node)&&node!=null)nodes.Add(node);
        }
        nodes.Sort(delegate(RebirthProgressionGraphNode a,RebirthProgressionGraphNode b)
        {
            if(string.Equals(a.Id,b.Id,StringComparison.OrdinalIgnoreCase))return 0;
            if(string.Equals(a.Id,focusId,StringComparison.OrdinalIgnoreCase))return -1;
            if(string.Equals(b.Id,focusId,StringComparison.OrdinalIgnoreCase))return 1;
            int c=a.Type.CompareTo(b.Type);return c!=0?c:string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);
        });

        List<RebirthProgressionGraphEdge> edges=new List<RebirthProgressionGraphEdge>();
        HashSet<RebirthProgressionGraphEdge> seenEdges=new HashSet<RebirthProgressionGraphEdge>();
        foreach(string id in selected.Keys)
        {
            RebirthProgressionGraphEdge[] adjacent=RebirthProgressionGraphRegistry.GetOutgoing(id);
            for(int i=0;i<adjacent.Length;i++)if(selected.ContainsKey(adjacent[i].ToId)&&seenEdges.Add(adjacent[i]))edges.Add(adjacent[i]);
        }
        edges.Sort(CompareEdges);
        return new RebirthProgressionGraphNeighborhood(focusId,nodes,edges,truncated);
    }

    public static RebirthProgressionGraphPath FindShortestPath(string fromId,string toId,RebirthProgressionGraphTraversalDirection direction,int maxDepth)
    {
        EnsureReady();
        fromId=(fromId??string.Empty).Trim();toId=(toId??string.Empty).Trim();
        if(maxDepth<1)maxDepth=1;
        RebirthProgressionGraphNode a,b;
        if(!RebirthProgressionGraphRegistry.TryGetNode(fromId,out a)||a==null||!RebirthProgressionGraphRegistry.TryGetNode(toId,out b)||b==null)
            return new RebirthProgressionGraphPath(false,new string[0],new RebirthProgressionGraphEdge[0]);
        if(string.Equals(fromId,toId,StringComparison.OrdinalIgnoreCase))return new RebirthProgressionGraphPath(true,new[]{fromId},new RebirthProgressionGraphEdge[0]);

        Queue<string> q=new Queue<string>();
        Dictionary<string,int> depth=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,string> previous=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,RebirthProgressionGraphEdge> previousEdge=new Dictionary<string,RebirthProgressionGraphEdge>(StringComparer.OrdinalIgnoreCase);
        q.Enqueue(fromId);depth[fromId]=0;
        bool found=false;
        while(q.Count>0&&!found)
        {
            string current=q.Dequeue();int d=depth[current];if(d>=maxDepth)continue;
            List<RebirthProgressionGraphEdge> candidates=GetTraversalEdges(current,direction);
            for(int i=0;i<candidates.Count;i++)
            {
                RebirthProgressionGraphEdge edge=candidates[i];
                string next=string.Equals(edge.FromId,current,StringComparison.OrdinalIgnoreCase)?edge.ToId:edge.FromId;
                if(depth.ContainsKey(next))continue;
                depth[next]=d+1;previous[next]=current;previousEdge[next]=edge;
                if(string.Equals(next,toId,StringComparison.OrdinalIgnoreCase)){found=true;break;}
                q.Enqueue(next);
            }
        }
        if(!found)return new RebirthProgressionGraphPath(false,new string[0],new RebirthProgressionGraphEdge[0]);

        List<string> nodeIds=new List<string>();List<RebirthProgressionGraphEdge> pathEdges=new List<RebirthProgressionGraphEdge>();
        string cursor=toId;nodeIds.Add(cursor);
        while(!string.Equals(cursor,fromId,StringComparison.OrdinalIgnoreCase))
        {
            RebirthProgressionGraphEdge edge=previousEdge[cursor];pathEdges.Add(edge);cursor=previous[cursor];nodeIds.Add(cursor);
        }
        nodeIds.Reverse();pathEdges.Reverse();
        return new RebirthProgressionGraphPath(true,nodeIds,pathEdges);
    }

    public static RebirthProgressionGraphNode[] GetPrerequisites(string targetId)
    {
        EnsureReady();
        List<RebirthProgressionGraphNode> values=new List<RebirthProgressionGraphNode>();
        RebirthProgressionGraphEdge[] incoming=RebirthProgressionGraphRegistry.GetIncoming(targetId);
        for(int i=0;i<incoming.Length;i++)
        {
            if(!IsHardRequirement(incoming[i].Type))continue;
            RebirthProgressionGraphNode node;
            if(RebirthProgressionGraphRegistry.TryGetNode(incoming[i].FromId,out node)&&node!=null&&!ContainsNode(values,node.Id))values.Add(node);
        }
        values.Sort(delegate(RebirthProgressionGraphNode x,RebirthProgressionGraphNode y){return string.Compare(x.Id,y.Id,StringComparison.OrdinalIgnoreCase);});
        return values.ToArray();
    }

    public static RebirthProgressionGraphNode[] GetDependents(string sourceId)
    {
        EnsureReady();
        List<RebirthProgressionGraphNode> values=new List<RebirthProgressionGraphNode>();
        RebirthProgressionGraphEdge[] outgoing=RebirthProgressionGraphRegistry.GetOutgoing(sourceId);
        for(int i=0;i<outgoing.Length;i++)
        {
            if(!IsDependencyOrUnlock(outgoing[i].Type))continue;
            RebirthProgressionGraphNode node;
            if(RebirthProgressionGraphRegistry.TryGetNode(outgoing[i].ToId,out node)&&node!=null&&!ContainsNode(values,node.Id))values.Add(node);
        }
        values.Sort(delegate(RebirthProgressionGraphNode x,RebirthProgressionGraphNode y){return string.Compare(x.Id,y.Id,StringComparison.OrdinalIgnoreCase);});
        return values.ToArray();
    }

    public static RebirthProgressionGraphNode[] GetTrainingSources(string skillId)
    {
        EnsureReady();
        List<RebirthProgressionGraphNode> values=new List<RebirthProgressionGraphNode>();
        RebirthProgressionGraphEdge[] incoming=RebirthProgressionGraphRegistry.GetIncoming(skillId);
        for(int i=0;i<incoming.Length;i++)
        {
            if(incoming[i].Type!=RebirthProgressionGraphEdgeType.TrainsSkill)continue;
            RebirthProgressionGraphNode node;
            if(RebirthProgressionGraphRegistry.TryGetNode(incoming[i].FromId,out node)&&node!=null&&!ContainsNode(values,node.Id))values.Add(node);
        }
        values.Sort(delegate(RebirthProgressionGraphNode x,RebirthProgressionGraphNode y){return string.Compare(x.Id,y.Id,StringComparison.OrdinalIgnoreCase);});
        return values.ToArray();
    }

    private static void Traverse(string root,int maxDepth,int maxNodes,bool incoming,Dictionary<string,int> selected,ref bool truncated)
    {
        if(maxDepth<=0)return;
        if(selected.Count>=maxNodes)
        {
            RebirthProgressionGraphEdge[] pending=incoming?RebirthProgressionGraphRegistry.GetIncoming(root):RebirthProgressionGraphRegistry.GetOutgoing(root);
            if(pending.Length>0)truncated=true;
            return;
        }
        Queue<string> q=new Queue<string>();Queue<int> depths=new Queue<int>();
        Dictionary<string,bool> visited=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        q.Enqueue(root);depths.Enqueue(0);visited[root]=true;
        while(q.Count>0)
        {
            string current=q.Dequeue();int depth=depths.Dequeue();if(depth>=maxDepth)continue;
            RebirthProgressionGraphEdge[] edges=incoming?RebirthProgressionGraphRegistry.GetIncoming(current):RebirthProgressionGraphRegistry.GetOutgoing(current);
            Array.Sort(edges,CompareEdges);
            for(int i=0;i<edges.Length;i++)
            {
                string next=incoming?edges[i].FromId:edges[i].ToId;
                if(!selected.ContainsKey(next))
                {
                    if(selected.Count>=maxNodes){truncated=true;return;}
                    selected[next]=depth+1;
                }
                if(!visited.ContainsKey(next)){visited[next]=true;q.Enqueue(next);depths.Enqueue(depth+1);}
            }
        }
    }

    private static List<RebirthProgressionGraphEdge> GetTraversalEdges(string id,RebirthProgressionGraphTraversalDirection direction)
    {
        List<RebirthProgressionGraphEdge> result=new List<RebirthProgressionGraphEdge>();
        if(direction==RebirthProgressionGraphTraversalDirection.Outgoing||direction==RebirthProgressionGraphTraversalDirection.Both)result.AddRange(RebirthProgressionGraphRegistry.GetOutgoing(id));
        if(direction==RebirthProgressionGraphTraversalDirection.Incoming||direction==RebirthProgressionGraphTraversalDirection.Both)result.AddRange(RebirthProgressionGraphRegistry.GetIncoming(id));
        result.Sort(CompareEdges);return result;
    }

    private static int CompareEdges(RebirthProgressionGraphEdge a,RebirthProgressionGraphEdge b)
    {
        if(ReferenceEquals(a,b))return 0;
        int c=a.Type.CompareTo(b.Type);if(c!=0)return c;c=string.Compare(a.FromId,b.FromId,StringComparison.OrdinalIgnoreCase);if(c!=0)return c;c=string.Compare(a.ToId,b.ToId,StringComparison.OrdinalIgnoreCase);if(c!=0)return c;
        c=a.HasMinimum.CompareTo(b.HasMinimum);if(c!=0)return c;if(a.HasMinimum){c=a.Minimum.CompareTo(b.Minimum);if(c!=0)return c;}
        c=a.HasRecommended.CompareTo(b.HasRecommended);if(c!=0)return c;if(a.HasRecommended){c=a.Recommended.CompareTo(b.Recommended);if(c!=0)return c;}
        c=string.Compare(a.RequirementGroupPath,b.RequirementGroupPath,StringComparison.OrdinalIgnoreCase);if(c!=0)return c;return string.Compare(a.RequirementGroupMode,b.RequirementGroupMode,StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHardRequirement(RebirthProgressionGraphEdgeType type)
    {return type==RebirthProgressionGraphEdgeType.RequiresKnowledge||type==RebirthProgressionGraphEdgeType.RequiresSkill||type==RebirthProgressionGraphEdgeType.RequiresDiscipline;}
    private static bool IsDependencyOrUnlock(RebirthProgressionGraphEdgeType type)
    {return IsHardRequirement(type)||type==RebirthProgressionGraphEdgeType.UnlocksAction||type==RebirthProgressionGraphEdgeType.UnlocksRecipe||type==RebirthProgressionGraphEdgeType.UnlocksDiscipline||type==RebirthProgressionGraphEdgeType.TrainsSkill;}
    private static bool ContainsNode(List<RebirthProgressionGraphNode> values,string id){for(int i=0;i<values.Count;i++)if(string.Equals(values[i].Id,id,StringComparison.OrdinalIgnoreCase))return true;return false;}
    private static void EnsureReady(){if(!RebirthProgressionGraphRegistry.IsReady)RebirthProgressionGraphRegistry.BuildFromCurrentAuthority();}
}
