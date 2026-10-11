using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Explicit row-based scrolling for the expandable physical backpack.
///
/// The stock Backpack controller owns item stacks/selection. This controller only moves the
/// authored grid inside its clipping scrollview and drives the visible scrollbar. It does not
/// change slot ownership, Bag capacity, encumbrance or item data.
/// </summary>
public sealed class XUiC_RebirthExpandableBackpackScroll : XUiController
{
    private static XUiC_RebirthExpandableBackpackScroll activeInstance;
    private static bool diagnosticLogging;
    private static long wheelEventCount;
    private static long geometryRefreshCount;
    private static string lastWheelSender=string.Empty;
    private static float lastWheelDelta;
    private static int lastWiredControllerCount;

    private const int Columns=7;
    private const int CellHeight=75;
    private const int ViewportHeight=301;
    private const int TrackHeight=301;
    private const int TrackTop=0;
    private const int VisibleRows=4;
    private const int MinThumbHeight=38;

    private XUiController inventory;
    private XUiController scrollTrack;
    private XUiController scrollThumb;
    private XUiController nativeScrollHost;
    private XUiController nativeScrollView;
    private XUiController pagingRegisteredHost;
    private UIScrollView pagingRegisteredView;
    private UIProgressBar pagingRegisteredBar;
    private void RefreshPagingRegistration()
    {
        UIScrollView view = (nativeScrollView?.ViewComponent as XUiV_ScrollView)?.scrollView;
        UIProgressBar bar = view != null ? view.verticalScrollBar : null;
        if (ReferenceEquals(pagingRegisteredHost, nativeScrollHost) && ReferenceEquals(pagingRegisteredView, view)
            && ReferenceEquals(pagingRegisteredBar, bar)) return;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(nativeScrollHost);
        pagingRegisteredHost = nativeScrollHost; pagingRegisteredView = view; pagingRegisteredBar = bar;
    }
    private XUiController nativeScrollProxy;
    private XUiController backpackViewport;
    private float lastNativeValue=-1f;
    private int lastNativeWriteFrame=-1;
    private bool wasVisibleLastUpdate;
    private float nextDiagnosticHeartbeatTime;
    private bool diagnosticHierarchyDumped;
    private int diagnosticNativeComponentFindFailures;
    private string diagnosticLastNativeComponent="<none>";

    private Vector2i baseGridPosition;
    private int rowOffset;
    private float pixelOffset;
    private int totalRows=1;
    private bool coreWired;
    private float nextWireCheck;
    private int wiredSlotGeneration=int.MinValue;
    private readonly HashSet<XUiController> scrollWiredControllers=new HashSet<XUiController>();
    private readonly HashSet<XUiC_ItemStack> interactionDiagnosticWiredSlots=new HashSet<XUiC_ItemStack>();
    private readonly Dictionary<XUiC_ItemStack,int> interactionDiagnosticSlotIndices=new Dictionary<XUiC_ItemStack,int>();
    private long diagnosticSlotPressCount;
    private long diagnosticSlotDragCount;
    private int automaticInteractionMissBudget=12;
    private int diagnosticPendingPressCheckFrame=-1;
    private long diagnosticPendingPressSnapshot;
    private string diagnosticPendingPressCandidate="<none>";
    private bool dragging;
    private bool pagingMode;
    private int lastEventScrollFrame=-1;
    private bool diagnosticUpdateProofLogged;
    private float dragAccumulatedUiY;
    private int dragStartOffset;
    private int openSettleFrames;
    private float nextTrackDiagnosticHeartbeat;
    private int diagnosticTrackPressCount;
    private int diagnosticThumbPressCount;
    private int diagnosticRawTrackMouseDownCount;
    private string diagnosticLastTrackEvent="<none>";
    private bool diagnosticBottomSnapshotLogged;
    private float diagnosticLastBottomPixel=-9999f;
    private Collider blockingBackgroundCollider;
    private bool blockingBackgroundColliderWasEnabled;
    private bool blockingBackgroundColliderLogged;
    private Collider blockingScrollSurfaceCollider;
    private bool blockingScrollSurfaceColliderWasEnabled;
    private bool blockingScrollSurfaceColliderLogged;

    public override void Init()
    {
        base.Init();
        activeInstance=this;
        ResolveControls();
        DisableBlockingBackpackBackgroundInput();
        RefreshPagingRegistration();
        if(Time.time>=nextWireCheck){nextWireCheck=Time.time+1f;WireControls();}
        DisableBlockingBackpackScrollSurfaceInput();
        RefreshGeometry(true);
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] INIT "+BuildDebugReport()+" "+BuildNativeDebugReport());
    }

    public override void OnOpen()
    {
        base.OnOpen();
        activeInstance=this;
        rowOffset=0;
        pixelOffset=0f;
        lastNativeValue=0f;
        lastNativeWriteFrame=-1;
        ResolveControls();
        DisableBlockingBackpackBackgroundInput();
        RefreshPagingRegistration();
        WireControls();
        DisableBlockingBackpackScrollSurfaceInput();
        RefreshGeometry(true);
        ForceTopPosition();
        openSettleFrames=3;
        wasVisibleLastUpdate=true;
        diagnosticHierarchyDumped=false;
        diagnosticBottomSnapshotLogged=false;
        diagnosticLastBottomPixel=-9999f;
        if (diagnosticLogging) Log.Out("[REBIRTH BackpackScroll] INPUT_READY "+BuildSlotInputForwardingReport());
        if(diagnosticLogging)
        {
            Log.Out("[REBIRTH BackpackScroll] ON_OPEN "+BuildDebugReport()+" "+BuildNativeDebugReport());
            DumpNativeHierarchy();
        }
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(nativeScrollHost);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        ResolveControls();
        DisableBlockingBackpackBackgroundInput();
        RefreshPagingRegistration();
        if(Time.time>=nextWireCheck){nextWireCheck=Time.time+1f;WireControls();}
        DisableBlockingBackpackScrollSurfaceInput();

        bool visible=ViewComponent!=null&&ViewComponent.IsVisible;
        if(visible&&!wasVisibleLastUpdate)
        {
            rowOffset=0;
            pixelOffset=0f;
            lastNativeValue=0f;
            lastNativeWriteFrame=-1;
            RefreshGeometry(true);
            ForceTopPosition();
            openSettleFrames=3;
        }
        else
        {
            RefreshGeometry(false);
        }
        wasVisibleLastUpdate=visible;

        if(visible && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() && xui?.DragAndDropWindow?.IsEmpty() == true)
        {
            bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
            if (pagingMode != pagingNow) { pagingMode = pagingNow; dragging = false; }
            if (pagingNow) SetPixelOffset(pixelOffset);
        }
        NormalizeBackpackViewport();
        if(visible)
            ApplyGridPosition();

        if(visible&&openSettleFrames>0)
        {
            rowOffset=0;
            pixelOffset=0f;
            ApplyGridPosition();
            UpdateScrollbar();
            openSettleFrames--;

            // Capture the baseline only after the final late-layout settle frame. This avoids
            // freezing an intermediate viewport position before XUi has finished opening.
            if(openSettleFrames==0)
            {
                        NormalizeBackpackViewport();
            }

            if(diagnosticLogging)
                Log.Out("[REBIRTH BackpackScroll] OPEN_SETTLE remaining="+openSettleFrames
                    +" gridPos="+inventory.ViewComponent.Position);
        }

        if(diagnosticLogging)
        {
            if(!diagnosticHierarchyDumped)DumpNativeHierarchy();
            if(Time.realtimeSinceStartup>=nextDiagnosticHeartbeatTime)
            {
                nextDiagnosticHeartbeatTime=Time.realtimeSinceStartup+0.5f;
                Log.Out("[REBIRTH BackpackScroll] HEARTBEAT "+BuildDebugReport()+" "+BuildNativeDebugReport()
                    +" hovered="+DescribeHoveredObject()
                    +" mouseWheel="+Input.GetAxis("Mouse ScrollWheel").ToString("0.###",System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if(diagnosticLogging && !diagnosticUpdateProofLogged)
        {
            diagnosticUpdateProofLogged=true;
            Log.Out("[REBIRTH BackpackScroll] UPDATE_ACTIVE "+BuildDebugReport());
        }
        if(!diagnosticLogging)diagnosticUpdateProofLogged=false;
        PollMouseWheelFallback();
        PollTrackClickDiagnostics();
        PollBackpackInteractionDiagnostics();
        PollBottomGeometryDiagnostics();
    }

    public override void OnClose()
    {
        diagnosticPendingPressCheckFrame=-1;
        diagnosticPendingPressCandidate=string.Empty;
        RestoreBlockingBackpackScrollSurfaceInput();
        RestoreBlockingBackpackBackgroundInput();
        base.OnClose();
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        pagingRegisteredHost = null; pagingRegisteredView = null; pagingRegisteredBar = null;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(nativeScrollHost);
    }

    public static void SetDiagnosticLogging(bool enabled)
    {
        diagnosticLogging=enabled;
        if(activeInstance!=null)
        {
            activeInstance.automaticInteractionMissBudget=enabled?12:0;
            activeInstance.diagnosticPendingPressCheckFrame=-1;
            activeInstance.diagnosticPendingPressCandidate=string.Empty;
        }
        Log.Out("[REBIRTH BackpackScroll] diagnosticLogging="+enabled);
        if(enabled&&activeInstance!=null)
            Log.Out(activeInstance.BuildDebugReport());
    }

    public static void ResetDiagnosticsForWorldChange()
    {
        diagnosticLogging=false;
        wheelEventCount=geometryRefreshCount=0;
        lastWheelSender=string.Empty;
        if(activeInstance!=null)
        {
            activeInstance.diagnosticPendingPressCheckFrame=-1;
            activeInstance.automaticInteractionMissBudget=0;
            activeInstance.diagnosticPendingPressCandidate=string.Empty;
        }
        activeInstance=null;
    }

    public static string BuildActiveDebugReport()
    {
        return activeInstance!=null?activeInstance.BuildDebugReport():
            "[REBIRTH BackpackScroll] controller=<inactive> logging="+diagnosticLogging
            +" wheelEvents="+wheelEventCount+" lastSender="+lastWheelSender+" lastDelta="+lastWheelDelta;
    }

    private string BuildDebugReport()
    {
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        int itemControllers=backpack!=null&&backpack.itemControllers!=null?backpack.itemControllers.Length:0;
        int visibleControllers=0;
        if(backpack!=null&&backpack.itemControllers!=null)
        {
            for(int i=0;i<backpack.itemControllers.Length;i++)
            {
                XUiC_ItemStack slot=backpack.itemControllers[i];
                if(slot!=null&&slot.ViewComponent!=null&&slot.ViewComponent.IsVisible)visibleControllers++;
            }
        }

        EntityPlayerLocal player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        int bagSlots=player!=null&&player.bag!=null&&player.bag.ItemGrid.items!=null?player.bag.ItemGrid.items.Length:0;
        int desired=player!=null?RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(player):0;
        Vector2i gridPos=inventory!=null&&inventory.ViewComponent!=null?inventory.ViewComponent.Position:new Vector2i(0,0);
        Vector2i gridSize=inventory!=null&&inventory.ViewComponent!=null?inventory.ViewComponent.Size:new Vector2i(0,0);
        XUiV_Grid grid=inventory!=null?inventory.ViewComponent as XUiV_Grid:null;
        int gridRows=grid!=null?grid.Rows:-1;

        return "[REBIRTH BackpackScroll]"
            +" logging="+diagnosticLogging
            +" bagSlots="+bagSlots
            +" desiredPhysical="+desired
            +" totalRows="+totalRows
            +" visibleRows="+VisibleRows
            +" rowOffset="+rowOffset
            +" maxOffset="+MaxOffset
            +" pixelOffset="+pixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
            +" maxPixelOffset="+MaxPixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
            +" gridRows="+gridRows
            +" gridPos="+gridPos
            +" gridSize="+gridSize
            +" itemControllers="+itemControllers
            +" visibleControllers="+visibleControllers
            +" wiredControllers="+scrollWiredControllers.Count
            +" wheelEvents="+wheelEventCount
            +" lastWheelSender="+lastWheelSender
            +" lastWheelDelta="+lastWheelDelta.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
            +" geometryRefreshes="+geometryRefreshCount
            +" trackPresses="+diagnosticTrackPressCount
            +" thumbPresses="+diagnosticThumbPressCount
            +" rawTrackMouseDowns="+diagnosticRawTrackMouseDownCount
            +" slotPresses="+diagnosticSlotPressCount
            +" slotDrags="+diagnosticSlotDragCount
            +" hitCandidate="+DescribeBackpackHitCandidate()
            +" lastTrackEvent="+diagnosticLastTrackEvent
            +" bottomGeometry=["+BuildBottomGeometryReport()+"]";
    }

    private void PollBottomGeometryDiagnostics()
    {
        if(!diagnosticLogging||MaxPixelOffset<=0f)return;

        bool atBottom=Mathf.Abs(pixelOffset-MaxPixelOffset)<=0.5f;
        if(!atBottom)
        {
            diagnosticBottomSnapshotLogged=false;
            diagnosticLastBottomPixel=pixelOffset;
            return;
        }

        // Wait until the max position has remained stable for a frame before taking the snapshot.
        if(!diagnosticBottomSnapshotLogged || Mathf.Abs(pixelOffset-diagnosticLastBottomPixel)>0.01f)
        {
            diagnosticLastBottomPixel=pixelOffset;
            diagnosticBottomSnapshotLogged=true;
            Log.Out("[REBIRTH BackpackScroll] BOTTOM_GEOMETRY "+BuildBottomGeometryReport());
        }
    }

    private string BuildBottomGeometryReport()
    {
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        int physical=0;
        EntityPlayerLocal player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        if(player!=null&&player.bag!=null&&player.bag.ItemGrid.items!=null)
            physical=Math.Min(player.bag.ItemGrid.items.Length,RebirthSurvivorGearService.MaxPhysicalBagSlots);

        string grid="<none>";
        if(inventory!=null&&inventory.ViewComponent!=null)
        {
            Transform gt=inventory.ViewComponent.UiTransform;
            grid="xuiPos="+inventory.ViewComponent.Position
                +" xuiSize="+inventory.ViewComponent.Size
                +" local="+(gt!=null?gt.localPosition.ToString():"<null>")
                +" world="+(gt!=null?gt.position.ToString():"<null>");
        }

        string viewport=DescribeViewportGeometry();
        string slots=DescribePhysicalSlotGeometry(backpack,physical);

        return "pixelOffset="+pixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
            +" maxPixelOffset="+MaxPixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
            +" contentHeight="+(totalRows*CellHeight)
            +" viewportHeight="+ViewportHeight
            +" totalRows="+totalRows
            +" physical="+physical
            +" grid={"+grid+"}"
            +" viewport={"+viewport+"}"
            +" slots={"+slots+"}";
    }

    private string DescribeViewportGeometry()
    {
        if(backpackViewport==null||backpackViewport.ViewComponent==null||backpackViewport.ViewComponent.UiTransform==null)
            return "<unavailable>";

        Transform t=backpackViewport.ViewComponent.UiTransform;
        string result="xuiPos="+backpackViewport.ViewComponent.Position
            +" xuiSize="+backpackViewport.ViewComponent.Size
            +" local="+t.localPosition
            +" world="+t.position;

        Component panel=t.GetComponent("UIPanel");
        if(panel==null&&t.parent!=null)panel=t.parent.GetComponent("UIPanel");
        if(panel!=null)
        {
            object clipRange=ReadMember(panel,"baseClipRegion");
            if(clipRange==null)clipRange=ReadMember(panel,"clipRange");
            object clipOffset=ReadMember(panel,"clipOffset");
            result+=" panel="+BuildComponentPath(panel)
                +" clipRange="+(clipRange!=null?clipRange.ToString():"<missing>")
                +" clipOffset="+(clipOffset!=null?clipOffset.ToString():"<missing>");
        }
        else result+=" panel=<none>";

        return result;
    }

    private string DescribePhysicalSlotGeometry(XUiC_Backpack backpack,int physical)
    {
        if(backpack==null||backpack.itemControllers==null||physical<=0)return "<none>";

        int last=Math.Min(physical-1,backpack.itemControllers.Length-1);
        int lastRowStart=(last/Columns)*Columns;
        int previousRowStart=Math.Max(0,lastRowStart-Columns);
        int firstVisibleCandidate=Math.Max(0,lastRowStart-(VisibleRows-1)*Columns);

        int[] indices=new int[]
        {
            0,
            firstVisibleCandidate,
            previousRowStart,
            lastRowStart,
            last
        };

        string result="";
        for(int i=0;i<indices.Length;i++)
        {
            int index=indices[i];
            if(index<0||index>=backpack.itemControllers.Length)continue;
            if(i>0)result+=" | ";
            result+="slot"+index+"="+DescribeSlotScreenGeometry(backpack.itemControllers[index]);
        }
        return result;
    }

    private static string DescribeSlotScreenGeometry(XUiC_ItemStack slot)
    {
        if(slot==null||slot.ViewComponent==null||slot.ViewComponent.UiTransform==null)return "<unavailable>";
        Transform t=slot.ViewComponent.UiTransform;
        Camera camera=UICamera.currentCamera;
        string screen="<no-camera>";
        if(camera!=null)
        {
            Vector3 p=camera.WorldToScreenPoint(t.position);
            screen="screen="+p.x.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +","+p.y.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
        }

        return "visible="+slot.ViewComponent.IsVisible
            +" xuiPos="+slot.ViewComponent.Position
            +" local="+t.localPosition
            +" world="+t.position
            +" "+screen;
    }

    private static string DescribeController(XUiController controller)
    {
        if(controller==null)return "<null>";
        string type=controller.GetType().Name;
        try
        {
            if(controller.ViewComponent!=null)
                return type+" view="+controller.ViewComponent.GetType().Name;
        }
        catch{}
        return type;
    }

    private void RefreshGeometry(bool force)
    {
        if(inventory==null||inventory.ViewComponent==null)return;
        geometryRefreshCount++;

        EntityPlayerLocal player=xui!=null&&xui.playerUI!=null?xui.playerUI.entityPlayer:null;
        int physical=0;
        if(player!=null&&player.bag!=null&&player.bag.ItemGrid.items!=null)
            physical=Math.Min(player.bag.ItemGrid.items.Length,RebirthSurvivorGearService.MaxPhysicalBagSlots);

        int rows=Math.Max(1,(physical+Columns-1)/Columns);

        XUiV_Grid grid=inventory.ViewComponent as XUiV_Grid;
        if(grid!=null&&grid.Rows!=rows)
            grid.Rows=rows;

        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        if(backpack!=null&&backpack.itemControllers!=null)
        {
            for(int i=0;i<backpack.itemControllers.Length;i++)
            {
                XUiC_ItemStack slot=backpack.itemControllers[i];
                if(slot==null||slot.ViewComponent==null)continue;
                bool shouldBeVisible=i<physical;
                if(slot.ViewComponent.IsVisible!=shouldBeVisible)
                    slot.ViewComponent.IsVisible=shouldBeVisible;
            }
        }

        if(rows!=totalRows||force)
        {
            int oldRows=totalRows;
            totalRows=rows;
            rowOffset=Mathf.Clamp(rowOffset,0,MaxOffset);
            pixelOffset=Mathf.Clamp(pixelOffset,0f,MaxPixelOffset);
            ApplyGridPosition();
            UpdateScrollbar();
            WriteRowOffsetToNativeScrollbar();
            if(diagnosticLogging)
                Log.Out("[REBIRTH BackpackScroll] GEOMETRY oldRows="+oldRows+" rows="+rows
                    +" physical="+physical+" offset="+rowOffset+" maxOffset="+MaxOffset
                    +" report="+BuildDebugReport());
        }
    }

    private void ResolveControls()
    {
        if(inventory==null)
        {
            inventory=GetChildById("inventory");
            if(inventory!=null&&inventory.ViewComponent!=null)
                baseGridPosition=inventory.ViewComponent.Position;
        }
        if(scrollTrack==null)scrollTrack=GetChildById("rebirthBackpackScrollTrack");
        if(scrollThumb==null)scrollThumb=GetChildById("rebirthBackpackScrollThumb");
        if(nativeScrollHost==null)nativeScrollHost=GetChildById("rebirthBackpackNativeScrollHost");
        if(nativeScrollView==null)nativeScrollView=GetChildById("rebirthBackpackNativeScrollView");
        if(nativeScrollProxy==null)nativeScrollProxy=GetChildById("rebirthBackpackNativeScrollProxy");
        if(backpackViewport==null)backpackViewport=GetChildById("rebirthBackpackScrollView");

    }

    private void DisableBlockingBackpackBackgroundInput()
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(ViewComponent==null||ViewComponent.UiTransform==null)return;

        Transform content=ViewComponent.UiTransform.parent;
        if(content==null)return;

        Transform background=content.Find("backgroundMain")??content.Find("sprBackground");
        if(background==null)return;

        Collider collider=background.GetComponent<Collider>();
        if(collider==null)return;

        if(blockingBackgroundCollider==null)
        {
            blockingBackgroundCollider=collider;
            blockingBackgroundColliderWasEnabled=collider.enabled;
        }

        if(collider.enabled)
            collider.enabled=false;

        if(!blockingBackgroundColliderLogged)
        {
            blockingBackgroundColliderLogged=true;
            if (diagnosticLogging) Log.Out("[REBIRTH BackpackScroll] INPUT_BLOCKER_DISABLED"
                +" path="+BuildTransformPath(background)
                +" collider="+collider.GetType().Name
                +" wasEnabled="+blockingBackgroundColliderWasEnabled);
        }
    }


    private void DisableBlockingBackpackScrollSurfaceInput()
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(ViewComponent==null||ViewComponent.UiTransform==null)return;

        // PC050 removed the authored backgroundMain collider, and the next live log showed the
        // controller's own full-viewport scroll surface immediately became the new NGUI hover
        // target above every ItemStack. Mouse-wheel scrolling no longer needs this collider: the
        // raw-wheel fallback is already strictly scoped to the backpack geometry. Disable this
        // surface so NGUI raycasts can reach each native XUiC_ItemStack collider again.
        Collider collider=ViewComponent.UiTransform.GetComponent<Collider>();
        if(collider==null)return;

        if(blockingScrollSurfaceCollider==null)
        {
            blockingScrollSurfaceCollider=collider;
            blockingScrollSurfaceColliderWasEnabled=collider.enabled;
        }

        if(collider.enabled)
            collider.enabled=false;

        // The root must not request NGUI wheel events after its collider is removed. Backpack
        // wheel ownership is handled by PollMouseWheelFallback over the slot/viewport geometry.
        ViewComponent.EventOnScroll=false;

        if(!blockingScrollSurfaceColliderLogged)
        {
            blockingScrollSurfaceColliderLogged=true;
            if (diagnosticLogging) Log.Out("[REBIRTH BackpackScroll] INPUT_SCROLL_SURFACE_DISABLED"
                +" path="+BuildTransformPath(ViewComponent.UiTransform)
                +" collider="+collider.GetType().Name
                +" wasEnabled="+blockingScrollSurfaceColliderWasEnabled);
        }
    }

    private void RestoreBlockingBackpackScrollSurfaceInput()
    {
        if(blockingScrollSurfaceCollider!=null&&blockingScrollSurfaceColliderWasEnabled)
            blockingScrollSurfaceCollider.enabled=true;
        blockingScrollSurfaceCollider=null;
        blockingScrollSurfaceColliderWasEnabled=false;
        blockingScrollSurfaceColliderLogged=false;
    }

    private void RestoreBlockingBackpackBackgroundInput()
    {
        if(blockingBackgroundCollider!=null&&blockingBackgroundColliderWasEnabled)
            blockingBackgroundCollider.enabled=true;
        blockingBackgroundCollider=null;
        blockingBackgroundColliderWasEnabled=false;
        blockingBackgroundColliderLogged=false;
    }

    private void WireControls()
    {
        if(inventory==null||scrollTrack==null||scrollThumb==null)return;

        if(!coreWired)
        {
            // Do not wire this full-viewport controller as an NGUI scroll event surface.
            // Its generated BoxCollider was proven by the PC050 log to sit above native ItemStacks.
            // Raw wheel polling keeps scrolling functional without stealing click/drag hit-tests.
            WireScrollOnce(nativeScrollHost);
            WireScrollOnce(nativeScrollView);
            WireScrollOnce(nativeScrollProxy);
            WireScrollOnce(scrollTrack);
            WireScrollOnce(scrollThumb);

            if(scrollThumb.ViewComponent!=null)scrollThumb.ViewComponent.EventOnDrag=true;
            scrollThumb.OnDrag+=Thumb_OnDrag;
            scrollTrack.OnPress+=Track_OnPressed;
            scrollTrack.OnPress+=Track_OnPressedDiagnostic;
            scrollThumb.OnPress+=Thumb_OnPressedDiagnostic;
            coreWired=true;
        }

        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        XUiC_ItemStack[] slots=backpack!=null?backpack.itemControllers:null;
        int generation=17;unchecked{for(int i=0;slots!=null&&i<slots.Length;i++)generation=generation*31+(slots[i]!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slots[i]):0);}
        if(generation==wiredSlotGeneration)return;
        wiredSlotGeneration=generation;
        // Re-walk only when the native backpack replaces its slot-controller generation.
        WireScrollRecursive(inventory);
        for(int i=0;slots!=null&&i<slots.Length;i++)WireScrollRecursive(slots[i]);
        WireSlotInteractionDiagnostics();
    }

    private void WireScrollRecursive(XUiController controller)
    {
        if(controller==null)return;
        WireScrollOnce(controller);
        if(controller.Children==null)return;
        for(int i=0;i<controller.Children.Count;i++)
            WireScrollRecursive(controller.Children[i]);
    }

    private void WireSlotInteractionDiagnostics()
    {
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        if(backpack==null||backpack.itemControllers==null)return;

        for(int i=0;i<backpack.itemControllers.Length;i++)
        {
            XUiC_ItemStack slot=backpack.itemControllers[i];
            if(slot==null)continue;

            // The PC049 log proved the visible ItemStack controllers have press forwarding but
            // zero drag forwarding. Re-enable the native press/drag flags while the blocker fix
            // below restores NGUI hit-testing to the ItemStack tree.
            if(slot.ViewComponent!=null)
            {
                slot.ViewComponent.EventOnPress=true;
                slot.ViewComponent.EventOnDrag=true;
            }

            if(interactionDiagnosticWiredSlots.Contains(slot))continue;
            interactionDiagnosticWiredSlots.Add(slot);
            interactionDiagnosticSlotIndices[slot]=i;

            // Observational listeners. They run after the native ItemStack event reaches the
            // controller and let the log distinguish hit-testing failures from ItemStack logic.
            slot.OnPress+=Slot_OnPressDiagnostic;
            slot.OnDrag+=Slot_OnDragDiagnostic;
        }
    }

    private void Slot_OnPressDiagnostic(XUiController sender,int mouseButton)
    {
        if(!diagnosticLogging)return;
        XUiC_ItemStack slot=sender as XUiC_ItemStack;
        int index=-1;
        if(slot!=null)interactionDiagnosticSlotIndices.TryGetValue(slot,out index);
        diagnosticSlotPressCount++;
        if(!diagnosticLogging)return;
        Log.Out("[REBIRTH BackpackScroll] SLOT_PRESS count="+diagnosticSlotPressCount
            +" slot="+index+" button="+mouseButton
            +" hovered="+DescribeHoveredObject()
            +" candidate="+DescribeBackpackHitCandidate()
            +" mouse="+((Vector2)Input.mousePosition).ToString());
    }

    private void Slot_OnDragDiagnostic(XUiController sender,EDragType dragType,Vector2 mousePositionDelta)
    {
        if(!diagnosticLogging)return;
        XUiC_ItemStack slot=sender as XUiC_ItemStack;
        int index=-1;
        if(slot!=null)interactionDiagnosticSlotIndices.TryGetValue(slot,out index);
        diagnosticSlotDragCount++;
        if(!diagnosticLogging)return;
        Log.Out("[REBIRTH BackpackScroll] SLOT_DRAG count="+diagnosticSlotDragCount
            +" slot="+index+" dragType="+dragType
            +" delta="+mousePositionDelta
            +" hovered="+DescribeHoveredObject());
    }

    private void WireScrollOnce(XUiController controller)
    {
        if(controller==null||scrollWiredControllers.Contains(controller))return;
        scrollWiredControllers.Add(controller);
        lastWiredControllerCount=scrollWiredControllers.Count;
        if(controller.ViewComponent!=null)controller.ViewComponent.EventOnScroll=true;
        controller.OnScroll+=HandleScroll;
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] wired "+DescribeController(controller)+" total="+scrollWiredControllers.Count);
    }

    private void HandleScroll(XUiController sender,float delta)
    {
        if(Mathf.Approximately(delta,0f))return;
        lastEventScrollFrame=Time.frameCount;
        if(diagnosticLogging)
        {
            wheelEventCount++;
            lastWheelSender=DescribeController(sender);
            lastWheelDelta=delta;
        }
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] WHEEL sender="+lastWheelSender
                +" delta="+delta.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" beforeOffset="+rowOffset+" maxOffset="+MaxOffset);
        if(RebirthScrollbarPagingPolicy.Enabled) SetPixelOffset(RebirthScrollbarPagingPolicy.Step(pixelOffset, MaxPixelOffset, VisibleRows * CellHeight, delta > 0f ? -1 : 1));
        else if(delta>0f)SetPixelOffset(pixelOffset-30f);
        else if(delta<0f)SetPixelOffset(pixelOffset+30f);
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] WHEEL result offset="+rowOffset+" gridPos="
                +(inventory!=null&&inventory.ViewComponent!=null?inventory.ViewComponent.Position.ToString():"<none>"));
    }

    private void PollMouseWheelFallback()
    {
        // Some native item_stack descendants do not forward XUi OnScroll even with EventOnScroll
        // enabled. Read the Unity wheel once per frame as a fallback, but only while NGUI says the
        // hovered object is inside this backpack scroll host. If XUi already delivered a wheel event
        // this frame, skip it so one notch can never advance twice.
        if(lastEventScrollFrame==Time.frameCount)return;
        if(ViewComponent==null || !ViewComponent.IsVisible)return;

        float delta=Input.GetAxis("Mouse ScrollWheel");
        if(Mathf.Abs(delta)<0.0001f)return;

        // Never consume a global wheel event merely because the backpack window is open.
        // The cursor must actually be over this backpack scroll host (or one of its item-stack
        // descendants). This prevents recipe-list scrolling from also moving backpack rows.
        if(!IsMouseOverBackpackArea())return;

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] RAW_WHEEL delta="
                +delta.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" hover="+DescribeHoveredObject()
                +" hostVisible=True beforeOffset="+rowOffset+" maxOffset="+MaxOffset);

        if(RebirthScrollbarPagingPolicy.Enabled) SetPixelOffset(RebirthScrollbarPagingPolicy.Step(pixelOffset, MaxPixelOffset, VisibleRows * CellHeight, delta > 0f ? -1 : 1));
        else if(delta>0f)SetPixelOffset(pixelOffset-30f);
        else if(delta<0f)SetPixelOffset(pixelOffset+30f);
    }

    private bool IsMouseOverBackpackArea()
    {
        GameObject hovered=UICamera.hoveredObject;
        Transform hoveredTransform=hovered!=null?hovered.transform:null;

        if(IsDescendantOfController(hoveredTransform,inventory))return true;
        if(IsDescendantOfController(hoveredTransform,backpackViewport))return true;
        if(IsDescendantOfController(hoveredTransform,scrollTrack))return true;
        if(IsDescendantOfController(hoveredTransform,scrollThumb))return true;
        if(IsDescendantOfController(hoveredTransform,this))return true;

        // Some item_stack templates report a nested NGUI collider whose transform is not exposed
        // through the same XUi controller ancestry. Fall back to the live slot collider trees so
        // mouse-wheel input over both occupied and empty backpack cells still targets the bag.
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        if(backpack!=null&&backpack.itemControllers!=null)
        {
            for(int i=0;i<backpack.itemControllers.Length;i++)
            {
                XUiC_ItemStack slot=backpack.itemControllers[i];
                if(slot==null||slot.ViewComponent==null||!slot.ViewComponent.IsVisible)continue;
                if(IsMouseInsideColliderTree(slot.ViewComponent.UiTransform))return true;
            }
        }
        return false;
    }

    private static bool IsDescendantOfController(Transform candidate,XUiController controller)
    {
        if(candidate==null||controller==null||controller.ViewComponent==null||controller.ViewComponent.UiTransform==null)return false;
        Transform root=controller.ViewComponent.UiTransform;
        Transform current=candidate;
        while(current!=null)
        {
            if(current==root)return true;
            current=current.parent;
        }
        return false;
    }

    private static bool IsMouseInsideColliderTree(Transform root)
    {
        if(root==null)return false;
        Camera camera=UICamera.currentCamera;
        if(camera==null)return false;
        Collider[] colliders=root.GetComponentsInChildren<Collider>(true);
        Vector2 mouse=(Vector2)Input.mousePosition;
        for(int i=0;i<colliders.Length;i++)
        {
            Collider collider=colliders[i];
            if(collider==null||!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
            Bounds b=collider.bounds;
            Vector3 min=camera.WorldToScreenPoint(b.min);
            Vector3 max=camera.WorldToScreenPoint(b.max);
            float left=Mathf.Min(min.x,max.x);
            float right=Mathf.Max(min.x,max.x);
            float bottom=Mathf.Min(min.y,max.y);
            float top=Mathf.Max(min.y,max.y);
            if(mouse.x>=left&&mouse.x<=right&&mouse.y>=bottom&&mouse.y<=top)return true;
        }
        return false;
    }

    private string DescribeBackpackHitCandidate()
    {
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        if(backpack==null||backpack.itemControllers==null)return "<no-backpack>";
        GameObject hovered=UICamera.hoveredObject;
        Transform hoveredTransform=hovered!=null?hovered.transform:null;
        for(int i=0;i<backpack.itemControllers.Length;i++)
        {
            XUiC_ItemStack slot=backpack.itemControllers[i];
            if(slot==null||slot.ViewComponent==null||!slot.ViewComponent.IsVisible)continue;
            if(IsDescendantOfController(hoveredTransform,slot)||IsMouseInsideColliderTree(slot.ViewComponent.UiTransform))
                return "slot="+i+" controller="+DescribeController(slot);
        }
        return "<none>";
    }

    private static string DescribeHoveredObject()
    {
        try
        {
            GameObject hovered=UICamera.hoveredObject;
            if(hovered==null)return "<none>";
            return hovered.name+" componentCount="+hovered.GetComponents<Component>().Length;
        }
        catch{return "<error>";}
    }


    private static string DescribeHoveredObjectDetailed()
    {
        try
        {
            GameObject hovered=UICamera.hoveredObject;
            if(hovered==null)return "<none>";
            Component[] components=hovered.GetComponents<Component>();
            string names="";
            for(int i=0;i<components.Length;i++)
            {
                Component component=components[i];
                if(component==null)continue;
                if(names.Length>0)names+=",";
                names+=component.GetType().Name;
            }
            return hovered.name
                +" path="+BuildTransformPath(hovered.transform)
                +" components=["+names+"]"
                +" layer="+hovered.layer
                +" active="+hovered.activeInHierarchy;
        }
        catch(Exception ex){return "<error "+ex.GetType().Name+">";}
    }


    private void PollBackpackInteractionDiagnostics()
    {
        if(!diagnosticLogging)return;

        if(diagnosticPendingPressCheckFrame>=0&&Time.frameCount>=diagnosticPendingPressCheckFrame)
        {
            if(diagnosticSlotPressCount==diagnosticPendingPressSnapshot)
            {
                Log.Out("[REBIRTH BackpackScroll] INTERACTION_MISS candidate="+diagnosticPendingPressCandidate
                    +" hoveredNow="+DescribeHoveredObjectDetailed()
                    +" pressForwarding="+BuildSlotInputForwardingReport()
                    +" note=no XUiC_ItemStack.OnPress observed within two frames of mouse-down");
            }
            diagnosticPendingPressCheckFrame=-1;
        }

        if(automaticInteractionMissBudget<=0 || !Input.GetMouseButtonDown(0))return;
        bool over=IsMouseOverBackpackArea();
        if(!over)return;
        automaticInteractionMissBudget--; // Count attempts, including successful native clicks.
        diagnosticPendingPressSnapshot=diagnosticSlotPressCount;
        diagnosticPendingPressCandidate=DescribeBackpackHitCandidate();
        diagnosticPendingPressCheckFrame=Time.frameCount+2;
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] BACKPACK_MOUSE_DOWN hovered="+DescribeHoveredObject()
                +" candidate="+diagnosticPendingPressCandidate
                +" root="+DescribeController(this)
                +" inventory="+DescribeController(inventory)
                +" mouse="+((Vector2)Input.mousePosition).ToString());
    }

    private string BuildSlotInputForwardingReport()
    {
        XUiC_Backpack backpack=inventory as XUiC_Backpack;
        if(backpack==null||backpack.itemControllers==null)return "<no-backpack>";
        int visible=0;
        int press=0;
        int drag=0;
        for(int i=0;i<backpack.itemControllers.Length;i++)
        {
            XUiC_ItemStack slot=backpack.itemControllers[i];
            if(slot==null||slot.ViewComponent==null||!slot.ViewComponent.IsVisible)continue;
            visible++;
            if(slot.ViewComponent.EventOnPress)press++;
            if(slot.ViewComponent.EventOnDrag)drag++;
        }
        return "visible="+visible+" press="+press+" drag="+drag;
    }


    private void Thumb_OnDrag(XUiController sender,EDragType dragType,Vector2 mousePositionDelta)
    {
        if(MaxPixelOffset<=0f)return;

        if(dragType==EDragType.DragStart)
        {
            dragging=true;
            dragAccumulatedUiY=0f;
            dragStartOffset=Mathf.RoundToInt(pixelOffset);
        }

        if(!dragging)return;

        int thumbHeight=GetThumbHeight();
        int travel=Math.Max(1,TrackHeight-thumbHeight);

        if(dragType!=EDragType.DragEnd)
            dragAccumulatedUiY+=-mousePositionDelta.y;

        float desired=dragStartOffset+(dragAccumulatedUiY*(MaxPixelOffset/travel));
        SetPixelOffset(desired);

        if(dragType==EDragType.DragEnd)
            dragging=false;
    }

    private void Track_OnPressed(XUiController sender,int mouseButton)
    {
        // XUiV_Button OnPress reports -1 for the mouse activation/release path used by this
        // transparent track overlay in V3.2 b9. Diagnostics proved every valid track click
        // arrives with button=-1, so rejecting anything other than 0 discarded every click.
        if((mouseButton!=0&&mouseButton!=-1)||MaxOffset<=0||scrollTrack==null
            ||scrollTrack.ViewComponent==null||scrollTrack.ViewComponent.UiTransform==null)return;

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] TRACK_CLICK_ACCEPTED button="+mouseButton
                +" hovered="+DescribeHoveredObject()
                +" rowOffsetBefore="+rowOffset+" max="+MaxOffset);

        Transform trackTransform=scrollTrack.ViewComponent.UiTransform;
        Collider collider=trackTransform.GetComponent<Collider>();
        Camera camera=UICamera.currentCamera;

        if(collider==null||camera==null)
        {
            // Conservative fallback if the XUi button has not produced its collider yet.
            SetRowOffset(rowOffset+1);
            return;
        }

        Vector2 mouse=UICamera.currentTouch!=null
            ?UICamera.currentTouch.pos
            :(Vector2)Input.mousePosition;

        Bounds bounds=collider.bounds;
        float screenY1=camera.WorldToScreenPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)).y;
        float screenY2=camera.WorldToScreenPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z)).y;

        float bottom=Mathf.Min(screenY1,screenY2);
        float top=Mathf.Max(screenY1,screenY2);
        float span=top-bottom;

        if(span<=0.001f)
        {
            SetRowOffset(rowOffset+1);
            return;
        }

        // Screen Y increases upward. Backpack offset 0 is the top, MaxOffset is the bottom.
        float normalized=1f-Mathf.Clamp01((mouse.y-bottom)/span);
        float requested=Mathf.Clamp(normalized*MaxPixelOffset,0f,MaxPixelOffset);

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] TRACK_CLICK mouseY="
                +mouse.y.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" top="+top.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" bottom="+bottom.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" normalized="+normalized.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)
                +" requestedPixels="+requested.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" maxPixels="+MaxPixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));

        SetPixelOffset(requested);
    }

    private void Track_OnPressedDiagnostic(XUiController sender,int mouseButton)
    {
        if(!diagnosticLogging)return;
        diagnosticTrackPressCount++;
        diagnosticLastTrackEvent="OnPress button="+mouseButton+" sender="+DescribeController(sender);
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] TRACK_EVENT OnPress count="+diagnosticTrackPressCount
                +" button="+mouseButton
                +" hovered="+DescribeHoveredObject()
                +" sender="+DescribeController(sender)
                +" "+BuildTrackHitDebugReport());
    }

    private void Thumb_OnPressedDiagnostic(XUiController sender,int mouseButton)
    {
        if(!diagnosticLogging)return;
        diagnosticThumbPressCount++;
        diagnosticLastTrackEvent="ThumbOnPress button="+mouseButton+" sender="+DescribeController(sender);
        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] THUMB_EVENT OnPress count="+diagnosticThumbPressCount
                +" button="+mouseButton
                +" hovered="+DescribeHoveredObject()
                +" sender="+DescribeController(sender)
                +" "+BuildTrackHitDebugReport());
    }

    private void PollTrackClickDiagnostics()
    {
        if(!diagnosticLogging)return;

        if(Time.realtimeSinceStartup>=nextTrackDiagnosticHeartbeat)
        {
            nextTrackDiagnosticHeartbeat=Time.realtimeSinceStartup+0.5f;
            Log.Out("[REBIRTH BackpackScroll] TRACK_HEARTBEAT "
                +BuildTrackHitDebugReport()
                +" hovered="+DescribeHoveredObject()
                +" mouseDown="+Input.GetMouseButton(0)
                +" mousePos="+((Vector2)Input.mousePosition).ToString()
                +" lastEvent="+diagnosticLastTrackEvent);
        }

        if(!Input.GetMouseButtonDown(0))return;

        diagnosticRawTrackMouseDownCount++;
        bool insideTrack=IsMouseInsideControllerCollider(scrollTrack);
        bool insideThumb=IsMouseInsideControllerCollider(scrollThumb);

        Log.Out("[REBIRTH BackpackScroll] RAW_MOUSE_DOWN count="+diagnosticRawTrackMouseDownCount
            +" insideTrack="+insideTrack
            +" insideThumb="+insideThumb
            +" hovered="+DescribeHoveredObject()
            +" mousePos="+((Vector2)Input.mousePosition).ToString()
            +" "+BuildTrackHitDebugReport());
    }

    private static bool IsMouseInsideControllerCollider(XUiController controller)
    {
        if(controller==null||controller.ViewComponent==null||controller.ViewComponent.UiTransform==null)return false;
        Transform t=controller.ViewComponent.UiTransform;
        Collider collider=t.GetComponent<Collider>();
        if(collider==null)return false;
        Camera camera=UICamera.currentCamera;
        if(camera==null)return false;

        Bounds b=collider.bounds;
        Vector3 min=camera.WorldToScreenPoint(b.min);
        Vector3 max=camera.WorldToScreenPoint(b.max);
        Vector2 mouse=(Vector2)Input.mousePosition;

        float left=Mathf.Min(min.x,max.x);
        float right=Mathf.Max(min.x,max.x);
        float bottom=Mathf.Min(min.y,max.y);
        float top=Mathf.Max(min.y,max.y);
        return mouse.x>=left&&mouse.x<=right&&mouse.y>=bottom&&mouse.y<=top;
    }

    private static string DescribeCollider(XUiController controller)
    {
        if(controller==null||controller.ViewComponent==null||controller.ViewComponent.UiTransform==null)return "<controller-unavailable>";
        Transform t=controller.ViewComponent.UiTransform;
        Collider collider=t.GetComponent<Collider>();
        if(collider==null)return "<no-collider> path="+BuildTransformPath(t);

        Camera camera=UICamera.currentCamera;
        string screen="<no-camera>";
        if(camera!=null)
        {
            Bounds b=collider.bounds;
            Vector3 min=camera.WorldToScreenPoint(b.min);
            Vector3 max=camera.WorldToScreenPoint(b.max);
            screen="screen=["+min.x.ToString("0.0")+","+min.y.ToString("0.0")
                +" -> "+max.x.ToString("0.0")+","+max.y.ToString("0.0")+"]";
        }

        return collider.GetType().Name
            +" enabled="+collider.enabled
            +" goActive="+t.gameObject.activeInHierarchy
            +" layer="+t.gameObject.layer
            +" path="+BuildTransformPath(t)
            +" "+screen;
    }

    private string BuildTrackHitDebugReport()
    {
        return "track="+DescribeController(scrollTrack)
            +" trackCollider="+DescribeCollider(scrollTrack)
            +" thumb="+DescribeController(scrollThumb)
            +" thumbCollider="+DescribeCollider(scrollThumb)
            +" nativeHost="+DescribeController(nativeScrollHost)
            +" trackPresses="+diagnosticTrackPressCount
            +" thumbPresses="+diagnosticThumbPressCount
            +" rawMouseDowns="+diagnosticRawTrackMouseDownCount;
    }


    private int MaxOffset
    {
        get { return Math.Max(0,totalRows-VisibleRows); }
    }

    private float MaxPixelOffset
    {
        get
        {
            // The grid's live Y position increases as content scrolls upward in this XUi layout.
            // Its bottom-stop distance is therefore the hidden content below the viewport:
            // full content height minus the visible viewport height.
            int contentHeight=totalRows*CellHeight;
            return Math.Max(0f,contentHeight-ViewportHeight);
        }
    }

    private void SetRowOffset(int requested)
    {
        int clamped=Mathf.Clamp(requested,0,MaxOffset);
        SetPixelOffset(Mathf.Clamp(clamped*CellHeight,0f,MaxPixelOffset));
    }

    private void SetPixelOffset(float requested)
    {
        float clamped=RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(requested, MaxPixelOffset, VisibleRows * CellHeight, true) : Mathf.Clamp(requested,0f,MaxPixelOffset);
        if(Mathf.Abs(clamped-pixelOffset)<0.01f)
        {
            UpdateScrollbar();
            WriteRowOffsetToNativeScrollbar();
            return;
        }

        pixelOffset=clamped;
        rowOffset=CellHeight>0?Mathf.Clamp(Mathf.FloorToInt(pixelOffset/CellHeight),0,MaxOffset):0;
        NormalizeBackpackViewport();
        ApplyGridPosition();
        UpdateScrollbar();
        WriteRowOffsetToNativeScrollbar();

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] PIXEL_OFFSET requested="
                +requested.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" applied="+pixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)
                +" rowFloor="+rowOffset
                +" maxPixels="+MaxPixelOffset.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture));
    }

    private void ApplyGridPosition()
    {
        if(inventory==null||inventory.ViewComponent==null)return;

        // XUi Y coordinates grow upward; adding one cell height moves the next logical row
        // into the fixed clipping viewport.
        Vector2i target=new Vector2i(
            baseGridPosition.x,
            baseGridPosition.y+Mathf.RoundToInt(pixelOffset));

        // XUiV_Grid.Position already owns the live NGUI transform. Writing both Position and
        // UiTransform.localPosition caused the scroll displacement to be applied twice in-game:
        // at max offset (~224 px) the effective movement was roughly ~448 px, which placed the
        // final row at the TOP of the viewport instead of the bottom.
        inventory.ViewComponent.Position=target;
    }

    private int GetThumbHeight()
    {
        if(totalRows<=VisibleRows)return TrackHeight;
        return RebirthScrollbarPresentation.ThumbHeight(TrackHeight,VisibleRows,totalRows);
    }

    private void UpdateScrollbar()
    {
        bool needed=MaxOffset>0;
        if(nativeScrollHost!=null&&nativeScrollHost.ViewComponent!=null)
            nativeScrollHost.ViewComponent.IsVisible=needed;
        if(scrollTrack!=null&&scrollTrack.ViewComponent!=null)
            scrollTrack.ViewComponent.IsVisible=needed;
        if(scrollThumb!=null&&scrollThumb.ViewComponent!=null)
            scrollThumb.ViewComponent.IsVisible=needed;
        if(!needed)return;

        if(nativeScrollProxy!=null&&nativeScrollProxy.ViewComponent!=null)
        {
            const int nativeViewportHeight=285;
            int contentHeight=Math.Max(nativeViewportHeight+1,
                Mathf.CeilToInt(nativeViewportHeight*(totalRows/(float)VisibleRows)));
            nativeScrollProxy.ViewComponent.Size=new Vector2i(1,contentHeight);
            RefreshNativeScrollView(nativeScrollView);
            WriteRowOffsetToNativeScrollbar();
        }

        RebirthScrollbarPresentation.Render(scrollTrack,scrollThumb,new Vector2i(544,TrackTop),
            TrackHeight,VisibleRows*CellHeight,totalRows*CellHeight,pixelOffset);
    }
    private void PollNativeScroll()
    {
        // Presentation-only native scrollbar. Do not consume its autonomous activation value.
    }

    private void WriteRowOffsetToNativeScrollbar()
    {
        if(MaxOffset<=0)return;
        float normalized=MaxPixelOffset>0f?Mathf.Clamp01(pixelOffset/MaxPixelOffset):0f;
        TrySetNativeScrollbarValue(normalized);
        lastNativeValue=normalized;
        lastNativeWriteFrame=Time.frameCount;
    }

    private void ForceTopPosition()
    {
        rowOffset=0;
        pixelOffset=0f;
        NormalizeBackpackViewport();
        ApplyGridPosition();
        UpdateScrollbar();
        WriteRowOffsetToNativeScrollbar();

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] OPEN_TOP offset=0 gridPos="
                +(inventory!=null&&inventory.ViewComponent!=null?inventory.ViewComponent.Position.ToString():"<none>"));
    }

    private void NormalizeBackpackViewport()
    {
        // The main backpack viewport is now an XML <panel clipping="softclip">, not a
        // <scrollview>. It therefore has no UIScrollView motion state to normalize.
        //
        // Keep this method as a narrow sanity hook because SetPixelOffset/Open call it before
        // applying grid position. It intentionally performs NO position correction.
        if(backpackViewport==null||backpackViewport.ViewComponent==null)return;
    }


    private Component FindNativeScrollbarComponent()
    {
        if(nativeScrollHost==null||nativeScrollHost.ViewComponent==null||nativeScrollHost.ViewComponent.UiTransform==null)
        {
            diagnosticNativeComponentFindFailures++;
            diagnosticLastNativeComponent="<host-unavailable>";
            return null;
        }

        Transform root=nativeScrollHost.ViewComponent.UiTransform;
        Component[] components=root.GetComponentsInChildren<Component>(true);
        for(int i=0;i<components.Length;i++)
        {
            Component component=components[i];
            if(component==null)continue;
            string typeName=component.GetType().Name;
            if(typeName=="UIScrollBar"||typeName=="UIScrollbar")
            {
                diagnosticLastNativeComponent=BuildComponentPath(component);
                return component;
            }
        }

        diagnosticNativeComponentFindFailures++;
        diagnosticLastNativeComponent="<not-found>";
        return null;
    }

    private bool TryGetNativeScrollbarValue(out float value)
    {
        value=0f;
        Component bar=FindNativeScrollbarComponent();
        if(bar==null)return false;

        object raw=ReadMember(bar,"value");
        if(raw==null)
        {
            if(diagnosticLogging)Log.Out("[REBIRTH BackpackScroll] NATIVE_READ value-member=<missing> component="+BuildComponentPath(bar));
            return false;
        }

        try
        {
            value=Convert.ToSingle(raw,System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch(Exception ex)
        {
            if(diagnosticLogging)Log.Out("[REBIRTH BackpackScroll] NATIVE_READ conversion-failed rawType="+raw.GetType().FullName+" ex="+ex.GetType().Name);
            return false;
        }
    }

    private bool TrySetNativeScrollbarValue(float value)
    {
        Component bar=FindNativeScrollbarComponent();
        if(bar==null)return false;
        value=Mathf.Clamp01(value);
        Type type=bar.GetType();

        System.Reflection.PropertyInfo property=type.GetProperty("value",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if(property!=null&&property.CanWrite)
        {
            property.SetValue(bar,value,null);
            return true;
        }

        System.Reflection.FieldInfo field=type.GetField("value",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if(field!=null)
        {
            field.SetValue(bar,value);
            return true;
        }

        if(diagnosticLogging)
            Log.Out("[REBIRTH BackpackScroll] NATIVE_WRITE value-member=<missing> component="+BuildComponentPath(bar));
        return false;
    }

    private static Component FindNativeScrollViewComponent(XUiController scrollViewController)
    {
        if(scrollViewController==null||scrollViewController.ViewComponent==null||scrollViewController.ViewComponent.UiTransform==null)return null;
        Transform t=scrollViewController.ViewComponent.UiTransform;
        Component component=t.GetComponent("UIScrollView");
        if(component!=null)return component;
        return t.parent!=null?t.parent.GetComponent("UIScrollView"):null;
    }

    private static object ReadMember(object instance,string name)
    {
        if(instance==null)return null;
        Type type=instance.GetType();
        System.Reflection.PropertyInfo property=type.GetProperty(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        if(property!=null&&property.CanRead)return property.GetValue(instance,null);
        System.Reflection.FieldInfo field=type.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        return field!=null?field.GetValue(instance):null;
    }

    private static void RefreshNativeScrollView(XUiController scrollViewController)
    {
        Component scroll=FindNativeScrollViewComponent(scrollViewController);
        if(scroll==null)return;
        Type type=scroll.GetType();
        System.Reflection.MethodInfo update=type.GetMethod("UpdateScrollbars",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new Type[]{typeof(bool)},null);
        if(update!=null){update.Invoke(scroll,new object[]{true});return;}
        System.Reflection.MethodInfo reset=type.GetMethod("ResetPosition",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,Type.EmptyTypes,null);
        if(reset!=null)reset.Invoke(scroll,null);
    }


    private static string BuildTransformPath(Transform transform)
    {
        if(transform==null)return "<null>";
        string path=transform.name;
        Transform current=transform.parent;
        int guard=0;
        while(current!=null&&guard++<12)
        {
            path=current.name+"/"+path;
            current=current.parent;
        }
        return path;
    }

    private static string BuildComponentPath(Component component)
    {
        if(component==null)return "<null>";
        return component.GetType().FullName+"@"+BuildTransformPath(component.transform);
    }

    private string BuildNativeDebugReport()
    {
        Component bar=FindNativeScrollbarComponent();
        string value="<unreadable>";
        if(bar!=null)
        {
            object raw=ReadMember(bar,"value");
            if(raw!=null)value=Convert.ToString(raw,System.Globalization.CultureInfo.InvariantCulture);
        }

        Component scroll=FindNativeScrollViewComponent(nativeScrollView);
        string scrollPath=scroll!=null?BuildComponentPath(scroll):"<none>";

        return "nativeHost="+DescribeController(nativeScrollHost)
            +" nativeView="+DescribeController(nativeScrollView)
            +" nativeProxy="+DescribeController(nativeScrollProxy)
            +" nativeBar="+(bar!=null?BuildComponentPath(bar):diagnosticLastNativeComponent)
            +" nativeBarValue="+value
            +" nativeScrollViewComponent="+scrollPath
            +" findFailures="+diagnosticNativeComponentFindFailures
            +" lastWriteFrame="+lastNativeWriteFrame
            +" frame="+Time.frameCount;
    }

    private void DumpNativeHierarchy()
    {
        diagnosticHierarchyDumped=true;
        if(nativeScrollHost==null||nativeScrollHost.ViewComponent==null||nativeScrollHost.ViewComponent.UiTransform==null)
        {
            Log.Out("[REBIRTH BackpackScroll] HIERARCHY nativeScrollHost=<unavailable>");
            return;
        }

        Transform root=nativeScrollHost.ViewComponent.UiTransform;
        Log.Out("[REBIRTH BackpackScroll] HIERARCHY root="+BuildTransformPath(root));
        DumpTransformRecursive(root,0);
    }

    private static void DumpTransformRecursive(Transform transform,int depth)
    {
        if(transform==null||depth>8)return;

        Component[] components=transform.GetComponents<Component>();
        string componentNames="";
        for(int i=0;i<components.Length;i++)
        {
            Component component=components[i];
            if(component==null)continue;
            if(componentNames.Length>0)componentNames+=",";
            componentNames+=component.GetType().FullName;
        }

        Collider collider=transform.GetComponent<Collider>();
        Log.Out("[REBIRTH BackpackScroll] HIERARCHY depth="+depth
            +" path="+BuildTransformPath(transform)
            +" active="+transform.gameObject.activeInHierarchy
            +" collider="+(collider!=null?collider.GetType().Name:"<none>")
            +" components=["+componentNames+"]");

        for(int i=0;i<transform.childCount;i++)
            DumpTransformRecursive(transform.GetChild(i),depth+1);
    }

}
