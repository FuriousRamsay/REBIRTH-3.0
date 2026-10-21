using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool ValidateCancellationPermission(CancellationClient original,XElement body,bool terminalOnly=false)
    {
        if(body.Name!="placedCancellationPermission"||body.Attributes().Count()!=5||body.Elements().Count()!=7||(int?)body.Attribute("version")!=1||
            (string)body.Attribute("cancel")!=original.Operation.ToString("N")||(string)body.Attribute("job")!=original.Original.Operation.ToString("N")||
            (string)body.Attribute("source")!=original.Source.ToString("N")||(string)body.Attribute("binding")!=original.Binding||body.Element("request")?.Value!=original.Header)return false;
        var header=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(header,body,out long workspace))return false;
        workspace=Math.Max(0,workspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes);
        if(workspace>original.PermissionWorkspace)
        {if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace-original.PermissionWorkspace)))return false;original.PermissionWorkspace=workspace;}
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(header.Element("queue").Value,out var queue)||
            !RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(queue[original.Slot],original.SavedPaidRow,original.Original.Operation,original.Source,original.Binding,out var remaining)||
            !ValidateInput(original.Original,out _,out _,out _,out _,out _,out _,out _,out int belt,out var payment,out _,out _)||
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(Convert.FromBase64String(body.Element("beforeTile").Value),out var before)||
            RebirthCookingRemoteAdmissionBackend.Queue(before.Queue)!=RebirthCookingRemoteAdmissionBackend.Queue(queue)||
            Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(before.Input))!=header.Element("input").Value||
            !CancellationQueuePlan(queue,original.Slot,original.NativeOwnerId,out var afterQueue,out _))return false;
        var afterActor=original.Before.Copy();var input=ItemStack.Clone(before.Input);
        if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory)
        {
            if(!RebirthRecyclerOwnerDelivery.Plan(original.Before.Bag,original.Before.Belt,original.Before.BagLocks,null,belt,remaining.ToArray(),out var bag,out var toolbelt))return false;
            afterActor.Bag=bag;afterActor.Belt=toolbelt;
        }
        else foreach(var fragment in remaining){if(ItemStack.AddToItemStackArray(input,fragment.Clone())==-1)return false;}
        var after=Convert.FromBase64String(body.Element("afterTile").Value);
        byte[] expected;
        if(terminalOnly){before.Input=input;before.Queue=afterQueue;expected=RebirthRemoteResourcePlacedStationNativeReader.Encode(before);}
        else if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(original.Station,input,before.Output,afterQueue,before.Burning,out expected,out _))return false;
        if(body.Element("afterActor").Value!=RebirthRemoteResourceClientOriginal.ActorImage(afterActor)||
            body.Element("refund").Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(remaining.ToArray()))||
            body.Element("afterQueue").Value!=RebirthRemoteResourcePlacedStationQueue.Encode(afterQueue)||
            !expected.SequenceEqual(after))return false;
        if(!ValidateInput(original.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
            !ValidateCancellationSnapshot(RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("beforeEvidence").Value,MaximumResultBytes),original.Operation,original.Source,original.Binding,original.Birth,original.Header,false,
                Convert.FromBase64String(body.Element("beforeTile").Value),original.Before,original.NativeOwnerId,original.Original.Deaths,position,null,out var savedBefore))return false;
        byte[] marker=CancellationMarkerImage(original.Operation,original.Original.Operation,original.Source,original.Binding,original.Birth,original.Header);int growth;
        using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write("RebirthPlacedCancellationV1:"+original.Operation.ToString("N"));writer.Write(ulong.MaxValue);writer.Write(false);writer.Write((ushort)marker.Length);writer.Write(marker);writer.Flush();growth=checked((int)stream.Length);}
        if(!RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(savedBefore.Snapshot,savedBefore.Offset,savedBefore.Tile,after,growth,out int raw,out _,out long bound))return false;
        long future=checked(32*(4L*((raw+bound+1024*1024+2)/3)+RebirthRemoteResourceClientOriginal.ActorImage(afterActor).Length+65536)+65536);
        if(future>original.TerminalWorkspace)
        {if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+future-original.TerminalWorkspace)))return false;original.TerminalWorkspace=future;}
        original.After=afterActor;original.NativeAfter=after;original.NativeInput=input;original.NativeQueue=afterQueue;return true;
    }
    internal static void CancellationResult(EntityPlayerLocal actor,Guid job,string text)
    {
        if(!ProtocolActive||!cancellationClients.TryGetValue(CancellationOperation(job),out var original)||!ReferenceEquals(original.Actor,actor))return;
        bool metadataOnly=!original.Current();bool savedAfterCandidate=CancellationSavedAfterCandidate(original);
        if(metadataOnly&&!CancellationMetadataCandidate(original)&&!savedAfterCandidate)return;
        if((metadataOnly||!original.NativeBodyCurrent())&&(cancellationNativeResult?.Current!=true||cancellationNativeResult.Text!=text||cancellationNativeResult.Operation!=job))return;
        if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return;
        using(scratch)
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            if(body.Name!="placedStationCancelResult"||body.Attributes().Count()!=5||body.Elements().Count()!=(body.Element("reconstruction")==null?2:3)||(int?)body.Attribute("version")!=1||
                (string)body.Attribute("original")!=job.ToString("N")||(string)body.Attribute("cancel")!=original.Operation.ToString("N")||
                (int?)body.Attribute("phase") is not int phase||phase!=1&&phase!=4&&phase!=5||(bool?)body.Attribute("grant") is not bool grant||grant&&phase!=1)return;
            // A warm owner may retain its exact native2 grid/version while a
            // restarted host reconstructs Before and publishes its first After.
            // The actual typed publication result can finish metadata without
            // borrowing a newly opened UI lock or rewriting the native tile.
            bool savedAfter=savedAfterCandidate&&phase==4&&(metadataOnly||body.Element("reconstruction")!=null);
            if(body.Element("reconstruction")!=null&&phase!=5&&phase!=4&&(phase!=1||original.RestartBeforeOwner==null))return;
            string canonical=body.Element("permission")?.Value;if(canonical==null||original.Permission!=null&&original.Permission!=canonical)return;
            var permission=RebirthCookingRemoteAdmission.ReadProtocolXml(canonical,MaximumResultBytes);
            if(metadataOnly&&phase!=5&&!savedAfter||!ValidateCancellationPermission(original,permission,metadataOnly||original.SavedAfterProjection!=null||original.RestartBeforeOwner!=null)||!metadataOnly&&!original.Current())return;
            if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Permission",Encoding.UTF8.GetBytes(canonical),false))return;
            original.Permission=canonical;original.PermissionSaved=true;
            float native=actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation));
            if(phase==1)
            {
                if(native==10f&&original.Before.Matches(RebirthCamperEnergyActorImage.Capture(actor)))
                {
                    actor.Buffs.SetCustomVar(CancellationReceipt(original.Operation),1f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);original.Stage=1;original.Next=0;return;
                }
                if(grant&&native==1f)
                {
                    if(original.RestartBeforeOwner!=null&&!AcceptRestartBeforeGrant(original,body.Element("reconstruction")?.Value))return;
                    if(!CancellationOwnerPermission.TryCreate(original,out var proof)||!RebirthRemoteResourceOwnerAction.ApplyPlacedCancellation(proof)||
                        !original.After.Matches(RebirthCamperEnergyActorImage.Capture(actor)))return;
                    actor.Buffs.SetCustomVar(CancellationReceipt(original.Operation),2f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
                    original.OwnerSavedAfterVersion=RebirthRemoteResourceOwnerAction.PlacedCancellationSavedAfterVersion.Capture(original);original.Stage=2;original.Next=0;
                }
                return;
            }
            if(native!=2f&&native!=3f||!original.After.Matches(RebirthCamperEnergyActorImage.Capture(actor)))return;
            string evidenceText=body.Element("evidence")?.Value;
            if(original.SavedAfterProjection!=null&&evidenceText!=original.TerminalEvidence)return;
            if(metadataOnly&&!savedAfter&&evidenceText!=original.TerminalEvidence)return;
            if(evidenceText==null||Encoding.UTF8.GetByteCount(evidenceText)>MaximumResultBytes||
                !original.Memory.Reserve(checked(original.Memory.ReservedBytes+Math.Max(0,12L*Encoding.UTF8.GetByteCount(evidenceText)-original.TerminalWorkspace))))return;
            original.TerminalWorkspace=Math.Max(original.TerminalWorkspace,12L*Encoding.UTF8.GetByteCount(evidenceText));
            var evidence=RebirthCookingRemoteAdmission.ReadProtocolXml(evidenceText,MaximumResultBytes);
            var marker=CancellationMarkerImage(original.Operation,original.Original.Operation,original.Source,original.Binding,original.Birth,original.Header);
            if(!ValidateInput(original.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                !ValidateCancellationSnapshot(evidence,original.Operation,original.Source,original.Binding,original.Birth,original.Header,true,
                    original.NativeAfter,original.After,original.NativeOwnerId,original.Original.Deaths,position,marker,out var savedNative))return;
            if(savedAfter)
            {
                var projection=CancellationSavedAfterProjection.FromActualResult(original,body.Element("reconstruction")?.Value,savedNative);
                if(projection==null)return;original.SavedAfterProjection=projection;
            }
            else if(metadataOnly&&!CancellationMetadataReentry.TryCreate(original,body.Element("reconstruction")?.Value,savedNative,out original.Reentry))return;
            original.TerminalEvidence=evidenceText;original.HostTerminal=true;
            if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Terminal",CancellationClientTerminalBytes(original),true))return;
            if(phase==4)
            {
                if(original.SavedAfterProjection==null&&original.RestartBeforeOwner==null&&!HydrateCancellationClient(original))return;
                actor.Buffs.SetCustomVar(CancellationReceipt(original.Operation),3f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
                original.OwnerTerminalVersion=RebirthRemoteResourceOwnerAction.PlacedCancellationTerminalVersion.Capture(original);if(original.OwnerTerminalVersion==null)return;
                original.Stage=3;original.Next=0;return;
            }
            if(native!=3f||!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"FinalAck",CancellationClientFinalAckBytes(original),true))return;
            original.HostReleased=true;
            if(!RebirthRemoteResourceOwnerAction.ReleasePlacedCancellation(original))return;
            actor.Buffs.SetCustomVar(CancellationReceipt(original.Operation),0f,true);actor.Buffs.RemoveCustomVar(CancellationReceipt(original.Operation));
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);DropCancellationClient(original);
        }
        catch{/* Mixed/unknown retains the exact original and its holds. */}
    }
    private static byte[] CancellationClientTerminalBytes(CancellationClient original)=>Encoding.UTF8.GetBytes(new XElement("placedCancellationClientTerminal",new XAttribute("version",1),
        new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),
        new XElement("permission",original.Permission),new XElement("evidence",original.TerminalEvidence)).ToString(SaveOptions.DisableFormatting));
    private static byte[] CancellationClientFinalAckBytes(CancellationClient original)=>Encoding.UTF8.GetBytes(new XElement("placedCancellationClientFinalAck",new XAttribute("version",1),
        new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),
        new XAttribute("terminal",RebirthRemoteResourceDurableJournal.Digest(Encoding.UTF8.GetString(CancellationClientTerminalBytes(original))))).ToString(SaveOptions.DisableFormatting));
    private sealed class CancellationClientSetter
    {internal CancellationClient Original;internal string Method;internal object Value;internal bool Used;}
    [ThreadStatic]private static CancellationClientSetter cancellationClientSetter;
    internal static bool ConsumeCancellationClientSetter(TileEntityWorkstation station,MethodBase method,object[] arguments)
    {
        var once=cancellationClientSetter;
        if(once==null||once.Used||!once.Original.NativeLease()||!once.Original.HostTerminal||!once.Original.PermissionSaved||
            once.Original.Actor.Buffs.GetCustomVar(CancellationReceipt(once.Original.Operation))!=2f&&once.Original.Actor.Buffs.GetCustomVar(CancellationReceipt(once.Original.Operation))!=3f||
            !ReferenceEquals(once.Original.Station,station)||method?.DeclaringType!=typeof(TileEntityWorkstation)||method.Name!=once.Method||arguments?.Length!=1||!ReferenceEquals(once.Value,arguments[0]))return false;
        once.Used=true;return true;
    }
    private static bool SetCancellationClient(CancellationClient original,string method,object value,Action invoke)
    {
        if(cancellationClientSetter!=null||!original.NativeLease())return false;
        var once=new CancellationClientSetter{Original=original,Method=method,Value=value};cancellationClientSetter=once;
        try{invoke();return once.Used&&original.NativeLease();}finally{cancellationClientSetter=null;}
    }
    private static bool HydrateCancellationClient(CancellationClient original)
    {
        if(!original.NativeLease()||!HasCancellationClientTerminal(original)||!original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
        var station=original.Station;
        if(RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var existing,out _)&&existing.SequenceEqual(original.NativeAfter)&&
            TryUiQueue(original.Window,out var currentUi)&&RebirthRemoteResourcePlacedStationQueue.Encode(currentUi)==RebirthRemoteResourcePlacedStationQueue.Encode(original.NativeQueue))return true;
        var header=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(header.Element("queue").Value,out var before)||
            !CancellationQueuePlan(before,original.Slot,original.Actor.entityId,out _,out var mapping)||station.Queue.Length!=mapping.Length)return false;
        var next=new RecipeQueueItem[mapping.Length];for(int slot=0;slot<next.Length;slot++)next[slot]=mapping[slot]<0?original.NativeQueue[slot]:station.Queue[mapping[slot]];
        if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,original.NativeInput,station.Output,next,station.IsBurning,out var expected,out _)||!expected.SequenceEqual(original.NativeAfter))return false;
        if(!SameGrid(station.Input,original.NativeInput)&&!SetCancellationClient(original,"set_Input",original.NativeInput,()=>station.Input=original.NativeInput))return false;
        if(!SetCancellationClient(original,"set_Queue",next,()=>station.Queue=next))return false;
        original.Window.syncUIfromTE();original.Card.IsCrafting=false;original.Card.ClearRecipe();original.Window.craftingQueue.RefreshQueue();original.Window.SetAllChildrenDirty();
        return TryUiQueue(original.Window,out var ui)&&RebirthRemoteResourcePlacedStationQueue.Encode(ui)==RebirthRemoteResourcePlacedStationQueue.Encode(original.NativeQueue)&&
            RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var actual,out _)&&actual.SequenceEqual(original.NativeAfter);
    }
}
