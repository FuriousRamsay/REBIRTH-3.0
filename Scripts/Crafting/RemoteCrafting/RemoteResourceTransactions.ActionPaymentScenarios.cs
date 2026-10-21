#if REBIRTH_DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Scripting;

public static partial class RemoteResourceTransactions
{
    private static readonly Dictionary<Guid,ActionPayment> actionScenarioOriginals=new Dictionary<Guid,ActionPayment>();
    private static void RequireActionScenario()
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||GameManager.Instance?.World?.IsRemote()!=false)
            throw new InvalidOperationException("Actual disposable CodexTest authority main thread required");
    }
    internal static Guid BeginActionScenario(Vector3i position,string itemKey,int count,string faultStage)
    {
        RequireActionScenario();if(count<1||actionScenarioOriginals.Count>=8)throw new InvalidOperationException("Scenario bound exceeded");
        var world=GameManager.Instance.World;var actor=world.GetPrimaryPlayer();
        if(actor==null||world.GetTileEntity(position) is not TileEntityPowered tile||!RebirthInfrastructureWorkService.TryGetRecord(position,out var known)||!RebirthElectricalSourceIdentity.Matches(world,known))
            throw new InvalidOperationException("Prepare an actual placed, previously serviced powered target before this fixture");
        var candidate=CopyActionCandidate(known);candidate.Condition=100;candidate.ServiceCount=checked(candidate.ServiceCount+1);candidate.LastServiceWorldTime=world.worldTime;
        var item=ItemClass.GetItem(itemKey,false);if(item==null||item.IsEmpty())throw new ArgumentException("Unknown real item");
        if(!TryBeginElectricalPayment(actor,tile,candidate,new[]{new ItemStack(item,count)},out var payment,out var failure))throw new InvalidOperationException(failure);
        return ArmActualActionScenario(payment,faultStage);
    }
    internal static Guid BeginOwnedServiceScenario(Vector3i position,string faultStage)
    {
        RequireActionScenario();if(actionScenarioOriginals.Count>=8)throw new InvalidOperationException("Scenario bound exceeded");
        var actor=GameManager.Instance.World.GetPrimaryPlayer();
        if(!RebirthInfrastructureWorkService.BeginOwnedService(actor,0,position,out var payment,out var failure))throw new InvalidOperationException(failure);
        return ArmActualActionScenario(payment,faultStage);
    }
    private static Guid ArmActualActionScenario(ActionPayment payment,string faultStage)
    {
        if(actionScenarioOriginals.ContainsKey(payment.Operation))return payment.Operation;
        payment.ScenarioRetain=true;actionScenarioOriginals.Add(payment.Operation,payment);
        if(!string.IsNullOrEmpty(faultStage))
        {
            bool fired=false;
            if(faultStage=="candidate-count"||faultStage=="candidate-metadata")
            {
                payment.ScenarioArgumentMutation=faultStage;
                ActionPaymentScenarioArgumentMutation=(operation,candidate,live)=>
                {
                    if(operation!=payment.Operation||fired)return;fired=true;
                    if(faultStage=="candidate-count"){int changed=checked(candidate.count+1);candidate.count=changed;live.count=changed;}
                    else{if(candidate.IsEmpty()||live.IsEmpty())throw new InvalidOperationException("Metadata alias fixture requires a source retaining a nonempty native stack");string key="rbMP040Alias_"+operation.ToString("N");candidate.itemValue.SetMetadata(key,"candidate-mutated");live.itemValue.SetMetadata(key,"candidate-mutated");}
                    payment.ScenarioMutationObserved=true;
                };
            }
            else if(faultStage=="head-staged"||faultStage=="head-published")ActionPaymentScenarioHeadBoundary=(operation,boundary)=>
            {if(operation==payment.Operation&&boundary==faultStage&&!fired){fired=true;throw new IOException("Explicit actual current-head interruption "+boundary);}};
            else if(faultStage=="native-unobserved")ActionPaymentScenarioLoseNativeReturn=operation=>{if(operation!=payment.Operation||fired)return false;fired=true;return true;};
            else ActionPaymentScenarioBoundary=(operation,stage)=>
            {
                bool match=stage==faultStage||faultStage=="never-entered"&&stage.EndsWith("-debit-intent",StringComparison.Ordinal)||
                    faultStage=="returned-receipt-loss"&&stage.EndsWith("-debited",StringComparison.Ordinal)||faultStage=="pause-prepared"&&stage=="prepared";
                if(operation==payment.Operation&&match&&!fired){fired=true;if(faultStage=="pause-prepared")payment.ScenarioPaused=true;throw new IOException("Explicit actual action journal boundary failure "+stage);}
            };
        }
        AdvanceElectricalPayment(payment);return payment.Operation;
    }
    internal static void VerifyActionScenario(Guid operation,bool returned)
    {
        RequireActionScenario();if(!actionScenarioOriginals.TryGetValue(operation,out var payment))throw new InvalidOperationException("Actual original not armed in this process");
        if(!payment.Terminal||payment.State!=(returned?ActionPaymentState.Returned:ActionPaymentState.Used))throw new InvalidOperationException("Actual owning outcome has not completed: "+payment.State);
        if(!payment.Events.ContainsKey(returned?"returned":"used")||!Directory.Exists(payment.Directory))throw new InvalidOperationException("Full durable original/outcome missing");
        ValidateActionHead(payment);
        var original=System.Xml.Linq.XElement.Parse(payment.Original);
        var planned=original.Element("sources")?.Elements("source").ToArray();
        if(planned==null||planned.Length!=payment.Legs.Count)throw new InvalidOperationException("Full source allocation original missing");
        for(int participant=0;participant<planned.Length;participant++)
        {
            var takes=planned[participant].Element("takes")?.Elements("take").ToArray();var leg=payment.Legs[participant];
            if(takes==null||takes.Length!=leg.Slots.Length||takes.Where((take,index)=>(int)take.Attribute("slot")!=leg.Slots[index]||(int)take.Attribute("count")!=leg.Paid[index].count).Any())
                throw new InvalidOperationException("Immutable original lost exact slot allocation");
        }
        foreach(var leg in payment.Legs)
        {
            int legIndex=payment.Legs.IndexOf(leg);
            if(leg.Npc!=null)
            {
                if(leg.Commit?.Disposition==RemoteNpcResourceSource.CommitDisposition.Published&&(!payment.ScenarioNativeCalls.TryGetValue(legIndex+":-1:debit",out int debitCalls)||debitCalls!=1))throw new InvalidOperationException("NPC debit invocation repeated");
                if(leg.ReturnCommit?.Disposition==RemoteNpcResourceSource.CommitDisposition.Published&&(!payment.ScenarioNativeCalls.TryGetValue(legIndex+":-1:return",out int returnCalls)||returnCalls!=1))throw new InvalidOperationException("NPC refund invocation repeated");
            }
            else for(int take=0;take<leg.Slots.Length;take++)
            {
                string prefix=legIndex+":"+leg.Slots[take]+":";
                if(leg.DebitedSlots[take]&&(!payment.ScenarioNativeCalls.TryGetValue(prefix+"debit",out int debitCalls)||debitCalls!=1))throw new InvalidOperationException("Actual native debit repeated");
                if(leg.ReturnedSlots[take]&&(!payment.ScenarioNativeCalls.TryGetValue(prefix+"return",out int returnCalls)||returnCalls!=1))throw new InvalidOperationException("Actual native refund repeated");
            }
            if(leg.State==ActionLegState.Unknown||leg.State==ActionLegState.DebitAttempted||leg.State==ActionLegState.ReturnAttempted)throw new InvalidOperationException("Unknown effect falsely terminalized");
            if(returned)
            {
                if(!leg.Local&&leg.State==ActionLegState.Returned&&!leg.ReturnSaved)throw new InvalidOperationException("Actual saved source refund missing");
                if(leg.Npc!=null){if(!ActionNpcImage(leg,false,leg.State==ActionLegState.Returned))throw new InvalidOperationException("Canonical NPC return mismatch");}
                else if(!SameActionItems(ActionLive(payment,leg),leg.Before))throw new InvalidOperationException("Full original source was not restored");
            }
            else
            {
                if(!leg.AfterSaved||leg.State!=ActionLegState.Debited)throw new InvalidOperationException("Unpaid source accepted");
                if(leg.Npc!=null){if(!ActionNpcImage(leg,true,false))throw new InvalidOperationException("Canonical NPC debit mismatch");}
                else if(!SameActionItems(ActionLive(payment,leg),leg.After))throw new InvalidOperationException("Native debit differs in full metadata or unrelated slots");
            }
        }
        int sequence=payment.Sequence;
        if(AdvanceElectricalPayment(payment)||AdvanceElectricalReturn(payment)||CompleteElectricalPayment(payment)||payment.Sequence!=sequence)throw new InvalidOperationException("Terminal original replayed");
        if(!returned&&(!payment.TargetBeforeSaved||!payment.TargetAfterSaved||!payment.OwnerAfterSaved||!RebirthInfrastructureWorkService.ValidatePaidActionArtifact(payment,Path.Combine(payment.Directory,"service-after.native"))))
            throw new InvalidOperationException("Coupled full native/service/owner saved outcome missing");
        if(!returned&&(!payment.Events.ContainsKey(payment.TrainingRequired?"training-saved":"training-not-required")||payment.TrainingRequired&&!HasActionTrainingReceipt(payment)))
            throw new InvalidOperationException("Original electrical training receipt was not saved");
        ActionPaymentScenarioBoundary=null;ActionPaymentScenarioHeadBoundary=null;ActionPaymentScenarioArgumentMutation=null;ActionPaymentScenarioLoseNativeReturn=null;actionScenarioOriginals.Remove(operation);payment.ScenarioRetain=false;PumpActionPayments();
    }
    internal static void VerifyActionUnknownScenario(Guid operation)
    {
        RequireActionScenario();if(!actionScenarioOriginals.TryGetValue(operation,out var payment)||payment.Terminal||!payment.Legs.Any(leg=>leg.State==ActionLegState.Unknown))throw new InvalidOperationException("Actual uncertain native boundary required");
        if(payment.ScenarioArgumentMutation!=null)
        {
            if(!payment.ScenarioMutationObserved)throw new InvalidOperationException("Actual native candidate/listener mutation did not execute");
            var changed=payment.Legs.First(leg=>leg.State==ActionLegState.Unknown);
            if(changed.AfterSaved||changed.RefusalStage!=null||payment.Events.ContainsKey("paid"))throw new InvalidOperationException("Mutated native argument authorized a financial receipt or no-effect classification");
            if(payment.ScenarioArgumentMutation=="candidate-metadata")
            {string key="rbMP040Alias_"+operation.ToString("N");if(changed.Before.Concat(changed.After).Any(stack=>stack.itemValue.HasMetadata(key)))throw new InvalidOperationException("Mutable native candidate contaminated immutable plan metadata");}
        }
        int sequence=payment.Sequence;var calls=new Dictionary<string,int>(payment.ScenarioNativeCalls);if(calls.Count==0)throw new InvalidOperationException("No real native setter boundary was exercised");
        var images=payment.Legs.Where(leg=>leg.Npc==null).Select(leg=>ItemStack.Clone(ActionLive(payment,leg))).ToArray();
        for(int i=0;i<3;i++)PumpActionPayments();
        if(payment.Terminal||payment.PublicationEntered||payment.Sequence!=sequence)throw new InvalidOperationException("Unknown effect was published or retried");
        if(calls.Count!=payment.ScenarioNativeCalls.Count||calls.Any(pair=>!payment.ScenarioNativeCalls.TryGetValue(pair.Key,out int value)||value!=pair.Value))throw new InvalidOperationException("Unknown native invocation replayed");
        int index=0;foreach(var leg in payment.Legs.Where(leg=>leg.Npc==null))if(!SameActionItems(ActionLive(payment,leg),images[index++]))throw new InvalidOperationException("Unknown source changed during refused retry");
        ActionPaymentScenarioLoseNativeReturn=null;ActionPaymentScenarioArgumentMutation=null;ActionPaymentScenarioBoundary=null;
    }
    internal static void ExerciseActionHeadConflict(Guid operation)
    {
        RequireActionScenario();
        if(!actionScenarioOriginals.TryGetValue(operation,out var payment)||!payment.ScenarioPaused||payment.State!=ActionPaymentState.Prepared||payment.Sequence!=1)
            throw new InvalidOperationException("Actual pause-prepared boundary required before any native payment");
        string path=Path.Combine(payment.Directory,"current.xml");byte[] original=ActionHeadBytes(path);
        var before=payment.Legs.Select(leg=>ItemStack.Clone(ActionLive(payment,leg))).ToArray();
        int calls=payment.ScenarioNativeCalls.Values.Sum();
        try
        {
            using(var output=new FileStream(path,FileMode.Truncate,FileAccess.Write,FileShare.None)){output.WriteByte(0);output.Flush(true);}
            if(AdvanceElectricalPayment(payment)||payment.State!=ActionPaymentState.Prepared||payment.Sequence!=1||payment.PublicationEntered||payment.Terminal||payment.ScenarioNativeCalls.Values.Sum()!=calls)
                throw new InvalidOperationException("Conflicting current head permitted native payment or service");
            for(int index=0;index<payment.Legs.Count;index++)if(!SameActionItems(before[index],ActionLive(payment,payment.Legs[index])))
                throw new InvalidOperationException("Conflicting head changed a full source image");
        }
        finally
        {
            // Fixture-only restoration of the exact historical bytes. No
            // production recovery path promotes a conflicting file this way.
            using(var output=new FileStream(path,FileMode.Truncate,FileAccess.Write,FileShare.None)){output.Write(original,0,original.Length);output.Flush(true);}
        }
        ValidateActionHead(payment);
    }
    internal static void ExerciseActualActionEscape(Guid operation,int sourceIndex)
    {
        RequireActionScenario();if(!actionScenarioOriginals.TryGetValue(operation,out var payment)||payment.Terminal||payment.Retired||payment.Original==null||!payment.ActorCurrent())throw new InvalidOperationException("Await the actual live original preparation before escape qualification");
        object endpoint;ItemStack[] before=null;ActionLeg leg=null;
        if(sourceIndex==-1)endpoint=payment.ServiceTarget;
        else
        {
            if(sourceIndex<0||sourceIndex>=payment.Legs.Count)throw new ArgumentOutOfRangeException();leg=payment.Legs[sourceIndex];endpoint=leg.Endpoint;
            if(leg.Local||leg.Npc!=null||!ActionLegCurrent(payment,leg))throw new InvalidOperationException("Choose an actual native station/container/vehicle/drone source");
            before=ItemStack.Clone(ActionLive(payment,leg));
        }
        if(endpoint is EntityVehicle vehicle)
        {vehicle.OnCollectServer(payment.Actor.entityId);if(!ReferenceEquals(payment.World.GetEntity(vehicle.entityId),vehicle))throw new InvalidOperationException("Held actual vehicle was collected");}
        else if(endpoint is EntityDrone drone)
        {drone.OnCollectServer(payment.Actor.entityId);if(!ReferenceEquals(payment.World.GetEntity(drone.entityId),drone))throw new InvalidOperationException("Held actual drone was collected");}
        else if(endpoint is TileEntity tile)
        {
            var position=tile.ToWorldPos();var block=payment.World.GetBlock(position);
            GameManager.Instance.PickupBlockServer(position,block,payment.Actor.entityId);
            if(!ReferenceEquals(payment.World.GetTileEntity(position),tile)||!payment.World.GetBlock(position).Equals(block))throw new InvalidOperationException("Held actual native tile was picked up");
        }
        else throw new InvalidOperationException("No actual extraction boundary for this source kind");
        if(leg!=null&&!SameActionItems(ActionLive(payment,leg),before))throw new InvalidOperationException("Pickup refusal changed full original input");
        string prefix=sourceIndex==-1?"service-target|":"source-"+sourceIndex+"|";
        if(!payment.RefusedBoundaries.Any(pair=>pair.Key.StartsWith(prefix,StringComparison.Ordinal)&&pair.Value)||!Directory.Exists(Path.Combine(payment.Directory,"NativeRefusals")))throw new InvalidOperationException("Actual native extraction refusal lacks full durable original event");
    }
    internal static void ReleaseActionScenarioPause(Guid operation)
    {
        RequireActionScenario();if(!actionScenarioOriginals.TryGetValue(operation,out var original)||!original.ScenarioPaused)throw new InvalidOperationException("Actual paused original required");
        original.ScenarioPaused=false;PumpActionPayments();
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthActionPaymentScenario:ConsoleCmdAbstract
{
    public override string[] getCommands()=>new[]{"rbactionpaymentscenario"};
    public override string getDescription()=>"CodexTest actual owning resource payment fixture (NOT_RUN).";
    public override string getHelp()=>"rbactionpaymentscenario watch <x> <y> <z>\nrbactionpaymentscenario unwatch\nrbactionpaymentscenario service <x> <y> <z> [actualJournalStage|native-unobserved]\nrbactionpaymentscenario begin <x> <y> <z> <itemKey> <count> [actualJournalStage|native-unobserved]\nrbactionpaymentscenario escape <operationN> <sourceIndex|-1 target>\nrbactionpaymentscenario verify|returned|unknown <operationN>\nrbactionpaymentscenario remote-envelope\nrbactionpaymentscenario remote-start[-fault] <x> <y> <z> [initial-saved|attempt-saved|invocation-saved]\nrbactionpaymentscenario remote-checkpoint-arm <operationN> <boundary>\nrbactionpaymentscenario remote-checkpoint-verify|remote-checkpoint-release <operationN>\nrbactionpaymentscenario remote-admission-arm <operationN> <ready|invoking>\nrbactionpaymentscenario remote-admission-checkpoint|remote-admission-release|remote-noeffect-restart|remote-completed-restart|local-completed-restart <operationN>";
    public override void Execute(List<string> args,CommandSenderInfo sender)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest main thread required");
        if(args!=null&&args.Count==1&&args[0]=="unwatch")
        {RemoteResourceTransactions.StopActionNativeInputWatch();Log.Out("[MP040 WATCH] stopped");return;}
        if(args!=null&&args.Count>=4&&args.Count<=6&&args[0]=="watch")
        {RemoteResourceTransactions.WatchActionNativeInput(new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3])),args.Count>=5?int.Parse(args[4]):(int?)null,args.Count==6?args[5]:null);Log.Out("[MP040 WATCH] armed");return;}
        if(args!=null&&args.Count==1&&args[0]=="remote-envelope")
        {RemoteResourceTransactions.VerifyActionRemoteEnvelopeScenario();Log.Out("[MP040 PASS] remote-envelope");return;}
        if(args!=null&&args.Count==1&&(args[0]=="remote-restart-hold"||args[0]=="remote-restart-hold-off"))
        {RemoteResourceTransactions.SetActionNativeRestartScenarioHold(args[0]=="remote-restart-hold");Log.Out("[MP040 ARM] "+args[0]);return;}
        if(args==null||args.Count<2)throw new ArgumentException(getHelp());
        if(args[0]=="remote-start"&&args.Count==4)
        {var operation=RemoteResourceTransactions.BeginActionRemoteScenario(new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3])));Log.Out("[MP040 REMOTE ARM] "+operation.ToString("N"));return;}
        if(args[0]=="remote-start-fault"&&args.Count==5)
        {var operation=RemoteResourceTransactions.BeginActionRemoteScenario(new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3])),args[4]);Log.Out("[MP040 REMOTE ARM] "+operation.ToString("N"));return;}
        if(args[0]=="remote-checkpoint-arm"&&args.Count==3)
        {RemoteResourceTransactions.ArmActionClientCheckpointScenario(Guid.ParseExact(args[1],"N"),args[2]);Log.Out("[MP040 ARM] remote-checkpoint");return;}
        if(args[0]=="remote-admission-arm"&&args.Count==3)
        {RemoteResourceTransactions.ArmActionAdmissionCheckpointScenario(Guid.ParseExact(args[1],"N"),args[2]);Log.Out("[MP040 ARM] remote-admission-checkpoint");return;}
        if(args[0]=="escape"&&args.Count==3){RemoteResourceTransactions.ExerciseActualActionEscape(Guid.ParseExact(args[1],"N"),int.Parse(args[2]));Log.Out("[MP040 PASS] escape");return;}
        if(args[0]=="service"&&(args.Count==4||args.Count==5))
        {var operation=RemoteResourceTransactions.BeginOwnedServiceScenario(new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3])),args.Count==5?args[4]:null);Log.Out("[MP040 ARM] "+operation.ToString("N"));return;}
        if(args[0]=="begin"&&(args.Count==6||args.Count==7))
        {var operation=RemoteResourceTransactions.BeginActionScenario(new Vector3i(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3])),args[4],int.Parse(args[5]),args.Count==7?args[6]:null);Log.Out("[MP040 ARM] "+operation.ToString("N"));return;}
        if(args[0]=="terminal-released"&&(args.Count==2||args.Count==3))
        {bool released=RemoteResourceTransactions.ObserveActionTerminalReleaseScenario(Guid.ParseExact(args[1],"N"),args.Count==3?int.Parse(args[2]):(int?)null);Log.Out(released?"[MP040 PASS] terminal-released":"[MP040 WAIT] terminal-released");return;}
        if(args.Count!=2)throw new ArgumentException(getHelp());
        var id=Guid.ParseExact(args[1],"N");
        if(args[0]=="remote-restart-verify"||args[0]=="remote-restart-release")
        {RemoteResourceTransactions.VerifyActionNativeRestartScenario(id,args[0]=="remote-restart-release");Log.Out("[MP040 PASS] "+args[0]);return;}
        if(args[0]=="remote-noeffect-restart")
        {RemoteResourceTransactions.VerifyActionNoEffectNativeRestartScenario(id);Log.Out("[MP040 PASS] remote-noeffect-restart");return;}
        if(args[0]=="remote-completed-restart")
        {RemoteResourceTransactions.VerifyActionCompletedNativeRestartScenario(id);Log.Out("[MP040 PASS] remote-completed-restart");return;}
        if(args[0]=="local-completed-restart")
        {RemoteResourceTransactions.VerifyActionCompletedLocalNativeRestartScenario(id);Log.Out("[MP040 PASS] local-completed-restart");return;}
        if(args[0]=="ownerless-completed-restart")
        {RemoteResourceTransactions.VerifyActionOwnerlessCompletedNativeRestartScenario(id);Log.Out("[MP040 PASS] ownerless-completed-restart");return;}
        if(args[0]=="remote-admission-checkpoint"||args[0]=="remote-admission-release")
        {RemoteResourceTransactions.VerifyActionAdmissionCheckpointScenario(id,args[0]=="remote-admission-release");Log.Out("[MP040 PASS] "+args[0]);return;}
        if(args[0]=="remote-checkpoint-verify"){RemoteResourceTransactions.VerifyActionClientCheckpointScenario(id);Log.Out("[MP040 PASS] remote-checkpoint");return;}
        if(args[0]=="remote-checkpoint-release"){RemoteResourceTransactions.ReleaseActionClientCheckpointScenario(id);Log.Out("[MP040 RELEASE] remote-checkpoint");return;}
        if(args[0]=="remote-refused"){RemoteResourceTransactions.VerifyActionRemoteNoAdmissionScenario(id);Log.Out("[MP040 PASS] remote-refused");return;}
        if(args[0]=="remote-admission"){RemoteResourceTransactions.VerifyActionRemoteAdmissionScenario(id);Log.Out("[MP040 PASS] remote-admission");return;}
        if(args[0]=="remote-courier"){RemoteResourceTransactions.VerifyActionRemoteCourierScenario(id);Log.Out("[MP040 PASS] remote-courier");return;}
        if(args[0]=="head-conflict"){RemoteResourceTransactions.ExerciseActionHeadConflict(id);Log.Out("[MP040 PASS] head-conflict");return;}
        if(args[0]=="release"){RemoteResourceTransactions.ReleaseActionScenarioPause(id);Log.Out("[MP040 RELEASE] "+id.ToString("N"));return;}
        if(args[0]=="unknown")RemoteResourceTransactions.VerifyActionUnknownScenario(id);
        else if(args[0]=="verify"||args[0]=="returned")RemoteResourceTransactions.VerifyActionScenario(id,args[0]=="returned");
        else throw new ArgumentException(getHelp());
        Log.Out("[MP040 PASS] "+args[0]);
    }
}
#endif

