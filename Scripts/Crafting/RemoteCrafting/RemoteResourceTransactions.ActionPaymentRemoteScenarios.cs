#if REBIRTH_DEBUG
using System;
using System.IO;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    internal static void SetActionNativeRestartScenarioHold(bool held)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||GameManager.Instance?.World?.GetPrimaryPlayer() is not EntityPlayerLocal actor||
            actor.world==null||actor.IsDead()||held&&(actionRemoteClick!=null||IsActionPaymentHeld(actor)))
            throw new InvalidOperationException("Actual disposable remote local player before new payment required");
        if(held)actor.Buffs.SetCustomVar("rbActionRestartFixtureHold",1f,true);else actor.Buffs.RemoveCustomVar("rbActionRestartFixtureHold");
        GameManager.Instance.SaveLocalPlayerData();
        if(actor.world.IsRemote())SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(actor),false);
    }
    internal static void VerifyActionNativeRestartScenario(Guid operation,bool release)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||!actionScenarioOriginals.TryGetValue(operation,out var payment)||
            !payment.ScenarioPaused||payment.RestartTargetSource is not ActionTargetNativeOriginal target||!target.Current()||!target.Installed||!ReferenceEquals(target.Payment,payment)||
            (payment.RemoteOwner?.RestartNativeSource??SnapshotActionHostOwnerLoads().SingleOrDefault(value=>ReferenceEquals(value.Actor,payment.Actor))) is not ActionHostOwnerLoad owner||!ActionHostLoadedCurrent(owner)||!ReferenceEquals(payment.Actor,owner.Actor)||
            !payment.OwnerBeforeSaved||!payment.TargetBeforeSaved||payment.PublicationEntered!=payment.Events.ContainsKey("publication-returned")||payment.PublicationReturned!=payment.PublicationEntered||payment.Terminal||payment.ScenarioNativeCalls.Count!=0)
            throw new InvalidOperationException("Await actual independently loaded owner+target original before continuation");
        if(target.BeforeFirstGrant)
        {
            if(payment.State!=ActionPaymentState.Prepared||payment.Sequence<3||payment.Events.Count!=payment.Sequence||payment.RemoteDebitEntered||payment.OwnerAfterSaved||
                payment.Legs.Any(leg=>leg.State!=ActionLegState.Before||leg.AfterSaved||leg.AttemptedSlots.Any(value=>value)||leg.DebitedSlots.Any(value=>value)||!SameActionItems(leg.Progress,leg.Before)))
                throw new InvalidOperationException("Cold positive pre-entry original acquired attempted/debited authority");
        }
        else if(payment.State!=ActionPaymentState.Paid||!payment.OwnerAfterSaved||!payment.RemoteDebitEntered||payment.Legs.Any(leg=>leg.State!=ActionLegState.Debited||!leg.AfterSaved))
            throw new InvalidOperationException("Cold saved-paid original lost full positive source receipts");
        foreach(var leg in payment.Legs.Where(value=>value.HistoricalPaid))
            if(target.BeforeFirstGrant||leg.Source!=null||leg.Endpoint!=null||leg.Grid!=null||leg.Native!=null||leg.Npc!=null||ActionLegCurrent(payment,leg)||
                !leg.BeforeSaved||!leg.AfterSaved||leg.State!=ActionLegState.Debited||!SameActionItems(leg.Progress,leg.After))
                throw new InvalidOperationException("Saved mixed obligation acquired source setter/refund authority");
        if(payment.PublicationReturned&&(!payment.ReturnedPublicationNeedsRestore||payment.ServiceBefore==null||!payment.Events.ContainsKey("publication-intent")))
            throw new InvalidOperationException("Returned original lost exact canonical Before/result completion scope");
        ValidateActionHead(payment);
        if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(owner.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)||
            (payment.OwnerBagBefore==null?present:!present||phase!=(target.BeforeFirstGrant?1f:2f)))
            throw new InvalidOperationException("Actual loaded native phase differs from selected full original");
        int sequence=payment.Sequence;PumpActionPayments();PumpActionPayments();
        if(payment.Sequence!=sequence||payment.ScenarioNativeCalls.Count!=0||!payment.ScenarioPaused)throw new InvalidOperationException("Paused native recovery crossed a boundary");
        if(release)ReleaseActionScenarioPause(operation);
    }
    internal static void VerifyActionNoEffectNativeRestartScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
            throw new InvalidOperationException("Disposable CodexTest main thread required");
        PumpActionNeverAdmittedTerminalRestart();
        var source=SnapshotActionHostOwnerLoads().SingleOrDefault(value=>ActionHostLoadedCurrent(value)&&value.TerminalMetadataDone.Contains(operation));
        if(source==null||!source.Loaded||!source.Hydrated||!source.Joined||source.Spawn?.Returned!=true||
            !File.Exists(Path.Combine(source.Spawn.Save,"RebirthData","ActionOwnerOriginals",operation.ToString("N"),"never-admitted.xml"))||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(source.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)||!present||phase!=6f||
            !actionTerminalCouriers.TryGetValue(operation,out var courier)||!courier.Current()||actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation))
            throw new InvalidOperationException("Await actual native persisted owner reconstruction and no-effect terminal courier");
        var body=courier.Body;var bag=RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items);var belt=RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items);
        for(int attempt=0;attempt<3;attempt++){source.TerminalScanNext=0;PumpActionNeverAdmittedTerminalRestart();}
        if(!actionTerminalCouriers.TryGetValue(operation,out var same)||!ReferenceEquals(same,courier)||!ReferenceEquals(same.Body,body)||
            !bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items))||!belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items))||
            actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation))
            throw new InvalidOperationException("Cold metadata retry recreated financial work or changed current complete grids");
        courier.Next=0;PumpActionTerminalCouriers();
    }
    internal static void VerifyActionCompletedNativeRestartScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest main thread required");
        var source=SnapshotActionHostOwnerLoads().SingleOrDefault(value=>value.Spawn?.Local!=true&&ActionHostLoadedCurrent(value)&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(value.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)&&present&&(phase==2f||phase==3f||phase==6f||phase==8f));
        if(source==null||!source.Loaded||!source.Hydrated||!source.Joined||source.Spawn?.Returned!=true)throw new InvalidOperationException("Actual independently native loaded financial/terminal owner phase required");
        var bag=RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items);var belt=RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items);
        string head=Path.Combine(source.Spawn.Save,"RebirthData","ActionPayments",operation.ToString("N"),"current.xml");byte[] originalHead=ActionHeadBytes(head);
        if(!actionTerminalCouriers.ContainsKey(operation)&&!TryRestoreCompletedActionTerminalCourier(source,operation))throw new InvalidOperationException("Saved used/returned complete original did not reconstruct metadata courier");
        if(!source.TerminalMetadataDone.Contains(operation)||!actionTerminalCouriers.TryGetValue(operation,out var courier)||!courier.Current()||
            actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation))throw new InvalidOperationException("Completed cold original reconstructed a financial graph");
        var body=courier.Body;
        if(courier.Phase!=5&&courier.Phase!=6&&courier.Phase!=7)throw new InvalidOperationException("Completed courier acquired a financial grant phase");
        for(int attempt=0;attempt<3;attempt++)TryRestoreCompletedActionTerminalCourier(source,operation);
        if(!actionTerminalCouriers.TryGetValue(operation,out var same)||!ReferenceEquals(same,courier)||!ReferenceEquals(same.Body,body)||!ActionEnvelopeFileEquals(head,originalHead)||
            !bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items))||!belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items))||
            actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation))throw new InvalidOperationException("Completed cold metadata replay changed native inputs/original or acquired financial authority");
    }
    internal static void VerifyActionOwnerlessCompletedNativeRestartScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable native owner required");
        var source=SnapshotActionHostOwnerLoads().SingleOrDefault(value=>ActionHostLoadedCurrent(value)&&
            (value.TerminalMetadataDone.Contains(operation)||PendingActionOwnerOperations(value.Spawn).Contains(operation)));
        if(source==null)throw new InvalidOperationException("Actual owner native load and exact pending original required");
        string path=Path.Combine(source.Spawn.Save,"RebirthData","ActionPayments",operation.ToString("N"),"current.xml");byte[] head=ActionHeadBytes(path);
        var original=ReadRemoteActionOriginal(ReadActionBoundedXml(head,24*1024*1024).Element("original")?.Value);
        if(original.Element("ownerOriginal")!=null)throw new InvalidOperationException("Ownerless assertion requires no owner inventory contribution");
        byte[] bag=RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items),belt=RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items);
        if(!source.TerminalMetadataDone.Contains(operation)&&!TryRestoreCompletedActionTerminalCourier(source,operation))throw new InvalidOperationException("Ownerless completed metadata did not retire exact pending description");
        for(int n=0;n<3;n++)TryRestoreCompletedActionTerminalCourier(source,operation);
        if(!source.TerminalMetadataDone.Contains(operation)||File.Exists(ActionPendingTargetPath(source.Spawn.Save,operation))||actionTerminalCouriers.ContainsKey(operation)||
            actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation)||!ActionEnvelopeFileEquals(path,head)||
            !bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items))||!belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items)))
            throw new InvalidOperationException("Ownerless terminal metadata gained financial/owner setter authority");
    }
    internal static void VerifyActionCompletedLocalNativeRestartScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")throw new InvalidOperationException("Disposable CodexTest main thread required");
        var source=SnapshotActionHostOwnerLoads().SingleOrDefault(value=>value.Spawn?.Local==true&&ActionHostLoadedCurrent(value)&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(value.Buffs,ActionOwnerMarker(operation),out float phase,out bool present)&&present&&(phase==2f||phase==3f));
        if(source==null)throw new InvalidOperationException("Actual local native loaded completed owner marker required");
        byte[] bag=RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items),belt=RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items);
        string head=Path.Combine(source.Spawn.Save,"RebirthData","ActionPayments",operation.ToString("N"),"current.xml");byte[] original=ActionHeadBytes(head);
        if(!source.TerminalMetadataDone.Contains(operation)&&!TryRestoreCompletedActionTerminalCourier(source,operation))throw new InvalidOperationException("Actual completed local original did not finish saved metadata");
        for(int attempt=0;attempt<3;attempt++)TryRestoreCompletedActionTerminalCourier(source,operation);
        if(!source.TerminalMetadataDone.Contains(operation)||actionTerminalCouriers.ContainsKey(operation)||actionPayments.Any(value=>value.Operation==operation)||actionRemoteHostOriginals.ContainsKey(operation)||
            !ActionEnvelopeFileEquals(head,original)||!bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items))||
            !belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items))||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||!RebirthGearNativePlayerFile.TryRead(identity,out var saved)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,ActionOwnerMarker(operation),out _,out bool present)||present)
            throw new InvalidOperationException("Local terminal cleanup changed inventory/original or acquired financial authority");
    }
    private static Guid actionAdmissionScenario;private static string actionAdmissionScenarioStage;private static bool actionAdmissionScenarioHit;
    internal static void ArmActionAdmissionCheckpointScenario(Guid operation,string stage)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||operation==Guid.Empty||stage!="ready"&&stage!="invoking")
            throw new InvalidOperationException("Disposable CodexTest and actual future admission boundary required");
        actionAdmissionScenario=operation;actionAdmissionScenarioStage=stage;actionAdmissionScenarioHit=false;
        actionAdmissionBoundary=(id,observed)=>
        {
            if(id!=operation||observed!=stage)return;
            var source=actionRemoteHostOriginals[id];source.AdmissionScenarioPaused=true;actionAdmissionScenarioHit=true;actionAdmissionBoundary=null;
            throw new IOException("CodexTest interrupted actual admission checkpoint at "+stage);
        };
    }
    internal static void VerifyActionAdmissionCheckpointScenario(Guid operation,bool release)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||operation!=actionAdmissionScenario||!actionAdmissionScenarioHit||
            !actionRemoteHostOriginals.TryGetValue(operation,out var source)||!source.AdmissionScenarioPaused||source.AdmissionStage!=actionAdmissionScenarioStage)
            throw new InvalidOperationException("Await actual armed host admission checkpoint");
        byte[] expected=EncodeActionRemoteEnvelope(actionAdmissionScenarioStage+"\n"+source.Click,source.NativeBefore);
        if(!ActionEnvelopeFileEquals(Path.Combine(source.Directory,"admission-current.native"),expected)||
            !ActionEnvelopeFileEquals(Path.Combine(source.Directory,"admission-"+actionAdmissionScenarioStage+".native"),expected)||
            source.AdmissionIssued||source.AdmissionEntered!=(actionAdmissionScenarioStage=="invoking")||actionPayments.Any(value=>ReferenceEquals(value.RemoteOwner,source)))
            throw new InvalidOperationException("Full durable original or actual pre-entry state changed");
        byte[] bag=RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items),belt=RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items);
        source.AdmissionScenarioPaused=false;
        try
        {
            if(actionAdmissionScenarioStage=="invoking")for(int n=0;n<3;n++)PumpRemoteActionAdmissions();
            if(!bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.bag.ItemGrid.items))||
                !belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(source.Actor.inventory.ItemGrid.items))||
                actionPayments.Any(value=>ReferenceEquals(value.RemoteOwner,source)))throw new InvalidOperationException("Uncertain admission retried or changed complete financial grids");
        }
        finally{source.AdmissionScenarioPaused=true;}
        if(!release)return;
        if(actionAdmissionScenarioStage!="ready")throw new InvalidOperationException("An entered admission cannot be reset by the fixture");
        source.AdmissionScenarioPaused=false;PumpRemoteActionAdmissions();
    }
    private static Guid actionClientCheckpointScenario;private static string actionClientCheckpointScenarioBoundary;private static bool actionClientCheckpointScenarioHit;
    internal static void ArmActionClientCheckpointScenario(Guid operation,string boundary)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actionRemoteClick?.Operation!=operation||
            boundary!="initial-saved"&&boundary!="attempt-saved"&&boundary!="invocation-saved")throw new InvalidOperationException("Actual disposable pending remote click and known boundary required");
        actionClientCheckpointScenario=operation;actionClientCheckpointScenarioBoundary=boundary;actionClientCheckpointScenarioHit=false;
        actionClientCheckpointBoundary=(id,observed)=>
        {
            if(id!=operation||observed!=boundary)return;
            actionClientCheckpointScenarioHit=true;actionClientCheckpointBoundary=null;
            throw new IOException("CodexTest interrupted actual client checkpoint at "+observed);
        };
    }
    internal static void VerifyActionClientCheckpointScenario(Guid operation)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actionClientCheckpointScenario!=operation||!actionClientCheckpointScenarioHit||
            actionRemoteClick is not ActionRemoteClick click||click.Operation!=operation)throw new InvalidOperationException("Await actual armed client checkpoint boundary");
        if(actionClientCheckpointScenarioBoundary=="initial-saved")
        {
            if(click.InitialCheckpoint?.NativeSaved!=true||click.DebitEntered||click.RefundEntered||click.Plan!=null)
                throw new InvalidOperationException("Initial native Save did not remain before any financial grant");
            return;
        }
        var checkpoint=click.RefundCheckpoint??click.DebitCheckpoint;
        if(checkpoint?.NativeSaved!=true||checkpoint.Native==null||!ActionEnvelopeFileEquals(Path.Combine(checkpoint.Directory,checkpoint.Refund?"refund-before.native":"debit-before.native"),checkpoint.Native))
            throw new InvalidOperationException("Actual attempted native preimage was not fully archived");
        if(actionClientCheckpointScenarioBoundary!="invocation-saved")return;
        if(!checkpoint.Invoked||click.DebitEntered||click.RefundEntered)throw new InvalidOperationException("Invocation interruption did not retain unknown before first setter");
        var bag=RebirthCamperInventorySnapshot.Capture(click.Bag.items);var belt=RebirthCamperInventorySnapshot.Capture(click.Belt.items);
        for(int attempt=0;attempt<3;attempt++)ApplyActionRemoteGrant(click,checkpoint.Refund);
        if(click.DebitEntered||click.RefundEntered||!bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(click.Bag.items))||!belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(click.Belt.items)))
            throw new InvalidOperationException("Courier replay entered an uncertain invocation or changed source grids");
    }
    internal static void ReleaseActionClientCheckpointScenario(Guid operation)
    {
        VerifyActionClientCheckpointScenario(operation);
        var click=actionRemoteClick;var checkpoint=click.InitialCheckpoint??click.RefundCheckpoint??click.DebitCheckpoint;
        if(checkpoint==null||checkpoint.Invoked)throw new InvalidOperationException("Uncertain invocation cannot be reset by fixture");
        checkpoint.ScenarioPaused=false;actionClientCheckpointBoundary=null;
        // Normal same-original pumps/couriers perform the continuation.
    }
    internal static void VerifyActionRemoteEnvelopeScenario()
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
            throw new InvalidOperationException("Actual disposable CodexTest main thread required");
        // Exercise the real shared encoder and both production reader purposes.
        // The host may skip described PDS allocation, but must reject every
        // truncation/trailing byte which the retaining client reader rejects.
        byte[] native={1,2,3,4};const string original="<electricalOwnerClick version=\"1\"/>";
        byte[] body=EncodeActionRemoteEnvelope(original,native);
        ReadActionRemoteEnvelope(body,out var hostText,out var skipped,false);
        ReadActionRemoteEnvelope(body,out var clientText,out var retained);
        if(hostText!=original||clientText!=original||skipped!=null||!retained.SequenceEqual(native)||ReferenceEquals(retained,native))
            throw new InvalidOperationException("Purpose framing changed text/image ownership");
        var truncated=body.Take(body.Length-1).ToArray();var trailing=body.Concat(new byte[]{0}).ToArray();
        foreach(var invalid in new[]{truncated,trailing})foreach(bool keep in new[]{false,true})
        {
            bool refused=false;
            try{ReadActionRemoteEnvelope(invalid,out _,out _,keep);}catch(InvalidDataException){refused=true;}catch(EndOfStreamException){refused=true;}
            if(!refused)throw new InvalidOperationException("Purpose reader accepted incomplete or trailing native image");
        }
    }
    internal static Guid BeginActionRemoteScenario(Vector3i position,string checkpointBoundary=null)
    {
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest")
            throw new InvalidOperationException("Actual disposable CodexTest main thread required");
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
        if(checkpointBoundary!=null)
        {
            InstallActionPaymentNativeObservers();
            if(!CaptureActionRemoteClick(actor,position,out var pending,out _))throw new InvalidOperationException("Actual remote click source unavailable");
            ArmActionClientCheckpointScenario(pending,checkpointBoundary);
        }
        if(actor?.world?.IsRemote()!=true||!RequestOwnedElectricalService(actor,position)||actionRemoteClick==null)
            throw new InvalidOperationException("Actual remote native click/preparation source could not be retained");
        return actionRemoteClick.Operation;
    }
    internal static void VerifyActionRemoteNoAdmissionScenario(Guid operation)
    {
        RequireActionScenario();
        if(!actionRemoteHostOriginals.TryGetValue(operation,out var source)||source.NoAdmission==null||!source.Confirmed||source.AdmissionIssued||
            !source.Accepted||!source.Saved||actionPayments.Any(value=>value.Operation==operation))
            throw new InvalidOperationException("Await actual rejected preparation from authenticated saved source, before terminal release");
        PumpRemoteActionNoAdmission(source);
        var bytes=new System.Text.UTF8Encoding(false,true).GetBytes(source.NoAdmission);
        if(!ActionBytes(Path.Combine(source.Directory,"never-admitted.xml"),bytes.Length).SequenceEqual(bytes)||source.RefusalBody==null)
            throw new InvalidOperationException("Never-entered outcome was not saved before response");
        string original=source.NoAdmission;
        for(int attempt=0;attempt<3;attempt++)PumpRemoteActionAdmissions();
        if(source.NoAdmission!=original||source.AdmissionIssued||actionPayments.Any(value=>value.Operation==operation)||actionRemoteAdmission!=null)
            throw new InvalidOperationException("Rejected same original was replanned into a financial payment");
    }
    internal static void VerifyActionRemoteAdmissionScenario(Guid operation)
    {
        RequireActionScenario();
        if(!actionRemoteHostOriginals.TryGetValue(operation,out var source)||!source.Current()||!source.Accepted||!source.Saved)
            throw new InvalidOperationException("Actual authenticated native accepted and saved Before required");
        if(!ActionBytes(Path.Combine(source.Directory,"before.native"),source.NativeBefore.Length).SequenceEqual(source.NativeBefore))
            throw new InvalidOperationException("Actual full saved owner source differs");
        PumpRemoteActionAdmissions();
        var original=actionPayments.SingleOrDefault(value=>ReferenceEquals(value.RemoteOwner,source));
        if(original==null||original.Operation!=operation||!source.Confirmed||!source.AdmissionIssued||actionRemoteAdmission!=null)
            throw new InvalidOperationException("Actual saved source failed owning admission or leaked synchronous authority");
        int sequence=original.Sequence;var state=original.State;
        int nativeCalls=original.ScenarioNativeCalls.Values.Sum();
        for(int attempt=0;attempt<3;attempt++)PumpRemoteActionAdmissions();
        if(actionPayments.Count(value=>ReferenceEquals(value.RemoteOwner,source))!=1||
            !actionPayments.Any(value=>ReferenceEquals(value,original))||original.Sequence!=sequence||original.State!=state||
            original.ScenarioNativeCalls.Values.Sum()!=nativeCalls||actionRemoteAdmission!=null)
            throw new InvalidOperationException("Preparation retry duplicated an original or invoked a financial native boundary");
        foreach(var leg in original.Legs.Where(value=>value.RemoteOwner))
            if(!leg.Local||leg.Grid!=null||!ReferenceEquals(leg.Endpoint,source.Actor))
                throw new InvalidOperationException("Remote owner plan acquired a server replica setter");
    }
    internal static void VerifyActionRemoteCourierScenario(Guid operation)
    {
        RequireActionScenario();
        if(!actionTerminalCouriers.TryGetValue(operation,out var courier)||!courier.Current()||courier.Body==null||courier.Memory==null)
            throw new InvalidOperationException("Await actual saved native phase6 and financial graph retirement");
        if(actionPayments.Any(value=>value.Operation==operation&&!value.MemoryReleased)||actionRemoteHostOriginals.ContainsKey(operation))
            throw new InvalidOperationException("Terminal courier retained the live financial/source graph");
        var body=courier.Body;var actor=courier.Actor;
        var bag=RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items);
        var belt=RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items);
        // Queueing is intentionally not acknowledged delivery. The actual
        // courier remains owned until accepted native stamp removal, including
        // after any send callback/loss. This invokes the real producer only.
        courier.Next=0;PumpActionTerminalCouriers();
        if(!actionTerminalCouriers.TryGetValue(operation,out var retained)||!ReferenceEquals(retained,courier)||!ReferenceEquals(body,retained.Body)||
            !bag.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.bag.ItemGrid.items))||
            !belt.SequenceEqual(RebirthCamperInventorySnapshot.Capture(actor.inventory.ItemGrid.items)))
            throw new InvalidOperationException("Metadata courier changed owner inputs or retired on mere queue admission");
    }
}
#endif
