using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class NativeTools:IDisposable
    {
        internal Original Source;internal XUiC_WorkstationWindowGroup Window;internal XUiC_WorkstationToolGrid Grid;
        internal long Session;internal int Steps,Setups;internal bool Unknown,Header,OwnerSaved,SaveRequested,SyncEntered,SyncReturned,Saved,SavedEcho;
        internal RebirthProductiveEarnedPendingToolGestures.Frame Before,Last;internal RebirthProductiveEarnedRemoteToolPlan.Description Plan;internal byte[] Record;
        internal IDisposable Memory;internal NetPackageTileEntity Packet;
        internal bool Current()=>!Unknown&&Source?.Identity()==true&&ReferenceEquals(Window?.WorkstationData?.TileEntity,Source.Station)&&
            ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Source.Actor)&&ReferenceEquals(Window.GetChildByType<XUiC_WorkstationToolGrid>(),Grid)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var current)&&current==Session;
        public void Dispose(){Memory?.Dispose();Memory=null;Source=null;Window=null;Grid=null;Before=Last=null;Plan=null;Record=null;Packet=null;}
    }
    private sealed class ToolGestureCall {internal NativeTools Tools;internal bool Returned;}
    private sealed class ToolWindow {internal WeakReference<XUiC_WorkstationWindowGroup> Window;internal long Session;}
    private static readonly ConditionalWeakTable<TileEntityWorkstation,ToolWindow> toolWindows=new ConditionalWeakTable<TileEntityWorkstation,ToolWindow>();
    [ThreadStatic]private static NativeTools syncingTools;
    internal static void ObserveRemoteToolsWindow(XUiC_WorkstationWindowGroup window,long session)
    {
        var actor=window?.xui?.playerUI?.entityPlayer;var station=window?.WorkstationData?.TileEntity;
        if(actor?.world?.IsRemote()!=true||station==null||session<1||!ReferenceEquals(actor.world,GameManager.Instance?.World))return;
        toolWindows.Remove(station);toolWindows.Add(station,new ToolWindow{Window=new WeakReference<XUiC_WorkstationWindowGroup>(window),Session=session});
        ObserveViewerToolRequest(actor,station,window,session);
        if(originals.TryGetValue(station,out var source)&&source.Output?.Saved==true&&source.Output.Plan.PendingIndex>=0&&source.Tools==null&&source.Pending==null)
            GameManager.Instance.SaveLocalPlayerData(); // Genuine pre-gesture native baseline, not a synthetic owner receipt.
    }
    internal static object EnterRemoteToolGesture(XUiC_ItemStack controller,bool run)
    {
        if(!run||!RebirthProductiveEarnedCoordinator.Enabled||!ThreadManager.IsMainThread())return null;
        var window=controller?.GetParentByType<XUiC_WorkstationWindowGroup>()??XUiC_RebirthStationWorkspace.ActiveInstance;
        var station=window?.WorkstationData?.TileEntity;var grid=window?.GetChildByType<XUiC_WorkstationToolGrid>();
        if(station==null||grid==null||!originals.TryGetValue(station,out var source)||source.World.IsRemote()!=true||source.Output?.Saved!=true||source.Output.Plan.PendingIndex<0||source.Pending!=null)return null;
        if(!grid.GetItemStackControllers().Any(value=>ReferenceEquals(value,controller))&&controller.StackLocation!=XUiC_ItemStack.StackLocationTypes.Backpack&&controller.StackLocation!=XUiC_ItemStack.StackLocationTypes.ToolBelt)return null;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return null;
            if(source.Tools==null)
            {
                if(!CurrentNativeOutputOwner(source)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*source.Image.Length+8L*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+65536),out var held))return null;
                var initial=RebirthProductiveEarnedPendingToolGestures.Frame.Capture(source.Actor,grid);
                if(initial==null||!initial.Decode(3,out var cursor)||cursor.Length!=1||!cursor[0].IsEmpty()||
                    !RebirthProductiveEarnedArtifact.SameTools(station.Tools,Convert.FromBase64String(initial.Cells[2]))){held.Dispose();return null;}
                source.Tools=new NativeTools{Source=source,Window=window,Grid=grid,Session=session,Before=initial,Last=initial,Memory=held};
            }
            var tools=source.Tools;if(!tools.Current()||tools.Plan!=null||tools.Steps>=64)return null;
            var before=RebirthProductiveEarnedPendingToolGestures.Frame.Capture(source.Actor,grid);
            if(before==null||!before.Same(tools.Last)){tools.Unknown=true;return null;}
            return new ToolGestureCall{Tools=tools};
        }
    }
    internal static void ObserveRemoteToolGesture(bool run,object state)
    {
        if(!run||state is not ToolGestureCall call)return;var tools=call.Tools;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!tools.Current()){tools.Unknown=true;return;}
            var after=RebirthProductiveEarnedPendingToolGestures.Frame.Capture(tools.Source.Actor,tools.Grid);
            if(after==null||!tools.Before.Decode(0,out var bag)||!tools.Before.Decode(1,out var belt)||!tools.Before.Decode(2,out var beforeTools)||!tools.Before.Decode(3,out var beforeCursor)||
                !after.Decode(0,out var afterBag)||!after.Decode(1,out var afterBelt)||!after.Decode(2,out var afterTools)||!after.Decode(3,out var afterCursor)||
                !RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(bag,belt,beforeTools,beforeCursor[0],afterBag,afterBelt,afterTools,afterCursor[0])){tools.Unknown=true;return;}
            tools.Last=after;tools.Steps++;call.Returned=true;
        }
    }
    internal static void EndRemoteToolGesture(Exception error,object state)
    {if(state is ToolGestureCall call&&(error!=null||!call.Returned))call.Tools.Unknown=true;}
    private static bool PumpNativeTools(Original source)
    {
        var tools=source.Tools;if(tools==null)return false;
        if(tools.Saved)return false;
        if(!tools.Current()&&tools.Plan!=null&&toolWindows.TryGetValue(source.Station,out var observed)&&observed.Window.TryGetTarget(out var window)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session)&&session==observed.Session)
            AttachRemoteToolsWindow(tools,window,session);
        if(!tools.Current()||tools.Steps<1||tools.Unknown)
        {
            if(tools.Record!=null&&source.Identity())RebirthProductiveEarnedWire.Send(source.Actor,source.Station,RebirthProductiveEarnedWire.Kind.NativeToolPlan,source.Frame.Operation,source.Nonce,tools.Record,null);
            return true;
        }
        if(tools.Plan==null)
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!tools.Last.Decode(3,out var cursor)||cursor.Length!=1||!cursor[0].IsEmpty()||tools.Before.Cells[2]==tools.Last.Cells[2]||!CurrentNativeOutputOwner(source)||
                    !RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(source.Station,source.Station.Queue,null,null,null,tools.Grid.GetSlots(),out var before,out var after,out int beforeOffset,out int afterOffset))return true;
                var value=new RebirthProductiveEarnedRemoteToolPlan.Description{Operation=source.Frame.Operation,Birth=source.Frame.Birth,Nonce=source.Nonce,Gesture=Guid.NewGuid(),Digest=source.Digest,
                    BeforeBag=tools.Before.Cells[0],BeforeBelt=tools.Before.Cells[1],AfterBag=tools.Last.Cells[0],AfterBelt=tools.Last.Cells[1],Tools=tools.Last.Cells[2],
                    BagCount=tools.Before.Counts[0],BeltCount=tools.Before.Counts[1],OwnedBelt=RebirthToolbeltCapacity.GetOwnedSlotCount(source.Actor,tools.Before.Counts[1]),ToolCount=tools.Before.Counts[2],Before=before,After=after,BeforeOffset=beforeOffset,AfterOffset=afterOffset};
                byte[] record=RebirthProductiveEarnedRemoteToolPlan.Encode(value);
                if(!RebirthProductiveEarnedRemoteToolPlan.TryRead(record,source.Frame.Operation,source.Frame.Birth,source.Nonce,source.Digest,source.Output.Plan.Native,source.Output.Plan.Offset,out _)||
                    !WriteExact(source.Path+".nativeTools",record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return true;
                tools.Plan=value;tools.Record=record;TrimNativeToolsRetention(tools);
            }
        }
        if(RebirthProductiveEarnedWire.TryReadReply(source.Frame.Operation,out var reply)&&reply.Nonce==source.Nonce&&reply.Digest==source.Digest&&reply.Birth==source.Frame.Birth&&
            reply.World==source.WorldKey&&reply.Owner==source.Owner&&reply.Creation==source.Creation&&reply.Deaths==source.Deaths&&reply.Position.Equals(source.Station.ToWorldPos()))
        {
            if(reply.State==RebirthProductiveEarnedWire.Status.NativeToolHeader||reply.State==RebirthProductiveEarnedWire.Status.NativeToolSaved)
            {
                tools.Header=true;tools.OwnerSaved=reply.QueueOwner=="ownerSaved";
                if(reply.State==RebirthProductiveEarnedWire.Status.NativeToolSaved&&tools.SyncReturned&&MatchesSavedStageDescription(source,"nativeToolsSyncReturned",source.Nonce)&&
                    Stage(source,"nativeToolsSaved",source.Nonce))tools.Saved=true;
            }
        }
        if(tools.Header&&!tools.SaveRequested&&!tools.OwnerSaved)
        {
            tools.SaveRequested=true;if(!Stage(source,"nativeToolsOwnerUpload",source.Nonce))return true;
            GameManager.Instance.SaveLocalPlayerData(); // Actual native player-data upload and native host Save observer.
        }
        if(tools.OwnerSaved&&!tools.SyncEntered&&!File.Exists(source.Path+".nativeToolsSyncAttempt")&&!File.Exists(source.Path+".nativeToolsSyncAttempt.tmp"))
        {
            if(!Stage(source,"nativeToolsSyncAttempt",source.Nonce))return true;
            var previous=syncingTools;syncingTools=tools;
            try{tools.Window.syncTEfromUI();}
            finally{syncingTools=previous;}
        }
        RebirthProductiveEarnedWire.Send(source.Actor,source.Station,RebirthProductiveEarnedWire.Kind.NativeToolPlan,source.Frame.Operation,source.Nonce,tools.Record,null);
        if(tools.SyncEntered&&!tools.SyncReturned&&!tools.Saved)RebirthProductiveEarnedWire.Send(source.Actor,source.Station,RebirthProductiveEarnedWire.Kind.Query,source.Frame.Operation,source.Nonce,null,source.Digest,true);
        return true;
    }
    internal static bool EnterRemoteToolsSync(XUiC_WorkstationWindowGroup window,bool run,out object state)
    {
        state=null;var station=window?.WorkstationData?.TileEntity;
        if(station==null||!originals.TryGetValue(station,out var source)||source.Tools==null||source.Tools.Saved)return true;
        var tools=source.Tools;
        if(tools.Window==null&&tools.Plan!=null&&ReferenceEquals(window.xui?.playerUI?.entityPlayer,source.Actor)&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session))
        {if(!AttachRemoteToolsWindow(tools,window,session))return false;}
        if(!run||!ReferenceEquals(syncingTools,tools)||tools.SyncEntered||!tools.OwnerSaved||!tools.Current()||tools.Plan==null||!CurrentNativeOutputOwner(source)||
            !RebirthProductiveEarnedPendingToolGestures.Frame.Capture(source.Actor,tools.Grid).Same(tools.Last))return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!RebirthProductiveEarnedNativeBody.TryCaptureUiProjection(station,window.craftingQueue.GetRecipesToCraft().Select(RebirthProductiveEarnedUiOriginal.CardRow).ToArray(),
                window.inputWindow?.GetSlots(),window.outputWindow?.GetSlots(),window.fuelWindow?.GetSlots(),window.toolWindow?.GetSlots(),out _,out var after,out _,out int offset)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(after,offset,tools.Plan.After,tools.Plan.AfterOffset))return false;
        }
        tools.SyncEntered=true;state=tools;return true;
    }
    internal static void ObserveRemoteToolsSync(XUiC_WorkstationWindowGroup window,bool run,object state)
    {
        if(!run||state is not NativeTools tools||!ReferenceEquals(syncingTools,tools)||!ReferenceEquals(tools.Window,window)||!tools.SyncEntered||tools.Setups!=1||!tools.Current())return;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(tools.Source.Station,tools.Plan.After,tools.Plan.AfterOffset)&&Stage(tools.Source,"nativeToolsSyncReturned",tools.Source.Nonce))tools.SyncReturned=true;
    }
    internal static void ObserveRemoteToolsSetup(NetPackageTileEntity packet,TileEntity tile,StreamModeWrite mode,bool run)
    {
        var tools=syncingTools;if(!run||tools==null||!tools.SyncEntered||!tools.Current()||mode!=StreamModeWrite.ToServer||!ReferenceEquals(tools.Source.Station,tile)||packet.ms==null)return;
        if(packet.ms.Length<1||packet.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return;
        byte[] body=new byte[(int)packet.ms.Length];lock(packet.ms){long old=packet.ms.Position;try{packet.ms.Position=0;int at=0;while(at<body.Length){int n=packet.ms.Read(body,at,body.Length-at);if(n<1)return;at+=n;}}finally{packet.ms.Position=old;}}
        if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(body,tools.Plan.AfterOffset,tools.Plan.After,tools.Plan.AfterOffset)){tools.Unknown=true;return;}
        tools.Setups++;tools.Packet=packet;
    }
    private static RebirthProductiveEarnedNativeOutputPlan.Description EffectiveNativeOutput(Original source)
        =>source.Tools?.Saved==true?RebirthProductiveEarnedRemoteHost.EffectiveNativeOutput(source.Output.Plan,source.Tools.Plan):source.Output.Plan;
    private static void TrimNativeToolsRetention(NativeTools tools)
    {
        if(tools?.Memory==null||tools.Record==null||tools.Plan==null)return;
        RebirthProductiveEarnedObservationBudget.TryResizeDetached(tools.Memory,
            checked(4L*tools.Record.Length+2L*((tools.Before?.Size??0)+(tools.Last?.Size??0))+65536));
    }
    private static bool AttachRemoteToolsWindow(NativeTools tools,XUiC_WorkstationWindowGroup window,long session)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            var grid=window.GetChildByType<XUiC_WorkstationToolGrid>();
            var actual=decoder==null||grid==null?null:RebirthProductiveEarnedPendingToolGestures.Frame.Capture(tools.Source.Actor,grid);
            if(actual==null||actual.Cells[0]!=tools.Plan.AfterBag||actual.Cells[1]!=tools.Plan.AfterBelt||actual.Cells[2]!=tools.Plan.Tools||!actual.Decode(3,out var cursor)||!cursor[0].IsEmpty())return false;
            tools.Window=window;tools.Grid=grid;tools.Session=session;tools.Last=actual;return true;
        }
    }
    private static bool LoadNativeToolsDescription(Original source,RebirthProductiveEarnedObservationBudget.Decoder decoder)
    {
        string path=source.Path+".nativeTools";if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(source.Output?.Saved!=true||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*new FileInfo(path).Length+65536),out var held))return false;
        try
        {
            byte[] record=Read(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            if(decoder==null||!RebirthProductiveEarnedRemoteToolPlan.TryRead(record,source.Frame.Operation,source.Frame.Birth,source.Nonce,source.Digest,source.Output.Plan.Native,source.Output.Plan.Offset,out var plan))return false;
            var tools=new NativeTools{Source=source,Plan=plan,Record=record,Memory=held,Steps=1,
                SaveRequested=MatchesSavedStageDescription(source,"nativeToolsOwnerUpload",source.Nonce),SyncEntered=MatchesSavedStageDescription(source,"nativeToolsSyncAttempt",source.Nonce),
                SyncReturned=MatchesSavedStageDescription(source,"nativeToolsSyncReturned",source.Nonce),Saved=MatchesSavedStageDescription(source,"nativeToolsSaved",source.Nonce),
                SavedEcho=MatchesSavedStageDescription(source,"nativeToolsSavedEcho",source.Nonce)};
            if(tools.SyncReturned&&!tools.SyncEntered||tools.SavedEcho&&!tools.SyncEntered||tools.Saved&&!tools.SyncReturned&&!tools.SavedEcho)return false;
            tools.Before=new RebirthProductiveEarnedPendingToolGestures.Frame{Cells=new[]{plan.BeforeBag,plan.BeforeBelt,"",""},Counts=new[]{plan.BagCount,plan.BeltCount,plan.ToolCount,1}};
            // The new actual UI observation must fill its current complete
            // cursor image; metadata does not synthesize an old gesture return.
            source.Tools=tools;held=null;TrimNativeToolsRetention(tools);return true;
        }
        finally{held?.Dispose();}
    }
}
