using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Final Survivor Character presentation. This controller is intentionally a projection over the
/// owner snapshot plus the metabolism snapshot: it does not own progression, conditions,
/// metabolism, persistence, or gear state. The only mutation exposed here is the already
/// server-authoritative Survivor gear unequip request.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthSurvivorCharacter : XUiController
{
    public const string WindowGroupId = "rebirthSurvivorCharacter";
    public static XUiC_RebirthSurvivorCharacter ActiveInstance { get; private set; }
    public bool IsCharacterWindowOpen { get; private set; }
    private RebirthCharacterLayoutService characterLayout;
    private XUiC_RebirthCharacterOverviewList overviewTraitsList, overviewConditionsList;
    private XUiV_Label overviewBonusName, overviewBonusDescription, overviewTraitsCount, overviewConditionsCount;
    private XUiV_Sprite overviewBonusIcon;
    private XUiController overviewModelUnavailable;
    private bool overviewRowsResolved;
    private XUiC_RebirthCharacterOverviewList conditionsPageList, historyPageList, skillsPageList, knowledgePageList;
    private readonly RebirthWindowHudScope windowHud = new RebirthWindowHudScope();
    private readonly XUiController[] characterHud = new XUiController[3];
    private readonly bool[] characterHudVisible = new bool[3];
    private readonly bool[] characterHudEnabled = new bool[3];
    private bool characterHudCaptured;
    // The authored Character tree is fixed for this controller's lifetime. Periodic
    // projections must not recursively search thousands of descendants for each label.
    private readonly Dictionary<string, XUiController> projectionViews = new Dictionary<string, XUiController>(StringComparer.Ordinal);
    private long lastOverviewTraceRevision = long.MinValue;
    private static readonly string[] SidebarPages = { "Overview", "Progression", "Condition", "Statistics", "Metabolism" };
    private readonly Dictionary<string, XUiV_Sprite> sidebarBackgrounds = new Dictionary<string, XUiV_Sprite>();
    private const int TraitRows = 12;
    private const int SkillRows = 48;
    private const int KnowledgeRows = 27;
    private const int OverviewTraitRows = 64;
    private const int OverviewAttributeRows = 8;
    private const int OverviewConditionRows = 32;
    private const int OverviewVitalRows = 5;
    private const int ProgressionAttributeRows = 8;
    private const int ProgressionSkillRows = 48;
    private const int ProgressionKnowledgeRows = 24;
    private const int ConditionRows = 32;
    private const int ConditionHistoryRows = 10;
    private const int ConditionHistoryCapacity = 32;

    private enum Tab { Overview, Origin, Progression, Attributes, Skills, Knowledge, Traits, Condition, Statistics, Metabolism }

    private readonly XUiController[] panels = new XUiController[10];
    private readonly XUiController[] overviewTraitRows = new XUiController[OverviewTraitRows];
    private readonly XUiV_Sprite[] overviewTraitIcons = new XUiV_Sprite[OverviewTraitRows];
    private readonly XUiV_Label[] overviewTraitNames = new XUiV_Label[OverviewTraitRows];
    private readonly XUiController[] overviewAttributeRows = new XUiController[OverviewAttributeRows];
    private readonly XUiV_Sprite[] overviewAttributeIcons = new XUiV_Sprite[OverviewAttributeRows];
    private readonly XUiV_Label[] overviewAttributeNames = new XUiV_Label[OverviewAttributeRows];
    private readonly XUiV_Sprite[] overviewAttributeFills = new XUiV_Sprite[OverviewAttributeRows];
    private readonly XUiV_Label[] overviewAttributeValues = new XUiV_Label[OverviewAttributeRows];
    private readonly XUiController[] overviewConditionRows = new XUiController[OverviewConditionRows];
    private readonly XUiV_Sprite[] overviewConditionAccents = new XUiV_Sprite[OverviewConditionRows];
    private readonly XUiV_Sprite[] overviewConditionIcons = new XUiV_Sprite[OverviewConditionRows];
    private readonly XUiV_Label[] overviewConditionNames = new XUiV_Label[OverviewConditionRows];
    private readonly XUiV_Label[] overviewConditionDetails = new XUiV_Label[OverviewConditionRows];
    private readonly XUiV_Sprite[] overviewVitalFills = new XUiV_Sprite[OverviewVitalRows];
    private readonly XUiV_Label[] overviewVitalValues = new XUiV_Label[OverviewVitalRows];
    private readonly XUiController[] progressionAttributeRows = new XUiController[ProgressionAttributeRows];
    private readonly XUiV_Sprite[] progressionAttributeIcons = new XUiV_Sprite[ProgressionAttributeRows];
    private readonly XUiV_Label[] progressionAttributeNames = new XUiV_Label[ProgressionAttributeRows];
    private readonly XUiV_Label[] progressionAttributeValues = new XUiV_Label[ProgressionAttributeRows];
    private readonly XUiV_Sprite[] progressionAttributeCurrent = new XUiV_Sprite[ProgressionAttributeRows];
    private readonly XUiV_Sprite[] progressionAttributePotential = new XUiV_Sprite[ProgressionAttributeRows];
    private readonly XUiController[] progressionSkillRows = new XUiController[ProgressionSkillRows];
    private readonly XUiV_Sprite[] progressionSkillSelections = new XUiV_Sprite[ProgressionSkillRows];
    private readonly XUiV_Sprite[] progressionSkillIcons = new XUiV_Sprite[ProgressionSkillRows];
    private readonly XUiV_Label[] progressionSkillNames = new XUiV_Label[ProgressionSkillRows];
    private readonly XUiV_Label[] progressionSkillPractical = new XUiV_Label[ProgressionSkillRows];
    private readonly XUiV_Label[] progressionSkillKnowledge = new XUiV_Label[ProgressionSkillRows];
    private readonly XUiV_Sprite[] progressionSkillPracticalFills = new XUiV_Sprite[ProgressionSkillRows];
    private readonly XUiV_Sprite[] progressionSkillKnowledgeFills = new XUiV_Sprite[ProgressionSkillRows];
    private readonly XUiController[] progressionSkillButtons = new XUiController[ProgressionSkillRows];
    private readonly Dictionary<XUiController, int> progressionSkillButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] progressionKnowledgeRows = new XUiController[ProgressionKnowledgeRows];
    private readonly XUiV_Sprite[] progressionKnowledgeIcons = new XUiV_Sprite[ProgressionKnowledgeRows];
    private readonly XUiV_Label[] progressionKnowledgeNames = new XUiV_Label[ProgressionKnowledgeRows];
    private readonly XUiV_Label[] progressionKnowledgeStatus = new XUiV_Label[ProgressionKnowledgeRows];
    private readonly XUiController[] progressionKnowledgeButtons = new XUiController[ProgressionKnowledgeRows];
    private readonly string[] progressionKnowledgeIds = new string[ProgressionKnowledgeRows];
    private readonly Dictionary<XUiController, int> progressionKnowledgeButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] traitRows = new XUiController[TraitRows];
    private readonly XUiV_Sprite[] traitIcons = new XUiV_Sprite[TraitRows];
    private readonly XUiV_Label[] traitNames = new XUiV_Label[TraitRows];
    private readonly XUiV_Label[] traitMeta = new XUiV_Label[TraitRows];
    private readonly XUiController[] traitButtons = new XUiController[TraitRows];
    private readonly Dictionary<XUiController, int> traitButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] skillRows = new XUiController[SkillRows];
    private readonly XUiV_Label[] skillNames = new XUiV_Label[SkillRows];
    private readonly XUiV_Label[] skillValues = new XUiV_Label[SkillRows];
    private readonly XUiController[] skillButtons = new XUiController[SkillRows];
    private readonly string[] skillExploreIds = new string[SkillRows];
    private readonly Dictionary<XUiController, int> skillButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] knowledgeRows = new XUiController[KnowledgeRows];
    private readonly XUiV_Label[] knowledgeNames = new XUiV_Label[KnowledgeRows];
    private readonly XUiController[] knowledgeButtons = new XUiController[KnowledgeRows];
    private readonly string[] knowledgeExploreIds = new string[KnowledgeRows];
    private readonly Dictionary<XUiController, int> knowledgeButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] conditionRows = new XUiController[ConditionRows];
    private readonly XUiV_Sprite[] conditionSelections = new XUiV_Sprite[ConditionRows];
    private readonly XUiV_Sprite[] conditionAccents = new XUiV_Sprite[ConditionRows];
    private readonly XUiV_Sprite[] conditionIcons = new XUiV_Sprite[ConditionRows];
    private readonly XUiV_Label[] conditionNames = new XUiV_Label[ConditionRows];
    private readonly XUiV_Label[] conditionCategories = new XUiV_Label[ConditionRows];
    private readonly XUiV_Label[] conditionDetails = new XUiV_Label[ConditionRows];
    private readonly XUiController[] conditionButtons = new XUiController[ConditionRows];
    private readonly Dictionary<XUiController, int> conditionButtonIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] conditionHistoryRows = new XUiController[ConditionHistoryRows];
    private readonly XUiV_Label[] conditionHistoryTimes = new XUiV_Label[ConditionHistoryRows];
    private readonly XUiV_Label[] conditionHistoryTexts = new XUiV_Label[ConditionHistoryRows];
    private readonly List<ConditionHistoryEntry> conditionHistory = new List<ConditionHistoryEntry>();
    private readonly Dictionary<string, string> previousConditionNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private RebirthSurvivorOwnerStateSnapshot snapshot;
    private RebirthCharacterUiSnapshot overviewSnapshot;
    private RebirthMetabolismSnapshot metabolism;
    private bool hasMetabolism;
    private long lastRevision = long.MinValue;
    private RebirthSurvivorOwnerHeader lastOwnerHeader;
    private long lastTextRevision = long.MinValue;
    private long lastTrainingProjectionRevision = long.MinValue;
    private readonly RebirthUiSortedIdProjection skillOrder = new RebirthUiSortedIdProjection();
    private readonly RebirthUiSortedIdProjection knowledgeOrder = new RebirthUiSortedIdProjection();
    private int lastMetabolismRevision = int.MinValue;
    private float refresh;
    private Tab tab;
    private int traitOffset;
    private const int TraitTrackHeight = 552;
    private XUiController traitNativeScrollHost;
    private XUiController traitNativeScrollView;
    private XUiController traitNativeScrollProxy;
    private bool syncingTraitNativeScroll;
    private int focusedTraitIndex = -1;
    private bool suspendedForProgressionExplorer;
    private bool resumeFromProgressionExplorerPending;
    private string progressionExplorerReturnToken = string.Empty;
    private XUiController progressionExplorerReturnFocus;
    private Tab progressionExplorerReturnTab;
    private XUiController skillsContent;
    private XUiController knowledgeContent;
    private Vector2i progressionExplorerSkillsContentPosition;
    private Vector2i progressionExplorerKnowledgeContentPosition;

    private XUiController legacyMainContent;
    private XUiController legacyGearPanel;
    private XUiController overviewArt;
    private XUiController overviewArtPlaceholder;
    private RebirthSurvivorArtTextureBinder overviewArtBinder;
    private RebirthCharacterModelBinder overviewModelBinder;
    private XUiV_Label overviewPlayerName;
    private XUiV_Label overviewLevelProfile;
    private XUiV_Label overviewBackgroundName;
    private XUiV_Label overviewBackgroundBody;
    private XUiV_Sprite overviewBackpackIcon;
    private XUiV_Sprite overviewWalkmanIcon;
    private XUiV_Sprite overviewBeltIcon;
    private XUiV_Sprite overviewSupportIcon;
    private XUiV_Label overviewBackpackName;
    private XUiV_Label overviewBeltName;
    private XUiV_Label overviewSupportName;
    private string renderedSupportWaterItem = string.Empty;

    private string selectedProgressionSkillId = string.Empty;
    private XUiController progressionSkillsContent;
    private XUiController progressionKnowledgeContent;
    private Vector2i progressionExplorerProgressionSkillsContentPosition;
    private Vector2i progressionExplorerProgressionKnowledgeContentPosition;
    private XUiV_Sprite progressionSelectedIcon;
    private XUiV_Label progressionSelectedName;
    private XUiV_Label progressionSelectedAttribute;
    private XUiV_Label progressionSelectedPractical;
    private XUiV_Label progressionSelectedKnowledge;
    private XUiV_Sprite progressionSelectedPracticalFill;
    private XUiV_Sprite progressionSelectedKnowledgeFill;
    private XUiV_Label progressionSelectedTraining;
    private XUiV_Label progressionSelectedBounds;
    private XUiV_Label progressionSoloStatus;
    private XUiV_Label progressionKnowledgeCount;
    private XUiV_Label progressionKnowledgeEmpty;

    private string selectedConditionId = string.Empty;
    private bool conditionHistoryInitialized;
    private XUiV_Sprite conditionSelectedAccent;
    private XUiV_Sprite conditionSelectedIcon;
    private XUiV_Label conditionSelectedName;
    private XUiV_Label conditionSelectedCategory;
    private XUiV_Label conditionSelectedDuration;
    private XUiV_Label conditionSelectedDescription;
    private XUiV_Label conditionSelectedStatus;
    private XUiV_Label conditionSelectedEffects;
    private XUiV_Label conditionSelectedGuidance;
    private XUiV_Label conditionSelectedSource;
    private XUiController conditionEffectsPanel;
    private XUiController conditionGuidancePanel;
    private XUiV_Label conditionTotalCount;
    private XUiV_Label conditionListSummary;
    private XUiV_Label conditionPositiveCount;
    private XUiV_Label conditionNegativeCount;
    private XUiV_Label conditionNeutralCount;
    private XUiV_Label conditionNativeCount;
    private XUiV_Label conditionMoodSummary;
    private XUiV_Label conditionDietSummary;
    private XUiV_Label conditionHealthSummary;
    private XUiV_Label conditionMetabolismSummary;
    private XUiV_Label conditionHistoryEmpty;

    private XUiController originArt;
    private XUiController originArtPlaceholder;
    private RebirthSurvivorArtTextureBinder originArtBinder;
    private XUiV_Sprite dietIcon;

    private XUiV_Label originTitle;
    private XUiV_Label originBody;
    private XUiV_Label attributesText;
    private XUiV_Label skillsText;
    private XUiV_Label knowledgeText;
    private XUiV_Label traitPageText;
    private XUiV_Label traitDetails;
    private XUiV_Label metabolismText;
    private XUiV_Label gearCapacity;
    private XUiV_Label gearHint;
    private XUiV_Label backpackName;
    private XUiV_Label beltName;
    private XUiV_Label supportName;
    private XUiV_Sprite backpackIcon;
    private XUiV_Sprite beltIcon;
    private XUiV_Sprite supportIcon;
    private XUiController btnBackpackUnequip;
    private XUiController btnBeltUnequip;
    private XUiController btnSupportUnequip;
    private XUiC_RebirthHudTrackingManager hudTrackingManager;


    public override void Init()
    {
        base.Init();
        panels[(int)Tab.Overview] = GetChildById("survivorOverviewPanel");
        panels[(int)Tab.Origin] = GetChildById("survivorOriginPanel");
        panels[(int)Tab.Progression] = GetChildById("survivorProgressionPanel");
        panels[(int)Tab.Attributes] = GetChildById("survivorAttributesPanel");
        panels[(int)Tab.Skills] = GetChildById("survivorSkillsPanel");
        panels[(int)Tab.Knowledge] = GetChildById("survivorKnowledgePanel");
        panels[(int)Tab.Traits] = GetChildById("survivorTraitsPanel");
        panels[(int)Tab.Condition] = GetChildById("survivorConditionPanel");
        panels[(int)Tab.Statistics] = GetChildById("survivorStatisticsPanel");
        panels[(int)Tab.Metabolism] = GetChildById("survivorMetabolismPanel");

        Wire("btnSurvivorTabOverview", delegate { SelectTab(Tab.Overview); });
        Wire("btnSurvivorTabOrigin", delegate { SelectTab(Tab.Origin); });
        Wire("btnSurvivorTabProgression", delegate { SelectTab(Tab.Progression); });
        Wire("btnSurvivorTabAttributes", delegate { SelectTab(Tab.Attributes); });
        Wire("btnSurvivorTabSkills", delegate { SelectTab(Tab.Skills); });
        Wire("btnSurvivorTabKnowledge", delegate { SelectTab(Tab.Knowledge); });
        Wire("btnSurvivorTabTraits", delegate { SelectTab(Tab.Traits); });
        Wire("btnSurvivorTabCondition", delegate { SelectTab(Tab.Condition); });
        Wire("btnSurvivorTabStatistics", delegate { SelectTab(Tab.Statistics); });
        Wire("btnSurvivorTabMetabolism", delegate { SelectTab(Tab.Metabolism); });
        Wire("btnSurvivorClose", Close_OnPressed);

        btnBackpackUnequip = Wire("btnSurvivorGearBackpackUnequip", delegate { RequestUnequip(RebirthSurvivorGearService.BackpackSlotId); });
        btnBeltUnequip = Wire("btnSurvivorGearBeltUnequip", delegate { RequestUnequip(RebirthSurvivorGearService.BeltSlotId); });
        btnSupportUnequip = Wire("btnSurvivorGearSupportUnequip", delegate { RequestUnequip(RebirthSurvivorGearService.SupportSlotId); });
        hudTrackingManager = GetChildByType<XUiC_RebirthHudTrackingManager>();

        legacyMainContent = GetChildById("survivorMainContent");
        legacyGearPanel = GetChildById("survivorGearPanel");
        overviewArt = GetChildById("survivorOverviewBackgroundArt");
        overviewArtPlaceholder = GetChildById("survivorOverviewBackgroundPlaceholder");
        overviewArtBinder = new RebirthSurvivorArtTextureBinder(overviewArt, 1f);
        overviewModelBinder = new RebirthCharacterModelBinder(GetChildById("survivorOverviewModel"));
        overviewPlayerName = Label("survivorOverviewPlayerName");
        overviewLevelProfile = Label("survivorOverviewLevelProfile");
        overviewBackgroundName = Label("survivorOverviewBackgroundName");
        overviewBackgroundBody = Label("survivorOverviewBackgroundBody");
        Wire("btnPc134UnequipBackpack", delegate { RequestUnequip(RebirthSurvivorGearService.BackpackSlotId); });
        Wire("btnPc134UnequipBelt", delegate { RequestUnequip(RebirthSurvivorGearService.BeltSlotId); });
        Wire("btnPc134UnequipSupport", delegate { if(!string.IsNullOrEmpty(RebirthOverviewWaterRequest.EquippedItem(xui.playerUI.entityPlayer)))RebirthOverviewWaterRequest.Dispatch(xui.playerUI.entityPlayer,null,true);else RequestUnequip(RebirthSurvivorGearService.SupportSlotId); });
        overviewBackpackIcon = Sprite("survivorOverviewGearBackpackIcon");
        overviewWalkmanIcon = Sprite("survivorOverviewGearWalkmanIcon");
        Wire("btnWalkmanLibrary", delegate { if(RebirthSurvivorGearService.HasEquippedWalkman(xui.playerUI.entityPlayer)) xui.playerUI.windowManager.Open("rebirthMusicLibrary",true); });
        overviewBeltIcon = Sprite("survivorOverviewGearBeltIcon");
        overviewSupportIcon = Sprite("survivorOverviewGearSupportIcon");
        Wire("btnBackpackLibrary", delegate { xui.playerUI.windowManager.Open("rebirthBackpackLibrary",true); });
        overviewBackpackName = Label("survivorOverviewGearBackpackName");
        overviewBeltName = Label("survivorOverviewGearBeltName");
        overviewSupportName = Label("survivorOverviewGearSupportName");

        progressionSkillsContent = GetChildById("survivorProgressionSkillsContent");
        progressionKnowledgeContent = GetChildById("survivorProgressionKnowledgeContent");
        progressionSelectedIcon = Sprite("survivorProgressionSelectedIcon");
        progressionSelectedName = Label("survivorProgressionSelectedName");
        progressionSelectedAttribute = Label("survivorProgressionSelectedAttribute");
        progressionSelectedPractical = Label("survivorProgressionSelectedPractical");
        progressionSelectedKnowledge = Label("survivorProgressionSelectedKnowledge");
        progressionSelectedPracticalFill = Sprite("survivorProgressionSelectedPracticalFill");
        progressionSelectedKnowledgeFill = Sprite("survivorProgressionSelectedKnowledgeFill");
        progressionSelectedTraining = Label("survivorProgressionSelectedTraining");
        progressionSelectedBounds = Label("survivorProgressionSelectedBounds");
        progressionSoloStatus=Label("survivorProgressionSoloStatus");
        progressionKnowledgeCount = Label("survivorProgressionKnowledgeCount");
        progressionKnowledgeEmpty = Label("survivorProgressionKnowledgeEmpty");
        Wire("btnSurvivorProgressionExploreSelected", ProgressionExploreSelected_OnPressed);
        Wire("btnSurvivorProgressionReflect", ProgressionReflect_OnPressed);
        Wire("btnSurvivorProgressionCancelStudy",ProgressionCancelStudy_OnPressed);
        for (int i = 0; i < ProgressionAttributeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            progressionAttributeRows[i] = GetChildById("survivorProgressionAttributeRow" + suffix);
            progressionAttributeIcons[i] = Sprite("survivorProgressionAttributeIcon" + suffix);
            progressionAttributeNames[i] = Label("survivorProgressionAttributeName" + suffix);
            progressionAttributeValues[i] = Label("survivorProgressionAttributeValue" + suffix);
            progressionAttributeCurrent[i] = Sprite("survivorProgressionAttributeCurrent" + suffix);
            progressionAttributePotential[i] = Sprite("survivorProgressionAttributePotential" + suffix);
        }
        for (int i = 0; i < ProgressionSkillRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            progressionSkillRows[i] = GetChildById("survivorProgressionSkillRow" + suffix);
            progressionSkillSelections[i] = Sprite("survivorProgressionSkillSelection" + suffix);
            progressionSkillIcons[i] = Sprite("survivorProgressionSkillIcon" + suffix);
            progressionSkillNames[i] = Label("survivorProgressionSkillName" + suffix);
            progressionSkillPractical[i] = Label("survivorProgressionSkillPractical" + suffix);
            progressionSkillKnowledge[i] = Label("survivorProgressionSkillKnowledge" + suffix);
            progressionSkillPracticalFills[i] = Sprite("survivorProgressionSkillPracticalFill" + suffix);
            progressionSkillKnowledgeFills[i] = Sprite("survivorProgressionSkillKnowledgeFill" + suffix);
            progressionSkillButtons[i] = GetChildById("btnSurvivorProgressionSkill" + suffix);
            if (progressionSkillButtons[i] != null)
            {
                progressionSkillButtonIndex[progressionSkillButtons[i]] = i;
                progressionSkillButtons[i].OnPress += ProgressionSkill_OnPressed;
            }
        }
        for (int i = 0; i < ProgressionKnowledgeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            progressionKnowledgeRows[i] = GetChildById("survivorProgressionKnowledgeRow" + suffix);
            progressionKnowledgeIcons[i] = Sprite("survivorProgressionKnowledgeIcon" + suffix);
            progressionKnowledgeNames[i] = Label("survivorProgressionKnowledgeName" + suffix);
            progressionKnowledgeStatus[i] = Label("survivorProgressionKnowledgeStatus" + suffix);
            progressionKnowledgeButtons[i] = GetChildById("btnSurvivorProgressionKnowledge" + suffix);
            if (progressionKnowledgeButtons[i] != null)
            {
                progressionKnowledgeButtonIndex[progressionKnowledgeButtons[i]] = i;
                progressionKnowledgeButtons[i].OnPress += ProgressionKnowledge_OnPressed;
            }
        }

        originArt = GetChildById("survivorOriginArt");
        originArtPlaceholder = GetChildById("survivorOriginArtPlaceholder");
        originArtBinder = new RebirthSurvivorArtTextureBinder(originArt, 16f / 9f);
        dietIcon = Sprite("survivorOriginDietIcon");

        originTitle = Label("survivorOriginTitle");
        originBody = Label("survivorOriginBody");
        attributesText = Label("survivorAttributesText");
        skillsText = Label("survivorSkillsText");
        knowledgeText = Label("survivorKnowledgeText");
        skillsContent = GetChildById("survivorSkillsContent");
        knowledgeContent = GetChildById("survivorKnowledgeContent");
        traitPageText = Label("survivorTraitPageText");
        traitDetails = Label("survivorTraitDetails");
        traitNativeScrollHost = GetChildById("survivorTraitNativeScrollHost");
        traitNativeScrollView = GetChildById("survivorTraitNativeScrollView");
        traitNativeScrollProxy = GetChildById("survivorTraitNativeScrollProxy");
        WireTraitScroll(traitNativeScrollHost);
        WireTraitScroll(traitNativeScrollView);
        WireTraitScroll(traitNativeScrollProxy);
        metabolismText = Label("survivorMetabolismText");
        gearCapacity = Label("survivorGearCapacity");
        gearHint = Label("survivorGearHint");
        backpackName = Label("survivorGearBackpackName");
        beltName = Label("survivorGearBeltName");
        supportName = Label("survivorGearSupportName");
        backpackIcon = Sprite("survivorGearBackpackIcon");
        beltIcon = Sprite("survivorGearBeltIcon");
        supportIcon = Sprite("survivorGearSupportIcon");

        conditionSelectedAccent = Sprite("survivorConditionSelectedAccent");
        conditionSelectedIcon = Sprite("survivorConditionSelectedIcon");
        conditionSelectedName = Label("survivorConditionSelectedName");
        conditionSelectedCategory = Label("survivorConditionSelectedCategory");
        conditionSelectedDuration = Label("survivorConditionSelectedDuration");
        conditionSelectedDescription = Label("survivorConditionSelectedDescription");
        conditionSelectedStatus = Label("survivorConditionSelectedStatus");
        conditionSelectedEffects = Label("survivorConditionSelectedEffects");
        conditionSelectedGuidance = Label("survivorConditionSelectedGuidance");
        conditionSelectedSource = Label("survivorConditionSelectedSource");
        conditionEffectsPanel = GetChildById("survivorConditionEffectsPanel");
        conditionGuidancePanel = GetChildById("survivorConditionGuidancePanel");
        conditionTotalCount = Label("survivorConditionTotalCount");
        conditionListSummary = Label("survivorConditionListSummary");
        conditionPositiveCount = Label("survivorConditionPositiveCount");
        conditionNegativeCount = Label("survivorConditionNegativeCount");
        conditionNeutralCount = Label("survivorConditionNeutralCount");
        conditionNativeCount = Label("survivorConditionNativeCount");
        conditionMoodSummary = Label("survivorConditionMoodSummary");
        conditionDietSummary = Label("survivorConditionDietSummary");
        conditionHealthSummary = Label("survivorConditionHealthSummary");
        conditionMetabolismSummary = Label("survivorConditionMetabolismSummary");
        conditionHistoryEmpty = Label("survivorConditionHistoryEmpty");
        for (int i = 0; i < ConditionRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            conditionRows[i] = GetChildById("survivorConditionRow" + suffix);
            conditionSelections[i] = Sprite("survivorConditionSelection" + suffix);
            conditionAccents[i] = Sprite("survivorConditionAccent" + suffix);
            conditionIcons[i] = Sprite("survivorConditionIcon" + suffix);
            conditionNames[i] = Label("survivorConditionName" + suffix);
            conditionCategories[i] = Label("survivorConditionCategory" + suffix);
            conditionDetails[i] = Label("survivorConditionDetail" + suffix);
            conditionButtons[i] = GetChildById("btnSurvivorCondition" + suffix);
            if (conditionButtons[i] != null)
            {
                conditionButtonIndex[conditionButtons[i]] = i;
                conditionButtons[i].OnPress += Condition_OnPressed;
            }
        }
        for (int i = 0; i < ConditionHistoryRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            conditionHistoryRows[i] = GetChildById("survivorConditionHistoryRow" + suffix);
            conditionHistoryTimes[i] = Label("survivorConditionHistoryTime" + suffix);
            conditionHistoryTexts[i] = Label("survivorConditionHistoryText" + suffix);
        }

        for (int i = 0; i < OverviewTraitRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            overviewTraitRows[i] = GetChildById("survivorOverviewTraitRow" + suffix);
            overviewTraitIcons[i] = Sprite("survivorOverviewTraitIcon" + suffix);
            overviewTraitNames[i] = Label("survivorOverviewTraitName" + suffix);
        }
        for (int i = 0; i < OverviewAttributeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            overviewAttributeRows[i] = GetChildById("survivorOverviewAttributeRow" + suffix);
            overviewAttributeIcons[i] = Sprite("survivorOverviewAttributeIcon" + suffix);
            overviewAttributeNames[i] = Label("survivorOverviewAttributeName" + suffix);
            overviewAttributeFills[i] = Sprite("survivorOverviewAttributeFill" + suffix);
            overviewAttributeValues[i] = Label("survivorOverviewAttributeValue" + suffix);
        }
        for (int i = 0; i < OverviewConditionRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            overviewConditionRows[i] = GetChildById("survivorOverviewConditionRow" + suffix);
            overviewConditionAccents[i] = Sprite("survivorOverviewConditionAccent" + suffix);
            overviewConditionIcons[i] = Sprite("survivorOverviewConditionIcon" + suffix);
            overviewConditionNames[i] = Label("survivorOverviewConditionName" + suffix);
            overviewConditionDetails[i] = Label("survivorOverviewConditionDetail" + suffix);
        }
        for (int i = 0; i < OverviewVitalRows; i++)
        {
            overviewVitalFills[i] = Sprite("survivorOverviewVitalFill" + i.ToString(CultureInfo.InvariantCulture));
            overviewVitalValues[i] = Label("survivorOverviewVitalValue" + i.ToString(CultureInfo.InvariantCulture));
        }

        for (int i = 0; i < TraitRows; i++)
        {
            traitRows[i] = GetChildById("survivorTraitRow" + i.ToString(CultureInfo.InvariantCulture));
            traitIcons[i] = Sprite("survivorTraitIcon" + i.ToString(CultureInfo.InvariantCulture));
            traitNames[i] = Label("survivorTraitName" + i.ToString(CultureInfo.InvariantCulture));
            traitMeta[i] = Label("survivorTraitMeta" + i.ToString(CultureInfo.InvariantCulture));
            traitButtons[i] = GetChildById("btnSurvivorTrait" + i.ToString(CultureInfo.InvariantCulture));
            if (traitButtons[i] != null)
            {
                traitButtonIndex[traitButtons[i]] = i;
                traitButtons[i].OnPress += TraitRow_OnPressed;
                WireTraitScroll(traitButtons[i]);
            }
        }
        for (int i = 0; i < SkillRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            skillRows[i] = GetChildById("survivorSkillRow" + suffix);
            skillNames[i] = Label("survivorSkillName" + suffix);
            skillValues[i] = Label("survivorSkillValue" + suffix);
            skillButtons[i] = GetChildById("btnSurvivorSkillExplore" + suffix);
            if (skillButtons[i] != null)
            {
                skillButtonIndex[skillButtons[i]] = i;
                skillButtons[i].OnPress += SkillExplore_OnPressed;
            }
        }
        for (int i = 0; i < KnowledgeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            knowledgeRows[i] = GetChildById("survivorKnowledgeRow" + suffix);
            knowledgeNames[i] = Label("survivorKnowledgeName" + suffix);
            knowledgeButtons[i] = GetChildById("btnSurvivorKnowledgeExplore" + suffix);
            if (knowledgeButtons[i] != null)
            {
                knowledgeButtonIndex[knowledgeButtons[i]] = i;
                knowledgeButtons[i].OnPress += KnowledgeExplore_OnPressed;
            }
        }
        RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();
        characterLayout = new RebirthCharacterLayoutService(this);
        overviewTraitsList = GetChildById("survivorOverviewTraitsList") as XUiC_RebirthCharacterOverviewList;
        conditionsPageList = GetChildById("pc134ConditionsList") as XUiC_RebirthCharacterOverviewList;
        historyPageList = GetChildById("pc134HistoryList") as XUiC_RebirthCharacterOverviewList;
        skillsPageList = GetChildById("pc134SkillsList") as XUiC_RebirthCharacterOverviewList;
        knowledgePageList = GetChildById("pc134KnowledgeList") as XUiC_RebirthCharacterOverviewList;
        if (conditionsPageList != null) conditionsPageList.DataRangeChanged += RenderConditionList;
        if (historyPageList != null) historyPageList.DataRangeChanged += RenderConditionHistory;
        if (skillsPageList != null) skillsPageList.DataRangeChanged += RenderProgression;
        if (knowledgePageList != null) knowledgePageList.DataRangeChanged += RenderProgression;
        overviewConditionsList = GetChildById("survivorOverviewConditionsList") as XUiC_RebirthCharacterOverviewList;
        overviewBonusName = Label("survivorOverviewBonusName");
        overviewBonusDescription = Label("survivorOverviewBonusDescription");
        overviewBonusIcon = Sprite("survivorOverviewBonusIcon");
        overviewTraitsCount = Label("survivorOverviewTraitsCount");
        overviewConditionsCount = Label("survivorOverviewConditionsCount");
        overviewModelUnavailable = GetChildById("survivorOverviewModelUnavailable");
        overviewRowsResolved = overviewTraitsList != null;
        for (int i = 0; i < OverviewTraitRows; i++) overviewRowsResolved &= overviewTraitRows[i] != null && overviewTraitNames[i] != null;
        if (overviewTraitsList != null) overviewTraitsList.DataRangeChanged += RenderOverviewTraits;
        if (overviewConditionsList != null) overviewConditionsList.DataRangeChanged += RenderOverviewConditions;
        for (int i = 0; i < SidebarPages.Length; i++)
        {
            string page = SidebarPages[i];
            sidebarBackgrounds[page] = Sprite("btnSurvivorTab" + page + "Bg");
            XUiV_Label pageLabel = Label("btnSurvivorTab" + page + "Label");
            if (pageLabel != null) Set(pageLabel, (pageLabel.Text ?? string.Empty).ToUpperInvariant());
            XUiController button = GetChildById("btnSurvivorTab" + page);
            if (button != null) button.OnHover += delegate(XUiController sender, bool over) { StyleSidebar(page, over); };
        }
        characterLayout.Apply(true);
        SelectTab(Tab.Overview);
    }

    public override void OnOpen()
    {
        IsCharacterWindowOpen = true;
        ActiveInstance = this;
        characterLayout?.Apply(true);
        windowHud.Maintain(xui);
        base.OnOpen();
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Character);
        if (resumeFromProgressionExplorerPending)
        {
            resumeFromProgressionExplorerPending = false;
            suspendedForProgressionExplorer = false;
            SelectTab(progressionExplorerReturnTab);
            if (skillsContent != null && skillsContent.ViewComponent != null) skillsContent.ViewComponent.Position = progressionExplorerSkillsContentPosition;
            if (knowledgeContent != null && knowledgeContent.ViewComponent != null) knowledgeContent.ViewComponent.Position = progressionExplorerKnowledgeContentPosition;
            skillsPageList?.RestoreScrollOffset(progressionExplorerProgressionSkillsContentPosition.y);
            knowledgePageList?.RestoreScrollOffset(progressionExplorerProgressionKnowledgeContentPosition.y);
            if (progressionExplorerReturnFocus != null && progressionExplorerReturnFocus.ViewComponent != null && xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
                xui.playerUI.CursorController.SetNavigationTargetLater(progressionExplorerReturnFocus.ViewComponent);
            progressionExplorerReturnFocus = null;
            progressionExplorerReturnToken = string.Empty;
            return;
        }
        traitOffset = 0;
        focusedTraitIndex = -1;
        selectedConditionId = string.Empty;
        conditionHistory.Clear();
        previousConditionNames.Clear();
        conditionHistoryInitialized = false;
        SelectTab(Tab.Overview);
    }

    public override void OnClose()
    {
        hudTrackingManager?.CloseManager();
        IsCharacterWindowOpen = false;
        if (xui?.DragAndDropWindow != null)
            xui.DragAndDropWindow.InMenu = xui.playerUI.windowManager.IsWindowOpen("windowpaging");
        if (ActiveInstance == this) ActiveInstance = null;
        windowHud.Restore();
        if (!suspendedForProgressionExplorer && originArtBinder != null) originArtBinder.Clear();
        if (!suspendedForProgressionExplorer && overviewArtBinder != null) overviewArtBinder.Clear();
        if (!suspendedForProgressionExplorer && overviewModelBinder != null) overviewModelBinder.Suspend();
        if (!suspendedForProgressionExplorer && !string.IsNullOrEmpty(progressionExplorerReturnToken))
        {
            RebirthProgressionExplorerReturnRegistry.Unregister(progressionExplorerReturnToken);
            progressionExplorerReturnToken = string.Empty;
        }
        RebirthTheorySoloStatusClient.Close();
        base.OnClose();
    }

    public override void Cleanup()
    {
        overviewModelBinder?.Clear();
        base.Cleanup();
    }

    public override void Update(float dt)
    {
        if (IsCharacterWindowOpen) windowHud.Maintain(xui);
        if (!IsCharacterWindowOpen) return;
        characterLayout?.Apply(false);
        base.Update(dt);
        if (tab == Tab.Origin && originArtBinder != null) originArtBinder.Update();
        if (tab == Tab.Overview && overviewArtBinder != null) overviewArtBinder.Update();
        refresh += Math.Max(0f, dt);
        if (refresh >= 0.25f)
        {
            refresh = 0f;
            RefreshSnapshots(false);
            if (tab == Tab.Progression){RefreshSelectedProgressionTraining();Set(progressionSoloStatus,RebirthTheorySoloStatusClient.View(xui.playerUI.entityPlayer,selectedProgressionSkillId));SetVisible(GetChildById("btnSurvivorProgressionReflect"),RebirthTheorySoloStatusClient.CanReflect(xui.playerUI.entityPlayer,selectedProgressionSkillId));SetVisible(GetChildById("btnSurvivorProgressionCancelStudy"),RebirthTheorySoloStatusClient.TryCancellation(xui.playerUI.entityPlayer,selectedProgressionSkillId,out _));}
        }
        long trainingProjectionRevision = RebirthSkillTrainingUiProjectionService.Revision;
        if (tab == Tab.Progression && trainingProjectionRevision != lastTrainingProjectionRevision)
        {
            lastTrainingProjectionRevision = trainingProjectionRevision;
            RenderProgression();
        }
        if (tab == Tab.Traits) PollTraitNativeScroll();
        // The dialog owns the navigation lock; route Escape through its view so
        // one release closes the dialog without also closing Character.
        XUiView cancelView = hudTrackingManager != null && hudTrackingManager.IsManagerOpen
            ? hudTrackingManager.GetChildById("survivorHudTrackingOverlay")?.ViewComponent : viewComponent;
        if (!RebirthConsoleInputGuardRuntime.BlocksGameplayInput() && cancelView != null && XUiUtils.HotkeysAllowedFor(cancelView) && xui != null && xui.playerUI != null
            && xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
        {
            if (hudTrackingManager != null && hudTrackingManager.IsManagerOpen) hudTrackingManager.CloseManager();
            else CloseWindow();
        }
    }

    private float nextMetabolismOnlyProgressionBuild;

    private void RefreshSnapshots(bool force)
    {
        RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
        long textRevision = RebirthUiProjectionTextCache.Revision;
        bool ownerChanged = !header.Equals(lastOwnerHeader);
        bool characterChanged = ownerChanged || textRevision != lastTextRevision;
        RebirthSurvivorOwnerStateSnapshot next = snapshot;
        if (force || ownerChanged) next = RebirthSurvivorClientState.GetOwnerStateSnapshot(out lastOwnerHeader);
        lastTextRevision = textRevision;
        long revision = next != null ? next.CharacterRevision : long.MinValue;

        RebirthMetabolismSnapshot met;
        bool foundMet = RebirthMetabolismClientState.TryGet(out met);
        if (!foundMet && xui != null && xui.playerUI != null && xui.playerUI.entityPlayer != null && RebirthMetabolismService.IsServerAuthority)
        {
            met = RebirthMetabolismService.BuildSnapshot(xui.playerUI.entityPlayer);
            foundMet = true;
        }
        bool metabolismChanged = foundMet != hasMetabolism || (foundMet && met.Revision != lastMetabolismRevision);
        if (!force && !characterChanged && tab == Tab.Overview && snapshot != null && snapshot.RebirthModeEnabled && snapshot.HasCharacter && overviewSnapshot != null)
        {
            if(foundMet){metabolism=met;lastMetabolismRevision=met.Revision;}
            hasMetabolism=foundMet;
            EntityPlayer livePlayer=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
            RebirthCharacterUiSnapshotBuilder.RefreshVitals(overviewSnapshot,livePlayer,hasMetabolism,metabolism);
            RenderOverviewVitalsOnly();
            // Water custody changes metabolism, not the separate gear record revision.
            // Keep the Support icon and withdrawal button current on this fast path.
            if (metabolismChanged && !string.Equals(renderedSupportWaterItem,
                RebirthOverviewWaterRequest.EquippedItem(livePlayer), StringComparison.Ordinal))
                RenderOverviewGearSlot(RebirthSurvivorGearService.SupportSlotId, overviewSupportName, overviewSupportIcon,
                    L("xuiRebirthSurvivorGearSupport", "Support"), "rb_slot_support");
            return;
        }
        if (!force && !characterChanged && !metabolismChanged)
        {
            if (tab == Tab.Condition && snapshot != null && snapshot.RebirthModeEnabled && snapshot.HasCharacter)
            {
                EntityPlayer livePlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
                overviewSnapshot = RebirthCharacterUiSnapshotBuilder.Build(livePlayer, snapshot, hasMetabolism, metabolism, false);
                RenderConditions();
            }
            return;
        }

        // Progression is rebuilt from scratch (dozens of strings). A metabolism-only revision bump happens every
        // few ticks and does not change what the page shows, so rebuild for it at most every 2 s.
        if (!force && !characterChanged && tab == Tab.Progression && snapshot != null && overviewSnapshot != null
            && Time.realtimeSinceStartup < nextMetabolismOnlyProgressionBuild)
        {
            if (foundMet) { metabolism = met; lastMetabolismRevision = met.Revision; }
            hasMetabolism = foundMet;
            return;
        }
        nextMetabolismOnlyProgressionBuild = Time.realtimeSinceStartup + 2f;

        snapshot = next;
        lastRevision = revision;
        if (foundMet)
        {
            metabolism = met;
            lastMetabolismRevision = met.Revision;
        }
        hasMetabolism = foundMet;

        if (snapshot == null || !snapshot.RebirthModeEnabled || !snapshot.HasCharacter)
        {
            overviewSnapshot = null;
            if (snapshot != null && !snapshot.RebirthModeEnabled) CloseWindow();
            else RenderOverview();
            return;
        }
        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        overviewSnapshot = RebirthCharacterUiSnapshotBuilder.Build(player, snapshot, hasMetabolism, metabolism, tab == Tab.Progression);
        RenderAll();
    }

    private void RenderAll()
    {
        // No hidden page rebuilds or hidden SDCS renders when a periodic owner snapshot arrives.
        if (!IsCharacterWindowOpen) return;
        switch (tab)
        {
            case Tab.Overview: RenderOverview(); break;
            case Tab.Progression: RenderProgression(); break;
            case Tab.Origin: RenderOrigin(); break;
            case Tab.Attributes: RenderAttributes(); break;
            case Tab.Skills: RenderSkills(); break;
            case Tab.Knowledge: RenderKnowledge(); break;
            case Tab.Traits: RenderTraits(); break;
            case Tab.Condition: RenderConditions(); break;
            case Tab.Metabolism: break; // The shared live metabolism controller owns these bindings.
        }
        // Gear is presented on Overview; detail pages do not duplicate it.
    }

    public void ShowSkillsPage() { SelectTab(Tab.Progression); }

    private void SelectTab(Tab next)
    {
        if (next == Tab.Origin || next == Tab.Traits) next = Tab.Overview;
        tab = next;
        bool fullWidth = tab == Tab.Overview || tab == Tab.Progression || tab == Tab.Condition || tab == Tab.Statistics || tab == Tab.Metabolism;
        SetVisible(legacyMainContent, !fullWidth);
        SetVisible(legacyGearPanel, !fullWidth);
        SetVisible(ProjectionView("survivorCharacterLegacyPages"), !fullWidth);
        for (int i = 0; i < panels.Length; i++) SetVisible(panels[i], i == (int)tab);
        foreach (string page in SidebarPages) StyleSidebar(page, false);
        if (IsCharacterWindowOpen) RefreshSnapshots(true);
        if (tab == Tab.Statistics)
            GetChildByType<XUiC_RebirthSurvivorStatisticsPanel>()?.RefreshVisiblePage();
    }

    private void StyleSidebar(string page, bool hovered)
    {
        XUiV_Sprite bg;
        if (!sidebarBackgrounds.TryGetValue(page, out bg) || bg == null) return;
        bool highlighted = hovered || string.Equals(tab.ToString(), page, StringComparison.Ordinal);
        string art = highlighted ? "ui_game_select_row" : "menu_empty2px";
        Color color = highlighted ? Color.white : (Color)new Color32(12, 12, 15, 255);
        if (bg.SpriteName != art) bg.SpriteName = art;
        if (bg.Color != color) bg.Color = color;
    }

    private void RenderOverview()
    {
        if (!IsCharacterWindowOpen || tab != Tab.Overview) return;
        if (overviewSnapshot == null)
        {
            Set(overviewPlayerName, L("xuiRebirthCharacterDataPending", "Waiting for character data..."));
            overviewTraitsList?.SetItemCount(0, L("xuiRebirthCharacterDataPending", "Waiting for character data..."));
            overviewConditionsList?.SetItemCount(0, L("xuiRebirthCharacterDataPending", "Waiting for character data..."));
            for (int i = 0; i < OverviewTraitRows; i++) SetVisible(overviewTraitRows[i], false);
            for (int i = 0; i < OverviewConditionRows; i++) SetVisible(overviewConditionRows[i], false);
            return;
        }
        Set(overviewPlayerName, string.IsNullOrEmpty(overviewSnapshot.PlayerName) ? L("xuiRebirthSurvivorCharacter", "Survivor") : overviewSnapshot.PlayerName);
        string profile = string.IsNullOrEmpty(overviewSnapshot.SourceProfileName)
            ? L("xuiRebirthSurvivorWorldNative", "Created directly for this world")
            : overviewSnapshot.SourceProfileName;
        Set(overviewLevelProfile, L("xuiLevel", "Level") + " " + overviewSnapshot.PlayerLevel.ToString(CultureInfo.InvariantCulture)
            + "\n[C6C6CE]" + profile + "[-]");

        RebirthBackgroundDefinition bg;
        bool hasBg = RebirthSurvivorDefinitionRegistry.TryGetBackground(overviewSnapshot.BackgroundId, out bg) && bg != null;
        bool hasArt = hasBg && overviewArtBinder != null && overviewArtBinder.BindBackgroundThumbnail(bg);
        SetVisible(overviewArt, hasArt);
        SetVisible(overviewArtPlaceholder, !hasArt);
        Set(overviewBackgroundName, overviewSnapshot.BackgroundName);
        StringBuilder bgText = new StringBuilder(256);
        if (!string.IsNullOrEmpty(overviewSnapshot.BackgroundIdentity)) bgText.Append(overviewSnapshot.BackgroundIdentity);
        if (!string.IsNullOrEmpty(overviewSnapshot.BackgroundStartingExperience))
        {
            if (bgText.Length > 0) bgText.Append("\n");
            bgText.Append("[C6C6CE]").Append(overviewSnapshot.BackgroundStartingExperience).Append("[-]");
        }
        Set(overviewBackgroundBody, bgText.ToString());
        string backgroundDetails = overviewSnapshot.BackgroundDescription + "\n\n" + overviewSnapshot.BackgroundStartingExperience;
        RebirthDietDefinition overviewDiet;
        if (snapshot != null && RebirthSurvivorDefinitionRegistry.TryGetDiet(snapshot.DietId, out overviewDiet) && overviewDiet != null)
            backgroundDetails += "\n\n" + RebirthSurvivorUiText.L(overviewDiet.NameKey, overviewDiet.Id) + "\n" + overviewDiet.RuleSummary;
        SetTooltip(overviewBackgroundName, backgroundDetails);
        SetTooltip(overviewBackgroundBody, backgroundDetails);

        Set(overviewBonusName, RebirthSurvivorUiText.SignatureBonusName(overviewSnapshot.BackgroundId));
        string bonusDescription = RebirthSurvivorUiText.SignatureBonusDescription(overviewSnapshot.BackgroundId);
        Set(overviewBonusDescription, bonusDescription);
        SetTooltip(overviewBonusDescription, bonusDescription);
        string bonusIcon = RebirthSurvivorUiText.SignatureBonusIcon(overviewSnapshot.BackgroundId);
        SetSprite(overviewBonusIcon, "RebirthSurvivorIcons", bonusIcon, Color.white);
        SetVisible(overviewBonusIcon?.Controller, !string.IsNullOrEmpty(bonusIcon));
        string missing = L("xuiRebirthCharacterListUnavailable", "List presentation unavailable. Check the installed Character files.");
        bool listsReady = overviewRowsResolved && overviewTraitsList.IsReady;
        overviewTraitsList?.SetItemCount(listsReady ? overviewSnapshot.Traits.Count : 0,
            listsReady ? L("xuiRebirthCharacterNoTraits", "No traits on this character.") : missing);
        overviewConditionsList?.SetItemCount(listsReady ? overviewSnapshot.Conditions.Count : 0,
            listsReady ? L("xuiRebirthCharacterNoConditions", "No current conditions.") : missing);
        // SetItemCount only raises range callbacks when the count/range changes. Content can change
        // at a stable count, so explicitly publish the current rows every snapshot refresh.
        RenderOverviewConditions();
        Set(overviewTraitsCount, listsReady ? overviewSnapshot.Traits.Count.ToString(CultureInfo.InvariantCulture) : "—");
        Set(overviewConditionsCount, listsReady ? overviewSnapshot.Conditions.Count.ToString(CultureInfo.InvariantCulture) : "—");
        if (listsReady) RenderOverviewTraits();

        EntityPlayer player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        bool modelReady = overviewModelBinder != null && overviewModelBinder.Bind(player, false);
        SetVisible(overviewModelUnavailable, !modelReady);

        for (int i = 0; i < OverviewAttributeRows; i++)
        {
            bool visible = i < overviewSnapshot.Attributes.Count && overviewSnapshot.Attributes[i] != null;
            SetVisible(overviewAttributeRows[i], visible);
            if (!visible) continue;
            RebirthCharacterUiAttribute attribute = overviewSnapshot.Attributes[i];
            SetSprite(overviewAttributeIcons[i], attribute.Atlas, attribute.Icon, Color.white);
            Set(overviewAttributeNames[i], attribute.Name.ToUpperInvariant());
            float max = Mathf.Max(1f, attribute.Potential);
            SetFill(overviewAttributeFills[i], attribute.Current / max);
            Set(overviewAttributeValues[i], attribute.Current.ToString("0.0", CultureInfo.InvariantCulture) + " / " + attribute.Potential.ToString("0.0", CultureInfo.InvariantCulture));
        }

        RenderOverviewVital(0, overviewSnapshot.Health, overviewSnapshot.HealthMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Health));
        RenderOverviewVital(1, overviewSnapshot.Stamina, overviewSnapshot.StaminaMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Stamina));
        Color32 energyColor = RebirthVitalHudColors.Get(RebirthVitalHudKind.Energy);
        RenderOverviewVital(2, overviewSnapshot.Energy, overviewSnapshot.EnergyMax, energyColor);
        RenderOverviewVital(3, overviewSnapshot.Nutrition, overviewSnapshot.NutritionMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Food));
        RenderOverviewVital(4, overviewSnapshot.Hydration, overviewSnapshot.HydrationMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Water));

        if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled && lastOverviewTraceRevision != overviewSnapshot.SurvivorRevision)
        {
            lastOverviewTraceRevision = overviewSnapshot.SurvivorRevision;
            Log.Out("[REBIRTH Character Overview] traits=" + overviewSnapshot.Traits.Count + " conditions=" + overviewSnapshot.Conditions.Count
                + " rowsResolved=" + listsReady + " modelReady=" + modelReady + " opacity=" + RebirthPersonalCraftingPanelOpacity.Percent);
        }

        RenderOverviewGearSlot(RebirthSurvivorGearService.BackpackSlotId, overviewBackpackName, overviewBackpackIcon, L("xuiRebirthSurvivorGearBackpack", "Backpack"), "rb_slot_backpack");
        RenderOverviewGearSlot(RebirthSurvivorGearService.WalkmanSlotId, null, overviewWalkmanIcon, L("xuiRebirthGearSlotWalkman", "Walkman"), "WalkmanMod_FR");
        RenderOverviewGearSlot(RebirthSurvivorGearService.BeltSlotId, overviewBeltName, overviewBeltIcon, L("xuiRebirthSurvivorGearBelt", "Belt"), "rb_slot_belt");
        RenderOverviewGearSlot(RebirthSurvivorGearService.SupportSlotId, overviewSupportName, overviewSupportIcon, L("xuiRebirthSurvivorGearSupport", "Support"), "rb_slot_support");
    }

    private void RenderOverviewTraits()
    {
        if (overviewSnapshot == null || overviewTraitsList == null) return;
        int first = overviewTraitsList.FirstDataIndex;
        for (int i = 0; i < OverviewTraitRows; i++)
        {
            int index = first + i;
            bool visible = index < overviewSnapshot.Traits.Count && overviewSnapshot.Traits[index] != null;
            if (visible)
            {
                RebirthCharacterUiTrait trait = overviewSnapshot.Traits[index];
                Color tint = trait.Polarity == RebirthTraitPolarity.Positive ? (Color)new Color32(143,209,143,255)
                    : trait.Polarity == RebirthTraitPolarity.Negative ? (Color)new Color32(204,107,100,255) : (Color)new Color32(214,201,120,255);
                SetSprite(overviewTraitIcons[i], "RebirthSurvivorIcons", trait.Icon, tint);
                Set(overviewTraitNames[i], trait.Name);
                if (overviewTraitNames[i] != null)
                {
                    if (overviewTraitNames[i].Color != tint) overviewTraitNames[i].Color = tint;
                    SetTooltip(overviewTraitNames[i], trait.Name + "\n\n" + trait.Description);
                    SetTooltip(overviewTraitIcons[i], trait.Name + "\n\n" + trait.Description);
                }
            }
            SetVisible(overviewTraitRows[i], visible);
        }
    }

    private void RenderOverviewConditions()
    {
        if (overviewSnapshot == null || overviewConditionsList == null) return;
        int first = overviewConditionsList.FirstDataIndex;
        for (int i = 0; i < OverviewConditionRows; i++)
        {
            int index = first + i;
            bool visible = index < overviewSnapshot.Conditions.Count && overviewSnapshot.Conditions[index] != null;
            if (visible)
            {
                RebirthCharacterUiCondition condition = overviewSnapshot.Conditions[index];
                bool iconVisible = !string.IsNullOrEmpty(condition.Icon);
                SetSprite(overviewConditionIcons[i], condition.Atlas, condition.Icon, condition.IconColor);
                SetVisible(overviewConditionIcons[i]?.Controller, iconVisible);
                SetSpriteColor(overviewConditionAccents[i], condition.Negative ? (Color)new Color32(196,42,42,255)
                    : condition.Positive ? (Color)new Color32(82,136,91,255) : (Color)new Color32(120,120,130,255));
                Set(overviewConditionNames[i], condition.Name);
                Set(overviewConditionDetails[i], condition.Detail);
                SetTooltip(overviewConditionNames[i], condition.Name + "\n" + condition.Description);
                SetTooltip(overviewConditionDetails[i], condition.Detail + "\n" + condition.Effects);
            }
            SetVisible(overviewConditionRows[i], visible);
        }
    }

    private void RenderProgression()
    {
        if (overviewSnapshot == null) return;

        for (int i = 0; i < ProgressionAttributeRows; i++)
        {
            bool visible = i < overviewSnapshot.Attributes.Count && overviewSnapshot.Attributes[i] != null;
            SetVisible(progressionAttributeRows[i], visible);
            if (!visible) continue;
            RebirthCharacterUiAttribute attribute = overviewSnapshot.Attributes[i];
            SetSprite(progressionAttributeIcons[i], attribute.Atlas, attribute.Icon, Color.white);
            Set(progressionAttributeNames[i], attribute.Name);
            Set(progressionAttributeValues[i], attribute.Current.ToString("0.0", CultureInfo.InvariantCulture) + " / " + attribute.Potential.ToString("0.0", CultureInfo.InvariantCulture));
            SetFill(progressionAttributeCurrent[i], attribute.Current / 100f);
            SetFill(progressionAttributePotential[i], attribute.Potential / 100f);
        }

        if (overviewSnapshot.Skills.Count == 0)
        {
            selectedProgressionSkillId = string.Empty;
        }
        else
        {
            bool selectedFound = false;
            for (int i = 0; i < overviewSnapshot.Skills.Count; i++)
                if (overviewSnapshot.Skills[i] != null && string.Equals(overviewSnapshot.Skills[i].Id, selectedProgressionSkillId, StringComparison.OrdinalIgnoreCase)) { selectedFound = true; break; }
            if (!selectedFound) selectedProgressionSkillId = overviewSnapshot.Skills[0].Id;
        }

        skillsPageList?.SetItemCount(overviewSnapshot.Skills.Count, "No skills available.");
        int skillFirst = skillsPageList?.FirstDataIndex ?? 0;
        for (int i = 0; i < ProgressionSkillRows; i++)
        {
            int index = skillFirst + i;
            bool visible = index < overviewSnapshot.Skills.Count && overviewSnapshot.Skills[index] != null;
            SetVisible(progressionSkillRows[i], visible);
            if (!visible) continue;
            RebirthCharacterUiSkill skill = overviewSnapshot.Skills[index];
            bool selected = string.Equals(skill.Id, selectedProgressionSkillId, StringComparison.OrdinalIgnoreCase);
            SetSprite(progressionSkillSelections[i], "UIAtlas", selected ? "ui_game_select_row" : "menu_empty", selected ? Color.white : (Color)new Color32(29,29,29,255));
            SetSprite(progressionSkillIcons[i], skill.Atlas, skill.Icon, Color.white);
            Set(progressionSkillNames[i], skill.Name);
            Set(progressionSkillPractical[i], "Lv " + skill.Practical.ToString("0", CultureInfo.InvariantCulture));
            Set(progressionSkillKnowledge[i], skill.SkillKnowledge.ToString("0", CultureInfo.InvariantCulture));
            SetFill(progressionSkillPracticalFills[i], skill.Practical >= skill.PracticalMax ? 1f : Mathf.Clamp01(skill.PracticalProgress));
            SetFill(progressionSkillKnowledgeFills[i], Normalize(skill.SkillKnowledge, skill.SkillKnowledgeMin, skill.SkillKnowledgeMax));
        }

        RebirthCharacterUiSkill selectedSkill = null;
        for (int i = 0; i < overviewSnapshot.Skills.Count; i++)
        {
            RebirthCharacterUiSkill skill = overviewSnapshot.Skills[i];
            if (skill != null && string.Equals(skill.Id, selectedProgressionSkillId, StringComparison.OrdinalIgnoreCase)) { selectedSkill = skill; break; }
        }
        if (selectedSkill == null)
        {
            Set(progressionSelectedName, L("xuiRebirthSurvivorNone", "None"));
            Set(progressionKnowledgeCount, "0 / 0");
            for (int i = 0; i < ProgressionKnowledgeRows; i++) SetVisible(progressionKnowledgeRows[i], false);
            return;
        }

        SetSprite(progressionSelectedIcon, selectedSkill.Atlas, selectedSkill.Icon, Color.white);
        Set(progressionSelectedName, selectedSkill.Name + "  ·  " + selectedSkill.Practical.ToString("0.##", CultureInfo.InvariantCulture) + "  ·  " + selectedSkill.PracticalStatus);
        Set(progressionSelectedAttribute, L("xuiRebirthProgressionPrimaryAttribute", "Primary Attribute") + ": [FFFFFF]" + selectedSkill.PrimaryAttributeName + "[-]");
        Set(progressionSelectedPractical, selectedSkill.Practical >= selectedSkill.PracticalMax ? "Maximum level reached" : (Mathf.Clamp01(selectedSkill.PracticalProgress) * 100f).ToString("0", CultureInfo.InvariantCulture) + "% toward level " + (selectedSkill.Practical + 1).ToString("0", CultureInfo.InvariantCulture));
        RebirthTheorySoloStatusClient.View(xui.playerUI.entityPlayer,selectedSkill.Id);
        SetVisible(GetChildById("btnSurvivorProgressionReflect"), RebirthTheorySoloStatusClient.CanReflect(xui.playerUI.entityPlayer,selectedSkill.Id));
        if(RebirthTheorySoloRegistry.TryGet(selectedSkill.Id,out var soloRule))SetTooltip(GetChildById("btnSurvivorProgressionReflect")?.ViewComponent,string.Format(L("xuiRebirthSoloTheoryRequirements","Reflect for {0} seconds after completing {1} relevant tasks. Moving or taking damage pauses study; select Reflect again to resume."),soloRule.Duration.ToString("0",CultureInfo.InvariantCulture),soloRule.MinimumOutcomes));
        Set(progressionSelectedKnowledge, selectedSkill.SkillKnowledge.ToString("0.0", CultureInfo.InvariantCulture) + " / " + selectedSkill.SkillKnowledgeMax.ToString("0", CultureInfo.InvariantCulture) + "  ·  " + selectedSkill.TheoryStatus);
        SetFill(progressionSelectedPracticalFill, selectedSkill.Practical >= selectedSkill.PracticalMax ? 1f : Mathf.Clamp01(selectedSkill.PracticalProgress));
        SetFill(progressionSelectedKnowledgeFill, Normalize(selectedSkill.SkillKnowledge, selectedSkill.SkillKnowledgeMin, selectedSkill.SkillKnowledgeMax));
        string relationshipHint = RebirthSurvivorUiText.BuildSkillRelationshipHint(selectedSkill.Id);
        string trainingText = BuildSelectedProgressionTrainingText(selectedSkill);
        Set(progressionSelectedTraining, BuildSelectedProgressionSummaryText(selectedSkill));
        SetTooltip(progressionSelectedTraining, trainingText + "\n\n" + relationshipHint);
        Set(progressionSelectedBounds, "Below 0 = starting weakness · 0 = neutral proficiency\nPractice raises your level, including negative levels.");

        List<RebirthCharacterUiKnowledge> related = new List<RebirthCharacterUiKnowledge>();
        for (int i = 0; i < overviewSnapshot.Knowledge.Count; i++)
        {
            RebirthCharacterUiKnowledge row = overviewSnapshot.Knowledge[i];
            if (row == null) continue;
            bool associated = false;
            for (int k = 0; k < row.AssociatedSkillIds.Count; k++)
                if (string.Equals(row.AssociatedSkillIds[k], selectedSkill.Id, StringComparison.OrdinalIgnoreCase)) { associated = true; break; }
            if (associated) related.Add(row);
        }
        related.Sort(delegate(RebirthCharacterUiKnowledge a, RebirthCharacterUiKnowledge b)
        {
            if (a.Known != b.Known) return a.Known ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        int knownCount = 0;
        for (int i = 0; i < related.Count; i++) if (related[i].Known) knownCount++;
        Set(progressionKnowledgeCount, knownCount.ToString(CultureInfo.InvariantCulture) + " / " + related.Count.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthProgressionKnownSuffix", "known"));
        SetVisible(ProjectionView("survivorProgressionKnowledgeEmpty"), related.Count == 0);
        Set(progressionKnowledgeEmpty, related.Count == 0 ? L("xuiRebirthProgressionNoAssociatedKnowledge", "No recipes or techniques are associated with this skill.") : string.Empty);
        knowledgePageList?.SetItemCount(related.Count, Localization.Get("xuiRebirthProgressionNoAssociatedKnowledge"));
        int knowledgeFirst = knowledgePageList?.FirstDataIndex ?? 0;
        for (int i = 0; i < ProgressionKnowledgeRows; i++)
        {
            int index = knowledgeFirst + i;
            bool visible = index < related.Count;
            SetVisible(progressionKnowledgeRows[i], visible);
            progressionKnowledgeIds[i] = visible ? related[index].Id : string.Empty;
            if (!visible) continue;
            RebirthCharacterUiKnowledge row = related[index];
            SetSprite(progressionKnowledgeIcons[i], "RebirthSurvivorIcons", "rb_ui_knowledge", row.Known ? Color.white : new Color32(110,110,110,255));
            Set(progressionKnowledgeNames[i], row.Name);
            Set(progressionKnowledgeStatus[i], row.Known ? "[8FD18F]" + L("xuiRebirthProgressionKnown", "KNOWN") + "[-]" : "[777777]" + L("xuiRebirthProgressionUnknown", "UNKNOWN") + "[-]");
        }
    }

    private void ProgressionSkill_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!progressionSkillButtonIndex.TryGetValue(sender, out index) || overviewSnapshot == null) return;
        index += skillsPageList?.FirstDataIndex ?? 0;
        if (index < 0 || index >= overviewSnapshot.Skills.Count) return;
        RebirthCharacterUiSkill skill = overviewSnapshot.Skills[index];
        if (skill == null) return;
        selectedProgressionSkillId = skill.Id;
        RenderProgression();
    }

    private void ProgressionKnowledge_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!progressionKnowledgeButtonIndex.TryGetValue(sender, out index) || index < 0 || index >= progressionKnowledgeIds.Length) return;
        string id = progressionKnowledgeIds[index];
        if (string.IsNullOrEmpty(id)) return;
        OpenLiveProgressionExplorer(id, "Character Progression Knowledge", sender, Tab.Progression);
    }

    private void ProgressionCancelStudy_OnPressed(XUiController sender,int mouseButton)
    {
        var player=xui?.playerUI?.entityPlayer;if(player==null||!RebirthTheorySoloStatusClient.TryCancellation(player,selectedProgressionSkillId,out var session))return;
        RebirthTheorySoloStatusClient.Refresh();var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null&&connection.IsServer){bool success=RebirthTheorySoloService.TryCancel(player,selectedProgressionSkillId,session,out var reason);RebirthTeachingService.NotifyStudy(player,success,reason);}
        else if(connection!=null)connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>().Setup(player.entityId,RebirthSurvivorSupportAction.CancelSoloTheory,null,selectedProgressionSkillId+"|"+session));
    }
    private void ProgressionReflect_OnPressed(XUiController sender,int mouseButton)
    {
        var player=xui?.playerUI?.entityPlayer;if(player==null||string.IsNullOrEmpty(selectedProgressionSkillId))return;
        RebirthTheorySoloStatusClient.Refresh();
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(connection!=null&&connection.IsServer){if(RebirthTheorySoloService.TryBegin(player,selectedProgressionSkillId,out var reason))RebirthTeachingService.NotifyStudy(player,true,reason);else RebirthTeachingService.NotifyStudy(player,false,reason);}
        else if(connection!=null)connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>().Setup(player.entityId,RebirthSurvivorSupportAction.SoloTheoryStudy,null,selectedProgressionSkillId));
    }
    private void ProgressionExploreSelected_OnPressed(XUiController sender, int mouseButton)
    {
        if (string.IsNullOrEmpty(selectedProgressionSkillId)) return;
        OpenLiveProgressionExplorer(selectedProgressionSkillId, "Character Progression Skill", sender, Tab.Progression);
    }

    private static float Normalize(float value, float min, float max)
    {
        float span = max - min;
        if (span <= 0.0001f) return 0f;
        return Mathf.Clamp01((value - min) / span);
    }

    private void RenderOverviewVitalsOnly()
    {
        if(overviewSnapshot==null)return;
        RenderOverviewVital(0, overviewSnapshot.Health, overviewSnapshot.HealthMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Health));
        RenderOverviewVital(1, overviewSnapshot.Stamina, overviewSnapshot.StaminaMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Stamina));
        Color32 energyColor = RebirthVitalHudColors.Get(RebirthVitalHudKind.Energy);
        RenderOverviewVital(2, overviewSnapshot.Energy, overviewSnapshot.EnergyMax, energyColor);
        RenderOverviewVital(3, overviewSnapshot.Nutrition, overviewSnapshot.NutritionMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Food));
        RenderOverviewVital(4, overviewSnapshot.Hydration, overviewSnapshot.HydrationMax, RebirthVitalHudColors.Get(RebirthVitalHudKind.Water));
    }

    private void RenderOverviewVital(int index, float value, float max, Color32 color)
    {
        if (index < 0 || index >= OverviewVitalRows) return;
        if (max <= 0f)
        {
            SetFill(overviewVitalFills[index], 0f);
            Set(overviewVitalValues[index], "—");
            return;
        }
        float safeMax = Mathf.Max(1f, max);
        SetFill(overviewVitalFills[index], value / safeMax);
        SetSpriteColor(overviewVitalFills[index], color);
        Set(overviewVitalValues[index], value.ToString("0", CultureInfo.InvariantCulture) + " / " + safeMax.ToString("0", CultureInfo.InvariantCulture));
    }

    private void RenderOverviewGearSlot(string slotId, XUiV_Label label, XUiV_Sprite icon, string emptyLabel, string emptyIcon)
    {
        string itemId = string.Empty;
        if (overviewSnapshot != null)
        {
            for (int i = 0; i < overviewSnapshot.Gear.Count; i++)
            {
                RebirthCharacterUiGear gear = overviewSnapshot.Gear[i];
                if (gear != null && string.Equals(gear.SlotId, slotId, StringComparison.OrdinalIgnoreCase))
                {
                    itemId = gear.ItemId ?? string.Empty;
                    break;
                }
            }
        }
        string waterItem = string.Empty;
        if(slotId==RebirthSurvivorGearService.SupportSlotId)
        {
            waterItem=RebirthOverviewWaterRequest.EquippedItem(xui.playerUI.entityPlayer);
            renderedSupportWaterItem=waterItem;
            if(!string.IsNullOrEmpty(waterItem))itemId=waterItem;
        }
        string gearKind = slotId == RebirthSurvivorGearService.WalkmanSlotId ? "Walkman" : slotId == RebirthSurvivorGearService.BackpackSlotId ? "Backpack" : slotId == RebirthSurvivorGearService.BeltSlotId ? "Belt" : "Support";
        SetVisible(ProjectionView("btnPc134Unequip" + gearKind), slotId==RebirthSurvivorGearService.SupportSlotId&&!string.IsNullOrEmpty(waterItem));
        var input=ProjectionView("survivorGearInput"+gearKind) as XUiC_RebirthSurvivorGearSlot;
        if(input!=null)input.EquippedItemId=itemId;
        if(slotId==RebirthSurvivorGearService.BackpackSlotId)SetVisible(ProjectionView("btnBackpackLibrary"),!string.IsNullOrEmpty(itemId));
        if(slotId==RebirthSurvivorGearService.WalkmanSlotId)SetVisible(ProjectionView("btnWalkmanLibrary"),!string.IsNullOrEmpty(itemId));
        if (string.IsNullOrEmpty(itemId))
        {
            Set(label, emptyLabel);
            SetTooltip(icon, emptyLabel + " — Empty\nEquip a compatible item from Inventory.");
            bool walkman = slotId == RebirthSurvivorGearService.WalkmanSlotId;
            SetSprite(icon, walkman ? "ItemIconAtlasGreyscale" : "RebirthUiIcons", emptyIcon,
                new Color32(200,200,200,180));
            return;
        }
        Set(label, emptyLabel);
        SetTooltip(icon, LocalizeItem(itemId));
        ItemValue value = ItemClass.GetItem(itemId, false);
        ItemClass item = value != null ? value.ItemClass : null;
        if (item != null) SetSprite(icon, "ItemIconAtlas", item.GetIconName(), item.GetIconTint());
        else SetSprite(icon, "RebirthUiIcons", emptyIcon, Color.white);
    }

    private void RenderOrigin()
    {
        if (snapshot == null) return;
        RebirthBackgroundDefinition bg;
        bool hasBg = RebirthSurvivorDefinitionRegistry.TryGetBackground(snapshot.BackgroundId, out bg) && bg != null;
        bool art = hasBg && originArtBinder != null && originArtBinder.BindBackground(bg);
        SetVisible(originArt, art);
        SetVisible(originArtPlaceholder, !art);
        Set(originTitle, hasBg ? RebirthSurvivorUiText.L(bg.NameKey, bg.Id) : snapshot.BackgroundId);

        RebirthDietDefinition diet;
        bool hasDiet = RebirthSurvivorDefinitionRegistry.TryGetDiet(snapshot.DietId, out diet) && diet != null;
        SetSprite(dietIcon, "RebirthSurvivorIcons", hasDiet ? diet.IconKey : "rb_diet_unrestricted", Color.white);
        StringBuilder text = new StringBuilder(768);
        if (hasBg)
        {
            text.Append(RebirthSurvivorUiText.L(bg.DescriptionKey, bg.Identity)).Append("\n\n")
                .Append(RebirthSurvivorUiText.BuildSignatureBonusSummary(bg.Id, true)).Append("\n\n")
                .Append("[B58CFF]").Append(L("xuiRebirthSurvivorStartingExperience", "Starting Experience")).Append("[-]\n")
                .Append(bg.StartingExperience).Append("\n\n");
        }
        text.Append("[B58CFF]").Append(L("xuiRebirthSurvivorReviewDiet", "DIET")).Append("[-]\n")
            .Append(hasDiet ? RebirthSurvivorUiText.L(diet.NameKey, diet.Id) : snapshot.DietId);
        if (hasDiet && !string.IsNullOrEmpty(diet.RuleSummary)) text.Append("\n").Append(diet.RuleSummary);
        text.Append("\n\n[B58CFF]").Append(L("xuiRebirthSurvivorSourceProfile", "SOURCE PROFILE")).Append("[-]\n")
            .Append(string.IsNullOrEmpty(snapshot.SourceProfileName) ? L("xuiRebirthSurvivorWorldNative", "Created directly for this world") : snapshot.SourceProfileName)
            .Append("\n\n[777777]").Append(snapshot.OriginDefinitionVersion).Append("[-]");
        Set(originBody, text.ToString());
    }

    /// <summary>Short text for the panel: the full training text does not fit and the label would shrink it to an unreadable size; the complete text stays in the tooltip.</summary>
    private string BuildSelectedProgressionSummaryText(RebirthCharacterUiSkill selectedSkill)
    {
        if(selectedSkill==null)return string.Empty;
        return selectedSkill.StartingPoint+"\n\nPractical training: "+selectedSkill.TrainingSource;
    }

    private string BuildSelectedProgressionTrainingText(RebirthCharacterUiSkill selectedSkill)
    {
        if(selectedSkill==null)return string.Empty;
        EntityPlayer trainingPlayer=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        string contribution=RebirthSkillTrainingUiProjectionService.GetOrRequest(trainingPlayer,selectedSkill.Id);
        string trainingText=selectedSkill.StartingPoint+"\n\nPractical training: "+selectedSkill.TrainingSource;
        if(!string.IsNullOrEmpty(contribution))trainingText+="\n\nTraining contribution: "+contribution;
        trainingText+="\n\nTheory sources: "+selectedSkill.TheorySource;
        if(!string.IsNullOrEmpty(selectedSkill.TheoryReveal))trainingText+="\n\n"+selectedSkill.TheoryReveal;
        return trainingText;
    }

    private void RefreshSelectedProgressionTraining()
    {
        if(overviewSnapshot==null||progressionSelectedTraining==null||string.IsNullOrEmpty(selectedProgressionSkillId))return;
        for(int i=0;i<overviewSnapshot.Skills.Count;i++)
        {
            RebirthCharacterUiSkill skill=overviewSnapshot.Skills[i];
            if(skill!=null&&string.Equals(skill.Id,selectedProgressionSkillId,StringComparison.OrdinalIgnoreCase))
            {Set(progressionSelectedTraining,BuildSelectedProgressionSummaryText(skill));return;}
        }
    }

    private void RenderAttributes()
    {
        if (snapshot == null) return;
        StringBuilder b = new StringBuilder(512);
        AppendAttribute(b, "strength");
        AppendAttribute(b, "dexterity");
        AppendAttribute(b, "constitution");
        AppendAttribute(b, "intelligence");
        AppendAttribute(b, "charisma");
        b.Append("\n\n[FFFFFF]").Append(L("xuiRebirthSurvivorHealthPotential", "Health Potential")).Append("[-]\n")
            .Append(snapshot.HealthPotential.ToString("0.0", CultureInfo.InvariantCulture));
        b.Append("\n\n[C6C6CE]").Append(L("xuiRebirthSurvivorAttributeHint", "Current is your trained value and is used for equipment requirements. Growth slows near Potential and continues beyond it.")).Append("[-]");
        Set(attributesText, b.ToString());
    }

    private void AppendAttribute(StringBuilder b, string id)
    {
        for (int i = 0; i < snapshot.Attributes.Count; i++)
        {
            RebirthSurvivorOwnerAttributeSnapshot a = snapshot.Attributes[i];
            if (a == null || !string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
            if (b.Length > 0) b.Append("\n");
            b.Append("[FFFFFF]").Append(RebirthUiProjectionTextCache.DefinitionName(id)).Append("[-]\n")
                .Append(L("xuiRebirthConditionCurrent", "Current")).Append(": ")
                .Append(a.Current.ToString("0.0", CultureInfo.InvariantCulture)).Append("    ")
                .Append(L("xuiRebirthConditionPotential", "Potential")).Append(": ")
                .Append(a.Potential.ToString("0.0", CultureInfo.InvariantCulture));
            return;
        }
    }

    private void RenderSkills()
    {
        if (snapshot == null) return;
        List<string> sourceIds = new List<string>(snapshot.Skills.Count);
        Dictionary<string, RebirthSurvivorOwnerSkillSnapshot> byId = new Dictionary<string, RebirthSurvivorOwnerSkillSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (RebirthSurvivorOwnerSkillSnapshot skill in snapshot.Skills)
            if (skill != null && !string.IsNullOrEmpty(skill.Id)) { sourceIds.Add(skill.Id); byId[skill.Id] = skill; }
        IList<string> ids = skillOrder.Get(sourceIds, RebirthUiProjectionTextCache.DefinitionName);
        List<RebirthSurvivorOwnerSkillSnapshot> list = new List<RebirthSurvivorOwnerSkillSnapshot>(ids.Count);
        foreach (string id in ids) list.Add(byId[id]);
        for (int i = 0; i < SkillRows; i++)
        {
            bool visible = i < list.Count && list[i] != null;
            SetVisible(skillRows[i], visible);
            skillExploreIds[i] = visible ? list[i].Id : string.Empty;
            if (!visible) { Set(skillNames[i], string.Empty); Set(skillValues[i], string.Empty); continue; }
            RebirthSurvivorOwnerSkillSnapshot skill = list[i];
            Set(skillNames[i], RebirthUiProjectionTextCache.DefinitionName(skill.Id));
            Set(skillValues[i], skill.Value.ToString("0.0", CultureInfo.InvariantCulture) + "   [C6C6CE]" + (Mathf.Clamp01(skill.Progress) * 100f).ToString("0", CultureInfo.InvariantCulture) + "% to next[-]");
        }
        Set(skillsText, list.Count == 0 ? L("xuiRebirthSurvivorNone", "None") : string.Empty);
    }

    private void RenderKnowledge()
    {
        if (snapshot == null) return;
        IList<string> ids = knowledgeOrder.Get(snapshot.KnowledgeIds, RebirthUiProjectionTextCache.DefinitionName);
        for (int i = 0; i < KnowledgeRows; i++)
        {
            bool visible = i < ids.Count;
            SetVisible(knowledgeRows[i], visible);
            knowledgeExploreIds[i] = visible ? ids[i] : string.Empty;
            Set(knowledgeNames[i], visible ? RebirthUiProjectionTextCache.DefinitionName(ids[i]) : string.Empty);
        }
        Set(knowledgeText, ids.Count == 0 ? L("xuiRebirthProgressionNoKnowledge", "No recipes or techniques learned.") : string.Empty);
    }

    private void SkillExplore_OnPressed(XUiController sender, int mouseButton)
    {
        int index; if (!skillButtonIndex.TryGetValue(sender, out index) || index < 0 || index >= skillExploreIds.Length) return;
        OpenLiveProgressionExplorer(skillExploreIds[index], "Live Skill", sender, Tab.Skills);
    }

    private void KnowledgeExplore_OnPressed(XUiController sender, int mouseButton)
    {
        int index; if (!knowledgeButtonIndex.TryGetValue(sender, out index) || index < 0 || index >= knowledgeExploreIds.Length) return;
        OpenLiveProgressionExplorer(knowledgeExploreIds[index], "Live Knowledge", sender, Tab.Knowledge);
    }

    private void OpenLiveProgressionExplorer(string focusId, string reason, XUiController returnFocus, Tab returnTab)
    {
        focusId = (focusId ?? string.Empty).Trim();
        if (focusId.Length == 0 || xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        RebirthSurvivorOwnerStateSnapshot current = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (current == null || !current.RebirthModeEnabled || !current.HasCharacter) return;
        string token = "character:" + Guid.NewGuid().ToString("N");
        progressionExplorerReturnToken = token;
        progressionExplorerReturnFocus = returnFocus;
        progressionExplorerReturnTab = returnTab;
        if (skillsContent != null && skillsContent.ViewComponent != null) progressionExplorerSkillsContentPosition = skillsContent.ViewComponent.Position;
        if (knowledgeContent != null && knowledgeContent.ViewComponent != null) progressionExplorerKnowledgeContentPosition = knowledgeContent.ViewComponent.Position;
        progressionExplorerProgressionSkillsContentPosition = new Vector2i(0, Mathf.RoundToInt(skillsPageList?.ScrollOffset ?? 0f));
        progressionExplorerProgressionKnowledgeContentPosition = new Vector2i(0, Mathf.RoundToInt(knowledgePageList?.ScrollOffset ?? 0f));
        RebirthProgressionExplorerReturnRegistry.Register(token, delegate(XUi returnXui, string ignored)
        {
            if (returnXui == null || returnXui.playerUI == null || returnXui.playerUI.windowManager == null) return;
            resumeFromProgressionExplorerPending = true;
            suspendedForProgressionExplorer = true;
            XUiController caller = returnXui.FindWindowGroupByName(WindowGroupId);
            if (caller != null) returnXui.playerUI.windowManager.Open((GUIWindow)caller.windowGroup, true);
        });
        RebirthProgressionExplorerReturnContext returnContext = new RebirthProgressionExplorerReturnContext(WindowGroupId, token,
            L("xuiRebirthProgressionExplorerReturnCharacter", "RETURN TO CHARACTER"));
        RebirthProgressionExplorerLaunchRequest request = new RebirthProgressionExplorerLaunchRequest(focusId,
            RebirthProgressionExplorerMode.LiveCharacter, reason, returnContext);
        suspendedForProgressionExplorer = true;
        xui.playerUI.windowManager.Close(WindowGroupId);
        string error;
        if (!RebirthProgressionExplorerUiService.Open(xui, request, out error))
        {
            RebirthProgressionExplorerReturnRegistry.Unregister(token);
            progressionExplorerReturnToken = string.Empty;
            resumeFromProgressionExplorerPending = true;
            suspendedForProgressionExplorer = true;
            XUiController caller = xui.FindWindowGroupByName(WindowGroupId);
            if (caller != null) xui.playerUI.windowManager.Open((GUIWindow)caller.windowGroup, true);
            Log.Warning("[REBIRTH Survivor] Progression Explorer live-character launch failed: " + error);
        }
    }

    private void RenderTraits()
    {
        if (snapshot == null) return;
        int count = snapshot.TraitIds.Count;
        int maxOffset = Math.Max(0, count - TraitRows);
        traitOffset = Mathf.Clamp(traitOffset, 0, maxOffset);
        int start = traitOffset;
        for (int i = 0; i < TraitRows; i++)
        {
            int index = start + i;
            bool visible = index < count;
            SetVisible(traitRows[i], visible);
            if (!visible) continue;
            RebirthTraitDefinition trait;
            string id = snapshot.TraitIds[index];
            if (RebirthSurvivorDefinitionRegistry.TryGetTrait(id, out trait) && trait != null)
            {
                SetSprite(traitIcons[i], "RebirthSurvivorIcons", trait.IconKey, Color.white);
                Set(traitNames[i], RebirthSurvivorUiText.L(trait.NameKey, trait.Id));
                Set(traitMeta[i], PolarityText(trait) + "  •  " + trait.Category);
            }
            else
            {
                SetSprite(traitIcons[i], "RebirthSurvivorIcons", string.Empty, Color.white);
                Set(traitNames[i], id);
                Set(traitMeta[i], string.Empty);
            }
        }
        int first = count > 0 ? traitOffset + 1 : 0;
        int last = Math.Min(count, traitOffset + TraitRows);
        Set(traitPageText, count > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0} {1}-{2} / {3}", L("xuiRebirthSurvivorTraits", "Traits"), first, last, count)
            : L("xuiRebirthSurvivorNoTraits", "None"));
        if (focusedTraitIndex < 0 || focusedTraitIndex >= count) focusedTraitIndex = count > 0 ? 0 : -1;
        RenderFocusedTrait();
        UpdateTraitNativeScroll(count);
    }

    private void RenderFocusedTrait()
    {
        if (snapshot == null || focusedTraitIndex < 0 || focusedTraitIndex >= snapshot.TraitIds.Count)
        {
            Set(traitDetails, L("xuiRebirthSurvivorNoTraits", "None")); return;
        }
        RebirthTraitDefinition trait;
        if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(snapshot.TraitIds[focusedTraitIndex], out trait) || trait == null)
        { Set(traitDetails, snapshot.TraitIds[focusedTraitIndex]); return; }
        StringBuilder b = new StringBuilder(512);
        b.Append("[FFFFFF]").Append(RebirthSurvivorUiText.L(trait.NameKey, trait.Id)).Append("[-]\n")
            .Append(RebirthSurvivorUiText.L(trait.DescriptionKey, trait.EffectSummary)).Append("\n\n")
            .Append(PolarityText(trait)).Append("  •  ").Append(trait.Category);
        if (!string.IsNullOrEmpty(trait.EffectSummary)) b.Append("\n\n").Append(trait.EffectSummary);
        Set(traitDetails, b.ToString());
    }

    private sealed class ConditionHistoryEntry
    {
        public string Time = string.Empty;
        public string Text = string.Empty;
    }

    private void RenderConditions()
    {
        if (overviewSnapshot == null) return;
        UpdateConditionHistory();
        EnsureSelectedCondition();
        RenderConditionList();
        RenderSelectedCondition();
        RenderConditionSummary();
        RenderConditionHistory();
    }

    private void EnsureSelectedCondition()
    {
        if (overviewSnapshot == null || overviewSnapshot.Conditions.Count == 0)
        {
            selectedConditionId = string.Empty;
            return;
        }
        for (int i = 0; i < overviewSnapshot.Conditions.Count; i++)
        {
            RebirthCharacterUiCondition condition = overviewSnapshot.Conditions[i];
            if (condition != null && string.Equals(condition.Id, selectedConditionId, StringComparison.OrdinalIgnoreCase)) return;
        }
        selectedConditionId = overviewSnapshot.Conditions[0] != null ? overviewSnapshot.Conditions[0].Id ?? string.Empty : string.Empty;
    }

    private void RenderConditionList()
    {
        int count = overviewSnapshot != null ? overviewSnapshot.Conditions.Count : 0;
        Set(conditionTotalCount, count.ToString(CultureInfo.InvariantCulture));
        int positive = 0, negative = 0, neutral = 0;
        for (int i = 0; i < count; i++)
        {
            RebirthCharacterUiCondition c = overviewSnapshot.Conditions[i];
            if (c == null) continue;
            if (c.Positive) positive++;
            else if (c.Negative) negative++;
            else neutral++;
        }
        Set(conditionListSummary, positive.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionPositive", "positive") +
            "  •  " + negative.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionNegative", "negative") +
            "  •  " + neutral.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthConditionNeutral", "neutral"));

        conditionsPageList?.SetItemCount(count, "No active conditions.");
        int first = conditionsPageList?.FirstDataIndex ?? 0;
        for (int i = 0; i < ConditionRows; i++)
        {
            int index = first + i;
            bool visible = index < count && overviewSnapshot.Conditions[index] != null;
            SetVisible(conditionRows[i], visible);
            if (!visible) continue;
            RebirthCharacterUiCondition c = overviewSnapshot.Conditions[index];
            bool selected = string.Equals(c.Id, selectedConditionId, StringComparison.OrdinalIgnoreCase);
            SetVisible(conditionSelections[i] != null ? conditionSelections[i].Controller : null, selected);
            Color accent = ConditionAccent(c);
            SetSpriteColor(conditionAccents[i], accent);
            bool hasIcon = !string.IsNullOrEmpty(c.Icon);
            SetVisible(conditionIcons[i] != null ? conditionIcons[i].Controller : null, hasIcon);
            if (hasIcon) SetSprite(conditionIcons[i], string.IsNullOrEmpty(c.Atlas) ? "RebirthSurvivorIcons" : c.Atlas, c.Icon, c.IconColor);
            Set(conditionNames[i], c.Name);
            Set(conditionCategories[i], c.Category);
            Set(conditionDetails[i], c.Detail);
        }
    }

    private void RenderSelectedCondition()
    {
        RebirthCharacterUiCondition selected = FindSelectedCondition();
        bool hasSelected = selected != null;
        SetVisible(conditionSelectedAccent != null ? conditionSelectedAccent.Controller : null, hasSelected);
        SetVisible(conditionSelectedIcon != null ? conditionSelectedIcon.Controller : null, hasSelected);
        if (!hasSelected)
        {
            Set(conditionSelectedName, L("xuiRebirthConditionNoneActive", "No conditions available"));
            Set(conditionSelectedCategory, string.Empty);
            Set(conditionSelectedDuration, string.Empty);
            Set(conditionSelectedDescription, L("xuiRebirthConditionNoneAvailableBody", "No source-backed condition data is currently available."));
            Set(conditionSelectedStatus, string.Empty);
            Set(conditionSelectedEffects, string.Empty);
            Set(conditionSelectedGuidance, string.Empty);
            Set(conditionSelectedSource, string.Empty);
            SetVisible(conditionEffectsPanel, false);
            SetVisible(conditionGuidancePanel, false);
            return;
        }

        Color accent = ConditionAccent(selected);
        SetSpriteColor(conditionSelectedAccent, accent);
        bool hasSelectedIcon = !string.IsNullOrEmpty(selected.Icon);
        SetVisible(conditionSelectedIcon != null ? conditionSelectedIcon.Controller : null, hasSelectedIcon);
        if (hasSelectedIcon) SetSprite(conditionSelectedIcon, string.IsNullOrEmpty(selected.Atlas) ? "RebirthSurvivorIcons" : selected.Atlas, selected.Icon, selected.IconColor);
        Set(conditionSelectedName, selected.Name);
        Set(conditionSelectedCategory, selected.Category);
        Set(conditionSelectedDuration, selected.Duration);
        Set(conditionSelectedDescription, selected.Description);
        SetTooltip(conditionSelectedDescription, selected.Description);
        Set(conditionSelectedStatus, selected.Status);
        bool hasEffects = !string.IsNullOrWhiteSpace(selected.Effects) && selected.Effects != selected.Description;
        bool hasGuidance = !string.IsNullOrWhiteSpace(selected.Guidance);
        SetVisible(conditionEffectsPanel, hasEffects);
        SetVisible(conditionGuidancePanel, hasGuidance);
        Set(conditionSelectedEffects, selected.Effects);
        SetTooltip(conditionSelectedEffects, selected.Effects);
        Set(conditionSelectedGuidance, selected.Guidance);
        SetTooltip(conditionSelectedGuidance, selected.Guidance);
        Set(conditionSelectedSource, selected.SourceSystem);
    }

    private void RenderConditionSummary()
    {
        int positive = 0, negative = 0, neutral = 0, native = 0;
        if (overviewSnapshot != null)
        {
            for (int i = 0; i < overviewSnapshot.Conditions.Count; i++)
            {
                RebirthCharacterUiCondition c = overviewSnapshot.Conditions[i];
                if (c == null) continue;
                if (c.Positive) positive++;
                else if (c.Negative) negative++;
                else neutral++;
                if ((c.Id ?? string.Empty).StartsWith("buff:", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.SourceSystem, L("xuiRebirthConditionSourceNative", "Base Game"), StringComparison.OrdinalIgnoreCase)) native++;
            }
        }
        float total = Math.Max(1, positive + negative + neutral);
        SetFill(Sprite("pc135PositiveFill"), positive / total);
        SetFill(Sprite("pc135NegativeFill"), negative / total);
        SetFill(Sprite("pc135NeutralFill"), neutral / total);
        Set(conditionPositiveCount, L("xuiRebirthConditionPositive", "Positive") + "  " + positive.ToString(CultureInfo.InvariantCulture));
        Set(conditionNegativeCount, L("xuiRebirthConditionNegative", "Negative") + "  " + negative.ToString(CultureInfo.InvariantCulture));
        Set(conditionNeutralCount, L("xuiRebirthConditionNeutral", "Neutral") + "  " + neutral.ToString(CultureInfo.InvariantCulture));
        Set(conditionNativeCount, L("xuiRebirthConditionNativeStatuses", "Native statuses") + "  " + native.ToString(CultureInfo.InvariantCulture));

        if (snapshot != null)
        {
            SetSprite(Sprite("pc135MoodIcon"), "RebirthSurvivorIcons", snapshot.MoodCurrent >= 80f ? "rb_condition_mood_excellent" : snapshot.MoodCurrent >= 55f ? "rb_condition_mood_good" : snapshot.MoodCurrent >= 30f ? "rb_condition_mood_low" : "rb_condition_mood_miserable", Color.white);
            SetFill(Sprite("pc135MoodFill"), snapshot.MoodCurrent / 100f);
            SetFill(Sprite("pc135DietFill"), snapshot.DietSatisfaction / 100f);
            SetFill(Sprite("pc135HealthFill"), snapshot.HealthCapacity / Math.Max(1f, snapshot.HealthPotential));
            Set(conditionMoodSummary, "Mood  " + snapshot.MoodCurrent.ToString("0.0", CultureInfo.InvariantCulture) +
                " / 100  •  " + L("xuiRebirthConditionTarget", "target") + " " + snapshot.MoodTarget.ToString("0.0", CultureInfo.InvariantCulture));
            Set(conditionDietSummary, L("xuiRebirthConditionDietSatisfaction", "Diet Satisfaction") + "  " + snapshot.DietSatisfaction.ToString("0.0", CultureInfo.InvariantCulture) +
                "  •  " + snapshot.RecentVarietyCount.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthCharacterOverviewVarieties", "varieties"));
            Set(conditionHealthSummary, L("xuiRebirthCharacterOverviewHealthCapacity", "Health Capacity") + "  " + snapshot.HealthCapacity.ToString("0.0", CultureInfo.InvariantCulture) +
                " / " + snapshot.HealthPotential.ToString("0.0", CultureInfo.InvariantCulture));
        }
        else
        {
            Set(conditionMoodSummary, string.Empty);
            Set(conditionDietSummary, string.Empty);
            Set(conditionHealthSummary, string.Empty);
        }
        if (hasMetabolism)
        {
            Set(conditionMetabolismSummary, L("xuiRebirthConditionMetabolismState", "Metabolism") + "  H " + PercentText(metabolism.Hydration, metabolism.HydrationMax) +
                "  •  N " + PercentText(metabolism.Food, metabolism.FoodMax) + "  •  E " + PercentText(metabolism.Energy, metabolism.EnergyMax));
        }
        else Set(conditionMetabolismSummary, L("xuiRebirthMetabolismAwaiting", "Waiting for authoritative metabolism state…"));
    }

    private void UpdateConditionHistory()
    {
        Dictionary<string, string> current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (overviewSnapshot != null)
        {
            for (int i = 0; i < overviewSnapshot.Conditions.Count; i++)
            {
                RebirthCharacterUiCondition c = overviewSnapshot.Conditions[i];
                if (c == null || string.IsNullOrEmpty(c.Id)) continue;
                current[c.Id] = string.IsNullOrEmpty(c.Name) ? c.Id : c.Name;
            }
        }
        if (!conditionHistoryInitialized)
        {
            previousConditionNames.Clear();
            foreach (KeyValuePair<string, string> kv in current) previousConditionNames[kv.Key] = kv.Value;
            conditionHistoryInitialized = true;
            return;
        }

        foreach (KeyValuePair<string, string> kv in current)
            if (!previousConditionNames.ContainsKey(kv.Key)) AddConditionHistory(L("xuiRebirthConditionLogAdded", "Added") + ": " + kv.Value);
        foreach (KeyValuePair<string, string> kv in previousConditionNames)
            if (!current.ContainsKey(kv.Key)) AddConditionHistory(L("xuiRebirthConditionLogCleared", "Cleared") + ": " + kv.Value);

        previousConditionNames.Clear();
        foreach (KeyValuePair<string, string> kv in current) previousConditionNames[kv.Key] = kv.Value;
    }

    private void AddConditionHistory(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        conditionHistory.Insert(0, new ConditionHistoryEntry { Time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture), Text = text });
        if (conditionHistory.Count > ConditionHistoryCapacity) conditionHistory.RemoveRange(ConditionHistoryCapacity, conditionHistory.Count - ConditionHistoryCapacity);
    }

    private void RenderConditionHistory()
    {
        historyPageList?.SetItemCount(conditionHistory.Count, "No condition changes observed this session.");
        int first = historyPageList?.FirstDataIndex ?? 0;
        int visibleCount = Math.Min(ConditionHistoryRows, conditionHistory.Count - first);
        for (int i = 0; i < ConditionHistoryRows; i++)
        {
            bool visible = i < visibleCount;
            SetVisible(conditionHistoryRows[i], visible);
            if (!visible) continue;
            Set(conditionHistoryTimes[i], conditionHistory[first + i].Time);
            Set(conditionHistoryTexts[i], conditionHistory[first + i].Text);
        }
        SetVisible(conditionHistoryEmpty != null ? conditionHistoryEmpty.Controller : null, conditionHistory.Count == 0);
    }

    private void Condition_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!conditionButtonIndex.TryGetValue(sender, out index) || overviewSnapshot == null) return;
        index += conditionsPageList?.FirstDataIndex ?? 0;
        if (index < 0 || index >= overviewSnapshot.Conditions.Count) return;
        RebirthCharacterUiCondition condition = overviewSnapshot.Conditions[index];
        if (condition == null) return;
        selectedConditionId = condition.Id ?? string.Empty;
        RenderConditionList();
        RenderSelectedCondition();
    }

    private RebirthCharacterUiCondition FindSelectedCondition()
    {
        if (overviewSnapshot == null) return null;
        for (int i = 0; i < overviewSnapshot.Conditions.Count; i++)
        {
            RebirthCharacterUiCondition c = overviewSnapshot.Conditions[i];
            if (c != null && string.Equals(c.Id, selectedConditionId, StringComparison.OrdinalIgnoreCase)) return c;
        }
        return null;
    }

    private static Color ConditionAccent(RebirthCharacterUiCondition condition)
    {
        if (condition != null && condition.Positive) return new Color32(82, 136, 91, 255);
        if (condition != null && condition.Negative) return new Color32(196, 42, 42, 255);
        return new Color32(128, 128, 128, 255);
    }

    private static string PercentText(float value, float max)
    {
        return (max > 0.001f ? Mathf.Clamp01(value / max) * 100f : 0f).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    private void RenderMetabolism()
    {
        if (!hasMetabolism)
        {
            Set(metabolismText, L("xuiRebirthMetabolismAwaiting", "Waiting for authoritative metabolism state…")); return;
        }
        StringBuilder b = new StringBuilder(1280);
        b.Append("[FFFFFF]HYDRATION[-]\n")
            .Append(metabolism.Hydration.ToString("0.0", CultureInfo.InvariantCulture)).Append(" / ").Append(metabolism.HydrationMax.ToString("0.0", CultureInfo.InvariantCulture))
            .Append("   •   net ").Append(Signed(metabolism.HydrationNetPointsPerRealMinute)).Append(" / real min\n\n")
            .Append("[FFFFFF]NUTRITION[-]\n")
            .Append(metabolism.Food.ToString("0.0", CultureInfo.InvariantCulture)).Append(" / ").Append(metabolism.FoodMax.ToString("0.0", CultureInfo.InvariantCulture))
            .Append("   •   net ").Append(Signed(metabolism.NutritionNetPointsPerRealMinute)).Append(" / real min\n\n")
            .Append("[FFFFFF]ENERGY[-]\n")
            .Append(metabolism.Energy.ToString("0.0", CultureInfo.InvariantCulture)).Append(" / ").Append(metabolism.EnergyMax.ToString("0.0", CultureInfo.InvariantCulture))
            .Append("   •   use ").Append(metabolism.EnergyUsePerRealMinute.ToString("0.00", CultureInfo.InvariantCulture)).Append(" / min")
            .Append("   •   recovery ").Append(metabolism.EnergyRecoveryPerRealMinute.ToString("0.00", CultureInfo.InvariantCulture)).Append(" / min\n\n")
            .Append("[FFFFFF]DIGESTION[-]\n")
            .Append("Digestive Health  ").Append(metabolism.DigestiveHealth.ToString("0.0", CultureInfo.InvariantCulture)).Append(" / 100\n")
            .Append("Stomach  ").Append(metabolism.FullnessMl.ToString("0", CultureInfo.InvariantCulture)).Append(" / ").Append(metabolism.StomachCapacityMl.ToString("0", CultureInfo.InvariantCulture)).Append(" mL")
            .Append("   •   liquid ").Append(metabolism.StomachFluidMl.ToString("0", CultureInfo.InvariantCulture)).Append("   •   solid ").Append(metabolism.StomachSolidMl.ToString("0", CultureInfo.InvariantCulture)).Append("\n")
            .Append("Intestine  ").Append((metabolism.IntestinalFluidMl + metabolism.IntestinalSolidMl).ToString("0", CultureInfo.InvariantCulture)).Append(" / ").Append(metabolism.IntestinalCapacityMl.ToString("0", CultureInfo.InvariantCulture)).Append(" mL")
            .Append("   •   liquid ").Append(metabolism.IntestinalFluidMl.ToString("0", CultureInfo.InvariantCulture)).Append("   •   solid ").Append(metabolism.IntestinalSolidMl.ToString("0", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(metabolism.ActiveIntakeItemName))
            b.Append("\n\n[C6C6CE]Active intake: ").Append(LocalizeItem(metabolism.ActiveIntakeItemName)).Append("  •  ").Append(metabolism.ActiveIntakeRemainingMl.ToString("0", CultureInfo.InvariantCulture)).Append(" mL remaining[-]");
        Set(metabolismText, b.ToString());
    }

    private void RenderGear()
    {
        if (snapshot == null) return;
        int physical = snapshot.PhysicalBagSlots > 0 ? snapshot.PhysicalBagSlots : RebirthSurvivorGearService.BasePhysicalBagSlots;
        Set(gearCapacity, physical.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthSurvivorPhysicalSlots", "physical backpack slots") + "\n[C6C6CE]" + L("xuiRebirthSurvivorEncumbranceSeparate", "Encumbrance remains governed by Carry Capacity.") + "[-]");
        Set(gearHint, L("xuiRebirthSurvivorGearEquipHint", "Use a compatible Survivor gear item from your inventory to equip it. Unequip returns the item to inventory when space is available."));
        RenderGearSlot(RebirthSurvivorGearService.BackpackSlotId, backpackName, backpackIcon, btnBackpackUnequip, L("xuiRebirthSurvivorGearBackpack", "Backpack"));
        RenderGearSlot(RebirthSurvivorGearService.BeltSlotId, beltName, beltIcon, btnBeltUnequip, L("xuiRebirthSurvivorGearBelt", "Belt"));
        RenderGearSlot(RebirthSurvivorGearService.SupportSlotId, supportName, supportIcon, btnSupportUnequip, L("xuiRebirthSurvivorGearSupport", "Support"));
    }

    private void RenderGearSlot(string slotId, XUiV_Label label, XUiV_Sprite icon, XUiController unequip, string emptyLabel)
    {
        string itemId = string.Empty;
        for (int i = 0; i < snapshot.GearSlots.Count; i++)
        {
            RebirthSurvivorOwnerGearSnapshot g = snapshot.GearSlots[i];
            if (g != null && string.Equals(g.SlotId, slotId, StringComparison.OrdinalIgnoreCase)) { itemId = g.ItemId ?? string.Empty; break; }
        }
        bool occupied = !string.IsNullOrEmpty(itemId);
        Set(label, occupied ? LocalizeItem(itemId) : "[777777]" + emptyLabel + " — Empty[-]");
        SetVisible(unequip, occupied);
        if (!occupied) { SetSprite(icon, "UIAtlas", "ui_game_symbol_backpack", new Color32(115,115,115,255)); return; }
        ItemValue value = ItemClass.GetItem(itemId, false);
        ItemClass item = value != null ? value.ItemClass : null;
        if (item != null) SetSprite(icon, "ItemIconAtlas", item.GetIconName(), item.GetIconTint());
        else SetSprite(icon, "UIAtlas", "ui_game_symbol_backpack", Color.white);
    }

    private void RequestUnequip(string slotId)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.entityPlayer == null) return;
        EntityPlayer player = xui.playerUI.entityPlayer;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null) return;
        c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>()
            .Setup(player.entityId, RebirthSurvivorSupportAction.UnequipGearSlot, null, slotId));
    }

    private void TraitRow_OnPressed(XUiController sender, int mouseButton)
    {
        int local; if (!traitButtonIndex.TryGetValue(sender, out local) || snapshot == null) return;
        int index = traitOffset + local;
        if (index < 0 || index >= snapshot.TraitIds.Count) return;
        focusedTraitIndex = index;
        RenderFocusedTrait();
    }
    private void WireTraitScroll(XUiController controller)
    {
        if (controller == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta)
        {
            int count = snapshot != null ? snapshot.TraitIds.Count : 0;
            int old = traitOffset;
            if (delta > 0f) traitOffset--; else if (delta < 0f) traitOffset++;
            traitOffset = Mathf.Clamp(traitOffset, 0, Math.Max(0, count - TraitRows));
            if (traitOffset != old) RenderTraits();
        };
    }

    private void UpdateTraitNativeScroll(int count)
    {
        int maxOffset = Math.Max(0, count - TraitRows);
        if (traitNativeScrollHost != null && traitNativeScrollHost.ViewComponent != null)
            traitNativeScrollHost.ViewComponent.IsVisible = maxOffset > 0;
        if (traitNativeScrollProxy == null || traitNativeScrollProxy.ViewComponent == null) return;
        int contentHeight = maxOffset > 0 ? Math.Max(TraitTrackHeight + 1, Mathf.CeilToInt(TraitTrackHeight * (count / (float)TraitRows))) : TraitTrackHeight;
        traitNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        syncingTraitNativeScroll = true;
        RebirthNativeScrollbarUtil.Refresh(traitNativeScrollView);
        RebirthNativeScrollbarUtil.TrySetValue(traitNativeScrollView, maxOffset > 0 ? traitOffset / (float)maxOffset : 0f);
        RebirthNativeScrollbarUtil.Refresh(traitNativeScrollView);
        syncingTraitNativeScroll = false;
    }

    private void PollTraitNativeScroll()
    {
        if (syncingTraitNativeScroll || snapshot == null) return;
        int maxOffset = Math.Max(0, snapshot.TraitIds.Count - TraitRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!RebirthNativeScrollbarUtil.TryGetValue(traitNativeScrollView, out normalized)) return;
        int requested = Mathf.Clamp(Mathf.RoundToInt(normalized * maxOffset), 0, maxOffset);
        if (requested == traitOffset) return;
        traitOffset = requested;
        RenderTraits();
    }
    private void CaptureCharacterHud()
    {
        if (characterHudCaptured || xui == null) return;
        XUiController toolbelt = xui.FindWindowGroupByName("toolbelt");
        string[] ids = { "windowLocation", "windowQuestTracker", "windowRecipeTracker" };
        for (int i = 0; i < ids.Length; i++)
        {
            XUiController c = toolbelt?.GetChildById(ids[i]);
            characterHud[i] = c;
            if (c?.ViewComponent == null) continue;
            characterHudVisible[i] = c.ViewComponent.IsVisible;
            characterHudEnabled[i] = c.ViewComponent.Enabled;
            RebirthPersonalCraftingHudSuppressionInstaller.ForceHidden(c);
        }
        characterHudCaptured = true;
    }

    private void RestoreCharacterHud()
    {
        if (!characterHudCaptured) return;
        for (int i = 0; i < characterHud.Length; i++)
        {
            XUiController c = characterHud[i];
            if (c?.ViewComponent != null)
            {
                c.ViewComponent.Enabled = characterHudEnabled[i];
                c.ViewComponent.IsVisible = characterHudVisible[i];
            }
            characterHud[i] = null;
        }
        characterHudCaptured = false;
    }

    private void Close_OnPressed(XUiController sender, int mouseButton) { CloseWindow(); }

    private void CloseWindow()
    {
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
            xui.playerUI.windowManager.Close(WindowGroupId);
    }

    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController c = GetChildById(id);
        if (c != null) c.OnPress += delegate(XUiController sender, int mouseButton)
        {
            if (!RebirthConsoleInputGuardRuntime.BlocksGameplayInput()
                && !(hudTrackingManager?.IsManagerOpen ?? false)) handler(sender, mouseButton);
        };
        return c;
    }
    private XUiController ProjectionView(string id)
    {
        if (projectionViews.TryGetValue(id, out var found)) return found;
        found = GetChildById(id);
        if (found != null) projectionViews.Add(id, found);
        return found;
    }
    private XUiV_Label Label(string id) { return ProjectionView(id)?.ViewComponent as XUiV_Label; }
    private XUiV_Sprite Sprite(string id) { return ProjectionView(id)?.ViewComponent as XUiV_Sprite; }
    private static void Set(XUiV_Label label, string value) { if (label != null && label.Text != (value ?? string.Empty)) label.Text = value ?? string.Empty; }
    private static void SetTooltip(XUiView view, string value) { if (view != null && view.ToolTip != (value ?? string.Empty)) view.ToolTip = value ?? string.Empty; }
    private static void SetVisible(XUiController c, bool visible) { if (c != null && c.ViewComponent != null && c.ViewComponent.IsVisible != visible) c.ViewComponent.IsVisible = visible; }
    private static void SetSprite(XUiV_Sprite sprite, string atlas, string name, Color color)
    {
        if (sprite == null) return;
        if (sprite.UIAtlas != (atlas ?? "UIAtlas")) sprite.UIAtlas = atlas ?? "UIAtlas";
        if (sprite.SpriteName != (name ?? string.Empty)) sprite.SetSpriteImmediately(name ?? string.Empty);
        // Keep the stored view color and live widget in sync; immediate-only writes
        // leave Color stale and can skip the next red-to-white selection change.
        bool changed = sprite.Color != color || (sprite.Sprite != null && sprite.Sprite.color != color);
        if (changed) { sprite.Color = color; sprite.SetColorImmediately(color); }
    }
    private static void SetFill(XUiV_Sprite sprite, float fill) { if (sprite != null) sprite.Fill = Mathf.Clamp01(fill); }
    private static void SetSpriteColor(XUiV_Sprite sprite, Color color) { if (sprite == null) return; if (sprite.Color != color || (sprite.Sprite != null && sprite.Sprite.color != color)) { sprite.Color = color; sprite.SetColorImmediately(color); } }
    private static string L(string key, string fallback) { return RebirthSurvivorUiText.L(key, fallback); }
    private static string LocalizeItem(string itemId)
    {
        ItemValue value = ItemClass.GetItem(itemId ?? string.Empty, false);
        ItemClass item = value != null ? value.ItemClass : null;
        return item != null ? item.GetLocalizedItemName() : (itemId ?? string.Empty);
    }
    private static string PolarityText(RebirthTraitDefinition t)
    {
        if (t == null) return string.Empty;
        if (t.Polarity == RebirthTraitPolarity.Positive) return "[8FD18F]POSITIVE[-]";
        if (t.Polarity == RebirthTraitPolarity.Negative) return "[CC6B64]NEGATIVE[-]";
        return "[D6C978]MIXED[-]";
    }
    private static string MoodIcon(float mood)
    {
        if (mood >= 80f) return "rb_condition_mood_excellent";
        if (mood >= 55f) return "rb_condition_mood_good";
        if (mood >= 30f) return "rb_condition_mood_low";
        return "rb_condition_mood_miserable";
    }
    private static string FormatCause(string id, float delta)
    {
        if (string.IsNullOrEmpty(id) || Math.Abs(delta) < 0.001f) return "[777777]—[-]";
        string label = string.Equals(id, "diet_satisfaction", StringComparison.OrdinalIgnoreCase) ? L("xuiRebirthConditionDietCause", "Diet Satisfaction") : id.Replace('_', ' ');
        return (delta > 0f ? "[8FD18F]+ " : "[CC6B64]− ") + label + "  " + Math.Abs(delta).ToString("0.0", CultureInfo.InvariantCulture) + "[-]";
    }
    private static string FormatDuration(float seconds)
    {
        if (seconds <= 0f) return "0m";
        int total = Mathf.CeilToInt(seconds);
        int h = total / 3600; int m = (total % 3600) / 60;
        return h > 0 ? h.ToString(CultureInfo.InvariantCulture) + "h " + m.ToString(CultureInfo.InvariantCulture) + "m" : Math.Max(1, m).ToString(CultureInfo.InvariantCulture) + "m";
    }
    private static string Signed(float v) { return (v >= 0f ? "+" : string.Empty) + v.ToString("0.00", CultureInfo.InvariantCulture); }
}
