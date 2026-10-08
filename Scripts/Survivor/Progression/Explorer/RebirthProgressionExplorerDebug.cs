using System;
using System.Globalization;
using System.Text;

#nullable disable

public static class RebirthProgressionExplorerDebug
{
    public static string BuildNeighborhood(string id,int incomingDepth,int outgoingDepth,int maxNodes)
    {
        RebirthProgressionGraphNeighborhood n=RebirthProgressionGraphQueryService.GetFocusedNeighborhood(id,incomingDepth,outgoingDepth,maxNodes);
        StringBuilder b=new StringBuilder();b.Append("[REBIRTH ProgressionExplorer] neighborhood focus=").Append(n.FocusId).Append(" nodes=").Append(n.Nodes.Count).Append(" edges=").Append(n.Edges.Count).Append(" truncated=").Append(n.WasTruncated);
        for(int i=0;i<n.Nodes.Count;i++)b.Append("\n  node ").Append(n.Nodes[i].Id).Append(" type=").Append(n.Nodes[i].Type);
        return b.ToString();
    }

    public static string BuildPath(string fromId,string toId,string direction)
    {
        RebirthProgressionGraphTraversalDirection d=RebirthProgressionGraphTraversalDirection.Both;
        if(string.Equals(direction,"incoming",StringComparison.OrdinalIgnoreCase))d=RebirthProgressionGraphTraversalDirection.Incoming;
        else if(string.Equals(direction,"outgoing",StringComparison.OrdinalIgnoreCase))d=RebirthProgressionGraphTraversalDirection.Outgoing;
        RebirthProgressionGraphPath p=RebirthProgressionGraphQueryService.FindShortestPath(fromId,toId,d,12);
        StringBuilder b=new StringBuilder();b.Append("[REBIRTH ProgressionExplorer] path found=").Append(p.Found).Append(" from=").Append(fromId).Append(" to=").Append(toId).Append(" direction=").Append(d);
        if(p.Found)for(int i=0;i<p.NodeIds.Count;i++){if(i>0)b.Append(" -> ");b.Append(p.NodeIds[i]);}
        return b.ToString();
    }

    public static string BuildOverlay(string id,string mode)
    {
        RebirthProgressionGraphNeighborhood n=RebirthProgressionGraphQueryService.GetFocusedNeighborhood(id,2,2,32);
        RebirthProgressionExplorerOverlaySnapshot overlay;
        if(string.Equals(mode,"live",StringComparison.OrdinalIgnoreCase))overlay=RebirthProgressionExplorerOverlayService.CreateLiveCharacter(n,RebirthSurvivorClientState.GetOwnerStateSnapshot());
        else if(string.Equals(mode,"creator",StringComparison.OrdinalIgnoreCase))
        {
            RebirthSurvivorCreationNetworkResponse last=RebirthSurvivorClientState.GetLastCreationResult();
            overlay=RebirthProgressionExplorerOverlayService.CreateCreatorPreview(n,last!=null?last.ValidationResult:null);
        }
        else overlay=RebirthProgressionExplorerOverlayService.CreateNeutral(n);
        StringBuilder b=new StringBuilder();b.Append("[REBIRTH ProgressionExplorer] overlay mode=").Append(overlay.Mode).Append(" focus=").Append(id).Append(" background=").Append(overlay.BackgroundId).Append(" nodes=").Append(overlay.Nodes.Count);
        for(int i=0;i<overlay.Nodes.Count;i++)
        {
            RebirthProgressionNodeOverlay x=overlay.Nodes[i];b.Append("\n  ").Append(x.NodeId).Append(" access=").Append(x.AccessState);
            if(x.HasSkillValue)b.Append(" skill=").Append(x.SkillValue.ToString("0.##",CultureInfo.InvariantCulture)).Append(" progress=").Append(x.SkillProgress.ToString("0.##",CultureInfo.InvariantCulture));
            if(x.IsLearnedKnowledge)b.Append(" learned=true");if(x.IsSelectedBackground)b.Append(" selectedBackground=true");if(x.IsSelectedTrait)b.Append(" selectedTrait=true");
            for(int r=0;r<x.Requirements.Count;r++){RebirthProgressionRequirementOverlay q=x.Requirements[r];b.Append("\n    req ").Append(q.Kind).Append(' ').Append(q.RequirementId).Append(" state=").Append(q.State);if(q.RequiredValue!=0f||q.CurrentValue!=0f)b.Append(" current=").Append(q.CurrentValue.ToString("0.##",CultureInfo.InvariantCulture)).Append(" required=").Append(q.RequiredValue.ToString("0.##",CultureInfo.InvariantCulture));}
        }
        return b.ToString();
    }
}
