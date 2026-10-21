using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthCamperStationContentsDelivery
{
    private sealed class OwnerDelivery:IDisposable
    {
        internal EntityPlayerLocal Actor;internal World World;internal string Save,Creation,Source,Owner;internal Guid Operation,Vehicle,Installation;
        internal int Deaths,Entity;internal RebirthCamperRemovalParcelPlan Plan;internal float Next;internal string Nonce=Guid.NewGuid().ToString("N");
        internal bool Current=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&ReferenceEquals(Actor?.world,World)&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(Actor.entityId),Actor)&&Actor.IsSpawned()&&!Actor.IsDead()&&
            Actor.entityId==Entity&&Actor.Died==Deaths&&GameIO.GetSaveGameDir()==Save&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(Actor,out var identity)&&identity.StorageKey==Owner;
        internal IDisposable Allocation;
        public void Dispose(){Allocation?.Dispose();Allocation=null;Plan=null;Actor=null;World=null;}
    }
    private sealed class ParcelPermit{internal OwnerDelivery Owner;internal ItemStackGrid Grid;internal int Slot;internal ItemStack Value;internal bool Used,Cursor;}
    private static readonly Dictionary<Guid,OwnerDelivery> parcelOwners=new Dictionary<Guid,OwnerDelivery>();
    private const string ParcelHeld="rbRemovalParcelHeld",ParcelHeldLife="rbRemovalParcelHeldLife";
    private static float parcelScanAt;
    [ThreadStatic]private static ParcelPermit parcelPermit;
    private static bool parcelDeliveryInstalled;
    internal static void InstallParcelDelivery()
    {
        if(parcelDeliveryInstalled)return;
        if(GameManager.Instance?.World!=null)throw new InvalidOperationException("Removal parcel protocol must install before World creation.");
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        RebirthCamperRemovalParcelOverflow.Install();ModEvents.GameUpdate.RegisterHandler(UpdateParcelOwners);parcelDeliveryInstalled=true;
    }
    internal static bool HoldsOwner(EntityPlayerLocal player)=>player?.Buffs!=null&&ReferenceEquals(GameManager.Instance?.World,player.world)&&
        ReferenceEquals(player.world?.GetPrimaryPlayer(),player)&&!player.IsDead()&&
        (player.Buffs.GetCustomVar(ParcelHeld)==1f&&player.Buffs.GetCustomVar(ParcelHeldLife)==player.Died||
        parcelOwners.Values.Any(value=>ReferenceEquals(value.Actor,player)&&value.Current));
    internal static bool ConsumeOwnerSlot(ItemStackGrid grid,int slot,ItemStack value)
    {
        var permit=parcelPermit;if(permit==null||permit.Cursor||permit.Used||!permit.Owner.Current||!ReferenceEquals(permit.Grid,grid)||permit.Slot!=slot||!ReferenceEquals(permit.Value,value))return false;
        permit.Used=true;return true;
    }
    internal static bool AllowOwnerCursor(XUiC_DragAndDropWindow cursor,ItemStack value)
    {
        var permit=parcelPermit;if(permit==null||!permit.Cursor||permit.Used||!permit.Owner.Current||
            !ReferenceEquals(permit.Owner.Actor.PlayerUI?.xui?.DragAndDropWindow,cursor)||!ReferenceEquals(permit.Value,value))return false;
        permit.Used=true;return true;
    }
    private static RebirthCamperTransferJournal OwnerJournal()=>new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOwnerOriginals"));
    private static bool StartParcelOwner(EntityPlayerLocal actor,RebirthCamperTransferSnapshot before,RebirthCamperTransferSnapshot after)
    {
        OwnerDelivery reserved=null;bool admitted=false;
        try
        {
            if(!parcelDeliveryInstalled||actor==null||before==null||after==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RebirthCamperPortableStation.TryGetSourceAddress(after.Cursor,out var address))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor),world=actor.world.worldState.Guid;
            string source=RebirthStationPublicationRecord.Digest(before.Capture())+RebirthStationPublicationRecord.Digest(after.Capture());
            Guid operation=RebirthCamperRemovalParcelPlan.Key(world,source,identity.StorageKey,creation);
            if(parcelOwners.TryGetValue(operation,out var prior))return prior.Current&&prior.Source==source;
            bool retainedInitial=actor.Buffs.GetCustomVar(ParcelHeld)==1f&&actor.Buffs.GetCustomVar(ParcelHeldLife)==actor.Died&&
                actor.Buffs.GetCustomVar("rbRemovalParcel_"+operation.ToString("N"))==1f;
            if(parcelOwners.Values.Any(value=>ReferenceEquals(value.Actor,actor)&&value.Current)||parcelOwners.Count>=32||
                !retainedInitial&&(RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(actor)||RebirthCookingOutputDelivery.IsHeld(actor))||
                RebirthDogLifecycleService.IsHireInventoryHeld(actor)||RebirthCamperEnergyOwnerHold.Blocks(actor.bag)||
                RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthGearOwnerReservation.IsHeld(actor)||RebirthBackpackLibraryReservation.IsHeld(actor))return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out var allocation))return false;
            var value=reserved=new OwnerDelivery{Actor=actor,World=actor.world,Save=GameIO.GetSaveGameDir(),Creation=creation,Source=source,Owner=identity.StorageKey,
                Operation=operation,Vehicle=address.Vehicle,Installation=address.Installation,Deaths=actor.Died,Entity=actor.entityId,Allocation=allocation};
            var retained=OwnerJournal().Read(operation);
            if(retained==null&&!PreflightParcelCapacity(actor,after.Cursor))return false;
            if(retained!=null)
            {
                if(retained.Before.Length>24*1024*1024||retained.Vehicle!=address.Vehicle||retained.OwnerCreation!=creation||!retained.Before.SequenceEqual(retained.After)||
                    !RebirthCamperRemovalParcelPlan.TryRead(RebirthCookingRemoteAdmission.ReadProtocolXml(new System.Text.UTF8Encoding(false,true).GetString(retained.Before),24*1024*1024),out var plan)||
                    plan.Operation!=operation||plan.Source!=source||plan.Owner!=identity.StorageKey||plan.Creation!=creation||plan.Deaths!=actor.Died)return false;
                value.Plan=plan;
            }
            if(!value.Current)return false;parcelOwners.Add(operation,value);admitted=true;
            if(retained==null)
            {
                // Publish exact initial owner/carrier before asking the host
                // to derive the removal plan from its actual applied source.
                actor.Buffs.SetCustomVar("rbRemovalParcel_"+operation.ToString("N"),1f,true);PublishParcelOwner(value);
            }
            actor.Buffs.SetCustomVar(ParcelHeld,1f,true);actor.Buffs.SetCustomVar(ParcelHeldLife,actor.Died,true);PublishParcelOwner(value);
            PumpParcelOwner(value);return true;
        }
        catch{return false;}
        finally{if(!admitted)reserved?.Dispose();}
    }
    private static bool PreflightParcelCapacity(EntityPlayerLocal actor,ItemStack carrier)
    {
        var before=RebirthCamperEnergyActorImage.Capture(actor);var locks=(bool[])before.BagLocks.Clone();
        int matches=RebirthStationGridIngredients.IsSameStackSnapshot(before.Cursor,carrier)?1:0;
        for(int slot=0;slot<before.Bag.Length;slot++)if(RebirthStationGridIngredients.IsSameStackSnapshot(before.Bag[slot],carrier)){matches++;locks[slot]=true;}
        return matches==1&&RebirthCamperStationRemovalContents.TryRead(carrier,out var contents)&&
            RebirthRecyclerOwnerDelivery.PlanWithOverflow(before.Bag,before.Belt,locks,new bool[before.Belt.Length],0,contents,out _,out _,out var overflow)&&
            RebirthCamperRemovalParcelOverflow.CanFit(actor,overflow);
    }
    private static void PublishParcelOwner(OwnerDelivery value)
    {
        if(!value.Current)return;GameManager.Instance.SaveLocalPlayerData();
        if(value.World.IsRemote())GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
    }
    private static bool SavedParcelOwner(OwnerDelivery value,out RebirthCamperEnergyActorImage actual,out float phase)
    {
        actual=null;phase=float.NaN;
        return value.Current&&RebirthStablePlayerIdentity.TryResolveServerEntity(value.Actor,out var identity)&&
            RebirthCamperEnergySavedOwner.TryRead(identity,out actual,out var saved)&&saved.id==value.Actor.entityId&&saved.deaths==value.Deaths&&
            RebirthCookingRemoteAdmissionBackend.NativePhase(saved,"rbRemovalParcel_"+value.Operation.ToString("N"),out phase,out bool present)&&present&&value.Current;
    }
    private static RebirthCamperEnergyActorImage Prefix(RebirthCamperRemovalParcelPlan plan,int count)
    {
        var frame=plan.Before.Copy();var slots=Enumerable.Range(0,frame.Bag.Length).Where(slot=>!RebirthStationGridIngredients.IsSameStackSnapshot(plan.Before.Bag[slot],plan.Credited.Bag[slot])).ToArray();
        if(count<0||count>slots.Length)throw new InvalidDataException();
        for(int index=0;index<count;index++)frame.Bag[slots[index]]=plan.Credited.Bag[slots[index]].Clone();return frame;
    }
    private static bool SavedParcelFrame(RebirthCamperRemovalParcelPlan plan,RebirthCamperEnergyActorImage frame,float phase)
    {
        try
        {
            // Retired500 is query-only: Pump asks for the existing host's
            // saved overflow result before advancing the marker. It never
            // resumes a credit setter from this frame.
            if(phase==500f)return plan.Credited.Matches(frame)||plan.Retired.Matches(frame);
            if(phase==1000f)return plan.Retired.Matches(frame);
            if(phase==1f)return plan.Before.Matches(frame);
            return phase==Math.Floor(phase)&&phase>=11f&&Prefix(plan,(int)phase-10).Matches(frame);
        }
        catch{return false;}
    }
    private static bool ApplyParcelOwnerPrefix(OwnerDelivery value)
    {
        var plan=value.Plan;if(plan==null||!SavedParcelOwner(value,out var saved,out float phase))return false;
        if(phase==500f)return plan.Credited.Matches(saved);
        int count=phase==1f?0:(int)phase-10;
        var slots=Enumerable.Range(0,plan.Before.Bag.Length).Where(slot=>!RebirthStationGridIngredients.IsSameStackSnapshot(plan.Before.Bag[slot],plan.Credited.Bag[slot])).ToArray();
        if(phase!=1f&&(phase!=Math.Floor(phase)||count<1)||count<0||count>slots.Length)return false;
        var before=Prefix(plan,count);if(!before.Matches(saved))return false;
        if(count==slots.Length)
        {
            if(!before.Matches(RebirthCamperEnergyActorImage.Capture(value.Actor)))return false;
            value.Actor.Buffs.SetCustomVar(plan.Receipt,500f,true);PublishParcelOwner(value);return false;
        }
        var after=Prefix(plan,count+1);var live=RebirthCamperEnergyActorImage.Capture(value.Actor);
        // A returned native setter may have failed to save. Finish only its
        // exact already-present next prefix, without reissuing that setter.
        if(after.Matches(live)&&value.Actor.Buffs.GetCustomVar(plan.Receipt)==11+count)
        {PublishParcelOwner(value);return false;}
        if(!before.Matches(live))return false;
        Guid child=RebirthCookingRemoteAdmissionBackend.StationKey(plan.World,plan.Operation.ToString("N")+"|owner-slot|"+count);
        var journal=new RebirthCamperTransferJournal(Path.Combine(value.Save,"REBIRTH","RemovalParcelOwnerSlots"));
        byte[] b=RebirthCamperRemovalParcelPlan.Bytes(new XElement("removalParcelOwnerSlot",new XAttribute("binding",plan.Binding),new XAttribute("index",count),
            new XElement("before",RebirthRemoteResourceClientOriginal.ActorImage(before)),new XElement("after",RebirthRemoteResourceClientOriginal.ActorImage(after))));
        var record=journal.Prepare(child,plan.Vehicle,plan.Creation,0,b,b);
        if(record.State!=RebirthCamperTransferJournal.Phase.Prepared)return false; // Unknown entered setter never repeats on a matching shape.
        journal.Advance(child,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(!value.Current||!before.Matches(RebirthCamperEnergyActorImage.Capture(value.Actor)))return false;
        int slot=slots[count];var desired=after.Bag[slot].Clone();var permit=new ParcelPermit{Owner=value,Grid=value.Actor.bag.ItemGrid,Slot=slot,Value=desired};
        if(parcelPermit!=null)return false;parcelPermit=permit;
        try{value.Actor.bag.SetSlot(slot,desired);}finally{parcelPermit=null;}
        if(!permit.Used||!value.Current||!after.Matches(RebirthCamperEnergyActorImage.Capture(value.Actor)))return false;
        value.Actor.Buffs.SetCustomVar(plan.Receipt,11+count,true);PublishParcelOwner(value);return false;
    }
    private static void PumpParcelOwner(OwnerDelivery value)
    {
        if(!value.Current||UnityEngine.Time.realtimeSinceStartup<value.Next)return;
        value.Next=UnityEngine.Time.realtimeSinceStartup+0.25f;
        int stage=0;
        if(value.Plan!=null)
        {
            if(SavedParcelOwner(value,out var retired,out float phase)&&phase==1000f&&value.Plan.Retired.Matches(retired)){SendParcelRetired(value);return;}
            bool retiredBeforeMarker=phase==500f&&value.Plan.Retired.Matches(retired);
            if(!retiredBeforeMarker&&!ApplyParcelOwnerPrefix(value))return;stage=1;
        }
        var request=new XElement("removalParcelRequest",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),
            new XAttribute("source",value.Source),new XAttribute("vehicle",value.Vehicle.ToString("N")),new XAttribute("installation",value.Installation.ToString("N")),
            new XAttribute("creation",value.Creation),new XAttribute("nonce",value.Nonce),new XAttribute("stage",stage));
        RebirthCamperRemovalParcelTransport.Send(value.Actor,value.Operation,request);
    }
    internal static void AcceptParcelResult(EntityPlayerLocal actor,Guid operation,XElement result)
    {
        try
        {
            if(!parcelOwners.TryGetValue(operation,out var value)||!ReferenceEquals(actor,value.Actor)||!value.Current||
                result?.Name!="removalParcelResult"||result.Attributes().Count()!=7||result.Elements().Count()!=1||(int?)result.Attribute("version")!=1||
                (string)result.Attribute("operation")!=operation.ToString("N")||(string)result.Attribute("nonce")!=value.Nonce||
                !RebirthCamperRemovalParcelPlan.TryRead(result.Element("removalParcelPlan"),out var plan)||plan.Operation!=operation||plan.Source!=value.Source||
                plan.Owner!=value.Owner||plan.Creation!=value.Creation||plan.World!=actor.world.worldState.Guid||plan.Deaths!=value.Deaths||
                (string)result.Attribute("binding")!=plan.Binding||(string)result.Attribute("source")!=value.Source)return;
            int status=(int)result.Attribute("status");string proof=(string)result.Attribute("proof");
            if(status<0||status>2||proof==null||status>0&&(proof.Length!=64||!proof.All(Uri.IsHexDigit)))return;
            if(value.Plan==null)
            {
                if(status!=0||!plan.Before.Matches(RebirthCamperEnergyActorImage.Capture(actor))||
                    !SavedParcelOwner(value,out var native,out float phase)||phase!=1f||!plan.Before.Matches(native))return;
                byte[] bytes=RebirthCamperRemovalParcelPlan.Bytes(plan.Write());
                var record=OwnerJournal().Prepare(operation,plan.Vehicle,plan.Creation,0,bytes,bytes);
                if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return;
                value.Plan=plan;
            }
            else if(value.Plan.Binding!=plan.Binding)return;
            if(status==0){value.Next=0;PumpParcelOwner(value);return;}
            if(status==1)
            {
                if(!SavedParcelOwner(value,out var saved,out float phase))return;
                if(phase==1000f&&plan.Retired.Matches(saved)){SendParcelRetired(value);return;}
                if(phase!=500f||!plan.Credited.Matches(saved)&&!plan.Retired.Matches(saved))return;
                var live=RebirthCamperEnergyActorImage.Capture(actor);
                float livePhase=actor.Buffs.GetCustomVar(plan.Receipt);
                if(plan.Retired.Matches(live)&&(livePhase==500f||livePhase==1000f))
                {actor.Buffs.SetCustomVar(plan.Receipt,1000f,true);PublishParcelOwner(value);return;}
                if(!plan.Credited.Matches(saved)||!plan.Credited.Matches(live)||parcelPermit!=null)return;
                var desired=(plan.CarrierSlot<0?plan.Retired.Cursor:plan.Retired.Bag[plan.CarrierSlot]).Clone();
                var permit=new ParcelPermit{Owner=value,Grid=actor.bag.ItemGrid,Slot=plan.CarrierSlot,Value=desired,Cursor=plan.CarrierSlot<0};parcelPermit=permit;
                try{if(permit.Cursor)actor.PlayerUI.xui.DragAndDropWindow.SetCurrentStack(desired,false);else actor.bag.SetSlot(plan.CarrierSlot,desired);}
                finally{parcelPermit=null;}
                if(!permit.Used||!value.Current||!plan.Retired.Matches(RebirthCamperEnergyActorImage.Capture(actor)))return;
                actor.Buffs.SetCustomVar(plan.Receipt,1000f,true);PublishParcelOwner(value);SendParcelRetired(value);return;
            }
            if(!SavedParcelOwner(value,out var retired,out float terminalPhase)||terminalPhase!=1000f||!plan.Retired.Matches(retired))return;
            var journal=OwnerJournal();var terminal=journal.Read(operation);
            if(terminal?.State==RebirthCamperTransferJournal.Phase.Prepared)terminal=journal.Advance(operation,terminal.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(terminal?.State==RebirthCamperTransferJournal.Phase.Uncertain)terminal=journal.Advance(operation,terminal.State,RebirthCamperTransferJournal.Phase.Applied);
            if(terminal?.State==RebirthCamperTransferJournal.Phase.Applied)
            {actor.Buffs.SetCustomVar(ParcelHeld,0f,true);PublishParcelOwner(value);parcelOwners.Remove(operation);value.Dispose();}
        }
        catch{/* Native/transport ambiguity retains original and its owner hold. */}
    }
    private static void SendParcelRetired(OwnerDelivery value)
    {
        if(!value.Current)return;
        var request=new XElement("removalParcelRequest",new XAttribute("version",1),new XAttribute("operation",value.Operation.ToString("N")),
            new XAttribute("source",value.Source),new XAttribute("vehicle",value.Vehicle.ToString("N")),new XAttribute("installation",value.Installation.ToString("N")),
            new XAttribute("creation",value.Creation),new XAttribute("nonce",value.Nonce),new XAttribute("stage",2));
        RebirthCamperRemovalParcelTransport.Send(value.Actor,value.Operation,request);
    }
    private static void UpdateParcelOwners(ref ModEvents.SGameUpdateData data)
    {
        RebirthCamperRemovalParcelOverflow.ReleaseStaleObservations();
        ScanSavedParcelOwner();
        if(parcelOwners.Count!=0)
            foreach(var pair in parcelOwners.ToArray())
            {if(!pair.Value.Current){parcelOwners.Remove(pair.Key);pair.Value.Dispose();continue;}try{PumpParcelOwner(pair.Value);}catch{}}
        try{RebirthCamperRemovalParcelTransport.Tick();}catch{/* Retry only unchanged transport fragments. */}
    }
    private static void ScanSavedParcelOwner()
    {
        var actor=GameManager.Instance?.World?.GetPrimaryPlayer();
        if(actor==null||actor.IsDead()||!actor.IsSpawned()||UnityEngine.Time.realtimeSinceStartup<parcelScanAt||
            parcelOwners.Values.Any(value=>ReferenceEquals(value.Actor,actor)&&value.Current))return;
        parcelScanAt=UnityEngine.Time.realtimeSinceStartup+1f;
        IDisposable allocation=null;OwnerDelivery candidate=null;
        try
        {
            string path=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOwnerOriginals");
            if(!Directory.Exists(path))return;
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out allocation)||
                !RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=actor.Died||saved.buffData==null||saved.buffData.Length>1024*1024)return;
            var operations=new List<Guid>();
            using(var stream=new MemoryStream(saved.buffData.ToArray(),false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return;
                int count=reader.ReadUInt16();for(int index=0;index<count;index++)new BuffValue().Read(reader,version);
                count=reader.ReadUInt16();
                for(int index=0;index<count;index++)
                {
                    string name=reader.ReadString();float phase=reader.ReadSingle();
                    if(name.StartsWith("rbRemovalParcel_",StringComparison.Ordinal)&&name.Length==48&&phase>=1f&&
                        Guid.TryParseExact(name.Substring(16),"N",out var operation)&&!operations.Contains(operation))operations.Add(operation);
                }
                if(stream.Position!=stream.Length)return;
            }
            foreach(var operation in operations)
            {
                var record=RebirthCamperTransferJournal.ReadExisting(path,operation);
                if(record==null||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After)||
                    !RebirthCamperRemovalParcelPlan.TryRead(RebirthCookingRemoteAdmission.ReadProtocolXml(new System.Text.UTF8Encoding(false,true).GetString(record.Before),24*1024*1024),out var plan)||
                    plan.Owner!=identity.StorageKey||plan.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||plan.World!=actor.world.worldState.Guid)continue;
                if(record.State==RebirthCamperTransferJournal.Phase.Applied)
                {
                    if(actor.Buffs.GetCustomVar(plan.Receipt)==1000f&&actor.Buffs.GetCustomVar(ParcelHeld)==1f)
                    {actor.Buffs.SetCustomVar(ParcelHeld,0f,true);GameManager.Instance.SaveLocalPlayerData();}
                    continue;
                }
                if(plan.Deaths!=actor.Died||!RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float retainedPhase,out bool retainedPresent)||
                    !retainedPresent||!SavedParcelFrame(plan,actual,retainedPhase)||candidate!=null)return;
                candidate=new OwnerDelivery{Actor=actor,World=actor.world,Save=GameIO.GetSaveGameDir(),Creation=plan.Creation,Source=plan.Source,Owner=plan.Owner,
                    Operation=operation,Vehicle=plan.Vehicle,Installation=plan.Installation,Deaths=plan.Deaths,Entity=actor.entityId,Plan=plan};
            }
            if(candidate==null||!candidate.Current)return;
            candidate.Allocation=allocation;allocation=null;parcelOwners.Add(candidate.Operation,candidate);candidate=null;
        }
        catch{/* Unreadable/ambiguous retained originals never create a new credit. */}
        finally{allocation?.Dispose();candidate?.Dispose();}
    }
}
