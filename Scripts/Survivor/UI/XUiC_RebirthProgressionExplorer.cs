using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// PE-04 reusable Progression Explorer shell with navigation safety over the PE-01/02 graph/query layers.
/// Permanent Main Menu, Creator and live-character launch integration belongs to PE-06/07/08.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthProgressionExplorer : XUiController
{
    public const string WindowGroupId="rebirthProgressionExplorer";
    private const int SideSlots=24;
    private const int NodeSlots=SideSlots*2+1;
    private const int VisibleSideRows=5;
    private long lastTrainingProjectionRevision=long.MinValue;
    private float trainingProjectionRefresh;
    private const int RelationshipRowStep=98;
    private const int RelationshipTrackHeight=486;
    private const string BackgroundsHomeId="__backgrounds_home__";

    private readonly XUiController[] cards=new XUiController[NodeSlots];
    private readonly XUiController[] buttons=new XUiController[NodeSlots];
    private readonly XUiV_Sprite[] cardBackgrounds=new XUiV_Sprite[NodeSlots];
    private readonly XUiV_Sprite[] cardAccents=new XUiV_Sprite[NodeSlots];
    private readonly XUiV_Sprite[] iconSprites=new XUiV_Sprite[NodeSlots];
    private readonly XUiV_Sprite[] stateBackgrounds=new XUiV_Sprite[NodeSlots];
    private readonly XUiV_Label[] stateLabels=new XUiV_Label[NodeSlots];
    private readonly XUiV_Label[] iconLabels=new XUiV_Label[NodeSlots];
    private readonly XUiV_Label[] typeLabels=new XUiV_Label[NodeSlots];
    private readonly XUiV_Label[] nameLabels=new XUiV_Label[NodeSlots];
    private readonly XUiV_Label[] relationLabels=new XUiV_Label[NodeSlots];
    private readonly XUiV_Label[] metaLabels=new XUiV_Label[NodeSlots];
    private readonly Dictionary<XUiController,int> buttonSlots=new Dictionary<XUiController,int>();
    private readonly XUiController[] incomingLines=new XUiController[SideSlots];
    private readonly XUiController[] outgoingLines=new XUiController[SideSlots];
    private readonly string[] boundNodeIds=new string[NodeSlots];

    private XUiController incomingTrunk,outgoingTrunk,incomingFocusLine,outgoingFocusLine;
    private XUiController incomingScrollTrack,outgoingScrollTrack,incomingScrollCapture,outgoingScrollCapture,incomingScrollThumbControl,outgoingScrollThumbControl;
    private XUiV_Sprite incomingScrollChrome,incomingScrollChromeInner,outgoingScrollChrome,outgoingScrollChromeInner;
    private XUiV_Button incomingScrollThumb,outgoingScrollThumb;
    private int incomingOffset,outgoingOffset;
    private XUiController incomingNativeScrollHost,outgoingNativeScrollHost,incomingNativeScrollProxy,outgoingNativeScrollProxy,incomingNativeScrollView,outgoingNativeScrollView;
    private bool syncingNativeRelationshipScroll;
    private bool backgroundBrowserPinned;
    private int postOpenRefreshFrames;
    private bool incomingThumbDragging,outgoingThumbDragging;
    private float incomingThumbDragY,outgoingThumbDragY;
    private XUiController incomingPageUp,incomingPageDown,outgoingPageUp,outgoingPageDown;
    private readonly XUiV_Sprite[] legendTypeOffAccents=new XUiV_Sprite[6];
    private XUiV_Label titleLabel,modeLabel,focusTypeLabel,focusNameLabel,focusStateLabel,focusSkillKnowledgeLabel,focusDescriptionLabel,focusCardDescriptionLabel,focusRequirementsLabel,focusSummaryLabel,leftMoreLabel,rightMoreLabel;
    private RebirthProgressionExplorerUiProjection projection;
    private string preparedFocusId=string.Empty;
    private RebirthProgressionExplorerMode preparedMode=RebirthProgressionExplorerMode.Neutral;
    private string launchReason=string.Empty;
    private RebirthProgressionExplorerReturnContext returnContext;
    private RebirthSurvivorCreationResult creatorPreviewResult;
    private readonly RebirthProgressionExplorerNavigationState navigation=new RebirthProgressionExplorerNavigationState();
    private XUiV_Label breadcrumbLabel,navigationStatusLabel,incomingHeaderLabel,outgoingHeaderLabel;
    private const int BreadcrumbSlots=10;
    private readonly XUiController[] breadcrumbButtons=new XUiController[BreadcrumbSlots];
    private readonly XUiController[] breadcrumbContainers=new XUiController[BreadcrumbSlots];
    private readonly XUiV_Label[] breadcrumbLabels=new XUiV_Label[BreadcrumbSlots];
    private readonly string[] breadcrumbNodeIds=new string[BreadcrumbSlots];
    private readonly List<string> breadcrumbPath=new List<string>();
    private readonly XUiController[] legendTypeEntries=new XUiController[6];
    private readonly XUiController[] legendTypeButtons=new XUiController[6];
    private readonly XUiV_Sprite[] legendTypeOffOverlays=new XUiV_Sprite[6];
    private readonly Dictionary<XUiController,RebirthProgressionGraphNodeType> legendTypeButtonTypes=new Dictionary<XUiController,RebirthProgressionGraphNodeType>();
    private readonly HashSet<RebirthProgressionGraphNodeType> enabledNodeTypes=new HashSet<RebirthProgressionGraphNodeType>((RebirthProgressionGraphNodeType[])Enum.GetValues(typeof(RebirthProgressionGraphNodeType)));
    private readonly XUiController[] legendStateEntries=new XUiController[5];
    private XUiController btnBack,btnForward,btnHome,btnReturn;
    // HUD Tracker Chunk F: this is a dedicated focus action. Graph-node buttons remain navigation-only.
    private XUiController trackFocusControl,btnTrackFocus;
    private XUiV_Label trackFocusLabel;
    private const int SearchSlots=6;
    private XUiC_TextInput searchInput;
    private XUiController searchPanel;
    private XUiV_Label searchEmptyLabel;
    private readonly XUiController[] searchButtons=new XUiController[SearchSlots];
    private readonly XUiV_Label[] searchTypeLabels=new XUiV_Label[SearchSlots];
    private readonly XUiV_Label[] searchNameLabels=new XUiV_Label[SearchSlots];
    private readonly XUiV_Label[] searchMetaLabels=new XUiV_Label[SearchSlots];
    private readonly Dictionary<XUiController,int> searchButtonSlots=new Dictionary<XUiController,int>();
    private RebirthProgressionExplorerSearchResult[] searchResults=new RebirthProgressionExplorerSearchResult[0];
    private string searchText=string.Empty;
    private RebirthSurvivorOwnerHeader lastLiveOwnerHeader;
    private bool hasLiveOwnerHeader;
    private string lastGraphSemanticHash=string.Empty;
    private string readingFocusId;
    private RebirthProgressionExplorerMode readingMode;
    private readonly XUiC_RebirthReadableText[] readers=new XUiC_RebirthReadableText[3];

    public void Prepare(string focusId,RebirthProgressionExplorerMode mode){Prepare(new RebirthProgressionExplorerLaunchRequest(focusId,mode));}
    public void Prepare(RebirthProgressionExplorerLaunchRequest request)
    {
        if(request==null)request=new RebirthProgressionExplorerLaunchRequest(string.Empty,RebirthProgressionExplorerMode.Neutral);
        preparedMode=request.Mode;launchReason=request.LaunchReason??string.Empty;returnContext=request.ReturnContext;creatorPreviewResult=request.CreatorPreviewResult;backgroundBrowserPinned=false;incomingOffset=0;outgoingOffset=0;navigation.Reset(BackgroundsHomeId);preparedFocusId=string.IsNullOrEmpty(request.FocusId)?BackgroundsHomeId:request.FocusId;if(!string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase))navigation.Navigate(preparedFocusId);ResetBreadcrumbPath(preparedFocusId);
    }

    public override void Init()
    {
        base.Init();
        readers[0]=GetChildById("progressionExplorerFocusDescriptionReader") as XUiC_RebirthReadableText;
        readers[1]=GetChildById("progressionExplorerRequirementsReader") as XUiC_RebirthReadableText;
        readers[2]=GetChildById("progressionExplorerSummaryReader") as XUiC_RebirthReadableText;
        RebirthHudTrackingRegistry.EnsureDefaults();
        titleLabel=Label("progressionExplorerTitle");modeLabel=Label("progressionExplorerMode");focusTypeLabel=Label("progressionExplorerFocusType");focusNameLabel=Label("progressionExplorerFocusName");
        focusStateLabel=Label("progressionExplorerFocusState");focusSkillKnowledgeLabel=Label("progressionExplorerSkillKnowledge");focusDescriptionLabel=Label("progressionExplorerFocusDescription");focusCardDescriptionLabel=Label("progressionFocusCardDescription");focusRequirementsLabel=Label("progressionExplorerRequirements");focusSummaryLabel=Label("progressionExplorerSummary");
        leftMoreLabel=Label("progressionExplorerLeftMore");rightMoreLabel=Label("progressionExplorerRightMore");breadcrumbLabel=Label("progressionExplorerBreadcrumbs");navigationStatusLabel=Label("progressionExplorerNavigationStatus");incomingHeaderLabel=Label("progressionExplorerIncomingHeader");outgoingHeaderLabel=Label("progressionExplorerOutgoingHeader");
        for(int i=0;i<NodeSlots;i++)
        {
            string n=i.ToString(CultureInfo.InvariantCulture);cards[i]=GetChildById("progressionNodeCard"+n);buttons[i]=GetChildById("btnProgressionNode"+n);
            cardBackgrounds[i]=Sprite("progressionNodeBg"+n);cardAccents[i]=Sprite("progressionNodeAccent"+n);iconSprites[i]=Sprite("progressionNodeIconSprite"+n);stateBackgrounds[i]=Sprite("progressionNodeStateBg"+n);stateLabels[i]=Label("progressionNodeState"+n);iconLabels[i]=Label("progressionNodeIcon"+n);typeLabels[i]=Label("progressionNodeType"+n);nameLabels[i]=Label("progressionNodeName"+n);relationLabels[i]=Label("progressionNodeRelation"+n);metaLabels[i]=Label("progressionNodeMeta"+n);
            if(buttons[i]!=null){buttonSlots[buttons[i]]=i;buttons[i].OnPress+=Node_OnPressed;if(i<SideSlots)WireScroll(buttons[i],delegate(float d){ScrollRelationships(true,d);});else if(i>SideSlots)WireScroll(buttons[i],delegate(float d){ScrollRelationships(false,d);});}
        }
        for(int i=0;i<SideSlots;i++){incomingLines[i]=GetChildById("progressionIncomingLine"+i);outgoingLines[i]=GetChildById("progressionOutgoingLine"+i);}
        incomingTrunk=GetChildById("progressionIncomingTrunk");outgoingTrunk=GetChildById("progressionOutgoingTrunk");incomingFocusLine=GetChildById("progressionIncomingFocusLine");outgoingFocusLine=GetChildById("progressionOutgoingFocusLine");
        incomingScrollCapture=GetChildById("progressionExplorerIncomingScrollCapture");outgoingScrollCapture=GetChildById("progressionExplorerOutgoingScrollCapture");
        incomingNativeScrollHost=GetChildById("progressionExplorerIncomingNativeScrollHost");outgoingNativeScrollHost=GetChildById("progressionExplorerOutgoingNativeScrollHost");
        incomingNativeScrollProxy=GetChildById("progressionExplorerIncomingNativeScrollProxy");outgoingNativeScrollProxy=GetChildById("progressionExplorerOutgoingNativeScrollProxy");
        incomingNativeScrollView=GetChildById("progressionExplorerIncomingNativeScrollView");outgoingNativeScrollView=GetChildById("progressionExplorerOutgoingNativeScrollView");
        WireScroll(incomingNativeScrollHost,delegate(float d){ScrollRelationships(true,d);});WireScroll(incomingNativeScrollView,delegate(float d){ScrollRelationships(true,d);});WireScroll(incomingNativeScrollProxy,delegate(float d){ScrollRelationships(true,d);});
        WireScroll(outgoingNativeScrollHost,delegate(float d){ScrollRelationships(false,d);});WireScroll(outgoingNativeScrollView,delegate(float d){ScrollRelationships(false,d);});WireScroll(outgoingNativeScrollProxy,delegate(float d){ScrollRelationships(false,d);});
        incomingScrollTrack=GetChildById("progressionExplorerIncomingScrollTrackInput");outgoingScrollTrack=GetChildById("progressionExplorerOutgoingScrollTrackInput");
        incomingScrollThumbControl=GetChildById("progressionExplorerIncomingScrollThumb");outgoingScrollThumbControl=GetChildById("progressionExplorerOutgoingScrollThumb");
        incomingScrollChrome=Sprite("progressionExplorerIncomingScrollBarChrome");incomingScrollChromeInner=Sprite("progressionExplorerIncomingScrollBarChromeInner");outgoingScrollChrome=Sprite("progressionExplorerOutgoingScrollBarChrome");outgoingScrollChromeInner=Sprite("progressionExplorerOutgoingScrollBarChromeInner");
        incomingScrollThumb=incomingScrollThumbControl!=null?incomingScrollThumbControl.ViewComponent as XUiV_Button:null;outgoingScrollThumb=outgoingScrollThumbControl!=null?outgoingScrollThumbControl.ViewComponent as XUiV_Button:null;
        WireScroll(incomingScrollCapture,delegate(float d){ScrollRelationships(true,d);});WireScroll(outgoingScrollCapture,delegate(float d){ScrollRelationships(false,d);});
        WireScroll(incomingScrollTrack,delegate(float d){ScrollRelationships(true,d);});WireScroll(outgoingScrollTrack,delegate(float d){ScrollRelationships(false,d);});
        WireScroll(incomingScrollThumbControl,delegate(float d){ScrollRelationships(true,d);});WireScroll(outgoingScrollThumbControl,delegate(float d){ScrollRelationships(false,d);});
        WireThumbDrag(incomingScrollThumbControl,true);WireThumbDrag(outgoingScrollThumbControl,false);
        incomingPageUp=GetChildById("btnProgressionExplorerIncomingPageUp");incomingPageDown=GetChildById("btnProgressionExplorerIncomingPageDown");outgoingPageUp=GetChildById("btnProgressionExplorerOutgoingPageUp");outgoingPageDown=GetChildById("btnProgressionExplorerOutgoingPageDown");WirePageRegion(incomingPageUp,true,-1);WirePageRegion(incomingPageDown,true,1);WirePageRegion(outgoingPageUp,false,-1);WirePageRegion(outgoingPageDown,false,1);
        for(int i=0;i<BreadcrumbSlots;i++){string n=i.ToString(CultureInfo.InvariantCulture);breadcrumbContainers[i]=GetChildById("progressionBreadcrumb"+n);breadcrumbButtons[i]=GetChildById("btnProgressionBreadcrumb"+n);breadcrumbLabels[i]=Label("progressionBreadcrumbLabel"+n);if(breadcrumbButtons[i]!=null)breadcrumbButtons[i].OnPress+=Breadcrumb_OnPressed;}
        string[] legendTypes={"Background","Skill","Knowledge","Action","Recipe","Discipline"};RebirthProgressionGraphNodeType[] legendNodeTypes={RebirthProgressionGraphNodeType.Background,RebirthProgressionGraphNodeType.Skill,RebirthProgressionGraphNodeType.Knowledge,RebirthProgressionGraphNodeType.Action,RebirthProgressionGraphNodeType.Recipe,RebirthProgressionGraphNodeType.Discipline};for(int i=0;i<legendTypes.Length;i++){legendTypeEntries[i]=GetChildById("progressionLegendType"+legendTypes[i]);legendTypeButtons[i]=GetChildById("btnProgressionLegendType"+legendTypes[i]);legendTypeOffOverlays[i]=Sprite("progressionLegendTypeOff"+legendTypes[i]);legendTypeOffAccents[i]=Sprite("progressionLegendTypeOffAccent"+legendTypes[i]);if(legendTypeButtons[i]!=null){legendTypeButtonTypes[legendTypeButtons[i]]=legendNodeTypes[i];legendTypeButtons[i].OnPress+=LegendType_OnPressed;}}
        string[] legendStates={"Available","Locked","Recommended","Unknown","Informational"};for(int i=0;i<legendStates.Length;i++)legendStateEntries[i]=GetChildById("progressionLegendState"+legendStates[i]);
        btnBack=Wire("btnProgressionExplorerBack",delegate{if(navigation.Back()){preparedFocusId=navigation.CurrentId;SyncBreadcrumbToFocus(preparedFocusId);Render();}});
        btnForward=Wire("btnProgressionExplorerForward",delegate{if(navigation.Forward()){preparedFocusId=navigation.CurrentId;SyncBreadcrumbToFocus(preparedFocusId);Render();}});
        btnHome=Wire("btnProgressionExplorerHome",delegate{backgroundBrowserPinned=false;navigation.Home();preparedFocusId=BackgroundsHomeId;ResetBreadcrumbPath(BackgroundsHomeId);incomingOffset=0;outgoingOffset=0;Render();});
        btnReturn=Wire("btnProgressionExplorerReturn",delegate{ReturnOrClose();});
        trackFocusControl=GetChildById("progressionExplorerTrackFocus");
        trackFocusLabel=Label("progressionExplorerTrackFocusLabel");
        btnTrackFocus=GetChildById("btnProgressionExplorerTrackFocus");
        if(btnTrackFocus!=null)btnTrackFocus.OnPress+=TrackFocus_OnPressed;
        SetVisible(trackFocusControl,false);
        searchInput=GetChildById("progressionExplorerSearchInput") as XUiC_TextInput;
        if(searchInput!=null)searchInput.OnChangeHandler+=Search_OnChanged;
        searchPanel=GetChildById("progressionExplorerSearchPanel");searchEmptyLabel=Label("progressionExplorerSearchEmpty");
        for(int i=0;i<SearchSlots;i++)
        {
            string n=i.ToString(CultureInfo.InvariantCulture);searchButtons[i]=GetChildById("btnProgressionSearch"+n);searchTypeLabels[i]=Label("progressionSearchType"+n);searchNameLabels[i]=Label("progressionSearchName"+n);searchMetaLabels[i]=Label("progressionSearchMeta"+n);
            if(searchButtons[i]!=null){searchButtonSlots[searchButtons[i]]=i;searchButtons[i].OnPress+=SearchResult_OnPressed;}
        }
        Set(titleLabel,L("xuiRebirthProgressionExplorer","PROGRESSION EXPLORER"));
        RenderSearch();
        Render();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if(windowGroup!=null)windowGroup.isEscClosable=false;
        postOpenRefreshFrames=2;hasLiveOwnerHeader=false;lastGraphSemanticHash=RebirthProgressionGraphRegistry.SemanticHash??string.Empty;
        if(RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][UI] OPEN focus="+preparedFocusId+" pinnedBackground="+backgroundBrowserPinned+" postOpenRefresh=2");
        Render();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(incomingNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(outgoingNativeScrollHost);
    }
    private bool pagingMode;
    public override void Update(float dt)
    {
        base.Update(dt);
        if(!IsOpen)return;
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; incomingThumbDragging = outgoingThumbDragging = false; if (pagingNow) Render(); }
        PollNativeRelationshipScroll(true);PollNativeRelationshipScroll(false);
        bool authorityChanged=false;
        string graphHash=RebirthProgressionGraphRegistry.SemanticHash??string.Empty;
        if(!string.Equals(graphHash,lastGraphSemanticHash,StringComparison.Ordinal)){lastGraphSemanticHash=graphHash;authorityChanged=true;}
        if(preparedMode==RebirthProgressionExplorerMode.LiveCharacter)
        {
            RebirthSurvivorOwnerHeader ownerHeader=RebirthSurvivorClientState.GetOwnerHeader();
            if(!hasLiveOwnerHeader||!ownerHeader.Equals(lastLiveOwnerHeader)){lastLiveOwnerHeader=ownerHeader;hasLiveOwnerHeader=true;authorityChanged=true;}
        }
        long trainingProjectionRevision=RebirthSkillTrainingUiProjectionService.Revision;
        if(trainingProjectionRevision!=lastTrainingProjectionRevision){lastTrainingProjectionRevision=trainingProjectionRevision;authorityChanged=true;}
        bool needsRender=authorityChanged;
        if(postOpenRefreshFrames>0)
        {
            postOpenRefreshFrames--;
            if(RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][UI] post-open refresh remaining="+postOpenRefreshFrames+" focus="+preparedFocusId);
            needsRender=true;
        }
        if(needsRender)Render();
        trainingProjectionRefresh+=Math.Max(0f,dt);
        if(trainingProjectionRefresh>=0.5f)
        {
            trainingProjectionRefresh=0f;
            if(projection!=null && !needsRender)
            {
                RebirthProgressionGraphNode liveFocus;
                if(RebirthProgressionGraphRegistry.TryGetNode(projection.FocusId,out liveFocus)&&liveFocus!=null&&liveFocus.Type==RebirthProgressionGraphNodeType.Skill)
                    Set(focusSummaryLabel,FocusSummaryWithSkillDetails());
            }
        }
        if(trackFocusControl!=null&&trackFocusControl.ViewComponent!=null&&trackFocusControl.ViewComponent.IsVisible)
        {
            string ignoredHudTrackingSaveError;
            RebirthHudTrackingPreferenceService.FlushPendingIfDue(out ignoredHudTrackingSaveError);
        }
        if(xui!=null&&xui.playerUI!=null&&xui.playerUI.playerInput!=null &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
        {
            { if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) Log.Out("[REBIRTH Progression Explorer][Input] ESC action=return caller="+(returnContext!=null?returnContext.CallerWindowGroupId:"<none>")); }
            ReturnOrClose();
        }
    }

    public override void OnClose()
    {
        string ignoredHudTrackingSaveError;
        RebirthHudTrackingPreferenceService.SaveNow(out ignoredHudTrackingSaveError);
        SetVisible(trackFocusControl,false);
        base.OnClose();
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(incomingNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(outgoingNativeScrollHost);
    }

    private void Render()
    {
        if(readingFocusId!=preparedFocusId || readingMode!=preparedMode)
        {
            readingFocusId=preparedFocusId;readingMode=preparedMode;
            foreach(var reader in readers)reader?.ResetReadingPosition();
        }
        if(RebirthSurvivorDefinitionRegistry.Bundle==null || RebirthSurvivorDefinitionRegistry.Bundle.Progression==null || !RebirthCapabilityRegistry.IsReady)
        {
            Set(modeLabel,ModeText(preparedMode));
            Set(focusTypeLabel,L("xuiRebirthProgressionExplorerLoadingType","Progression"));
            Set(focusNameLabel,L("xuiRebirthProgressionExplorerLoading","Progression data is loading"));
            Set(focusStateLabel,L("xuiRebirthProgressionExplorerLoadingState","Unavailable until Survivor definitions finish loading."));Set(focusSkillKnowledgeLabel,string.Empty);
            Set(focusDescriptionLabel,L("xuiRebirthProgressionExplorerLoadingDesc","The Explorer will use the authoritative Survivor and Capability definitions when they are ready."));Set(focusCardDescriptionLabel,string.Empty);
            Set(focusRequirementsLabel,string.Empty);Set(focusSummaryLabel,string.Empty);Set(leftMoreLabel,string.Empty);Set(rightMoreLabel,string.Empty);
            SetVisible(trackFocusControl,false);
            for(int i=0;i<NodeSlots;i++){SetVisible(cards[i],false);boundNodeIds[i]=string.Empty;}
            for(int i=0;i<SideSlots;i++){SetVisible(incomingLines[i],false);SetVisible(outgoingLines[i],false);}
            SetVisible(incomingTrunk,false);SetVisible(incomingFocusLine,false);SetVisible(outgoingTrunk,false);SetVisible(outgoingFocusLine,false);
            RenderNavigation();
            return;
        }
        if(string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase)){RenderBackgroundsHome();return;}
        if(backgroundBrowserPinned){RebirthProgressionGraphNode pinnedNode;if(RebirthProgressionGraphRegistry.TryGetNode(preparedFocusId,out pinnedNode)&&pinnedNode!=null&&pinnedNode.Type==RebirthProgressionGraphNodeType.Background){RenderBackgroundBrowserSelection();return;}backgroundBrowserPinned=false;}
        projection=RebirthProgressionExplorerUiProjectionService.Build(preparedFocusId,preparedMode,creatorPreviewResult);preparedFocusId=projection.FocusId;if(navigation.Count==0)navigation.Reset(preparedFocusId);else if(!string.Equals(navigation.CurrentId,preparedFocusId,StringComparison.OrdinalIgnoreCase))navigation.Navigate(preparedFocusId);
        Set(modeLabel,ModeText(projection.Mode));RenderSkillKnowledgeLine();Set(incomingHeaderLabel,L("xuiRebirthProgressionExplorerIncoming","PREREQUISITES / SOURCES"));Set(outgoingHeaderLabel,L("xuiRebirthProgressionExplorerOutgoing","UNLOCKS / TRAINING / OUTPUTS"));Set(focusTypeLabel,projection.FocusType);Set(focusNameLabel,FriendlyDisplayName(projection.FocusName));Set(focusStateLabel,preparedMode==RebirthProgressionExplorerMode.Neutral?L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL"):projection.FocusState);
        string authoredFocusDescription=projection.FocusDescription;RebirthBackgroundDefinition focusedBackground;if(RebirthSurvivorDefinitionRegistry.TryGetBackground(projection.FocusId,out focusedBackground)&&focusedBackground!=null)authoredFocusDescription=RebirthProgressionGraphRegistry.BuildBackgroundDescription(focusedBackground);string resolvedFocusDescription=PlayerFacingDescription(authoredFocusDescription,projection.FocusType);Set(focusDescriptionLabel,resolvedFocusDescription);Set(focusCardDescriptionLabel,resolvedFocusDescription);
        RenderFocusTrackingAction();
        Set(focusRequirementsLabel,string.Empty);RenderRequirementRows();Set(focusSummaryLabel,FocusSummaryWithSkillDetails());
        Set(leftMoreLabel,projection.HiddenIncomingCount>0?"+"+projection.HiddenIncomingCount.ToString(CultureInfo.InvariantCulture)+" more incoming":"");
        Set(rightMoreLabel,projection.HiddenOutgoingCount>0?"+"+projection.HiddenOutgoingCount.ToString(CultureInfo.InvariantCulture)+" more outgoing":"");RenderNavigation();
        for(int i=0;i<NodeSlots;i++){SetVisible(cards[i],false);boundNodeIds[i]=string.Empty;}
        int incomingCount=0,outgoingCount=0;
        for(int i=0;i<projection.Slots.Count;i++){RebirthProgressionExplorerUiSlot countSlot=projection.Slots[i];if(countSlot.Role==RebirthProgressionExplorerSlotRole.Focus||!IsNodeTypeEnabled(countSlot.NodeType))continue;if(countSlot.Role==RebirthProgressionExplorerSlotRole.Incoming)incomingCount++;else if(countSlot.Role==RebirthProgressionExplorerSlotRole.Outgoing)outgoingCount++;}
        incomingOffset=ClampOffset(incomingOffset,incomingCount,VisibleSideRows);outgoingOffset=ClampOffset(outgoingOffset,outgoingCount,VisibleSideRows);
        int incomingOrdinal=0,outgoingOrdinal=0;
        for(int i=0;i<projection.Slots.Count;i++)
        {
            RebirthProgressionExplorerUiSlot slot=projection.Slots[i];if(slot.SlotIndex<0||slot.SlotIndex>=NodeSlots)continue;if(slot.Role!=RebirthProgressionExplorerSlotRole.Focus&&!IsNodeTypeEnabled(slot.NodeType))continue;int s=slot.SlotIndex;bool show=true;int row=0;
            if(slot.Role==RebirthProgressionExplorerSlotRole.Incoming){row=incomingOrdinal-incomingOffset;show=row>=0&&row<VisibleSideRows;incomingOrdinal++;}
            else if(slot.Role==RebirthProgressionExplorerSlotRole.Outgoing){row=outgoingOrdinal-outgoingOffset;show=row>=0&&row<VisibleSideRows;outgoingOrdinal++;}
            if(!show)continue;boundNodeIds[s]=slot.NodeId;SetVisible(cards[s],true);
            if(slot.Role!=RebirthProgressionExplorerSlotRole.Focus&&cards[s]!=null&&cards[s].ViewComponent!=null)cards[s].ViewComponent.Position=new Vector2i(12,-12-row*RelationshipRowStep);
            RebirthProgressionNodeAccessState displayAccess=preparedMode==RebirthProgressionExplorerMode.Neutral?RebirthProgressionNodeAccessState.Informational:slot.AccessState;
            Set(iconLabels[s],string.Empty);Set(typeLabels[s],TypeText(slot.NodeType));Set(nameLabels[s],FriendlyDisplayName(slot.DisplayName));Set(relationLabels[s],slot.RelationText);Set(stateLabels[s],AccessText(displayAccess));Set(metaLabels[s],CardMeta(slot.MetaText,slot.NodeType,displayAccess));
            Color color=RelationshipColor(slot);if(cardAccents[s]!=null)cardAccents[s].SetColorImmediately(color);if(cardBackgrounds[s]!=null)cardBackgrounds[s].SetColorImmediately(StateBackground(displayAccess,slot.Role));
            SetNodeIcon(iconSprites[s],slot);if(stateBackgrounds[s]!=null)stateBackgrounds[s].SetColorImmediately(StateBadgeColor(displayAccess));
        }
        UpdateRelationshipScrollbar(true,incomingOffset,incomingCount);UpdateRelationshipScrollbar(false,outgoingOffset,outgoingCount);RenderLegend();
    }

    private void WireScroll(XUiController controller,Action<float> callback)
    {
        if(controller==null||callback==null)return;if(controller.ViewComponent!=null)controller.ViewComponent.EventOnScroll=true;controller.OnScroll+=delegate(XUiController sender,float delta){callback(delta);};
    }
    private void ScrollRelationships(bool incoming,float delta)
    {
        int count=GetVisibleRelationshipCount(incoming);
        int old=incoming?incomingOffset:outgoingOffset;
        int value=ScrollOffset(old,delta,count,VisibleSideRows);
        if(value==old)return;
        if(incoming)incomingOffset=value;else outgoingOffset=value;
        DebugScroll("wheel",incoming,"delta="+delta+" old="+old+" new="+value+" total="+count);
        Render();
    }
    private int GetVisibleRelationshipCount(bool incoming)
    {
        if(incoming&&(string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase)||backgroundBrowserPinned))return IsNodeTypeEnabled(RebirthProgressionGraphNodeType.Background)?GetBackgroundNodes().Count:0;
        if(projection==null)return 0;
        int count=0;
        for(int i=0;i<projection.Slots.Count;i++)
        {
            RebirthProgressionExplorerUiSlot slot=projection.Slots[i];
            if(slot.Role==(incoming?RebirthProgressionExplorerSlotRole.Incoming:RebirthProgressionExplorerSlotRole.Outgoing)&&IsNodeTypeEnabled(slot.NodeType))count++;
        }
        return count;
    }
    private static int ScrollOffset(int offset,float delta,int total,int visible)
    {
        if(RebirthScrollbarPagingPolicy.Enabled) return (int)RebirthScrollbarPagingPolicy.Step(offset, Math.Max(0,total-visible), visible, delta>0f?-1:delta<0f?1:0);
        if(delta>0f)offset--;
        else if(delta<0f)offset++;
        return ClampOffset(offset,total,visible);
    }
    private static int ClampOffset(int value,int total,int visible)
    {
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value,0,Math.Max(0,total-visible)), Math.Max(0,total-visible), visible, RebirthScrollbarPagingPolicy.Enabled);
    }
    private void WireThumbDrag(XUiController thumb,bool incoming)
    {
        if(thumb==null)return;
        if(thumb.ViewComponent!=null)thumb.ViewComponent.EventOnDrag=true;
        thumb.OnPress+=delegate(XUiController sender,int mouseButton)
        {
            DebugScroll("thumb-press",incoming,"button="+mouseButton+" offset="+(incoming?incomingOffset:outgoingOffset)+" total="+GetVisibleRelationshipCount(incoming));
        };
        thumb.OnDrag+=delegate(XUiController sender,EDragType type,Vector2 delta)
        {
            DragRelationshipThumb(incoming,type,-delta.y);
        };
    }
    private void DragRelationshipThumb(bool incoming,EDragType type,float dy)
    {
        int total=GetVisibleRelationshipCount(incoming);
        if(total<=VisibleSideRows){DebugScroll("thumb-drag",incoming,"ignored total="+total);return;}
        int h=GetThumbHeight(total,VisibleSideRows,RelationshipTrackHeight);
        int travel=Math.Max(1,RelationshipTrackHeight-h);
        int max=Math.Max(1,total-VisibleSideRows);
        bool active=incoming?incomingThumbDragging:outgoingThumbDragging;
        float dragY=incoming?incomingThumbDragY:outgoingThumbDragY;
        int currentOffset=incoming?incomingOffset:outgoingOffset;

        if(!active)
        {
            dragY=travel*(currentOffset/(float)max);
            active=true;
            DebugScroll("thumb-drag-start",incoming,"offset="+currentOffset+" pixel="+dragY+" travel="+travel+" total="+total);
        }

        if(type==EDragType.DragEnd)
        {
            DebugScroll("thumb-drag-end",incoming,"offset="+currentOffset+" pixel="+dragY);
            if(incoming){incomingThumbDragging=false;incomingThumbDragY=dragY;}else{outgoingThumbDragging=false;outgoingThumbDragY=dragY;}
            return;
        }

        dragY=Mathf.Clamp(dragY+dy,0f,travel);
        int requested=(int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt((dragY/travel)*max),0,max), max, VisibleSideRows, RebirthScrollbarPagingPolicy.Enabled);
        if(incoming){incomingOffset=requested;incomingThumbDragging=true;incomingThumbDragY=dragY;}
        else{outgoingOffset=requested;outgoingThumbDragging=true;outgoingThumbDragY=dragY;}

        DebugScroll("thumb-drag",incoming,"dy="+dy+" pixel="+dragY+" requested="+requested+" travel="+travel+" max="+max);
        Render();

        XUiV_Button thumb=incoming?incomingScrollThumb:outgoingScrollThumb;
        if(thumb!=null)
        {
            int pixel=RebirthScrollbarPagingPolicy.Enabled ? Mathf.RoundToInt(travel * (requested / (float)max)) : Mathf.RoundToInt(dragY);
            RebirthScrollbarPresentation.RenderThumb(thumb,RelationshipTrackHeight,VisibleSideRows,total,
                RebirthScrollbarPagingPolicy.Enabled?requested:dragY*max/Math.Max(1,travel),8,3);

        }
    }
    private static int GetThumbHeight(int total,int visible,int trackHeight)
    {
        if(total<=visible||total<=0)return trackHeight;
        return RebirthScrollbarPresentation.ThumbHeight(trackHeight,visible,total);
    }
    private void WirePageRegion(XUiController controller,bool incoming,int direction)
    {
        if(controller==null)return;
        controller.OnPress+=delegate(XUiController sender,int mouseButton)
        {
            int total=GetVisibleRelationshipCount(incoming);
            int old=incoming?incomingOffset:outgoingOffset;
            int requested=ClampOffset(old+(direction*VisibleSideRows),total,VisibleSideRows);
            if(incoming)incomingOffset=requested;else outgoingOffset=requested;
            DebugScroll(direction<0?"track-page-up":"track-page-down",incoming,"button="+mouseButton+" old="+old+" new="+requested+" total="+total);
            Render();
        };
        WireScroll(controller,delegate(float d){ScrollRelationships(incoming,d);});
    }
    private void UpdateRelationshipScrollbar(bool incoming,int offset,int total)
    {
        XUiController host=incoming?incomingNativeScrollHost:outgoingNativeScrollHost;
        XUiController proxy=incoming?incomingNativeScrollProxy:outgoingNativeScrollProxy;
        XUiController scrollView=incoming?incomingNativeScrollView:outgoingNativeScrollView;
        if(host!=null&&host.ViewComponent!=null)host.ViewComponent.IsVisible=true;
        if(proxy==null||proxy.ViewComponent==null)return;

        int contentHeight=total>VisibleSideRows?Math.Max(RelationshipTrackHeight+1,Mathf.CeilToInt(RelationshipTrackHeight*(total/(float)VisibleSideRows))):RelationshipTrackHeight;
        proxy.ViewComponent.Size=new Vector2i(1,contentHeight);
        int maxOffset=Math.Max(0,total-VisibleSideRows);
        float normalized=maxOffset>0?Mathf.Clamp01(offset/(float)maxOffset):0f;

        syncingNativeRelationshipScroll=true;
        RefreshNativeScrollView(scrollView);
        TrySetNativeScrollValue(scrollView,normalized);
        syncingNativeRelationshipScroll=false;
        DebugScroll("native-layout",incoming,"offset="+offset+" total="+total+" normalized="+normalized+" contentH="+contentHeight);
    }
    private void PollNativeRelationshipScroll(bool incoming)
    {
        if(syncingNativeRelationshipScroll)return;
        XUiController scrollView=incoming?incomingNativeScrollView:outgoingNativeScrollView;
        int total=GetVisibleRelationshipCount(incoming);
        int maxOffset=Math.Max(0,total-VisibleSideRows);
        if(maxOffset<=0)return;

        float normalized;
        if(!TryGetNativeScrollValue(scrollView,out normalized))return;
        int requested=(int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized)*maxOffset),0,maxOffset), maxOffset, VisibleSideRows, RebirthScrollbarPagingPolicy.Enabled);
        int current=incoming?incomingOffset:outgoingOffset;
        if(requested==current)return;

        if(incoming)incomingOffset=requested;else outgoingOffset=requested;
        DebugScroll("native-change",incoming,"normalized="+normalized+" old="+current+" new="+requested+" total="+total);
        Render();
    }
    private static void DebugScroll(string action,bool incoming,string detail)
    {
        if(!RebirthSurvivorDebug.Enabled)return;
        Log.Out("[REBIRTH Progression Explorer][Scroll] side="+(incoming?"incoming":"outgoing")+" action="+action+" "+detail);
    }

    private void RenderFocusTrackingAction()
    {
        RebirthHudTrackingId resolved;
        if(!TryResolveTrackableFocus(out resolved))
        {
            SetVisible(trackFocusControl,false);
            return;
        }
        RebirthHudTrackingPreferences preferences;
        string error;
        if(!RebirthHudTrackingPreferenceService.TryGetCurrent(out preferences,out error))
        {
            SetVisible(trackFocusControl,false);
            return;
        }
        bool tracked=preferences!=null&&preferences.Contains(resolved.Type,resolved.StableId);
        Set(trackFocusLabel,tracked?L("xuiRebirthHudTrackingUntrackFocus","UNTRACK FROM HUD"):L("xuiRebirthHudTrackingTrackFocus","TRACK ON HUD"));
        SetEnabled(btnTrackFocus,true);
        SetVisible(trackFocusControl,true);
    }

    private bool TryResolveTrackableFocus(out RebirthHudTrackingId id)
    {
        id=null;
        // Explorer tracking is a live-character UI preference. Creator-preview and neutral Explorer
        // sessions must never mutate a different/worldless Survivor preference context.
        if(preparedMode!=RebirthProgressionExplorerMode.LiveCharacter||projection==null||string.IsNullOrEmpty(projection.FocusId))return false;
        RebirthSurvivorOwnerHeader owner=RebirthSurvivorClientState.GetOwnerHeader();
        if(!owner.Available||!owner.RebirthModeEnabled||!owner.HasCharacter)return false;
        RebirthProgressionGraphNode node;
        if(!RebirthProgressionGraphRegistry.TryGetNode(projection.FocusId,out node)||node==null)return false;
        RebirthHudTrackType type;
        if(node.Type==RebirthProgressionGraphNodeType.Skill)type=RebirthHudTrackType.Skill;
        else if(node.Type==RebirthProgressionGraphNodeType.Knowledge)type=RebirthHudTrackType.Knowledge;
        else return false;
        if(!RebirthHudTrackingRegistry.CanTrack(type,node.Id))return false;
        id=new RebirthHudTrackingId(type,node.Id);
        return true;
    }

    private void TrackFocus_OnPressed(XUiController sender,int mouseButton)
    {
        RebirthHudTrackingId resolved;
        if(!TryResolveTrackableFocus(out resolved))
        {
            RenderFocusTrackingAction();
            return;
        }
        string error;
        bool ok=RebirthHudTrackingPreferenceService.Mutate(delegate(RebirthHudTrackingPreferences p)
        {
            if(p.Contains(resolved.Type,resolved.StableId))p.Remove(resolved.Type,resolved.StableId);
            else p.Add(resolved.Type,resolved.StableId);
        },out error);
        if(!ok&&RebirthSurvivorDebug.Enabled)Log.Warning("[REBIRTH Progression Explorer][HUD Tracking] focus toggle failed id="+resolved.Key+" error="+(error??string.Empty));
        else if(ok&&RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][HUD Tracking] focus toggled id="+resolved.Key);
        RenderFocusTrackingAction();
    }

    private void Node_OnPressed(XUiController sender,int mouseButton)
    {
        int slot;if(!buttonSlots.TryGetValue(sender,out slot))return;
        if(slot==24)return;
        string nodeId=slot>=0&&slot<boundNodeIds.Length?boundNodeIds[slot]:string.Empty;if(string.IsNullOrEmpty(nodeId))return;
        RebirthProgressionGraphNode node;bool isBackground=RebirthProgressionGraphRegistry.TryGetNode(nodeId,out node)&&node!=null&&node.Type==RebirthProgressionGraphNodeType.Background;
        bool browsingBackgrounds=string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase)||backgroundBrowserPinned;
        if(browsingBackgrounds&&isBackground&&slot<VisibleSideRows)
        {
            backgroundBrowserPinned=true;preparedFocusId=nodeId;outgoingOffset=0;navigation.Navigate(preparedFocusId);ResetBreadcrumbPath(preparedFocusId);Render();return;
        }
        backgroundBrowserPinned=false;preparedFocusId=nodeId;incomingOffset=0;outgoingOffset=0;navigation.Navigate(preparedFocusId);FollowBreadcrumb(preparedFocusId);Render();
    }
    private void Search_OnChanged(XUiController sender,string text,bool changeFromCode)
    {
        if(changeFromCode)return;searchText=text??string.Empty;searchResults=RebirthProgressionExplorerSearchService.Search(searchText,SearchSlots);RenderSearch();
    }
    private void SearchResult_OnPressed(XUiController sender,int mouseButton)
    {
        int slot;if(!searchButtonSlots.TryGetValue(sender,out slot)||slot<0||slot>=searchResults.Length)return;
        RebirthProgressionExplorerSearchResult result=searchResults[slot];
        preparedFocusId=result.NodeId;outgoingOffset=0;navigation.Navigate(preparedFocusId);

        if(result.NodeType==RebirthProgressionGraphNodeType.Background)
        {
            backgroundBrowserPinned=true;
            List<RebirthProgressionGraphNode> backgrounds=GetBackgroundNodes();
            int selectedIndex=backgrounds.FindIndex(delegate(RebirthProgressionGraphNode node){return node!=null&&string.Equals(node.Id,preparedFocusId,StringComparison.OrdinalIgnoreCase);});
            if(selectedIndex<0)incomingOffset=0;
            else
            {
                int maxOffset=Math.Max(0,backgrounds.Count-VisibleSideRows);
                incomingOffset=Mathf.Clamp(selectedIndex-(VisibleSideRows/2),0,maxOffset);
            }
            ResetBreadcrumbPath(preparedFocusId);
            if(RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][Search] selected background="+preparedFocusId+" browserPinned=true listIndex="+selectedIndex+" offset="+incomingOffset);
        }
        else
        {
            backgroundBrowserPinned=false;incomingOffset=0;
            ResetSearchBreadcrumbPath(preparedFocusId);
            if(RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][Search] selected="+preparedFocusId+" browserPinned=false breadcrumbMode=neutral-root");
        }

        RenderSearch(false);Render();
    }
    private void RenderSearch(){RenderSearch(true);}
    private void RenderSearch(bool showWhenPopulated)
    {
        bool hasQuery=showWhenPopulated&&!string.IsNullOrWhiteSpace(searchText);bool hasResults=searchResults!=null&&searchResults.Length>0;bool visible=hasQuery;SetVisible(searchPanel,visible);Set(searchEmptyLabel,hasQuery&&!hasResults?L("xuiRebirthProgressionExplorerSearchEmpty","No matching progression entries."):string.Empty);
        for(int i=0;i<SearchSlots;i++)
        {
            bool row=visible&&hasResults&&i<searchResults.Length;SetVisible(searchButtons[i],row);if(!row){Set(searchTypeLabels[i],string.Empty);Set(searchNameLabels[i],string.Empty);Set(searchMetaLabels[i],string.Empty);continue;}RebirthProgressionExplorerSearchResult result=searchResults[i];
            Set(searchTypeLabels[i],TypeText(result.NodeType));Set(searchNameLabels[i],result.DisplayName);Set(searchMetaLabels[i],SearchMeta(result));
        }
    }
    private static string SearchMeta(RebirthProgressionExplorerSearchResult result)
    {
        if(result==null)return string.Empty;string category=FriendlyDisplayName((result.Category??string.Empty).Trim());return category.Length>0?category:TypeText(result.NodeType);
    }
    private void ReturnOrClose()
    {
        if(xui==null||xui.playerUI==null||xui.playerUI.windowManager==null)return;var manager=xui.playerUI.windowManager;
        { if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) Log.Out("[REBIRTH Progression Explorer][Return] BEGIN caller="+(returnContext!=null?returnContext.CallerWindowGroupId:"<none>")+" callerOpenBefore="+(returnContext!=null&&returnContext.HasCaller&&manager.IsWindowOpen(returnContext.CallerWindowGroupId))); }
        manager.Close(WindowGroupId);
        if(returnContext!=null&&RebirthProgressionExplorerReturnRegistry.TryHandle(xui,returnContext))return;
        if(returnContext!=null&&returnContext.HasCaller)
        {
            // Main Menu and other callers may deliberately remain open underneath the Explorer.
            // Never close/recreate an already-open caller just to return to it.
            if(manager.IsWindowOpen(returnContext.CallerWindowGroupId)){{ if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) Log.Out("[REBIRTH Progression Explorer][Return] caller already open: "+returnContext.CallerWindowGroupId); }return;}
            XUiController caller=xui.FindWindowGroupByName(returnContext.CallerWindowGroupId);
            if(caller!=null&&caller.windowGroup!=null){manager.Open((GUIWindow)caller.windowGroup,true);{ if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) Log.Out("[REBIRTH Progression Explorer][Return] reopened caller="+returnContext.CallerWindowGroupId+" openAfter="+manager.IsWindowOpen(returnContext.CallerWindowGroupId)); }}
            else Log.Error("[REBIRTH Progression Explorer][Return] caller window group not found: "+returnContext.CallerWindowGroupId);
        }
    }
    private void RenderNavigation()
    {
        for(int i=0;i<breadcrumbNodeIds.Length;i++){breadcrumbNodeIds[i]=string.Empty;SetVisible(breadcrumbContainers[i],false);SetVisible(breadcrumbButtons[i],false);Set(breadcrumbLabels[i],string.Empty);if(breadcrumbButtons[i]?.ViewComponent!=null)breadcrumbButtons[i].ViewComponent.ToolTip=string.Empty;}
        if(breadcrumbPath.Count==0)ResetBreadcrumbPath(preparedFocusId);
        List<string> ids=new List<string>(breadcrumbPath);if(ids.Count>BreadcrumbSlots)ids.RemoveRange(1,ids.Count-BreadcrumbSlots);
        List<string> labels=new List<string>();
        for(int i=0;i<ids.Count;i++)labels.Add(i==0?L("xuiRebirthProgressionExplorerBreadcrumbRoot","PROGRESSION"):FriendlyNodeName(ids[i]));
        int[] widths=BreadcrumbWidths(labels);
        int gap=2,x=378;
        for(int i=0;i<ids.Count&&i<BreadcrumbSlots;i++)
        {
            string display=labels[i]+(i<ids.Count-1?"  ›":"");int width=widths[i];breadcrumbNodeIds[i]=ids[i];SetVisible(breadcrumbContainers[i],true);SetVisible(breadcrumbButtons[i],true);Set(breadcrumbLabels[i],display);if(breadcrumbButtons[i]?.ViewComponent!=null)breadcrumbButtons[i].ViewComponent.ToolTip=labels[i];SetControllerRect(breadcrumbContainers[i],x,-29,width,25);SetControllerRect(breadcrumbButtons[i],0,0,width,25);if(breadcrumbLabels[i]!=null){breadcrumbLabels[i].Position=new Vector2i(width/2,-13);breadcrumbLabels[i].Size=new Vector2i(width,25);}x+=width+gap;
        }
        Set(breadcrumbLabel,string.Empty);SetEnabled(btnBack,navigation.CanBack);SetEnabled(btnForward,navigation.CanForward);SetEnabled(btnHome,!string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase));SetVisible(btnReturn,true);Set(navigationStatusLabel,string.Empty);
    }
    private static int[] BreadcrumbWidths(IList<string> labels)
    {
        int count=labels.Count;
        int[] widths=new int[count];
        if(count==0)return widths;
        int available=700-(count-1)*2;
        int minimum=Math.Min(60,available/count);
        int desiredExtra=0;
        for(int i=0;i<count;i++)
        {
            int length=Math.Min(20,(labels[i]??string.Empty).Length)+(i<count-1?3:0);
            widths[i]=Math.Max(minimum,Math.Min(200,18+length*9));
            desiredExtra+=widths[i]-minimum;
        }
        int extraBudget=Math.Max(0,available-minimum*count);
        if(desiredExtra>extraBudget)
            for(int i=0;i<count;i++)widths[i]=minimum+(widths[i]-minimum)*extraBudget/desiredExtra;
        return widths;
    }
    private void ResetSearchBreadcrumbPath(string focusId)
    {
        breadcrumbPath.Clear();
        breadcrumbPath.Add(BackgroundsHomeId);
        if(!string.IsNullOrEmpty(focusId)&&!string.Equals(focusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase))breadcrumbPath.Add(focusId);
    }
    private void ResetBreadcrumbPath(string focusId)
    {
        breadcrumbPath.Clear();breadcrumbPath.Add(BackgroundsHomeId);if(string.IsNullOrEmpty(focusId)||string.Equals(focusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase))return;
        List<string> canonical=BuildCanonicalBreadcrumbPath(focusId);for(int i=0;i<canonical.Count;i++)if(!string.Equals(canonical[i],BackgroundsHomeId,StringComparison.OrdinalIgnoreCase))breadcrumbPath.Add(canonical[i]);
    }
    private void FollowBreadcrumb(string nodeId)
    {
        if(string.IsNullOrEmpty(nodeId)||string.Equals(nodeId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase)){ResetBreadcrumbPath(BackgroundsHomeId);return;}int existing=breadcrumbPath.FindIndex(delegate(string x){return string.Equals(x,nodeId,StringComparison.OrdinalIgnoreCase);});if(existing>=0){if(existing+1<breadcrumbPath.Count)breadcrumbPath.RemoveRange(existing+1,breadcrumbPath.Count-existing-1);return;}breadcrumbPath.Add(nodeId);if(breadcrumbPath.Count>BreadcrumbSlots)breadcrumbPath.RemoveAt(1);
    }
    private void SyncBreadcrumbToFocus(string focusId)
    {
        int existing=breadcrumbPath.FindIndex(delegate(string x){return string.Equals(x,focusId,StringComparison.OrdinalIgnoreCase);});
        if(existing>=0){if(existing+1<breadcrumbPath.Count)breadcrumbPath.RemoveRange(existing+1,breadcrumbPath.Count-existing-1);}
        else ResetBreadcrumbPath(focusId);
    }
    private static List<string> BuildCanonicalBreadcrumbPath(string targetId)
    {
        List<string> best=new List<string>();RebirthProgressionGraphNode target;if(!RebirthProgressionGraphRegistry.TryGetNode(targetId,out target)||target==null)return best;if(target.Type==RebirthProgressionGraphNodeType.Background){best.Add(target.Id);return best;}
        List<RebirthProgressionGraphNode> homes=GetBackgroundNodes();for(int i=0;i<homes.Count;i++){RebirthProgressionGraphNode n=homes[i];RebirthProgressionGraphPath path=RebirthProgressionGraphQueryService.FindShortestPath(n.Id,targetId,RebirthProgressionGraphTraversalDirection.Outgoing,8);if(path!=null&&path.Found&&(best.Count==0||path.NodeIds.Count<best.Count)){best=new List<string>(path.NodeIds);}}
        if(best.Count==0)best.Add(targetId);return best;
    }
    private static string FriendlyNodeName(string id){RebirthProgressionGraphNode n;return RebirthProgressionGraphRegistry.TryGetNode(id,out n)&&n!=null?FriendlyDisplayName(RebirthProgressionExplorerSearchService.ResolveDisplayName(n)):FriendlyDisplayName(id);}
    private static void SetControllerRect(XUiController c,int x,int y,int width,int height){if(c==null||c.ViewComponent==null)return;c.ViewComponent.Position=new Vector2i(x,y);c.ViewComponent.Size=new Vector2i(width,height);}
    private void Breadcrumb_OnPressed(XUiController sender,int mouseButton)
    {
        for(int i=0;i<breadcrumbButtons.Length;i++)
        {
            if(sender!=breadcrumbButtons[i])continue;
            string id=breadcrumbNodeIds[i];
            if(string.IsNullOrEmpty(id))return;
            if(string.Equals(id,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase))
            {
                backgroundBrowserPinned=false;
                navigation.Home();
                preparedFocusId=BackgroundsHomeId;
                ResetBreadcrumbPath(BackgroundsHomeId);
            }
            else
            {
                RebirthProgressionGraphNode node;
                bool isBackground=RebirthProgressionGraphRegistry.TryGetNode(id,out node)&&node!=null&&node.Type==RebirthProgressionGraphNodeType.Background;
                backgroundBrowserPinned=isBackground;
                navigation.Navigate(id);
                preparedFocusId=id;
                int pathIndex=breadcrumbPath.FindIndex(delegate(string x){return string.Equals(x,id,StringComparison.OrdinalIgnoreCase);});
                if(pathIndex>=0&&pathIndex+1<breadcrumbPath.Count)breadcrumbPath.RemoveRange(pathIndex+1,breadcrumbPath.Count-pathIndex-1);
                if(RebirthSurvivorDebug.Enabled)Log.Out("[REBIRTH Progression Explorer][BackgroundBrowser] breadcrumb id="+id+" isBackground="+isBackground+" pinned="+backgroundBrowserPinned);
            }
            incomingOffset=0;outgoingOffset=0;Render();return;
        }
    }

    private void RenderBackgroundsHome()
    {
        projection=null;SetVisible(trackFocusControl,false);Set(modeLabel,ModeText(preparedMode));Set(incomingHeaderLabel,L("xuiRebirthProgressionExplorerBackgroundsHeader","BACKGROUNDS"));Set(outgoingHeaderLabel,L("xuiRebirthProgressionExplorerBackgroundHomeHint","SELECT A BACKGROUND TO EXPLORE"));
        Set(focusTypeLabel,L("xuiRebirthProgressionExplorerProgressionType","PROGRESSION"));Set(focusNameLabel,L("xuiRebirthProgressionExplorerBackgroundsHome","Backgrounds"));Set(focusStateLabel,L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL"));Set(focusSkillKnowledgeLabel,string.Empty);
        Set(focusDescriptionLabel,L("xuiRebirthProgressionExplorerBackgroundsHomeDescription","Backgrounds represent a survivor's pre-apocalypse experience. Choose one to explore the Skills, Knowledge and other starting relationships it provides."));Set(focusCardDescriptionLabel,string.Empty);
        Set(focusRequirementsLabel,L("xuiRebirthProgressionExplorerBackgroundsHomeRequirements","Choose a Background to explore its starting progression relationships."));
        List<RebirthProgressionGraphNode> backgrounds=GetBackgroundNodes();Set(focusSummaryLabel,backgrounds.Count+" "+L("xuiRebirthProgressionExplorerBackgroundsAvailable","Backgrounds available to explore."));Set(leftMoreLabel,string.Empty);Set(rightMoreLabel,string.Empty);
        for(int i=0;i<NodeSlots;i++){SetVisible(cards[i],false);boundNodeIds[i]=string.Empty;}
        bool showBackgrounds=IsNodeTypeEnabled(RebirthProgressionGraphNodeType.Background);int visibleCount=showBackgrounds?backgrounds.Count:0;incomingOffset=Mathf.Clamp(incomingOffset,0,Math.Max(0,visibleCount-VisibleSideRows));outgoingOffset=0;
        int shown=showBackgrounds?Math.Min(VisibleSideRows,Math.Max(0,backgrounds.Count-incomingOffset)):0;
        for(int row=0;row<shown;row++){int slot=row;RebirthProgressionGraphNode node=backgrounds[incomingOffset+row];boundNodeIds[slot]=node.Id;SetVisible(cards[slot],true);if(cards[slot]!=null&&cards[slot].ViewComponent!=null)cards[slot].ViewComponent.Position=new Vector2i(12,-12-row*RelationshipRowStep);Set(iconLabels[slot],string.Empty);Set(typeLabels[slot],L("xuiRebirthProgressionExplorerTypeBackground","BACKGROUND"));Set(nameLabels[slot],RebirthProgressionExplorerSearchService.ResolveDisplayName(node));Set(relationLabels[slot],L("xuiRebirthProgressionExplorerSelectBackground","SELECT TO EXPLORE"));Set(stateLabels[slot],AccessText(RebirthProgressionNodeAccessState.Informational));Set(metaLabels[slot],string.Empty);Color color=TypeColor(RebirthProgressionGraphNodeType.Background);if(cardAccents[slot]!=null)cardAccents[slot].SetColorImmediately(color);if(cardBackgrounds[slot]!=null)cardBackgrounds[slot].SetColorImmediately(StateBackground(RebirthProgressionNodeAccessState.Informational,RebirthProgressionExplorerSlotRole.Incoming));var temp=new RebirthProgressionExplorerUiSlot(slot,node.Id,RebirthProgressionExplorerSlotRole.Incoming,node.Type,RebirthProgressionExplorerSearchService.ResolveDisplayName(node),"","",RebirthProgressionNodeAccessState.Informational);SetNodeIcon(iconSprites[slot],temp);if(stateBackgrounds[slot]!=null)stateBackgrounds[slot].SetColorImmediately(StateBadgeColor(RebirthProgressionNodeAccessState.Informational));}
        UpdateRelationshipScrollbar(true,incomingOffset,visibleCount);UpdateRelationshipScrollbar(false,0,0);RenderNavigation();RenderLegend();
    }
    private void RenderBackgroundBrowserSelection()
    {
        projection=RebirthProgressionExplorerUiProjectionService.Build(preparedFocusId,preparedMode,creatorPreviewResult);preparedFocusId=projection.FocusId;
        RenderFocusTrackingAction();
        Set(modeLabel,ModeText(projection.Mode));Set(incomingHeaderLabel,L("xuiRebirthProgressionExplorerBackgroundBrowserHeader","BACKGROUNDS"));Set(outgoingHeaderLabel,L("xuiRebirthProgressionExplorerBackgroundBrowserHint","STARTING RELATIONSHIPS"));
        Set(focusTypeLabel,projection.FocusType);Set(focusNameLabel,FriendlyDisplayName(projection.FocusName));Set(focusStateLabel,preparedMode==RebirthProgressionExplorerMode.Neutral?L("xuiRebirthProgressionExplorerStateInfo","INFORMATIONAL"):projection.FocusState);RenderSkillKnowledgeLine();
        string authoredFocusDescription=projection.FocusDescription;RebirthBackgroundDefinition focusedBackground;if(RebirthSurvivorDefinitionRegistry.TryGetBackground(projection.FocusId,out focusedBackground)&&focusedBackground!=null)authoredFocusDescription=RebirthProgressionGraphRegistry.BuildBackgroundDescription(focusedBackground);string resolvedFocusDescription=PlayerFacingDescription(authoredFocusDescription,projection.FocusType);Set(focusDescriptionLabel,resolvedFocusDescription);Set(focusCardDescriptionLabel,resolvedFocusDescription);Set(focusRequirementsLabel,string.Empty);RenderRequirementRows();Set(focusSummaryLabel,FocusSummaryWithSkillDetails());Set(leftMoreLabel,string.Empty);Set(rightMoreLabel,string.Empty);
        for(int i=0;i<NodeSlots;i++){SetVisible(cards[i],false);boundNodeIds[i]=string.Empty;}

        List<RebirthProgressionGraphNode> backgrounds=GetBackgroundNodes();bool showBackgrounds=IsNodeTypeEnabled(RebirthProgressionGraphNodeType.Background);int backgroundCount=showBackgrounds?backgrounds.Count:0;incomingOffset=Mathf.Clamp(incomingOffset,0,Math.Max(0,backgroundCount-VisibleSideRows));
        int shown=showBackgrounds?Math.Min(VisibleSideRows,Math.Max(0,backgroundCount-incomingOffset)):0;
        for(int row=0;row<shown;row++)
        {
            int s=row;RebirthProgressionGraphNode node=backgrounds[incomingOffset+row];boundNodeIds[s]=node.Id;SetVisible(cards[s],true);if(cards[s]!=null&&cards[s].ViewComponent!=null)cards[s].ViewComponent.Position=new Vector2i(12,-12-row*RelationshipRowStep);
            bool selected=string.Equals(node.Id,preparedFocusId,StringComparison.OrdinalIgnoreCase);Set(iconLabels[s],string.Empty);Set(typeLabels[s],L("xuiRebirthProgressionExplorerTypeBackground","BACKGROUND"));Set(nameLabels[s],RebirthProgressionExplorerSearchService.ResolveDisplayName(node));Set(relationLabels[s],selected?L("xuiRebirthProgressionExplorerSelectedBackground","SELECTED"):L("xuiRebirthProgressionExplorerSelectBackground","SELECT TO EXPLORE"));Set(stateLabels[s],AccessText(RebirthProgressionNodeAccessState.Informational));Set(metaLabels[s],string.Empty);
            Color color=TypeColor(RebirthProgressionGraphNodeType.Background);if(cardAccents[s]!=null)cardAccents[s].SetColorImmediately(color);if(cardBackgrounds[s]!=null)cardBackgrounds[s].SetColorImmediately(selected?new Color32(64,48,35,255):StateBackground(RebirthProgressionNodeAccessState.Informational,RebirthProgressionExplorerSlotRole.Incoming));
            var temp=new RebirthProgressionExplorerUiSlot(s,node.Id,RebirthProgressionExplorerSlotRole.Incoming,node.Type,RebirthProgressionExplorerSearchService.ResolveDisplayName(node),"","",RebirthProgressionNodeAccessState.Informational);SetNodeIcon(iconSprites[s],temp);if(stateBackgrounds[s]!=null)stateBackgrounds[s].SetColorImmediately(StateBadgeColor(RebirthProgressionNodeAccessState.Informational));
        }

        RebirthProgressionExplorerUiSlot focusSlot=null;List<RebirthProgressionExplorerUiSlot> outgoing=new List<RebirthProgressionExplorerUiSlot>();
        for(int i=0;i<projection.Slots.Count;i++){RebirthProgressionExplorerUiSlot slot=projection.Slots[i];if(slot.Role==RebirthProgressionExplorerSlotRole.Focus)focusSlot=slot;else if(slot.Role==RebirthProgressionExplorerSlotRole.Outgoing&&IsNodeTypeEnabled(slot.NodeType))outgoing.Add(slot);}
        if(focusSlot!=null)
        {
            int s=24;boundNodeIds[s]=focusSlot.NodeId;SetVisible(cards[s],true);RebirthProgressionNodeAccessState access=preparedMode==RebirthProgressionExplorerMode.Neutral?RebirthProgressionNodeAccessState.Informational:focusSlot.AccessState;
            Set(iconLabels[s],string.Empty);Set(typeLabels[s],TypeText(focusSlot.NodeType));Set(nameLabels[s],FriendlyDisplayName(focusSlot.DisplayName));Set(relationLabels[s],focusSlot.RelationText);Set(stateLabels[s],AccessText(access));Set(metaLabels[s],CardMeta(focusSlot.MetaText,focusSlot.NodeType,access));
            Color color=RelationshipColor(focusSlot);if(cardAccents[s]!=null)cardAccents[s].SetColorImmediately(color);if(cardBackgrounds[s]!=null)cardBackgrounds[s].SetColorImmediately(StateBackground(access,focusSlot.Role));SetNodeIcon(iconSprites[s],focusSlot);if(stateBackgrounds[s]!=null)stateBackgrounds[s].SetColorImmediately(StateBadgeColor(access));
        }

        outgoingOffset=Mathf.Clamp(outgoingOffset,0,Math.Max(0,outgoing.Count-VisibleSideRows));int outgoingShown=Math.Min(VisibleSideRows,Math.Max(0,outgoing.Count-outgoingOffset));
        for(int row=0;row<outgoingShown;row++)
        {
            RebirthProgressionExplorerUiSlot slot=outgoing[outgoingOffset+row];int s=25+row;if(s>=NodeSlots)break;boundNodeIds[s]=slot.NodeId;SetVisible(cards[s],true);if(cards[s]!=null&&cards[s].ViewComponent!=null)cards[s].ViewComponent.Position=new Vector2i(12,-12-row*RelationshipRowStep);RebirthProgressionNodeAccessState access=preparedMode==RebirthProgressionExplorerMode.Neutral?RebirthProgressionNodeAccessState.Informational:slot.AccessState;
            Set(iconLabels[s],string.Empty);Set(typeLabels[s],TypeText(slot.NodeType));Set(nameLabels[s],FriendlyDisplayName(slot.DisplayName));Set(relationLabels[s],slot.RelationText);Set(stateLabels[s],AccessText(access));Set(metaLabels[s],CardMeta(slot.MetaText,slot.NodeType,access));Color color=RelationshipColor(slot);if(cardAccents[s]!=null)cardAccents[s].SetColorImmediately(color);if(cardBackgrounds[s]!=null)cardBackgrounds[s].SetColorImmediately(StateBackground(access,RebirthProgressionExplorerSlotRole.Outgoing));SetNodeIcon(iconSprites[s],slot);if(stateBackgrounds[s]!=null)stateBackgrounds[s].SetColorImmediately(StateBadgeColor(access));
        }
        UpdateRelationshipScrollbar(true,incomingOffset,backgroundCount);UpdateRelationshipScrollbar(false,outgoingOffset,outgoing.Count);RenderNavigation();RenderLegend();
    }

    private static List<RebirthProgressionGraphNode> GetBackgroundNodes()
    {
        List<RebirthProgressionGraphNode> result=new List<RebirthProgressionGraphNode>();RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle!=null&&bundle.Backgrounds!=null){for(int i=0;i<bundle.Backgrounds.Count;i++){RebirthBackgroundDefinition bg=bundle.Backgrounds[i];if(bg==null)continue;RebirthProgressionGraphNode node;if(RebirthProgressionGraphRegistry.TryGetNode(bg.Id,out node)&&node!=null)result.Add(node);}}
        if(result.Count==0){RebirthProgressionGraphNode[] all=RebirthProgressionGraphRegistry.GetNodesSnapshot();for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].Type==RebirthProgressionGraphNodeType.Background)result.Add(all[i]);}
        result.Sort(delegate(RebirthProgressionGraphNode a,RebirthProgressionGraphNode b){return string.Compare(RebirthProgressionExplorerSearchService.ResolveDisplayName(a),RebirthProgressionExplorerSearchService.ResolveDisplayName(b),StringComparison.CurrentCultureIgnoreCase);});
        return result;
    }
    private bool IsNodeTypeEnabled(RebirthProgressionGraphNodeType type){return enabledNodeTypes.Contains(type);}
    private void LegendType_OnPressed(XUiController sender,int mouseButton)
    {
        RebirthProgressionGraphNodeType type;if(!legendTypeButtonTypes.TryGetValue(sender,out type))return;if(enabledNodeTypes.Contains(type))enabledNodeTypes.Remove(type);else enabledNodeTypes.Add(type);incomingOffset=0;outgoingOffset=0;Render();
    }
    private void RenderRequirementRows()
    {
        if(projection==null||projection.Requirements==null||projection.Requirements.Count==0){Set(focusRequirementsLabel,L("xuiRebirthProgressionExplorerNoRequirements","No direct progression requirements are exposed for this entry."));return;}
        // Keep every requirement readable in one bounded native scrolling region.
        // The old six fixed rows could extend into the Context section below.
        var text = new System.Text.StringBuilder();
        for(int i=0;i<projection.Requirements.Count;i++)
        {
            var row=projection.Requirements[i];
            if(i>0)text.Append("\n\n");
            text.Append("[B58CFF]").Append(row.DisplayName).Append("[-]\n").Append(row.Detail);
        }
        Set(focusRequirementsLabel,text.ToString());
    }
    private void RenderLegend()
    {
        HashSet<RebirthProgressionGraphNodeType> types=new HashSet<RebirthProgressionGraphNodeType>();
        if(string.Equals(preparedFocusId,BackgroundsHomeId,StringComparison.OrdinalIgnoreCase)||backgroundBrowserPinned)types.Add(RebirthProgressionGraphNodeType.Background);
        if(projection!=null)
        {
            for(int i=0;i<projection.Slots.Count;i++)
            {
                RebirthProgressionExplorerUiSlot slot=projection.Slots[i];
                if(slot.Role!=RebirthProgressionExplorerSlotRole.Focus)types.Add(slot.NodeType);
            }
        }
        RebirthProgressionGraphNodeType[] order={RebirthProgressionGraphNodeType.Background,RebirthProgressionGraphNodeType.Skill,RebirthProgressionGraphNodeType.Knowledge,RebirthProgressionGraphNodeType.Action,RebirthProgressionGraphNodeType.Recipe,RebirthProgressionGraphNodeType.Discipline};int x=108;for(int i=0;i<order.Length;i++){bool visible=types.Contains(order[i]);SetVisible(legendTypeEntries[i],visible);bool filtered=visible&&!IsNodeTypeEnabled(order[i]);if(legendTypeOffOverlays[i]!=null)legendTypeOffOverlays[i].IsVisible=filtered;if(legendTypeOffAccents[i]!=null)legendTypeOffAccents[i].IsVisible=filtered;if(visible){int w=order[i]==RebirthProgressionGraphNodeType.Background?142:118;SetControllerRect(legendTypeEntries[i],x,-4,w,34);if(legendTypeButtons[i]!=null&&legendTypeButtons[i].ViewComponent!=null)legendTypeButtons[i].ViewComponent.Size=new Vector2i(w,34);if(legendTypeOffOverlays[i]!=null)legendTypeOffOverlays[i].Size=new Vector2i(w,34);if(legendTypeOffAccents[i]!=null){legendTypeOffAccents[i].Size=new Vector2i(w,3);legendTypeOffAccents[i].Position=new Vector2i(0,-31);}x+=w+14;}}
        if(preparedMode==RebirthProgressionExplorerMode.Neutral){for(int i=0;i<legendStateEntries.Length;i++)SetVisible(legendStateEntries[i],i==4);if(legendStateEntries[4]!=null)SetControllerRect(legendStateEntries[4],88,-43,185,30);return;}
        HashSet<RebirthProgressionNodeAccessState> states=new HashSet<RebirthProgressionNodeAccessState>();if(projection!=null)for(int i=0;i<projection.Slots.Count;i++)states.Add(projection.Slots[i].AccessState);RebirthProgressionNodeAccessState[] stateOrder={RebirthProgressionNodeAccessState.Available,RebirthProgressionNodeAccessState.Locked,RebirthProgressionNodeAccessState.AvailableWithRecommendations,RebirthProgressionNodeAccessState.Unknown,RebirthProgressionNodeAccessState.Informational};int sx=88;for(int i=0;i<stateOrder.Length;i++){bool visible=states.Contains(stateOrder[i]);SetVisible(legendStateEntries[i],visible);if(visible){int w=i==2?175:(i==4?185:145);SetControllerRect(legendStateEntries[i],sx,-43,w,30);sx+=w+10;}}
    }
    private string FocusSummaryWithSkillDetails()
    {
        string summary=projection!=null?(projection.FocusSummary??string.Empty):string.Empty;
        if(projection==null)return summary;
        RebirthProgressionGraphNode node;
        if(!RebirthProgressionGraphRegistry.TryGetNode(projection.FocusId,out node)||node==null||node.Type!=RebirthProgressionGraphNodeType.Skill)return summary;
        EntityPlayer player=preparedMode==RebirthProgressionExplorerMode.LiveCharacter&&xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        string detail=RebirthCharacterUiSnapshotBuilder.BuildExplorerSkillDetails(player,node.Id,preparedMode==RebirthProgressionExplorerMode.LiveCharacter);
        if(string.IsNullOrEmpty(detail))return summary;
        return string.IsNullOrEmpty(summary)?detail:summary+"\n\n"+detail;
    }

    private void RenderSkillKnowledgeLine()
    {
        if(focusSkillKnowledgeLabel==null)return;
        if(projection==null){Set(focusSkillKnowledgeLabel,string.Empty);return;}
        RebirthProgressionGraphNode node;
        if(!RebirthProgressionGraphRegistry.TryGetNode(projection.FocusId,out node)||node==null||node.Type!=RebirthProgressionGraphNodeType.Skill)
        {Set(focusSkillKnowledgeLabel,string.Empty);return;}
        if(preparedMode==RebirthProgressionExplorerMode.Neutral)
        {Set(focusSkillKnowledgeLabel,L("xuiRebirthProgressionExplorerSkillLayerLabels","PRACTICAL SKILL  •  THEORY"));return;}
        RebirthSurvivorOwnerStateSnapshot snapshot=RebirthSurvivorClientState.GetOwnerStateSnapshot();
        float practical=0f,theory=0f;bool hasPractical=false,hasTheory=false;
        if(preparedMode==RebirthProgressionExplorerMode.CreatorPreview&&creatorPreviewResult!=null)
        {hasPractical=creatorPreviewResult.StartingSkills.TryGetValue(node.Id,out practical);hasTheory=creatorPreviewResult.StartingSkillKnowledge.TryGetValue(node.Id,out theory);}
        else if(snapshot!=null)
        {
            for(int i=0;i<snapshot.Skills.Count;i++)if(snapshot.Skills[i]!=null&&string.Equals(snapshot.Skills[i].Id,node.Id,StringComparison.OrdinalIgnoreCase)){practical=snapshot.Skills[i].Value;hasPractical=true;break;}
            for(int i=0;i<snapshot.SkillKnowledge.Count;i++)if(snapshot.SkillKnowledge[i]!=null&&string.Equals(snapshot.SkillKnowledge[i].Id,node.Id,StringComparison.OrdinalIgnoreCase)){theory=snapshot.SkillKnowledge[i].Value;hasTheory=true;break;}
        }
        string p=hasPractical?practical.ToString("0.0",CultureInfo.InvariantCulture):"?";
        string k=hasTheory?theory.ToString("0.0",CultureInfo.InvariantCulture):"?";
        Set(focusSkillKnowledgeLabel,L("xuiRebirthProgressionExplorerPracticalShort","Skill").ToUpperInvariant()+" "+p+"   •   "+L("xuiRebirthProgressionExplorerSkillKnowledge","Theory").ToUpperInvariant()+" "+k);
    }

    private static string FriendlyDisplayName(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return string.Empty;string v=value.Trim();int colon=v.IndexOf(':');if(colon>=0&&colon+1<v.Length){string prefix=v.Substring(0,colon+1);string suffix=v.Substring(colon+1).Trim();return prefix+" "+FriendlyDisplayName(suffix);}if(v.StartsWith("skill.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("knowledge.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("recipe.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("procedure.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("pattern.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("background.",StringComparison.OrdinalIgnoreCase)||v.StartsWith("discipline.",StringComparison.OrdinalIgnoreCase))v=v.Substring(v.IndexOf('.')+1);v=v.Replace('_',' ').Replace('.',' ').Replace('-',' ');v=Regex.Replace(v,"(?<=[a-z0-9])(?=[A-Z])"," ");string[] parts=v.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);for(int i=0;i<parts.Length;i++)if(parts[i].Length>0)parts[i]=char.ToUpperInvariant(parts[i][0])+parts[i].Substring(1);return string.Join(" ",parts);
    }
    private string CardMeta(string meta, RebirthProgressionGraphNodeType type, RebirthProgressionNodeAccessState access)
    {
        if(preparedMode==RebirthProgressionExplorerMode.Neutral)return string.Empty;
        string value=(meta??string.Empty).Trim();
        return string.Equals(value,AccessText(access),StringComparison.OrdinalIgnoreCase)
            ||string.Equals(value,TypeText(type),StringComparison.OrdinalIgnoreCase)?string.Empty:value;
    }
    private static string PlayerFacingDescription(string authored,string type)
    {
        string value=(authored??string.Empty).Trim();
        if(value.StartsWith("xui",StringComparison.OrdinalIgnoreCase)||value.StartsWith("lbl",StringComparison.OrdinalIgnoreCase))
        {
            string localized=Localization.Get(value);
            if(!string.IsNullOrEmpty(localized)&&!string.Equals(localized,value,StringComparison.OrdinalIgnoreCase))value=localized;
        }
        string lower=value.ToLowerInvariant();
        if(lower.Contains("individual_recipe_discovery") || lower.Contains("binary what knowledge"))
            return L("xuiRebirthProgressionExplorerRecipeDiscoveryHelp", "Learn this recipe from its matching recipe card or manual. Learning it does not replace the skill, materials or workstation needed to craft it.");
        bool technical=lower.Contains("has_current_")||lower.Contains("runtime_gate")||lower.Contains("associated skill link")||lower.Contains("relationship;")||lower.Contains("_relationship");
        value=string.IsNullOrWhiteSpace(value)||technical?FallbackDescription(type):value;
        return string.IsNullOrEmpty(value)?value:char.ToUpper(value[0],CultureInfo.CurrentCulture)+value.Substring(1);
    }
    private static string FallbackDescription(string type)
    {
        string t=(type??string.Empty).ToUpperInvariant();if(t.Contains("SKILL"))return L("xuiRebirthProgressionExplorerSkillDescriptionFallback","A practical proficiency improved through legitimate gameplay. Related Knowledge can unlock additional activities that train it.");if(t.Contains("KNOWLEDGE"))return L("xuiRebirthProgressionExplorerKnowledgeDescriptionFallback","A learned recipe or technique that can unlock a specific craft or activity.");if(t.Contains("ACTION"))return L("xuiRebirthProgressionExplorerActionDescriptionFallback","A player activity performed in the world. Actions can require Knowledge and can train one or more Skills.");if(t.Contains("BACKGROUND"))return L("xuiRebirthProgressionExplorerBackgroundDescriptionFallback","A survivor's pre-apocalypse experience. Backgrounds provide starting Skills, Knowledge and other starting biases; they are not permanent classes.");if(t.Contains("RECIPE"))return L("xuiRebirthProgressionExplorerRecipeDescriptionFallback","A craft or process that may depend on Knowledge, Skills, tools or a workstation.");if(t.Contains("DISCIPLINE"))return L("xuiRebirthProgressionExplorerDisciplineDescriptionFallback","A discoverable advanced progression path earned through Rebirth gameplay. Disciplines are not character-creation classes.");return L("xuiRebirthProgressionExplorerNoDescription","No additional description is authored for this entry yet.");
    }
    private XUiController Wire(string id,Action action){XUiController c=GetChildById(id);if(c!=null&&action!=null)c.OnPress+=delegate{action();};return c;}
    private static void SetEnabled(XUiController c,bool enabled){if(c!=null&&c.ViewComponent!=null)c.ViewComponent.Enabled=enabled;}
    private void SetNavStatus(string value){Set(navigationStatusLabel,string.Empty);}
    private static string ModeText(RebirthProgressionExplorerMode mode)
    {
        if(mode==RebirthProgressionExplorerMode.CreatorPreview)return L("xuiRebirthProgressionExplorerModeCreator","CREATOR PREVIEW");
        if(mode==RebirthProgressionExplorerMode.LiveCharacter)return L("xuiRebirthProgressionExplorerModeLive","LIVE CHARACTER");
        return L("xuiRebirthProgressionExplorerModeNeutral","NEUTRAL EXPLORATION");
    }
    private static string TypeText(RebirthProgressionGraphNodeType type)
    {
        string key="xuiRebirthProgressionExplorerType"+type.ToString();
        string fallback=type==RebirthProgressionGraphNodeType.Action?"ACTION / ACTIVITY":type.ToString().ToUpperInvariant();
        return L(key,fallback);
    }
    private static string AccessText(RebirthProgressionNodeAccessState state)
    {
        if(state==RebirthProgressionNodeAccessState.Locked)return "LOCKED";
        if(state==RebirthProgressionNodeAccessState.Available)return "AVAILABLE";
        if(state==RebirthProgressionNodeAccessState.AvailableWithRecommendations)return "RECOMMENDED";
        if(state==RebirthProgressionNodeAccessState.Unknown)return "UNKNOWN";
        return "INFORMATIONAL";
    }
    private static Color StateBadgeColor(RebirthProgressionNodeAccessState state)
    {
        if(state==RebirthProgressionNodeAccessState.Locked)return new Color32(106,45,45,255);
        if(state==RebirthProgressionNodeAccessState.Available)return new Color32(39,91,57,255);
        if(state==RebirthProgressionNodeAccessState.AvailableWithRecommendations)return new Color32(120,92,30,255);
        if(state==RebirthProgressionNodeAccessState.Unknown)return new Color32(62,62,68,255);
        return new Color32(42,68,88,255);
    }
    private static string IconSpriteName(RebirthProgressionExplorerUiSlot slot)
    {
        if(slot==null)return "rb_ui_knowledge";
        if(slot.NodeType==RebirthProgressionGraphNodeType.Skill)
        {
            string id=(slot.NodeId??string.Empty).Trim().ToLowerInvariant();
            if(id.StartsWith("skill."))id=id.Substring(6);
            id=id.Replace('.','_').Replace('-','_');
            return "rb_skill_"+id;
        }
        if(slot.NodeType==RebirthProgressionGraphNodeType.Knowledge)return "rb_ui_knowledge";
        if(slot.NodeType==RebirthProgressionGraphNodeType.Action)return "rb_skill_maintenance";
        if(slot.NodeType==RebirthProgressionGraphNodeType.Recipe)return "rb_trait_systems_thinker";
        if(slot.NodeType==RebirthProgressionGraphNodeType.Background)return "rb_trait_generalist";
        if(slot.NodeType==RebirthProgressionGraphNodeType.Discipline)return "rb_trait_generalist";
        return "rb_ui_knowledge";
    }
    private static void SetNodeIcon(XUiV_Sprite sprite,RebirthProgressionExplorerUiSlot slot)
    {
        if(sprite==null||slot==null)return;
        sprite.UIAtlas="RebirthSurvivorIcons";
        sprite.SetSpriteImmediately(IconSpriteName(slot));
        sprite.SetColorImmediately(RelationshipColor(slot));
    }
    private static Color RelationshipColor(RebirthProgressionExplorerUiSlot slot)
    {
        if(slot!=null&&slot.NodeType==RebirthProgressionGraphNodeType.Skill&&slot.IsStartingSkillAdjustment&&slot.HasRelationshipValue)
            return slot.IsWeakness?new Color32(204,107,100,255):new Color32(143,209,143,255);
        return TypeColor(slot!=null?slot.NodeType:RebirthProgressionGraphNodeType.Skill);
    }
    private static Color TypeColor(RebirthProgressionGraphNodeType type)
    {
        if(type==RebirthProgressionGraphNodeType.Skill)return new Color32(129,171,222,255);if(type==RebirthProgressionGraphNodeType.Knowledge)return new Color32(181,140,255,255);
        if(type==RebirthProgressionGraphNodeType.Action)return new Color32(118,190,145,255);if(type==RebirthProgressionGraphNodeType.Recipe)return new Color32(218,174,92,255);
        if(type==RebirthProgressionGraphNodeType.Background)return new Color32(189,140,99,255);if(type==RebirthProgressionGraphNodeType.Discipline)return new Color32(201,198,105,255);return new Color32(160,160,160,255);
    }
    private static Color StateBackground(RebirthProgressionNodeAccessState state,RebirthProgressionExplorerSlotRole role)
    {
        return role==RebirthProgressionExplorerSlotRole.Focus ? new Color32(29,27,36,255) : new Color32(24,24,29,255);
    }
    private XUiV_Label Label(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Label:null;}
    private XUiV_Sprite Sprite(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Sprite:null;}
    private static void Set(XUiV_Label l,string value){if(l!=null)l.Text=value??string.Empty;}
    private static void SetVisible(XUiController c,bool visible){if(c!=null&&c.ViewComponent!=null)c.ViewComponent.IsVisible=visible;}
    private static string L(string key,string fallback){string v=Localization.Get(key??string.Empty);return string.IsNullOrEmpty(v)||v==key?fallback:v;}

    private static bool TryGetNativeScrollValue(XUiController controller,out float value)
    {
        return RebirthNativeScrollbarUtil.TryGetValue(controller,out value);
    }
    private static bool TrySetNativeScrollValue(XUiController controller,float value)
    {
        return RebirthNativeScrollbarUtil.TrySetValue(controller,value);
    }
    private static void RefreshNativeScrollView(XUiController controller)
    {
        RebirthNativeScrollbarUtil.Refresh(controller);
    }

}

public static class RebirthProgressionExplorerUiService
{
    public static bool Open(XUi xui,string focusId,RebirthProgressionExplorerMode mode,out string error)
    {
        error=string.Empty;if(xui==null||xui.playerUI==null||xui.playerUI.windowManager==null){error="Player UI is unavailable.";return false;}
        XUiController group=xui.FindWindowGroupByName(XUiC_RebirthProgressionExplorer.WindowGroupId);XUiC_RebirthProgressionExplorer controller=group!=null?group.GetChildByType<XUiC_RebirthProgressionExplorer>():null;
        if(controller==null){error="Progression Explorer window is not registered in this XUi context.";return false;}
        return Open(xui,new RebirthProgressionExplorerLaunchRequest(focusId,mode),out error);
    }
    public static bool Open(XUi xui,RebirthProgressionExplorerLaunchRequest request,out string error)
    {
        error=string.Empty;if(xui==null||xui.playerUI==null||xui.playerUI.windowManager==null){error="Player UI is unavailable.";return false;}
        XUiController group=xui.FindWindowGroupByName(XUiC_RebirthProgressionExplorer.WindowGroupId);XUiC_RebirthProgressionExplorer controller=group!=null?group.GetChildByType<XUiC_RebirthProgressionExplorer>():null;
        if(controller==null){error="Progression Explorer window is not registered in this XUi context.";return false;}
        controller.Prepare(request);xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,true,false);return true;
    }



}
