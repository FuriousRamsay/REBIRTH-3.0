using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool TryCancellationClientFiles(Guid job,out long bytes)
    {
        bytes=0;try
        {
            string prefix=job.ToString("N");int count=0;
            foreach(string domain in new[]{"RemoteResourceClientOriginals","NativePlacedOwnerPermissions","NativePlacedClientRetired"})
            {
                string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",domain);
                foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
                {
                    CompletionAncestors(folder);if(!Directory.Exists(folder))continue;
                    foreach(string path in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
                    {
                        if(++count>96)return false;if(folder==Path.Combine(root,"completed")&&Path.GetFileName(path)==prefix&&Directory.Exists(path))continue;
                        if(Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||folder==Path.Combine(root,"failedCandidates"))return false;
                        long length=new FileInfo(path).Length;int limit=domain=="RemoteResourceClientOriginals"?RebirthRemoteResourceClientOriginal.MaximumOriginalBytes:domain=="NativePlacedClientRetired"?MaximumApplicationCommand:MaximumResultBytes;
                        if(length<1||length>2L*limit+8192)return false;bytes=checked(bytes+8*length);
                        if(bytes>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
                    }
                }
            }
            return true;
        }
        catch{return false;}
    }
    private static bool CancellationUiCurrent(CancellationClient original)
    {
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);var window=original.Window;
        return original.NativeLease()&&TryUiQueue(window,out var queue)&&RebirthRemoteResourcePlacedStationQueue.Encode(queue)==body.Element("queue").Value&&
            (window.inputWindow==null||SameGrid(window.inputWindow.GetSlots(),original.Station.Input))&&
            (window.outputWindow==null||SameGrid(window.outputWindow.GetSlots(),original.Station.Output))&&
            (window.toolWindow==null||SameGrid(window.toolWindow.GetSlots(),original.Station.Tools))&&
            (window.fuelWindow==null||SameGrid(window.fuelWindow.GetSlots(),original.Station.Fuel));
    }
    private static bool ReadCancellationOriginal(Guid job,RebirthRemoteResourcePlacedStationPreparation.UploadRead memory,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        var record=new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemoteResourceClientOriginals")).Read(job);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Before.Length>RebirthRemoteResourceClientOriginal.MaximumOriginalBytes)return false;
        string text=new UTF8Encoding(false,true).GetString(record.Before);var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,RebirthRemoteResourceClientOriginal.MaximumOriginalBytes);
        return RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(body,out long workspace)&&
            memory.Reserve(checked(memory.ReservedBytes+Math.Max(0,workspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))&&
            RebirthRemoteResourceClientOriginal.TryDecodeOriginal(text,out original);
    }
    private static bool BeginCancellation(XUiC_RecipeStack card)
    {
        RebirthRemoteResourcePlacedStationPreparation.UploadRead memory=null;
        try
        {
            BindCancellationWorld();var actor=card?.xui?.playerUI?.entityPlayer;var window=card?.GetParentByType<XUiC_WorkstationWindowGroup>();var station=window?.WorkstationData?.TileEntity;
            if(actor?.world?.IsRemote()!=true||station==null||station is RebirthCamperStationState||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||
                window.windowGroup?.isShowing!=true||LockManager.Instance?.IsLockedByLocalPlayer(station,0)!=true||
                RebirthRemoteResourcePlacedStationClientCustody.Blocks(station)||!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(actor))return false;
            var marker=card.GetRecipe()?.ingredients?.FirstOrDefault()?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string jobText)||!Guid.TryParseExact(jobText,"N",out var job)||job==Guid.Empty||
                !marker.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.SourceKey,out string sourceText)||!Guid.TryParseExact(sourceText,"N",out var source)||source==Guid.Empty||
                !marker.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.BindingKey,out string binding))return false;
            Guid operation=CancellationOperation(job);if(cancellationClients.TryGetValue(operation,out var prior))return prior.Current();
            if(!TryCancellationClientFiles(job,out long clientFileBytes)||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(station,RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out long initialWorkspace)||
                !RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(operation,checked(initialWorkspace+clientFileBytes),out memory)||
                !ReadCancellationOriginal(job,memory,out var original)||!IsCandidate(original)||!RebirthRemoteResourceClientOriginal.Current(actor,original))return false;
            var terminal=SmallPlacedJournal(job,SmallJournalDomain.ClientRetired).Read(job);if(terminal?.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            string proof=(string)RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(terminal.Before),MaximumApplicationCommand).Attribute("proof");
            var paid=ClientPermissions().Read(job);if(paid==null||!paid.Before.SequenceEqual(paid.After))return false;
            var full=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(paid.Before),MaximumResultBytes);
            if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(full,out long historicalWorkspace)||
                !memory.Reserve(checked(memory.ReservedBytes+Math.Max(0,historicalWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))||
                !HasClientRetired(original,source,binding,proof)||
                !RebirthRemoteResourcePlacedStationReference.TryRead(full.Element("reference")?.Value,out var reference)||reference.Source!=source||reference.Binding!=binding||reference.Job!=job||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(full.Element("queue")?.Value,out var saved)||!TryUiQueue(window,out var queue))return false;
            var cards=window.craftingQueue.GetRecipesToCraft();int slot=Array.IndexOf(cards,card);
            var own=saved.Select((row,index)=>new{row,index}).Where(value=>value.row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string id)&&id==jobText)==true).ToArray();
            if(slot<0||slot>=queue.Length||own.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(queue[slot],own[0].row,job,source,binding,out _)||
                !ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out _,out var payment,out _,out _)||station.ToWorldPos()!=position||
                (window.inputWindow==null)!=(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory))return false;
            if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationOwnerBeforeCapture(actor,out long ownerWorkspace)||!memory.Reserve(checked(memory.ReservedBytes+ownerWorkspace)))return false;
            var before=RebirthCamperEnergyActorImage.Capture(actor);if(before.Cursor?.IsEmpty()!=true)return false;
            var request=new XElement("placedStationCancelRequest",new XAttribute("version",1),new XAttribute("stage",0),new XAttribute("cancel",operation.ToString("N")),
                new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("proof",proof),new XAttribute("birth",reference.Birth),new XAttribute("slot",slot),
                original.Copy(),new XElement("queue",RebirthRemoteResourcePlacedStationQueue.Encode(queue)),new XElement("actorBefore",RebirthRemoteResourceClientOriginal.ActorImage(before)),
                new XElement("input",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(station.Input))),new XElement("paidOriginal",new UTF8Encoding(false,true).GetString(paid.Before)));
            string header=request.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(header);
            if(bytes.Length>MaximumResultBytes||!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(request,null,out long workspace)||
                !memory.Reserve(checked(memory.ReservedBytes+workspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes+32L*bytes.Length+65536))||
                CancellationFilesExist(operation,"Client"))return false;
            var state=new CancellationClient{Actor=actor,Original=original,Operation=operation,Source=source,Binding=binding,Proof=proof,Birth=reference.Birth,Slot=slot,
                NativeOwnerId=actor.entityId,OriginalWorld=actor.world,Window=window,Card=card,Station=station,Chunk=station.GetChunk(),Directory=GameIO.GetSaveGameDir(),Header=header,Before=before,Memory=memory,JournalWorkspace=32L*bytes.Length+65536,Stage=-1,
                SavedPaidRow=own[0].row,FullPaidPermission=new UTF8Encoding(false,true).GetString(paid.Before)};
            if(!CancellationUiCurrent(state)||!SaveCancellation(operation,source,original.Creation,"Client",bytes,false))return false;
            cancellationClients.Add(operation,state);memory=null;
            if(!RebirthRemoteResourceOwnerAction.HoldPlacedCancellation(state))return true;
            actor.Buffs.SetCustomVar(CancellationReceipt(operation),10f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            QueueCancellationBefore(state);return true;
        }
        catch{return false;}
        finally{memory?.Dispose();}
    }
    private static void QueueCancellationBefore(CancellationClient original)
    {
        if(original.HeaderQueued||!original.NativeLease()||!RebirthRemoteResourceOwnerAction.HoldsPlacedCancellation(original)||!CancellationUiCurrent(original)||!original.Before.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return;
        var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
        var retained=original.Memory;
        original.HeaderQueued=NetPackageRebirthRemoteRepairOriginal.SendObserved(original.Actor.entityId,persistent.PrimaryId,original.Original,original.Header,sent=>
        {
            original.HeaderPending=false;
            try
            {
            original.Stage=1;
            if(!sent||cancellationFlush!=null||!original.Current()||!CancellationUiCurrent(original))return;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;var connection=manager?.connectionToServer?[0];
            if(manager?.IsClient!=true||manager.IsServer||!RebirthLogisticsOriginalTransport.CanSendNative(connection))return;
            cancellationFlush=original;
            try
            {
                // Actual native SetModified inside this original sync emits
                // the one ToServer body after the full observation header.
                original.Window.syncTEfromUI();if(original.FlushObserved&&CancellationUiCurrent(original)){original.Stage=1;original.Next=0;}
            }
            finally{cancellationFlush=null;}
            }
            finally{if(original.Memory==null)retained.Dispose();}
        });
        original.HeaderPending=original.HeaderQueued;
    }
    private static bool EnterCancellationFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,out object state)
    {
        state=null;var original=cancellationFlush;if(original==null)return true;
        if(!runOriginal||original.FlushEntered||!ReferenceEquals(window,original.Window)||!CancellationUiCurrent(original))return false;
        original.FlushEntered=true;state=original;return true;
    }
    private static void ObserveCancellationFlush(XUiC_WorkstationWindowGroup window,bool runOriginal,object state)
    {
        var original=state as CancellationClient;
        if(!runOriginal||original==null||!ReferenceEquals(original,cancellationFlush)||!ReferenceEquals(window,original.Window)||!original.FlushEntered||!CancellationUiCurrent(original))return;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        original.FlushObserved=RebirthRemoteResourcePlacedStationQueue.Encode(original.Station.Queue)==body.Element("queue").Value;
    }
    private static void SendCancellationRequest(CancellationClient original,int stage)
    {
        if(!original.Current()&&(stage!=3||!CancellationMetadataCandidate(original))&&(stage!=2||!CancellationSavedAfterCandidate(original)))return;var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)return;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);body.SetAttributeValue("stage",stage);
        NetPackageRebirthRemoteRepairOriginal.Send(original.Actor.entityId,persistent.PrimaryId,original.Original,body.ToString(SaveOptions.DisableFormatting));
    }
    private static void PumpCancellationClient()
    {
        float now=Time.realtimeSinceStartup;
        foreach(var original in cancellationClients.Values.ToArray())
        {
            if(!original.Current()&&!CancellationMetadataCandidate(original)&&!CancellationSavedAfterCandidate(original))continue;if(original.Stage<0){QueueCancellationBefore(original);continue;}
            if(original.Stage==0||now<original.Next)continue;original.Next=now+1;
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);SendCancellationRequest(original,original.Stage);
        }
    }
}
