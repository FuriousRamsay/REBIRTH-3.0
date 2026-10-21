using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

// Durable original custom-grid custody. Native bag/belt are witnesses only.
internal static class RebirthCookingRemoteAdmissionBackend
{
    private sealed class Watch:IDisposable
    {internal IDisposable Evidence;internal Func<bool> Published;internal string Queue;internal float Until;public void Dispose(){Evidence?.Dispose();Evidence=null;}}
    private static readonly Dictionary<Guid,Watch> Watches=new Dictionary<Guid,Watch>();
    private static readonly HashSet<Guid> Terminal=new HashSet<Guid>();
    private static World scopeWorld;private static string scopeDirectory;private static bool installed;
    private sealed class Counts{internal long Prepared,Published,GridAdoptions;internal string Token,AdmissionStage,AdmissionOperation;internal TileEntityWorkstation Station;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,Counts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,Counts>();
    internal static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer actor)
    {
        Counts counts=null;if(actor!=null)Audit.TryGetValue(actor,out counts);
        return new Newtonsoft.Json.Linq.JObject{["actor"]=actor?.entityId??-1,["prepared"]=counts?.Prepared??0,["published"]=counts?.Published??0,["gridAdoptions"]=counts?.GridAdoptions??0,["token"]=counts?.Token,
            ["snapshotEvidence"]=Newtonsoft.Json.Linq.JToken.FromObject(RebirthStationSnapshotEvidence.CaptureDiagnostics()),
            ["publicationWatches"]=Watches.Count,["admissionStage"]=counts?.AdmissionStage,["admissionOperation"]=counts?.AdmissionOperation,
            ["protocolOriginals"]=RebirthCookingProtocolAudit.Capture(actor),
            ["ownerDeltaHeld"]=actor is EntityPlayerLocal local&&RebirthCookingOwnerDelta.HasOwnHold(local),
            ["clientHeld"]=RebirthCookingRemoteAdmission.HeldBy(actor),["client"]=RebirthCookingRemoteAdmission.CaptureClient(actor),
            ["nativeMatchingRows"]=counts?.Station?.Queue?.Count(row=>RebirthCookingHeat.Managed(row?.Recipe)&&
                RebirthCookingBatch.Receipt(row.Recipe).TryGetMetadata("rebirth.cooking.token",out string token)&&token==counts.Token)??0};
    }
    internal static void Install()
    {
        if(installed)return;
        var harmony=new Harmony("rebirth.cooking.remote.original-admission.v1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperEnergyDetachedStackRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperEnergyDetachedArrayRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingAdmissionTileHold));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingAdmissionUiHold));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingAdmissionSlotHold));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingAdmissionGestureHold));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingCancellationQueueWrite));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingHiddenOutputWrite));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCookingHiddenOutputNativeBody));
        RebirthCookingGridCustody.Install();
        ModEvents.GameUpdate.RegisterHandler(Tick);installed=true;
    }
    private static void Scope()
    {
        var world=GameManager.Instance?.World;string directory=GameIO.GetSaveGameDir();
        if(ReferenceEquals(scopeWorld,world)&&scopeDirectory==directory)return;
        foreach(var watch in Watches.Values)watch.Dispose();Watches.Clear();Terminal.Clear();scopeWorld=world;scopeDirectory=directory;
    }
    private static RebirthCamperTransferJournal Journal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingAdmissions"));
    private static RebirthCamperTransferJournal Returns()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridReturns"));
    private static IEnumerable<RebirthCamperTransferJournal.Record> Originals()
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingAdmissions");if(!Directory.Exists(directory))yield break;
        foreach(string path in Directory.EnumerateFiles(directory,"*.0.rcj"))
        {if(!Guid.TryParseExact(Path.GetFileName(path).Substring(0,32),"N",out var id))throw new InvalidDataException();yield return Journal().Read(id);}
    }
    private static bool ChildAdmissionAdopted(Guid operation)=>Originals().Any(record=>(record.State!=RebirthCamperTransferJournal.Phase.Rejected||XElement.Parse(Encoding.UTF8.GetString(record.Before)).Element("afterGrid")!=null)&&
        (string)XElement.Parse(Encoding.UTF8.GetString(record.Before)).Attribute("parent")==operation.ToString("N"));
    private static RebirthCamperTransferJournal GridAdoptions()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridAdoptions"));
    private static IEnumerable<RebirthCamperTransferJournal.Record> AdoptionOriginals()
    {
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridAdoptions");if(!Directory.Exists(directory))yield break;
        foreach(string path in Directory.EnumerateFiles(directory,"*.0.rcj"))
        {string name=Path.GetFileName(path);if(name.Length!=38||!Guid.TryParseExact(name.Substring(0,32),"N",out var id))throw new InvalidDataException();yield return GridAdoptions().Read(id);}
    }
    private static bool Adopted(Guid operation)=>ChildAdmissionAdopted(operation)||AdoptionOriginals().Any(record=>record.Vehicle==operation&&record.State!=RebirthCamperTransferJournal.Phase.Rejected);
    // Invoked only after the authenticated owner witness read receipt and complete
    // actor image from one primary native file. The grid is original client UI
    // custody, never a server replica inventory write or client-frozen output.
    internal static bool WitnessGridAdoption(EntityPlayer player,Guid child,int phase,string expectedActor,string originalImage)
    {
        if(originalImage==null||Encoding.UTF8.GetByteCount(originalImage)>8*1024*1024||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        var original=RebirthCookingRemoteAdmission.ReadProtocolXml(originalImage,8*1024*1024);
        if(original.Name!="cookingGridOriginal"||(int?)original.Attribute("version")!=1||
            !Guid.TryParseExact((string)original.Attribute("parentAdmission"),"N",out var parent)||parent==Guid.Empty||parent==child||
            (string)original.Attribute("world")!=player.world.worldState.Guid||(string)original.Attribute("owner")!=identity.CanonicalId||
            (string)original.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(player)||(int?)original.Attribute("deaths")!=player.Died||
            original.Element(phase==1?"beforeActor":"afterActor")?.Value!=expectedActor)return false;
        string location=(string)original.Attribute("location"),creation=(string)original.Attribute("creation");if(location==null||location.Length>1024)return false;
        var source=Journal().Read(parent);if(source==null||source.OwnerCreation!=creation||
            (source.State!=RebirthCamperTransferJournal.Phase.Applied&&source.State!=RebirthCamperTransferJournal.Phase.Rejected))return false;
        var sourceRoot=XElement.Parse(Encoding.UTF8.GetString(source.Before));
        if((string)sourceRoot.Attribute("world")!=player.world.worldState.Guid||(string)sourceRoot.Attribute("owner")!=identity.StorageKey||(string)sourceRoot.Attribute("location")!=location)return false;
        var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Element("beforeGrid").Value));
        var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Element("afterGrid").Value));
        if(before.Length!=12||after.Length!=12||!Contains(before,RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Owed(source,sourceRoot)))))return false;
        if(ChildAdmissionAdopted(parent)||Returns().FindPending(parent)!=null||Returns().FindApplied(parent,creation).Any()||
            AdoptionOriginals().Any(record=>record.Vehicle==parent&&record.Operation!=child&&record.State!=RebirthCamperTransferJournal.Phase.Rejected))return false;
        var journal=GridAdoptions();var retained=journal.Read(child);
        // The accepted parent already owns this unpaid UI escrow for this exact
        // owner. This handoff has no station effects: closing/reconnecting must
        // not require reopening a TE lease merely to prove that owner's delta.
        // Initial queue admission still requires the exact native station lease.
        if(retained==null&&phase!=1)return false;
        var envelope=new XElement("gridAdoption",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),new XAttribute("owner",identity.CanonicalId),
            new XAttribute("creation",creation),new XAttribute("location",location),new XAttribute("parent",parent.ToString("N")),new XAttribute("child",child.ToString("N")),new XElement("original",originalImage));
        byte[] bytes=Encoding.UTF8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));
        // Prepare compares the immutable complete original on every phase/retry.
        retained=journal.Prepare(child,parent,creation,0,bytes,bytes);
        if(retained.State==RebirthCamperTransferJournal.Phase.Prepared)retained=journal.Advance(child,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        if(phase==2&&retained.State==RebirthCamperTransferJournal.Phase.Uncertain)
        {retained=journal.Advance(child,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);Audit.GetValue(player,_=>new Counts()).GridAdoptions++;}
        return phase==1?(retained.State==RebirthCamperTransferJournal.Phase.Uncertain||retained.State==RebirthCamperTransferJournal.Phase.Applied):retained.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    internal static bool Contains(ItemStack[] source,ItemStack[] owed)
    {
        Dictionary<string,long> Totals(ItemStack[] values)
        {
            var totals=new Dictionary<string,long>(StringComparer.Ordinal);
            foreach(var item in values)
            {
                if(item==null||item.count<0)throw new InvalidDataException();if(item.IsEmpty())continue;
                string image=RebirthNativeItemCodec.Encode(item.itemValue);totals.TryGetValue(image,out long count);totals[image]=checked(count+item.count);
            }
            return totals;
        }
        var available=Totals(source);return Totals(owed).All(pair=>available.TryGetValue(pair.Key,out long count)&&count>=pair.Value);
    }
    internal static Guid StationKey(string world,string location)
    {using(var hash=System.Security.Cryptography.SHA256.Create())return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(world+"|"+location)).Take(16).ToArray());}
    internal static string Queue(RecipeQueueItem[] rows)
    {
        if(rows==null||rows.Length<1||rows.Length>255)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);writer.Write((byte)rows.Length);foreach(var row in rows){if(row==null)throw new InvalidDataException();row.Write(writer);}writer.Flush();if(stream.Length>1024*1024)throw new InvalidDataException();return Convert.ToBase64String(stream.ToArray());}
    }
    internal static RecipeQueueItem[] ReadQueue(string image)
    {
        if(image==null||image.Length>1400000)throw new InvalidDataException();
        using(var stream=new MemoryStream(Convert.FromBase64String(image),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {reader.SetBaseStream(stream);int count=reader.ReadByte();if(count<1)throw new InvalidDataException();var rows=new RecipeQueueItem[count];for(int i=0;i<count;i++){rows[i]=new RecipeQueueItem();rows[i].Read(reader);}if(stream.Position!=stream.Length||Queue(rows)!=image)throw new InvalidDataException();return rows;}
    }
    private static string Grid(ItemStack[] values)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(values));
    private static string Owed(RebirthCamperTransferJournal.Record record,XElement root)=>
        root.Element(record.State==RebirthCamperTransferJournal.Phase.Rejected?"beforeGrid":"afterGrid")?.Value;
    internal static ItemStack[] PaidInputs(Guid token,string world,string creation,string location,string canonicalOwner)
    {
        var record=Journal().Read(token);if(record==null)return null;
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||record.OwnerCreation!=creation||(string)root.Attribute("world")!=world||
            (string)root.Attribute("location")!=location||(string)root.Attribute("canonicalOwner")!=canonicalOwner)throw new InvalidDataException();
        var before=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("beforeGrid").Value));
        var after=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("afterGrid").Value));
        if(before.Length!=12||after.Length!=12)throw new InvalidDataException();var paid=new List<ItemStack>();
        for(int i=0;i<12;i++)
        {
            int count=before[i].count-after[i].count;if(count<0||(!after[i].IsEmpty()&&RebirthNativeItemCodec.Encode(before[i].itemValue)!=RebirthNativeItemCodec.Encode(after[i].itemValue)))throw new InvalidDataException();
            if(count>0)paid.Add(new ItemStack(before[i].itemValue.Clone(),count));
        }
        return paid.ToArray();
    }
    private static string Refused(RebirthCamperTransferJournal.Record record,XElement root)
    {
        var response=new XElement(root);response.SetElementValue("afterGrid",Owed(record,root));
        return "refused-grid:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(response.ToString(SaveOptions.DisableFormatting)));
    }
    internal static bool Current(EntityPlayer player,string location,string creation,out TileEntityWorkstation station,out RebirthWorldCharacterRecord record)
    {
        station=null;record=null;
        if(!ThreadManager.IsMainThread()||player?.world?.IsRemote()!=false)return false;
        if(RebirthCamperStationAddress.TryParse(location,out var mobile))return RebirthStationLiveAccess.TryResolveMobile(player,mobile,creation,out station,out record);
        return RebirthCookingJobRuntime.TryPosition(location,out var position)&&RebirthStationLiveAccess.TryResolve(player,position,creation,out station,out record);
    }
    internal static bool Phase(RebirthStablePlayerIdentity identity,string receipt,out float phase)
    {return NativePhase(identity,receipt,out phase,out bool present)&&present;}
    internal static bool NativePhase(RebirthStablePlayerIdentity identity,string receipt,out float phase,out bool present)
    {phase=0f;present=false;return RebirthGearNativePlayerFile.TryRead(identity,out var saved)&&NativePhase(saved,receipt,out phase,out present);}
    internal static bool NativePhase(PlayerDataFile saved,string receipt,out float phase,out bool present)
    {
        phase=0f;present=false;if(saved?.buffData==null||saved.buffData.Length>1024*1024)return false;
        using(var stream=new MemoryStream(saved.buffData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
            int count=reader.ReadUInt16();for(int i=0;i<count;i++)new BuffValue().Read(reader,version);
            count=reader.ReadUInt16();bool found=false;
            for(int i=0;i<count;i++){string key=reader.ReadString();float value=reader.ReadSingle();if(key!=receipt)continue;if(found||float.IsNaN(value)||float.IsInfinity(value))return false;phase=value;found=true;}
            present=found;return stream.Position==stream.Length;
        }
    }
    internal static string Reject(EntityPlayer player,string location,string creation,Guid operation,string reason,XElement input)
    {
        if(!Current(player,location,creation,out _,out _)||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return RebirthCookingRemoteAdmission.Pending;
        var journal=Journal();var record=journal.Read(operation);
        if(record==null)
        {
            if(!Phase(identity,RebirthCookingRemoteAdmission.Receipt(operation),out float unspent)||unspent!=1f)return RebirthCookingRemoteAdmission.Pending;
            string image=input?.Element("grid")?.Value;if(image==null||image.Length>4*1024*1024)return RebirthCookingRemoteAdmission.Pending;
            var grid=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image));if(grid.Length!=12||Grid(grid)!=image)return RebirthCookingRemoteAdmission.Pending;
            var root=new XElement("cookingOriginal",new XAttribute("version",1),new XAttribute("world",player.world.worldState.Guid),
                new XAttribute("location",location),new XAttribute("creation",creation),new XAttribute("owner",identity.StorageKey),new XAttribute("canonicalOwner",identity.CanonicalId),
                new XAttribute("operation",operation.ToString("N")),new XAttribute("refusedCustody",true),new XAttribute("reason",reason),new XElement("beforeGrid",image),new XElement("afterGrid",image));
            if(Guid.TryParseExact((string)input.Attribute("parent"),"N",out var parentId))
            {
                var parent=journal.Read(parentId);if(parent==null||Adopted(parentId)||Returns().FindPending(parentId)!=null||Returns().FindApplied(parentId,creation).Any())return RebirthCookingRemoteAdmission.Pending;
                var parentRoot=XElement.Parse(Encoding.UTF8.GetString(parent.Before));
                if((parent.State!=RebirthCamperTransferJournal.Phase.Applied&&(parent.State!=RebirthCamperTransferJournal.Phase.Rejected||parentRoot.Element("afterGrid")==null))||
                    parent.OwnerCreation!=creation||(string)parentRoot.Attribute("owner")!=identity.StorageKey||(string)parentRoot.Attribute("world")!=player.world.worldState.Guid||
                    (string)parentRoot.Attribute("location")!=location||Owed(parent,parentRoot)==null||!Contains(grid,RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Owed(parent,parentRoot)))))return RebirthCookingRemoteAdmission.Pending;
                root.SetAttributeValue("parent",parentId.ToString("N"));
            }
            byte[] payload=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));
            journal.Prepare(operation,StationKey(player.world.worldState.Guid,location),creation,0,payload,payload);
            record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
        }
        return record.State==RebirthCamperTransferJournal.Phase.Rejected?Refused(record,XElement.Parse(Encoding.UTF8.GetString(record.Before))):RebirthCookingRemoteAdmission.Pending;
    }
    internal static bool Begin(RebirthCookingRemoteAdmission.AdmissionPlan plan)
    {
        Scope();
        int enteredDeaths=plan?.Player?.Died??-1;var enteredWorld=plan?.Player?.world;var enteredNetwork=plan?.Player==null?null:RebirthRemoteResourceNetworkScope.Capture(plan.Player);
        if(plan?.Player==null)return false;
        var attempt=Audit.GetOrCreateValue(plan.Player);attempt.AdmissionOperation=plan.Operation.ToString("N");
        // Diagnostic stage only: every existing authority/payment guard remains
        // mandatory and in its original order. No receipt or item state changes.
        attempt.AdmissionStage="station-lease";
        if(!Current(plan.Player,plan.Location,plan.Creation,out var station,out var owner)||!ReferenceEquals(station,plan.Station))return false;
        attempt.AdmissionStage="saved-owner-phase";
        if(!Phase(plan.Identity,plan.OwnerReceipt,out float phase)||phase!=1f)return false;
        attempt.AdmissionStage="owner-identity";
        if(!RebirthWorldCharacterService.TryGetIdentity(plan.Player,out var identity)||identity.StorageKey!=plan.Identity.StorageKey)return false;
        attempt.AdmissionStage="saved-owner-source";
        if(!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks)||
            RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,plan.Player.inventory.Length)!=plan.SourceImageHash)return false;
        attempt.AdmissionStage="station-pending-original";
        if(Journal().FindPending(StationKey(plan.Player.world.worldState.Guid,plan.Location))!=null)return false;
        attempt.AdmissionStage="saved-station-birth";
        if(!RebirthCookingHiddenOutputRecovery.TryStationBirth(plan.Player,station,plan.Location,plan.Creation,out string birth))return false;
        attempt.AdmissionStage="native-network-scope";
        if(enteredNetwork?.Current!=true||plan.Player.Died!=enteredDeaths||!ReferenceEquals(plan.Player.world,enteredWorld)||!ReferenceEquals(GameManager.Instance?.World,enteredWorld))return false;
        attempt.AdmissionStage="queue-slot";
        string prior=Queue(station.Queue);if(!station.Queue.Any(row=>row!=null&&row.Recipe==null))return false;
        string next=Queue(plan.Queue);string key=plan.Operation.ToString("N");
        attempt.AdmissionStage="parent-original";
        if(plan.ParentOperation!=Guid.Empty)
        {
            var parent=Journal().Read(plan.ParentOperation);if(parent==null||Adopted(plan.ParentOperation)||Returns().FindPending(plan.ParentOperation)!=null||Returns().FindApplied(plan.ParentOperation,plan.Creation).Any())return false;
            var parentImage=XElement.Parse(Encoding.UTF8.GetString(parent.Before));
            if((parent.State!=RebirthCamperTransferJournal.Phase.Applied&&(parent.State!=RebirthCamperTransferJournal.Phase.Rejected||parentImage.Element("afterGrid")==null))||
                (string)parentImage.Attribute("owner")!=identity.StorageKey||(string)parentImage.Attribute("world")!=plan.Player.world.worldState.Guid||
                (string)parentImage.Attribute("location")!=plan.Location||parent.OwnerCreation!=plan.Creation||
                !Contains(plan.GridBefore,RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Owed(parent,parentImage)))))return false;
        }
        attempt.AdmissionStage="saved-cooking-ticket";
        if(!owner.Progression.Cooking.Tickets.TryGetValue(key,out var ticket)||ticket.StationPosition!=plan.Location||ticket.Recipe!=plan.Recipe.GetName()||ticket.Portions!=plan.Batches||ticket.OutputCount!=plan.Recipe.count)return false;
        var root=new XElement("cookingOriginal",new XAttribute("version",1),new XAttribute("world",plan.Player.world.worldState.Guid),new XAttribute("owner",identity.StorageKey),
            new XAttribute("canonicalOwner",identity.CanonicalId),new XAttribute("creation",plan.Creation),new XAttribute("location",plan.Location),new XAttribute("operation",key),
            new XAttribute("source",plan.SourceImageHash),new XAttribute("birth",birth),new XAttribute("deaths",enteredDeaths),new XAttribute("block",station is RebirthCamperStationState?"mobile":station.block?.GetBlockName()??""),
            new XElement("beforeQueue",prior),new XElement("afterQueue",next),new XElement("beforeGrid",Grid(plan.GridBefore)),new XElement("afterGrid",Grid(plan.GridAfter)));
        if(plan.ParentOperation!=Guid.Empty)root.SetAttributeValue("parent",plan.ParentOperation.ToString("N"));
        attempt.AdmissionStage="original-payload";
        byte[] payload=Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting));if(payload.Length>4*1024*1024)return false;
        var journal=Journal();var stationKey=StationKey(plan.Player.world.worldState.Guid,plan.Location);journal.AssertAvailable(stationKey,plan.Operation);
        attempt.AdmissionStage="existing-original";
        var existing=journal.Read(plan.Operation);if(existing!=null)return false;
        attempt.AdmissionStage="resource-source-custody";
        if(RebirthRemoteResourceSourceCustody.BlocksStation(station))return false;
        attempt.AdmissionStage="prepare-original";
        journal.Prepare(plan.Operation,stationKey,plan.Creation,0,payload,payload);
        var counts=Audit.GetOrCreateValue(plan.Player);counts.Prepared++;counts.Token=key;counts.Station=station;
        // This original invocation alone owns the initial native queue assignment.
        if(enteredNetwork?.Current!=true||plan.Player.Died!=enteredDeaths||!ReferenceEquals(plan.Player.world,enteredWorld)||
            !Current(plan.Player,plan.Location,plan.Creation,out var final,out var finalOwner)||!ReferenceEquals(final,station)||!ReferenceEquals(finalOwner,owner)||
            !RebirthCookingHiddenOutputRecovery.StationBirthMatches(plan.Player,station,plan.Location,root)||Queue(station.Queue)!=prior)return true;
        attempt.AdmissionStage="native-queue-publication";
        station.Queue=ReadQueue(next);station.lastTickTime=0;station.ResetTickTime();station.setModified();
        Observe(plan.Player,plan.Location,plan.Creation,plan.Operation);return true;
    }
    internal static bool Published(Guid operation,EntityPlayer actor,TileEntityWorkstation station,string expected,Func<bool> current,Recipe retainedRecipe=null)
    {
        if(Watches.TryGetValue(operation,out var watch)&&(watch.Queue!=expected||UnityEngine.Time.realtimeSinceStartup>=watch.Until))
        {watch.Dispose();Watches.Remove(operation);watch=null;}
        if(watch==null)
        {
            var recipe=retainedRecipe??station.Queue.Select(row=>row.Recipe).FirstOrDefault(value=>RebirthCookingHeat.Managed(value));
            if(recipe==null)return false;
            var evidence=RebirthCookingOutputPlanPublication.BeginAdmissionPublication(actor,station,recipe,current,out var observe);
            if(evidence==null)return false;
            watch=new Watch{Evidence=evidence,Published=observe,Queue=expected,Until=UnityEngine.Time.realtimeSinceStartup+25f};Watches[operation]=watch;
        }
        return current()&&watch.Published();
    }
    internal static string Observe(EntityPlayer player,string location,string creation,Guid operation)
    {
        Scope();var journal=Journal();var record=journal.Read(operation);if(record==null)return null;
        var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));
        if((string)root.Attribute("world")!=player.world.worldState.Guid||(string)root.Attribute("location")!=location||record.OwnerCreation!=creation||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity.StorageKey!=(string)root.Attribute("owner")||identity.CanonicalId!=(string)root.Attribute("canonicalOwner"))return RebirthCookingRemoteAdmission.Pending;
        if(record.State==RebirthCamperTransferJournal.Phase.Applied){Terminal.Add(operation);return RebirthCookingRemoteAdmission.Accepted;}
        if(record.State==RebirthCamperTransferJournal.Phase.Rejected)return root.Element("beforeGrid")!=null?Refused(record,root):RebirthCookingRemoteAdmission.Pending;
        if(!Current(player,location,creation,out var station,out var owner))return RebirthCookingRemoteAdmission.Pending;
        string before=root.Element("beforeQueue").Value,paused=root.Element("afterQueue").Value;
        if(!RebirthCookingHiddenOutputRecovery.StationBirthMatches(player,station,location,root)||
            station is not RebirthCamperStationState&&station.block?.GetBlockName()!=(string)root.Attribute("block"))return RebirthCookingRemoteAdmission.Pending;
        string live=Queue(station.Queue);
        // Earlier UI versions advanced the native row before payment finished.
        // Keep that exact original's progress/Take plan, but still require the
        // original saved queue, grid debit and native owner phase below.
        if(live!=paused&&RebirthCookingAdmissionProgress.TryMatch(paused,live,operation,
            record.State==RebirthCamperTransferJournal.Phase.Uncertain,out string progressed))paused=progressed;
        bool Still(string expected)=>Current(player,location,creation,out var current,out var currentOwner)&&ReferenceEquals(current,station)&&ReferenceEquals(currentOwner,owner)&&
            RebirthCookingHiddenOutputRecovery.StationBirthMatches(player,station,location,root)&&Queue(station.Queue)==expected;
        bool originalPresent=station.Queue.Any(row=>RebirthCookingHeat.Managed(row?.Recipe)&&row.Recipe.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string own)&&own==operation.ToString("N"));
        if(live==before||record.State==RebirthCamperTransferJournal.Phase.Prepared&&!originalPresent)
        {
            // Never replay an uncertain initial assignment into a replacement TE.
            // Refusal publishes the exact current empty image first; only a saved
            // unspent owner receipt can then recover the original complete grid.
            if(record.State!=RebirthCamperTransferJournal.Phase.Prepared||!Phase(identity,RebirthCookingRemoteAdmission.Receipt(operation),out float unspent)||unspent!=1f)
                return RebirthCookingRemoteAdmission.Pending;
            var retained=ReadQueue(paused).Select(row=>row.Recipe).FirstOrDefault(value=>RebirthCookingHeat.Managed(value)&&value.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string own)&&own==operation.ToString("N"));
            if(!Published(operation,player,station,live,()=>Still(live),retained))return RebirthCookingRemoteAdmission.Pending;
            record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);
            if(Watches.TryGetValue(operation,out var refusedWatch)){refusedWatch.Dispose();Watches.Remove(operation);}
            return Refused(record,root);
        }
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
        {
            if(live!=paused||!Published(operation,player,station,paused,()=>Still(paused)))return RebirthCookingRemoteAdmission.Pending;
            record=journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
            if(Watches.TryGetValue(operation,out var old)){old.Dispose();Watches.Remove(operation);}
        }
        if(!Phase(identity,RebirthCookingRemoteAdmission.Receipt(operation),out float phase)||phase==1f)return "clear:"+operation.ToString("N");
        if(phase!=2f&&phase!=3f)return RebirthCookingRemoteAdmission.Pending;
        var finalQueue=ReadQueue(paused);foreach(var row in finalQueue)if(RebirthCookingHeat.Managed(row.Recipe))row.Recipe.ingredients[0].itemValue.SetMetadata(RebirthCookingRemoteAdmission.PendingKey,2);
        string committed=Queue(finalQueue);
        if(live==paused){if(!Still(paused))return RebirthCookingRemoteAdmission.Pending;station.Queue=finalQueue;station.lastTickTime=0;station.ResetTickTime();station.setModified();live=committed;}
        if(live!=committed||!Published(operation,player,station,committed,()=>Still(committed)))return RebirthCookingRemoteAdmission.Pending;
        journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        Audit.GetOrCreateValue(player).Published++;
        Terminal.Add(operation);if(Watches.TryGetValue(operation,out var done)){done.Dispose();Watches.Remove(operation);}
        var admitted=station.Queue.Select(row=>row.Recipe).FirstOrDefault(value=>RebirthCookingHeat.Managed(value)&&value.ingredients[0].itemValue.TryGetMetadata("rebirth.cooking.queue.token",out string own)&&own==operation.ToString("N"));
        if(admitted!=null&&RebirthCookingBatch.NeedsHeat(admitted))station.IsBurning=true;
        station.ResetTickTime();station.setModified();return RebirthCookingRemoteAdmission.Accepted;
    }
    internal static string Recovery(EntityPlayer player,string creation,string location)
    {
        try
        {
            Install();Scope();if(!Current(player,location,creation,out _,out _))return "";
            var stationKey=StationKey(player.world.worldState.Guid,location);
            var original=Journal().FindPending(stationKey);
            if(original==null)
            {
                var residual=Originals().Where(record=>
                {
                    if(record.Vehicle!=stationKey||record.OwnerCreation!=creation||
                        (record.State!=RebirthCamperTransferJournal.Phase.Applied&&record.State!=RebirthCamperTransferJournal.Phase.Rejected)||
                        Adopted(record.Operation)||Returns().FindApplied(record.Operation,creation).Any())return false;
                    var image=XElement.Parse(Encoding.UTF8.GetString(record.Before));
                    return image.Element("afterGrid")!=null&&
                        RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Owed(record,image))).Any(item=>!item.IsEmpty());
                }).ToArray();
                if(residual.Length!=1)return "";original=residual[0];
            }
            var root=XElement.Parse(Encoding.UTF8.GetString(original.Before));
            if(original.OwnerCreation!=creation||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity.StorageKey!=(string)root.Attribute("owner"))return "";
            bool terminal=original.State==RebirthCamperTransferJournal.Phase.Applied||original.State==RebirthCamperTransferJournal.Phase.Rejected;
            if(!Phase(identity,RebirthCookingRemoteAdmission.Receipt(original.Operation),out float phase)||(phase!=1f&&phase!=2f&&phase!=3f))return "";
            var response=new XElement(root);response.SetElementValue("afterGrid",Owed(original,root));response.SetAttributeValue("admissionApplied",original.State==RebirthCamperTransferJournal.Phase.Applied||original.State==RebirthCamperTransferJournal.Phase.Rejected);
            response.SetAttributeValue("admissionRefused",original.State==RebirthCamperTransferJournal.Phase.Rejected);
            response.SetAttributeValue("ownerReceiptPhase",terminal?(original.State==RebirthCamperTransferJournal.Phase.Rejected?1:2):(int)phase);
            response.SetAttributeValue("returnPending",Returns().FindPending(original.Operation)!=null);
            return "recover:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(response.ToString(SaveOptions.DisableFormatting)));
        }
        catch{return "";}
    }
    // Returning this owner's already admitted unpaid grid changes no station
    // state. Closing its native UI must not revoke the saved entitlement.
    private static bool ReturnOwnerCurrent(EntityPlayer player,string creation)=>ThreadManager.IsMainThread()&&player?.world?.IsRemote()==false&&
        ReferenceEquals(GameManager.Instance?.World,player.world)&&ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
        player.IsSpawned()&&!player.IsDead()&&creation==RebirthCamperCharacterAccess.CurrentCreation(player);
    internal static string ReturnGrid(EntityPlayer player,string creation,string location,ItemValue carrier,string expectedPlan="")
    {
        try
        {
            Install();Scope();if(!ReturnOwnerCurrent(player,creation)||!RebirthCookingRemoteAdmission.TryOperation(carrier,out var originalId)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return RebirthCookingRemoteAdmission.Pending;
            var original=Journal().Read(originalId);if(original==null||(original.State!=RebirthCamperTransferJournal.Phase.Applied&&original.State!=RebirthCamperTransferJournal.Phase.Rejected)||Adopted(originalId))return RebirthCookingRemoteAdmission.Pending;
            var root=XElement.Parse(Encoding.UTF8.GetString(original.Before));
            if(original.OwnerCreation!=creation||(string)root.Attribute("world")!=player.world.worldState.Guid||(string)root.Attribute("owner")!=identity.StorageKey||
                (string)root.Attribute("location")!=location)return RebirthCookingRemoteAdmission.Pending;
            var journal=Returns();var terminal=journal.FindApplied(originalId,creation).ToArray();if(terminal.Length>1)throw new InvalidDataException();
            if(terminal.Length==1)return "returned:"+Convert.ToBase64String(terminal[0].Before);
            if(!string.IsNullOrEmpty(expectedPlan))
            {
                if(!Guid.TryParseExact(expectedPlan,"N",out var expected))return RebirthCookingRemoteAdmission.Pending;
                var retained=journal.Read(expected);
                if(retained==null||retained.OwnerCreation!=creation||retained.Vehicle!=originalId)return RebirthCookingRemoteAdmission.Pending;
                if(retained.State==RebirthCamperTransferJournal.Phase.Rejected)
                {
                    // The client still names this exact original. Even when
                    // its saved rejection is visible now, return that original
                    // once more so its client can retire the hint before a
                    // replacement plan is issued.
                    RebirthCookingOwnerDelta.ObserveRejected(identity,retained);
                    return "retry-return:"+Convert.ToBase64String(retained.Before);
                }
            }
            var record=journal.FindPending(originalId);
            RebirthCookingOwnerDelta.Plan plan;
            if(record==null)
            {
                foreach(var rejected in journal.FindRejected(originalId,creation))
                    if(!RebirthCookingOwnerDelta.ObserveRejected(identity,rejected))return "retry-return:"+Convert.ToBase64String(rejected.Before);
                var outputs=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(Owed(original,root))).Where(item=>!item.IsEmpty()).ToList();
                if(outputs.Count==0)return "";
                if(!RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var locks))return RebirthCookingRemoteAdmission.Pending;
                if(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(bag,belt,locks,null,player.inventory.Length,outputs,out var afterBag,out var afterBelt,out var overflow))return RebirthCookingRemoteAdmission.Pending;
                plan=new RebirthCookingOwnerDelta.Plan{Operation=Guid.NewGuid(),World=player.world.worldState.Guid,Creation=creation,Owner=identity.CanonicalId,Deaths=player.Died,
                    SourceHash=RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,player.inventory.Length),BeforeBag=bag,AfterBag=afterBag,BeforeBelt=belt,AfterBelt=afterBelt,
                    Overflow=overflow.Length==0?null:overflow,OverflowOwnerEntity=player.entityId};
                byte[] payload=Encoding.UTF8.GetBytes(RebirthCookingOwnerDelta.Encode(plan));journal.AssertAvailable(originalId,plan.Operation);
                record=journal.Prepare(plan.Operation,originalId,creation,0,payload,payload);
            }
            else plan=RebirthCookingOwnerDelta.Decode(Encoding.UTF8.GetString(record.Before));
            if(plan.Owner!=identity.CanonicalId||plan.Creation!=creation||plan.World!=player.world.worldState.Guid||plan.Deaths!=player.Died)return RebirthCookingRemoteAdmission.Pending;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)
            {
                if(RebirthCookingOwnerDelta.Saved(identity,plan,1))record=journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
                else if(RebirthCookingOwnerDelta.TryReadUnspentSource(identity,plan,out var bag,out var belt,out var locks))
                {
                    if(RebirthCookingRemoteAdmission.SourceHash(bag,belt,locks,player.inventory.Length)!=plan.SourceHash)
                    {journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Rejected);return "retry-return:"+Convert.ToBase64String(record.Before);}
                }
            }
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain&&RebirthCookingOwnerDelta.Saved(identity,plan,2))
            {
                // Keep the return pending (and unavailable for adoption) until
                // every original remainder has positive native saved evidence.
                // Uncertain alone proves admission, never owner credit/drop.
                var enteredWorld=player.world;string enteredSave=GameIO.GetSaveGameDir();byte[] sourceImage=(byte[])original.Before.Clone();
                bool SourceCurrent()
                {
                    if(!ReferenceEquals(player.world,enteredWorld)||!ReferenceEquals(GameManager.Instance?.World,enteredWorld)||GameIO.GetSaveGameDir()!=enteredSave||
                        !ReturnOwnerCurrent(player,creation)||!RebirthWorldCharacterService.TryGetIdentity(player,out var currentOwner)||currentOwner.CanonicalId!=plan.Owner||Adopted(originalId))return false;
                    var source=Journal().Read(originalId);var returned=Returns().Read(record.Operation);
                    return source!=null&&source.OwnerCreation==creation&&source.State==original.State&&source.Before.SequenceEqual(sourceImage)&&source.After.SequenceEqual(original.After)&&
                        returned!=null&&returned.Vehicle==originalId&&returned.OwnerCreation==creation&&returned.State==RebirthCamperTransferJournal.Phase.Uncertain&&returned.Before.SequenceEqual(record.Before)&&returned.After.SequenceEqual(record.After);
                }
                if(plan.Overflow!=null&&(!SourceCurrent()||!RebirthCookingOverflowOriginal.Advance(record,player,identity.CanonicalId,creation,plan.Overflow,plan.Deaths,plan.OverflowOwnerEntity,SourceCurrent)))return RebirthCookingRemoteAdmission.Pending;
                record=journal.Advance(record.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);return "returned:"+Convert.ToBase64String(record.Before);
            }
            return "return:"+(record.State==RebirthCamperTransferJournal.Phase.Uncertain?"1:":"0:")+Convert.ToBase64String(record.Before);
        }
        catch(Exception error){Log.Warning("[Cooking] Original grid return retained: "+error.GetType().Name);return RebirthCookingRemoteAdmission.Pending;}
    }
    internal static bool Held(TileEntityWorkstation station)
    {
        try{return HeldCore(station);}
        catch(Exception error){Log.Warning("[Cooking] Admission authority retained: "+error.GetType().Name);return true;}
    }
    private static bool HeldCore(TileEntityWorkstation station)
    {
        if(station==null)return true;
        if(RebirthCookingHiddenOutputRecovery.Held(station))return true;
        if(RebirthCookingRemoteCancellation.Held(station))return true;
        if(GameManager.Instance?.World?.IsRemote()==true)
            return (station.Queue??Array.Empty<RecipeQueueItem>()).Any(row=>RebirthCookingHeat.Managed(row?.Recipe)&&row.Recipe.ingredients[0].itemValue.TryGetMetadata(RebirthCookingRemoteAdmission.PendingKey,out int phase)&&phase==1);
        Scope();foreach(var row in station.Queue??Array.Empty<RecipeQueueItem>())
        {
            if(!RebirthCookingHeat.Managed(row?.Recipe)||!row.Recipe.ingredients[0].itemValue.HasMetadata(RebirthCookingRemoteAdmission.PendingKey))continue;
            if(!RebirthCookingBatch.Receipt(row.Recipe).TryGetMetadata("rebirth.cooking.token",out string token)||!Guid.TryParse(token,out var operation))return true;
            if(!Terminal.Contains(operation))
            {var record=Journal().Read(operation);if(record?.State==RebirthCamperTransferJournal.Phase.Applied)Terminal.Add(operation);else return true;}
        }
        return false;
    }
    private static void Tick(ref ModEvents.SGameUpdateData data){RebirthCookingOverflowOriginal.Tick();RebirthCookingHiddenOutputRecovery.TickServer();RebirthCookingHiddenOutputRecovery.TickClients();RebirthCookingRemoteAdmission.TickClients();RebirthCookingRemoteCancellation.TickClients();}
}

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthCookingAdmissionTileHold
{[HarmonyPriority(Priority.First)]private static bool Prefix(TileEntityWorkstation __instance)
    =>__instance is RebirthCamperStationState mobile&&mobile.block is BlockCampfire&&
        mobile.BoundVehicle?.world?.IsRemote()==false||!RebirthCookingRemoteAdmissionBackend.Held(__instance);}
[HarmonyPatch(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))]
internal static class RebirthCookingAdmissionUiHold
{
    [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_RecipeStack __instance)
    {
        var station=__instance.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity;
        if(station is RebirthCamperStationState mobile&&mobile.block is BlockCampfire&&
            mobile.BoundVehicle?.world?.IsRemote()==false)return true;
        return !RebirthCookingHiddenOutputRecovery.Held(station)&&(!RebirthCookingHeat.Managed(__instance.recipe)||
            (!RebirthCookingRemoteAdmissionBackend.Held(station)&&!RebirthCookingRemoteCancellation.PresentationHeld(station)));
    }
}
[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetRecipeQueueItems))]
internal static class RebirthCookingCancellationQueueWrite
{
    [HarmonyPriority(Priority.First)]private static bool Prefix(XUiM_Workstation __instance,RecipeQueueItem[] __0)
    {
        var station=__instance?.TileEntity as TileEntityWorkstation;
        if(station is RebirthCamperStationState mobile&&mobile.block is BlockCampfire&&
            mobile.BoundVehicle?.world?.IsRemote()==false)return true;
        if(RebirthCookingHiddenOutputRecovery.Held(station))return false;
        if(station==null||!(station.Queue?.Any(row=>RebirthCookingHeat.Managed(row?.Recipe))==true||__0?.Any(row=>RebirthCookingHeat.Managed(row?.Recipe))==true))return true;
        if(RebirthCookingRemoteAdmissionBackend.Held(station)||RebirthCookingRemoteCancellation.PresentationHeld(station))return false;
        var world=GameManager.Instance?.World;var player=world?.GetPrimaryPlayer();
        return ThreadManager.IsMainThread()&&player!=null&&ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.IsSpawned()&&!player.IsDead();
    }
}
[HarmonyPatch(typeof(XUiM_Workstation),nameof(XUiM_Workstation.SetOutputStacks))]
internal static class RebirthCookingHiddenOutputWrite
{[HarmonyPriority(Priority.First)]private static bool Prefix(XUiM_Workstation __instance)=>!RebirthCookingHiddenOutputRecovery.Held(__instance?.TileEntity as TileEntityWorkstation);}
[HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage))]
internal static class RebirthCookingHiddenOutputNativeBody
{
    // The packet body is already detached by native read. Refuse its whole
    // server application before SetHandle/read/NotifyListeners; do not deserialize
    // a rejected FromClient body into the paid source merely to consume it.
    [HarmonyPriority(Priority.First)]private static bool Prefix(NetPackageTileEntity __instance,World _world)
        =>_world==null||_world.IsRemote()||!RebirthCookingHiddenOutputRecovery.ServerHeld(_world.GetTileEntity(__instance.teWorldPos) as TileEntityWorkstation);
}
[HarmonyPatch(typeof(XUiC_RebirthCookingSlot),nameof(XUiC_RebirthCookingSlot.CanSwap))]
internal static class RebirthCookingAdmissionSlotHold
{private static bool Prefix(XUiC_RebirthCookingSlot __instance,ref bool __result){var workspace=__instance.GetParentByType<XUiC_RebirthCookingWorkspace>();if(!RebirthCookingRemoteAdmission.IsHeld(workspace))return true;__result=false;return false;}}
[HarmonyPatch]
internal static class RebirthCookingAdmissionGestureHold
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {foreach(string name in new[]{"HandleStackSwap","HandlePartialStackPickup","HandleDropOne","HandleMoveToPreferredLocation","HandleClickComplete"})yield return AccessTools.Method(typeof(XUiC_ItemStack),name)??throw new MissingMethodException(typeof(XUiC_ItemStack).FullName,name);}
    private static bool Prefix(XUiC_ItemStack __instance)=>!(__instance is XUiC_RebirthCookingSlot slot)||
        !RebirthCookingRemoteAdmission.IsHeld(slot.GetParentByType<XUiC_RebirthCookingWorkspace>());
}

