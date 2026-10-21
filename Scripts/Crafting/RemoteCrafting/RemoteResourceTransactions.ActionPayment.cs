using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Distinct owning-action payment originals. Fetch/camper tokens are never imported.
public static partial class RemoteResourceTransactions
{
    internal enum ActionPaymentState:byte { Prepared, Paying, Paid, Returning, Returned, Used, Uncertain }
    internal enum ActionLegState:byte { Before, DebitAttempted, Debited, Refused, ReturnAttempted, Returned, Unknown }
    internal sealed class ActionLeg
    {
        internal readonly string Id;internal readonly IRemoteResourceSource Source;
        internal readonly ItemStack[] Before,After;internal readonly bool[] Locks;
        internal readonly ItemStackGrid Grid;internal readonly object Endpoint;
        internal RebirthRemoteResourceNativeBinding Native;
        internal RebirthLogisticsEndpointIdentity.Admission StationIdentity;internal RebirthLogisticsVehiclePublication.Expected VehicleIdentity;internal RebirthLogisticsDronePublication.Expected DroneIdentity;
        internal readonly RemoteNpcResourceSource Npc;
        internal readonly bool Local,Belt;internal bool RemoteOwner,HistoricalPaid;internal readonly int Revision;
        internal readonly int[] Slots;internal readonly ItemStack[] Paid;internal readonly Guid DebitMutation=Guid.NewGuid(),ReturnMutation=Guid.NewGuid();internal RebirthNpcInventorySnapshot NpcBefore,NpcAfter;internal byte[] NpcBodyBefore;internal RemoteNpcResourceSource.CommitEvent ReturnCommit;
        internal ActionLegState State;internal RemoteNpcResourceSource.CommitEvent Commit;
        internal string RefusalStage,NeverEnteredIntent;internal XElement RefusalDetail;
        internal RebirthRemoteResourceNativePublication.Watch Publication;
        internal ActionSavedMaterial SavedMaterial;
        internal bool NativeLifeEnded;
        internal bool BeforeSaved,AfterSaved,ReturnSaved;internal readonly bool[] DebitedSlots,ReturnedSlots,AttemptedSlots;internal ItemStack[] Progress;internal string SavedBefore,SavedAfter,SavedReturn;
        internal ActionLeg(string id,IRemoteResourceSource source,ItemStack[] before,ItemStack[] after,bool[] locks,int[] slots,ItemStack[] paid,
            object endpoint,ItemStackGrid grid,RebirthRemoteResourceNativeBinding native,bool local,bool belt,int revision,Guid? debit=null,Guid? returned=null)
        {Id=id;Source=source;Before=before;After=after;Locks=locks;Slots=slots;Paid=paid;Endpoint=endpoint;Grid=grid;Native=native;Npc=source as RemoteNpcResourceSource;Local=local;Belt=belt;Revision=revision;if(debit.HasValue)DebitMutation=debit.Value;if(returned.HasValue)ReturnMutation=returned.Value;DebitedSlots=new bool[slots.Length];ReturnedSlots=new bool[slots.Length];AttemptedSlots=new bool[slots.Length];Progress=ItemStack.Clone(before);}
    }
    internal sealed class ActionPayment
    {
        internal readonly Guid Operation;internal World World;internal EntityPlayer Actor;
        internal readonly string Account,Creation,Save,Directory,Action,Target,NativeWorld;
        internal readonly int Deaths,NativeEntityId;internal readonly ulong Recovery;internal readonly List<ActionLeg> Legs;
        internal TileEntityPowered ServiceTarget;internal readonly RebirthElectricalServiceRecord ServiceCandidate;
        internal readonly ActionRemoteHostOriginal RemoteOwner;
        internal readonly float TrainingRaw;
        internal readonly bool TrainingRequired;
        internal long TrainingNextAttempt;
        internal ActionTargetNativeOriginal RestartTargetSource;
        internal XElement ServiceBefore;internal bool ReturnedPublicationNeedsRestore;
        internal ActionSavedPaidPublication SavedPaidPublication;
        internal bool RemoteDebitEntered,RemoteRefundEntered,RemoteTerminalNativeSaved,RemoteTerminalReleased;internal long RemoteGrantNext;internal RebirthRemoteResourceFragments.Body RemoteGrantBody;internal IDisposable RemoteGrantMemory;
        internal ItemStackGrid OwnerBagGrid,OwnerBeltGrid;internal ItemStack[] OwnerBagBefore,OwnerBeltBefore;
        internal readonly IDisposable Memory;internal string Original;internal long OriginalBytesBound;internal int Sequence;internal readonly Dictionary<string,int> Events=new Dictionary<string,int>(StringComparer.Ordinal);
        internal readonly Dictionary<string,bool> RefusedBoundaries=new Dictionary<string,bool>(StringComparer.Ordinal);
        internal ActionPaymentState State;internal bool PublicationEntered,PublicationReturned,Terminal,Retired,MemoryReleased,AdmissionReserved,OwnerObservationsReleased,Notified,OwnerNativeLifeEnded;
#if REBIRTH_DEBUG
        internal bool ScenarioRetain,ScenarioPaused,ScenarioMutationObserved;internal string ScenarioArgumentMutation;
        internal readonly Dictionary<string,int> ScenarioNativeCalls=new Dictionary<string,int>(StringComparer.Ordinal);
#endif
        internal bool OwnerBeforeSaved,OwnerAfterSaved,OwnerReturnSaved,TargetBeforeSaved,TargetAfterSaved;internal RebirthRemoteResourceNativePublication.Watch TargetPublication;
        internal ActionSavedMaterial TargetMaterial,ServiceMaterial;internal bool ServiceAfterSaved;
        internal ActionPayment(EntityPlayer actor,string account,string creation,string save,List<ActionLeg> legs,TileEntityPowered tile,RebirthElectricalServiceRecord candidate,IDisposable memory,ItemStack[] ownerBag,ItemStack[] ownerBelt,ActionRemoteHostOriginal remoteOwner=null,int? originalDeaths=null,float? originalTrainingRaw=null,bool? originalTrainingRequired=null,Guid? originalOperation=null)
        {Operation=remoteOwner?.Operation??originalOperation??Guid.NewGuid();RemoteOwner=remoteOwner;TrainingRequired=originalTrainingRequired??RebirthSurvivorMode.IsEnabledForCurrentWorld();TrainingRaw=originalTrainingRaw??(TrainingRequired?RebirthPhase9TechnicalTrainingService.CaptureElectricalOriginalTraining(candidate.Kind):0f);if(float.IsNaN(TrainingRaw)||float.IsInfinity(TrainingRaw)||TrainingRaw<0f||TrainingRaw>10f||!TrainingRequired&&TrainingRaw!=0f)throw new InvalidDataException("Electrical original training amount is invalid");World=actor.world;NativeWorld=World.worldState.Guid;Actor=actor;NativeEntityId=actor.entityId;Account=account;Creation=creation;Deaths=originalDeaths??actor.Died;Save=save;Recovery=RebirthNpcRecoveryGeneration.RequireCurrent();Legs=legs;ServiceTarget=tile;ServiceCandidate=CopyActionCandidate(candidate);Memory=memory;Action="electrical-service";Target=candidate.SourceIncarnation;Directory=Path.Combine(save,"RebirthData","ActionPayments",Operation.ToString("N"));OwnerBagBefore=ownerBag;OwnerBeltBefore=ownerBelt;if(ownerBag!=null&&remoteOwner==null){OwnerBagGrid=actor.bag.ItemGrid;OwnerBeltGrid=actor.inventory.ItemGrid;}}
        internal bool ActorCurrent()=>!Terminal&&!Retired&&ThreadManager.IsMainThread()&&World!=null&&!World.IsRemote()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            ReferenceEquals(Actor.world,World)&&Actor.entityId==NativeEntityId&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&!Actor.IsDead()&&(Actor.Died==Deaths||SavedPaidPublication?.Current==true)&&
            RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&RebirthStablePlayerIdentity.TryResolveServerEntity(Actor,out var account)&&account.CanonicalId==Account&&
            Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&NativeWorld==World.worldState.Guid&&Recovery==RebirthNpcRecoveryGeneration.RequireCurrent();
        internal bool TargetCurrent()=>ActorCurrent()&&(RestartTargetSource==null||RestartTargetSource.Current())&&ReferenceEquals(World.GetTileEntity(ServiceCandidate.Position),ServiceTarget)&&RebirthElectricalSourceIdentity.Matches(World,ServiceCandidate);
    }
    private static readonly List<ActionPayment> actionPayments=new List<ActionPayment>();
    internal static bool IsActionPaymentHeld(EntityPlayerLocal actor)
    {lock(actionOwnerGate)return actor!=null&&(ActionClientLoadedInventoryHeld(actor.bag?.ItemGrid)||ActionClientLoadedInventoryHeld(actor.inventory?.ItemGrid)||ActionHostLoadedInventoryHeld(actor.bag?.ItemGrid)||ActionHostLoadedInventoryHeld(actor.inventory?.ItemGrid)||actionRemoteClick?.NativeLifeEnded!=true&&ReferenceEquals(actionRemoteClick?.Actor,actor)||actionPayments.Any(value=>!value.Terminal&&!value.Retired&&!value.OwnerNativeLifeEnded&&ReferenceEquals(value.Actor,actor)));}
    private static void AuditActionInvocation(ActionPayment payment,ActionLeg leg,int slot,bool returned)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")
        {string key=payment.Legs.IndexOf(leg)+":"+slot+":"+(returned?"return":"debit");payment.ScenarioNativeCalls.TryGetValue(key,out int count);payment.ScenarioNativeCalls[key]=checked(count+1);}
#endif
    }
    private static RebirthElectricalServiceRecord CopyActionCandidate(RebirthElectricalServiceRecord r)=>new RebirthElectricalServiceRecord{ClrIdx=r.ClrIdx,Position=r.Position,Kind=r.Kind,Condition=r.Condition,Workmanship=r.Workmanship,LastServiceWorldTime=r.LastServiceWorldTime,ServiceCount=r.ServiceCount,ConfiguredByStableId=r.ConfiguredByStableId,ConfiguredByBackgroundId=r.ConfiguredByBackgroundId,ConfiguredByBonusId=r.ConfiguredByBonusId,ConfiguredSkillValue=r.ConfiguredSkillValue,SourceBlock=r.SourceBlock,SourceIncarnation=r.SourceIncarnation};
    [ThreadStatic]private static ActionNativeWrite actionNativeWrite;
    [ThreadStatic]private static ActionPayment actionNpcPayment;
    [ThreadStatic]private static ActionLeg actionNpcLeg;
    [ThreadStatic]private static bool actionNpcPermitUsed;
    private sealed class ActionNativeWrite:IDisposable
    {
        internal readonly ActionPayment Payment;internal readonly ActionLeg Leg;internal readonly int Slot;
        internal readonly ItemStack Desired;internal readonly ActionFrozenDesired Frozen;internal ItemStack LeafArgument;internal ItemStack[] StationArgument;internal bool Entered,Observed,Ran,Consumed;
        internal ActionNativeWrite(ActionPayment payment,ActionLeg leg,int slot,ActionFrozenDesired desired)
        {if(actionNativeWrite!=null)throw new InvalidOperationException("Nested action setter refused");Payment=payment;Leg=leg;Slot=slot;Frozen=desired;Desired=desired.Argument;actionNativeWrite=this;}
        public void Dispose(){if(ReferenceEquals(actionNativeWrite,this))actionNativeWrite=null;}
    }
    internal static bool OwnsElectricalTargetObservation(ActionPayment payment)
    {
        lock(actionOwnerGate)return payment!=null&&actionPayments.Contains(payment)&&!payment.Terminal&&!payment.Retired&&payment.Original!=null&&payment.Events.ContainsKey("prepared")&&
            (payment.State==ActionPaymentState.Prepared||payment.State==ActionPaymentState.Paid&&payment.PublicationReturned&&payment.Legs.All(leg=>leg.AfterSaved)&&payment.OwnerAfterSaved);
    }
    private static bool SameActionItems(ItemStack[] left,ItemStack[] right)=>left!=null&&right!=null&&left.Length==right.Length&&left.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,right[index])).All(value=>value);
    private static void ActionPath(string path)
    {for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory!=null;directory=directory.Parent)if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Action payment path is a reparse point");}
    private static byte[] ActionBytes(string path,int expected)
    {
        ActionPath(path);if(expected<1||expected>24*1024*1024||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Action image bound invalid");
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(input.Length!=expected)throw new IOException("Action image length changed");var bytes=new byte[expected];int offset=0;while(offset<bytes.Length){int count=input.Read(bytes,offset,bytes.Length-offset);if(count==0)throw new EndOfStreamException();offset+=count;}if(input.ReadByte()!=-1)throw new IOException("Action image grew");return bytes;}
    }
    private static void ActionRecord(ActionPayment payment,string stage,XElement detail=null)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(1024L*1024,out var measurement))throw new InvalidDataException("Action journal measurement unavailable");
        using(measurement)
        {
            int sequence=payment.Events.TryGetValue(stage,out int existing)?existing:payment.Sequence;
            // Clone the small owned detail structure so a retained refusal node
            // never acquires a parent link to an entire historical event graph.
            var image=new XElement("actionPaymentEvent",new XAttribute("version",1),new XAttribute("sequence",sequence),new XAttribute("stage",stage),new XElement("original",payment.Original),detail==null?null:new XElement(detail));
            long length;
            using(var count=new ActionCountStream(24L*1024*1024))
            using(var writer=System.Xml.XmlWriter.Create(count,new System.Xml.XmlWriterSettings{Encoding=new UTF8Encoding(false,true),OmitXmlDeclaration=true,ConformanceLevel=System.Xml.ConformanceLevel.Fragment}))
            {image.WriteTo(writer);writer.Flush();length=count.Count;}
            // Current/prior heads are independently bounded at24MiB even if a
            // damaged file replaced a tiny old receipt. Native serialization
            // and exact head comparisons run sequentially, not cumulatively.
            long peak=checked(Math.Max(8*length,48L*1024*1024+length)+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(peak,out var scratch))throw new InvalidDataException("Action journal scratch unavailable");
            using(scratch)ActionRecordReserved(payment,stage,image);
        }
    }
    private static void ActionRecordReserved(ActionPayment payment,string stage,XElement image)
    {
        if(!payment.ActorCurrent()&&!(stage=="owner-terminal-saved"&&ActionRemoteMetadataCurrent(payment)))throw new InvalidOperationException("Action original is not current");
        bool existing=payment.Events.TryGetValue(stage,out int eventSequence);if(!existing){if(payment.Sequence>=256)throw new InvalidDataException("Action causal event bound exceeded");eventSequence=payment.Sequence;}
        if((int?)image.Attribute("sequence")!=eventSequence||(string)image.Attribute("stage")!=stage)throw new InvalidOperationException("Action event changed during measurement");
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(image.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)throw new InvalidDataException("Action record exceeds retained allocation");
        ActionPath(payment.Directory);string path=Path.Combine(payment.Directory,eventSequence.ToString("D6")+"-"+stage+".xml");
        if(File.Exists(path)){if(!ActionBytes(path,bytes.Length).SequenceEqual(bytes))throw new IOException("Action event conflicts");}
        else{using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}if(!ActionBytes(path,bytes.Length).SequenceEqual(bytes))throw new IOException("Action event readback differs");}
        // Publish the exact causal event as the current head before its caller
        // receives permission to enter a native boundary. A retained old receipt
        // may be retried, but must never move this head backwards.
        if(!existing)PublishActionHead(payment,eventSequence,bytes);
        else ValidateActionHead(payment);
        if(existing)return;payment.Events.Add(stage,eventSequence);payment.Sequence++;
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")ActionPaymentScenarioBoundary?.Invoke(payment.Operation,stage);
#endif
    }
#if REBIRTH_DEBUG
    internal static Action<Guid,string> ActionPaymentScenarioBoundary;
    internal static Func<Guid,bool> ActionPaymentScenarioLoseNativeReturn;
    internal static Action<Guid,ItemStack,ItemStack> ActionPaymentScenarioArgumentMutation;
#endif
    private static bool ActionLegCurrent(ActionPayment payment,ActionLeg leg)
    {
        if(leg.HistoricalPaid)return false; // Saved obligation descriptor: never a setter/refund capability.
        if(!payment.ActorCurrent())return false;
        if(leg.RemoteOwner)return RemoteActionOriginalCurrent(payment.RemoteOwner);
        if(leg.Local)return ReferenceEquals(leg.Grid,leg.Belt?payment.Actor.inventory.ItemGrid:payment.Actor.bag.ItemGrid);
        if(leg.Npc!=null)return ReferenceEquals(payment.World.GetEntity(leg.Npc.NativeActor.entityId),leg.Npc.NativeActor)&&leg.Npc.NativeActor.RebirthRuntimeState?.StableId==leg.Npc.NativeIdentity&&
            leg.Npc.IsLoaded&&leg.Npc.IsAuthorized(payment.Actor,false,out _);
        return leg.Native?.Current()==true&&ReferenceEquals(leg.Native.Endpoint,leg.Endpoint)&&ReferenceEquals(leg.Native.Grid,leg.Grid)&&leg.Native.IsAuthorized(payment.Actor,out _);
    }
    private static ItemStack[] ActionLive(ActionPayment payment,ActionLeg leg)=>leg.RemoteOwner?leg.Progress:leg.Local?leg.Grid.items:leg.Source.Slots;
    internal static bool ActionBlocksNative(object target)
    {
        if(target==null)return false;
        if(target is Bag bag)target=bag.ItemGrid;
        else if(target is Inventory inventory)target=inventory.ItemGrid;
        else if(target is TEFeatureStorage storage)target=storage.ItemGrid;
        // Native inventory guards also run on save/network workers. Only compare
        // retained identities here; world retirement belongs to the main pump.
        lock(actionOwnerGate)
        {
            if(ThreadManager.IsMainThread())foreach(var payment in actionPayments)
            {
                // Death is not a financial outcome or a refund permit. Stop
                // fencing native death inventory cleanup; retain every original
                // and uncertain financial obligation for its owning continuation.
                if(payment.Actor?.IsDead()==true||payment.Actor!=null&&payment.Actor.Died!=payment.Deaths){payment.OwnerNativeLifeEnded=true;foreach(var leg in payment.Legs)if(leg.Local)leg.NativeLifeEnded=true;}
                foreach(var leg in payment.Legs)if(leg.Endpoint is EntityAlive actor&&actor.IsDead())leg.NativeLifeEnded=true;
            }
            return ActionHostLoadedInventoryHeld(target)||ActionRemoteBlocksNative(target)||actionPayments.Any(payment=>!payment.Terminal&&!payment.Retired&&
                (!payment.OwnerNativeLifeEnded&&(ReferenceEquals(payment.OwnerBagGrid,target)||ReferenceEquals(payment.OwnerBeltGrid,target))||
                 payment.Legs.Any(leg=>!leg.NativeLifeEnded&&(ReferenceEquals(leg.Grid,target)||ReferenceEquals(leg.Endpoint,target)))));
        }
    }
    private static bool ActionDesired(ActionNativeWrite scope,ItemStack desired)=>(ReferenceEquals(scope.Desired,desired)||scope.Leg.Local&&scope.Leg.Belt&&desired!=null&&(ReferenceEquals(scope.Desired.itemValue,desired.itemValue)||scope.Frozen.Expected.IsEmpty()&&desired.IsEmpty()&&ReferenceEquals(desired.itemValue,ItemValue.None)))&&scope.Frozen.Matches(desired);
    internal static bool ActionAllowsGrid(ItemStackGrid grid,int slot,ItemStack desired)
    {
        if(ActionRemoteBlocksNative(grid))return AllowRemoteActionGrid(grid,slot,desired);
        if(!ActionBlocksNative(grid))return true;var scope=actionNativeWrite;
        if(scope==null||scope.Consumed||!ReferenceEquals(scope.Leg.Grid,grid)||scope.Slot!=slot||!ActionDesired(scope,desired)||!ActionLegCurrent(scope.Payment,scope.Leg))return false;
        scope.LeafArgument=desired;scope.Consumed=true;scope.Entered=true;return true;
    }
    internal static void ObserveActionGrid(ItemStackGrid grid,int slot,ItemStack desired,bool ran)
    {
        ObserveRemoteActionGrid(grid,slot,desired,ran);
        var scope=actionNativeWrite;if(scope==null||!ReferenceEquals(scope.Leg.Grid,grid)||scope.Slot!=slot||!ReferenceEquals(scope.LeafArgument,desired))return;
        if(ran)AuditActionInvocation(scope.Payment,scope.Leg,slot,scope.Leg.State==ActionLegState.ReturnAttempted);
#if REBIRTH_DEBUG
        if(ran&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")ActionPaymentScenarioArgumentMutation?.Invoke(scope.Payment.Operation,desired,grid.items[slot]);
        if(ran&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ActionPaymentScenarioLoseNativeReturn?.Invoke(scope.Payment.Operation)==true)throw new IOException("Explicit actual native return observation failure");
#endif
        scope.Observed=true;scope.Ran=ran;
    }
    internal static bool ActionAllowsStation(TileEntityWorkstation station,ItemStack[] desired)
    {
        if(!ActionBlocksNative(station))return true;var scope=actionNativeWrite;
        if(scope==null||scope.Consumed||!ReferenceEquals(scope.Leg.Endpoint,station)||!ActionLegCurrent(scope.Payment,scope.Leg)||desired==null||desired.Length!=scope.Leg.Before.Length)return false;
        var current=station.Output;for(int i=0;i<desired.Length;i++)if(i==scope.Slot?!scope.Frozen.Matches(desired[i]):!RebirthStationGridIngredients.IsSameStackSnapshot(desired[i],current[i]))return false;
        scope.StationArgument=desired;scope.Consumed=true;scope.Entered=true;return true;
    }
    internal static void ObserveActionStation(TileEntityWorkstation station,ItemStack[] desired,bool ran)
    {var scope=actionNativeWrite;if(scope==null||!ReferenceEquals(scope.Leg.Endpoint,station)||!ReferenceEquals(scope.StationArgument,desired))return;
        if(ran)AuditActionInvocation(scope.Payment,scope.Leg,scope.Slot,scope.Leg.State==ActionLegState.ReturnAttempted);
#if REBIRTH_DEBUG
        if(ran&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")ActionPaymentScenarioArgumentMutation?.Invoke(scope.Payment.Operation,desired[scope.Slot],station.Output[scope.Slot]);
        if(ran&&GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest"&&ActionPaymentScenarioLoseNativeReturn?.Invoke(scope.Payment.Operation)==true)throw new IOException("Explicit actual native return observation failure");
#endif
        scope.Observed=true;scope.Ran=ran;}
    internal static bool ActionAllowsOwnerMutation(object target,System.Reflection.MethodBase method,object[] args)
    {
        if(target is Bag remoteBag&&ActionRemoteBlocksNative(remoteBag.ItemGrid)||target is Inventory remoteBelt&&ActionRemoteBlocksNative(remoteBelt.ItemGrid))return RemoteActionOwnerArguments(target,method,args);
        if(!ActionBlocksNative(target))return true;var scope=actionNativeWrite;if(scope==null||scope.Consumed||!ActionLegCurrent(scope.Payment,scope.Leg)||args==null)return false;
        if(!scope.Frozen.Matches(scope.Desired))return false;
        if(target is Bag bag&&ReferenceEquals(bag.ItemGrid,scope.Leg.Grid)&&method.Name=="SetSlot"&&args.Length==2)return args[0] is int index&&index==scope.Slot&&ReferenceEquals(args[1],scope.Desired);
        if(target is Inventory belt&&ReferenceEquals(belt.ItemGrid,scope.Leg.Grid)&&method.Name=="SetItem"&&args.Length==3)return args[0] is int beltIndex&&beltIndex==scope.Slot&&ReferenceEquals(args[1],scope.Desired.itemValue)&&args[2] is int count&&count==scope.Frozen.Expected.count;
        return false;
    }
    internal static void ObserveActionOwnerMutation(object target,System.Reflection.MethodBase method,object[] args,bool ran)
    {
        ObserveRemoteActionOwnerArguments(target,method,args,ran);
        var scope=actionNativeWrite;if(scope==null||ran||scope.Observed||args==null||args.Length<2||args[0] is not int index||index!=scope.Slot)return;
        bool exact=target is Bag bag&&ReferenceEquals(bag.ItemGrid,scope.Leg.Grid)&&method.Name=="SetSlot"&&args.Length==2&&ReferenceEquals(args[1],scope.Desired)||
            target is Inventory belt&&ReferenceEquals(belt.ItemGrid,scope.Leg.Grid)&&method.Name=="SetItem"&&args.Length==3&&ReferenceEquals(args[1],scope.Desired.itemValue)&&args[2] is int count&&count==scope.Desired.count;
        if(exact){scope.Observed=true;scope.Ran=false;}
    }
    internal static bool ActionBlocksNpc(RebirthNpcStableId id)
    {lock(actionOwnerGate)return actionPayments.Any(payment=>!payment.Terminal&&!payment.Retired&&payment.Legs.Any(leg=>leg.Npc?.NativeIdentity==id));}
    internal static bool ActionAllowsNpc(RebirthNpcInventoryTransaction transaction)
    {
        var payment=actionNpcPayment;var leg=actionNpcLeg;
        if(transaction==null||payment==null||leg?.Npc==null||actionNpcPermitUsed||!ActionLegCurrent(payment,leg)||transaction.NpcId!=leg.Npc.NativeIdentity)return false;
        Guid expected=leg.State==ActionLegState.DebitAttempted?leg.DebitMutation:leg.State==ActionLegState.ReturnAttempted?leg.ReturnMutation:Guid.Empty;
        if(expected==Guid.Empty||transaction.TransactionId!=expected)return false;actionNpcPermitUsed=true;return true;
    }
    internal static bool ActionAllowsDog(RebirthNpcStableId id,uint revision,ItemStack[] slots,PackedBoolArray locks)
    {
        var leg=actionNpcLeg;if(actionNpcPayment==null||leg?.Npc==null||!leg.Npc.IsDogStorage||actionNpcPermitUsed||leg.Npc.NativeIdentity!=id||!ActionLegCurrent(actionNpcPayment,leg))return false;
        var expected=leg.State==ActionLegState.DebitAttempted?leg.After:leg.State==ActionLegState.ReturnAttempted?leg.Before:null;
        if(expected==null||revision!=(leg.State==ActionLegState.DebitAttempted?(uint)leg.Revision:(uint)leg.Revision+1)||!SameActionItems(slots,expected)||locks==null||locks.Length!=leg.Locks.Length)return false;
        for(int i=0;i<locks.Length;i++)if(locks[i]!=leg.Locks[i])return false;actionNpcPermitUsed=true;return true;
    }
}
