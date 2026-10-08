using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

#nullable disable

public enum RebirthProgressionExplorerSlotRole
{
    Incoming,
    Focus,
    Outgoing
}

public sealed class RebirthProgressionExplorerUiSlot
{
    public int SlotIndex { get; private set; }
    public string NodeId { get; private set; }
    public RebirthProgressionExplorerSlotRole Role { get; private set; }
    public RebirthProgressionGraphNodeType NodeType { get; private set; }
    public string DisplayName { get; private set; }
    public string RelationText { get; private set; }
    public string MetaText { get; private set; }
    public RebirthProgressionNodeAccessState AccessState { get; private set; }
    public bool HasRelationshipValue { get; private set; }
    public float RelationshipValue { get; private set; }
    public bool IsWeakness { get { return HasRelationshipValue && RelationshipValue < 0f; } }
    public bool IsStartingSkillAdjustment { get; private set; }

    public RebirthProgressionExplorerUiSlot(int slotIndex,string nodeId,RebirthProgressionExplorerSlotRole role,RebirthProgressionGraphNodeType nodeType,string displayName,string relationText,string metaText,RebirthProgressionNodeAccessState accessState)
        : this(slotIndex,nodeId,role,nodeType,displayName,relationText,metaText,accessState,false,0f,false)
    {
    }
    public RebirthProgressionExplorerUiSlot(int slotIndex,string nodeId,RebirthProgressionExplorerSlotRole role,RebirthProgressionGraphNodeType nodeType,string displayName,string relationText,string metaText,RebirthProgressionNodeAccessState accessState,bool hasRelationshipValue,float relationshipValue,bool isStartingSkillAdjustment)
    {
        SlotIndex=slotIndex;NodeId=nodeId??string.Empty;Role=role;NodeType=nodeType;DisplayName=displayName??string.Empty;
        RelationText=relationText??string.Empty;MetaText=metaText??string.Empty;AccessState=accessState;
        HasRelationshipValue=hasRelationshipValue;RelationshipValue=relationshipValue;IsStartingSkillAdjustment=isStartingSkillAdjustment;
    }
}

public sealed class RebirthProgressionExplorerRequirementRow
{
    public string RequirementId { get; private set; }
    public RebirthProgressionGraphNodeType NodeType { get; private set; }
    public string DisplayName { get; private set; }
    public string Detail { get; private set; }
    public RebirthProgressionExplorerRequirementRow(string id,RebirthProgressionGraphNodeType type,string name,string detail){RequirementId=id??string.Empty;NodeType=type;DisplayName=name??string.Empty;Detail=detail??string.Empty;}
}

public sealed class RebirthProgressionExplorerUiProjection
{
    public string FocusId { get; private set; }
    public string FocusName { get; private set; }
    public string FocusType { get; private set; }
    public string FocusDescription { get; private set; }
    public string FocusState { get; private set; }
    public string FocusRequirements { get; private set; }
    public string FocusSummary { get; private set; }
    public RebirthProgressionExplorerMode Mode { get; private set; }
    public int HiddenIncomingCount { get; private set; }
    public int HiddenOutgoingCount { get; private set; }
    public ReadOnlyCollection<RebirthProgressionExplorerUiSlot> Slots { get; private set; }
    public ReadOnlyCollection<RebirthProgressionExplorerRequirementRow> Requirements { get; private set; }

    public RebirthProgressionExplorerUiProjection(string focusId,string focusName,string focusType,string focusDescription,string focusState,string focusRequirements,string focusSummary,RebirthProgressionExplorerMode mode,int hiddenIncoming,int hiddenOutgoing,IList<RebirthProgressionExplorerUiSlot> slots,IList<RebirthProgressionExplorerRequirementRow> requirements=null)
    {
        FocusId=focusId??string.Empty;FocusName=focusName??string.Empty;FocusType=focusType??string.Empty;FocusDescription=focusDescription??string.Empty;
        FocusState=focusState??string.Empty;FocusRequirements=focusRequirements??string.Empty;FocusSummary=focusSummary??string.Empty;Mode=mode;
        HiddenIncomingCount=Math.Max(0,hiddenIncoming);HiddenOutgoingCount=Math.Max(0,hiddenOutgoing);
        Slots=new ReadOnlyCollection<RebirthProgressionExplorerUiSlot>(new List<RebirthProgressionExplorerUiSlot>(slots??new List<RebirthProgressionExplorerUiSlot>()));Requirements=new ReadOnlyCollection<RebirthProgressionExplorerRequirementRow>(new List<RebirthProgressionExplorerRequirementRow>(requirements??new List<RebirthProgressionExplorerRequirementRow>()));
    }
}

/// <summary>
/// PE-03 presentation projection. It converts the canonical graph plus a PE-02 read-only overlay into
/// a small, deterministic focus neighborhood suitable for XUi. No progression rules are owned here.
/// </summary>
public static class RebirthProgressionExplorerUiProjectionService
{
    public const int SideSlotCount=24;

    public static RebirthProgressionExplorerUiProjection Build(string focusId,RebirthProgressionExplorerMode mode,RebirthSurvivorCreationResult creatorPreviewResult=null)
    {
        if(!RebirthProgressionGraphRegistry.IsReady)RebirthProgressionGraphRegistry.BuildFromCurrentAuthority();
        focusId=ResolveFocusId(focusId);
        RebirthProgressionGraphNeighborhood neighborhood=RebirthProgressionGraphQueryService.GetFocusedNeighborhood(focusId,1,1,256);
        RebirthProgressionExplorerOverlaySnapshot overlay=BuildOverlay(neighborhood,mode,creatorPreviewResult);
        RebirthProgressionGraphNode focus=null;RebirthProgressionGraphRegistry.TryGetNode(focusId,out focus);
        if(focus==null)return new RebirthProgressionExplorerUiProjection(focusId,focusId,"UNKNOWN","","Unavailable","","No progression node was found for this focus.",mode,0,0,new RebirthProgressionExplorerUiSlot[0]);

        List<RelationCandidate> incoming=BuildCandidates(focusId,true,overlay);
        List<RelationCandidate> outgoing=BuildCandidates(focusId,false,overlay);
        incoming.Sort(CompareCandidates);outgoing.Sort(CompareCandidates);
        List<RebirthProgressionExplorerUiSlot> slots=new List<RebirthProgressionExplorerUiSlot>();
        int shownIncoming=Math.Min(SideSlotCount,incoming.Count);
        for(int i=0;i<shownIncoming;i++)slots.Add(ToSlot(i,incoming[i],RebirthProgressionExplorerSlotRole.Incoming));
        RebirthProgressionNodeOverlay focusOverlay=null;overlay.TryGet(focusId,out focusOverlay);
        slots.Add(new RebirthProgressionExplorerUiSlot(SideSlotCount,focus.Id,RebirthProgressionExplorerSlotRole.Focus,focus.Type,Name(focus),"FOCUS",BuildMeta(focus,focusOverlay),focusOverlay!=null?focusOverlay.AccessState:RebirthProgressionNodeAccessState.Informational));
        int shownOutgoing=Math.Min(SideSlotCount,outgoing.Count);
        for(int i=0;i<shownOutgoing;i++)slots.Add(ToSlot(SideSlotCount+1+i,outgoing[i],RebirthProgressionExplorerSlotRole.Outgoing));

        string requirements=BuildRequirements(focusOverlay);List<RebirthProgressionExplorerRequirementRow> requirementRows=BuildRequirementRows(focusOverlay,mode);
        string summary=BuildFocusSummary(focus,incoming.Count,outgoing.Count,shownIncoming,shownOutgoing,focusOverlay,mode);
        string focusState=mode==RebirthProgressionExplorerMode.Neutral?"○ "+L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL"):StateText(focus,focusOverlay);
        return new RebirthProgressionExplorerUiProjection(focus.Id,Name(focus),TypeText(focus.Type),focus.Description,focusState,requirements,summary,mode,incoming.Count-shownIncoming,outgoing.Count-shownOutgoing,slots,requirementRows);
    }

    private static RebirthProgressionExplorerOverlaySnapshot BuildOverlay(RebirthProgressionGraphNeighborhood n,RebirthProgressionExplorerMode mode,RebirthSurvivorCreationResult creatorPreviewResult)
    {
        if(mode==RebirthProgressionExplorerMode.LiveCharacter)return RebirthProgressionExplorerOverlayService.CreateLiveCharacter(n,RebirthSurvivorClientState.GetOwnerStateSnapshot());
        if(mode==RebirthProgressionExplorerMode.CreatorPreview)
        {
            if(creatorPreviewResult!=null)return RebirthProgressionExplorerOverlayService.CreateCreatorPreview(n,creatorPreviewResult);
            RebirthSurvivorCreationNetworkResponse last=RebirthSurvivorClientState.GetLastCreationResult();
            return RebirthProgressionExplorerOverlayService.CreateCreatorPreview(n,last!=null?last.ValidationResult:null);
        }
        return RebirthProgressionExplorerOverlayService.CreateNeutral(n);
    }

    private static List<RelationCandidate> BuildCandidates(string focusId,bool incoming,RebirthProgressionExplorerOverlaySnapshot overlay)
    {
        RebirthProgressionGraphEdge[] edges=incoming?RebirthProgressionGraphRegistry.GetIncoming(focusId):RebirthProgressionGraphRegistry.GetOutgoing(focusId);
        Dictionary<string,RelationCandidate> byNode=new Dictionary<string,RelationCandidate>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<edges.Length;i++)
        {
            RebirthProgressionGraphEdge e=edges[i];string other=incoming?e.FromId:e.ToId;RebirthProgressionGraphNode node;
            if(!RebirthProgressionGraphRegistry.TryGetNode(other,out node)||node==null)continue;
            RebirthProgressionNodeOverlay state=null;overlay.TryGet(other,out state);
            RelationCandidate current;
            if(!byNode.TryGetValue(other,out current))byNode[other]=new RelationCandidate(node,e,state);
            else current.AddEdge(e);
        }
        return new List<RelationCandidate>(byNode.Values);
    }

    private static RebirthProgressionExplorerUiSlot ToSlot(int index,RelationCandidate c,RebirthProgressionExplorerSlotRole role)
    {
        bool hasValue=false;float value=0f;bool startingSkill=false;
        for(int i=0;i<c.Edges.Count;i++)
        {
            RebirthProgressionGraphEdge e=c.Edges[i];
            if(e.Type==RebirthProgressionGraphEdgeType.GrantedByBackground&&c.Node.Type==RebirthProgressionGraphNodeType.Skill&&e.HasMinimum)
            {
                hasValue=true;value=e.Minimum;startingSkill=true;break;
            }
        }
        return new RebirthProgressionExplorerUiSlot(index,c.Node.Id,role,c.Node.Type,Name(c.Node),RelationText(c),BuildMeta(c.Node,c.Overlay),c.Overlay!=null?c.Overlay.AccessState:RebirthProgressionNodeAccessState.Informational,hasValue,value,startingSkill);
    }

    private static int CompareCandidates(RelationCandidate a,RelationCandidate b)
    {
        int t=a.Node.Type.CompareTo(b.Node.Type);
        return t!=0?t:string.Compare(Name(a.Node),Name(b.Node),StringComparison.CurrentCultureIgnoreCase);
    }
    private static string RelationText(RelationCandidate c)
    {
        if(c.Edges.Count==0)return string.Empty;
        for(int i=0;i<c.Edges.Count;i++)
        {
            RebirthProgressionGraphEdge e=c.Edges[i];
            if(e.Type!=RebirthProgressionGraphEdgeType.GrantedByBackground)continue;
            if(c.Node.Type==RebirthProgressionGraphNodeType.Skill&&e.HasMinimum)
            {
                string signed=(e.Minimum>0f?"+":string.Empty)+e.Minimum.ToString("0.#",CultureInfo.InvariantCulture);
                if(e.Minimum<0f)return "[CC6B64]"+L("xuiRebirthProgressionExplorerWeakness","WEAKNESS")+"  "+signed+"[-]";
                return "[8FD18F]"+L("xuiRebirthProgressionExplorerStartingSkill","STARTING SKILL")+"  "+signed+"[-]";
            }
            if(c.Node.Type==RebirthProgressionGraphNodeType.Knowledge)return L("xuiRebirthProgressionExplorerStartingKnowledge","STARTING RECIPES & TECHNIQUES");
        }
        for(int i=0;i<c.Edges.Count;i++)
        {
            RebirthProgressionGraphEdge e=c.Edges[i];
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresKnowledge)
                return L("xuiRebirthProgressionExplorerRequiresKnowledge","Requires Recipe / Technique");
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresSkill)
            {
                string minimum=e.HasMinimum?"  "+L("xuiRebirthProgressionExplorerMinimum","MINIMUM")+" "+e.Minimum.ToString("0.#",CultureInfo.InvariantCulture):string.Empty;
                return L("xuiRebirthProgressionExplorerRequiresSkill","REQUIRES SKILL")+minimum;
            }
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresDiscipline)
                return L("xuiRebirthProgressionExplorerRequiresDiscipline","REQUIRES DISCIPLINE");
            if(e.Type==RebirthProgressionGraphEdgeType.UnlocksDiscipline)
                return L("xuiRebirthProgressionExplorerUnlocksDiscipline","UNLOCKS DISCIPLINE");
            if(e.Type==RebirthProgressionGraphEdgeType.TrainsSkill)
                return L("xuiRebirthProgressionExplorerTrainsSkill","TRAINS SKILL");
        }
        List<string> names=new List<string>();
        for(int i=0;i<c.Edges.Count;i++){string relation=SplitEnum(c.Edges[i].Type.ToString()).ToUpperInvariant();if(!names.Contains(relation))names.Add(relation);}
        return string.Join(" / ",names.ToArray());
    }
    private static string BuildMeta(RebirthProgressionGraphNode node,RebirthProgressionNodeOverlay overlay)
    {
        if(node==null)return string.Empty;
        if(node.Type==RebirthProgressionGraphNodeType.Skill&&overlay!=null&&overlay.HasSkillValue)
        {
            string practical=overlay.SkillValue.ToString("0.0",CultureInfo.InvariantCulture);
            string theory=overlay.HasSkillKnowledgeValue?overlay.SkillKnowledgeValue.ToString("0.0",CultureInfo.InvariantCulture):"?";
            return "SKILL "+practical+"  •  KNOWLEDGE "+theory;
        }
        if(node.Type==RebirthProgressionGraphNodeType.Knowledge&&overlay!=null)return overlay.IsLearnedKnowledge?"LEARNED":"NOT LEARNED";
        if(overlay!=null&&(node.Type==RebirthProgressionGraphNodeType.Action||node.Type==RebirthProgressionGraphNodeType.Recipe||node.Type==RebirthProgressionGraphNodeType.Discipline))return SplitEnum(overlay.AccessState.ToString()).ToUpperInvariant();
        return SplitEnum(node.Type.ToString()).ToUpperInvariant();
    }
    private static List<RebirthProgressionExplorerRequirementRow> BuildRequirementRows(RebirthProgressionNodeOverlay overlay,RebirthProgressionExplorerMode mode)
    {
        List<RebirthProgressionExplorerRequirementRow> rows=new List<RebirthProgressionExplorerRequirementRow>();
        if(overlay==null)return rows;
        for(int i=0;i<overlay.Requirements.Count;i++)
        {
            RebirthProgressionRequirementOverlay r=overlay.Requirements[i];
            RebirthProgressionGraphNode node;
            RebirthProgressionGraphNodeType type;
            if(RebirthProgressionGraphRegistry.TryGetNode(r.RequirementId,out node)&&node!=null)
            {
                type=node.Type;
                if(type!=RebirthProgressionGraphNodeType.Skill&&type!=RebirthProgressionGraphNodeType.Knowledge&&type!=RebirthProgressionGraphNodeType.Discipline&&type!=RebirthProgressionGraphNodeType.Action)continue;
            }
            else if((r.Kind??string.Empty).IndexOf("skill",StringComparison.OrdinalIgnoreCase)>=0)type=RebirthProgressionGraphNodeType.Skill;
            else if((r.Kind??string.Empty).IndexOf("knowledge",StringComparison.OrdinalIgnoreCase)>=0)type=RebirthProgressionGraphNodeType.Knowledge;
            else if((r.Kind??string.Empty).IndexOf("discipline",StringComparison.OrdinalIgnoreCase)>=0)type=RebirthProgressionGraphNodeType.Discipline;
            else if((r.Kind??string.Empty).IndexOf("trial",StringComparison.OrdinalIgnoreCase)>=0||(r.Kind??string.Empty).IndexOf("accomplishment",StringComparison.OrdinalIgnoreCase)>=0||(r.Kind??string.Empty).IndexOf("action",StringComparison.OrdinalIgnoreCase)>=0)type=RebirthProgressionGraphNodeType.Action;
            else continue;

            string detail;
            if(mode==RebirthProgressionExplorerMode.Neutral)
            {
                if(type==RebirthProgressionGraphNodeType.Skill)detail=r.RequiredValue>0f?"Minimum "+r.RequiredValue.ToString("0.#",CultureInfo.InvariantCulture):"Required Skill";
                else if(type==RebirthProgressionGraphNodeType.Discipline)detail="Required Discipline";
                else if(type==RebirthProgressionGraphNodeType.Action)detail="Required Milestone / Trial";
                else detail="Required Recipe / Technique";
            }
            else
            {
                if(r.State==RebirthProgressionRequirementState.Satisfied)detail="Satisfied";
                else if(r.State==RebirthProgressionRequirementState.Blocked)detail="Not met";
                else if(r.State==RebirthProgressionRequirementState.RecommendationOnly)detail="Recommended";
                else detail="Status unavailable";
                if(r.RequiredValue>0f)detail+="  "+r.CurrentValue.ToString("0.#",CultureInfo.InvariantCulture)+" / "+r.RequiredValue.ToString("0.#",CultureInfo.InvariantCulture);
            }
            rows.Add(new RebirthProgressionExplorerRequirementRow(r.RequirementId,type,DisplayName(r.RequirementId),detail));
        }
        return rows;
    }

    private static string BuildRequirements(RebirthProgressionNodeOverlay overlay)
    {
        if(overlay==null||overlay.Requirements.Count==0)return L("xuiRebirthProgressionExplorerNoRequirements","No direct progression requirements are exposed for this node.");
        StringBuilder b=new StringBuilder();
        for(int i=0;i<overlay.Requirements.Count;i++)
        {
            RebirthProgressionRequirementOverlay r=overlay.Requirements[i];if(i>0)b.Append('\n');
            b.Append(StateGlyph(r.State)).Append(' ').Append(DisplayName(r.RequirementId)).Append("  [").Append(SplitEnum(r.State.ToString())).Append(']');
            if(r.RequiredValue!=0f||r.CurrentValue!=0f)b.Append("  ").Append(r.CurrentValue.ToString("0.#",CultureInfo.InvariantCulture)).Append(" / ").Append(r.RequiredValue.ToString("0.#",CultureInfo.InvariantCulture));
        }
        return b.ToString();
    }
    private static string BuildFocusSummary(RebirthProgressionGraphNode focus,int incoming,int outgoing,int shownIncoming,int shownOutgoing,RebirthProgressionNodeOverlay overlay,RebirthProgressionExplorerMode mode)
    {
        StringBuilder b=new StringBuilder();
        if(focus!=null&&focus.Type==RebirthProgressionGraphNodeType.Skill)
        {
            if(mode==RebirthProgressionExplorerMode.Neutral)b.Append(L("xuiRebirthProgressionExplorerSkillLayersNeutral","Practical Skill is HOW WELL you perform. Theory is WHY/HOW understanding learned from study, instruction, and Insights."));
            else if(overlay!=null&&overlay.HasSkillValue)
            {
                b.Append(L("xuiRebirthProgressionExplorerPracticalSkill","Practical Skill")).Append(": ").Append(overlay.SkillValue.ToString("0.0",CultureInfo.InvariantCulture));
                b.Append("  •  ").Append(L("xuiRebirthProgressionExplorerSkillKnowledge","Theory")).Append(": ").Append(overlay.HasSkillKnowledgeValue?overlay.SkillKnowledgeValue.ToString("0.0",CultureInfo.InvariantCulture):L("xuiRebirthProgressionExplorerUnavailable","Unavailable"));
            }
            AppendLiteratureHints(b,focus.Id,true);
            string relationshipHint=RebirthSurvivorUiText.BuildSkillRelationshipHint(focus.Id);
            if(!string.IsNullOrEmpty(relationshipHint)){if(b.Length>0)b.Append("\n");b.Append("[D6C978]").Append(relationshipHint).Append("[-]");}
        }
        else if(focus!=null&&focus.Type==RebirthProgressionGraphNodeType.Knowledge)
        {
            if(mode!=RebirthProgressionExplorerMode.Neutral&&overlay!=null)
                b.Append(overlay.IsLearnedKnowledge?L("xuiRebirthProgressionExplorerKnowledgeLearnedDetail","Learned by this survivor."):L("xuiRebirthProgressionExplorerKnowledgeMissingDetail","Not learned by this survivor."));
            AppendLiteratureHints(b,focus.Id,false);
        }
        if(b.Length>0)b.Append("\n");
        if(incoming>shownIncoming||outgoing>shownOutgoing)
        {
            if(b.Length>0)b.Append("\n");
            b.Append("Scroll the cards to see more requirements and things you can unlock.");
        }
        return b.ToString();
    }

    private static void AppendLiteratureHints(StringBuilder b,string id,bool theory)
    {
        RebirthLiteratureDefinition[] literature=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
        List<string> names=new List<string>();
        for(int i=0;i<literature.Length;i++)
        {
            RebirthLiteratureDefinition d=literature[i];if(d==null)continue;
            bool match=theory?string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase)&&string.Equals(d.SkillId,id,StringComparison.OrdinalIgnoreCase)
                :string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase)&&string.Equals(d.KnowledgeId,id,StringComparison.OrdinalIgnoreCase);
            if(!match)continue;
            string name=Localization.Get(d.ItemId);
            if(string.IsNullOrEmpty(name)||string.Equals(name,d.ItemId,StringComparison.OrdinalIgnoreCase))name=FriendlyLiteratureName(d.ItemId);
            if(!names.Contains(name))names.Add(name);
        }
        if(names.Count==0)return;
        if(b.Length>0)b.Append("\n");
        b.Append(theory?L("xuiRebirthProgressionExplorerStudySources","Study sources: "):L("xuiRebirthProgressionExplorerAcquisitionSources","Acquisition: "));
        int shown=Math.Min(3,names.Count);for(int i=0;i<shown;i++){if(i>0)b.Append(", ");b.Append(names[i]);}
        if(names.Count>shown)b.Append(" +").Append(names.Count-shown).Append(" more");
    }

    private static string FriendlyLiteratureName(string id)
    {
        string v=id??string.Empty;if(v.StartsWith("rebirth",StringComparison.OrdinalIgnoreCase))v=v.Substring(7);
        StringBuilder b=new StringBuilder();for(int i=0;i<v.Length;i++){char c=v[i];if(i>0&&char.IsUpper(c)&&!char.IsUpper(v[i-1]))b.Append(' ');b.Append(c);}return b.ToString().Trim();
    }

    private static string StateText(RebirthProgressionGraphNode node,RebirthProgressionNodeOverlay overlay)
    {
        if(node!=null&&node.Type==RebirthProgressionGraphNodeType.Knowledge&&overlay!=null)
            return overlay.IsLearnedKnowledge?"✓ "+L("xuiRebirthProgressionExplorerStateLearned","LEARNED"):"× "+L("xuiRebirthProgressionExplorerStateNotLearned","NOT LEARNED");
        if(overlay==null)return "○ "+L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL");
        if(overlay.AccessState==RebirthProgressionNodeAccessState.Locked)return "× "+L("xuiRebirthProgressionExplorerStateLocked","LOCKED");
        if(overlay.AccessState==RebirthProgressionNodeAccessState.Available)return "✓ "+L("xuiRebirthProgressionExplorerStateAvailable","AVAILABLE");
        if(overlay.AccessState==RebirthProgressionNodeAccessState.AvailableWithRecommendations)return "△ "+L("xuiRebirthProgressionExplorerStateRecommended","AVAILABLE / RECOMMENDED");
        if(overlay.AccessState==RebirthProgressionNodeAccessState.Unknown)return "? "+L("xuiRebirthProgressionExplorerStateUnknown","UNKNOWN");
        return "○ "+L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL");
    }
    private static string TypeText(RebirthProgressionGraphNodeType type)
    {
        string v=Localization.Get("xuiRebirthProgressionExplorerType"+type.ToString());
        return string.IsNullOrEmpty(v)||v=="xuiRebirthProgressionExplorerType"+type.ToString()?type.ToString().ToUpperInvariant():v;
    }
    private static string L(string key,string fallback){string v=Localization.Get(key??string.Empty);return string.IsNullOrEmpty(v)||v==key?fallback:v;}
    private static string StateGlyph(RebirthProgressionRequirementState s){if(s==RebirthProgressionRequirementState.Satisfied)return "✓";if(s==RebirthProgressionRequirementState.Blocked)return "✕";if(s==RebirthProgressionRequirementState.RecommendationOnly)return "△";return "○";}
    private static string DisplayName(string id)
    {
        RebirthProgressionGraphNode n;if(RebirthProgressionGraphRegistry.TryGetNode(id,out n)&&n!=null)return Name(n);
        string value=(id??string.Empty).Trim();int dot=value.LastIndexOf('.');if(dot>=0&&dot+1<value.Length)value=value.Substring(dot+1);
        value=value.Replace('_',' ').Replace('-',' ');if(value.Length==0)return string.Empty;
        string[] parts=value.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);for(int i=0;i<parts.Length;i++)if(parts[i].Length>0)parts[i]=char.ToUpperInvariant(parts[i][0])+parts[i].Substring(1);return string.Join(" ",parts);
    }
    private static string Name(RebirthProgressionGraphNode node)
    {
        return RebirthProgressionExplorerSearchService.ResolveDisplayName(node);
    }
    private static string ResolveFocusId(string requested)
    {
        string id=(requested??string.Empty).Trim();RebirthProgressionGraphNode n;if(!string.IsNullOrEmpty(id)&&RebirthProgressionGraphRegistry.TryGetNode(id,out n)&&n!=null)return n.Id;
        string[] preferred={"skill.mechanics","skill.electrical","knowledge.electrical.fundamentals"};for(int i=0;i<preferred.Length;i++)if(RebirthProgressionGraphRegistry.TryGetNode(preferred[i],out n)&&n!=null)return n.Id;
        RebirthProgressionGraphNode[] all=RebirthProgressionGraphRegistry.GetNodesSnapshot();return all.Length>0?all[0].Id:string.Empty;
    }
    private static string SplitEnum(string value)
    {
        if(string.IsNullOrEmpty(value))return string.Empty;StringBuilder b=new StringBuilder();for(int i=0;i<value.Length;i++){char c=value[i];if(i>0&&char.IsUpper(c)&&!char.IsUpper(value[i-1]))b.Append(' ');b.Append(c);}return b.ToString();
    }

    private sealed class RelationCandidate
    {
        public readonly RebirthProgressionGraphNode Node;public readonly RebirthProgressionNodeOverlay Overlay;public readonly List<RebirthProgressionGraphEdge> Edges=new List<RebirthProgressionGraphEdge>();
        public RelationCandidate(RebirthProgressionGraphNode node,RebirthProgressionGraphEdge edge,RebirthProgressionNodeOverlay overlay){Node=node;Overlay=overlay;AddEdge(edge);}
        public void AddEdge(RebirthProgressionGraphEdge edge){if(edge==null)return;for(int i=0;i<Edges.Count;i++)if(Edges[i].Type==edge.Type&&Math.Abs(Edges[i].Minimum-edge.Minimum)<0.0001f&&Edges[i].HasMinimum==edge.HasMinimum)return;Edges.Add(edge);}
    }
}
