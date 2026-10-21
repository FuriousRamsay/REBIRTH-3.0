using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class ClientJob
    {internal RebirthRemoteResourceClientOriginal.Intent Original;internal ItemActionEntryCraft Entry;internal float Next;internal int Stage;internal ClientPaid Paid;}
    private sealed class ClientPaid
    {internal EntityPlayerLocal Player;internal Guid Source;internal string Binding,Canonical;internal RebirthCamperEnergyActorImage Before,After;internal bool DebitGranted,PreparedConfirmed,UncertainConfirmed;}
    private static readonly Dictionary<Guid,ClientJob> clientJobs=new Dictionary<Guid,ClientJob>();
    private static World clientWorld;private static string clientDirectory;private static float clientScan;
    private static RebirthRemoteResourceClientOriginalJournal ClientPermissions()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedOwnerPermissions"));
    private static void BindClientWorld(World world)
    {string directory=GameIO.GetSaveGameDir();if(!ReferenceEquals(world,clientWorld)||directory!=clientDirectory){clientJobs.Clear();beforeFlushPending.Clear();clientWorld=world;clientDirectory=directory;clientScan=0;}}
    internal static bool BeginClient(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> required,List<ItemStack> remote)
    {
        try
        {
            if(!ProtocolActive||!IsEntryCandidate(player,entry)||!RebirthRemoteResourceRepairProtocol.SupportsClientTransport())return false;
            BindClientWorld(player.world);
            if(clientJobs.Values.Any(job=>ReferenceEquals(job.Entry,entry)&&RebirthRemoteResourceClientOriginal.Current(player,job.Original)))return true;
            if(!RebirthRemoteResourceClientOriginal.TryReadPending(out var pending))return false;
            foreach(var old in pending.Where(value=>IsCandidate(value)&&RebirthRemoteResourceClientOriginal.Current(player,value)))
            {
                // A different click cannot manufacture a new original over an
                // unresolved native owner/target claim. Reconnect queries use
                // the retained original; they do not replay this native entry.
                if(!clientJobs.ContainsKey(old.Operation)&&clientJobs.Count<32)clientJobs.Add(old.Operation,new ClientJob{Original=old,Stage=1});
                return true;
            }
            if(clientJobs.Count>=32||!Capture(player,entry,epoch,request,required,remote,out var original)||
                !RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,original))return false;
            var job=new ClientJob{Original=original,Entry=entry,Stage=-1};clientJobs.Add(original.Operation,job);
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation),10f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            // Failure retains the durable intent/native10 and is retried by
            // the original pump; it never falls back to legacy bag payment.
            if(!FlushCapturedBefore(player,entry,original))job.Stage=1;return true;
        }
        catch{return false;}
    }
    private static void ClientUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(!ProtocolActive)return;var world=GameManager.Instance?.World;BindClientWorld(world);BindUploadWorld();BindReadyWorld();BindApplicationWorld();
        if(world?.IsRemote()!=true)return;var player=world.GetPrimaryPlayer();if(player==null||player.IsDead()||!player.IsSpawned())return;
        float now=Time.realtimeSinceStartup;
        if(now>=clientScan)
        {
            clientScan=now+5;
            if(RebirthRemoteResourceClientOriginal.TryReadPending(out var pending))foreach(var original in pending)
                if(IsCandidate(original)&&(RebirthRemoteResourceClientOriginal.Current(player,original)||EndedClientScope(player,original))&&!clientJobs.ContainsKey(original.Operation)&&clientJobs.Count<32)
                    clientJobs.Add(original.Operation,new ClientJob{Original=original,Stage=1});
        }
        foreach(var job in clientJobs.Values.ToArray())
        {
            bool ended=EndedClientScope(player,job.Original);
            if(!ended&&job.Stage<0){PumpCapturedBefore(job);continue;}
            if(now<job.Next||!ended&&!RebirthRemoteResourceClientOriginal.Current(player,job.Original))continue;
            job.Next=now+1;var persistent=GameManager.Instance.GetPersistentLocalPlayer();if(persistent?.PrimaryId==null)continue;
            if(!ended&&job.Stage==0)
            {
                if(job.Entry==null||!RebirthRemoteResourcePlacedStationClientCustody.TryHoldFresh(player,job.Entry,job.Original))continue;
            }
            GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            var query=new XElement("placedStationRequest",new XAttribute("version",1),new XAttribute("stage",ended?4:job.Stage),job.Original.Copy());
            if(ended)NetPackageRebirthRemoteRepairOriginal.SendPlacedEnded(player.entityId,persistent.PrimaryId,job.Original);
            else NetPackageRebirthRemoteRepairOriginal.Send(player.entityId,persistent.PrimaryId,job.Original,query.ToString(SaveOptions.DisableFormatting));
        }
    }
    private static string CanonicalPermission(XElement reply)
    {
        var body=new XElement(reply);body.SetAttributeValue("phase",2);body.SetAttributeValue("outcome",1);body.SetAttributeValue("debitPermission",false);body.SetAttributeValue("ownerProof","");
        return body.ToString(SaveOptions.DisableFormatting);
    }
    private static bool PermissionCurrent(EntityPlayerLocal player,ClientJob job,ClientPaid paid)
    {
        if(!ReferenceEquals(job.Paid,paid)||!ReferenceEquals(paid.Player,player)||!RebirthRemoteResourceClientOriginal.Current(player,job.Original)||
            !RebirthRemoteResourcePlacedStationClientCustody.OwnsOriginal(player,job.Original))return false;
        // Private observations are created only after exact durable readback
        // below. Native setters may query this once per slot; do not reread the
        // entire multi-megabyte permission journal for every permit check.
        return paid.PreparedConfirmed&&paid.DebitGranted&&paid.UncertainConfirmed;
    }
    internal static void Result(EntityPlayerLocal player,Guid operation,string text)
    {
        if(!ProtocolActive||!clientJobs.TryGetValue(operation,out var job)||!RebirthRemoteResourceClientOriginal.Current(player,job.Original)&&!EndedClientScope(player,job.Original))return;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            if(body.Name=="placedStationEndedNoPayment"){AcceptEndedNeverEntered(player,job,body);return;}
            if(!RebirthRemoteResourceClientOriginal.Current(player,job.Original))return;
            if(body.Name=="placedStationNoPayment"){AcceptNoPaymentResult(player,job,body);return;}
            if(body.Name=="placedStationProgress")
            {
                if(body.Attributes().Count()!=4||body.Nodes().Any()||(int?)body.Attribute("version")!=1||
                    (string)body.Attribute("original")!=operation.ToString("N")||(string)body.Attribute("digest")!=job.Original.Digest||
                    (int?)body.Attribute("phase") is not int phase||phase<0||phase>1)return;
                return; // Metadata only; no permission or source paid payload.
            }
            if((int?)body.Attribute("phase")==4){AcceptUsedResult(player,job,body);return;}
            if(!TryAcceptClientPermission(player,job,body,out var paid))return;
            bool Current()=>PermissionCurrent(player,job,paid);
            float native=player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(operation));
            if(native==10f&&paid.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))
            {
                if(!RebirthRemoteResourceOwnerAction.TryHoldPreparedOriginal(player,job.Original))return;
                player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(operation),1f,true);GameManager.Instance.SaveLocalPlayerData();
                GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);job.Stage=1;job.Next=0;return;
            }
            if((bool)body.Attribute("debitPermission")&&native==1f)
            {
                if(!paid.DebitGranted)
                {
                    if(!paid.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return;
                }
                var journal=ClientPermissions();var record=journal.Read(operation);
                if(record==null||record.Vehicle!=paid.Source||record.OwnerCreation!=job.Original.Creation||record.Revision!=0||
                    !record.Before.SequenceEqual(record.After)||new UTF8Encoding(false,true).GetString(record.Before)!=paid.Canonical)return;
                if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
                if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return;
                paid.UncertainConfirmed=true;paid.DebitGranted=true;
                if(!Current()||!RebirthRemoteResourceOwnerAction.TryHold(player,job.Original,paid.Source,paid.Binding,paid.Before,paid.After,Current))return;
                // Warm continuation uses the same private actual native1 grant
                // and exact original hold. Cold mixed images grant no setters.
                foreach(var area in new[]{new[]{paid.Before.Bag,paid.After.Bag},new[]{paid.Before.Belt,paid.After.Belt}})
                {
                    bool bag=ReferenceEquals(area[0],paid.Before.Bag);var grid=bag?player.bag.ItemGrid:player.inventory.ItemGrid;
                    for(int slot=0;slot<area[1].Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(area[0][slot],area[1][slot])&&
                        !RebirthRemoteResourceOwnerAction.WriteOriginalSlot(player,operation,grid,slot,area[1][slot]))return;
                }
                if(!paid.After.Matches(RebirthCamperEnergyActorImage.Capture(player)))return;
                player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(operation),2f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
                job.Stage=1;job.Next=0;
            }
        }
        catch{/* Retain all paid intent/cost custody. No callback replay/refund. */}
    }
    private static bool TryAcceptClientPermission(EntityPlayerLocal player,ClientJob job,XElement body,out ClientPaid paid,bool terminalOnly=false)
    {
        paid=null;var original=job.Original;
        if(body.Name!="placedStationResult"||body.Attributes().Count()!=11||body.Elements().Count()!=6||
            body.Elements().Select(element=>element.Name.LocalName).Distinct().Count()!=6||
            body.Elements().Any(element=>element.Name.NamespaceName.Length!=0||!new[]{"reference","beforeActor","afterActor","afterTile","queue","paid"}.Contains(element.Name.LocalName)||element.HasAttributes||element.Nodes().Any(node=>node is not XText))||
            (int?)body.Attribute("version")!=1||(string)body.Attribute("original")!=original.Operation.ToString("N")||
            (string)body.Attribute("digest")!=original.Digest||(int?)body.Attribute("phase")!=2||(int?)body.Attribute("outcome")!=1||
            (bool?)body.Attribute("permission")!=true||(bool?)body.Attribute("debitPermission")==null||(string)body.Attribute("ownerProof")!=""||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(body)||
            !RebirthRemoteResourcePlacedStationReference.TryRead(body.Element("reference").Value,out var reference)||reference.Job!=original.Operation||reference.Original!=original.Digest||
            (string)body.Attribute("source")!=reference.Source.ToString("N")||(string)body.Attribute("sourceWorld")!=reference.World||(string)body.Attribute("binding")!=reference.Binding||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)||reference.Owner!=owner.StorageKey||reference.Creation!=original.Creation||
            reference.Source!=RebirthRemoteResourceDurableJournal.Operation(reference.World,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind)||
            (!terminalOnly&&!RebirthRemoteResourcePlacedStationClientCustody.OwnsOriginal(player,original))||
            (terminalOnly&&(bool?)body.Attribute("debitPermission")!=false)||body.Element("beforeActor").Value!=original.Copy().Element("actorBefore").Value||
            !ValidateInput(original,out var position,out _,out _,out _,out var template,out int count,out int tier,out int belt,out var payment,out _,out var input)||payment!=reference.Payment)return false;
        var before=RebirthRemoteResourceClientOriginal.ReadActor(body.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(body.Element("afterActor").Value);
        var fragments=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element("paid").Value));
        var requirements=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
        RebirthCamperEnergyActorImage derived;ItemStack[] afterInput;List<ItemStack> allPaid;
        if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory)
        {
            if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(before,requirements,fragments,belt)||!RebirthRemoteResourceCraftPlan.TryDebit(before,requirements,fragments,belt,out derived,out allPaid))return false;
            afterInput=input;
        }
        else
        {
            if(!RebirthRemoteResourcePlacedStationInputPlan.TryCreate(input,requirements,fragments,out var plan))return false;
            derived=before;afterInput=plan.CopyAfter();allPaid=plan.CopyPaid().ToList();
        }
        if(!after.Matches(derived)||terminalOnly&&(player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation))!=2f||!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))||
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(Convert.FromBase64String(body.Element("afterTile").Value),out var tile)||
            tile.LocalPosition!=new Vector3i(position.x&15,position.y,position.z&15)||
            !RebirthCamperInventorySnapshot.Capture(tile.Input).SequenceEqual(RebirthCamperInventorySnapshot.Capture(afterInput))||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(body.Element("queue").Value,out var queue)||
            RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(tile.Queue))return false;
        var marked=queue.Select((row,slot)=>new{row,slot}).Where(pair=>pair.row?.Recipe?.ingredients?.FirstOrDefault()?.itemValue?.HasMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey)==true).ToArray();
        var own=marked.Where(pair=>pair.row.Recipe.ingredients[0].itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string jobId)&&jobId==original.Operation.ToString("N")).ToArray();
        if(own.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryCreate(original.Operation,reference.Source,reference.Binding,template,allPaid,count,tier,terminalOnly?own[0].row.StartingEntityId:player.entityId,
            own[0].slot==queue.Length-1,out var expected,original.Copy().Element("action").Element("outputImage").Value)||
            RebirthRemoteResourcePlacedStationQueue.Encode(new[]{expected})!=RebirthRemoteResourcePlacedStationQueue.Encode(new[]{own[0].row}))return false;
        string canonical=CanonicalPermission(body);byte[] bytes=Encoding.UTF8.GetBytes(canonical);var journal=ClientPermissions();
        var record=terminalOnly?journal.Read(original.Operation):journal.Prepare(original.Operation,reference.Source,original.Creation,0,bytes,bytes);
        if(record==null||record.Vehicle!=reference.Source||record.OwnerCreation!=original.Creation||record.Revision!=0||
            terminalOnly&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain||
            (record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain)||
            !record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||
            !(terminalOnly?RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out string savedWorld,out var savedSource,out string savedBinding,out var savedPaid)&&
                savedWorld==reference.World&&savedSource==reference.Source&&savedBinding==reference.Binding&&RebirthCamperInventorySnapshot.Capture(savedPaid).SequenceEqual(RebirthCamperInventorySnapshot.Capture(fragments)):
                RebirthRemoteResourceClientOriginal.SaveCraftSource(player,original,reference.World,reference.Source,reference.Binding,fragments,()=>RebirthRemoteResourceClientOriginal.Current(player,original))))return false;
        if(job.Paid!=null)
        {if(job.Paid.Source!=reference.Source||job.Paid.Binding!=reference.Binding||job.Paid.Canonical!=canonical)return false;if(ReferenceEquals(job.Paid.Player,player)){paid=job.Paid;return true;}if(!terminalOnly)return false;}
        var created=new ClientPaid{Player=player,Source=reference.Source,Binding=reference.Binding,Canonical=canonical,Before=before,After=after,PreparedConfirmed=true};job.Paid=created;paid=created;return true;
    }
}
