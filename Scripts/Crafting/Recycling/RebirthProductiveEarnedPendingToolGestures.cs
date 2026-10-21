using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Observes actual native gestures only. No gesture is blocked, invoked,
// replayed, paid or converted into a saved owner/rebase receipt by this class.
internal static partial class RebirthProductiveEarnedPendingToolGestures
{
    internal sealed class Frame
    {
        internal string[] Cells;internal int[] Counts;
        internal long Size=>Cells.Sum(value=>2L*value.Length);
        internal bool Same(Frame other)=>other!=null&&Counts.SequenceEqual(other.Counts)&&Cells.SequenceEqual(other.Cells);
        internal static Frame Capture(EntityPlayerLocal actor,XUiC_WorkstationToolGrid tools)
        {
            var cursor=tools.xui?.DragAndDropWindow?.CurrentStack;
            var values=new[]{actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,tools.GetSlots(),new[]{cursor}};
            if(values[0]==null||values[0].Length>169||values[1]==null||values[1].Length>20||values[2]==null||values[2].Length>255||cursor==null)return null;
            var result=new Frame{Cells=new string[4],Counts=new int[4]};
            for(int i=0;i<4;i++)
            {
                if(values[i].Length<1||!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(values[i],262144,349528,out result.Cells[i]))return null;
                result.Counts[i]=values[i].Length;
            }
            return result;
        }
        internal bool Decode(int index,out ItemStack[] items)
            =>RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(Cells[index],Counts[index],262144,349528,out items);
    }
    internal sealed class Observation:IDisposable
    {
        internal readonly Guid Operation=Guid.NewGuid();
        internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;internal XUiC_WorkstationWindowGroup Window;
        internal XUiC_WorkstationToolGrid Tools;internal World World;internal string Owner,Creation;internal int Deaths;
        internal long Session;internal int Steps;internal bool Unknown,Closed;
        internal RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation Source;
        internal Frame Before,After;private IDisposable memory;
        internal OwnerCapture OwnerBefore,OwnerAfter;internal bool BeforeSaveRequested;
        internal GestureJournal Journal;
        internal NativeToolSync StationSync;
        internal Observation(IDisposable allocation){memory=allocation;}
        internal bool WindowCurrent()
        {
            try{return !Closed&&!string.IsNullOrEmpty(Owner)&&!string.IsNullOrEmpty(Creation)&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&World?.IsRemote()==false&&
                ReferenceEquals(Actor?.world,World)&&ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&
                !Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
                RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&
                ReferenceEquals(Window?.xui?.playerUI?.entityPlayer,Actor)&&ReferenceEquals(Window.WorkstationData?.TileEntity,Station)&&
                ReferenceEquals(Window.GetChildByType<XUiC_WorkstationToolGrid>(),Tools)&&ReferenceEquals(Tools?.xui,Window.xui)&&
                (Station is RebirthCamperStationState mobile?RebirthCamperStationWindowSession.Admitted(Window,mobile):
                    LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true)&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(Window,out var session)&&session==Session;}
            catch{return false;}
        }
        internal SavedToolPublication ToolPublication;
        internal bool ReturnedContext()=>!Unknown&&Steps>0&&WindowCurrent()&&Source?.CurrentContext()==true&&
            observations.TryGetValue(Station,out var current)&&ReferenceEquals(current,this);
        internal bool BeforeContext()=>!Unknown&&Steps==0&&WindowCurrent()&&Source?.CurrentContext()==true&&
            observations.TryGetValue(Station,out var current)&&ReferenceEquals(current,this);
        internal bool Reserve(Frame candidate)
            =>memory!=null&&RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,
                checked(4L*((Before?.Size??0)+(After?.Size??0)+(candidate?.Size??0))+65536));
        public void Dispose()
        {
            if(Closed)return;Closed=true;observations.Remove(Station);Before=After=null;
            var source=Source;Source=null;var allocation=memory;memory=null;
            var ownerAfter=OwnerAfter;OwnerAfter=null;
            var ownerBefore=OwnerBefore;OwnerBefore=null;
            Journal=null; // Durable records are deliberately retained on disk.
            var stationSync=StationSync;StationSync=null;
            var toolPublication=ToolPublication;ToolPublication=null;toolPublication?.Dispose();
            Actor=null;Station=null;Window=null;Tools=null;World=null;Owner=Creation=null;
            try{stationSync?.Dispose();}finally{try{ownerBefore?.Dispose();}finally{try{ownerAfter?.Dispose();}finally{try{source?.Dispose();}finally{allocation?.Dispose();}}}}
        }
    }
    private sealed class Call {internal Observation Observation;internal object Remote;internal bool Returned;}
    private static readonly Dictionary<TileEntityWorkstation,Observation> observations=new Dictionary<TileEntityWorkstation,Observation>();
    [ThreadStatic]private static Call executing;
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;RebirthProductiveEarnedUiDeferredTransport.InstallRecipientWindowObserver();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.pending.tool.gesture.observer"),typeof(Gesture));
        InstallOwnerObserver();installed=true;
    }
    internal static void Pump()
    {
        if(!ThreadManager.IsMainThread()||executing!=null)return;
        foreach(var value in observations.Values.ToArray())
            if(!RebirthProductiveEarnedCoordinator.Enabled||!value.WindowCurrent()||value.Source?.RetainedContext()!=true)value.Dispose();
        PumpOwnerBefore();
        PumpJournalOwnerAfter();
        PumpToolPublications();
    }
    internal static bool TryObserveReturnedGesture(EntityPlayerLocal actor,TileEntityWorkstation station,out Observation observation)
    {
        observation=null;if(!ThreadManager.IsMainThread()||executing!=null||station==null)return false;
        try
        {
            if(!observations.TryGetValue(station,out var value)||!ReferenceEquals(value.Actor,actor)||!value.ReturnedContext())return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var live=Frame.Capture(actor,value.Tools);
                if(live==null||!value.After.Same(live))return false;observation=value;return true;
            }
        }
        catch{return false;}
    }
    private static bool Conserved(Frame before,Frame after)
    {
        long graph=0,scratch=0,bytes=0;
        foreach(var frame in new[]{before,after})foreach(string cell in frame.Cells)
        {
            byte[] raw=Convert.FromBase64String(cell);
            if(!RebirthProductiveEarnedObservationBudget.TryMeasureTools(raw,out long measured,out long workspace))return false;
            graph=checked(graph+measured);scratch=Math.Max(scratch,workspace);bytes=checked(bytes+raw.Length);
        }
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(2*graph+scratch+4*bytes+65536),out var allocation))return false;
        using(allocation)
        {
            if(!before.Decode(0,out var bag)||!before.Decode(1,out var belt)||!before.Decode(2,out var tools)||!before.Decode(3,out var cursor)||
                !after.Decode(0,out var nextBag)||!after.Decode(1,out var nextBelt)||!after.Decode(2,out var nextTools)||!after.Decode(3,out var nextCursor))return false;
            return RebirthProductiveEarnedPendingToolTransfer.ConservedGesture(bag,belt,tools,cursor[0],nextBag,nextBelt,nextTools,nextCursor[0]);
        }
    }
    private static bool SourceSlot(XUiC_ItemStack controller,EntityPlayerLocal actor,XUiC_WorkstationToolGrid tools)
    {
        if(tools.GetItemStackControllers().Any(value=>ReferenceEquals(value,controller)))return true;
        ItemStack[] slots=controller.StackLocation==XUiC_ItemStack.StackLocationTypes.Backpack?actor.bag.ItemGrid.items:
            controller.StackLocation==XUiC_ItemStack.StackLocationTypes.ToolBelt?actor.inventory.ItemGrid.items:null;
        return slots!=null&&controller.SlotNumber>=0&&controller.SlotNumber<slots.Length&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{controller.ItemStack},262144,349528,out var displayed)&&
            RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(new[]{slots[controller.SlotNumber]},262144,349528,out var physical)&&displayed==physical;
    }
    [HarmonyPatch]
    private static class Gesture
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"HandleStackSwap","SwapItem","HandlePartialStackPickup","HandleDropOne","HandleMoveToPreferredLocation"})
            {
                var method=AccessTools.Method(typeof(XUiC_ItemStack),name);
                if(method==null||method.ReturnType!=typeof(void)||method.GetParameters().Length!=0)throw new InvalidOperationException("Native tool gesture signature changed: "+name);
                yield return method;
            }
            var drop=AccessTools.DeclaredMethod(typeof(XUiC_RequiredItemStack),"HandleDropOne");
            if(drop==null||drop.ReturnType!=typeof(void)||drop.GetParameters().Length!=0)throw new InvalidOperationException("Native required tool drop signature changed.");
            yield return drop;
        }
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(XUiC_ItemStack __instance,bool __runOriginal,out Call __state)
        {
            __state=null;if(!__runOriginal||executing!=null||!RebirthProductiveEarnedCoordinator.Enabled||!ThreadManager.IsMainThread())return;
            try{var remote=RebirthProductiveEarnedRemoteClient.EnterRemoteToolGesture(__instance,__runOriginal);if(remote!=null){__state=new Call{Remote=remote};return;}}catch{}
            Observation created=null;IDisposable allocation=null;RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation source=null;
            try
            {
                Pump();var window=__instance.GetParentByType<XUiC_WorkstationWindowGroup>()??XUiC_RebirthStationWorkspace.ActiveInstance;
                var actor=window?.xui?.playerUI?.entityPlayer;var station=window?.WorkstationData?.TileEntity;var tools=window?.GetChildByType<XUiC_WorkstationToolGrid>();
                if(actor==null||station==null||tools==null||actor.world?.IsRemote()!=false||
                    !ReferenceEquals(__instance.xui,window.xui)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out long session))return;
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!SourceSlot(__instance,actor,tools))return;
                    if(!observations.TryGetValue(station,out var value))
                    {
                        if(observations.Count>=4||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||
                            !RebirthProductiveEarnedCoordinator.TryObserveSavedPendingTools(station,decoder,out source)||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out allocation))return;
                        created=new Observation(allocation){Actor=actor,Station=station,Window=window,Tools=tools,World=actor.world,Owner=owner.StorageKey,
                            Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,Session=session,Source=source};allocation=null;source=null;
                        created.Before=Frame.Capture(actor,tools);
                        if(created.Before==null||!created.Reserve(created.Before)||!created.WindowCurrent()||
                            !created.Source.MatchesBeforeTools(Convert.FromBase64String(created.Before.Cells[2]),decoder))return;
                        created.After=created.Before;observations.Add(station,created);value=created;created=null;
                    }
                    if(value.Unknown||value.Steps>=64||!value.WindowCurrent()||!value.Source.CurrentContext())return;
                    var before=Frame.Capture(actor,tools);
                    if(before==null||!value.Reserve(before)||!value.After.Same(before)){value.Unknown=true;return;}
                    if(value.Steps==0&&value.OwnerBefore?.Saved(value)==true&&value.Journal==null)
                    {
                        if(!GestureJournal.TryStart(value,out var journal)){value.Unknown=true;return;}
                        value.Journal=journal;
                    }
                    if(value.Journal!=null&&!value.Journal.Enter(value,before)){value.Unknown=true;return;}
                    __state=new Call{Observation=value};executing=__state;
                }
            }
            catch{/* Observation failure changes no native gesture. */}
            finally{created?.Dispose();allocation?.Dispose();source?.Dispose();}
        }
        private static void Postfix(bool __runOriginal,Call __state)
        {
            try{RebirthProductiveEarnedRemoteClient.ObserveRemoteToolGesture(__runOriginal,__state?.Remote);}catch{}
            if(!__runOriginal||__state==null||!ReferenceEquals(executing,__state))return;
            var value=__state.Observation;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!value.WindowCurrent()||!value.Source.CurrentContext()){value.Unknown=true;return;}
                    var after=Frame.Capture(value.Actor,value.Tools);
                    if(after==null||!value.Reserve(after)||!Conserved(value.Before,after)){value.Unknown=true;return;}
                    if(value.Journal!=null&&!value.Journal.Return(value,after)){value.Unknown=true;return;}
                    value.OwnerAfter?.Dispose();value.OwnerAfter=null;
                    value.After=after;value.Steps++;__state.Returned=true;value.Reserve(null);
                }
            }
            catch{value.Unknown=true;}
        }
        private static Exception Finalizer(Exception __exception,Call __state)
        {
            try{RebirthProductiveEarnedRemoteClient.EndRemoteToolGesture(__exception,__state?.Remote);}catch{}
            if(__state!=null&&ReferenceEquals(executing,__state))
            {if(__exception!=null||!__state.Returned)__state.Observation.Unknown=true;executing=null;}
            return __exception;
        }
    }
}
