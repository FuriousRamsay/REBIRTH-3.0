using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthSurvivorCreator : XUiController
{
    private static XUiC_RebirthSurvivorCreator activeNativeEditorOwner;

#if REBIRTH_DEBUG
    private static XUiC_RebirthSurvivorCreator activeDebugInstance;
#endif

    public const string WindowGroupId = "rebirthSurvivorCreator";

    // Only REBIRTH-owned entry points are allowed to open this window. The native Play Game
    // -> Player Profile route is deliberately untouched. This creator contains a ProfilesList
    // and SDCS preview for appearance association, but it must never instantiate a second
    // PlayerProfile parent controller. An unarmed creator open is closed only; REBIRTH never
    // tries to redirect or reopen the native playerProfiles window.
    private static int nextOpenRequestSerial;
    private static int pendingOpenRequestSerial;
    private static string pendingOpenRequestReason = string.Empty;
    private static RebirthSurvivorCreatorPurpose pendingOpenRequestPurpose;
    private static bool pendingOpenRequestArmed;

    private int activeOpenRequestSerial;
    private string activeOpenRequestReason = string.Empty;
    private bool unauthorizedOpenRedirectPending;
    private bool suspendedForNativePicker;
    // Opening the base-game customCharacterSystem closes the current window synchronously.
    // Keep the Survivor Creator session alive while that native child editor owns the screen.
    private bool suspendedForNativeChildEditor;
    private bool suspendedForProgressionExplorer;
    private bool resumeFromProgressionExplorerPending;
    private string progressionExplorerReturnToken = string.Empty;
    private Vector2i progressionExplorerDetailContentPosition;
    private XUiController progressionExplorerReturnFocus;
    private bool resumeFromNativeChildEditorPending;

    private const int ProfileRows = 6;
    private const int BackgroundRows = 5;
    private const int ProfileTrackHeight = 330;
    private const int NativeProfileVisibleRows = 7;
    private const int NativeProfileRowHeight = 60;
    private const int NativeProfileTrackHeight = 420;
    private const int BackgroundTrackHeight = 500;
    private const int BackgroundSkillCards = 12;
    private const int BackgroundKnowledgeRows = 3;
    private const int BackgroundKnowledgeTrackHeight = 90;
    private const int BackgroundStartingItemRows = 8;
    private const int BackgroundStartingItemTrackHeight = 132;
    private const int BackgroundWeaknessRows = 2;
    private const int TraitRows = 12; // legacy single-list page size retained for saved/test compatibility
    private const int TraitSideRows = 6;
    private const int TraitTrackHeight = 416;
    private const int TraitCategoryTabs = 6;
    private const int DietRows = 4;
    private const int DietFoodSlots = 98;
    private const int ReviewSkillRows = 10; // physical reusable review rows; logical progression list scrolls independently
    private const int ReviewKnowledgeRows = 27;
    private const int ReviewSummaryProgressionRows = 10;
    private const int ReviewProgressionTrackHeight = 290;
    private const int ReviewTraitRows = 11;
    private const int ReviewTraitTrackHeight = 319;
    private const int ReviewWeaknessRows = 2;
    private const int ReviewStartingItemRows = 8;
    private const int ReviewStartingItemTrackHeight = 224;

    private enum NativeProfileCreatePhase
    {
        None,
        AwaitTempEditor,
        EditingTemp,
        AwaitSaveName
    }

    private readonly XUiController[] profileRows = new XUiController[ProfileRows];
    private readonly XUiController[] profileButtons = new XUiController[ProfileRows];
    private readonly XUiV_Label[] profileNames = new XUiV_Label[ProfileRows];
    private readonly XUiV_Label[] profileSummaries = new XUiV_Label[ProfileRows];
    private readonly XUiV_Sprite[] profileSelections = new XUiV_Sprite[ProfileRows];
    private readonly Dictionary<XUiController, int> profileIndex = new Dictionary<XUiController, int>();
    private RebirthSurvivorProfile[] profiles = new RebirthSurvivorProfile[0];
    private int profileOffset;

    private readonly XUiController[] backgroundRows = new XUiController[BackgroundRows];
    private readonly XUiController[] backgroundButtons = new XUiController[BackgroundRows];
    private readonly XUiV_Label[] backgroundNames = new XUiV_Label[BackgroundRows];
    private readonly XUiV_Label[] backgroundSummaries = new XUiV_Label[BackgroundRows];
    private readonly XUiV_Sprite[] backgroundSelections = new XUiV_Sprite[BackgroundRows];
    private readonly XUiController[] backgroundRowArt = new XUiController[BackgroundRows];
    private readonly XUiController[] backgroundRowArtPlaceholder = new XUiController[BackgroundRows];
    private readonly RebirthSurvivorArtTextureBinder[] backgroundRowArtBinders = new RebirthSurvivorArtTextureBinder[BackgroundRows];
    private readonly Dictionary<XUiController, int> backgroundIndex = new Dictionary<XUiController, int>();

    // Visual Background summary. These are presentation-only projections of the authoritative
    // Background definitions; they do not invent a second "experience" state.
    private readonly XUiController[] backgroundSkillCards = new XUiController[BackgroundSkillCards];
    private readonly XUiV_Sprite[] backgroundSkillIcons = new XUiV_Sprite[BackgroundSkillCards];
    private readonly XUiV_Label[] backgroundSkillNames = new XUiV_Label[BackgroundSkillCards];
    private readonly XUiV_Label[] backgroundSkillValues = new XUiV_Label[BackgroundSkillCards];
    private readonly XUiController[] backgroundSkillExploreButtons = new XUiController[BackgroundSkillCards];
    private readonly string[] backgroundSkillExploreIds = new string[BackgroundSkillCards];
    private readonly Dictionary<XUiController,int> backgroundSkillExploreIndex = new Dictionary<XUiController,int>();
    private readonly XUiController[] backgroundKnowledgeRows = new XUiController[BackgroundKnowledgeRows];
    private readonly XUiV_Sprite[] backgroundKnowledgeIcons = new XUiV_Sprite[BackgroundKnowledgeRows];
    private readonly XUiV_Label[] backgroundKnowledgeNames = new XUiV_Label[BackgroundKnowledgeRows];
    private readonly XUiController[] backgroundKnowledgeExploreButtons = new XUiController[BackgroundKnowledgeRows];
    private readonly string[] backgroundKnowledgeExploreIds = new string[BackgroundKnowledgeRows];
    private readonly Dictionary<XUiController,int> backgroundKnowledgeExploreIndex = new Dictionary<XUiController,int>();
    private XUiController backgroundKnowledgeNativeScrollHost;
    private XUiController backgroundKnowledgeNativeScrollProxy;
    private XUiController backgroundKnowledgeNativeScrollView;
    private XUiController backgroundKnowledgeScrollCapture;
    private int backgroundKnowledgeOffset;
    private bool syncingBackgroundKnowledgeNativeScroll;
    private int backgroundKnowledgeNativeLayoutRefreshFrames;
    private readonly XUiController[] backgroundStartingItemRows = new XUiController[BackgroundStartingItemRows];
    private readonly XUiV_Sprite[] backgroundStartingItemIcons = new XUiV_Sprite[BackgroundStartingItemRows];
    private readonly XUiV_Label[] backgroundStartingItemNames = new XUiV_Label[BackgroundStartingItemRows];
    private readonly XUiV_Label[] backgroundStartingItemMeta = new XUiV_Label[BackgroundStartingItemRows];
    private XUiController backgroundStartingItemsNativeScrollHost;
    private XUiController backgroundStartingItemsNativeScrollProxy;
    private XUiController backgroundStartingItemsNativeScrollView;
    private XUiController backgroundStartingItemsScrollCapture;
    private int backgroundStartingItemsOffset;
    private bool syncingBackgroundStartingItemsNativeScroll;
    private int backgroundStartingItemsNativeLayoutRefreshFrames;
    private readonly XUiController[] backgroundWeaknessRows = new XUiController[BackgroundWeaknessRows];
    private readonly XUiV_Sprite[] backgroundWeaknessIcons = new XUiV_Sprite[BackgroundWeaknessRows];
    private readonly XUiV_Label[] backgroundWeaknessNames = new XUiV_Label[BackgroundWeaknessRows];
    private readonly XUiV_Label[] backgroundWeaknessValues = new XUiV_Label[BackgroundWeaknessRows];
    private readonly XUiController[] backgroundWeaknessExploreButtons = new XUiController[BackgroundWeaknessRows];
    private readonly string[] backgroundWeaknessExploreIds = new string[BackgroundWeaknessRows];
    private readonly Dictionary<XUiController,int> backgroundWeaknessExploreIndex = new Dictionary<XUiController,int>();

    private readonly XUiController[] traitRows = new XUiController[TraitRows];
    private readonly XUiController[] traitButtons = new XUiController[TraitRows];
    private readonly XUiV_Label[] traitCaptions = new XUiV_Label[TraitRows];
    private readonly XUiV_Sprite[] traitIcons = new XUiV_Sprite[TraitRows];
    private readonly Dictionary<XUiController, int> traitIndex = new Dictionary<XUiController, int>();

    // Approved dual-pane Traits creator. The old single-list members above are deliberately retained
    // only so older profile/test paths do not lose their serialized paging/filter contract.
    private readonly XUiController[] positiveTraitRows = new XUiController[TraitSideRows];
    private readonly XUiController[] positiveTraitButtons = new XUiController[TraitSideRows];
    private readonly XUiV_Sprite[] positiveTraitIcons = new XUiV_Sprite[TraitSideRows];
    private readonly XUiV_Sprite[] positiveTraitSelections = new XUiV_Sprite[TraitSideRows];
    private readonly XUiV_Label[] positiveTraitNames = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] positiveTraitSummaries = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] positiveTraitPoints = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] positiveTraitActions = new XUiV_Label[TraitSideRows];
    private readonly Dictionary<XUiController, int> positiveTraitIndex = new Dictionary<XUiController, int>();

    private readonly XUiController[] negativeTraitRows = new XUiController[TraitSideRows];
    private readonly XUiController[] negativeTraitButtons = new XUiController[TraitSideRows];
    private readonly XUiV_Sprite[] negativeTraitIcons = new XUiV_Sprite[TraitSideRows];
    private readonly XUiV_Sprite[] negativeTraitSelections = new XUiV_Sprite[TraitSideRows];
    private readonly XUiV_Label[] negativeTraitNames = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] negativeTraitSummaries = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] negativeTraitPoints = new XUiV_Label[TraitSideRows];
    private readonly XUiV_Label[] negativeTraitActions = new XUiV_Label[TraitSideRows];
    private readonly Dictionary<XUiController, int> negativeTraitIndex = new Dictionary<XUiController, int>();

    private readonly XUiController[] traitCategoryButtons = new XUiController[TraitCategoryTabs];
    private readonly Dictionary<XUiController, RebirthSurvivorTraitCategoryFilter> traitCategoryIndex = new Dictionary<XUiController, RebirthSurvivorTraitCategoryFilter>();

    private readonly XUiController[] dietRows = new XUiController[DietRows];
    private readonly XUiController[] dietButtons = new XUiController[DietRows];
    private readonly XUiV_Label[] dietNames = new XUiV_Label[DietRows];
    private readonly XUiV_Label[] dietSummaries = new XUiV_Label[DietRows];
    private readonly XUiV_Label[] dietPoints = new XUiV_Label[DietRows];
    private readonly XUiV_Sprite[] dietIcons = new XUiV_Sprite[DietRows];
    private readonly XUiV_Sprite[] dietSelections = new XUiV_Sprite[DietRows];
    private readonly Dictionary<XUiController, int> dietIndex = new Dictionary<XUiController, int>();
    private readonly XUiController[] dietFoodRows = new XUiController[DietFoodSlots];
    private readonly XUiV_Sprite[] dietFoodIcons = new XUiV_Sprite[DietFoodSlots];
    private readonly XUiV_Label[] dietFoodNames = new XUiV_Label[DietFoodSlots];
    private readonly XUiController[] dietFoodFilterButtons = new XUiController[5];
    private readonly Dictionary<XUiController, int> dietFoodFilterIndex = new Dictionary<XUiController, int>();
    private int dietFoodFilter;
    // The physical XML pool is deliberately reusable. Logical diet content can exceed it, so
    // keep a virtual offset instead of silently truncating entries beyond DietFoodSlots.
    private int dietFoodOffset;
    private int dietPagingCapacity = 4, dietPagingTotal;
    private XUiController dietPagingHost;
    private UIScrollView dietPagingView;
    private UIProgressBar dietPagingBar;
    private bool dietPagingRegistered, dietPagingSyncing;
    private float dietPagingBarValue;
    private float dietPagingHeight;
    private int dietPagingTextHeight;
    private int dietPagingAnalogDirection;
    private float dietPagingNextAnalog;
    private DietChromeWitness dietChromeWitness;
    private sealed class DietChromeWitness
    {
        private readonly XUiC_RebirthSurvivorCreator controller;
        private readonly XUi owner;
        private readonly LocalPlayerUI ui;
        private readonly GUIWindowManager manager;
        private readonly object model;
        private readonly XUiController host;
        private readonly XUiV_ScrollView source;
        private readonly UIScrollView view;
        private readonly UIProgressBar bar;
        internal readonly Func<bool> Admission;
        internal DietChromeWitness(XUiC_RebirthSurvivorCreator controller, XUi owner, LocalPlayerUI ui,
            GUIWindowManager manager, object model, XUiController host, XUiV_ScrollView source,
            UIScrollView view, UIProgressBar bar)
        {
            this.controller = controller; this.owner = owner; this.ui = ui; this.manager = manager;
            this.model = model; this.host = host; this.source = source; this.view = view; this.bar = bar;
            Admission = IsCurrent;
        }
        internal bool Matches(XUi owner, LocalPlayerUI ui, GUIWindowManager manager, object model,
            XUiController host, XUiV_ScrollView source, UIScrollView view, UIProgressBar bar) =>
            ReferenceEquals(this.owner, owner) && ReferenceEquals(this.ui, ui) && ReferenceEquals(this.manager, manager) &&
            ReferenceEquals(this.model, model) && ReferenceEquals(this.host, host) && ReferenceEquals(this.source, source) &&
            ReferenceEquals(this.view, view) && ReferenceEquals(this.bar, bar);
        private bool IdentityCurrent() =>
            RebirthScrollbarPagingPolicy.Enabled && controller.dietPagingRegistered &&
            ReferenceEquals(controller.xui, owner) && ReferenceEquals(owner.playerUI, ui) &&
            ReferenceEquals(ui.windowManager, manager) && ReferenceEquals(controller.model, model) &&
            ReferenceEquals(controller.dietPagingHost, host) && ReferenceEquals(host.xui, owner) &&
            ReferenceEquals(controller.dietDetailNativeScrollView?.ViewComponent, source) &&
            ReferenceEquals(source.Controller?.xui, owner) &&
            ReferenceEquals(source.Controller, controller.dietDetailNativeScrollView) &&
            ReferenceEquals(source.Controller.Parent, host) && ReferenceEquals(controller.dietPagingView, view) &&
            ReferenceEquals(source.scrollView, view) && ReferenceEquals(controller.dietPagingBar, bar) &&
            ReferenceEquals(view.verticalScrollBar, bar);
        private bool IsCurrent()
        {
            if (!IdentityCurrent() || controller.model.Step != RebirthSurvivorCreatorStep.Diet) return false;
            bool visible = manager.IsWindowOpen(WindowGroupId);
            for (var node = host; node != null && visible; node = node.Parent)
                if (node.ViewComponent != null && !node.ViewComponent.IsVisible) visible = false;
            return visible && IdentityCurrent() && controller.model.Step == RebirthSurvivorCreatorStep.Diet;
        }
    }
    private void PublishDietLogicalVisibility()
    {
        var owner = xui;
        var ui = owner?.playerUI;
        var manager = ui?.windowManager;
        var host = dietPagingHost;
        var source = dietDetailNativeScrollView?.ViewComponent as XUiV_ScrollView;
        var view = dietPagingView;
        var bar = dietPagingBar;
        var currentModel = model;
        if (!dietPagingRegistered || !RebirthScrollbarPagingPolicy.Enabled || manager == null ||
            host == null || source == null || view == null || bar == null || currentModel == null ||
            !ReferenceEquals(host.xui, owner) || !ReferenceEquals(source.Controller?.xui, owner) ||
            !ReferenceEquals(source.Controller, dietDetailNativeScrollView) ||
            !ReferenceEquals(source.Controller.Parent, host) || !ReferenceEquals(source.scrollView, view) ||
            !ReferenceEquals(view.verticalScrollBar, bar))
        { dietChromeWitness = null; return; }
        if (dietChromeWitness == null || !dietChromeWitness.Matches(owner, ui, manager, currentModel, host, source, view, bar))
            dietChromeWitness = new DietChromeWitness(this, owner, ui, manager, currentModel, host, source, view, bar);
        var witness = dietChromeWitness;
        bool overflow = witness.Admission() && dietPagingTotal > dietPagingCapacity;
        if (!ReferenceEquals(xui, owner) || !ReferenceEquals(owner.playerUI, ui) ||
            !ReferenceEquals(ui.windowManager, manager) || !ReferenceEquals(model, currentModel) ||
            !ReferenceEquals(dietPagingHost, host) || !ReferenceEquals(host.xui, owner) ||
            !ReferenceEquals(source.Controller?.xui, owner) ||
            !ReferenceEquals(dietDetailNativeScrollView?.ViewComponent, source) ||
            !ReferenceEquals(source.scrollView, view) || !ReferenceEquals(dietPagingView, view) ||
            !ReferenceEquals(dietPagingBar, bar) || !ReferenceEquals(view.verticalScrollBar, bar))
        { dietChromeWitness = null; return; }
        RebirthScrollbarPagingNativeAdapter.SetCustomLogicalScrollbarVisibility(host, overflow, witness.Admission);
    }
    private void PollDietGamepad()
    {
        var owner = xui;
        var ui = owner?.playerUI;
        var host = dietPagingHost;
        var source = dietDetailNativeScrollView?.ViewComponent as XUiV_ScrollView;
        if (!RebirthScrollbarPagingPolicy.Enabled || !DietPagingSafe || ui == null ||
            host?.ViewComponent == null || source == null || !ReferenceEquals(host.xui, owner) ||
            !ReferenceEquals(source.Controller?.xui, owner) || !ReferenceEquals(source.Controller, dietDetailNativeScrollView) ||
            !ReferenceEquals(source.scrollView, dietPagingView) || !ReferenceEquals(source.Controller.Parent, host) ||
            !XUiUtils.HotkeysAllowedFor(host.ViewComponent))
        { dietPagingAnalogDirection = 0; return; }
        var target = ui.CursorController?.CurrentTarget;
        if (target?.Controller == null || !target.Controller.IsSelfOrChildOf(host) || ui.playerInput == null)
        { dietPagingAnalogDirection = 0; return; }
        Vector2 axis = ui.playerInput.GUIActions.Camera.Vector;
        int direction = Math.Abs(axis.y) >= Math.Abs(axis.x) * 1.8f && Math.Abs(axis.y) > .1f
            ? (axis.y > 0f ? -1 : 1) : 0;
        if (direction == 0) { dietPagingAnalogDirection = 0; return; }
        float now = Time.unscaledTime;
        if (direction == dietPagingAnalogDirection && now < dietPagingNextAnalog) return;
        if (!RebirthScrollbarPagingPolicy.Enabled || !DietPagingSafe || !ReferenceEquals(xui, owner) || !ReferenceEquals(owner?.playerUI, ui) ||
            !ReferenceEquals(host.xui, owner) || !ReferenceEquals(source.Controller?.xui, owner) ||
            !ReferenceEquals(dietPagingHost, host) || !ReferenceEquals(dietDetailNativeScrollView?.ViewComponent, source) ||
            !ReferenceEquals(source.Controller, dietDetailNativeScrollView) || !ReferenceEquals(source.Controller.Parent, host) ||
            !ReferenceEquals(source.scrollView, dietPagingView) || !ReferenceEquals(ui.CursorController?.CurrentTarget, target) ||
            !target.Controller.IsSelfOrChildOf(host))
        { dietPagingAnalogDirection = 0; return; }
        dietPagingNextAnalog = now + (direction == dietPagingAnalogDirection ? .16f : .35f);
        dietPagingAnalogDirection = direction;
        DietFoodVirtualScroll(direction < 0 ? 1f : -1f);
    }
    private bool DietPagingSafe => xui != null && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() &&
        (xui.DragAndDropWindow == null || xui.DragAndDropWindow.IsEmpty());
    private void PollDietPaging()
    {
        if (!RebirthScrollbarPagingPolicy.Enabled) dietChromeWitness = null;
        if (!DietPagingSafe) { dietPagingAnalogDirection = 0; return; }
        bool enabled = RebirthScrollbarPagingPolicy.Enabled;
        UIScrollView view = (dietDetailNativeScrollView?.ViewComponent as XUiV_ScrollView)?.scrollView;
        UIProgressBar bar = view != null ? view.verticalScrollBar : null;
        if (!enabled)
        {
            if (dietPagingRegistered) { RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(dietPagingHost); dietPagingRegistered = false; RenderDietDetails(); }
            dietPagingView = null; dietPagingBar = null; dietPagingAnalogDirection = 0;
            return;
        }
        if (!dietPagingRegistered || !ReferenceEquals(view, dietPagingView) || !ReferenceEquals(bar, dietPagingBar))
        {
            dietChromeWitness = null;
            bool transition = !dietPagingRegistered || !ReferenceEquals(view, dietPagingView);
            RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(dietPagingHost, true);
            dietPagingRegistered = true; dietPagingView = view; dietPagingBar = bar;
            if (transition) RebirthScrollbarPagingNativeAdapter.ResetCustomViewport(dietPagingHost);
            RenderDietDetails();
        }
        RebirthScrollbarPagingNativeAdapter.Geometry currentGeometry;
        float height = RebirthScrollbarPagingNativeAdapter.GeometryFor(view, out currentGeometry) ? currentGeometry.Height : 522f;
        int textHeight = 0;
        for (int i = 0; i < dietFoodNames.Length; i++)
            if (dietFoodNames[i] != null) textHeight = Math.Max(textHeight, dietFoodNames[i].Size.y);
        if (height != dietPagingHeight || textHeight != dietPagingTextHeight)
        {
            dietPagingHeight = height; dietPagingTextHeight = textHeight;
            RenderDietDetails();
        }
        if (!dietPagingSyncing && bar != null && Math.Abs(bar.value - dietPagingBarValue) > .0001f)
        {
            dietFoodOffset = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(
                bar.value * Math.Max(0, dietPagingTotal - dietPagingCapacity),
                Math.Max(0, dietPagingTotal - dietPagingCapacity), dietPagingCapacity, true);
            RenderDietDetails();
        }
        PublishDietLogicalVisibility();
        PollDietGamepad();
    }
    private string dietFoodSearchText = string.Empty;

    private readonly XUiController[] reviewSkillRows = new XUiController[ReviewSkillRows];
    private readonly XUiV_Sprite[] reviewSkillIcons = new XUiV_Sprite[ReviewSkillRows];
    private readonly XUiV_Label[] reviewSkillNames = new XUiV_Label[ReviewSkillRows];
    private readonly XUiV_Label[] reviewSkillValues = new XUiV_Label[ReviewSkillRows];
    private readonly XUiController[] reviewSkillExploreButtons = new XUiController[ReviewSkillRows];
    private readonly string[] reviewSkillExploreIds = new string[ReviewSkillRows];
    private readonly Dictionary<XUiController,int> reviewSkillExploreIndex = new Dictionary<XUiController,int>();
    private readonly XUiController[] reviewKnowledgeRows = new XUiController[ReviewKnowledgeRows];
    private readonly XUiV_Sprite[] reviewKnowledgeIcons = new XUiV_Sprite[ReviewKnowledgeRows];
    private readonly XUiV_Label[] reviewKnowledgeNames = new XUiV_Label[ReviewKnowledgeRows];
    private readonly XUiV_Label[] reviewKnowledgeValues = new XUiV_Label[ReviewKnowledgeRows];
    private readonly XUiController[] reviewKnowledgeExploreButtons = new XUiController[ReviewKnowledgeRows];
    private readonly string[] reviewKnowledgeExploreIds = new string[ReviewKnowledgeRows];
    private readonly Dictionary<XUiController,int> reviewKnowledgeExploreIndex = new Dictionary<XUiController,int>();
    private readonly XUiController[] reviewTraitRows = new XUiController[ReviewTraitRows];
    private readonly XUiV_Sprite[] reviewTraitIcons = new XUiV_Sprite[ReviewTraitRows];
    private readonly XUiV_Label[] reviewTraitNames = new XUiV_Label[ReviewTraitRows];
    private readonly XUiV_Label[] reviewTraitValues = new XUiV_Label[ReviewTraitRows];
    private readonly XUiController[] reviewWeaknessRows = new XUiController[ReviewWeaknessRows];
    private readonly XUiV_Sprite[] reviewWeaknessIcons = new XUiV_Sprite[ReviewWeaknessRows];
    private readonly XUiV_Label[] reviewWeaknessNames = new XUiV_Label[ReviewWeaknessRows];
    private readonly XUiV_Label[] reviewWeaknessValues = new XUiV_Label[ReviewWeaknessRows];
    private readonly XUiController[] reviewStartingItemRows = new XUiController[ReviewStartingItemRows];
    private readonly XUiV_Sprite[] reviewStartingItemIcons = new XUiV_Sprite[ReviewStartingItemRows];
    private readonly XUiV_Label[] reviewStartingItemNames = new XUiV_Label[ReviewStartingItemRows];
    private readonly XUiV_Label[] reviewStartingItemMeta = new XUiV_Label[ReviewStartingItemRows];
    private readonly bool[] reviewSkillExploreIsKnowledge = new bool[ReviewSkillRows];
    private XUiController reviewProgressionNativeScrollHost, reviewProgressionNativeScrollProxy, reviewProgressionNativeScrollView, reviewProgressionScrollCapture;
    private XUiController reviewTraitNativeScrollHost, reviewTraitNativeScrollProxy, reviewTraitNativeScrollView, reviewTraitScrollCapture;
    private XUiController reviewStartingItemsNativeScrollHost, reviewStartingItemsNativeScrollProxy, reviewStartingItemsNativeScrollView, reviewStartingItemsScrollCapture;
    private int reviewProgressionOffset, reviewTraitOffset, reviewStartingItemsOffset;
    private int reviewProgressionTotal, reviewTraitTotal, reviewStartingItemsTotal;
    private bool syncingReviewNativeScroll;
    private int reviewNativeLayoutRefreshFrames;

    private RebirthSurvivorCreatorViewModel model;
    private Action<RebirthSurvivorProfile> onLocalProfileSaved;
    private Action<RebirthSurvivorCreatorViewModel> onExternalConfirmed;
    private Action onExternalCancelled;
    private bool returnToProfileManagerAfterLocalSession = true;
    private string transientStatus = string.Empty;
    private string focusedTraitId = string.Empty;

    private XUiC_TextInput profileNameInput;
    private XUiController profilePanel;
    private XUiController backgroundPanel;
    private XUiController traitPanel;
    private XUiController dietPanel;
    private XUiController reviewPanel;
    private XUiController detailsPanel;
    private XUiController backgroundVisualDetailsPanel;
    private XUiController creatorDetailScroll;
    private XUiController dietDetailsPanel;
    private XUiController dietAllowedFoodsHeading;
    private XUiController dietRestrictedFoodsHeading;
    private XUiController dietFoodLegend;
    private XUiController dietDetailScrollContent;
    private XUiController dietSummaryScrollContent;
    private XUiController dietRuleHeading;
    private XUiController dietEffectsPanel,dietBenefitsPanel,dietChallengesPanel,dietLegendPanel;
    private XUiController dietDetailNativeScrollView,dietSummaryNativeScrollView;
    private int dietNativeLayoutRefreshFrames;
    private XUiC_TextInput dietFoodSearchInput;
    private XUiV_Label dietSelectedName;
    private XUiV_Label dietSelectedSummary;
    private XUiV_Label dietPointBonus;
    private XUiV_Label dietEffectsText;
    private XUiV_Label dietBenefitsText;
    private XUiV_Label dietChallengesText;
    private XUiController reviewVisualDetailsPanel;
    private XUiController reviewVisualContent;
    private XUiController reviewNoSkillsText;
    private XUiController reviewNoKnowledgeText;
    private XUiController reviewBackgroundArt;
    private XUiController reviewBackgroundArtPlaceholder;
    private RebirthSurvivorArtTextureBinder reviewBackgroundArtBinder;
    private XUiV_Label reviewSummaryDescription;
    private XUiV_Label reviewSummaryPlayerProfileMeta;
    private string reviewPreviewPlayerProfileName = string.Empty;
    private XUiController backgroundArtTexture;
    private XUiController backgroundArtPlaceholder;
    private RebirthSurvivorArtTextureBinder artBinder;

    private XUiV_Label creatorTitle;
    private XUiV_Label stepTitle;
    private XUiV_Label detailText;
    private XUiV_Label validationText;
    private XUiV_Label backgroundPageText;
    private XUiV_Label traitPageText;
    private XUiV_Label dietPageText;
    private XUiV_Label dietDetailText;
    private XUiV_Label traitFilterText;
    private XUiV_Label traitBudgetText;
    private XUiV_Label positiveTraitPageText;
    private XUiV_Label negativeTraitPageText;
    private XUiV_Label traitAvailablePoints;
    private XUiV_Label traitSelectedHeading;
    private XUiV_Label traitSelectedText;
    private XUiController traitSelectedNativeScrollProxy;
    private XUiV_Label traitFocusedDetails;
    private XUiV_Sprite traitFocusedIcon;
    private XUiController btnResetTraits;
    private XUiController positiveTraitScrollTrack;
    private XUiController positiveTraitScrollThumbControl;
    private XUiV_Button positiveTraitScrollThumb;
    private XUiController negativeTraitScrollTrack;
    private XUiController negativeTraitScrollThumbControl;
    private XUiV_Button negativeTraitScrollThumb;
    private XUiController positiveTraitNativeScrollHost, positiveTraitNativeScrollProxy, positiveTraitNativeScrollView;
    private XUiController negativeTraitNativeScrollHost, negativeTraitNativeScrollProxy, negativeTraitNativeScrollView;
    private bool syncingTraitNativeScroll;
    private XUiV_Label reviewText;
    private XUiV_Label statusText;
    private XUiV_Label backgroundArtAlt;
    private XUiV_Label backgroundVisualName;
    private XUiV_Label backgroundVisualDescription;
    private XUiV_Sprite backgroundSignatureBonusIcon;
    private XUiV_Label backgroundSignatureBonusName;
    private XUiV_Label backgroundSignatureBonusDescription;
    private XUiV_Label backgroundNoSkillsText;
    private XUiV_Label backgroundNoKnowledgeText;
    private XUiV_Label backgroundNoWeaknessText;
    private XUiV_Label backgroundTraitPointsValue;
    private XUiV_Label reviewProfileName;
    private XUiV_Label reviewBackgroundName;
    private XUiV_Sprite reviewSignatureBonusIcon;
    private XUiV_Label reviewSignatureBonusName;
    private XUiV_Label reviewSignatureBonusDescription;
    private XUiV_Label reviewDietName;
    private XUiController profileNameLabel;
    private XUiController profileScrollTrack;
    private XUiController profileNativeScrollHost,profileNativeScrollProxy,profileNativeScrollView;
    private XUiV_Sprite profileScrollChrome,profileScrollChromeInner;
    private XUiController profileScrollThumbControl;
    private XUiV_Button profileScrollThumb;
    private readonly XUiController[] nativeProfileRows = new XUiController[NativeProfileVisibleRows];
    private readonly XUiController[] nativeProfileButtons = new XUiController[NativeProfileVisibleRows];
    private readonly XUiV_Label[] nativeProfileNames = new XUiV_Label[NativeProfileVisibleRows];
    private readonly XUiV_Sprite[] nativeProfileSelections = new XUiV_Sprite[NativeProfileVisibleRows];
    private readonly XUiV_Sprite[] nativeProfileChecks = new XUiV_Sprite[NativeProfileVisibleRows];
    private readonly RebirthPlayerProfilePortraitBinder[] nativeProfilePortraits = new RebirthPlayerProfilePortraitBinder[NativeProfileVisibleRows];
    private readonly Dictionary<XUiController, int> nativeProfileButtonIndex = new Dictionary<XUiController, int>();
    private List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> nativeProfileSnapshot = new List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo>();
    private int nativeProfileScrollOffset;
    private int nativeProfileCount;
    private string nativeProfileLastSelection = string.Empty;
    // The base game's globally active Player Profile is only borrowed while this editor is open.
    // Survivor draft selection is independent and the global choice is restored on close.
    private string nativeGlobalProfileNameAtOpen = string.Empty;
    private string nativeGlobalProfileSourceAtOpen = string.Empty;
    private float nativeProfileCountProbeAt;
    private bool nativeProfileModalWasOpen;
    private bool nativePortraitPreloadStarted;
    private bool nativePortraitPreloadComplete;
    private int nativePortraitPreloadCursor;
    private string nativePortraitPreloadRestoreName = string.Empty;
    private XUiController profilePortraitPreloadMask;
    private string pendingSelectedPortraitName = string.Empty;
    private int pendingSelectedPortraitReadyFrame = -1;
    private readonly HashSet<string> nativePortraitFailureLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private XUiController playerPreviewBackgroundTexture;
    private RebirthSurvivorArtTextureBinder playerPreviewBackgroundBinder;
    private XUiController backgroundScrollTrack;
    private XUiController backgroundNativeScrollHost,backgroundNativeScrollProxy,backgroundNativeScrollView;
    private bool syncingCreatorNativeScroll;
    private XUiV_Sprite backgroundScrollChrome,backgroundScrollChromeInner;
    private XUiController backgroundScrollThumbControl;
    private XUiV_Button backgroundScrollThumb;
    private XUiController profileScrollPageUp,profileScrollPageDown,backgroundScrollPageUp,backgroundScrollPageDown;
    private bool profileThumbDragging,backgroundThumbDragging;
    private float profileThumbDragY,backgroundThumbDragY;
    private bool wasCursorHidden;

    private XUiController btnStepProfile;
    private XUiController btnStepBackground;
    private XUiController btnStepTraits;
    private XUiController btnStepDiet;
    private XUiController btnStepReview;
    private XUiController tabDimProfile;
    private XUiController tabDimBackground;
    private XUiController tabDimTraits;
    private XUiController tabDimDiet;
    private XUiController tabDimReview;
    private XUiController btnPrevious;
    private XUiController btnNext;
    private XUiController btnSave;
    private XUiController btnCancel;
    private XUiController btnSaveReusableProfile;
    private XUiController btnRebirthProfileCreate;
    private XUiController playerProfileSaveOverlay;
    private XUiC_TextInput newPlayerProfileNameInput;
    private XUiV_Label newPlayerProfileNameStatus;
    private XUiController btnSaveNewPlayerProfile;
    private XUiController btnDiscardNewPlayerProfile;
    private NativeProfileCreatePhase nativeProfileCreatePhase;
    private string nativeTemporaryProfileName = string.Empty;
    private string nativeOriginalProfileName = string.Empty;
    private string nativeFinalProfileName = string.Empty;
    private Archetype nativeTemporaryAppearance;
    private int nativeEditorDecision;
    private float nativeProfileCreateStageDeadline;
    private XUiController nativeEditorApplyButton;
    private XUiController nativeEditorBackButton;
    private XUiController rebirthEditorApplyButton;
    private XUiController rebirthEditorBackButton;
    private XUiController nativeProfileEditButton;
    private XUiController nativeProfileDeleteButton;
    private string nativeModifyStateProfileName = string.Empty;
    private bool nativeModifyState;
    private string pendingIntentionalDeleteName = string.Empty;
    private float pendingIntentionalDeleteDeadline;
    private XUiController playerProfileDeleteOverlay;
    private XUiV_Label playerProfileDeleteText;
    private XUiController btnConfirmDeletePlayerProfile;
    private XUiController btnCancelDeletePlayerProfile;
    private bool nativeExistingProfileEditActive;
    private bool nativeExistingProfileEditorOpened;
    private string nativeExistingProfileEditName = string.Empty;
    private string pendingSilentTempCleanupName = string.Empty;
    private float pendingSilentTempCleanupDeadline;

    public override void Init()
    {
        base.Init();
        profileNameInput = GetChildById("survivorProfileName") as XUiC_TextInput;
        if (profileNameInput != null) profileNameInput.OnChangeHandler += ProfileName_OnChanged;
        newPlayerProfileNameInput = GetChildById("newPlayerProfileName") as XUiC_TextInput;
        if (newPlayerProfileNameInput != null) newPlayerProfileNameInput.OnChangeHandler += NewPlayerProfileName_OnChanged;
        playerProfileSaveOverlay = GetChildById("playerProfileSaveOverlay");
        newPlayerProfileNameStatus = Label("newPlayerProfileNameStatus");
        profilePortraitPreloadMask = GetChildById("profilePortraitPreloadMask");
        nativeProfileEditButton = GetChildById("btnProfileEdit");
        nativeProfileDeleteButton = GetChildById("btnProfileDelete");
        if (nativeProfileEditButton != null) nativeProfileEditButton.OnPress += NativeProfileEditObserved_OnPressed;
        if (nativeProfileDeleteButton != null) nativeProfileDeleteButton.OnPress += NativeProfileDeleteObserved_OnPressed;
        XUiController nativeProfileHost = GetChildById("nativePlayerProfileHost");
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR-PROFILE-HOST type=" +
            (nativeProfileHost != null ? nativeProfileHost.GetType().FullName : "<null>") +
            " ownsPlayerProfileController=False"); }
        playerProfileDeleteOverlay = GetChildById("playerProfileDeleteOverlay");
        playerProfileDeleteText = Label("playerProfileDeleteText");
        btnConfirmDeletePlayerProfile = Wire("btnConfirmDeletePlayerProfile", ConfirmPlayerProfileDelete_OnPressed);
        btnCancelDeletePlayerProfile = Wire("btnCancelDeletePlayerProfile", CancelPlayerProfileDelete_OnPressed);

        profilePanel = GetChildById("creatorProfilePanel");
        backgroundPanel = GetChildById("creatorBackgroundPanel");
        traitPanel = GetChildById("creatorTraitPanel");
        dietPanel = GetChildById("creatorDietPanel");
        reviewPanel = GetChildById("creatorReviewPanel");
        detailsPanel = GetChildById("creatorDetailsPanel");
        backgroundVisualDetailsPanel = GetChildById("backgroundVisualDetailsPanel");
        creatorDetailScroll = GetChildById("creatorDetailScroll");
        dietDetailsPanel = GetChildById("creatorDietDetailsPanel");
        reviewVisualDetailsPanel = GetChildById("creatorReviewDetailsPanel");
        reviewVisualContent = GetChildById("creatorReviewDetailsContent");
        reviewNoSkillsText = GetChildById("reviewNoSkillsText");
        reviewNoKnowledgeText = GetChildById("reviewNoKnowledgeText");
        reviewBackgroundArt = GetChildById("reviewBackgroundArt");
        reviewBackgroundArtPlaceholder = GetChildById("reviewBackgroundArtPlaceholder");
        reviewBackgroundArtBinder = new RebirthSurvivorArtTextureBinder(reviewBackgroundArt, 1f);
        reviewSummaryDescription = Label("reviewSummaryDescription");
        reviewSummaryPlayerProfileMeta = Label("reviewSummaryPlayerProfileMeta");
        backgroundArtTexture = GetChildById("backgroundArtTexture");
        backgroundArtPlaceholder = GetChildById("backgroundArtPlaceholder");
        artBinder = new RebirthSurvivorArtTextureBinder(backgroundArtTexture, 530f / 178f); // 1672x560 master shown at ~3:1
        playerPreviewBackgroundTexture = GetChildById("playerPreviewBackground");
        playerPreviewBackgroundBinder = new RebirthSurvivorArtTextureBinder(playerPreviewBackgroundTexture, 928f / 548f);
        if (playerPreviewBackgroundBinder != null)
            playerPreviewBackgroundBinder.BindLocalSurvivorAsset("rb_survivor_player_profile_preview_bg");

        creatorTitle = Label("creatorTitle");
        stepTitle = Label("creatorStepTitle");
        detailText = Label("creatorDetailText");
        validationText = Label("creatorValidationText");
        backgroundPageText = Label("backgroundPageText");
        traitPageText = Label("traitPageText");
        dietPageText = Label("dietPageText");
        dietDetailText = Label("creatorDietDetailText");
        dietPagingHost = GetChildById("creatorDietDetailScroll");
        dietAllowedFoodsHeading = GetChildById("dietAllowedFoodsHeading");
        dietRestrictedFoodsHeading = GetChildById("dietRestrictedFoodsHeading");
        dietFoodLegend = GetChildById("dietFoodLegend");
        dietDetailScrollContent = GetChildById("creatorDietDetailScrollContent");
        if (dietDetailScrollContent != null) WireScroll(dietDetailScrollContent, DietFoodVirtualScroll);
        dietSummaryScrollContent = GetChildById("dietSummaryScrollContent");
        dietRuleHeading = GetChildById("dietRuleHeading");
        dietEffectsPanel = GetChildById("dietEffectsPanel");dietBenefitsPanel = GetChildById("dietBenefitsPanel");dietChallengesPanel = GetChildById("dietChallengesPanel");dietLegendPanel = GetChildById("dietLegendPanel");
        dietDetailNativeScrollView=GetChildById("creatorDietDetailNativeScrollView");dietSummaryNativeScrollView=GetChildById("dietSummaryNativeScrollView");
        dietSelectedName = Label("dietSelectedName");
        dietSelectedSummary = Label("dietSelectedSummary");
        dietPointBonus = Label("dietPointBonus");
        dietEffectsText = Label("dietEffectsText");
        dietBenefitsText = Label("dietBenefitsText");
        dietChallengesText = Label("dietChallengesText");
        dietFoodSearchInput = GetChildById("dietFoodSearchInput") as XUiC_TextInput;
        if (dietFoodSearchInput != null) dietFoodSearchInput.OnChangeHandler += DietFoodSearch_OnChanged;
        for (int filter = 0; filter < dietFoodFilterButtons.Length; filter++)
        {
            XUiController button = GetChildById("btnDietFoodFilter" + filter.ToString(CultureInfo.InvariantCulture));
            dietFoodFilterButtons[filter] = button;
            if (button != null)
            {
                dietFoodFilterIndex[button] = filter;
                button.OnPress += DietFoodFilter_OnPressed;
            }
        }
        traitFilterText = Label("traitFilterText");
        traitBudgetText = Label("traitBudgetText");
        positiveTraitPageText = Label("positiveTraitPageText");
        negativeTraitPageText = Label("negativeTraitPageText");
        traitAvailablePoints = Label("traitAvailablePoints");
        traitSelectedHeading = Label("traitSelectedHeading");
        traitSelectedText = Label("traitSelectedText");
        traitSelectedNativeScrollProxy = GetChildById("traitSelectedNativeScrollProxy");
        traitFocusedDetails = Label("traitFocusedDetails");
        traitFocusedIcon = Sprite("traitFocusedIcon");
        reviewText = Label("creatorReviewText");
        statusText = Label("creatorStatusText");
        backgroundArtAlt = Label("backgroundArtAlt");
        backgroundVisualName = Label("backgroundVisualName");
        backgroundVisualDescription = Label("backgroundVisualDescription");
        backgroundSignatureBonusIcon = Sprite("backgroundSignatureBonusIcon");
        backgroundSignatureBonusName = Label("backgroundSignatureBonusName");
        backgroundSignatureBonusDescription = Label("backgroundSignatureBonusDescription");
        backgroundNoSkillsText = Label("backgroundNoSkillsText");
        backgroundNoKnowledgeText = Label("backgroundNoKnowledgeText");
        backgroundNoWeaknessText = Label("backgroundNoWeaknessText");
        backgroundTraitPointsValue = Label("backgroundTraitPointsValue");
        reviewProfileName = Label("reviewProfileName");
        reviewBackgroundName = Label("reviewBackgroundName");
        reviewSignatureBonusIcon = Sprite("reviewSignatureBonusIcon");
        reviewSignatureBonusName = Label("reviewSignatureBonusName");
        reviewSignatureBonusDescription = Label("reviewSignatureBonusDescription");
        reviewDietName = Label("reviewDietName");
        profileNameLabel = GetChildById("creatorProfileNameLabel");

        btnStepProfile = Wire("btnStepProfile", new Action(delegate { GoToStep(RebirthSurvivorCreatorStep.Profile); }));
        btnStepBackground = Wire("btnStepBackground", new Action(delegate { GoToStep(RebirthSurvivorCreatorStep.Background); }));
        btnStepTraits = Wire("btnStepTraits", new Action(delegate { GoToStep(RebirthSurvivorCreatorStep.Traits); }));
        btnStepDiet = Wire("btnStepDiet", new Action(delegate { GoToStep(RebirthSurvivorCreatorStep.Diet); }));
        btnStepReview = Wire("btnStepReview", new Action(delegate { GoToStep(RebirthSurvivorCreatorStep.Review); }));
        tabDimProfile = GetChildById("tabDimProfile");
        tabDimBackground = GetChildById("tabDimBackground");
        tabDimTraits = GetChildById("tabDimTraits");
        tabDimDiet = GetChildById("tabDimDiet");
        tabDimReview = GetChildById("tabDimReview");

        btnSaveReusableProfile = Wire("btnSaveReusableProfile", SaveReusableProfile_OnPressed);
        btnRebirthProfileCreate = Wire("btnRebirthProfileCreate", NativePlayerProfileCreate_OnPressed);
        btnSaveNewPlayerProfile = Wire("btnSaveNewPlayerProfile", SaveNewPlayerProfile_OnPressed);
        btnDiscardNewPlayerProfile = Wire("btnDiscardNewPlayerProfile", DiscardNewPlayerProfile_OnPressed);
        Wire("btnTraitPrev", TraitPrev_OnPressed);
        Wire("btnTraitNext", TraitNext_OnPressed);
        Wire("btnTraitFilterPrev", TraitFilterPrev_OnPressed);
        Wire("btnTraitFilterNext", TraitFilterNext_OnPressed);
        Wire("btnRemoveUnavailableTraits", RemoveUnavailableTraits_OnPressed);
        btnResetTraits = Wire("btnResetTraits", ResetTraits_OnPressed);
        positiveTraitScrollTrack = GetChildById("positiveTraitScrollTrackInput");
        positiveTraitScrollThumbControl = GetChildById("positiveTraitScrollThumb");
        positiveTraitScrollThumb = positiveTraitScrollThumbControl != null ? positiveTraitScrollThumbControl.ViewComponent as XUiV_Button : null;
        negativeTraitScrollTrack = GetChildById("negativeTraitScrollTrackInput");
        negativeTraitScrollThumbControl = GetChildById("negativeTraitScrollThumb");
        negativeTraitScrollThumb = negativeTraitScrollThumbControl != null ? negativeTraitScrollThumbControl.ViewComponent as XUiV_Button : null;
        positiveTraitNativeScrollHost = GetChildById("positiveTraitNativeScrollHost");
        positiveTraitNativeScrollProxy = GetChildById("positiveTraitNativeScrollProxy");
        positiveTraitNativeScrollView = GetChildById("positiveTraitNativeScrollView");
        negativeTraitNativeScrollHost = GetChildById("negativeTraitNativeScrollHost");
        negativeTraitNativeScrollProxy = GetChildById("negativeTraitNativeScrollProxy");
        negativeTraitNativeScrollView = GetChildById("negativeTraitNativeScrollView");
        WireScroll(positiveTraitNativeScrollHost, PositiveTraitScroll);
        WireScroll(positiveTraitNativeScrollView, PositiveTraitScroll);
        WireScroll(positiveTraitNativeScrollProxy, PositiveTraitScroll);
        WireScroll(negativeTraitNativeScrollHost, NegativeTraitScroll);
        WireScroll(negativeTraitNativeScrollView, NegativeTraitScroll);
        WireScroll(negativeTraitNativeScrollProxy, NegativeTraitScroll);
        WireScroll(positiveTraitScrollTrack, PositiveTraitScroll);
        WireScroll(positiveTraitScrollThumbControl, PositiveTraitScroll);
        WireThumbDrag(positiveTraitScrollThumbControl, PositiveTraitThumbDrag);
        WireScroll(negativeTraitScrollTrack, NegativeTraitScroll);
        WireScroll(negativeTraitScrollThumbControl, NegativeTraitScroll);
        WireThumbDrag(negativeTraitScrollThumbControl, NegativeTraitThumbDrag);
        WireTraitCategory("btnTraitCategoryAll", RebirthSurvivorTraitCategoryFilter.All, 0);
        WireTraitCategory("btnTraitCategoryPhysical", RebirthSurvivorTraitCategoryFilter.Physical, 1);
        WireTraitCategory("btnTraitCategoryMental", RebirthSurvivorTraitCategoryFilter.Mental, 2);
        WireTraitCategory("btnTraitCategorySocial", RebirthSurvivorTraitCategoryFilter.Social, 3);
        WireTraitCategory("btnTraitCategoryLifestyle", RebirthSurvivorTraitCategoryFilter.Lifestyle, 4);
        WireTraitCategory("btnTraitCategoryAptitudes", RebirthSurvivorTraitCategoryFilter.Aptitudes, 5);

        btnPrevious = Wire("btnCreatorPrevious", Previous_OnPressed);
        btnNext = Wire("btnCreatorNext", Next_OnPressed);
        btnSave = Wire("btnCreatorSave", Save_OnPressed);
        btnCancel = Wire("btnCreatorCancel", Cancel_OnPressed);

        for (int i = 0; i < ProfileRows; i++)
        {
            profileRows[i] = GetChildById("profileRow" + i.ToString(CultureInfo.InvariantCulture));
            profileButtons[i] = GetChildById("btnProfile" + i.ToString(CultureInfo.InvariantCulture));
            profileNames[i] = Label("profileName" + i.ToString(CultureInfo.InvariantCulture));
            profileSummaries[i] = Label("profileSummary" + i.ToString(CultureInfo.InvariantCulture));
            profileSelections[i] = Sprite("profileSelection" + i.ToString(CultureInfo.InvariantCulture));
            if (profileButtons[i] != null)
            {
                profileIndex[profileButtons[i]] = i;
                profileButtons[i].OnPress += ProfileRow_OnPressed;
            }
            WireScroll(profileRows[i], ProfileScroll);
            WireScroll(profileButtons[i], ProfileScroll);
            WireScroll(profileNames[i] != null ? profileNames[i].Controller : null, ProfileScroll);
            WireScroll(profileSummaries[i] != null ? profileSummaries[i].Controller : null, ProfileScroll);
        }

        for (int i = 0; i < BackgroundRows; i++)
        {
            backgroundRows[i] = GetChildById("backgroundRow" + i.ToString(CultureInfo.InvariantCulture));
            backgroundButtons[i] = GetChildById("btnBackground" + i.ToString(CultureInfo.InvariantCulture));
            backgroundNames[i] = Label("backgroundName" + i.ToString(CultureInfo.InvariantCulture));
            backgroundSummaries[i] = Label("backgroundSummary" + i.ToString(CultureInfo.InvariantCulture));
            backgroundSelections[i] = Sprite("backgroundSelection" + i.ToString(CultureInfo.InvariantCulture));
            backgroundRowArt[i] = GetChildById("backgroundRowArt" + i.ToString(CultureInfo.InvariantCulture));
            backgroundRowArtPlaceholder[i] = GetChildById("backgroundRowArtPlaceholder" + i.ToString(CultureInfo.InvariantCulture));
            backgroundRowArtBinders[i] = new RebirthSurvivorArtTextureBinder(backgroundRowArt[i], 16f / 9f);
            if (backgroundButtons[i] != null)
            {
                backgroundIndex[backgroundButtons[i]] = i;
                backgroundButtons[i].OnPress += BackgroundRow_OnPressed;
            }
            WireScroll(backgroundRows[i], BackgroundScroll);
            WireScroll(backgroundButtons[i], BackgroundScroll);
            WireScroll(backgroundNames[i] != null ? backgroundNames[i].Controller : null, BackgroundScroll);
            WireScroll(backgroundSummaries[i] != null ? backgroundSummaries[i].Controller : null, BackgroundScroll);
            WireScroll(backgroundRowArt[i], BackgroundScroll);
            WireScroll(backgroundRowArtPlaceholder[i], BackgroundScroll);
        }
        for (int i = 0; i < BackgroundSkillCards; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            backgroundSkillCards[i] = GetChildById("backgroundSkillCard" + suffix);
            backgroundSkillIcons[i] = Sprite("backgroundSkillIcon" + suffix);
            backgroundSkillNames[i] = Label("backgroundSkillName" + suffix);
            backgroundSkillValues[i] = Label("backgroundSkillValue" + suffix);
            backgroundSkillExploreButtons[i] = GetChildById("btnBackgroundSkillExplore" + suffix);
            if(backgroundSkillExploreButtons[i]!=null){backgroundSkillExploreIndex[backgroundSkillExploreButtons[i]]=i;backgroundSkillExploreButtons[i].OnPress+=ProgressionSkillExplore_OnPressed;}
        }
        for (int i = 0; i < BackgroundKnowledgeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            backgroundKnowledgeRows[i] = GetChildById("backgroundKnowledgeRow" + suffix);
            backgroundKnowledgeIcons[i] = Sprite("backgroundKnowledgeIcon" + suffix);
            backgroundKnowledgeNames[i] = Label("backgroundKnowledgeName" + suffix);
            backgroundKnowledgeExploreButtons[i] = GetChildById("btnBackgroundKnowledgeExplore" + suffix);
            if(backgroundKnowledgeExploreButtons[i]!=null){backgroundKnowledgeExploreIndex[backgroundKnowledgeExploreButtons[i]]=i;backgroundKnowledgeExploreButtons[i].OnPress+=ProgressionKnowledgeExplore_OnPressed;}
            WireScroll(backgroundKnowledgeRows[i], BackgroundKnowledgeScroll);
            WireScroll(backgroundKnowledgeExploreButtons[i], BackgroundKnowledgeScroll);
        }
        backgroundKnowledgeNativeScrollHost = GetChildById("backgroundKnowledgeNativeScrollHost");
        backgroundKnowledgeNativeScrollProxy = GetChildById("backgroundKnowledgeNativeScrollProxy");
        backgroundKnowledgeNativeScrollView = GetChildById("backgroundKnowledgeNativeScrollView");
        backgroundKnowledgeScrollCapture = GetChildById("backgroundKnowledgeScrollCapture");
        WireScroll(backgroundKnowledgeNativeScrollHost, BackgroundKnowledgeScroll);
        WireScroll(backgroundKnowledgeNativeScrollView, BackgroundKnowledgeScroll);
        WireScroll(backgroundKnowledgeNativeScrollProxy, BackgroundKnowledgeScroll);
        WireScroll(backgroundKnowledgeScrollCapture, BackgroundKnowledgeScroll);
        for (int i = 0; i < BackgroundStartingItemRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            backgroundStartingItemRows[i] = GetChildById("backgroundStartingItemRow" + suffix);
            backgroundStartingItemIcons[i] = Sprite("backgroundStartingItemIcon" + suffix);
            backgroundStartingItemNames[i] = Label("backgroundStartingItemName" + suffix);
            backgroundStartingItemMeta[i] = Label("backgroundStartingItemMeta" + suffix);
            WireScroll(backgroundStartingItemRows[i], BackgroundStartingItemsScroll);
        }
        backgroundStartingItemsNativeScrollHost = GetChildById("backgroundStartingItemsNativeScrollHost");
        backgroundStartingItemsNativeScrollProxy = GetChildById("backgroundStartingItemsNativeScrollProxy");
        backgroundStartingItemsNativeScrollView = GetChildById("backgroundStartingItemsNativeScrollView");
        backgroundStartingItemsScrollCapture = GetChildById("backgroundStartingItemsScrollCapture");
        WireScroll(backgroundStartingItemsNativeScrollHost, BackgroundStartingItemsScroll);
        WireScroll(backgroundStartingItemsScrollCapture, BackgroundStartingItemsScroll);

        for (int i = 0; i < BackgroundWeaknessRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            backgroundWeaknessRows[i] = GetChildById("backgroundWeaknessRow" + suffix);
            backgroundWeaknessIcons[i] = Sprite("backgroundWeaknessSkillIcon" + suffix);
            backgroundWeaknessNames[i] = Label("backgroundWeaknessName" + suffix);
            backgroundWeaknessValues[i] = Label("backgroundWeaknessValue" + suffix);
            backgroundWeaknessExploreButtons[i] = GetChildById("btnBackgroundWeaknessExplore" + suffix);
            if(backgroundWeaknessExploreButtons[i]!=null){backgroundWeaknessExploreIndex[backgroundWeaknessExploreButtons[i]]=i;backgroundWeaknessExploreButtons[i].OnPress+=ProgressionWeaknessExplore_OnPressed;}
        }

        for (int i = 0; i < TraitRows; i++)
        {
            traitRows[i] = GetChildById("traitRow" + i.ToString(CultureInfo.InvariantCulture));
            traitButtons[i] = GetChildById("btnTrait" + i.ToString(CultureInfo.InvariantCulture));
            traitCaptions[i] = Label("traitCaption" + i.ToString(CultureInfo.InvariantCulture));
            traitIcons[i] = Sprite("traitIcon" + i.ToString(CultureInfo.InvariantCulture));
            if (traitButtons[i] != null)
            {
                traitIndex[traitButtons[i]] = i;
                traitButtons[i].OnPress += TraitRow_OnPressed;
            }
        }
        for (int i = 0; i < TraitSideRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            positiveTraitRows[i] = GetChildById("positiveTraitRow" + suffix);
            positiveTraitButtons[i] = GetChildById("btnPositiveTrait" + suffix);
            positiveTraitIcons[i] = Sprite("positiveTraitIcon" + suffix);
            positiveTraitSelections[i] = Sprite("positiveTraitSelection" + suffix);
            positiveTraitNames[i] = Label("positiveTraitName" + suffix);
            positiveTraitSummaries[i] = Label("positiveTraitSummary" + suffix);
            positiveTraitPoints[i] = Label("positiveTraitPoints" + suffix);
            positiveTraitActions[i] = Label("positiveTraitAction" + suffix);
            if (positiveTraitButtons[i] != null)
            {
                positiveTraitIndex[positiveTraitButtons[i]] = i;
                positiveTraitButtons[i].OnPress += TraitSideRow_OnPressed;
                WireScroll(positiveTraitButtons[i], PositiveTraitScroll);
            }

            negativeTraitRows[i] = GetChildById("negativeTraitRow" + suffix);
            negativeTraitButtons[i] = GetChildById("btnNegativeTrait" + suffix);
            negativeTraitIcons[i] = Sprite("negativeTraitIcon" + suffix);
            negativeTraitSelections[i] = Sprite("negativeTraitSelection" + suffix);
            negativeTraitNames[i] = Label("negativeTraitName" + suffix);
            negativeTraitSummaries[i] = Label("negativeTraitSummary" + suffix);
            negativeTraitPoints[i] = Label("negativeTraitPoints" + suffix);
            negativeTraitActions[i] = Label("negativeTraitAction" + suffix);
            if (negativeTraitButtons[i] != null)
            {
                negativeTraitIndex[negativeTraitButtons[i]] = i;
                negativeTraitButtons[i].OnPress += TraitSideRow_OnPressed;
                WireScroll(negativeTraitButtons[i], NegativeTraitScroll);
            }
        }
        for (int i = 0; i < DietRows; i++)
        {
            dietRows[i] = GetChildById("dietRow" + i.ToString(CultureInfo.InvariantCulture));
            dietButtons[i] = GetChildById("btnDiet" + i.ToString(CultureInfo.InvariantCulture));
            dietNames[i] = Label("dietName" + i.ToString(CultureInfo.InvariantCulture));
            dietSummaries[i] = Label("dietSummary" + i.ToString(CultureInfo.InvariantCulture));
            dietPoints[i] = Label("dietPoints" + i.ToString(CultureInfo.InvariantCulture));
            dietIcons[i] = Sprite("dietIcon" + i.ToString(CultureInfo.InvariantCulture));
            dietSelections[i] = Sprite("dietSelection" + i.ToString(CultureInfo.InvariantCulture));
            if (dietButtons[i] != null)
            {
                dietIndex[dietButtons[i]] = i;
                dietButtons[i].OnPress += DietRow_OnPressed;
            }
        }
        for (int i = 0; i < DietFoodSlots; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            dietFoodRows[i] = GetChildById("dietFoodRow" + suffix);
            dietFoodIcons[i] = Sprite("dietFoodIcon" + suffix);
            dietFoodNames[i] = Label("dietFoodName" + suffix);
            if (dietFoodRows[i] != null) WireScroll(dietFoodRows[i], DietFoodVirtualScroll);
        }
        for (int i = 0; i < ReviewSkillRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            reviewSkillRows[i] = GetChildById("reviewSkillRow" + suffix);
            reviewSkillIcons[i] = Sprite("reviewSkillIcon" + suffix);
            reviewSkillNames[i] = Label("reviewSkillName" + suffix);
            reviewSkillValues[i] = Label("reviewSkillValue" + suffix);
            reviewSkillExploreButtons[i] = GetChildById("btnReviewSkillExplore" + suffix);
            if(reviewSkillExploreButtons[i]!=null){reviewSkillExploreIndex[reviewSkillExploreButtons[i]]=i;reviewSkillExploreButtons[i].OnPress+=ProgressionReviewSkillExplore_OnPressed;}
        }
        for (int i = 0; i < ReviewKnowledgeRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            reviewKnowledgeRows[i] = GetChildById("reviewKnowledgeRow" + suffix);
            reviewKnowledgeIcons[i] = Sprite("reviewKnowledgeIcon" + suffix);
            reviewKnowledgeNames[i] = Label("reviewKnowledgeName" + suffix);
            reviewKnowledgeValues[i] = Label("reviewKnowledgeValue" + suffix);
            reviewKnowledgeExploreButtons[i] = GetChildById("btnReviewKnowledgeExplore" + suffix);
            if(reviewKnowledgeExploreButtons[i]!=null){reviewKnowledgeExploreIndex[reviewKnowledgeExploreButtons[i]]=i;reviewKnowledgeExploreButtons[i].OnPress+=ProgressionReviewKnowledgeExplore_OnPressed;}
        }
        for (int i = 0; i < ReviewTraitRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            reviewTraitRows[i] = GetChildById("reviewTraitRow" + suffix);
            reviewTraitIcons[i] = Sprite("reviewTraitIcon" + suffix);
            reviewTraitNames[i] = Label("reviewTraitName" + suffix);
            reviewTraitValues[i] = Label("reviewTraitValue" + suffix);
        }
        for (int i = 0; i < ReviewWeaknessRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            reviewWeaknessRows[i] = GetChildById("reviewWeaknessRow" + suffix);
            reviewWeaknessIcons[i] = Sprite("reviewWeaknessIcon" + suffix);
            reviewWeaknessNames[i] = Label("reviewWeaknessName" + suffix);
            reviewWeaknessValues[i] = Label("reviewWeaknessValue" + suffix);
        }
        for (int i = 0; i < ReviewStartingItemRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            reviewStartingItemRows[i] = GetChildById("reviewStartingItemRow" + suffix);
            reviewStartingItemIcons[i] = Sprite("reviewStartingItemIcon" + suffix);
            reviewStartingItemNames[i] = Label("reviewStartingItemName" + suffix);
            reviewStartingItemMeta[i] = Label("reviewStartingItemMeta" + suffix);
        }

        // Review uses the same native <defaultscrollbar/> contract as the rest of the Survivor UI.
        // Each independently variable list owns its own scrollbar so a long Traits selection cannot
        // hide entries in Skills/Knowledge or Starting Items.
        reviewProgressionNativeScrollHost = GetChildById("reviewProgressionNativeScrollHost");
        reviewProgressionNativeScrollProxy = GetChildById("reviewProgressionNativeScrollProxy");
        reviewProgressionNativeScrollView = GetChildById("reviewProgressionNativeScrollView");
        reviewProgressionScrollCapture = GetChildById("reviewProgressionScrollCapture");
        reviewTraitNativeScrollHost = GetChildById("reviewTraitNativeScrollHost");
        reviewTraitNativeScrollProxy = GetChildById("reviewTraitNativeScrollProxy");
        reviewTraitNativeScrollView = GetChildById("reviewTraitNativeScrollView");
        reviewTraitScrollCapture = GetChildById("reviewTraitScrollCapture");
        reviewStartingItemsNativeScrollHost = GetChildById("reviewStartingItemsNativeScrollHost");
        reviewStartingItemsNativeScrollProxy = GetChildById("reviewStartingItemsNativeScrollProxy");
        reviewStartingItemsNativeScrollView = GetChildById("reviewStartingItemsNativeScrollView");
        reviewStartingItemsScrollCapture = GetChildById("reviewStartingItemsScrollCapture");
        WireScroll(reviewProgressionNativeScrollHost, ReviewProgressionScroll);
        WireScroll(reviewProgressionNativeScrollView, ReviewProgressionScroll);
        WireScroll(reviewProgressionNativeScrollProxy, ReviewProgressionScroll);
        WireScroll(reviewProgressionScrollCapture, ReviewProgressionScroll);
        WireScroll(reviewTraitNativeScrollHost, ReviewTraitScroll);
        WireScroll(reviewTraitNativeScrollView, ReviewTraitScroll);
        WireScroll(reviewTraitNativeScrollProxy, ReviewTraitScroll);
        WireScroll(reviewTraitScrollCapture, ReviewTraitScroll);
        WireScroll(reviewStartingItemsNativeScrollHost, ReviewStartingItemsScroll);
        WireScroll(reviewStartingItemsNativeScrollView, ReviewStartingItemsScroll);
        WireScroll(reviewStartingItemsNativeScrollProxy, ReviewStartingItemsScroll);
        WireScroll(reviewStartingItemsScrollCapture, ReviewStartingItemsScroll);
        for (int i = 0; i < ReviewSummaryProgressionRows; i++)
        {
            WireScroll(reviewSkillRows[i], ReviewProgressionScroll);
            WireScroll(reviewSkillExploreButtons[i], ReviewProgressionScroll);
        }
        for (int i = 0; i < ReviewTraitRows; i++) WireScroll(reviewTraitRows[i], ReviewTraitScroll);
        for (int i = 0; i < ReviewStartingItemRows; i++) WireScroll(reviewStartingItemRows[i], ReviewStartingItemsScroll);

        for (int i = 0; i < NativeProfileVisibleRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            nativeProfileRows[i] = GetChildById("nativeProfileRow" + suffix);
            nativeProfileButtons[i] = GetChildById("btnNativeProfile" + suffix);
            nativeProfileNames[i] = Label("nativeProfileName" + suffix);
            nativeProfileSelections[i] = Sprite("nativeProfileSelection" + suffix);
            nativeProfileChecks[i] = Sprite("nativeProfileCheck" + suffix);
            nativeProfilePortraits[i] = new RebirthPlayerProfilePortraitBinder(GetChildById("nativeProfileFace" + suffix));
            if (nativeProfileButtons[i] != null)
            {
                nativeProfileButtonIndex[nativeProfileButtons[i]] = i;
                nativeProfileButtons[i].OnPress += NativeProfileRow_OnPressed;
            }
            WireScroll(nativeProfileRows[i], ProfileScroll);
            WireScroll(nativeProfileButtons[i], ProfileScroll);
            WireScroll(nativeProfileNames[i] != null ? nativeProfileNames[i].Controller : null, ProfileScroll);
            WireScroll(GetChildById("nativeProfileFace" + suffix), ProfileScroll);
        }

        profileScrollTrack = GetChildById("profileScrollTrackInput");
        profileNativeScrollHost = GetChildById("profileNativeScrollHost");profileNativeScrollProxy = GetChildById("profileNativeScrollProxy");profileNativeScrollView=GetChildById("profileNativeScrollView");
        WireScroll(profileNativeScrollHost, ProfileScroll);WireScroll(profileNativeScrollView, ProfileScroll);WireScroll(profileNativeScrollProxy, ProfileScroll);
        profileScrollThumbControl = GetChildById("profileScrollThumb");
        profileScrollChrome = Sprite("profileScrollBarChrome");profileScrollChromeInner = Sprite("profileScrollBarChromeInner");
        profileScrollThumb = profileScrollThumbControl != null ? profileScrollThumbControl.ViewComponent as XUiV_Button : null;
        WireScroll(GetChildById("profilePaneScrollCapture"), ProfileScroll);
        WireScroll(profileScrollTrack, ProfileScroll);
        WireScroll(profileScrollThumbControl, ProfileScroll);
        nativeProfileScrollOffset = 0;
        nativeProfileCount = 0;
        nativeProfileLastSelection = string.Empty;
        nativeGlobalProfileNameAtOpen = string.Empty;
        nativeGlobalProfileSourceAtOpen = string.Empty;
        pendingIntentionalDeleteName = string.Empty;
        pendingIntentionalDeleteDeadline = 0f;
        nativeExistingProfileEditActive = false;
        nativeExistingProfileEditorOpened = false;
        nativeExistingProfileEditName = string.Empty;
        SetVisible(playerProfileDeleteOverlay, false);
        pendingSilentTempCleanupName = string.Empty;
        pendingSilentTempCleanupDeadline = 0f;
        nativeProfileCountProbeAt = 0f;
        nativeProfileModalWasOpen = false;
        nativePortraitPreloadStarted = false;
        nativePortraitPreloadComplete = false;
        nativePortraitPreloadCursor = 0;
        nativePortraitPreloadRestoreName = string.Empty;
        pendingSelectedPortraitName = string.Empty;
        pendingSelectedPortraitReadyFrame = -1;
        nativePortraitFailureLogged.Clear();
        nativeProfileCreatePhase = NativeProfileCreatePhase.None;
        nativeTemporaryProfileName = string.Empty;
        nativeOriginalProfileName = string.Empty;
        nativeFinalProfileName = string.Empty;
        nativeTemporaryAppearance = null;
        nativeEditorDecision = 0;
        nativeProfileCreateStageDeadline = 0f;
        SetVisible(playerProfileSaveOverlay, false);
        WireCreatorThumbDrag(profileScrollThumbControl,true);
        profileScrollPageUp=GetChildById("btnCreatorProfileScrollPageUp");profileScrollPageDown=GetChildById("btnCreatorProfileScrollPageDown");
        WireCreatorPageRegion(profileScrollPageUp,true,-1);WireCreatorPageRegion(profileScrollPageDown,true,1);

        backgroundScrollTrack = GetChildById("backgroundScrollTrackInput");
        backgroundNativeScrollHost = GetChildById("backgroundNativeScrollHost");backgroundNativeScrollProxy = GetChildById("backgroundNativeScrollProxy");backgroundNativeScrollView=GetChildById("backgroundNativeScrollView");
        WireScroll(backgroundNativeScrollHost, BackgroundScroll);WireScroll(backgroundNativeScrollView, BackgroundScroll);WireScroll(backgroundNativeScrollProxy, BackgroundScroll);
        backgroundScrollThumbControl = GetChildById("backgroundScrollThumb");
        backgroundScrollChrome = Sprite("customScrollBarChrome");backgroundScrollChromeInner = Sprite("customScrollBarChromeInner");
        backgroundScrollThumb = backgroundScrollThumbControl != null ? backgroundScrollThumbControl.ViewComponent as XUiV_Button : null;
        WireScroll(GetChildById("backgroundPaneScrollCapture"), BackgroundScroll);
        WireScroll(backgroundScrollTrack, BackgroundScroll);
        WireScroll(backgroundScrollThumbControl, BackgroundScroll);
        WireCreatorThumbDrag(backgroundScrollThumbControl,false);
        backgroundScrollPageUp=GetChildById("btnCreatorBackgroundScrollPageUp");backgroundScrollPageDown=GetChildById("btnCreatorBackgroundScrollPageDown");
        WireCreatorPageRegion(backgroundScrollPageUp,false,-1);WireCreatorPageRegion(backgroundScrollPageDown,false,1);
    }

    private static int ArmOpenRequest(string reason, RebirthSurvivorCreatorPurpose purpose)
    {
        pendingOpenRequestSerial = ++nextOpenRequestSerial;
        pendingOpenRequestReason = reason ?? string.Empty;
        pendingOpenRequestPurpose = purpose;
        pendingOpenRequestArmed = true;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR-OPEN-REQUEST serial=" + pendingOpenRequestSerial +
            " reason=" + pendingOpenRequestReason + " purpose=" + purpose); }
        return pendingOpenRequestSerial;
    }

    private bool ConsumeOpenRequest()
    {
        if (!pendingOpenRequestArmed) return false;
        activeOpenRequestSerial = pendingOpenRequestSerial;
        activeOpenRequestReason = pendingOpenRequestReason ?? string.Empty;
        pendingOpenRequestArmed = false;
        pendingOpenRequestSerial = 0;
        pendingOpenRequestReason = string.Empty;
        return true;
    }

    public static void PrepareForRebirthMenuNavigation(XUi xui, string reason)
    {
        pendingOpenRequestArmed = false;
        pendingOpenRequestSerial = 0;
        pendingOpenRequestReason = string.Empty;
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        bool creatorOpen = manager.IsWindowOpen(WindowGroupId);
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("REBIRTH-MENU-NAV reason=" + (reason ?? string.Empty) +
            " creatorOpen=" + creatorOpen +
            " survivorManager=" + manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId) +
            " playerProfiles=" + manager.IsWindowOpen("playerProfiles") +
            " playGamePaging=" + manager.IsWindowOpen("playGamePaging")); }
        if (creatorOpen) manager.Close(WindowGroupId);
    }

    private string WindowStateForLog()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return "wm=<null>";
        GUIWindowManager manager = xui.playerUI.windowManager;
        return "mainMenu=" + manager.IsWindowOpen("mainMenu") +
            " playGamePaging=" + manager.IsWindowOpen("playGamePaging") +
            " playerProfiles=" + manager.IsWindowOpen("playerProfiles") +
            " playerProfilesCreate=" + manager.IsWindowOpen("playerProfilesCreate") +
            " survivorManager=" + manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId) +
            " creator=" + manager.IsWindowOpen(WindowGroupId);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        bool authorizedOpen = ConsumeOpenRequest();
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR-ONOPEN authorized=" + authorizedOpen +
            " serial=" + activeOpenRequestSerial + " reason=" + activeOpenRequestReason +
            " modelPurpose=" + (model != null ? model.Purpose.ToString() : "<null>") + " " + WindowStateForLog()); }
        if (string.Equals(activeOpenRequestReason, "spawnselection-create-profile", StringComparison.Ordinal))
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("creator OnOpen from spawnselection authorized=" + authorizedOpen
                + " visible=" + (ViewComponent != null && ViewComponent.IsVisible) + " " + WindowStateForLog()); }
        if (!authorizedOpen)
        {
            // Never allow the base game's Player Profile action (or stale window state) to
            // surface the Survivor creator. Close only; do not touch any native menu window.
            // Clear borrowed-profile state so OnClose is also side-effect free for this path.
            nativeGlobalProfileNameAtOpen = string.Empty;
            nativeGlobalProfileSourceAtOpen = string.Empty;
            suspendedForNativePicker = false;
            unauthorizedOpenRedirectPending = true;
            return;
        }
        unauthorizedOpenRedirectPending = false;
#if REBIRTH_DEBUG
        activeDebugInstance = this;
#endif
        if (resumeFromProgressionExplorerPending)
        {
            resumeFromProgressionExplorerPending=false;suspendedForProgressionExplorer=false;progressionExplorerReturnToken=string.Empty;
            if(windowGroup!=null)windowGroup.isEscClosable=false;
            RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();RenderAll();
            if(reviewVisualContent!=null&&reviewVisualContent.ViewComponent!=null)reviewVisualContent.ViewComponent.Position=progressionExplorerDetailContentPosition;
            PrepareCursor();
            if(progressionExplorerReturnFocus!=null&&progressionExplorerReturnFocus.ViewComponent!=null&&xui!=null&&xui.playerUI!=null&&xui.playerUI.CursorController!=null)xui.playerUI.CursorController.SetNavigationTargetLater(progressionExplorerReturnFocus.ViewComponent);
            progressionExplorerReturnFocus=null;Log.Out("[REBIRTH Survivor][ProgressionExplorer] CREATOR-RETURN step="+(model!=null?model.Step.ToString():"<null>"));return;
        }
        if (resumeFromNativeChildEditorPending)
        {
            resumeFromNativeChildEditorPending = false;
            suspendedForNativeChildEditor = false;
            if (windowGroup != null) windowGroup.isEscClosable = false;

            // This is a continuation of the same Survivor Creator session, not a fresh open.
            // Do not reset the view model, RBTemp_* workflow, original global profile capture,
            // or existing-profile edit state that survived the native child editor.
            bool awaitingSaveName = nativeProfileCreatePhase == NativeProfileCreatePhase.AwaitSaveName;
            SetVisible(playerProfileSaveOverlay, awaitingSaveName);
            SetVisible(playerProfileDeleteOverlay, false);
            SetVisible(profilePortraitPreloadMask, false);
            if (awaitingSaveName && newPlayerProfileNameInput != null && newPlayerProfileNameInput.ViewComponent != null &&
                xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
                xui.playerUI.CursorController.SetNavigationTargetLater(newPlayerProfileNameInput.ViewComponent);

            ScheduleSelectedProfilePortrait(model != null ? model.PlayerProfileName : string.Empty);
            RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();
            RenderAll();
            PrepareCursor();
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("CREATOR native-child resume phase=" +
                nativeProfileCreatePhase + " editActive=" + nativeExistingProfileEditActive + " " + WindowStateForLog()); }
            return;
        }
        if (windowGroup != null) windowGroup.isEscClosable = false;
        nativeGlobalProfileNameAtOpen = string.Empty;
        nativeGlobalProfileSourceAtOpen = string.Empty;
        pendingIntentionalDeleteName = string.Empty;
        pendingIntentionalDeleteDeadline = 0f;
        nativeExistingProfileEditActive = false;
        nativeExistingProfileEditorOpened = false;
        nativeExistingProfileEditName = string.Empty;
        SetVisible(playerProfileDeleteOverlay, false);
        pendingSilentTempCleanupName = string.Empty;
        pendingSilentTempCleanupDeadline = 0f;
        string definitionReport = string.Empty;
        if (!RebirthSurvivorDefinitionRegistry.IsReady || !RebirthSurvivorInstaller.IsInstalled)
            definitionReport = RebirthSurvivorInstaller.Install();
        if (model == null)
        {
            model = new RebirthSurvivorCreatorViewModel();
            model.BeginCreateLocalProfile();
        }
        else
        {
            model.RefreshValidation();
        }
        // Capture the base game's globally active Player Profile before this editor starts
        // borrowing the native ProfilesList for preview/portrait work. The Survivor draft may
        // select a different appearance, but that must never become the player's global default.
        string activePlayerProfile;
        string activePlayerProfileSource;
        EntityPlayerLocal localPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (RebirthNativePlayerProfileBridge.TryResolveActivePlayerProfileName(xui, localPlayer, out activePlayerProfile, out activePlayerProfileSource) &&
            !IsInternalTemporaryPlayerProfile(activePlayerProfile))
        {
            nativeGlobalProfileNameAtOpen = activePlayerProfile;
            nativeGlobalProfileSourceAtOpen = activePlayerProfileSource;
            if (model != null && !model.HasPlayerProfileSelection && !model.IsReadOnly)
                model.SetPlayerProfileName(activePlayerProfile);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("GLOBAL-CAPTURE profile='" + activePlayerProfile + "' source=" + activePlayerProfileSource +
                " draft='" + (model != null ? model.PlayerProfileName : string.Empty) + "'"); }
        }
        else if (model != null && !model.HasPlayerProfileSelection && !model.IsReadOnly)
        {
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] GLOBAL-CAPTURE unavailable; first real native profile will be used as the Survivor draft default.");
        }
        transientStatus = !RebirthSurvivorDefinitionRegistry.IsReady
            ? "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorDefinitionsUnavailable", "Survivor definitions could not be loaded. See the output log for details.") + "[-]"
            : (!RebirthSurvivorInstaller.IsInstalled
                ? "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorRuntimeIncomplete", "Survivor runtime initialization is incomplete. Creation is disabled; see the output log for [REBIRTH Survivor][RuntimeInstall].") + "[-]"
                : string.Empty);
        focusedTraitId = string.Empty;
        RebirthSurvivorProfileStore.Refresh();
        profiles = RebirthSurvivorProfileStore.GetProfilesSnapshot();
        profileOffset = 0;
        reviewProgressionOffset = 0;
        reviewTraitOffset = 0;
        reviewStartingItemsOffset = 0;
        reviewProgressionTotal = 0;
        reviewTraitTotal = 0;
        reviewStartingItemsTotal = 0;
        reviewNativeLayoutRefreshFrames = 0;
        nativeProfileScrollOffset = 0;
        nativeProfileLastSelection = string.Empty;
        nativePortraitPreloadStarted = false;
        nativePortraitPreloadComplete = false;
        nativePortraitPreloadCursor = 0;
        nativePortraitPreloadRestoreName = string.Empty;
        nativeProfileCreatePhase = NativeProfileCreatePhase.None;
        nativeTemporaryProfileName = string.Empty;
        nativeOriginalProfileName = string.Empty;
        nativeFinalProfileName = string.Empty;
        nativeTemporaryAppearance = null;
        nativeEditorDecision = 0;
        nativeProfileCreateStageDeadline = 0f;
        SetVisible(playerProfileSaveOverlay, false);
        SetVisible(profilePortraitPreloadMask, false);
        ScheduleSelectedProfilePortrait(model != null ? model.PlayerProfileName : string.Empty);
        EnsureSelectedProfileVisible();
        if (profileNameInput != null)
        {
            profileNameInput.Text = model.ProfileName;
            profileNameInput.Enabled = !model.IsReadOnly && (!model.IsUsingExistingProfile || model.IsLocalProfileFlow);
        }
        if (xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
            wasCursorHidden = xui.playerUI.CursorController.GetCursorHidden();
        PurgeStaleInternalTemporaryProfiles();
        RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();
        RenderAll();
        dietNativeLayoutRefreshFrames=3;
        PrepareCursor();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(profileNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(backgroundNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(positiveTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(negativeTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(backgroundKnowledgeNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(backgroundStartingItemsNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(reviewProgressionNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(reviewTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(reviewStartingItemsNativeScrollHost);
    }

    private bool pagingMode;
    public override void Update(float dt)
    {
        base.Update(dt);
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (model != null && pagingMode != pagingNow)
        {
            pagingMode = pagingNow; pagingDragRemainder = 0f; profileThumbDragging = backgroundThumbDragging = false;
            if (pagingNow)
            {
                nativeProfileScrollOffset = ClampOffset(nativeProfileScrollOffset, nativeProfileCount, NativeProfileVisibleRows);
                model.BackgroundOffset = ClampOffset(model.BackgroundOffset, model.GetBackgroundCount(), BackgroundRows);
                model.PositiveTraitOffset = ClampOffset(model.PositiveTraitOffset, model.GetPositiveTraitCount(), TraitSideRows);
                model.NegativeTraitOffset = ClampOffset(model.NegativeTraitOffset, model.GetNegativeTraitCount(), TraitSideRows);
                RenderAll();
            }
        }
        PollDietPaging();
        PollCreatorNativeScroll(true);PollCreatorNativeScroll(false);
        PollTraitNativeScroll(false);PollTraitNativeScroll(true);
        PollBackgroundKnowledgeNativeScroll();
        PollBackgroundStartingItemsNativeScroll();
        PollReviewNativeScroll();
        if(backgroundKnowledgeNativeLayoutRefreshFrames>0){backgroundKnowledgeNativeLayoutRefreshFrames--;RefreshNativeScrollView(backgroundKnowledgeNativeScrollView);}
        if(backgroundStartingItemsNativeLayoutRefreshFrames>0){backgroundStartingItemsNativeLayoutRefreshFrames--;RefreshNativeScrollView(backgroundStartingItemsNativeScrollView);}
        if(reviewNativeLayoutRefreshFrames>0){reviewNativeLayoutRefreshFrames--;RefreshNativeScrollView(reviewProgressionNativeScrollView);RefreshNativeScrollView(reviewTraitNativeScrollView);RefreshNativeScrollView(reviewStartingItemsNativeScrollView);}
        if(dietNativeLayoutRefreshFrames>0){dietNativeLayoutRefreshFrames--;if(!RebirthScrollbarPagingPolicy.Enabled)RefreshNativeScrollView(dietDetailNativeScrollView);RefreshNativeScrollView(dietSummaryNativeScrollView);}

        if (unauthorizedOpenRedirectPending)
        {
            unauthorizedOpenRedirectPending = false;
            if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
            {
                GUIWindowManager manager = xui.playerUI.windowManager;
                Log.Warning("[REBIRTH Survivor][UiRoute] UNAUTHORIZED-CREATOR-OPEN action=close-only " + WindowStateForLog());
                if (manager.IsWindowOpen(WindowGroupId)) manager.Close(WindowGroupId);
            }
            handleDirtyUpdateDefault();
            return;
        }

        if (artBinder != null) artBinder.Update();
        if (reviewBackgroundArtBinder != null) reviewBackgroundArtBinder.Update();
        if (playerPreviewBackgroundBinder != null) playerPreviewBackgroundBinder.Update();
        for (int i = 0; i < backgroundRowArtBinders.Length; i++)
            if (backgroundRowArtBinders[i] != null) backgroundRowArtBinders[i].Update();

        TickNativePlayerProfileCreateFlow();
        PumpExistingPlayerProfileEdit();
        PumpSilentTemporaryProfileCleanup();
        bool nativeProfileModalOpen = IsNativePlayerProfileModalOpen();
        bool customCreateFlowActive = nativeProfileCreatePhase != NativeProfileCreatePhase.None;
        bool portraitPreloadActive = nativePortraitPreloadStarted && !nativePortraitPreloadComplete;

        if (!customCreateFlowActive && !portraitPreloadActive && nativeProfileModalWasOpen && !nativeProfileModalOpen)
        {
            RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
            SyncEmbeddedPlayerProfileSelection();
            if (model != null && !string.IsNullOrEmpty(model.PlayerProfileName))
                RebirthPlayerProfilePortraitBinder.Invalidate(model.PlayerProfileName);
                RebirthPlayerProfileModelBinder.Invalidate(model.PlayerProfileName);
            nativeProfileLastSelection = string.Empty;
            nativeProfileScrollOffset = 0;
            pendingSelectedPortraitName = model != null ? (model.PlayerProfileName ?? string.Empty).Trim() : string.Empty;
            pendingSelectedPortraitReadyFrame = Time.frameCount + 1;
            nativePortraitFailureLogged.Clear();
            for (int i = 0; i < nativeProfilePortraits.Length; i++)
                if (nativeProfilePortraits[i] != null) nativeProfilePortraits[i].ClearView();
            RenderProfiles();
        }
        nativeProfileModalWasOpen = nativeProfileModalOpen;
        PumpIntentionalProfileDeleteRecovery();

        if (model != null && model.Step == RebirthSurvivorCreatorStep.Profile &&
            !customCreateFlowActive && !portraitPreloadActive &&
            Time.realtimeSinceStartup >= nativeProfileCountProbeAt)
        {
            nativeProfileCountProbeAt = Time.realtimeSinceStartup + 0.25f;
            RenderProfiles();
        }

        if (!nativeProfileModalOpen && !customCreateFlowActive && nativePortraitPreloadStarted && !nativePortraitPreloadComplete)
            PumpNativeProfilePortraitPreload();
        if (!nativeProfileModalOpen && !customCreateFlowActive && nativePortraitPreloadComplete &&
            model != null && model.Step == RebirthSurvivorCreatorStep.Profile)
            PumpSelectedProfilePortrait();

        portraitPreloadActive = nativePortraitPreloadStarted && !nativePortraitPreloadComplete;
        if (!nativeProfileModalOpen && !customCreateFlowActive && !portraitPreloadActive &&
            XUiUtils.HotkeysAllowedFor(viewComponent) && xui != null && xui.playerUI != null)
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            {
                bool goPrevious = model != null && !model.IsReadOnly && model.Step > RebirthSurvivorCreatorStep.Profile;
                { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR ESC step=" +
                    (model != null ? model.Step.ToString() : "<null>") +
                    " action=" + (goPrevious ? "previous" : "cancel")); }
                if (goPrevious)
                    Previous_OnPressed(this, -1);
                else
                    Cancel_OnPressed(this, -1);
            }
            else if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased)
            {
                if (model != null && model.Step == RebirthSurvivorCreatorStep.Review)
                {
                    if (model.IsReadOnly) Cancel_OnPressed(this, 0);
                    else Save_OnPressed(this, 0);
                }
                else Next_OnPressed(this, 0);
            }
        }
        handleDirtyUpdateDefault();
    }

    public override void OnClose()
    {
        bool preserveCreatorSession = suspendedForNativePicker || suspendedForNativeChildEditor || suspendedForProgressionExplorer;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR-ONCLOSE serial=" + activeOpenRequestSerial +
            " reason=" + activeOpenRequestReason +
            " suspendedForNativePicker=" + suspendedForNativePicker +
            " suspendedForNativeChildEditor=" + suspendedForNativeChildEditor +
            " suspendedForProgressionExplorer=" + suspendedForProgressionExplorer +
            " preserveSession=" + preserveCreatorSession +
            " unauthorizedRedirect=" + unauthorizedOpenRedirectPending + " " + WindowStateForLog()); }
        if (string.Equals(activeOpenRequestReason, "spawnselection-create-profile", StringComparison.Ordinal))
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("creator OnClose from spawnselection preserveSession=" + preserveCreatorSession
                + " visible=" + (ViewComponent != null && ViewComponent.IsVisible) + " " + WindowStateForLog()); }
        if (artBinder != null) artBinder.Clear();
        if (reviewBackgroundArtBinder != null) reviewBackgroundArtBinder.Clear();
        reviewPreviewPlayerProfileName = string.Empty;
        if (playerPreviewBackgroundBinder != null) playerPreviewBackgroundBinder.Clear();
        for (int i = 0; i < nativeProfilePortraits.Length; i++)
            if (nativeProfilePortraits[i] != null) nativeProfilePortraits[i].Clear();
        for (int i = 0; i < backgroundRowArtBinders.Length; i++)
            if (backgroundRowArtBinders[i] != null) backgroundRowArtBinders[i].Clear();
        UnwireNativeEditorDecisionButtons();
        SetVisible(playerProfileSaveOverlay, false);
        SetVisible(playerProfileDeleteOverlay, false);
        SetVisible(profilePortraitPreloadMask, false);

        if (!preserveCreatorSession)
        {
            nativeExistingProfileEditActive = false;
            nativeExistingProfileEditorOpened = false;
            nativeExistingProfileEditName = string.Empty;
            if (nativeProfileCreatePhase != NativeProfileCreatePhase.None)
            {
                if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
                {
                    GUIWindowManager manager = xui.playerUI.windowManager;
                    if (manager.IsWindowOpen("customCharacterSystem")) manager.Close("customCharacterSystem");
                    if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
                }
                if (!string.IsNullOrEmpty(nativeTemporaryProfileName))
                {
                    string cleanupError;
                    RebirthNativePlayerProfileBridge.TryDeleteEmbeddedPlayerProfileSilently(this, nativeTemporaryProfileName, out cleanupError);
                }
                nativeProfileCreatePhase = NativeProfileCreatePhase.None;
            }
            RestoreGlobalNativePlayerProfile();
            suspendedForNativeChildEditor = false;
            resumeFromNativeChildEditorPending = false;
            suspendedForProgressionExplorer = false;
            resumeFromProgressionExplorerPending = false;
            if(!string.IsNullOrEmpty(progressionExplorerReturnToken))RebirthProgressionExplorerReturnRegistry.Unregister(progressionExplorerReturnToken);
            progressionExplorerReturnToken=string.Empty;
        }

        RestoreUiInputState();
        base.OnClose();
        RestoreUiInputState();

        // A native picker/editor is a temporary child operation. Every other close ends this
        // creator session so stale state cannot leak into Play Game / Player Profile navigation.
        if (!preserveCreatorSession)
        {
#if REBIRTH_DEBUG
            if (object.ReferenceEquals(activeDebugInstance, this)) activeDebugInstance = null;
#endif
            model = null;
            onLocalProfileSaved = null;
            onExternalConfirmed = null;
            onExternalCancelled = null;
            returnToProfileManagerAfterLocalSession = true;
            activeOpenRequestSerial = 0;
            activeOpenRequestReason = string.Empty;
            transientStatus = string.Empty;
            focusedTraitId = string.Empty;
        }
        dietChromeWitness = null;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(dietPagingHost);
        dietPagingRegistered = false; dietPagingView = null; dietPagingBar = null;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(profileNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(backgroundNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(positiveTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(negativeTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(backgroundKnowledgeNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(backgroundStartingItemsNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(reviewProgressionNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(reviewTraitNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(reviewStartingItemsNativeScrollHost);
    }

#if REBIRTH_DEBUG
    /// <summary>Read-only state surface used by the consolidated Debug command.</summary>
    public static bool TryGetActiveDebugState(out RebirthSurvivorCreatorViewModel activeModel, out bool nextVisible, out bool nextEnabled, out string status)
    {
        activeModel = null; nextVisible = false; nextEnabled = false; status = string.Empty;
        XUiC_RebirthSurvivorCreator controller = activeDebugInstance;
        if (controller == null || controller.model == null) return false;
        activeModel = controller.model;
        status = controller.transientStatus ?? string.Empty;
        if (controller.btnNext != null && controller.btnNext.ViewComponent != null)
        {
            nextVisible = controller.btnNext.ViewComponent.IsVisible;
            nextEnabled = controller.btnNext.ViewComponent.Enabled;
        }
        return true;
    }
#endif

    public static void OpenCreate(XUi xui, Action<RebirthSurvivorProfile> saved)
    {
        XUiC_RebirthSurvivorCreator controller = FindController(xui);
        if (controller == null) return;
        RebirthSurvivorCreatorViewModel vm = new RebirthSurvivorCreatorViewModel();
        vm.BeginCreateLocalProfile();
        controller.Configure(vm, saved, null);
        ArmOpenRequest("survivor-manager-create", vm.Purpose);
        GUIWindowManager manager = xui.playerUI.windowManager;
        bool preserveSpawnHost = XUiC_RebirthSurvivorProfileManager.IsSpawnSelectionMode && manager.IsWindowOpen("spawnselection");
        if (preserveSpawnHost)
        {
            manager.Open((GUIWindow)controller.windowGroup, false, true);
            { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("CREATOR-OPEN preserved spawnselection host=True purpose=" + vm.Purpose); }
        }
        else
            manager.Open(WindowGroupId, true);
    }

    /// <summary>
    /// Opens the existing reusable Survivor Profile creation workflow from native spawnselection.
    /// This is the only intentional window transition in the integrated first-spawn flow.  Saving
    /// or cancelling returns through the supplied callback instead of opening the Profile Manager.
    /// </summary>
    public static bool OpenCreateForSpawnSelection(XUi xui, Action<XUi, RebirthSurvivorProfile> completed, out string error)
    {
        error = string.Empty;
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            error = "Survivor creator window manager is unavailable.";
            return false;
        }

        XUiC_RebirthSurvivorCreator controller = FindController(xui);
        if (controller == null)
        {
            error = "Survivor creator window is unavailable.";
            return false;
        }

        RebirthSurvivorCreatorViewModel vm = new RebirthSurvivorCreatorViewModel();
        vm.BeginCreateLocalProfile();
        controller.Configure(vm, delegate(RebirthSurvivorProfile profile)
        {
            if (completed != null) completed(xui, profile);
        }, null, null, false);

        GUIWindowManager manager = xui.playerUI.windowManager;
        GUIWindow spawnWindow = manager.GetWindow("spawnselection");
        bool spawnOpenBefore = manager.IsWindowOpen("spawnselection");
        bool creatorOpenBefore = manager.IsWindowOpen(WindowGroupId);
        if (spawnWindow != null) spawnWindow.isEscClosable = false;

        // Use the same proven overlay overload that REBIRTH QuickStack uses to keep a loot window
        // alive underneath its category dialog.  The previous string Open(..., true) call REPLACED
        // spawnselection in V3.2, which is why the player saw only the loading backdrop.
        ArmOpenRequest("spawnselection-create-profile", vm.Purpose);
        manager.Open((GUIWindow)controller.windowGroup, false, true);

        bool spawnOpenAfter = manager.IsWindowOpen("spawnselection");
        bool creatorOpenAfter = manager.IsWindowOpen(WindowGroupId);
        bool creatorVisible = controller.ViewComponent != null && controller.ViewComponent.IsVisible;
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection creator OVERLAY open before[spawn=" + spawnOpenBefore
            + ",creator=" + creatorOpenBefore + "] after[spawn=" + spawnOpenAfter
            + ",creator=" + creatorOpenAfter + ",visible=" + creatorVisible + "]"); }

        if (!creatorOpenAfter || !spawnOpenAfter)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] creator overlay contract failed spawnOpen=" + spawnOpenAfter
                + " creatorOpen=" + creatorOpenAfter + " creatorVisible=" + creatorVisible);
            if (creatorOpenAfter) manager.Close((GUIWindow)controller.windowGroup);
            if (!manager.IsWindowOpen("spawnselection")) manager.Open("spawnselection", true);
            error = "The Survivor Creator could not be opened without leaving the Spawn Selection screen.";
            return false;
        }
        return true;
    }

    public static bool OpenEdit(XUi xui, RebirthSurvivorProfile profile, Action<RebirthSurvivorProfile> saved, out string error)
    {
        error = string.Empty;
        XUiC_RebirthSurvivorCreator controller = FindController(xui);
        if (controller == null) { error = "Survivor creator window is unavailable."; return false; }
        RebirthSurvivorCreatorViewModel vm = new RebirthSurvivorCreatorViewModel();
        if (!vm.BeginEditLocalProfile(profile, out error)) return false;
        controller.Configure(vm, saved, null);
        ArmOpenRequest("survivor-manager-edit", vm.Purpose);
        xui.playerUI.windowManager.Open(WindowGroupId, true);
        return true;
    }

    public static bool OpenReview(XUi xui, RebirthSurvivorProfile profile, out string error)
    {
        error = string.Empty;
        XUiC_RebirthSurvivorCreator controller = FindController(xui);
        if (controller == null) { error = "Survivor creator window is unavailable."; return false; }
        RebirthSurvivorCreatorViewModel vm = new RebirthSurvivorCreatorViewModel();
        if (!vm.BeginReviewLocalProfile(profile, out error)) return false;
        controller.Configure(vm, null, null);
        ArmOpenRequest("survivor-manager-review", vm.Purpose);
        xui.playerUI.windowManager.Open(WindowGroupId, true);
        return true;
    }

    /// <summary>
    /// Reusable entry used by Chunk 07's first-world creator. The same view model and validation path are retained.
    /// </summary>
    public static bool OpenExternal(XUi xui, RebirthSurvivorCreatorViewModel vm, Action<RebirthSurvivorCreatorViewModel> confirmed, out string error)
    {
        return OpenExternal(xui, vm, confirmed, null, out error);
    }

    public static bool OpenExternal(XUi xui, RebirthSurvivorCreatorViewModel vm, Action<RebirthSurvivorCreatorViewModel> confirmed, Action cancelled, out string error)
    {
        error = string.Empty;
        if (vm == null) { error = "Survivor creator session is null."; return false; }
        XUiC_RebirthSurvivorCreator controller = FindController(xui);
        if (controller == null) { error = "Survivor creator window is unavailable."; return false; }
        controller.Configure(vm, null, confirmed, cancelled);
        ArmOpenRequest("first-world-or-external", vm.Purpose);
        xui.playerUI.windowManager.Open(WindowGroupId, true);
        return true;
    }

    private static XUiC_RebirthSurvivorCreator FindController(XUi xui)
    {
        XUiC_RebirthSurvivorCreator controller = xui != null ? xui.GetChildByType<XUiC_RebirthSurvivorCreator>() : null;
        if (controller == null) Log.Error("[REBIRTH Survivor UI] rebirthSurvivorCreator controller was not found.");
        return controller;
    }

    public void SuspendForNativePlayerProfilePicker()
    {
        suspendedForNativePicker = true;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("CREATOR suspend purpose=" + (model != null ? model.Purpose.ToString() : "<null>")); }
        RestoreUiInputState();
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
            xui.playerUI.windowManager.Close(WindowGroupId);
    }

    public void ResumeAfterNativePlayerProfilePicker(string playerProfileName, bool resolved, string message)
    {
        if (model != null && resolved && !string.IsNullOrEmpty(playerProfileName))
            model.SetPlayerProfileName(playerProfileName);
        suspendedForNativePicker = false;
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
        {
            ArmOpenRequest("native-player-profile-picker-resume", model != null ? model.Purpose : RebirthSurvivorCreatorPurpose.CreateLocalProfile);
            xui.playerUI.windowManager.Open(WindowGroupId, true);
        }
        transientStatus = resolved
            ? string.Empty
            : "[CC6B64]" + (message ?? string.Empty) + "[-]";
        RenderAll();
        PrepareCursor();
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("CREATOR resume resolved=" + resolved + " selected='" + (playerProfileName ?? string.Empty) + "'"); }
    }

    private void Configure(RebirthSurvivorCreatorViewModel vm, Action<RebirthSurvivorProfile> saved, Action<RebirthSurvivorCreatorViewModel> externalConfirmed, Action externalCancelled = null, bool returnToProfileManager = true)
    {
        model = vm;
        onLocalProfileSaved = saved;
        onExternalConfirmed = externalConfirmed;
        onExternalCancelled = externalCancelled;
        returnToProfileManagerAfterLocalSession = returnToProfileManager;
    }

    private void GoToStep(RebirthSurvivorCreatorStep step)
    {
        if (model == null) return;
        string reason;
        if (!model.TryNavigateToStep(step, out reason))
        {
            transientStatus = string.IsNullOrEmpty(reason) ? string.Empty : "[CC6B64]" + reason + "[-]";
            RenderAll();
            return;
        }
        transientStatus = string.Empty;
        RenderAll();
#if REBIRTH_DEBUG
        if (step == RebirthSurvivorCreatorStep.Traits && RebirthSurvivorDebug.TraitUiLoggingEnabled)
            Log.Out(RebirthSurvivorTraitUiDebug.BuildReport(model));
#endif
        SelectFirstForStep();
    }

    private void RenderAll()
    {
        if (model == null) return;
        SetVisible(profilePanel, model.Step == RebirthSurvivorCreatorStep.Profile);
        SetVisible(backgroundPanel, model.Step == RebirthSurvivorCreatorStep.Background);
        SetVisible(traitPanel, model.Step == RebirthSurvivorCreatorStep.Traits);
        SetVisible(dietPanel, model.Step == RebirthSurvivorCreatorStep.Diet);
        SetVisible(reviewPanel, model.Step == RebirthSurvivorCreatorStep.Review);
        SetVisible(detailsPanel, model.Step == RebirthSurvivorCreatorStep.Background);

        // XUiC_SDCSPreviewWindow renders its model on the shared SDCS UI layer. Merely hiding
        // a Creator panel does not destroy that panel's preview rig, so keeping both the
        // Profile-page and Review-page previews alive lets either camera render both models.
        // Enforce one live SDCS preview owner for this Creator window at a time.
        ReleaseInactiveSdcsPreview(model.Step != RebirthSurvivorCreatorStep.Profile ? "nativePlayerPreview" : null);
        ReleaseInactiveSdcsPreview(model.Step != RebirthSurvivorCreatorStep.Review ? "reviewPlayerPreview" : null);

        SetLabel(creatorTitle, model.IsReadOnly
            ? RebirthSurvivorUiText.L("xuiRebirthSurvivorReviewTitle", "REVIEW SURVIVOR PROFILE")
            : (model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate
                ? RebirthSurvivorUiText.L("xuiRebirthSurvivorWorldCreatorTitle", "CREATE NEW SURVIVOR")
                : RebirthSurvivorUiText.L("xuiRebirthSurvivorCreatorTitle", "SURVIVOR PROFILE CREATOR")));
        SetLabel(stepTitle, StepTitle(model.Step));
        SetLabel(validationText, RebirthSurvivorUiText.BuildValidationSummaryForStep(model.Validation, model.Step));
        SetLabel(statusText, transientStatus);

        RenderProfiles();
        RenderBackgrounds();
        RenderTraits();
        RenderDiets();
        RenderDetails();
        SetLabel(reviewText, RebirthSurvivorUiText.BuildReview(model));

        SetEnabled(btnStepProfile, model.CanNavigateToStep(RebirthSurvivorCreatorStep.Profile));
        SetEnabled(btnStepBackground, model.CanNavigateToStep(RebirthSurvivorCreatorStep.Background));
        SetEnabled(btnStepTraits, model.CanNavigateToStep(RebirthSurvivorCreatorStep.Traits));
        SetEnabled(btnStepDiet, model.CanNavigateToStep(RebirthSurvivorCreatorStep.Diet));
        SetEnabled(btnStepReview, model.CanNavigateToStep(RebirthSurvivorCreatorStep.Review));
        SetSelected(btnStepProfile, model.Step == RebirthSurvivorCreatorStep.Profile);
        SetSelected(btnStepBackground, model.Step == RebirthSurvivorCreatorStep.Background);
        SetSelected(btnStepTraits, model.Step == RebirthSurvivorCreatorStep.Traits);
        SetSelected(btnStepDiet, model.Step == RebirthSurvivorCreatorStep.Diet);
        SetSelected(btnStepReview, model.Step == RebirthSurvivorCreatorStep.Review);
        RenderStepTabDimming();

        SetVisible(btnPrevious, !model.IsReadOnly && model.Step > RebirthSurvivorCreatorStep.Profile);
        bool showNext = !model.IsReadOnly && model.Step < RebirthSurvivorCreatorStep.Review;
        SetVisible(btnNext, showNext);
        // Keep the visible Next control mouse-interactive. TryAdvance remains the authority gate and
        // reports the exact blocking reason if a prerequisite is incomplete. Disabling the view here
        // made the button silently unclickable and hid useful validation feedback.
        SetEnabled(btnNext, showNext);
        SetVisible(btnSave, model.Step == RebirthSurvivorCreatorStep.Review);

        bool reviewStep = model.Step == RebirthSurvivorCreatorStep.Review;
        bool showSaveReusable = reviewStep && model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate && !model.IsReadOnly;
        bool showProfileName = reviewStep && !model.IsReadOnly &&
            (model.IsLocalProfileFlow || (model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate && model.SaveAsReusableProfile));
        SetVisible(btnSaveReusableProfile, showSaveReusable);
        if (btnSaveReusableProfile != null && showSaveReusable)
            SetButtonText(btnSaveReusableProfile, RebirthSurvivorUiText.L("xuiRebirthSurvivorSaveReusable", "SAVE AS REUSABLE SURVIVOR PROFILE") +
                ": " + (model.SaveAsReusableProfile ? RebirthSurvivorUiText.L("xuiYes", "YES") : RebirthSurvivorUiText.L("xuiNo", "NO")));
        SetVisible(profileNameLabel, showProfileName);
        SetVisible(profileNameInput, showProfileName);
        if (profileNameInput != null)
        {
            profileNameInput.Enabled = showProfileName;
            if (!profileNameInput.IsSelected) profileNameInput.Text = model.ProfileName;
        }

        if (btnSave != null)
            SetButtonText(btnSave, model.IsReadOnly ? RebirthSurvivorUiText.L("xuiDone", "DONE") :
                (model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate
                    ? RebirthSurvivorUiText.L("xuiRebirthSurvivorUseSelection", "CREATE SURVIVOR")
                    : RebirthSurvivorUiText.L("xuiSave", "SAVE")));
        if (btnCancel != null)
            SetButtonText(btnCancel, model.IsReadOnly ? RebirthSurvivorUiText.L("xuiClose", "CLOSE") : RebirthSurvivorUiText.L("xuiCancel", "CANCEL"));
    }

    private void ReleaseInactiveSdcsPreview(string previewId)
    {
        if (string.IsNullOrEmpty(previewId)) return;
        XUiC_SDCSPreviewWindow preview = GetChildById(previewId) as XUiC_SDCSPreviewWindow;
        if (preview == null) return;

        if ((UnityEngine.Object)preview.previewTransform != (UnityEngine.Object)null)
        {
            SDCSUtils.DestroyViz(preview.previewTransform);
            preview.previewTransform = null;
        }

        // Force the native controller to rebuild from ProfileSDF when this page becomes active
        // again. The render-system/camera members are not public in the supported game assembly,
        // and they do not need to be torn down here: removing the inactive SDCS rig prevents the
        // shared render layer from drawing two characters while preserving the native preview
        // camera/RenderTexture for the next rebuild.
        preview.characterGazeController = null;
        preview.lastProfile = string.Empty;
    }

    private void RenderStepTabDimming()
    {
        if (model == null) return;
        SetVisible(tabDimProfile, model.Step != RebirthSurvivorCreatorStep.Profile);
        SetVisible(tabDimBackground, model.Step != RebirthSurvivorCreatorStep.Background);
        SetVisible(tabDimTraits, model.Step != RebirthSurvivorCreatorStep.Traits);
        SetVisible(tabDimDiet, model.Step != RebirthSurvivorCreatorStep.Diet);
        SetVisible(tabDimReview, model.Step != RebirthSurvivorCreatorStep.Review);
    }

    private void RenderProfiles()
    {
        if (model == null) return;

        for (int i = 0; i < ProfileRows; i++) SetVisible(profileRows[i], false);

        bool freezeNativeSnapshot = nativePortraitPreloadStarted && !nativePortraitPreloadComplete &&
            nativeProfileSnapshot != null && nativeProfileSnapshot.Count > 0;
        List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> snapshot;
        if (!freezeNativeSnapshot && RebirthNativePlayerProfileBridge.TryGetEmbeddedPlayerProfiles(this, out snapshot))
        {
            // A native Player Profile can already be selected before the Survivor editor opens.
            // Adopt that native selection when this Survivor build does not yet have an
            // appearance association, so the row that owns the visible SDCS preview starts
            // selected and is promoted to the top immediately.
            if (!model.HasPlayerProfileSelection)
            {
                string nativeName;
                string nativeSource;
                if (RebirthNativePlayerProfileBridge.TryResolveEmbeddedPlayerProfileName(this, out nativeName, out nativeSource) &&
                    !string.IsNullOrEmpty(nativeName))
                {
                    model.SetPlayerProfileName(nativeName);
                    { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EMBEDDED initial selection='" + nativeName + "' source=" + nativeSource); }
                }
            }
            nativeProfileSnapshot = OrderNativeProfiles(snapshot, model.PlayerProfileName);
            EnsureDraftPlayerProfileExists();
        }
        else if (nativeProfileSnapshot == null)
            nativeProfileSnapshot = new List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo>();

        nativeProfileCount = nativeProfileSnapshot.Count;
        string selectedName = (model.PlayerProfileName ?? string.Empty).Trim();
        if (!string.Equals(nativeProfileLastSelection, selectedName, StringComparison.OrdinalIgnoreCase))
        {
            nativeProfileLastSelection = selectedName;
            nativeProfileScrollOffset = 0;
        }
        nativeProfileScrollOffset = ClampOffset(nativeProfileScrollOffset, nativeProfileCount, NativeProfileVisibleRows);

        for (int row = 0; row < NativeProfileVisibleRows; row++)
        {
            int index = nativeProfileScrollOffset + row;
            bool visible = index >= 0 && index < nativeProfileSnapshot.Count;
            SetVisible(nativeProfileRows[row], visible);
            if (!visible)
            {
                SetLabel(nativeProfileNames[row], string.Empty);
                if (nativeProfileSelections[row] != null) nativeProfileSelections[row].IsVisible = false;
                if (nativeProfileChecks[row] != null) nativeProfileChecks[row].IsVisible = false;
                if (nativeProfilePortraits[row] != null) nativeProfilePortraits[row].Clear();
                continue;
            }

            if (nativeProfileButtons[row] != null && nativeProfileButtons[row].ViewComponent != null)
            {
                nativeProfileButtons[row].ViewComponent.EventOnPress = true;
                nativeProfileButtons[row].ViewComponent.Enabled = true;
                XUiV_Button rowButton = nativeProfileButtons[row].ViewComponent as XUiV_Button;
                if (rowButton != null) rowButton.Enabled = true;
            }

            RebirthNativePlayerProfileBridge.EmbeddedProfileInfo profile = nativeProfileSnapshot[index];
            SetLabel(nativeProfileNames[row], RebirthNativePlayerProfileBridge.DisplayProfileName(profile?.Name));
            bool selected = profile != null && string.Equals(profile.Name, selectedName, StringComparison.OrdinalIgnoreCase);
            if (nativeProfileSelections[row] != null) nativeProfileSelections[row].IsVisible = selected;
            if (nativeProfileChecks[row] != null) nativeProfileChecks[row].IsVisible = selected;
            if (profile != null && selected && !RebirthPlayerProfilePortraitBinder.IsCached(profile.Name) &&
                string.IsNullOrEmpty(pendingSelectedPortraitName) &&
                (!nativePortraitPreloadStarted || nativePortraitPreloadComplete))
            {
                ScheduleSelectedProfilePortrait(profile.Name);
            }
            if (nativeProfilePortraits[row] != null) nativeProfilePortraits[row].BindCached(profile != null ? profile.Name : string.Empty);
        }

        UpdateCreatorNativeScroll(true,nativeProfileScrollOffset,nativeProfileCount,NativeProfileVisibleRows,NativeProfileTrackHeight);
        if (playerPreviewBackgroundBinder != null)
            playerPreviewBackgroundBinder.BindLocalSurvivorAsset("rb_survivor_player_profile_preview_bg");

        UpdateNativeProfileModifyButtons(!nativePortraitPreloadStarted || nativePortraitPreloadComplete);

        if (!nativePortraitPreloadStarted && nativeProfileCount > 0)
            BeginNativeProfilePortraitPreload();
    }

    private void UpdateNativeProfileModifyButtons(bool interactionEnabled)
    {
        bool canModify = false;
        string source = string.Empty;
        string selected = model != null ? (model.PlayerProfileName ?? string.Empty).Trim() : string.Empty;
        if (interactionEnabled && selected.Length > 0)
            RebirthNativePlayerProfileBridge.TryCanModifyEmbeddedPlayerProfile(this, out canModify, out source);

        SetEnabled(GetChildById("btnProfileEdit"), interactionEnabled && canModify);
        SetEnabled(GetChildById("btnProfileDelete"), interactionEnabled && canModify);

        if (!string.Equals(nativeModifyStateProfileName, selected, StringComparison.OrdinalIgnoreCase) || nativeModifyState != canModify)
        {
            nativeModifyStateProfileName = selected;
            nativeModifyState = canModify;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("MODIFY-STATE profile='" + selected +
                "' canModify=" + canModify + " source=" + source); }
        }
    }

    private void ScheduleSelectedProfilePortrait(string profileName)
    {
        pendingSelectedPortraitName = (profileName ?? string.Empty).Trim();
        pendingSelectedPortraitReadyFrame = pendingSelectedPortraitName.Length > 0 ? Time.frameCount + 1 : -1;
    }

    /// <summary>
    /// Captures only the profile the PLAYER actually selected. This never changes the native
    /// ProfilesList selection, so portrait work cannot blank, reorder, or desynchronize the
    /// roster/live SDCS preview. One frame is allowed for SDCSPreviewWindow to rebuild first.
    /// </summary>
    private void PumpSelectedProfilePortrait()
    {
        if (string.IsNullOrEmpty(pendingSelectedPortraitName) || Time.frameCount < pendingSelectedPortraitReadyFrame) return;
        string actualName;
        string actualSource;
        if (!RebirthNativePlayerProfileBridge.TryResolveEmbeddedPlayerProfileName(this, out actualName, out actualSource) ||
            !string.Equals(actualName, pendingSelectedPortraitName, StringComparison.OrdinalIgnoreCase))
            return;

        Archetype archetype;
        string archetypeSource;
        if (RebirthNativePlayerProfileBridge.TryResolveEmbeddedPreviewArchetype(this, out archetype, out archetypeSource) && archetype != null)
        {
            bool captured = RebirthPlayerProfilePortraitBinder.CaptureNativePreview(pendingSelectedPortraitName, this, archetype);
            if (!captured) captured = RebirthPlayerProfilePortraitBinder.Capture(pendingSelectedPortraitName, archetype);
            if (captured)
            {
                nativePortraitFailureLogged.Remove(pendingSelectedPortraitName);
                { if (RebirthLogSettings.PlayerProfilePortraitLoggingEnabled) RebirthLogSettings.TracePlayerProfilePortrait("SELECTED-CAPTURE profile='" + pendingSelectedPortraitName +
                    "' selectionSource=" + actualSource + " archetypeSource=" + archetypeSource + " source=native-preview"); }
            }
        }
        else if (nativePortraitFailureLogged.Add(pendingSelectedPortraitName))
        {
            Log.Warning("[REBIRTH Survivor][PlayerProfilePortrait] selected preview Archetype unavailable profile='" + pendingSelectedPortraitName + "'.");
        }
        pendingSelectedPortraitName = string.Empty;
        pendingSelectedPortraitReadyFrame = -1;
        BindVisibleNativePortraits();
    }

    /// <summary>
    /// Warm stock/default portraits without touching native selection. Default 3.1 Player
    /// Profile names map to named SDCS Archetypes. Two are built per update after the visible
    /// rows, so scrolling quickly hits the shared cache without a selection-cycling freeze.
    /// Custom profiles are skipped until genuinely selected and captured above.
    /// </summary>
    private void BeginNativeProfilePortraitPreload()
    {
        // Do not cycle the embedded native Player Profile selection in the background.
        // That selection is also the live SDCS preview authority; changing it to manufacture
        // thumbnails can leave the Creator on the wrong model and can trigger overlapping
        // preview rebuilds.  Existing persistent portrait-cache entries remain available, and
        // an uncached portrait is captured only after the player genuinely selects that profile.
        nativePortraitPreloadStarted = true;
        nativePortraitPreloadComplete = true;
        nativePortraitPreloadCursor = nativeProfileSnapshot != null ? nativeProfileSnapshot.Count : 0;
        nativePortraitPreloadRestoreName = model != null ? (model.PlayerProfileName ?? string.Empty).Trim() : string.Empty;
        SetVisible(profilePortraitPreloadMask, false);
        SetNativeProfileInteractionEnabled(true);
        BindVisibleNativePortraits();
        { if (RebirthLogSettings.PlayerProfilePortraitLoggingEnabled) RebirthLogSettings.TracePlayerProfilePortrait("PRELOAD-SKIPPED selection-preserving cached=" +
            RebirthPlayerProfilePortraitBinder.GetCachedCount() + " profiles=" + nativeProfileCount); }
    }

    private void PumpNativeProfilePortraitPreload()
    {
        // Intentionally no-op. Portrait generation must never mutate ProfileSDF/native selection
        // behind the player's back. PumpSelectedProfilePortrait owns uncached capture.
    }

    private void CompleteNativeProfilePortraitPreload()
    {
        nativePortraitPreloadComplete = true;
        SetVisible(profilePortraitPreloadMask, false);
        SetNativeProfileInteractionEnabled(true);
        BindVisibleNativePortraits();
    }

    private void SetNativeProfileInteractionEnabled(bool enabled)
    {
        for (int i = 0; i < nativeProfileButtons.Length; i++)
            SetEnabled(nativeProfileButtons[i], enabled);
        SetEnabled(btnRebirthProfileCreate, enabled);
        UpdateNativeProfileModifyButtons(enabled);
        SetEnabled(profileScrollTrack, enabled);
        SetEnabled(profileScrollThumbControl, enabled);
    }

    private void BindVisibleNativePortraits()
    {
        if (nativeProfileSnapshot == null) return;
        for (int row = 0; row < NativeProfileVisibleRows; row++)
        {
            int index = nativeProfileScrollOffset + row;
            RebirthPlayerProfilePortraitBinder binder = nativeProfilePortraits[row];
            if (binder == null) continue;
            if (index < 0 || index >= nativeProfileSnapshot.Count || nativeProfileSnapshot[index] == null)
            {
                binder.ClearView();
                continue;
            }
            binder.BindCached(nativeProfileSnapshot[index].Name);
        }
    }

    private static List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> OrderNativeProfiles(
        List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> source, string selectedName)
    {
        List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> ordered = new List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo>();
        if (source == null) return ordered;
        string selected = (selectedName ?? string.Empty).Trim();
        if (selected.Length > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                RebirthNativePlayerProfileBridge.EmbeddedProfileInfo profile = source[i];
                if (profile != null && !IsInternalTemporaryPlayerProfile(profile.Name) &&
                    string.Equals(profile.Name, selected, StringComparison.OrdinalIgnoreCase))
                {
                    ordered.Add(profile);
                    break;
                }
            }
        }
        for (int i = 0; i < source.Count; i++)
        {
            RebirthNativePlayerProfileBridge.EmbeddedProfileInfo profile = source[i];
            if (profile == null || string.IsNullOrEmpty(profile.Name) || IsInternalTemporaryPlayerProfile(profile.Name)) continue;
            bool duplicate = false;
            for (int j = 0; j < ordered.Count; j++)
                if (string.Equals(ordered[j].Name, profile.Name, StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }
            if (!duplicate) ordered.Add(profile);
        }
        return ordered;
    }

    private static bool IsInternalTemporaryPlayerProfile(string name)
    {
        return RebirthNativePlayerProfileBridge.IsOwnedTemporaryProfile(name);
    }

    private void RenderBackgrounds()
    {
        RebirthBackgroundDefinition[] page = model.GetBackgroundPage(BackgroundRows);
        for (int i = 0; i < BackgroundRows; i++)
        {
            bool visible = i < page.Length;
            SetVisible(backgroundRows[i], visible);
            if (!visible)
            {
                SetLabel(backgroundNames[i], string.Empty);
                SetLabel(backgroundSummaries[i], string.Empty);
                if (backgroundButtons[i]?.ViewComponent != null) backgroundButtons[i].ViewComponent.ToolTip = string.Empty;
                if (backgroundSelections[i] != null) backgroundSelections[i].IsVisible = false;
                if (backgroundRowArtBinders[i] != null) backgroundRowArtBinders[i].Clear();
                SetVisible(backgroundRowArt[i], false);
                SetVisible(backgroundRowArtPlaceholder[i], false);
                continue;
            }
            RebirthBackgroundDefinition bg = page[i];
            bool selected = string.Equals(bg.Id, model.BackgroundId, StringComparison.OrdinalIgnoreCase);
            string backgroundListName = RebirthSurvivorUiText.L(bg.NameKey, bg.Id);
            if (string.Equals(bg.Id, "background.clean_slate", StringComparison.OrdinalIgnoreCase))
                backgroundListName = "(" + backgroundListName + ")";
            SetLabel(backgroundNames[i], backgroundListName);
            string backgroundSummary = BuildBackgroundListSummary(bg);
            SetLabel(backgroundSummaries[i], backgroundSummary);
            if (backgroundButtons[i]?.ViewComponent != null) backgroundButtons[i].ViewComponent.ToolTip = backgroundSummary;
            if (backgroundSelections[i] != null) backgroundSelections[i].IsVisible = selected;
            bool hasArt = backgroundRowArtBinders[i] != null && backgroundRowArtBinders[i].BindBackgroundThumbnail(bg);
            SetVisible(backgroundRowArt[i], hasArt);
            SetVisible(backgroundRowArtPlaceholder[i], !hasArt);
        }
        int total = model.GetBackgroundCount();
        SetLabel(backgroundPageText, string.Format(CultureInfo.InvariantCulture,
            RebirthSurvivorUiText.L("xuiRebirthSurvivorBackgroundCountFormat", "{0} Experiences"), total));
        UpdateCreatorNativeScroll(false,model.BackgroundOffset,total,BackgroundRows,BackgroundTrackHeight);
    }

    private static string BuildBackgroundListSummary(RebirthBackgroundDefinition bg)
    {
        int positive = 0;
        int negative = 0;
        if (bg != null)
        {
            for (int i = 0; i < bg.StartingSkills.Count; i++)
            {
                RebirthStartingSkillBiasDefinition authored = bg.StartingSkills[i];
                if (authored == null) continue;
                int value = authored.HasExplicitValue ? (int)Math.Round(authored.Value) : GetStartingTierValue(authored.TierId);
                if (value > 0) positive += value;
                else if (value < 0) negative += value;
            }
        }

        int traitPoints = GetBackgroundStartingTraitPoints(bg);
        string skillText = RebirthSurvivorUiText.L("xuiRebirthSurvivorBackgroundListSkillPoints", "Starting Skills");
        string weaknessText = RebirthSurvivorUiText.L("xuiRebirthSurvivorBackgroundListWeaknessPoints", "Weaknesses");
        string traitText = RebirthSurvivorUiText.L("xuiRebirthSurvivorBackgroundListBonusTraitPoints", "Bonus Trait Points");
        return string.Format(CultureInfo.InvariantCulture,
            "[8FD18F]{0} +{1}[-]    [CC6B64]{2} {3}[-]    [D6C978]{4} +{5}[-]",
            skillText, positive, weaknessText, negative, traitText, traitPoints);
    }

    private void RenderTraits()
    {
        RebirthSurvivorTraitChoice[] positivePage = model.GetPositiveTraitPage(TraitSideRows);
        RebirthSurvivorTraitChoice[] negativePage = model.GetNegativeTraitPage(TraitSideRows);
        RenderTraitSide(positivePage, positiveTraitRows, positiveTraitIcons, positiveTraitSelections,
            positiveTraitNames, positiveTraitSummaries, positiveTraitPoints, positiveTraitActions, false);
        RenderTraitSide(negativePage, negativeTraitRows, negativeTraitIcons, negativeTraitSelections,
            negativeTraitNames, negativeTraitSummaries, negativeTraitPoints, negativeTraitActions, true);

        int positiveTotal = model.GetPositiveTraitCount();
        int negativeTotal = model.GetNegativeTraitCount();
        SetLabel(positiveTraitPageText, PageText(model.PositiveTraitOffset, positivePage.Length, positiveTotal, TraitSideRows));
        SetLabel(negativeTraitPageText, PageText(model.NegativeTraitOffset, negativePage.Length, negativeTotal, TraitSideRows));
        UpdateTraitNativeScroll(false, model.PositiveTraitOffset, positiveTotal);
        UpdateTraitNativeScroll(true, model.NegativeTraitOffset, negativeTotal);

        for (int i = 0; i < traitCategoryButtons.Length; i++)
            SetSelected(traitCategoryButtons[i], (int)model.TraitCategoryFilter == i);

        int remaining = RebirthSurvivorUiText.GetTraitPointsRemaining(model);
        string remainingColor = remaining < 0 ? "E75A5A" : (remaining == 0 ? "D6C978" : "8FD18F");
        SetLabel(traitAvailablePoints, "[" + remainingColor + "]" + remaining.ToString(CultureInfo.InvariantCulture) + "[-]");
        SetLabel(traitBudgetText, RebirthSurvivorUiText.BuildTraitBudgetCompact(model));
        SetLabel(traitSelectedHeading, RebirthSurvivorUiText.L("xuiRebirthSurvivorSelectedTraits", "SELECTED TRAITS") +
            " (" + model.SelectedTraitIds.Count.ToString(CultureInfo.InvariantCulture) + ")");
        string selectedTraitSummary = RebirthSurvivorUiText.BuildSelectedTraitSummary(model);
        SetLabel(traitSelectedText, selectedTraitSummary);
        if (traitSelectedText != null)
        {
            int selectedTraitLines = 1;
            for (int i = 0; i < selectedTraitSummary.Length; i++) if (selectedTraitSummary[i] == '\n') selectedTraitLines++;
            // The native scroll view measures its child height. resizeheight alone could stop
            // at the original 118px authoring height, causing later selected Traits to exist in
            // the model but remain unreachable. Give the label an explicit content extent.
            int selectedTraitContentHeight = Math.Max(118, selectedTraitLines * 23 + 8);
            traitSelectedText.Size = new Vector2i(500, selectedTraitContentHeight);
            // An explicit one-pixel proxy is used as well because the 7DTD native ScrollView
            // can cache the authored label extent. The proxy guarantees the scrollbar's full
            // range tracks every selected Trait, including long 20+ Trait lists.
            if (traitSelectedNativeScrollProxy != null && traitSelectedNativeScrollProxy.ViewComponent != null)
                traitSelectedNativeScrollProxy.ViewComponent.Size = new Vector2i(1, selectedTraitContentHeight);
        }

        if (string.IsNullOrEmpty(focusedTraitId) || model.GetTrait(focusedTraitId) == null)
        {
            if (positivePage.Length > 0 && positivePage[0].Definition != null) focusedTraitId = positivePage[0].Definition.Id;
            else if (negativePage.Length > 0 && negativePage[0].Definition != null) focusedTraitId = negativePage[0].Definition.Id;
        }
        RebirthTraitDefinition focused = model.GetTrait(focusedTraitId);
        if (focused != null)
        {
            SetSprite(traitFocusedIcon, focused.IconKey);
            SetLabel(traitFocusedDetails, RebirthSurvivorUiText.BuildTraitDetails(model.GetTraitChoice(focused.Id), null));
        }
        else
        {
            SetSprite(traitFocusedIcon, string.Empty);
            SetLabel(traitFocusedDetails, RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitHelp",
                "Choose enduring positive, negative, and mixed Traits. Positive Traits spend points; negative Traits grant points."));
        }

        XUiController removeUnavailable = GetChildById("btnRemoveUnavailableTraits");
        int unresolvedCount = model.GetUnresolvedTraitIds().Count;
        SetVisible(removeUnavailable, !model.IsReadOnly && unresolvedCount > 0);
        if (removeUnavailable != null && unresolvedCount > 0)
            SetButtonText(removeUnavailable, string.Format(CultureInfo.InvariantCulture, RebirthSurvivorUiText.L("xuiRebirthSurvivorRemoveUnavailableFormat", "REMOVE UNAVAILABLE ({0})"), unresolvedCount));
        SetEnabled(btnResetTraits, !model.IsReadOnly && model.SelectedTraitIds.Count > 0);

        // Retain the legacy bindings as invisible compatibility surfaces for static tests/older layouts.
        SetLabel(traitFilterText, model.TraitCategoryFilter.ToString());
        SetLabel(traitPageText, (positiveTotal + negativeTotal).ToString(CultureInfo.InvariantCulture));
    }

    private void RenderTraitSide(RebirthSurvivorTraitChoice[] page, XUiController[] rows, XUiV_Sprite[] icons,
        XUiV_Sprite[] selections, XUiV_Label[] names, XUiV_Label[] summaries, XUiV_Label[] points,
        XUiV_Label[] actions, bool negativeSide)
    {
        for (int i = 0; i < TraitSideRows; i++)
        {
            bool visible = page != null && i < page.Length && page[i] != null && page[i].Definition != null;
            SetVisible(rows[i], visible);
            if (!visible)
            {
                SetSprite(icons[i], string.Empty);
                if (selections[i] != null) selections[i].IsVisible = false;
                SetLabel(names[i], string.Empty);
                SetLabel(summaries[i], string.Empty);
                SetLabel(points[i], string.Empty);
                SetLabel(actions[i], string.Empty);
                continue;
            }

            RebirthSurvivorTraitChoice choice = page[i];
            RebirthTraitDefinition trait = choice.Definition;
            SetSprite(icons[i], trait.IconKey);
            if (selections[i] != null) selections[i].IsVisible = choice.IsSelected;

            string badge = RebirthSkillAptitudeTraitFactory.IsAptitude(trait) ? "[B58CFF]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitBadgeAptitude", "APTITUDE") + "[-] " :
                (trait.Polarity != RebirthTraitPolarity.Positive && trait.Polarity != RebirthTraitPolarity.Negative ? "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitBadgeMixed", "MIXED") + "[-] " :
                (trait.Availability == RebirthDefinitionAvailability.Restricted ? "[E0B35C]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitBadgeBackground", "EXPERIENCE") + "[-] " : string.Empty));
            string nameColor = choice.CanToggle || choice.IsSelected ? "FFFFFF" : "858585";
            SetLabel(names[i], badge + "[" + nameColor + "]" + RebirthSurvivorUiText.TraitDisplayName(trait) + "[-]");

            string summary = BuildTraitRowSummary(choice);
            SetLabel(summaries[i], summary);
            string pointColor = negativeSide ? "8FD18F" : (trait.Points == 0 ? "D6C978" : "E75A5A");
            SetLabel(points[i], "[" + pointColor + "]" + RebirthSurvivorUiText.PointText(trait) + "[-]");
            SetLabel(actions[i], choice.IsSelected ? "[E7A15A]−[-]" : (choice.CanToggle ? "[B58CFF]+[-]" : "[707070]×[-]"));
        }
    }

    private static string BuildTraitRowSummary(RebirthSurvivorTraitChoice choice)
    {
        if (choice == null || choice.Definition == null) return string.Empty;
        if (!choice.IsEligibleForBackground) return "[707070]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitUnavailableBackground", "Unavailable for this Experience.") + "[-]";
        if (choice.HasDietConflict) return "[E7A15A]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitDietConflict", "Conflicts with the selected Diet.") + "[-]";
        if (choice.HasSelectedTraitConflict) return "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitConflictSelected", "Conflicts with a selected Trait.") + "[-]";
        string value = RebirthSurvivorUiText.TraitDescription(choice.Definition) ?? string.Empty;
        value = value.Replace("\r", " ").Replace("\n", " ").Trim();
        const int max = 78;
        return value.Length > max ? value.Substring(0, max - 1).TrimEnd() + "…" : value;
    }

    private void DietFoodSearch_OnChanged(XUiController sender, string text, bool changeFromCode)
    {
        if (changeFromCode) return;
        dietFoodSearchText = text ?? string.Empty;
        dietFoodOffset = 0;
        RenderDietDetails();
    }

    private void DietFoodFilter_OnPressed(XUiController sender, int mouseButton)
    {
        int filter;
        if (!dietFoodFilterIndex.TryGetValue(sender, out filter)) return;
        dietFoodFilter = Mathf.Clamp(filter, 0, dietFoodFilterButtons.Length - 1);
        dietFoodOffset = 0;
        RenderDietDetails();
    }


    private void DietFoodVirtualScroll(float delta)
    {
        if (delta == 0f) return;
        // Move by one visual row (four entries) so mouse-wheel navigation remains predictable
        // while still exposing logical catalogues larger than the reusable 98-row XML pool.
        int direction = delta > 0f ? -1 : 1;
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            if (!DietPagingSafe) { dietPagingAnalogDirection = 0; return; }
            dietFoodOffset = (int)RebirthScrollbarPagingPolicy.Step(dietFoodOffset,
                Math.Max(0, dietPagingTotal - dietPagingCapacity), dietPagingCapacity, direction);
        }
        else dietFoodOffset = Math.Max(0, dietFoodOffset + direction * 4);
        RenderDietDetails();
    }

    private bool DietFoodMatchesFilter(RebirthDietFoodEntry food)
    {
        if (food == null) return false;
        string query = (dietFoodSearchText ?? string.Empty).Trim();
        if (query.Length > 0 &&
            (food.DisplayName == null || food.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) &&
            (food.ItemId == null || food.ItemId.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)) return false;
        if (dietFoodFilter == 0) return true;
        if (dietFoodFilter == 1) return HasDietTag(food, "Fish");
        if (dietFoodFilter == 2) return HasDietTag(food, "Plant");
        if (dietFoodFilter == 3) return HasDietTag(food, "Egg") || HasDietTag(food, "Honey");
        if (dietFoodFilter == 4) return HasDietTag(food, "Meat") || HasDietTag(food, "AnimalFat");
        return true;
    }

    private static bool HasDietTag(RebirthDietFoodEntry food, string tag)
    {
        if (food == null || food.DietTags == null) return false;
        for (int i = 0; i < food.DietTags.Count; i++)
            if (string.Equals(food.DietTags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void RenderDiets()
    {
        dietNativeLayoutRefreshFrames=Math.Max(dietNativeLayoutRefreshFrames,2);
        RebirthDietDefinition[] page = model.GetDietPage(DietRows);
        for (int i = 0; i < DietRows; i++)
        {
            bool visible = i < page.Length;
            SetVisible(dietRows[i], visible);
            if (!visible)
            {
                SetLabel(dietNames[i], string.Empty);
                SetLabel(dietSummaries[i], string.Empty);
                SetLabel(dietPoints[i], string.Empty);
                SetSprite(dietIcons[i], string.Empty);
                if (dietSelections[i] != null) dietSelections[i].IsVisible = false;
                continue;
            }
            RebirthDietDefinition diet = page[i];
            bool selected = string.Equals(diet.Id, model.DietId, StringComparison.OrdinalIgnoreCase);
            SetLabel(dietNames[i], RebirthSurvivorUiText.L(diet.NameKey, diet.Id));
            SetLabel(dietSummaries[i], RebirthSurvivorUiText.BuildDietCompatibilityShort(diet));
            SetLabel(dietPoints[i], "[D6C978]" + RebirthSurvivorUiText.SignedPoints(diet.Points) + " pt[-]");
            SetSprite(dietIcons[i], diet.IconKey);
            if (dietSelections[i] != null) dietSelections[i].IsVisible = selected;
        }
        SetLabel(dietPageText, PageText(model.DietOffset, page.Length, model.GetDietCount(), DietRows));
    }

    private void RenderDietDetails()
    {
        RebirthDietDefinition diet = model != null ? model.GetSelectedDiet() : null;
        RebirthDietFoodEntry[] foods = RebirthDietFoodCatalogue.BuildCurrent();
        List<RebirthDietFoodEntry> allowedAll = new List<RebirthDietFoodEntry>();
        List<RebirthDietFoodEntry> restrictedAll = new List<RebirthDietFoodEntry>();
        List<RebirthDietFoodEntry> allowed = new List<RebirthDietFoodEntry>();
        List<RebirthDietFoodEntry> restricted = new List<RebirthDietFoodEntry>();

        for (int i = 0; i < foods.Length; i++)
        {
            RebirthDietFoodEntry food = foods[i];
            float dietModifier;
            bool compatible = diet == null || RebirthDietSatisfactionService.IsCompatible(diet.CompositionRule, food.DietTags, out dietModifier);
            if (compatible)
            {
                allowedAll.Add(food);
                if (DietFoodMatchesFilter(food)) allowed.Add(food);
            }
            else
            {
                restrictedAll.Add(food);
                if (DietFoodMatchesFilter(food)) restricted.Add(food);
            }
        }

        for (int i = 0; i < dietFoodFilterButtons.Length; i++) SetSelected(dietFoodFilterButtons[i], i == dietFoodFilter);

        int logicalCount = allowed.Count + restricted.Count;
        bool pagedDiet = RebirthScrollbarPagingPolicy.Enabled;
        int dietPageSlots = DietFoodSlots;
        int dietRowPitch = 70;
        if (pagedDiet)
        {
            RebirthScrollbarPagingNativeAdapter.Geometry geometry;
            float viewport = RebirthScrollbarPagingNativeAdapter.GeometryFor(
                (dietDetailNativeScrollView?.ViewComponent as XUiV_ScrollView)?.scrollView, out geometry)
                ? geometry.Height : 522f;
            // Reserve both section headers and the split row when both categories exist.
            int headerSpace = (allowed.Count > 0 ? Math.Max(38, dietAllowedFoodsHeading?.ViewComponent?.Size.y ?? 38) + 18 : 0)
                + (restricted.Count > 0 ? Math.Max(40, dietRestrictedFoodsHeading?.ViewComponent?.Size.y ?? 40) + 18 : 0);
            for (int i = 0; i < dietFoodNames.Length; i++)
                if (dietFoodNames[i] != null) dietRowPitch = Math.Max(dietRowPitch, dietFoodNames[i].Size.y + 18);
            int rows = Math.Max(1, (int)Math.Floor((viewport - headerSpace - 18) / dietRowPitch));
            if (allowed.Count > 0 && restricted.Count > 0) rows = Math.Max(1, rows - 1);
            dietPageSlots = Math.Min(DietFoodSlots, rows * 4);
        }
        dietPagingCapacity = dietPageSlots; dietPagingTotal = logicalCount;
        int maxOffset = Math.Max(0, logicalCount - dietPageSlots);
        if (pagedDiet) dietFoodOffset = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(dietFoodOffset, maxOffset, dietPageSlots, true);
        dietFoodOffset = Math.Max(0, Math.Min(dietFoodOffset, maxOffset));
        // Keep wheel movement aligned to visual rows without making the final partial page unreachable.
        if (dietFoodOffset < maxOffset && dietFoodOffset % 4 != 0) dietFoodOffset -= dietFoodOffset % 4;

        const int columns = 4;
        const int columnWidth = 230;
        int rowSpacing = pagedDiet ? dietRowPitch : 70;
        const int firstHeadingY = 0;

        string allowedCount = allowed.Count == allowedAll.Count ? allowedAll.Count.ToString(CultureInfo.InvariantCulture) : allowed.Count.ToString(CultureInfo.InvariantCulture) + " / " + allowedAll.Count.ToString(CultureInfo.InvariantCulture);
        string restrictedCount = restricted.Count == restrictedAll.Count ? restrictedAll.Count.ToString(CultureInfo.InvariantCulture) : restricted.Count.ToString(CultureInfo.InvariantCulture) + " / " + restrictedAll.Count.ToString(CultureInfo.InvariantCulture);

        int pageStart = dietFoodOffset;
        int pageEnd = Math.Min(logicalCount, pageStart + dietPageSlots);
        int allowedPageStart = Math.Min(allowed.Count, pageStart);
        int allowedPageEnd = Math.Min(allowed.Count, pageEnd);
        int allowedRendered = Math.Max(0, allowedPageEnd - allowedPageStart);
        int restrictedPageStart = Math.Max(0, pageStart - allowed.Count);
        int restrictedPageEnd = Math.Max(0, pageEnd - allowed.Count);
        int restrictedRendered = Math.Max(0, Math.Min(restricted.Count, restrictedPageEnd) - Math.Min(restricted.Count, restrictedPageStart));

        int slot = 0;
        int cursorY = firstHeadingY;
        if (allowedRendered > 0)
        {
            SetButtonText(dietAllowedFoodsHeading, RebirthSurvivorUiText.L("xuiRebirthSurvivorDietAllowedFoods", "ALLOWED FOODS") + "  [8FD18F](" + allowedCount + ")[-]");
            SetControllerPosition(dietAllowedFoodsHeading, 0, cursorY);
            SetVisible(dietAllowedFoodsHeading, true);
            int startY = cursorY - 38;
            for (int i = 0; i < allowedRendered && slot < DietFoodSlots; i++, slot++)
            {
                RebirthDietFoodEntry food = allowed[allowedPageStart + i];
                SetVisible(dietFoodRows[slot], true);
                SetControllerPosition(dietFoodRows[slot], (i % columns) * columnWidth, startY - (i / columns) * rowSpacing);
                SetLabel(dietFoodNames[slot], "[8FD18F]✓[-] " + food.DisplayName);
                SetItemSprite(dietFoodIcons[slot], food.IconName, food.IconTint);
            }
            int rows = (allowedRendered + columns - 1) / columns;
            cursorY = startY - rows * rowSpacing - 18;
        }
        else SetVisible(dietAllowedFoodsHeading, false);

        if (restrictedRendered > 0)
        {
            SetButtonText(dietRestrictedFoodsHeading, RebirthSurvivorUiText.L("xuiRebirthSurvivorDietRestrictedFoods", "RESTRICTED FOODS") + "  [CC6B64](" + restrictedCount + ")[-]");
            SetControllerPosition(dietRestrictedFoodsHeading, 0, cursorY);
            SetVisible(dietRestrictedFoodsHeading, true);
            int startY = cursorY - 40;
            for (int i = 0; i < restrictedRendered && slot < DietFoodSlots; i++, slot++)
            {
                RebirthDietFoodEntry food = restricted[restrictedPageStart + i];
                SetVisible(dietFoodRows[slot], true);
                SetControllerPosition(dietFoodRows[slot], (i % columns) * columnWidth, startY - (i / columns) * rowSpacing);
                SetLabel(dietFoodNames[slot], "[CC6B64]⊘[-] " + food.DisplayName);
                SetItemSprite(dietFoodIcons[slot], food.IconName, food.IconTint);
            }
            int rows = (restrictedRendered + columns - 1) / columns;
            cursorY = startY - rows * rowSpacing - 18;
        }
        else SetVisible(dietRestrictedFoodsHeading, false);

        for (int i = slot; i < DietFoodSlots; i++)
        {
            SetVisible(dietFoodRows[i], false);
            SetLabel(dietFoodNames[i], string.Empty);
            SetItemSprite(dietFoodIcons[i], string.Empty, Color.white);
        }

        if (pagedDiet && dietPagingBar != null)
        {
            dietPagingSyncing = true;
            try
            {
                UIScrollBar thumb = dietPagingBar as UIScrollBar;
                if (thumb != null) thumb.barSize = RebirthScrollbarPresentation.NativeFraction(Mathf.RoundToInt(dietPagingView?.panel?.height ?? 542),dietPageSlots,logicalCount);
                dietPagingBarValue = maxOffset == 0 ? 0f : (float)dietFoodOffset / maxOffset;
                dietPagingBar.value = dietPagingBarValue;
                // Authored native visibility/fade consumes the published logical overflow.
            }
            finally { dietPagingSyncing = false; }
        }
        int fittedContentHeight = Math.Max(542, -cursorY + 18);
        SetControllerSize(dietDetailScrollContent, 930, fittedContentHeight);

        string selectedName = diet != null ? RebirthSurvivorUiText.L(diet.NameKey, diet.Id) : RebirthSurvivorUiText.L("xuiRebirthSurvivorChooseDietPrompt", "Choose a Diet");
        string selectedSummaryText = diet != null ? RebirthSurvivorUiText.L(diet.DescriptionKey, diet.RuleSummary) : string.Empty;
        string ruleDetailText = diet != null ? RebirthSurvivorUiText.L(diet.DescriptionKey + ".rule", diet.RuleSummary) : string.Empty;
        string effectsText = BuildDietEffects(diet, allowedAll.Count, restrictedAll.Count);
        string benefitsText = BuildDietBenefits(diet);
        string challengesText = BuildDietChallenges(diet);
        string legendText = RebirthSurvivorUiText.L("xuiRebirthSurvivorDietFoodListLegend",
            "Allowed foods follow this diet. Restricted foods remain edible but reduce Diet Satisfaction/Mood when absorbed.");
        if (logicalCount > dietPageSlots)
            legendText += "\n" + "Showing " + (pageStart + 1).ToString(CultureInfo.InvariantCulture) + "–" + pageEnd.ToString(CultureInfo.InvariantCulture) +
                " of " + logicalCount.ToString(CultureInfo.InvariantCulture) + ". Scroll the food list to view more.";

        SetLabel(dietSelectedName, selectedName);
        SetLabel(dietSelectedSummary, selectedSummaryText);
        SetLabel(dietPointBonus, diet != null ? RebirthSurvivorUiText.SignedPoints(diet.Points) + " " + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitPoints", "Trait Points") : string.Empty);
        SetLabel(dietEffectsText, effectsText);
        SetLabel(dietBenefitsText, benefitsText);
        SetLabel(dietChallengesText, challengesText);
        SetButtonText(dietFoodLegend, legendText);
        if (dietDetailText != null) dietDetailText.Text = ruleDetailText;

        // Reuse the established summary layout code below the food list.
        LayoutDietSummaryPanels(selectedSummaryText, ruleDetailText, effectsText, benefitsText, challengesText, legendText);
        dietNativeLayoutRefreshFrames=Math.Max(dietNativeLayoutRefreshFrames,3);
    }

    private void LayoutDietSummaryPanels(string selectedSummaryText, string ruleDetailText, string effectsText, string benefitsText, string challengesText, string legendText)
    {
        const int contentWidth=314,bodyWidth=294,sectionGap=12;
        int summaryHeight=EstimateLocalizedTextHeight(selectedSummaryText,32,20,40);
        if(dietSelectedSummary!=null)dietSelectedSummary.Size=new Vector2i(294,summaryHeight);
        int ruleHeadingY=summaryHeight+30;
        SetControllerPosition(dietRuleHeading,4,-ruleHeadingY);
        int ruleTextY=ruleHeadingY+26;
        int ruleHeight=EstimateLocalizedTextHeight(ruleDetailText,32,20,44);
        if(dietDetailText!=null){dietDetailText.Position=new Vector2i(4,-ruleTextY);dietDetailText.Size=new Vector2i(294,ruleHeight);}

        int effectsY=ruleTextY+ruleHeight+12;
        int effectsBodyHeight=EstimateLocalizedTextHeight(effectsText,32,20,152);
        int effectsPanelHeight=Math.Max(208,44+effectsBodyHeight+12);
        PositionDietSection(dietEffectsPanel,dietEffectsText,effectsY,effectsPanelHeight,effectsBodyHeight);

        int benefitsY=effectsY+effectsPanelHeight+sectionGap;
        int benefitsBodyHeight=EstimateLocalizedTextHeight(benefitsText,32,20,78);
        int benefitsPanelHeight=Math.Max(134,44+benefitsBodyHeight+12);
        PositionDietSection(dietBenefitsPanel,dietBenefitsText,benefitsY,benefitsPanelHeight,benefitsBodyHeight);

        int challengesY=benefitsY+benefitsPanelHeight+sectionGap;
        int challengesBodyHeight=EstimateLocalizedTextHeight(challengesText,32,20,78);
        int challengesPanelHeight=Math.Max(134,44+challengesBodyHeight+12);
        PositionDietSection(dietChallengesPanel,dietChallengesText,challengesY,challengesPanelHeight,challengesBodyHeight);

        int legendY=challengesY+challengesPanelHeight+sectionGap;
        int legendTextHeight=EstimateLocalizedTextHeight(legendText,32,20,70);
        int legendPanelHeight=Math.Max(94,24+legendTextHeight);
        SetControllerPosition(dietLegendPanel,0,-legendY);
        SetControllerSize(dietLegendPanel,contentWidth,legendPanelHeight);
        SetControllerSize(dietFoodLegend,bodyWidth,legendTextHeight);
        SetControllerSize(dietSummaryScrollContent,contentWidth,legendY+legendPanelHeight+12);
    }

    private static void PositionDietSection(XUiController panel,XUiV_Label textLabel,int y,int panelHeight,int bodyHeight)
    {
        SetControllerPosition(panel,0,-y);
        SetControllerSize(panel,304,panelHeight);
        if(panel!=null&&panel.Children!=null&&panel.Children.Count>0)
            SetControllerSize(panel.Children[0],304,panelHeight);
        if(textLabel!=null)textLabel.Size=new Vector2i(284,bodyHeight);
    }

    private static int EstimateLocalizedTextHeight(string text,int charactersPerLine,int lineHeight,int minimumHeight)
    {
        if(string.IsNullOrEmpty(text))return minimumHeight;
        int lines=0;
        string[] logicalLines=text.Replace("\r",string.Empty).Split('\n');
        for(int i=0;i<logicalLines.Length;i++)
        {
            int visibleLength=VisibleTextLength(logicalLines[i]);
            lines+=Math.Max(1,(visibleLength+charactersPerLine-1)/charactersPerLine);
        }
        return Math.Max(minimumHeight,lines*lineHeight+4);
    }

    private static int VisibleTextLength(string text)
    {
        if(string.IsNullOrEmpty(text))return 0;
        int length=0;bool inTag=false;
        for(int i=0;i<text.Length;i++)
        {
            char c=text[i];
            if(c=='['){inTag=true;continue;}
            if(inTag){if(c==']')inTag=false;continue;}
            length++;
        }
        return length;
    }

    private static string BuildDietEffects(RebirthDietDefinition diet, int allowed, int restricted)
    {
        if (diet == null) return string.Empty;
        return "[8FD18F]" + allowed.ToString(CultureInfo.InvariantCulture) + "[-] " + RebirthSurvivorUiText.L("xuiRebirthSurvivorDietAllowedFoods", "Allowed Foods") + "\n" +
            "[CC6B64]" + restricted.ToString(CultureInfo.InvariantCulture) + "[-] " + RebirthSurvivorUiText.L("xuiRebirthSurvivorDietRestrictedFoods", "Restricted Foods") + "\n" +
            "[D6C978]" + RebirthSurvivorUiText.SignedPoints(diet.Points) + "[-] " + RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitPoints", "Trait Points") + "\n\n" +
            RebirthSurvivorUiText.L("xuiRebirthSurvivorDietOffDietCompact", "Restricted food stays edible, but reduces Diet Satisfaction/Mood when absorbed.");
    }

    private static string BuildDietBenefits(RebirthDietDefinition diet)
    {
        if (diet == null) return string.Empty;
        switch ((diet.CompositionRule ?? string.Empty).ToLowerInvariant())
        {
            case "pescatarian": return "• Fish and seafood remain compatible\n• Plant foods, eggs, honey and dairy remain compatible\n• +" + diet.Points + " starting Trait Points";
            case "carnivore": return "• Meat, fish and animal-origin foods remain compatible\n• +" + diet.Points + " starting Trait Points";
            case "vegan": return "• Plant-only foods are compatible\n• Largest starting Trait-point bonus: +" + diet.Points;
            default: return "• No dietary restrictions\n• Widest compatible food selection";
        }
    }

    private static string BuildDietChallenges(RebirthDietDefinition diet)
    {
        if (diet == null) return string.Empty;
        switch ((diet.CompositionRule ?? string.Empty).ToLowerInvariant())
        {
            case "pescatarian": return "• Land-animal meat is restricted\n• Rendered animal fat is restricted";
            case "carnivore": return "• Foods containing plant ingredients are restricted\n• Mixed recipes require attention";
            case "vegan": return "• Meat, fish, eggs, honey, dairy and animal fat are restricted\n• Mixed recipes require careful planning";
            default: return "• No Trait-point bonus from Diet";
        }
    }

    private void RenderDetails()
    {
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        bool backgroundStep = model.Step == RebirthSurvivorCreatorStep.Background;
        bool dietStep = model.Step == RebirthSurvivorCreatorStep.Diet;
        bool reviewStep = model.Step == RebirthSurvivorCreatorStep.Review;

        SetVisible(backgroundVisualDetailsPanel, backgroundStep);
        SetVisible(dietDetailsPanel, dietStep);
        SetVisible(reviewVisualDetailsPanel, reviewStep);
        SetVisible(creatorDetailScroll, !backgroundStep && !dietStep && !reviewStep);

        if (backgroundStep)
        {
            RenderBackgroundVisualDetails(bg);
            SetLabel(detailText, string.Empty);
            SetLabel(dietDetailText, string.Empty);
        }
        else if (dietStep)
        {
            RenderDietDetails();
            SetLabel(detailText, string.Empty);
        }
        else if (reviewStep)
        {
            RenderReviewVisual();
            SetLabel(detailText, string.Empty);
            SetLabel(dietDetailText, string.Empty);
        }
        else
        {
            SetLabel(dietDetailText, string.Empty);
            string text;
            switch (model.Step)
            {
                case RebirthSurvivorCreatorStep.Profile:
                    text = RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileDetail",
                        "PLAYER PROFILE\n\nThe base-game Player Profile controls your character's appearance. Select an existing default/custom Player Profile or create one using the native 7 Days to Die Player Profile screen.\n\nYour Rebirth Survivor Profile stores the association, but Experience, Traits, Diet, Attributes, Skills, and progression are separate from appearance.");
                    break;
                case RebirthSurvivorCreatorStep.Traits:
                    text = string.Empty;
                    break;
                default:
                    text = string.Empty;
                    break;
            }
            SetLabel(detailText, text);
        }

        bool hasArt = backgroundStep && artBinder != null && artBinder.BindBackground(bg);
        SetVisible(backgroundArtTexture, backgroundStep && hasArt);
        SetVisible(backgroundArtPlaceholder, backgroundStep && !hasArt);
        if (backgroundArtAlt != null)
            backgroundArtAlt.Text = bg != null
                ? RebirthSurvivorUiText.L(bg.BackgroundArtAltTextKey, RebirthSurvivorUiText.L(bg.NameKey, bg.Id))
                : string.Empty;
    }

    private void RenderReviewVisual()
    {
        if (model == null) return;
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        RebirthDietDefinition diet = model.GetSelectedDiet();

        string survivorProfileName = (model.ProfileName ?? string.Empty).Trim();
        if (survivorProfileName.Length == 0)
            survivorProfileName = RebirthSurvivorUiText.L("xuiRebirthSurvivorNewProfile", "New Survivor Profile");
        SetLabel(reviewProfileName, survivorProfileName);
        SetLabel(reviewBackgroundName, bg != null ? RebirthSurvivorUiText.L(bg.NameKey, bg.Id) : RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected"));
        SetLabel(reviewDietName, diet != null ? RebirthSurvivorUiText.L(diet.NameKey, diet.Id) : RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected"));
        SetLabel(reviewSummaryDescription, bg != null ? RebirthSurvivorUiText.L(bg.DescriptionKey, bg.Identity) : string.Empty);
        string reviewBackgroundId = bg != null ? bg.Id : string.Empty;
        SetSprite(reviewSignatureBonusIcon, RebirthSurvivorUiText.SignatureBonusIcon(reviewBackgroundId));
        SetLabel(reviewSignatureBonusName, RebirthSurvivorUiText.SignatureBonusName(reviewBackgroundId));
        SetLabel(reviewSignatureBonusDescription, RebirthSurvivorUiText.SignatureBonusDescription(reviewBackgroundId));
        SetLabel(reviewSummaryPlayerProfileMeta,
            "[8F8F98]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileTitle", "PLAYER PROFILE").ToUpperInvariant() + "[-]  " +
            (model.HasPlayerProfileSelection ? model.PlayerProfileName : RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected")));

        bool hasBackgroundArt = bg != null && reviewBackgroundArtBinder != null && reviewBackgroundArtBinder.BindBackgroundThumbnail(bg);
        SetVisible(reviewBackgroundArt, hasBackgroundArt);
        SetVisible(reviewBackgroundArtPlaceholder, !hasBackgroundArt);

        string selectedPlayerProfile = model.HasPlayerProfileSelection ? model.PlayerProfileName : string.Empty;
        if (!string.IsNullOrEmpty(selectedPlayerProfile) && !string.Equals(reviewPreviewPlayerProfileName, selectedPlayerProfile, StringComparison.OrdinalIgnoreCase))
        {
            string previewError;
            if (RebirthNativePlayerProfileBridge.TrySelectPlayerProfileForPreview(this, "reviewPlayerPreview", selectedPlayerProfile, out previewError))
                reviewPreviewPlayerProfileName = selectedPlayerProfile;
            else if (RebirthSurvivorDebug.Enabled)
                Log.Warning("[REBIRTH Survivor][Review] SDCS preview failed profile='" + selectedPlayerProfile + "' error=" + previewError);
        }

        // Review mirrors Survivor Profiles > Profile Summary. Every variable-length column is
        // independently scrollable, while the fixed two Experience weaknesses remain anchored.
        List<BackgroundSkillDisplay> positiveSkills = new List<BackgroundSkillDisplay>();
        List<BackgroundSkillDisplay> weaknesses = new List<BackgroundSkillDisplay>();
        if (bg != null && bg.StartingSkills != null)
        {
            for (int i = 0; i < bg.StartingSkills.Count; i++)
            {
                RebirthStartingSkillBiasDefinition authored = bg.StartingSkills[i];
                if (authored == null || string.IsNullOrEmpty(authored.SkillId)) continue;
                int value = authored.HasExplicitValue ? (int)Math.Round(authored.Value) : GetStartingTierValue(authored.TierId);
                if (value == 0) continue;
                BackgroundSkillDisplay display = new BackgroundSkillDisplay
                {
                    SkillId = authored.SkillId,
                    Name = RebirthSurvivorUiText.ResolveDefinitionName(authored.SkillId),
                    Value = value
                };
                if (value > 0) positiveSkills.Add(display); else weaknesses.Add(display);
            }
        }

        List<string> knowledge = bg != null
            ? RebirthSurvivorUiText.OrderedStartingKnowledgeIds(bg.StartingKnowledgeIds)
            : new List<string>();

        // Skills + Knowledge are one logical list using the ten physical Skill rows as display
        // slots. This lets the standard scrollbar reveal any future overflow without requiring
        // more XML rows. Knowledge keeps its own icon/value and deep-link target.
        reviewProgressionTotal = positiveSkills.Count + knowledge.Count;
        reviewProgressionOffset = ClampOffset(reviewProgressionOffset, reviewProgressionTotal, ReviewSummaryProgressionRows);
        for (int i = 0; i < ReviewSkillRows; i++)
        {
            SetVisible(reviewSkillRows[i], false);
            reviewSkillExploreIds[i] = string.Empty;
            reviewSkillExploreIsKnowledge[i] = false;
        }
        for (int i = 0; i < ReviewKnowledgeRows; i++)
        {
            SetVisible(reviewKnowledgeRows[i], false);
            reviewKnowledgeExploreIds[i] = string.Empty;
        }
        for (int slot = 0; slot < ReviewSummaryProgressionRows; slot++)
        {
            int logical = reviewProgressionOffset + slot;
            if (logical >= reviewProgressionTotal || reviewSkillRows[slot] == null) continue;
            SetVisible(reviewSkillRows[slot], true);
            SetControllerPosition(reviewSkillRows[slot], 0, -40 - (slot * 29));
            if (logical < positiveSkills.Count)
            {
                BackgroundSkillDisplay entry = positiveSkills[logical];
                reviewSkillExploreIds[slot] = entry.SkillId;
                SetSprite(reviewSkillIcons[slot], RebirthSkillAptitudeTraitFactory.SkillIconKey(entry.SkillId));
                SetLabel(reviewSkillNames[slot], entry.Name);
                SetLabel(reviewSkillValues[slot], "[8FD18F]+" + entry.Value.ToString(CultureInfo.InvariantCulture) + "[-]");
            }
            else
            {
                string knowledgeId = knowledge[logical - positiveSkills.Count];
                reviewSkillExploreIds[slot] = knowledgeId;
                reviewSkillExploreIsKnowledge[slot] = true;
                SetSprite(reviewSkillIcons[slot], RebirthSurvivorUiText.StartingKnowledgeIconKey(knowledgeId));
                SetLabel(reviewSkillNames[slot], RebirthSurvivorUiText.ResolveDefinitionName(knowledgeId));
                SetLabel(reviewSkillValues[slot], "[B58CFF]KNOWN[-]");
            }
        }
        SetVisible(reviewNoSkillsText, reviewProgressionTotal == 0);
        SetVisible(reviewNoKnowledgeText, false);

        List<RebirthTraitDefinition> positiveTraits = new List<RebirthTraitDefinition>();
        List<RebirthTraitDefinition> negativeTraits = new List<RebirthTraitDefinition>();
        var selectedTraitIds = model.SelectedTraitIds;
        for (int i = 0; i < selectedTraitIds.Count; i++)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(selectedTraitIds[i], out trait) || trait == null) continue;
            if (trait.Polarity == RebirthTraitPolarity.Negative) negativeTraits.Add(trait); else positiveTraits.Add(trait);
        }
        positiveTraits.Sort(delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.L(a.NameKey, a.Id), RebirthSurvivorUiText.L(b.NameKey, b.Id));
        });
        negativeTraits.Sort(delegate(RebirthTraitDefinition a, RebirthTraitDefinition b)
        {
            return StringComparer.CurrentCultureIgnoreCase.Compare(RebirthSurvivorUiText.L(a.NameKey, a.Id), RebirthSurvivorUiText.L(b.NameKey, b.Id));
        });
        List<RebirthTraitDefinition> traits = new List<RebirthTraitDefinition>(positiveTraits.Count + negativeTraits.Count);
        traits.AddRange(positiveTraits);
        traits.AddRange(negativeTraits);
        reviewTraitTotal = traits.Count;
        reviewTraitOffset = ClampOffset(reviewTraitOffset, reviewTraitTotal, ReviewTraitRows);
        for (int slot = 0; slot < ReviewTraitRows; slot++)
        {
            int logical = reviewTraitOffset + slot;
            bool visible = logical < reviewTraitTotal;
            SetVisible(reviewTraitRows[slot], visible);
            if (!visible) continue;
            SetControllerPosition(reviewTraitRows[slot], 564, -40 - (slot * 29));
            RebirthTraitDefinition trait = traits[logical];
            bool negative = trait.Polarity == RebirthTraitPolarity.Negative;
            Color32 tint = negative ? new Color32(228,18,21,255) : new Color32(116,220,84,255);
            SetSprite(reviewTraitIcons[slot], trait.IconKey);
            SetSpriteColor(reviewTraitIcons[slot], tint);
            SetLabel(reviewTraitNames[slot], RebirthSurvivorUiText.L(trait.NameKey, trait.Id));
            SetLabel(reviewTraitValues[slot], (negative ? "[CC6B64]" : "[8FD18F]") + RebirthSurvivorUiText.PointText(trait) + " PT[-]");
        }

        for (int i = 0; i < ReviewWeaknessRows; i++)
        {
            bool visible = i < weaknesses.Count;
            SetVisible(reviewWeaknessRows[i], visible);
            if (!visible) continue;
            BackgroundSkillDisplay entry = weaknesses[i];
            SetSprite(reviewWeaknessIcons[i], RebirthSkillAptitudeTraitFactory.SkillIconKey(entry.SkillId));
            SetLabel(reviewWeaknessNames[i], entry.Name);
            SetLabel(reviewWeaknessValues[i], "[CC6B64]" + entry.Value.ToString(CultureInfo.InvariantCulture) + "[-]");
        }

        reviewStartingItemsTotal = bg != null && bg.StartingItems != null ? bg.StartingItems.Count : 0;
        reviewStartingItemsOffset = ClampOffset(reviewStartingItemsOffset, reviewStartingItemsTotal, ReviewStartingItemRows);
        for (int slot = 0; slot < ReviewStartingItemRows; slot++)
        {
            int logical = reviewStartingItemsOffset + slot;
            bool visible = logical < reviewStartingItemsTotal;
            SetVisible(reviewStartingItemRows[slot], visible);
            if (!visible)
            {
                SetLabel(reviewStartingItemNames[slot], string.Empty);
                SetLabel(reviewStartingItemMeta[slot], string.Empty);
                SetStartingItemIcon(reviewStartingItemIcons[slot], null);
                continue;
            }
            SetControllerPosition(reviewStartingItemRows[slot], 1128, -132 - (slot * 29));
            RebirthStartingItemDefinition item = bg.StartingItems[logical];
            SetLabel(reviewStartingItemNames[slot], RebirthSurvivorUiText.L(item.NameKey, item.ItemId));
            SetLabel(reviewStartingItemMeta[slot], StartingItemMeta(item));
            SetStartingItemIcon(reviewStartingItemIcons[slot], item);
        }

        UpdateReviewNativeScroll(reviewProgressionNativeScrollHost, reviewProgressionNativeScrollProxy, reviewProgressionNativeScrollView,
            reviewProgressionOffset, reviewProgressionTotal, ReviewSummaryProgressionRows, ReviewProgressionTrackHeight);
        UpdateReviewNativeScroll(reviewTraitNativeScrollHost, reviewTraitNativeScrollProxy, reviewTraitNativeScrollView,
            reviewTraitOffset, reviewTraitTotal, ReviewTraitRows, ReviewTraitTrackHeight);
        UpdateReviewNativeScroll(reviewStartingItemsNativeScrollHost, reviewStartingItemsNativeScrollProxy, reviewStartingItemsNativeScrollView,
            reviewStartingItemsOffset, reviewStartingItemsTotal, ReviewStartingItemRows, ReviewStartingItemTrackHeight);
        if (reviewProgressionTotal > ReviewSummaryProgressionRows || reviewTraitTotal > ReviewTraitRows || reviewStartingItemsTotal > ReviewStartingItemRows)
            reviewNativeLayoutRefreshFrames = Math.Max(reviewNativeLayoutRefreshFrames, 3);
    }

    private void RenderBackgroundVisualDetails(RebirthBackgroundDefinition bg)
    {
        string noSkills = RebirthSurvivorUiText.L("xuiRebirthSurvivorNoStartingSkills",
            "No Experience Skill adjustments.");
        string noKnowledge = RebirthSurvivorUiText.L("xuiRebirthSurvivorNoStartingKnowledge",
            "No starting recipes or techniques.");
        string noWeakness = RebirthSurvivorUiText.L("xuiRebirthSurvivorNoInherentWeaknesses",
            "No inherent Skill weaknesses.");

        SetLabel(backgroundVisualName, bg != null ? RebirthSurvivorUiText.L(bg.NameKey, bg.Id) : string.Empty);
        SetLabel(backgroundVisualDescription, bg != null ? RebirthSurvivorUiText.L(bg.DescriptionKey, bg.Identity) : string.Empty);
        string backgroundId = bg != null ? bg.Id : string.Empty;
        SetSprite(backgroundSignatureBonusIcon, RebirthSurvivorUiText.SignatureBonusIcon(backgroundId));
        SetLabel(backgroundSignatureBonusName, RebirthSurvivorUiText.SignatureBonusName(backgroundId));
        SetLabel(backgroundSignatureBonusDescription, RebirthSurvivorUiText.SignatureBonusDescription(backgroundId));

        List<BackgroundSkillDisplay> positive = new List<BackgroundSkillDisplay>();
        List<BackgroundSkillDisplay> negative = new List<BackgroundSkillDisplay>();

        if (bg != null)
        {
            for (int i = 0; i < bg.StartingSkills.Count; i++)
            {
                RebirthStartingSkillBiasDefinition authored = bg.StartingSkills[i];
                int value = authored == null ? 0 : (authored.HasExplicitValue ? (int)Math.Round(authored.Value) : GetStartingTierValue(authored.TierId));
                if (value == 0 || authored == null || string.IsNullOrEmpty(authored.SkillId)) continue;
                BackgroundSkillDisplay display = new BackgroundSkillDisplay
                {
                    SkillId = authored.SkillId,
                    Name = RebirthSurvivorUiText.ResolveDefinitionName(authored.SkillId),
                    Value = value
                };
                if (value < 0) negative.Add(display);
                else positive.Add(display);
            }
        }

        // Keep authored order. The Background XML is intentionally curated from most defining
        // proficiency to least defining proficiency; changing to alphabetical order would erase
        // that design signal.
        for (int i = 0; i < BackgroundSkillCards; i++)
        {
            bool visible = i < positive.Count;
            SetVisible(backgroundSkillCards[i], visible);
            if (!visible)
            {
                SetLabel(backgroundSkillNames[i], string.Empty);
                SetLabel(backgroundSkillValues[i], string.Empty);backgroundSkillExploreIds[i]=string.Empty;
                continue;
            }
            BackgroundSkillDisplay entry = positive[i];backgroundSkillExploreIds[i]=entry.SkillId;
            SetSprite(backgroundSkillIcons[i], SkillIconKey(entry.SkillId));
            SetLabel(backgroundSkillNames[i], entry.Name);
            SetLabel(backgroundSkillValues[i], "[8FD18F]+" + entry.Value.ToString(CultureInfo.InvariantCulture) + "[-]");
        }
        SetVisible(backgroundNoSkillsText, positive.Count == 0);
        SetLabel(backgroundNoSkillsText, noSkills);

        List<string> orderedBackgroundKnowledge = bg != null
            ? RebirthSurvivorUiText.OrderedStartingKnowledgeIds(bg.StartingKnowledgeIds)
            : new List<string>();
        int knowledgeCount = orderedBackgroundKnowledge.Count;
        backgroundKnowledgeOffset = ClampOffset(backgroundKnowledgeOffset, knowledgeCount, BackgroundKnowledgeRows);
        for (int i = 0; i < BackgroundKnowledgeRows; i++)
        {
            int knowledgeIndex = backgroundKnowledgeOffset + i;
            bool visible = bg != null && knowledgeIndex >= 0 && knowledgeIndex < knowledgeCount;
            SetVisible(backgroundKnowledgeRows[i], visible);
            if (!visible)
            {
                SetLabel(backgroundKnowledgeNames[i], string.Empty);backgroundKnowledgeExploreIds[i]=string.Empty;
                continue;
            }
            string id = orderedBackgroundKnowledge[knowledgeIndex];backgroundKnowledgeExploreIds[i]=id;
            SetSprite(backgroundKnowledgeIcons[i], RebirthSurvivorUiText.StartingKnowledgeIconKey(id));
            SetLabel(backgroundKnowledgeNames[i], RebirthSurvivorUiText.ResolveDefinitionName(id));
        }
        SetVisible(backgroundNoKnowledgeText, knowledgeCount == 0);
        SetLabel(backgroundNoKnowledgeText, noKnowledge);
        UpdateBackgroundKnowledgeNativeScroll(knowledgeCount);
        backgroundKnowledgeNativeLayoutRefreshFrames = knowledgeCount > BackgroundKnowledgeRows
            ? Math.Max(backgroundKnowledgeNativeLayoutRefreshFrames, 3)
            : 0;

        int startingItemCount = bg != null && bg.StartingItems != null ? bg.StartingItems.Count : 0;
        backgroundStartingItemsOffset = ClampOffset(backgroundStartingItemsOffset, startingItemCount, BackgroundStartingItemRows);
        for (int i = 0; i < BackgroundStartingItemRows; i++)
        {
            int itemIndex = backgroundStartingItemsOffset + i;
            bool visible = bg != null && bg.StartingItems != null && itemIndex >= 0 && itemIndex < bg.StartingItems.Count;
            SetVisible(backgroundStartingItemRows[i], visible);
            if (!visible)
            {
                SetLabel(backgroundStartingItemNames[i], string.Empty);
                SetLabel(backgroundStartingItemMeta[i], string.Empty);
                SetStartingItemIcon(backgroundStartingItemIcons[i], null);
                continue;
            }
            RebirthStartingItemDefinition item = bg.StartingItems[itemIndex];
            SetLabel(backgroundStartingItemNames[i], item != null ? RebirthSurvivorUiText.L(item.NameKey, item.ItemId) : string.Empty);
            SetLabel(backgroundStartingItemMeta[i], StartingItemMeta(item));
            SetStartingItemIcon(backgroundStartingItemIcons[i], item);
        }
        UpdateBackgroundStartingItemsNativeScroll(startingItemCount);
        // The host is authored active with initial overflow so <defaultscrollbar/> is
        // initialized correctly. Keep the short delayed refresh as a second safeguard for
        // runtime proxy-height changes when switching between Experiences.
        backgroundStartingItemsNativeLayoutRefreshFrames = startingItemCount > BackgroundStartingItemRows
            ? Math.Max(backgroundStartingItemsNativeLayoutRefreshFrames, 3)
            : 0;

        for (int i = 0; i < BackgroundWeaknessRows; i++)
        {
            bool visible = i < negative.Count;
            SetVisible(backgroundWeaknessRows[i], visible);
            if (!visible)
            {
                SetLabel(backgroundWeaknessNames[i], string.Empty);
                SetLabel(backgroundWeaknessValues[i], string.Empty);backgroundWeaknessExploreIds[i]=string.Empty;
                continue;
            }
            BackgroundSkillDisplay entry = negative[i];backgroundWeaknessExploreIds[i]=entry.SkillId;
            SetSprite(backgroundWeaknessIcons[i], SkillIconKey(entry.SkillId));
            SetLabel(backgroundWeaknessNames[i], entry.Name);
            SetLabel(backgroundWeaknessValues[i], "[CC6B64]" + entry.Value.ToString(CultureInfo.InvariantCulture) + "[-]");
        }
        SetVisible(backgroundNoWeaknessText, negative.Count == 0);
        SetLabel(backgroundNoWeaknessText, noWeakness);

        int traitPoints = GetBackgroundStartingTraitPoints(bg);
        string pointText = traitPoints > 0
            ? "+" + traitPoints.ToString(CultureInfo.InvariantCulture)
            : traitPoints.ToString(CultureInfo.InvariantCulture);
        SetLabel(backgroundTraitPointsValue, pointText);
    }

    private static string StartingItemMeta(RebirthStartingItemDefinition item)
    {
        if (item == null) return string.Empty;
        string text = item.HasQuality ? "Q" + item.Quality.ToString(CultureInfo.InvariantCulture) : string.Empty;
        if (item.Count > 1)
        {
            if (text.Length > 0) text += " ";
            text += "×" + item.Count.ToString(CultureInfo.InvariantCulture);
        }
        return text;
    }

    private static void SetStartingItemIcon(XUiV_Sprite sprite, RebirthStartingItemDefinition item)
    {
        if (sprite == null) return;
        if (item == null || string.IsNullOrEmpty(item.ItemId))
        {
            sprite.UIAtlas = "ItemIconAtlas";
            sprite.SetSpriteImmediately("ui_game_symbol_backpack");
            sprite.SetColorImmediately(Color.white);
            return;
        }

        string overrideAtlas;
        string overrideIcon;
        if (TryGetStartingItemIconOverride(item.ItemId, out overrideAtlas, out overrideIcon))
        {
            sprite.UIAtlas = overrideAtlas;
            sprite.SetSpriteImmediately(overrideIcon);
            sprite.Color = Color.white;
            sprite.SetColorImmediately(Color.white);
            return;
        }

        sprite.UIAtlas = "ItemIconAtlas";
        ItemClass itemClass = ItemClass.GetItemClass(item.ItemId, false);
        string icon = itemClass != null ? itemClass.GetIconName() : item.ItemId;
        Color tint = itemClass != null ? itemClass.GetIconTint() : Color.white;
        tint.a = 1f;
        sprite.SetSpriteImmediately(string.IsNullOrEmpty(icon) ? item.ItemId : icon);
        sprite.Color = tint;
        sprite.SetColorImmediately(tint);
    }

    private static bool TryGetStartingItemIconOverride(string itemId, out string atlas, out string icon)
    {
        atlas = "ItemIconAtlas";
        icon = string.Empty;
        if (string.IsNullOrEmpty(itemId)) return false;

        switch (itemId)
        {
            // Some legacy/custom starter IDs are not present in the ItemClass registry during the
            // main-menu creator. Resolve them explicitly to their ORIGINAL ItemIconAtlas sprite
            // names from REBIRTH 2.6 rather than creating duplicate/renamed Survivor-atlas icons.
            case "ItemsWeaponsCleaver001_FR":
                atlas = "ItemIconAtlas"; icon = "FR_Cleaver_icon"; return true;
            case "ItemsWeaponsJunkBaton001_FR":
                atlas = "ItemIconAtlas"; icon = "ItemsWeaponsJunkBaton001_FR"; return true;
            case "ItemsWeaponsScythe004_FR":
                atlas = "ItemIconAtlas"; icon = "ItemsWeaponsScythe004_FR"; return true;
            case "FuriousRamsayFountainPen":
                atlas = "ItemIconAtlas"; icon = "FR_FountainPen_icon"; return true;
            case "FuriousRamsayHammerPliers":
                atlas = "ItemIconAtlas"; icon = "FR_HammerPliers_icon"; return true;
            case "FuriousRamsayScrewdriver":
                atlas = "ItemIconAtlas"; icon = "FR_Screwdriver_icon"; return true;
            case "FuriousRamsayPliers":
                atlas = "ItemIconAtlas"; icon = "FR_Pliers_icon"; return true;
            case "guppyFireExtinguisherItem":
                atlas = "ItemIconAtlas"; icon = "guppyFireExtinguisher"; return true;
            case "qt_claude":
                // Base-game treasure maps inherit treasureQuestMaster, whose authored CustomIcon is
                // treasureQuestMaster. Use the native ItemIconAtlas sprite directly; do not package
                // a duplicate Survivor atlas image for this vanilla asset.
                atlas = "ItemIconAtlas"; icon = "treasureQuestMaster"; return true;
            case "FuriousRamsayPropaneTank":
                atlas = "ItemIconAtlas"; icon = "FR_SM_Propane_icon"; return true;
            case "modArmorHelmetLight":
                atlas = "ItemIconAtlas"; icon = "modArmorHelmetLight"; return true;

            // Seed bundle and Water Barrel are special starter definitions. The seed bundle's
            // intended icon is the native farming bundle sprite; the barrel is a block rather
            // than an ItemClass, so resolve its authored block icon directly.
            case "FuriousRamsaySeedBundle":
                icon = "bundleFarm"; return true;
            case "FuriousRamsayWaterTank":
                icon = "cntBarrelPlasticSingle00"; return true;
        }
        return false;
    }

    private void BackgroundKnowledgeScroll(float delta)
    {
        RebirthBackgroundDefinition bg = model != null ? model.GetSelectedBackground() : null;
        int total = bg != null && bg.StartingKnowledgeIds != null ? bg.StartingKnowledgeIds.Count : 0;
        int next = ScrollOffset(backgroundKnowledgeOffset, delta, total, BackgroundKnowledgeRows);
        if (next == backgroundKnowledgeOffset) return;
        backgroundKnowledgeOffset = next;
        RenderBackgroundVisualDetails(bg);
    }

    private void UpdateBackgroundKnowledgeNativeScroll(int total)
    {
        if (backgroundKnowledgeNativeScrollHost != null && backgroundKnowledgeNativeScrollHost.ViewComponent != null)
            backgroundKnowledgeNativeScrollHost.ViewComponent.IsVisible = total > BackgroundKnowledgeRows;
        if (backgroundKnowledgeScrollCapture != null && backgroundKnowledgeScrollCapture.ViewComponent != null)
            backgroundKnowledgeScrollCapture.ViewComponent.IsVisible = total > 0;
        if (backgroundKnowledgeNativeScrollProxy == null || backgroundKnowledgeNativeScrollProxy.ViewComponent == null) return;
        int contentHeight = total > BackgroundKnowledgeRows
            ? Math.Max(BackgroundKnowledgeTrackHeight + 1, Mathf.CeilToInt(BackgroundKnowledgeTrackHeight * (total / (float)BackgroundKnowledgeRows)))
            : BackgroundKnowledgeTrackHeight;
        backgroundKnowledgeNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, total - BackgroundKnowledgeRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(backgroundKnowledgeOffset / (float)maxOffset) : 0f;
        syncingBackgroundKnowledgeNativeScroll = true;
        RefreshNativeScrollView(backgroundKnowledgeNativeScrollView);
        TrySetNativeScrollValue(backgroundKnowledgeNativeScrollView, normalized);
        RefreshNativeScrollView(backgroundKnowledgeNativeScrollView);
        syncingBackgroundKnowledgeNativeScroll = false;
    }

    private void PollBackgroundKnowledgeNativeScroll()
    {
        if (syncingBackgroundKnowledgeNativeScroll || model == null || model.Step != RebirthSurvivorCreatorStep.Background) return;
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        int total = bg != null && bg.StartingKnowledgeIds != null ? bg.StartingKnowledgeIds.Count : 0;
        int maxOffset = Math.Max(0, total - BackgroundKnowledgeRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!TryGetNativeScrollValue(backgroundKnowledgeNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, BackgroundKnowledgeRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == backgroundKnowledgeOffset) return;
        backgroundKnowledgeOffset = requested;
        RenderBackgroundVisualDetails(bg);
    }

    private void BackgroundStartingItemsScroll(float delta)
    {
        RebirthBackgroundDefinition bg = model != null ? model.GetSelectedBackground() : null;
        int total = bg != null && bg.StartingItems != null ? bg.StartingItems.Count : 0;
        backgroundStartingItemsOffset = ScrollOffset(backgroundStartingItemsOffset, delta, total, BackgroundStartingItemRows);
        RenderBackgroundVisualDetails(bg);
    }

    private void UpdateBackgroundStartingItemsNativeScroll(int total)
    {
        // The host is authored visible so 7DTD can construct <defaultscrollbar/> correctly,
        // then hidden at runtime for every current Experience because all authored starter
        // packages fit in the eight-slot two-column grid. It remains available as a future
        // overflow fallback without occupying space when no scrollbar is required.
        if (backgroundStartingItemsNativeScrollHost != null && backgroundStartingItemsNativeScrollHost.ViewComponent != null)
            backgroundStartingItemsNativeScrollHost.ViewComponent.IsVisible = total > BackgroundStartingItemRows;
        if (backgroundStartingItemsNativeScrollProxy == null || backgroundStartingItemsNativeScrollProxy.ViewComponent == null) return;
        int contentHeight = total > BackgroundStartingItemRows
            ? Math.Max(BackgroundStartingItemTrackHeight + 1, Mathf.CeilToInt(BackgroundStartingItemTrackHeight * (total / (float)BackgroundStartingItemRows)))
            : BackgroundStartingItemTrackHeight;
        backgroundStartingItemsNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, total - BackgroundStartingItemRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(backgroundStartingItemsOffset / (float)maxOffset) : 0f;
        syncingBackgroundStartingItemsNativeScroll = true;
        RefreshNativeScrollView(backgroundStartingItemsNativeScrollView);
        TrySetNativeScrollValue(backgroundStartingItemsNativeScrollView, normalized);
        RefreshNativeScrollView(backgroundStartingItemsNativeScrollView);
        syncingBackgroundStartingItemsNativeScroll = false;
    }

    private void PollBackgroundStartingItemsNativeScroll()
    {
        if (syncingBackgroundStartingItemsNativeScroll || model == null || model.Step != RebirthSurvivorCreatorStep.Background) return;
        RebirthBackgroundDefinition bg = model.GetSelectedBackground();
        int total = bg != null && bg.StartingItems != null ? bg.StartingItems.Count : 0;
        int maxOffset = Math.Max(0, total - BackgroundStartingItemRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!TryGetNativeScrollValue(backgroundStartingItemsNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, BackgroundStartingItemRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == backgroundStartingItemsOffset) return;
        backgroundStartingItemsOffset = requested;
        RenderBackgroundVisualDetails(bg);
    }

    private void ReviewProgressionScroll(float delta)
    {
        if (model == null || model.Step != RebirthSurvivorCreatorStep.Review) return;
        int next = ScrollOffset(reviewProgressionOffset, delta, reviewProgressionTotal, ReviewSummaryProgressionRows);
        if (next == reviewProgressionOffset) return;
        reviewProgressionOffset = next;
        RenderReviewVisual();
    }

    private void ReviewTraitScroll(float delta)
    {
        if (model == null || model.Step != RebirthSurvivorCreatorStep.Review) return;
        int next = ScrollOffset(reviewTraitOffset, delta, reviewTraitTotal, ReviewTraitRows);
        if (next == reviewTraitOffset) return;
        reviewTraitOffset = next;
        RenderReviewVisual();
    }

    private void ReviewStartingItemsScroll(float delta)
    {
        if (model == null || model.Step != RebirthSurvivorCreatorStep.Review) return;
        int next = ScrollOffset(reviewStartingItemsOffset, delta, reviewStartingItemsTotal, ReviewStartingItemRows);
        if (next == reviewStartingItemsOffset) return;
        reviewStartingItemsOffset = next;
        RenderReviewVisual();
    }

    private void UpdateReviewNativeScroll(XUiController host, XUiController proxy, XUiController scrollView,
        int offset, int total, int visible, int trackHeight)
    {
        bool needed = total > visible;
        if (host != null && host.ViewComponent != null) host.ViewComponent.IsVisible = needed;
        if (proxy == null || proxy.ViewComponent == null) return;
        int contentHeight = needed
            ? Math.Max(trackHeight + 1, Mathf.CeilToInt(trackHeight * (total / (float)visible)))
            : trackHeight;
        proxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, total - visible);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(offset / (float)maxOffset) : 0f;
        syncingReviewNativeScroll = true;
        RefreshNativeScrollView(scrollView);
        TrySetNativeScrollValue(scrollView, normalized);
        RefreshNativeScrollView(scrollView);
        syncingReviewNativeScroll = false;
    }

    private void PollReviewNativeScroll()
    {
        if (syncingReviewNativeScroll || model == null || model.Step != RebirthSurvivorCreatorStep.Review) return;
        bool changed = false;
        int requested;
        if (TryGetReviewNativeOffset(reviewProgressionNativeScrollView, reviewProgressionTotal, ReviewSummaryProgressionRows, out requested) && requested != reviewProgressionOffset)
        {
            reviewProgressionOffset = requested;
            changed = true;
        }
        if (TryGetReviewNativeOffset(reviewTraitNativeScrollView, reviewTraitTotal, ReviewTraitRows, out requested) && requested != reviewTraitOffset)
        {
            reviewTraitOffset = requested;
            changed = true;
        }
        if (TryGetReviewNativeOffset(reviewStartingItemsNativeScrollView, reviewStartingItemsTotal, ReviewStartingItemRows, out requested) && requested != reviewStartingItemsOffset)
        {
            reviewStartingItemsOffset = requested;
            changed = true;
        }
        if (changed) RenderReviewVisual();
    }

    private static bool TryGetReviewNativeOffset(XUiController scrollView, int total, int visible, out int offset)
    {
        offset = 0;
        int maxOffset = Math.Max(0, total - visible);
        if (maxOffset <= 0) return false;
        float normalized;
        if (!TryGetNativeScrollValue(scrollView, out normalized)) return false;
        offset = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, visible, RebirthScrollbarPagingPolicy.Enabled);
        return true;
    }

    private void ProgressionSkillExplore_OnPressed(XUiController sender,int mouseButton){int i;if(backgroundSkillExploreIndex.TryGetValue(sender,out i)&&i>=0&&i<backgroundSkillExploreIds.Length)OpenCreatorProgressionExplorer(backgroundSkillExploreIds[i],"Experience Skill",sender);}
    private void ProgressionKnowledgeExplore_OnPressed(XUiController sender,int mouseButton){int i;if(backgroundKnowledgeExploreIndex.TryGetValue(sender,out i)&&i>=0&&i<backgroundKnowledgeExploreIds.Length)OpenCreatorProgressionExplorer(backgroundKnowledgeExploreIds[i],"Experience Knowledge",sender);}
    private void ProgressionWeaknessExplore_OnPressed(XUiController sender,int mouseButton){int i;if(backgroundWeaknessExploreIndex.TryGetValue(sender,out i)&&i>=0&&i<backgroundWeaknessExploreIds.Length)OpenCreatorProgressionExplorer(backgroundWeaknessExploreIds[i],"Experience Skill weakness",sender);}
    private void ProgressionReviewSkillExplore_OnPressed(XUiController sender,int mouseButton){int i;if(reviewSkillExploreIndex.TryGetValue(sender,out i)&&i>=0&&i<reviewSkillExploreIds.Length)OpenCreatorProgressionExplorer(reviewSkillExploreIds[i],reviewSkillExploreIsKnowledge[i]?"Survivor review Knowledge":"Survivor review Skill",sender);}
    private void ProgressionReviewKnowledgeExplore_OnPressed(XUiController sender,int mouseButton){int i;if(reviewKnowledgeExploreIndex.TryGetValue(sender,out i)&&i>=0&&i<reviewKnowledgeExploreIds.Length)OpenCreatorProgressionExplorer(reviewKnowledgeExploreIds[i],"Survivor review Knowledge",sender);}

    private void OpenCreatorProgressionExplorer(string focusId,string reason,XUiController returnFocus)
    {
        focusId=(focusId??string.Empty).Trim();if(model==null||focusId.Length==0||xui==null||xui.playerUI==null||xui.playerUI.windowManager==null)return;
        model.RefreshValidation();
        string token="creator:"+Guid.NewGuid().ToString("N");progressionExplorerReturnToken=token;progressionExplorerReturnFocus=returnFocus;
        if(reviewVisualContent!=null&&reviewVisualContent.ViewComponent!=null)progressionExplorerDetailContentPosition=reviewVisualContent.ViewComponent.Position;
        RebirthProgressionExplorerReturnRegistry.Register(token,delegate(XUi returnXui,string ignored){
            if(returnXui==null||returnXui.playerUI==null||returnXui.playerUI.windowManager==null)return;
            resumeFromProgressionExplorerPending=true;suspendedForProgressionExplorer=true;ArmOpenRequest("progression-explorer-return",model!=null?model.Purpose:RebirthSurvivorCreatorPurpose.CreateLocalProfile);
            XUiController caller=returnXui.FindWindowGroupByName(WindowGroupId);if(caller!=null)returnXui.playerUI.windowManager.Open((GUIWindow)caller.windowGroup,true);
        });
        RebirthProgressionExplorerReturnContext returnContext=new RebirthProgressionExplorerReturnContext(WindowGroupId,token,RebirthSurvivorUiText.L("xuiRebirthProgressionExplorerReturnCreator","RETURN TO SURVIVOR PROFILE"));
        RebirthProgressionExplorerLaunchRequest request=new RebirthProgressionExplorerLaunchRequest(focusId,RebirthProgressionExplorerMode.CreatorPreview,reason,returnContext,model.Validation);
        suspendedForProgressionExplorer=true;
        xui.playerUI.windowManager.Close(WindowGroupId);
        string error;if(!RebirthProgressionExplorerUiService.Open(xui,request,out error))
        {
            RebirthProgressionExplorerReturnRegistry.Unregister(token);progressionExplorerReturnToken=string.Empty;resumeFromProgressionExplorerPending=true;ArmOpenRequest("progression-explorer-launch-failed",model.Purpose);
            XUiController caller=xui.FindWindowGroupByName(WindowGroupId);if(caller!=null)xui.playerUI.windowManager.Open((GUIWindow)caller.windowGroup,true);
            transientStatus="[CC6B64]"+(string.IsNullOrEmpty(error)?"Progression Explorer could not be opened.":error)+"[-]";
        }
    }

    private sealed class BackgroundSkillDisplay
    {
        public string SkillId = string.Empty;
        public string Name = string.Empty;
        public int Value;
    }

    private static int GetStartingTierValue(string tierId)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null || string.IsNullOrEmpty(tierId)) return 0;
        int value;
        return bundle.Progression.SkillBiasTiers.TryGetValue(tierId, out value) ? value : 0;
    }

    private static int GetBackgroundStartingTraitPoints(RebirthBackgroundDefinition bg)
    {
        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null) return 0;
        int points = bundle.Progression.BaseCreationPoints + (bg != null ? bg.CreationPointModifier : 0);
        if (bg != null && string.Equals(bg.Id, RebirthSurvivorIds.BackgroundCleanSlate, StringComparison.OrdinalIgnoreCase))
            points += bundle.Progression.CleanSlateBonus;
        return points;
    }

    private static string SkillIconKey(string skillId)
    {
        string id = (skillId ?? string.Empty).Trim().ToLowerInvariant();
        if (!id.StartsWith("skill.", StringComparison.Ordinal)) return "rb_skill_unarmed";
        string suffix = id.Substring("skill.".Length).Replace('.', '_').Replace('-', '_');
        return "rb_skill_" + suffix;
    }

    private RebirthSurvivorTraitChoice BuildCurrentTraitChoice(RebirthTraitDefinition trait)
    {
        RebirthSurvivorTraitChoice choice = model.GetTraitChoice(trait.Id);
        return choice ?? new RebirthSurvivorTraitChoice(trait, false, true, false, false, string.Empty);
    }

    private void PlayerProfile_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        transientStatus = string.Empty;

        bool nativeMenuAvailable = xui != null && xui.FindWindowGroupByName("playerProfiles") != null;
        if (nativeMenuAvailable)
        {
            string error;
            if (!RebirthNativePlayerProfileBridge.OpenNativePicker(xui, this, model, out error))
            {
                transientStatus = "[CC6B64]" + error + "[-]";
                RenderAll();
            }
            return;
        }

        string profileName;
        string source;
        EntityPlayerLocal localPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (RebirthNativePlayerProfileBridge.TryResolveActivePlayerProfileName(xui, localPlayer, out profileName, out source))
        {
            model.SetPlayerProfileName(profileName);
            transientStatus = string.Empty;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("IN-WORLD selected active profile='" + profileName + "' source=" + source); }
        }
        else
        {
            transientStatus = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileMainMenuRequired",
                "The active Player Profile could not be identified. Select/create the Player Profile from the main menu before creating this Rebirth Survivor.") + "[-]";
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] IN-WORLD active Player Profile resolution failed.");
        }
        RenderAll();
    }

    private void NativePlayerProfileCreate_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly || model.Step != RebirthSurvivorCreatorStep.Profile) return;
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.None) return;
        if (nativePortraitPreloadStarted && !nativePortraitPreloadComplete) return;

        nativeOriginalProfileName = (model.PlayerProfileName ?? string.Empty).Trim();
        nativeTemporaryProfileName = string.Empty;
        nativeFinalProfileName = string.Empty;
        nativeTemporaryAppearance = null;
        nativeEditorDecision = 0;
        transientStatus = string.Empty;
        SetVisible(playerProfileSaveOverlay, false);

        string error;
        if (!RebirthNativePlayerProfileBridge.TryCreateOwnedTemporaryProfile(out nativeTemporaryProfileName, out error))
        {
            AbortNativeProfileCreate(error);
            return;
        }

        // IMPORTANT: manager.Open(customCharacterSystem) closes this creator synchronously.
        // Mark the creator as suspended and establish the workflow phase BEFORE opening it so
        // OnClose preserves the model/RBTemp_* state rather than treating this as a real exit.
        nativeProfileCreatePhase = NativeProfileCreatePhase.AwaitTempEditor;
        nativeProfileCreateStageDeadline = Time.realtimeSinceStartup + 8f;
        suspendedForNativeChildEditor = true;
        if (!TryOpenCustomCharacterEditorDirect(nativeTemporaryProfileName, out error))
        {
            suspendedForNativeChildEditor = false;
            AbortNativeProfileCreate(error);
            return;
        }

        // The creator is closed at this point, so its Update() cannot be responsible for wiring
        // Back/Apply. Wire the native child editor immediately from this same call stack.
        EnterNativeEditorOwnedState();
        nativeEditorDecision = 0;
        nativeProfileCreatePhase = NativeProfileCreatePhase.EditingTemp;
        nativeProfileCreateStageDeadline = 0f;
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP editor opened profile='" +
            nativeTemporaryProfileName + "' owner=survivor-creator-suspended");
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] BEGIN temp='" + nativeTemporaryProfileName +
            "' original='" + nativeOriginalProfileName + "' phase=EditingTemp create=ProfileSDF-direct");
    }

    /// <summary>
    /// Drives only the REBIRTH-owned CREATE workflow across frames. The native appearance
    /// editor is reused, but Player Profile persistence is performed directly through the same
    /// ProfileSDF API used by the base game. This avoids instantiating or driving a second
    /// PlayerProfile controller inside the Survivor Creator.
    /// </summary>
    private void TickNativePlayerProfileCreateFlow()
    {
        if (nativeProfileCreatePhase == NativeProfileCreatePhase.None ||
            xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
            return;

        GUIWindowManager manager = xui.playerUI.windowManager;
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.EditingTemp &&
            nativeProfileCreatePhase != NativeProfileCreatePhase.AwaitSaveName &&
            Time.realtimeSinceStartup >= nativeProfileCreateStageDeadline)
        {
            AbortNativeProfileCreate("Timed out while opening the native Player Profile editor (phase=" +
                nativeProfileCreatePhase + ").");
            return;
        }

        switch (nativeProfileCreatePhase)
        {
            case NativeProfileCreatePhase.AwaitTempEditor:
                if (!manager.IsWindowOpen("customCharacterSystem")) return;
                if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
                EnterNativeEditorOwnedState();
                nativeEditorDecision = 0;
                nativeProfileCreatePhase = NativeProfileCreatePhase.EditingTemp;
                nativeProfileCreateStageDeadline = 0f;
                Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP editor opened profile='" +
                    nativeTemporaryProfileName + "'");
                break;

            case NativeProfileCreatePhase.EditingTemp:
                if (manager.IsWindowOpen("customCharacterSystem")) return;

                UnwireNativeEditorDecisionButtons();
                if (nativeEditorDecision <= 0)
                {
                    FinishTemporaryProfileCreate(false, string.Empty);
                    return;
                }
                if (nativeTemporaryAppearance == null)
                {
                    FinishTemporaryProfileCreate(false, "The temporary Player Profile appearance could not be captured.");
                    return;
                }

                if (newPlayerProfileNameInput != null) newPlayerProfileNameInput.Text = string.Empty;
                SetLabel(newPlayerProfileNameStatus, string.Empty);
                SetVisible(playerProfileSaveOverlay, true);
                nativeProfileCreatePhase = NativeProfileCreatePhase.AwaitSaveName;
                if (newPlayerProfileNameInput != null && newPlayerProfileNameInput.ViewComponent != null &&
                    xui.playerUI.CursorController != null)
                    xui.playerUI.CursorController.SetNavigationTargetLater(newPlayerProfileNameInput.ViewComponent);
                Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP editor applied; awaiting save decision.");
                break;

            case NativeProfileCreatePhase.AwaitSaveName:
                break;

        }
    }

    private bool TryOpenCustomCharacterEditorDirect(string profileName, out string error)
    {
        error = string.Empty;
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            error = "The native Player Profile appearance editor is unavailable.";
            return false;
        }
        try
        {
            ProfileSDF.SetSelectedProfile(profileName ?? string.Empty);
            GUIWindowManager manager = xui.playerUI.windowManager;
            if (manager.IsWindowOpen("customCharacterSystem")) manager.Close("customCharacterSystem");
            manager.Open("customCharacterSystem", true);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDITOR-OPEN-DIRECT profile='" +
                (profileName ?? string.Empty) + "' group=customCharacterSystem"); }
            return true;
        }
        catch (Exception ex)
        {
            error = "The native Player Profile appearance editor could not be opened: " + ex.Message;
            return false;
        }
    }

    private void ResumeCreatorAfterNativeChildEditor(string reason)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            Log.Error("[REBIRTH Survivor][PlayerProfileBridge] CREATOR native-child resume failed: window manager unavailable reason=" + reason);
            return;
        }

        resumeFromNativeChildEditorPending = true;
        RebirthSurvivorCreatorPurpose purpose = model != null
            ? model.Purpose
            : RebirthSurvivorCreatorPurpose.CreateLocalProfile;
        ArmOpenRequest("native-child-editor-resume:" + (reason ?? string.Empty), purpose);
        xui.playerUI.windowManager.Open(WindowGroupId, true);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("CREATOR native-child reopen reason=" +
            (reason ?? string.Empty) + " phase=" + nativeProfileCreatePhase +
            " selected='" + (model != null ? model.PlayerProfileName : string.Empty) + "'"); }
    }

    private void AbortNativeProfileCreate(string reason)
    {
        string message = string.IsNullOrEmpty(reason) ? "Player Profile creation was cancelled." : reason;
        Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] ABORT phase=" + nativeProfileCreatePhase +
            " reason=" + message);
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
        {
            GUIWindowManager manager = xui.playerUI.windowManager;
            if (manager.IsWindowOpen("customCharacterSystem")) manager.Close("customCharacterSystem");
            if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
        }
        FinishTemporaryProfileCreate(false, message);
    }

    private void EnterNativeEditorOwnedState()
    {
        WireNativeEditorDecisionButtons();
        activeNativeEditorOwner = this;
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null)
        {
            GUIWindow nativeEditorWindow = xui.playerUI.windowManager.GetWindow("customCharacterSystem");
            if (nativeEditorWindow != null) nativeEditorWindow.isEscClosable = false;
        }
    }

    private void WireNativeEditorDecisionButtons()
    {
        UnwireNativeEditorDecisionButtons();
        XUiController group = xui != null ? xui.FindWindowGroupByName("customCharacterSystem") : null;
        nativeEditorApplyButton = group != null ? group.GetChildById("btnApply") : null;
        nativeEditorBackButton = group != null ? group.GetChildById("btnBack") : null;
        rebirthEditorApplyButton = group != null ? group.GetChildById("btnRebirthTempApply") : null;
        rebirthEditorBackButton = group != null ? group.GetChildById("btnRebirthTempBack") : null;

        // During a Rebirth-created temporary Player Profile, do not let the stock Back button
        // execute its normal "delete this newly created profile?" path. Rebirth owns that
        // temporary profile and must discard it silently. The overlay buttons look identical
        // but route Back/Apply through this controller first.
        if (rebirthEditorApplyButton != null && rebirthEditorBackButton != null)
        {
            SetVisible(nativeEditorApplyButton, false);
            SetVisible(nativeEditorBackButton, false);
            SetVisible(rebirthEditorApplyButton, true);
            SetVisible(rebirthEditorBackButton, true);
            rebirthEditorApplyButton.OnPress += NativeEditorApply_OnPressed;
            rebirthEditorBackButton.OnPress += NativeEditorBack_OnPressed;
        }
        else
        {
            // Compatibility fallback only. The XML supplied by Rebirth 3.0 always provides
            // the overlay controls in both Menu and InGame character-editor contexts.
            if (nativeEditorApplyButton != null) nativeEditorApplyButton.OnPress += NativeEditorApply_OnPressed;
            if (nativeEditorBackButton != null) nativeEditorBackButton.OnPress += NativeEditorBack_OnPressed;
        }
    }

    private void UnwireNativeEditorDecisionButtons()
    {
        if (rebirthEditorApplyButton != null) rebirthEditorApplyButton.OnPress -= NativeEditorApply_OnPressed;
        if (rebirthEditorBackButton != null) rebirthEditorBackButton.OnPress -= NativeEditorBack_OnPressed;
        if (nativeEditorApplyButton != null) nativeEditorApplyButton.OnPress -= NativeEditorApply_OnPressed;
        if (nativeEditorBackButton != null) nativeEditorBackButton.OnPress -= NativeEditorBack_OnPressed;
        SetVisible(rebirthEditorApplyButton, false);
        SetVisible(rebirthEditorBackButton, false);
        SetVisible(nativeEditorApplyButton, true);
        SetVisible(nativeEditorBackButton, true);
        rebirthEditorApplyButton = null;
        rebirthEditorBackButton = null;
        nativeEditorApplyButton = null;
        nativeEditorBackButton = null;
        if (ReferenceEquals(activeNativeEditorOwner, this)) activeNativeEditorOwner = null;
    }

    public static bool TryHandleNativeCharacterEditorEscape()
    {
        XUiC_RebirthSurvivorCreator owner = activeNativeEditorOwner;
        if (owner == null) return false;
        owner.NativeEditorBack_OnPressed(owner, -1);
        return true;
    }

    private void NativeEditorApply_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeExistingProfileEditActive)
        {
            string editedSource;
            string editedSaveError;
            bool saved = RebirthNativePlayerProfileBridge.TrySaveCustomCharacterPlayerProfile(
                xui, nativeExistingProfileEditName, out editedSource, out editedSaveError);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-APPLY profile='" +
                nativeExistingProfileEditName + "' saved=" + saved + " source=" + editedSource +
                (string.IsNullOrEmpty(editedSaveError) ? string.Empty : " error='" + editedSaveError + "'")); }
            if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null &&
                xui.playerUI.windowManager.IsWindowOpen("customCharacterSystem"))
                xui.playerUI.windowManager.Close("customCharacterSystem");
            FinishExistingPlayerProfileEdit(saved, saved ? string.Empty :
                (string.IsNullOrEmpty(editedSaveError) ? "The Player Profile appearance could not be saved." : editedSaveError));
            ResumeCreatorAfterNativeChildEditor(saved ? "existing-edit-apply" : "existing-edit-apply-failed");
            return;
        }

        Archetype current;
        string source;
        if (RebirthNativePlayerProfileBridge.TryResolveCustomCharacterArchetype(xui, out current, out source) && current != null)
            nativeTemporaryAppearance = RebirthNativePlayerProfileBridge.CloneArchetype(current);

        if (nativeTemporaryAppearance == null)
        {
            nativeEditorDecision = 0;
            transientStatus = "[CC6B64]The temporary Player Profile appearance could not be captured.[-]";
            return;
        }

        string saveError;
        if (!RebirthNativePlayerProfileBridge.TrySaveOwnedTemporaryProfileFromArchetype(
            nativeTemporaryProfileName, nativeTemporaryAppearance, out saveError))
        {
            nativeEditorDecision = 0;
            transientStatus = "[CC6B64]" + saveError + "[-]";
            Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] TEMP direct save failed: " + saveError);
            return;
        }

        nativeEditorDecision = 1;
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null &&
            xui.playerUI.windowManager.IsWindowOpen("customCharacterSystem"))
            xui.playerUI.windowManager.Close("customCharacterSystem");
        UnwireNativeEditorDecisionButtons();

        if (newPlayerProfileNameInput != null) newPlayerProfileNameInput.Text = string.Empty;
        SetLabel(newPlayerProfileNameStatus, string.Empty);
        nativeProfileCreatePhase = NativeProfileCreatePhase.AwaitSaveName;
        nativeProfileCreateStageDeadline = 0f;
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP editor decision=APPLY appearance=True save=ProfileSDF-direct return=survivor-creator");
        ResumeCreatorAfterNativeChildEditor("temp-create-apply");
    }

    private void NativeEditorBack_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeExistingProfileEditActive)
        {
            if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null &&
                xui.playerUI.windowManager.IsWindowOpen("customCharacterSystem"))
                xui.playerUI.windowManager.Close("customCharacterSystem");
            FinishExistingPlayerProfileEdit(false, string.Empty);
            ResumeCreatorAfterNativeChildEditor("existing-edit-back");
            return;
        }

        nativeEditorDecision = -1;
        // Do NOT invoke the stock Back button. Delete RBTemp_* directly, restore the prior
        // selection, and explicitly reopen the suspended Survivor Creator in this callback.
        if (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null &&
            xui.playerUI.windowManager.IsWindowOpen("customCharacterSystem"))
            xui.playerUI.windowManager.Close("customCharacterSystem");
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP editor decision=BACK synchronous-cancel=True return=survivor-creator");
        FinishTemporaryProfileCreate(false, string.Empty);
        ResumeCreatorAfterNativeChildEditor("temp-create-back");
    }

    private void NewPlayerProfileName_OnChanged(XUiController sender, string text, bool changeFromCode)
    {
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.AwaitSaveName || changeFromCode) return;
        SetLabel(newPlayerProfileNameStatus, string.Empty);
    }

    private void SaveNewPlayerProfile_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.AwaitSaveName) return;
        string name = newPlayerProfileNameInput != null ? (newPlayerProfileNameInput.Text ?? string.Empty).Trim() : string.Empty;
        if (name.Length == 0)
        {
            SetLabel(newPlayerProfileNameStatus, "Enter a Player Profile name.");
            return;
        }
        if (RebirthNativePlayerProfileBridge.IsReservedTemporaryProfileName(name))
        {
            SetLabel(newPlayerProfileNameStatus, "The RBTemp_ prefix is reserved for REBIRTH temporary Player Profiles.");
            return;
        }
        if (name.Length > 30)
        {
            SetLabel(newPlayerProfileNameStatus, "Player Profile names are limited to 30 characters.");
            return;
        }
        RebirthNativePlayerProfileBridge.EmbeddedProfileInfo existing;
        if (RebirthNativePlayerProfileBridge.TryFindEmbeddedProfile(this, name, out existing))
        {
            SetLabel(newPlayerProfileNameStatus, "A Player Profile with that name already exists.");
            return;
        }

        nativeFinalProfileName = name;
        SetVisible(playerProfileSaveOverlay, false);
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] SAVE requested final='" + nativeFinalProfileName + "'");

        string createError = string.Empty;
        if (nativeTemporaryAppearance == null ||
            !RebirthNativePlayerProfileBridge.TrySavePlayerProfileFromArchetype(
                nativeFinalProfileName, nativeTemporaryAppearance, out createError))
        {
            SetVisible(playerProfileSaveOverlay, true);
            SetLabel(newPlayerProfileNameStatus, string.IsNullOrEmpty(createError) ?
                "The Player Profile appearance is unavailable." : createError);
            nativeProfileCreatePhase = NativeProfileCreatePhase.AwaitSaveName;
            Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] FINAL direct save failed: " + createError);
            return;
        }

        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] FINAL saved profile='" +
            nativeFinalProfileName + "' via=ProfileSDF-direct");
        FinishTemporaryProfileCreate(true, string.Empty);
    }

    private void DiscardNewPlayerProfile_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.AwaitSaveName) return;
        FinishTemporaryProfileCreate(false, string.Empty);
    }

    private void FinishTemporaryProfileCreate(bool keepFinal, string failure)
    {
        SetVisible(playerProfileSaveOverlay, false);
        UnwireNativeEditorDecisionButtons();

        string deleteError;
        pendingSilentTempCleanupName = nativeTemporaryProfileName ?? string.Empty;
        pendingSilentTempCleanupDeadline = string.IsNullOrEmpty(pendingSilentTempCleanupName) ? 0f : Time.realtimeSinceStartup + 8f;
        bool tempDeleted = RebirthNativePlayerProfileBridge.TryDeleteEmbeddedPlayerProfileSilently(this, nativeTemporaryProfileName, out deleteError);
        // RBTemp_* removal is direct ProfileSDF deletion. No native delete action is invoked,
        // therefore no confirmation message box exists to suppress or auto-click.
        RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
        RebirthPlayerProfilePortraitBinder.Invalidate(nativeTemporaryProfileName);
        RebirthPlayerProfileModelBinder.Invalidate(nativeTemporaryProfileName);

        // Both native rows and our ordered snapshot must reflect the final save and
        // temporary deletion before selection validation can choose a fallback.
        List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> refreshedProfiles;
        if (RebirthNativePlayerProfileBridge.TryGetEmbeddedPlayerProfiles(this, out refreshedProfiles))
            nativeProfileSnapshot = refreshedProfiles;

        string targetName = keepFinal ? nativeFinalProfileName : nativeOriginalProfileName;
        if (!string.IsNullOrEmpty(targetName))
        {
            string selectError;
            RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, targetName, out selectError);
            // Back from the native editor must return immediately to the previous Survivor-draft
            // profile. The internal deletion may still finish asynchronously, but the visible
            // model/checkmark/buttons must never remain attached to RBTemp_*.
            model.SetPlayerProfileName(targetName);
        }
        else if (!keepFinal)
        {
            model.SetPlayerProfileName(string.Empty);
        }

        if (keepFinal && nativeTemporaryAppearance != null && !string.IsNullOrEmpty(nativeFinalProfileName))
            RebirthPlayerProfilePortraitBinder.Capture(nativeFinalProfileName, nativeTemporaryAppearance);

        if (!tempDeleted && string.IsNullOrEmpty(failure)) failure = deleteError;
        transientStatus = string.IsNullOrEmpty(failure) ? string.Empty : "[CC6B64]" + failure + "[-]";
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] END keepFinal=" + keepFinal +
            " final='" + nativeFinalProfileName + "' tempDeleted=" + tempDeleted +
            (string.IsNullOrEmpty(failure) ? string.Empty : " error='" + failure + "'"));

        nativeProfileCreatePhase = NativeProfileCreatePhase.None;
        nativeTemporaryProfileName = string.Empty;
        nativeFinalProfileName = string.Empty;
        nativeTemporaryAppearance = null;
        nativeEditorDecision = 0;
        nativeProfileCreateStageDeadline = 0f;
        nativeProfileLastSelection = string.Empty;
        nativeProfileScrollOffset = 0;
        SetVisible(profilePortraitPreloadMask, false);
        SetNativeProfileInteractionEnabled(true);
        pendingSelectedPortraitName = targetName;
        pendingSelectedPortraitReadyFrame = string.IsNullOrEmpty(targetName) ? -1 : Time.frameCount + 1;
        RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
        EnsureDraftPlayerProfileExists();
        RenderProfiles();
    }

    private void PurgeStaleInternalTemporaryProfiles()
    {
        bool found = false;
        try
        {
            // Bounded positive-ownership cleanup, not an enumeration of user profile names.
            foreach (string name in RebirthNativePlayerProfileBridge.GetOwnedTemporaryProfileNames(32))
            {
                if (string.Equals(name, nativeTemporaryProfileName, StringComparison.OrdinalIgnoreCase)) continue;
                string cleanupError;
                if (RebirthNativePlayerProfileBridge.TryDeleteEmbeddedPlayerProfileSilently(this, name, out cleanupError))
                {
                    found = true;
                    RebirthPlayerProfilePortraitBinder.Invalidate(name);
                    RebirthPlayerProfileModelBinder.Invalidate(name);
                }
                else Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] owned cleanup deferred: " + cleanupError);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] ownership cleanup unavailable; profiles preserved: " + ex.Message);
        }
        if (!found) return;
        RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
        if (!string.IsNullOrEmpty(nativeGlobalProfileNameAtOpen))
        {
            string restoreError;
            RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, nativeGlobalProfileNameAtOpen, out restoreError);
        }
        Log.Out("[REBIRTH Survivor][PlayerProfileCreate] purged verified owned temporary Player Profiles on creator open.");
    }

    private void NativeProfileEditObserved_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.None || nativeExistingProfileEditActive ||
            !string.IsNullOrEmpty(pendingSilentTempCleanupName) || model == null) return;

        string profileName = (model.PlayerProfileName ?? string.Empty).Trim();
        bool canModify = false;
        string source = string.Empty;
        RebirthNativePlayerProfileBridge.TryCanModifyEmbeddedPlayerProfile(this, out canModify, out source);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-PRESS profile='" + profileName +
            "' canModify=" + canModify + " source=" + source + " mouse=" + mouseButton); }
        if (!canModify || string.IsNullOrEmpty(profileName)) return;

        string selectError;
        if (!RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, profileName, out selectError))
        {
            transientStatus = "[CC6B64]" + selectError + "[-]";
            return;
        }

        nativeExistingProfileEditActive = true;
        nativeExistingProfileEditorOpened = false;
        nativeExistingProfileEditName = profileName;
        SetNativeProfileInteractionEnabled(false);
        string openError;
        suspendedForNativeChildEditor = true;
        if (!TryOpenCustomCharacterEditorDirect(profileName, out openError))
        {
            suspendedForNativeChildEditor = false;
            FinishExistingPlayerProfileEdit(false, openError);
            return;
        }
        EnterNativeEditorOwnedState();
        nativeExistingProfileEditorOpened = true;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-EDITOR-READY profile='" + profileName +
            "' owner=survivor-creator-suspended"); }
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-BEGIN profile='" + profileName +
            "' editor=native-customCharacterSystem return=rebirth-creator"); }
    }

    private void NativeProfileDeleteObserved_OnPressed(XUiController sender, int mouseButton)
    {
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.None || nativeExistingProfileEditActive ||
            !string.IsNullOrEmpty(pendingSilentTempCleanupName) || model == null) return;
        string profileName = (model.PlayerProfileName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(profileName)) return;

        bool canModify = false;
        string modifySource = string.Empty;
        RebirthNativePlayerProfileBridge.TryCanModifyEmbeddedPlayerProfile(this, out canModify, out modifySource);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DELETE-PRESS profile='" + profileName +
            "' canModify=" + canModify + " source=" + modifySource + " mouse=" + mouseButton); }
        if (!canModify) return;

        pendingIntentionalDeleteName = profileName;
        pendingIntentionalDeleteDeadline = Time.realtimeSinceStartup + 20f;
        string text = string.Format(Localization.Get("xuiProfilesDeleteConfirmation"),
            Utils.EscapeBbCodes(profileName));
        SetLabel(playerProfileDeleteText, text);
        SetVisible(playerProfileDeleteOverlay, true);
        if (btnCancelDeletePlayerProfile != null && btnCancelDeletePlayerProfile.ViewComponent != null &&
            xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
            xui.playerUI.CursorController.SetNavigationTargetLater(btnCancelDeletePlayerProfile.ViewComponent);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DELETE-CONFIRM-OPEN profile='" + profileName + "'"); }
    }

    private void ConfirmPlayerProfileDelete_OnPressed(XUiController sender, int mouseButton)
    {
        string profileName = pendingIntentionalDeleteName ?? string.Empty;
        SetVisible(playerProfileDeleteOverlay, false);
        if (string.IsNullOrEmpty(profileName)) return;

        string error;
        if (!RebirthNativePlayerProfileBridge.TryDeletePlayerProfileDirect(profileName, out error))
        {
            transientStatus = "[CC6B64]" + error + "[-]";
            pendingIntentionalDeleteName = string.Empty;
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] DELETE-CONFIRM failed profile='" +
                profileName + "' error='" + error + "'");
            return;
        }
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DELETE-CONFIRM profile='" + profileName +
            "' via=ProfileSDF-direct"); }
        PumpIntentionalProfileDeleteRecovery();
    }

    private void CancelPlayerProfileDelete_OnPressed(XUiController sender, int mouseButton)
    {
        string profileName = pendingIntentionalDeleteName ?? string.Empty;
        pendingIntentionalDeleteName = string.Empty;
        pendingIntentionalDeleteDeadline = 0f;
        SetVisible(playerProfileDeleteOverlay, false);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DELETE-CANCEL profile='" + profileName + "'"); }
    }

    private void PumpExistingPlayerProfileEdit()
    {
        if (!nativeExistingProfileEditActive || xui == null || xui.playerUI == null ||
            xui.playerUI.windowManager == null) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        bool open = manager.IsWindowOpen("customCharacterSystem");
        if (open)
        {
            if (!nativeExistingProfileEditorOpened)
            {
                nativeExistingProfileEditorOpened = true;
                EnterNativeEditorOwnedState();
                { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-EDITOR-READY profile='" +
                    nativeExistingProfileEditName + "'"); }
            }
            return;
        }

        // If the editor was closed by any route other than our overlay controls, treat it as
        // a cancel and return to the Survivor Creator instead of allowing native OptionsProfiles
        // navigation to take over.
        if (nativeExistingProfileEditorOpened)
            FinishExistingPlayerProfileEdit(false, string.Empty);
    }

    private void FinishExistingPlayerProfileEdit(bool saved, string error)
    {
        string profileName = nativeExistingProfileEditName ?? string.Empty;
        UnwireNativeEditorDecisionButtons();
        nativeExistingProfileEditActive = false;
        nativeExistingProfileEditorOpened = false;
        nativeExistingProfileEditName = string.Empty;
        if (!string.IsNullOrEmpty(profileName))
        {
            string selectError;
            RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, profileName, out selectError);
            if (model != null) model.SetPlayerProfileName(profileName);
            RebirthPlayerProfilePortraitBinder.Invalidate(profileName);
            RebirthPlayerProfileModelBinder.Invalidate(profileName);
            ScheduleSelectedProfilePortrait(profileName);
        }
        RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
        nativeProfileLastSelection = string.Empty;
        SetNativeProfileInteractionEnabled(true);
        if (!string.IsNullOrEmpty(error)) transientStatus = "[CC6B64]" + error + "[-]";
        RenderProfiles();
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EDIT-END profile='" + profileName +
            "' saved=" + saved + (string.IsNullOrEmpty(error) ? string.Empty : " error='" + error + "'") ); }
    }

    private void PumpIntentionalProfileDeleteRecovery()
    {
        if (string.IsNullOrEmpty(pendingIntentionalDeleteName) || model == null) return;
        if (Time.realtimeSinceStartup > pendingIntentionalDeleteDeadline)
        {
            pendingIntentionalDeleteName = string.Empty;
            return;
        }

        string deletedName = pendingIntentionalDeleteName;
        RebirthNativePlayerProfileBridge.EmbeddedProfileInfo stillThere;
        if (RebirthNativePlayerProfileBridge.TryFindEmbeddedProfile(this, deletedName, out stillThere)) return;

        RebirthPlayerProfilePortraitBinder.Invalidate(deletedName);
        RebirthPlayerProfileModelBinder.Invalidate(deletedName);
        pendingIntentionalDeleteName = string.Empty;
        RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
        List<RebirthNativePlayerProfileBridge.EmbeddedProfileInfo> snapshot;
        if (!RebirthNativePlayerProfileBridge.TryGetEmbeddedPlayerProfiles(this, out snapshot) || snapshot == null) return;
        nativeProfileSnapshot = OrderNativeProfiles(snapshot, string.Empty);
        if (nativeProfileSnapshot.Count > 0)
        {
            RebirthNativePlayerProfileBridge.EmbeddedProfileInfo first = nativeProfileSnapshot[0];
            string selectError;
            RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfile(this, first, out selectError);
            model.SetPlayerProfileName(first.Name);
            if (string.Equals(nativeGlobalProfileNameAtOpen, deletedName, StringComparison.OrdinalIgnoreCase))
            {
                nativeGlobalProfileNameAtOpen = first.Name;
                nativeGlobalProfileSourceAtOpen = "delete-fallback";
            }
            nativeProfileScrollOffset = 0;
            nativeProfileLastSelection = string.Empty;
            ScheduleSelectedProfilePortrait(first.Name);
            UpdateNativeProfileModifyButtons(true);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DELETE-RECOVERY deleted='" + deletedName + "' selected-first='" + first.Name + "'"); }
        }
        else
        {
            model.SetPlayerProfileName(string.Empty);
            if (string.Equals(nativeGlobalProfileNameAtOpen, deletedName, StringComparison.OrdinalIgnoreCase))
                nativeGlobalProfileNameAtOpen = string.Empty;
        }
        RenderProfiles();
    }

    private void PumpSilentTemporaryProfileCleanup()
    {
        if (string.IsNullOrEmpty(pendingSilentTempCleanupName)) return;
        string tempName = pendingSilentTempCleanupName;
        string draft = model != null ? (model.PlayerProfileName ?? string.Empty).Trim() : string.Empty;

        RebirthNativePlayerProfileBridge.EmbeddedProfileInfo existing;
        if (!RebirthNativePlayerProfileBridge.TryFindEmbeddedProfile(this, tempName, out existing))
        {
            pendingSilentTempCleanupName = string.Empty;
            pendingSilentTempCleanupDeadline = 0f;
            RebirthPlayerProfilePortraitBinder.Invalidate(tempName);
            RebirthPlayerProfileModelBinder.Invalidate(tempName);
            RebirthNativePlayerProfileBridge.InvalidateEmbeddedProfileSnapshot();
            if (!string.IsNullOrEmpty(draft))
            {
                string selectError;
                RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, draft, out selectError);
            }
            UpdateNativeProfileModifyButtons(true);
            Log.Out("[REBIRTH Survivor][PlayerProfileCreate] TEMP-CLEANUP complete profile='" + tempName + "' restored='" + draft + "'.");
            return;
        }

        if (Time.realtimeSinceStartup > pendingSilentTempCleanupDeadline)
        {
            Log.Warning("[REBIRTH Survivor][PlayerProfileCreate] TEMP-CLEANUP timeout profile='" + tempName + "'.");
            pendingSilentTempCleanupName = string.Empty;
            if (!string.IsNullOrEmpty(draft))
            {
                string selectError;
                RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, draft, out selectError);
            }
            UpdateNativeProfileModifyButtons(true);
            return;
        }

        string deleteError;
        RebirthNativePlayerProfileBridge.TryDeleteEmbeddedPlayerProfileSilently(this, tempName, out deleteError);
        // Internal deletion temporarily selects the temporary profile in the native controller.
        // Immediately restore the Survivor-draft selection so the large preview and native
        // can_modify_profile state never appear to belong to RBTemp_*.
        if (!string.IsNullOrEmpty(draft))
        {
            string selectError;
            RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, draft, out selectError);
        }
        UpdateNativeProfileModifyButtons(true);
    }

    private void EnsureDraftPlayerProfileExists()
    {
        if (model == null || nativeProfileSnapshot == null || nativeProfileSnapshot.Count == 0) return;
        string selected = (model.PlayerProfileName ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(selected))
        {
            for (int i = 0; i < nativeProfileSnapshot.Count; i++)
                if (nativeProfileSnapshot[i] != null && string.Equals(nativeProfileSnapshot[i].Name, selected, StringComparison.OrdinalIgnoreCase)) return;
        }
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.None) return;
        RebirthNativePlayerProfileBridge.EmbeddedProfileInfo first = nativeProfileSnapshot[0];
        if (first == null || string.IsNullOrEmpty(first.Name)) return;
        model.SetPlayerProfileName(first.Name);
        string selectError;
        RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfile(this, first, out selectError);
        nativeProfileScrollOffset = 0;
        nativeProfileLastSelection = string.Empty;
        ScheduleSelectedProfilePortrait(first.Name);
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("DRAFT-FALLBACK selected-first='" + first.Name + "'"); }
    }

    private void RestoreGlobalNativePlayerProfile()
    {
        if (string.IsNullOrEmpty(nativeGlobalProfileNameAtOpen)) return;
        string error;
        if (RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfileByName(this, nativeGlobalProfileNameAtOpen, out error))
        {
            // The embedded creator selection is deliberately temporary. Persist the original
            // native selection again when the creator closes so editing/creating a Survivor
            // Profile never changes Play Game's globally selected appearance profile.
            ProfileSDF.SetSelectedProfile(nativeGlobalProfileNameAtOpen);
            ProfileSDF.Save();
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("GLOBAL-RESTORE profile='" + nativeGlobalProfileNameAtOpen +
                "' source=" + nativeGlobalProfileSourceAtOpen + " persisted=True"); }
        }
        else
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] GLOBAL-RESTORE failed profile='" + nativeGlobalProfileNameAtOpen + "' error=" + error);
    }

    private void SaveReusableProfile_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly || model.Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate) return;
        model.ToggleSaveAsReusableProfile();
        if (profileNameInput != null) profileNameInput.Text = model.ProfileName;
        transientStatus = string.Empty;
        RenderAll();
        SelectFirstForStep();
    }

    private void ProfileNew_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly || model.Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate) return;
        model.SelectNewFirstWorldProfile();
        profileOffset = 0;
        transientStatus = string.Empty;
        if (profileNameInput != null)
        {
            profileNameInput.Text = string.Empty;
            profileNameInput.Enabled = true;
        }
        RenderAll();
    }

    private void ProfileRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly || model.Purpose != RebirthSurvivorCreatorPurpose.FirstWorldCreate) return;
        int row;
        if (!profileIndex.TryGetValue(sender, out row)) return;
        int index = profileOffset + row;
        if (index < 0 || index >= profiles.Length) return;
        string error;
        if (!model.SelectExistingFirstWorldProfile(profiles[index], out error))
        {
            transientStatus = string.IsNullOrEmpty(error) ? string.Empty : "[CC6B64]" + error + "[-]";
            RenderAll();
            return;
        }
        if (profileNameInput != null)
        {
            profileNameInput.Text = model.ProfileName;
            profileNameInput.Enabled = false;
        }
        EnsureSelectedProfileVisible();
        transientStatus = string.Empty;
        RenderAll();
    }

    private void NativeProfileRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly || model.Step != RebirthSurvivorCreatorStep.Profile) return;
        if (nativeProfileCreatePhase != NativeProfileCreatePhase.None) return;
        if (nativePortraitPreloadStarted && !nativePortraitPreloadComplete) return;
        int row;
        if (!nativeProfileButtonIndex.TryGetValue(sender, out row)) return;
        int index = nativeProfileScrollOffset + row;
        if (index < 0 || nativeProfileSnapshot == null || index >= nativeProfileSnapshot.Count) return;
        RebirthNativePlayerProfileBridge.EmbeddedProfileInfo profile = nativeProfileSnapshot[index];
        if (profile == null || string.IsNullOrEmpty(profile.Name)) return;

        string error;
        bool nativeSelected = RebirthNativePlayerProfileBridge.TrySelectEmbeddedPlayerProfile(this, profile, out error);
        string confirmedName = string.Empty;
        string confirmedSource = string.Empty;
        bool confirmed = nativeSelected &&
                         RebirthNativePlayerProfileBridge.TryResolveEmbeddedPlayerProfileName(this, out confirmedName, out confirmedSource) &&
                         string.Equals(confirmedName, profile.Name, StringComparison.OrdinalIgnoreCase);
        if (confirmed)
        {
            model.SetPlayerProfileName(profile.Name);
            nativeProfileLastSelection = string.Empty;
            nativeProfileScrollOffset = 0;
            ScheduleSelectedProfilePortrait(profile.Name);
            transientStatus = string.Empty;
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("ROW-CONFIRMED profile='" + profile.Name + "' source=" + confirmedSource); }
        }
        else
        {
            transientStatus = "[D6C978]" + (string.IsNullOrEmpty(error)
                ? "The native Player Profile preview did not accept that selection. See [REBIRTH Survivor][PlayerProfileBridge] in the output log."
                : error) + "[-]";
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] ROW-REJECTED requested='" + profile.Name +
                "' actual='" + (confirmedName ?? string.Empty) + "' source=" + (confirmedSource ?? string.Empty));
        }
        RenderAll();
    }

    private void BackgroundRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        int row;
        if (!backgroundIndex.TryGetValue(sender, out row)) return;
        RebirthBackgroundDefinition[] page = model.GetBackgroundPage(BackgroundRows);
        if (row >= page.Length) return;
        model.SelectBackground(page[row].Id);
        backgroundKnowledgeOffset = 0;
        backgroundStartingItemsOffset = 0;
        transientStatus = model.LastPrunedTraitCount > 0
            ? "[D6C978]" + string.Format(CultureInfo.InvariantCulture, RebirthSurvivorUiText.L("xuiRebirthSurvivorBackgroundPrunedTraitsFormat", "Experience change removed {0} incompatible Trait selection(s)."), model.LastPrunedTraitCount) + "[-]"
            : string.Empty;
        RenderAll();
    }

    private void TraitSideRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null)
        {
#if REBIRTH_DEBUG
            if (RebirthSurvivorDebug.TraitUiLoggingEnabled) Log.Warning("[REBIRTH Survivor][TraitUI] click ignored: creator model is null");
#endif
            return;
        }
        int row;
        string side;
        RebirthSurvivorTraitChoice[] page;
        if (positiveTraitIndex.TryGetValue(sender, out row))
        {
            side = "positive";
            page = model.GetPositiveTraitPage(TraitSideRows);
        }
        else if (negativeTraitIndex.TryGetValue(sender, out row))
        {
            side = "negative";
            page = model.GetNegativeTraitPage(TraitSideRows);
        }
        else
        {
#if REBIRTH_DEBUG
            if (RebirthSurvivorDebug.TraitUiLoggingEnabled) Log.Warning("[REBIRTH Survivor][TraitUI] click ignored: sender is not registered in either Trait-side index");
#endif
            return;
        }
#if REBIRTH_DEBUG
        if (RebirthSurvivorDebug.TraitUiLoggingEnabled)
            Log.Out("[REBIRTH Survivor][TraitUI] click side=" + side + " row=" + row + " mouseButton=" + mouseButton +
                " pageLength=" + (page != null ? page.Length : 0) +
                " offset=" + (side == "negative" ? model.NegativeTraitOffset : model.PositiveTraitOffset));
#endif
        if (row < 0 || page == null || row >= page.Length || page[row] == null || page[row].Definition == null)
        {
#if REBIRTH_DEBUG
            if (RebirthSurvivorDebug.TraitUiLoggingEnabled) Log.Warning("[REBIRTH Survivor][TraitUI] click ignored after paging: side=" + side + " row=" + row + " pageLength=" + (page != null ? page.Length : 0));
#endif
            return;
        }

        focusedTraitId = page[row].Definition.Id;
#if REBIRTH_DEBUG
        if (RebirthSurvivorDebug.TraitUiLoggingEnabled)
            Log.Out("[REBIRTH Survivor][TraitUI] click resolved trait=" + focusedTraitId + " selectedBefore=" + page[row].IsSelected + " canToggle=" + page[row].CanToggle);
#endif
        if (!model.IsReadOnly)
        {
            string reason;
            if (!model.ToggleTrait(focusedTraitId, out reason) && !string.IsNullOrEmpty(reason))
                transientStatus = "[CC6B64]" + reason + "[-]";
            else transientStatus = string.Empty;
        }
        RenderAll();
    }

    private void TraitRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        int row;
        if (!traitIndex.TryGetValue(sender, out row)) return;
        RebirthSurvivorTraitChoice[] page = model.GetTraitPage(TraitRows);
        if (row >= page.Length || page[row].Definition == null) return;
        focusedTraitId = page[row].Definition.Id;
        string reason;
        if (!model.ToggleTrait(focusedTraitId, out reason) && !string.IsNullOrEmpty(reason)) transientStatus = "[CC6B64]" + reason + "[-]";
        else transientStatus = string.Empty;
        RenderAll();
    }

    private void DietRow_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        int row;
        if (!dietIndex.TryGetValue(sender, out row)) return;
        RebirthDietDefinition[] page = model.GetDietPage(DietRows);
        if (row >= page.Length) return;
        model.SelectDiet(page[row].Id);
        transientStatus = string.Empty;
        RenderAll();
    }

    private void ProfileScroll(float delta)
    {
        ScrollEmbeddedPlayerProfiles(delta);
    }

    private void BackgroundScroll(float delta)
    {
        if(model==null)return;
        int old=model.BackgroundOffset;
        model.BackgroundOffset=ScrollOffset(model.BackgroundOffset,delta,model.GetBackgroundCount(),BackgroundRows);
        DebugCreatorScroll("background-wheel","delta="+delta+" old="+old+" new="+model.BackgroundOffset+" total="+model.GetBackgroundCount());
        RenderBackgrounds();
    }

    public void ScrollEmbeddedPlayerProfiles(float delta)
    {
        nativeProfileCount = nativeProfileSnapshot != null ? nativeProfileSnapshot.Count : 0;
        int old = nativeProfileScrollOffset;
        nativeProfileScrollOffset = ScrollOffset(nativeProfileScrollOffset, delta, nativeProfileCount, NativeProfileVisibleRows);
        DebugCreatorScroll("profile-wheel", "delta=" + delta + " old=" + old + " new=" + nativeProfileScrollOffset + " total=" + nativeProfileCount);
        RenderProfiles();
    }

    private void WireCreatorThumbDrag(XUiController thumb,bool profile)
    {
        if(thumb==null)return;
        if(thumb.ViewComponent!=null)thumb.ViewComponent.EventOnDrag=true;
        thumb.OnDrag+=delegate(XUiController sender,EDragType type,Vector2 delta)
        {
            DragCreatorThumb(profile,type,-delta.y);
        };
    }

    private void DragCreatorThumb(bool profile,EDragType type,float dy)
    {
        int total=profile?(nativeProfileSnapshot!=null?nativeProfileSnapshot.Count:0):(model!=null?model.GetBackgroundCount():0);
        int visible=profile?NativeProfileVisibleRows:BackgroundRows;
        int trackHeight=profile?NativeProfileTrackHeight:BackgroundTrackHeight;
        if(total<=visible)return;

        int thumbHeight=GetThumbHeight(total,visible,trackHeight);
        int travel=Math.Max(1,trackHeight-thumbHeight);
        int max=Math.Max(1,total-visible);
        int current=profile?nativeProfileScrollOffset:model.BackgroundOffset;
        bool active=profile?profileThumbDragging:backgroundThumbDragging;
        float pixel=profile?profileThumbDragY:backgroundThumbDragY;

        if(!active)
        {
            pixel=travel*(current/(float)max);
            active=true;
            DebugCreatorScroll(profile?"profile-drag-start":"background-drag-start","offset="+current+" pixel="+pixel+" travel="+travel+" total="+total);
        }

        if(type==EDragType.DragEnd)
        {
            if(profile){profileThumbDragging=false;profileThumbDragY=pixel;}else{backgroundThumbDragging=false;backgroundThumbDragY=pixel;}
            DebugCreatorScroll(profile?"profile-drag-end":"background-drag-end","offset="+current+" pixel="+pixel);
            return;
        }

        pixel=Mathf.Clamp(pixel+dy,0f,travel);
        int requested=(int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt((pixel/travel)*max),0,max), max, visible, RebirthScrollbarPagingPolicy.Enabled);
        if(profile){nativeProfileScrollOffset=requested;profileThumbDragging=true;profileThumbDragY=pixel;RenderProfiles();}
        else{model.BackgroundOffset=requested;backgroundThumbDragging=true;backgroundThumbDragY=pixel;RenderBackgrounds();}

        XUiV_Button thumb=profile?profileScrollThumb:backgroundScrollThumb;
        if(thumb!=null)
        {
            RebirthScrollbarPresentation.RenderThumb(thumb,trackHeight,visible,total,
                RebirthScrollbarPagingPolicy.Enabled?requested:pixel*max/Math.Max(1,travel),8,3);

        }
        DebugCreatorScroll(profile?"profile-drag":"background-drag","dy="+dy+" pixel="+pixel+" offset="+requested+" travel="+travel+" total="+total);
    }

    private void WireCreatorPageRegion(XUiController controller,bool profile,int direction)
    {
        if(controller==null)return;
        controller.OnPress+=delegate(XUiController sender,int mouseButton)
        {
            int total=profile?(nativeProfileSnapshot!=null?nativeProfileSnapshot.Count:0):(model!=null?model.GetBackgroundCount():0);
            int visible=profile?NativeProfileVisibleRows:BackgroundRows;
            int current=profile?nativeProfileScrollOffset:model.BackgroundOffset;
            int requested=ClampOffset(current+(direction*visible),total,visible);
            if(profile){nativeProfileScrollOffset=requested;RenderProfiles();}else{model.BackgroundOffset=requested;RenderBackgrounds();}
            DebugCreatorScroll(profile?"profile-track-page":"background-track-page","direction="+direction+" old="+current+" new="+requested+" total="+total);
        };
        WireScroll(controller,profile?ProfileScroll:BackgroundScroll);
    }

    private void PositiveTraitThumbDrag(float dy)
    {
        if (model == null) return;
        int before = model.PositiveTraitOffset;
        int offset = before;
        // Update the model offset before rendering. Previously DragScroll rendered first and the
        // model was updated afterward, leaving the visible rows one page behind the click mapping.
        DragScroll(ref offset, model.GetPositiveTraitCount(), TraitSideRows, TraitTrackHeight, dy, null);
        model.PositiveTraitOffset = offset;
#if REBIRTH_DEBUG
        if (RebirthSurvivorDebug.TraitUiLoggingEnabled && before != offset)
            Log.Out("[REBIRTH Survivor][TraitUI] positive-thumb offset=" + before + "->" + offset + " dy=" + dy.ToString(CultureInfo.InvariantCulture));
#endif
        RenderTraits();
    }

    private void NegativeTraitThumbDrag(float dy)
    {
        if (model == null) return;
        int before = model.NegativeTraitOffset;
        int offset = before;
        // Keep the rendered page and the row-click lookup on the same offset. This matters most
        // after dragging into later negative-Trait rows.
        DragScroll(ref offset, model.GetNegativeTraitCount(), TraitSideRows, TraitTrackHeight, dy, null);
        model.NegativeTraitOffset = offset;
#if REBIRTH_DEBUG
        if (RebirthSurvivorDebug.TraitUiLoggingEnabled && before != offset)
            Log.Out("[REBIRTH Survivor][TraitUI] negative-thumb offset=" + before + "->" + offset + " dy=" + dy.ToString(CultureInfo.InvariantCulture));
#endif
        RenderTraits();
    }

    private void PositiveTraitScroll(float delta)
    {
        if (model == null) return;
        model.PositiveTraitOffset = ScrollOffset(model.PositiveTraitOffset, delta, model.GetPositiveTraitCount(), TraitSideRows);
        RenderTraits();
    }

    private void NegativeTraitScroll(float delta)
    {
        if (model == null) return;
        model.NegativeTraitOffset = ScrollOffset(model.NegativeTraitOffset, delta, model.GetNegativeTraitCount(), TraitSideRows);
        RenderTraits();
    }

    private void TraitCategory_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null) return;
        RebirthSurvivorTraitCategoryFilter filter;
        if (!traitCategoryIndex.TryGetValue(sender, out filter)) return;
        model.SetTraitCategoryFilter(filter);
        focusedTraitId = string.Empty;
        transientStatus = string.Empty;
        RenderAll();
    }

    private void ResetTraits_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        model.ClearTraits();
        focusedTraitId = string.Empty;
        transientStatus = string.Empty;
        RenderAll();
    }

    private void TraitPrev_OnPressed(XUiController sender, int mouseButton) { if (model == null) return; model.TraitOffset = Math.Max(0, model.TraitOffset - TraitRows); RenderAll(); }
    private void TraitNext_OnPressed(XUiController sender, int mouseButton) { if (model == null) return; model.TraitOffset += TraitRows; RenderAll(); }
    private void TraitFilterPrev_OnPressed(XUiController sender, int mouseButton) { if (model == null || model.IsReadOnly) return; model.CycleTraitFilter(-1); focusedTraitId = string.Empty; RenderAll(); }
    private void TraitFilterNext_OnPressed(XUiController sender, int mouseButton) { if (model == null || model.IsReadOnly) return; model.CycleTraitFilter(1); focusedTraitId = string.Empty; RenderAll(); }

    private void RemoveUnavailableTraits_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        int removed = model.RemoveUnresolvedTraits();
        transientStatus = removed > 0
            ? "[D6C978]" + string.Format(CultureInfo.InvariantCulture, RebirthSurvivorUiText.L("xuiRebirthSurvivorRemovedUnavailableFormat", "Removed {0} unavailable saved Trait selection(s)."), removed) + "[-]"
            : string.Empty;
        RenderAll();
    }

    private void DietPrev_OnPressed(XUiController sender, int mouseButton) { if (model == null) return; model.DietOffset = Math.Max(0, model.DietOffset - DietRows); RenderAll(); }
    private void DietNext_OnPressed(XUiController sender, int mouseButton) { if (model == null) return; model.DietOffset += DietRows; RenderAll(); }

    private void Previous_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        if (!model.TryGoPrevious()) return;
        transientStatus = string.Empty;
        RenderAll();
        SelectFirstForStep();
    }

    private void Next_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null || model.IsReadOnly) return;
        string reason;
        if (!model.TryAdvance(out reason))
        {
            transientStatus = string.IsNullOrEmpty(reason) ? string.Empty : "[CC6B64]" + reason + "[-]";
            RenderAll();
            return;
        }
        transientStatus = string.Empty;
        RenderAll();
        SelectFirstForStep();
    }

    private void Save_OnPressed(XUiController sender, int mouseButton)
    {
        if (model == null) return;
        if (model.IsReadOnly)
        {
            Cancel_OnPressed(sender, mouseButton);
            return;
        }
        if (model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate)
        {
            if (!model.HasPlayerProfileSelection)
            {
                transientStatus = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileRequired", "Select a Player Profile before continuing.") + "[-]";
                RenderAll();
                return;
            }
            if (!RebirthSurvivorInstaller.IsInstalled)
            {
                transientStatus = "[CC6B64]" + RebirthSurvivorInstaller.LastReport + "[-]";
                RenderAll();
                return;
            }
            if (model.SaveAsReusableProfile && !model.HasValidProfileName)
            {
                transientStatus = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileNameRequired", "Enter a profile name before continuing.") + "[-]";
                RenderAll();
                return;
            }
            if (model.Validation == null || !model.Validation.IsValid || model.Validation.RemainingCreationPoints != 0)
            {
                transientStatus = "[CC6B64]" + (model.Validation != null && model.Validation.IsValid
                    ? RebirthSurvivorUiText.L("xuiRebirthCreationErrorUnspentCreationPoints", "Trait-point balance must be exactly zero before this Survivor can be used.")
                    : RebirthSurvivorUiText.BuildValidationSummary(model.Validation)) + "[-]";
                RenderAll();
                return;
            }
            Action<RebirthSurvivorCreatorViewModel> callback = onExternalConfirmed;
            RebirthSurvivorCreatorViewModel confirmedModel = model;
            CloseAndReset(false);
            if (callback != null) callback(confirmedModel);
            return;
        }

        RebirthSurvivorProfile saved;
        string error;
        if (!model.SaveLocalProfile(out saved, out error))
        {
            transientStatus = "[CC6B64]" + error + "[-]";
            RenderAll();
            return;
        }
        Action<RebirthSurvivorProfile> callbackSaved = onLocalProfileSaved;
        bool returnToManager = returnToProfileManagerAfterLocalSession;
        CloseAndReset(false);
        if (callbackSaved != null) callbackSaved(saved);
        if (returnToManager) ReturnToSurvivorProfileManager("local-save");
    }

    private void Cancel_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorCreatorPurpose purpose = model != null
            ? model.Purpose
            : RebirthSurvivorCreatorPurpose.CreateLocalProfile;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("CREATOR cancel purpose=" + (model != null ? model.Purpose.ToString() : "<null>") +
            " playerProfile='" + (model != null ? model.PlayerProfileName : string.Empty) + "'"); }
        if (model != null && model.Purpose == RebirthSurvivorCreatorPurpose.FirstWorldCreate)
        {
            Action firstWorldCancelled = onExternalCancelled;
            CloseAndReset(true);
            if (firstWorldCancelled != null) firstWorldCancelled();
            else RebirthSurvivorFirstEntryUiService.LeaveWorld();
            return;
        }
        Action cancelled = onExternalCancelled;
        bool returnToManager = returnToProfileManagerAfterLocalSession;
        CloseAndReset(true);
        if (cancelled != null) cancelled();
        if (returnToManager && (purpose == RebirthSurvivorCreatorPurpose.CreateLocalProfile ||
            purpose == RebirthSurvivorCreatorPurpose.EditLocalProfile ||
            purpose == RebirthSurvivorCreatorPurpose.ReviewLocalProfile))
            ReturnToSurvivorProfileManager("local-cancel:" + purpose);
    }

    private void ReturnToSurvivorProfileManager(string reason)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            Log.Error("[REBIRTH Survivor][UiRoute] RETURN-SURVIVOR-MANAGER failed reason=" + reason + " windowManager=unavailable");
            return;
        }

        GUIWindowManager manager = xui.playerUI.windowManager;
        // The manager is deliberately closed before Creator opens.  Reopen it as a fresh window
        // so OnOpen owns cursor/action-set setup and rebinds the selected profile preview from the
        // Survivor profile instead of inheriting Creator's temporary native selection.
        if (manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId))
            manager.Close(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
        manager.ResetActionSets();
        bool preserveSpawnHost = XUiC_RebirthSurvivorProfileManager.IsSpawnSelectionMode && manager.IsWindowOpen("spawnselection");
        if (preserveSpawnHost)
        {
            XUiController profileGroup = xui.FindWindowGroupByName(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
            if (profileGroup != null && profileGroup.windowGroup != null)
                manager.Open((GUIWindow)profileGroup.windowGroup, false, true);
            else
                manager.Open(XUiC_RebirthSurvivorProfileManager.WindowGroupId, false);
        }
        else
            manager.Open(XUiC_RebirthSurvivorProfileManager.WindowGroupId, true);

        bool open = manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
        bool spawnOpen = manager.IsWindowOpen("spawnselection");
        if (!open)
            Log.Error("[REBIRTH Survivor][UiRoute] RETURN-SURVIVOR-MANAGER reopen failed reason=" + reason);
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("RETURN-SURVIVOR-MANAGER reason=" + reason +
            " open=" + open + " reopenedFresh=True preserveSpawnHost=" + preserveSpawnHost + " spawnOpen=" + spawnOpen); }
    }

    private void CloseAndReset(bool cancelled)
    {
        Action<RebirthSurvivorProfile> saved = onLocalProfileSaved;
        onLocalProfileSaved = null;
        onExternalConfirmed = null;
        onExternalCancelled = null;
        RestoreUiInputState();
        if (xui != null && xui.playerUI != null && windowGroup != null)
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
        RestoreUiInputState();
        if (cancelled && saved != null) saved(null);
        model = null;
        transientStatus = string.Empty;
        focusedTraitId = string.Empty;
        suspendedForNativePicker = false;
        suspendedForNativeChildEditor = false;
        resumeFromNativeChildEditorPending = false;
        activeOpenRequestSerial = 0;
        activeOpenRequestReason = string.Empty;
    }

    private void ProfileName_OnChanged(XUiController sender, string text, bool changeFromCode)
    {
        if (model == null || model.IsReadOnly || changeFromCode) return;
        model.SetProfileName(text);
        transientStatus = string.Empty;
        RenderAll();
    }

    private void SelectFirstForStep()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.CursorController == null || model == null) return;
        XUiController target = null;
        switch (model.Step)
        {
            case RebirthSurvivorCreatorStep.Profile:
                target = GetChildById("btnRebirthProfileCreate");
                break;
            case RebirthSurvivorCreatorStep.Background: target = backgroundButtons[0]; break;
            case RebirthSurvivorCreatorStep.Traits: target = positiveTraitButtons[0] ?? negativeTraitButtons[0]; break;
            case RebirthSurvivorCreatorStep.Diet: target = dietButtons[0]; break;
            case RebirthSurvivorCreatorStep.Review:
                target = (profileNameInput != null && profileNameInput.Enabled) ? (XUiController)profileNameInput : btnSave;
                break;
        }
        if (target != null && target.ViewComponent != null) xui.playerUI.CursorController.SetNavigationTargetLater(target.ViewComponent);
    }

    private void SyncEmbeddedPlayerProfileSelection()
    {
        if (model == null || model.Step != RebirthSurvivorCreatorStep.Profile || model.IsReadOnly) return;
        string profileName;
        string source;
        if (!RebirthNativePlayerProfileBridge.TryResolveEmbeddedPlayerProfileName(this, out profileName, out source)) return;
        if (string.IsNullOrEmpty(profileName) || string.Equals(model.PlayerProfileName, profileName, StringComparison.Ordinal)) return;

        model.SetPlayerProfileName(profileName);
        transientStatus = string.Empty;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("EMBEDDED selected='" + profileName + "' source=" + source); }
        RenderAll();
    }

    private bool IsNativePlayerProfileModalOpen()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return false;
        GUIWindowManager manager = xui.playerUI.windowManager;
        return manager.IsWindowOpen("playerProfilesCreate") || manager.IsWindowOpen("customCharacterSystem") ||
               (playerProfileSaveOverlay != null && playerProfileSaveOverlay.ViewComponent != null && playerProfileSaveOverlay.ViewComponent.IsVisible) ||
               (playerProfileDeleteOverlay != null && playerProfileDeleteOverlay.ViewComponent != null && playerProfileDeleteOverlay.ViewComponent.IsVisible);
    }

    private void PrepareCursor()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.CursorController == null) return;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        cursor.Locked = false;
        cursor.SetCursorHidden(false);
        cursor.ResetNavigationTarget();
        SelectFirstForStep();
    }

    private void RestoreUiInputState()
    {
        if (xui == null || xui.playerUI == null) return;
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(wasCursorHidden);
            cursor.Locked = false;
            cursor.ResetNavigationTarget();
        }
        if (xui.playerUI.windowManager != null) xui.playerUI.windowManager.ResetActionSets();
    }

    private void EnsureSelectedProfileVisible()
    {
        if (model == null || !model.IsUsingExistingProfile) return;
        int index = IndexOfProfile(model.SelectedSourceProfileId);
        if (index < 0) return;
        if (index < profileOffset) profileOffset = index;
        else if (index >= profileOffset + ProfileRows) profileOffset = index - ProfileRows + 1;
        profileOffset = ClampOffset(profileOffset, profiles.Length, ProfileRows);
    }

    private int IndexOfProfile(string profileId)
    {
        if (string.IsNullOrEmpty(profileId)) return -1;
        for (int i = 0; i < profiles.Length; i++)
            if (profiles[i] != null && string.Equals(profiles[i].ProfileId, profileId, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static string ProfileCompatibilityShort(RebirthSurvivorProfileCompatibilityKind kind)
    {
        switch (kind)
        {
            case RebirthSurvivorProfileCompatibilityKind.Ready:
                return RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileReady", "Ready");
            case RebirthSurvivorProfileCompatibilityKind.NeedsReview:
                return RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileNeedsReview", "Needs Review");
            case RebirthSurvivorProfileCompatibilityKind.MissingDefinition:
                return RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileMissingDefinition", "Missing Definition");
            case RebirthSurvivorProfileCompatibilityKind.UnsupportedSchema:
                return RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileUnsupported", "Unsupported");
            default:
                return RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileInvalid", "Invalid");
        }
    }

    private static void DebugCreatorScroll(string action, string detail)
    {
        if (!RebirthSurvivorDebug.Enabled) return;
        Log.Out("[REBIRTH Survivor Creator][Scroll] action=" + action + " " + detail);
    }

    private static int ScrollOffset(int offset, float delta, int total, int visible)
    {
        if (RebirthScrollbarPagingPolicy.Enabled) return (int)RebirthScrollbarPagingPolicy.Step(offset, Math.Max(0, total - visible), visible, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        if (delta > 0f) offset--;
        else if (delta < 0f) offset++;
        return ClampOffset(offset, total, visible);
    }

    private static int ClampOffset(int value, int total, int visible)
    {
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value, 0, Math.Max(0, total - visible)), Math.Max(0, total - visible), visible, RebirthScrollbarPagingPolicy.Enabled);
    }

    private static void WireScroll(XUiController controller, Action<float> callback)
    {
        if (controller == null || callback == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta) { callback(delta); };
    }

    private void WireThumbDrag(XUiController thumb, Action<float> callback)
    {
        if (thumb == null || callback == null) return;
        if (thumb.ViewComponent != null) thumb.ViewComponent.EventOnDrag = true;
        thumb.OnDrag += delegate(XUiController sender, EDragType type, Vector2 delta)
        {
            if (type == EDragType.DragStart) pagingDragRemainder = 0f;
            if (type != EDragType.DragEnd) callback(-delta.y);
        };
    }

    private float pagingDragRemainder;
    private void DragScroll(ref int offset, int total, int visible, int trackHeight, float dy, Action render)
    {
        if (total <= visible || dy == 0f) return;
        int thumbHeight = GetThumbHeight(total, visible, trackHeight);
        int travel = Math.Max(1, trackHeight - thumbHeight);
        int max = Math.Max(1, total - visible);
        int step = Mathf.RoundToInt(dy * (max / (float)travel));
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            pagingDragRemainder = Mathf.Clamp(offset + pagingDragRemainder + dy * (max / (float)travel), 0f, max) - offset;
            int next = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(offset + pagingDragRemainder, max, visible, true);
            pagingDragRemainder -= next - offset; offset = next;
        }
        else { if (step == 0) step = dy > 0f ? 1 : -1; offset = ClampOffset(offset + step, total, visible); }
        if (render != null) render();
    }

    private static int GetThumbHeight(int total, int visible, int trackHeight)
    {
        if (total <= visible || total <= 0) return trackHeight;
        return RebirthScrollbarPresentation.ThumbHeight(trackHeight,visible,total);
    }

    private void UpdateTraitNativeScroll(bool negative, int offset, int total)
    {
        XUiController host = negative ? negativeTraitNativeScrollHost : positiveTraitNativeScrollHost;
        XUiController proxy = negative ? negativeTraitNativeScrollProxy : positiveTraitNativeScrollProxy;
        XUiController scrollView = negative ? negativeTraitNativeScrollView : positiveTraitNativeScrollView;
        if (host != null && host.ViewComponent != null) host.ViewComponent.IsVisible = total > TraitSideRows;
        if (proxy == null || proxy.ViewComponent == null) return;
        int contentHeight = total > TraitSideRows ? Math.Max(TraitTrackHeight + 1, Mathf.CeilToInt(TraitTrackHeight * (total / (float)TraitSideRows))) : TraitTrackHeight;
        proxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, total - TraitSideRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(offset / (float)maxOffset) : 0f;
        syncingTraitNativeScroll = true;
        RefreshNativeScrollView(scrollView);
        TrySetNativeScrollValue(scrollView, normalized);
        RefreshNativeScrollView(scrollView);
        syncingTraitNativeScroll = false;
    }

    private void PollTraitNativeScroll(bool negative)
    {
        if (syncingTraitNativeScroll || model == null) return;
        XUiController scrollView = negative ? negativeTraitNativeScrollView : positiveTraitNativeScrollView;
        int total = negative ? model.GetNegativeTraitCount() : model.GetPositiveTraitCount();
        int maxOffset = Math.Max(0, total - TraitSideRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!TryGetNativeScrollValue(scrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, TraitSideRows, RebirthScrollbarPagingPolicy.Enabled);
        int current = negative ? model.NegativeTraitOffset : model.PositiveTraitOffset;
        if (requested == current) return;
        if (negative) model.NegativeTraitOffset = requested; else model.PositiveTraitOffset = requested;
        RenderTraits();
    }

    private void UpdateCreatorNativeScroll(bool profile,int offset,int total,int visible,int trackHeight)
    {
        XUiController host=profile?profileNativeScrollHost:backgroundNativeScrollHost;
        XUiController proxy=profile?profileNativeScrollProxy:backgroundNativeScrollProxy;
        XUiController scrollView=profile?profileNativeScrollView:backgroundNativeScrollView;
        if(host!=null&&host.ViewComponent!=null)host.ViewComponent.IsVisible=true;
        if(proxy==null||proxy.ViewComponent==null)return;

        int contentHeight=total>visible?Math.Max(trackHeight+1,Mathf.CeilToInt(trackHeight*(total/(float)visible))):trackHeight;
        proxy.ViewComponent.Size=new Vector2i(1,contentHeight);
        int maxOffset=Math.Max(0,total-visible);
        float normalized=maxOffset>0?Mathf.Clamp01(offset/(float)maxOffset):0f;

        syncingCreatorNativeScroll=true;
        RefreshNativeScrollView(scrollView);
        TrySetNativeScrollValue(scrollView,normalized);
        syncingCreatorNativeScroll=false;
    }
    private void PollCreatorNativeScroll(bool profile)
    {
        if(syncingCreatorNativeScroll)return;
        XUiController scrollView=profile?profileNativeScrollView:backgroundNativeScrollView;
        int total=profile?(nativeProfileSnapshot!=null?nativeProfileSnapshot.Count:0):(model!=null?model.GetBackgroundCount():0);
        int visible=profile?NativeProfileVisibleRows:BackgroundRows;
        int maxOffset=Math.Max(0,total-visible);
        if(maxOffset<=0)return;

        float normalized;
        if(!TryGetNativeScrollValue(scrollView,out normalized))return;
        int requested=(int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized)*maxOffset),0,maxOffset), maxOffset, visible, RebirthScrollbarPagingPolicy.Enabled);
        int current=profile?nativeProfileScrollOffset:model.BackgroundOffset;
        if(requested==current)return;

        if(profile)
        {
            nativeProfileScrollOffset=requested;
            RenderProfiles();
            DebugCreatorScroll("profile-native","normalized="+normalized+" old="+current+" new="+requested+" total="+total);
        }
        else
        {
            model.BackgroundOffset=requested;
            RenderBackgrounds();
            DebugCreatorScroll("background-native","normalized="+normalized+" old="+current+" new="+requested+" total="+total);
        }
    }

    private void UpdateCreatorInteractiveScrollBar(bool profile,XUiController track,XUiV_Button thumb,XUiV_Sprite chrome,XUiV_Sprite chromeInner,XUiController pageUp,XUiController pageDown,int offset,int total,int visible,int trackHeight)
    {
        bool needed=total>visible&&total>0;
        if(track!=null&&track.ViewComponent!=null)track.ViewComponent.IsVisible=needed;
        if(thumb!=null)thumb.IsVisible=needed;
        if(chrome!=null)chrome.IsVisible=needed;
        if(chromeInner!=null)chromeInner.IsVisible=needed;
        if(!needed){SetVisible(pageUp,false);SetVisible(pageDown,false);return;}

        int h=GetThumbHeight(total,visible,trackHeight);
        int max=Math.Max(1,total-visible);
        int travel=Math.Max(0,trackHeight-h);
        bool dragging=profile?profileThumbDragging:backgroundThumbDragging;
        float dragY=profile?profileThumbDragY:backgroundThumbDragY;
        int y=dragging&&!RebirthScrollbarPagingPolicy.Enabled?Mathf.RoundToInt(Mathf.Clamp(dragY,0f,travel)):Mathf.RoundToInt(travel*(offset/(float)max));

        RebirthScrollbarPresentation.RenderThumb(thumb,trackHeight,visible,total,
            dragging&&!RebirthScrollbarPagingPolicy.Enabled?Mathf.Clamp(dragY,0f,travel)*max/Math.Max(1,travel):offset,8,3);


        int upHeight=Math.Max(0,y);
        int downY=y+h;
        int downHeight=Math.Max(0,trackHeight-downY);
        SetControllerRect(pageUp,2,0,10,Math.Max(1,upHeight));
        SetControllerRect(pageDown,2,-downY,10,Math.Max(1,downHeight));
        SetVisible(pageUp,upHeight>0);SetVisible(pageDown,downHeight>0);
    }

    private static void UpdateScrollBar(XUiController track,XUiV_Button thumb,int offset,int total,int visible,int trackHeight)
    {
        if(track?.ViewComponent!=null)track.ViewComponent.IsVisible=total>visible&&visible>0;
        RebirthScrollbarPresentation.RenderThumb(thumb,trackHeight,visible,total,offset,6);
    }
    private void WireTraitCategory(string id, RebirthSurvivorTraitCategoryFilter filter, int index)
    {
        XUiController button = Wire(id, TraitCategory_OnPressed);
        if (index >= 0 && index < traitCategoryButtons.Length) traitCategoryButtons[index] = button;
        if (button != null) traitCategoryIndex[button] = filter;
    }

    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController child = GetChildById(id);
        if (child != null) child.OnPress += handler;
        return child;
    }

    private XUiController Wire(string id, Action action)
    {
        return Wire(id, delegate(XUiController sender, int mouseButton) { action(); });
    }

    private XUiV_Label Label(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private XUiV_Sprite Sprite(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Sprite : null;
    }

    private static void SetSprite(XUiV_Sprite sprite, string name)
    {
        if (sprite == null) return;
        sprite.UIAtlas = "RebirthSurvivorIcons";
        sprite.SetSpriteImmediately(name ?? string.Empty);
        sprite.SetColorImmediately(Color.white);
    }

    private static void SetSpriteColor(XUiV_Sprite sprite, Color32 color)
    {
        if (sprite == null) return;
        sprite.Color = color;
        sprite.SetColorImmediately(color);
    }

    private static void SetItemSprite(XUiV_Sprite sprite, string name, Color tint)
    {
        if (sprite == null) return;
        sprite.UIAtlas = "ItemIconAtlas";
        sprite.SetSpriteImmediately(name ?? string.Empty);
        sprite.SetColorImmediately(tint);
    }

    private static void SetControllerPosition(XUiController controller, int x, int y)
    {
        if (controller == null || controller.ViewComponent == null) return;
        controller.ViewComponent.Position = new Vector2i(x, y);
    }

    private static void SetControllerRect(XUiController controller, int x, int y, int width, int height)
    {
        if(controller==null||controller.ViewComponent==null)return;
        controller.ViewComponent.Position=new Vector2i(x,y);
        controller.ViewComponent.Size=new Vector2i(width,height);
    }

    private static void SetControllerSize(XUiController controller, int width, int height)
    {
        if (controller == null || controller.ViewComponent == null) return;
        controller.ViewComponent.Size = new Vector2i(width, height);
    }

    private static void SetLabel(XUiV_Label label, string text) { if (label != null) label.Text = text ?? string.Empty; }
    private static void SetButtonText(XUiController controller, string text) { if (controller != null && controller.ViewComponent is XUiV_Label) ((XUiV_Label)controller.ViewComponent).Text = text ?? string.Empty; }
    private static void SetVisible(XUiController controller, bool visible) { if (controller != null && controller.ViewComponent != null) controller.ViewComponent.IsVisible = visible; }
    private static void SetVisible(XUiV_Label label, bool visible) { if (label != null) label.IsVisible = visible; }
    private static void SetEnabled(XUiController controller, bool enabled) { if (controller != null && controller.ViewComponent != null) controller.ViewComponent.Enabled = enabled; }
    private static void SetSelected(XUiController controller, bool selected)
    {
        if (controller == null || controller.ViewComponent == null) return;
        XUiV_Button button = controller.ViewComponent as XUiV_Button;
        if (button != null) button.Selected = selected;
    }

    private static string StepTitle(RebirthSurvivorCreatorStep step)
    {
        switch (step)
        {
            case RebirthSurvivorCreatorStep.Profile: return RebirthSurvivorUiText.L("xuiRebirthSurvivorStepProfile", "1. PROFILE");
            case RebirthSurvivorCreatorStep.Background: return RebirthSurvivorUiText.L("xuiRebirthSurvivorStepBackground", "2. EXPERIENCE");
            case RebirthSurvivorCreatorStep.Diet: return RebirthSurvivorUiText.L("xuiRebirthSurvivorStepDiet", "3. DIET");
            case RebirthSurvivorCreatorStep.Traits: return RebirthSurvivorUiText.L("xuiRebirthSurvivorStepTraits", "4. TRAITS");
            default: return RebirthSurvivorUiText.L("xuiRebirthSurvivorStepReview", "5. REVIEW");
        }
    }

    private static string PageText(int offset, int shown, int total, int pageSize)
    {
        if (total <= 0) return "0 / 0";
        int first = offset + 1;
        int last = Math.Min(total, offset + shown);
        return string.Format(CultureInfo.InvariantCulture, "{0}-{1} / {2}", first, last, total);
    }

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
