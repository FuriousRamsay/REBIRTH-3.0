using System;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static bool ObserveCancellationTerminalServer(EntityPlayer actor,Guid operation,string address,string creation,byte[] before,byte[] after)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthOriginalActionScope.Matches(actor,owner.StorageKey,creation)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var terminalOwner)||terminalOwner.id!=actor.entityId||terminalOwner.deaths!=actor.Died||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(terminalOwner,"rbCamperNativeRetired_"+operation.ToString("N"),out float retired,out bool retirementPresent)||!retirementPresent||retired!=1f||
                !RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(before,after)||
                !RebirthCamperStationAddress.TryParse(address,out var parsed)||
                !RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(actor.world,operation,address,owner.StorageKey,creation,out var publication)||publication.OriginalDeaths!=actor.Died||
                publication.CopyBeforeFrame().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)!=new UTF8Encoding(false,true).GetString(before))return false;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");
            if(!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,operation.ToString("N")+"*").Any(path=>
                (File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0||!new[]{".0.rcj",".1.rcj",".2.rcj"}.Any(suffix=>Path.GetFileName(path)==operation.ToString("N")+suffix)))return false;
            var native=new RebirthCamperTransferJournal(directory).Read(operation);
            if(native?.State!=RebirthCamperTransferJournal.Phase.Applied||native.Vehicle!=parsed.Vehicle||native.OwnerCreation!=creation||!native.Before.SequenceEqual(before)||!native.After.SequenceEqual(after))return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            if(!RebirthCamperStationSnapshot.Capture(parsed.Installation,publication.RestoreAfter()).SequenceEqual(Convert.FromBase64String(frame.Element("stationImage").Value)))return false;
            var prior=publication.RestoreBefore();int slot=(int)publication.CopyBeforeFrame().Attribute("queueSlot");
            if(slot<0||slot>=prior.Queue.Length||!RebirthCamperStationNativeCrafting.TryJob(prior.Queue[slot]?.Recipe,out var parent)||NativeCancellationOperation(parent)!=operation)return false;
            var marker=prior.Queue[slot].Recipe.ingredients.FirstOrDefault()?.itemValue;
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=4||root.Binding!=binding||
                (string)root.Copy().Attribute("disposition")!="used"||!TryReadPlan(root,out var plan)||plan.Job!=parent||plan.Address!=address||plan.Creation!=creation||plan.Deaths!=actor.Died||publication.InstalledDefinition!=plan.InstalledDefinition||
                !TryObservePublication(actor.world,root,out var proof)||(string)root.Copy().Attribute("ownerProof")!=proof)return false;
            return true;
        }
        catch{return false;}
    }
    // Cancellation owns a DIFFERENT native operation and receipt. It never
    // borrows source10 or the enqueue's Before/After inventory images.
    internal static bool CanRebindCancellationServer(EntityPlayer actor,RebirthCamperStationState station,Guid cancellation,byte[] before,byte[] after,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,bool[] locks,
        out RebirthCamperEnergyActorImage image,out PlayerDataFile saved)
    {
        image=null;saved=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||station?.BoundVehicle==null||
                !RebirthCamperModuleRuntime.TryGetExisting(station.BoundVehicle,out var runtime)||runtime.TransferPending||RebirthCookingRemoteAdmissionBackend.Held(station))return false;
            string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments");
            if(!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0||Directory.EnumerateFileSystemEntries(directory,cancellation.ToString("N")+"*").Any(path=>
                (File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0||!new[]{".0.rcj",".1.rcj",".2.rcj"}.Any(suffix=>Path.GetFileName(path)==cancellation.ToString("N")+suffix)))return false;
            var record=new RebirthCamperTransferJournal(directory).Read(cancellation);
            if(record==null||!record.Before.SequenceEqual(before)||!record.After.SequenceEqual(after)||!RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(before,after))return false;
            var installation=runtime.Stations.Find(station.InstallationId);if(installation?.InstalledItem==null||!ReferenceEquals(installation.State,station))return false;
            var prior=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(before),8*1024*1024);
            var next=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            bool Area(System.Xml.Linq.XElement node,ItemStack[] items)
            {
                var slots=node.Elements("slot").ToArray();return slots.Length==items.Length&&slots.Select((cell,index)=>
                    (int?)cell.Attribute("count")==items[index].count&&(string)cell.Attribute("item")==RebirthNativeItemCodec.Encode(items[index].itemValue)).All(value=>value);
            }
            var bagLocks=prior.Element("bagLocks");
            if(!Area(prior.Element("bag"),beforeBag)||!Area(prior.Element("belt"),beforeBelt)||!Area(next.Element("bag"),afterBag)||!Area(next.Element("belt"),afterBelt)||
                !System.Xml.Linq.XNode.DeepEquals(bagLocks,next.Element("bagLocks"))||!System.Xml.Linq.XNode.DeepEquals(prior.Element("beltLocks"),next.Element("beltLocks"))||
                (locks==null?(bool?)bagLocks.Attribute("null")!=true:(bool?)bagLocks.Attribute("null")!=false||bagLocks.Value!=new string(locks.Select(value=>value?'1':'0').ToArray())))return false;
            var oldStation=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,Convert.FromBase64String(prior.Element("stationImage").Value));
            int slot=(int)prior.Attribute("queueSlot");if(slot<0||slot>=oldStation.Queue.Length)return false;
            var row=oldStation.Queue[slot];var marker=row?.Recipe?.ingredients?.FirstOrDefault()?.itemValue;
            if(!RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out var job)||NativeCancellationOperation(job)!=cancellation||
                oldStation.Queue.Count(card=>RebirthCamperStationNativeCrafting.TryJob(card?.Recipe,out var id)&&id==job)!=1||marker==null||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string sourceText)||!Guid.TryParseExact(sourceText,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root?.Phase!=4||root.Binding!=binding||
                (string)root.Copy().Attribute("disposition")!="used"||!TryReadPlan(root,out var plan)||plan.Job!=job||plan.Deaths!=actor.Died||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!MatchesNativePermission(root,plan)||
                plan.InstalledDefinition!=RebirthNativeItemCodec.Encode(installation.InstalledItem.itemValue)||!RebirthCamperStationAddress.TryParse(plan.Address,out var address)||
                runtime.Identity!=address.Vehicle||address.Installation!=station.InstallationId||
                !RebirthStationLiveAccess.TryGetMobileLease(actor,address,plan.Creation,out var current,out _)||!ReferenceEquals(current,station)||
                !TryObservePublication(actor.world,root,out var publication)||(string)root.Copy().Attribute("ownerProof")!=publication||
                RebirthRemoteResourceSourceCustody.HasForeignFetchedVehicleHold(station.BoundVehicle,station,plan.OwnerKey,plan.Source,plan.Binding))return false;
            foreach(var frame in new[]{prior,next})if((int?)frame.Attribute("version")!=2||(int?)frame.Attribute("deaths")!=plan.Deaths||
                (string)frame.Attribute("owner")!=plan.CanonicalOwner||(string)frame.Attribute("ownerKey")!=plan.OwnerKey||(string)frame.Attribute("world")!=plan.World||
                (string)frame.Attribute("station")!=address.Installation.ToString("N")||(int?)frame.Attribute("queueSlot")!=slot||(int?)frame.Attribute("ownedBelt")!=plan.OwnedBelt||(bool?)frame.Attribute("remote")!=true)return false;
            if(!RebirthCamperStationNativeCrafting.RestoreActorImages(row.Recipe)||!RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(row.Recipe))return false;
            var originalRow=plan.RestoreAfterStation().Queue[plan.QueueSlot];
            if(row.RepairItem!=null||row.Multiplier<1||row.Multiplier>originalRow.Multiplier||row.Quality!=originalRow.Quality||
                RebirthRemoteResourceOwnerAction.RecipeImage(row.Recipe)!=RebirthRemoteResourceOwnerAction.RecipeImage(originalRow.Recipe)||
                !RebirthCamperStationNativeCrafting.TryRemainingRefund(row,out var remaining))return false;
            foreach(var fragment in remaining)
            {
                if(!fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.owner",out string origin)||origin!=plan.CanonicalOwner||
                    !fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.source",out string kind)||(kind!="fetch"&&kind!="bag"&&kind!="belt")||
                    !fragment.itemValue.TryGetMetadata("rebirth.camper.native.actor.slot",out int index)||(kind=="fetch"?index!=-1:index<0))return false;
                fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.owner");fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.source");fragment.itemValue.Metadata.Remove("rebirth.camper.native.actor.slot");
            }
            if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,locks,null,plan.OwnedBelt,remaining,out var expectedBag,out var expectedBelt)||!SameArea(expectedBag,afterBag)||!SameArea(expectedBelt,afterBelt))return false;
            // Recreate ONLY the native empty-row delta on a detached model.
            oldStation.Queue[slot]=new RecipeQueueItem{StartingEntityId=-1};oldStation.setModified();
            if(!RebirthCamperStationSnapshot.Capture(address.Installation,oldStation).SequenceEqual(Convert.FromBase64String(next.Element("stationImage").Value)))return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Applied)
            {
                if(!RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(actor.world,cancellation,plan.Address,plan.OwnerKey,plan.Creation,out var savedCancellation)||
                    savedCancellation.OriginalDeaths!=plan.Deaths||savedCancellation.InstalledDefinition!=plan.InstalledDefinition||
                    savedCancellation.CopyBeforeFrame().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)!=prior.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)||
                    !RebirthCamperStationSnapshot.Capture(address.Installation,savedCancellation.RestoreAfter()).SequenceEqual(Convert.FromBase64String(next.Element("stationImage").Value))||
                    !SameArea(savedCancellation.RestoreAfterActor().Bag,afterBag)||!SameArea(savedCancellation.RestoreAfterActor().Belt,afterBelt))return false;
            }
            if(!RebirthCamperEnergySavedOwner.TryRead(owner,out image,out saved)||saved.id!=actor.entityId||saved.deaths!=plan.Deaths||image.Cursor?.IsEmpty()!=true||
                (image.BagLocks==null?locks!=null:locks==null||!image.BagLocks.SequenceEqual(locks)))return false;
            return true;
        }
        catch{image=null;saved=null;return false;}
    }
}
