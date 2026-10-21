using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    // The owner leg retains every actual slot boundary separately. It cannot
    // authorize the station leg or replace its accepted/saved native proof.
    internal sealed partial class OwnerPublication:IDisposable
    {
        internal readonly Original Original;
        private SavedOwnerCheckpoint saved;
        private LoadedOwnerCheckpoint loadedSaved;
        private int SavedPhase=>saved?.Phase??loadedSaved?.Phase??0;
        private int SavedCompleted=>saved?.Completed??loadedSaved?.Completed??-1;
        private bool CheckpointSaved=>saved?.ObservedSaved()==true||loadedSaved?.ObservedSaved()==true;
        private int completed;private bool attempted,observed,unknown,disposed,stationPaid;
        private Guid generation;
        private int initialPhase=1;
        private OwnerPublication(Original original){Original=original;}
        internal bool OwnerSaved=>stationPaid&&!disposed&&!unknown&&Original.Current()&&completed==Original.Slots.Length&&SavedPhase==3&&CheckpointSaved&&
            Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,completed);
        internal bool BeforeCheckpointSaved=>!disposed&&!unknown&&!stationPaid&&completed==0&&SavedPhase==1&&CheckpointSaved&&Original.Current()&&
            Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,0);
        internal bool StationPaid=>stationPaid;
        internal void TrySaveInitialCheckpoint()
        {if(!disposed&&!unknown&&saved==null&&loadedSaved==null&&!attempted&&!observed)SaveCheckpoint(initialPhase);}
        internal SavedOwnerCheckpoint PendingCheckpoint=>saved;
        internal bool ObserveUnusedOwnerBoundary()=>!disposed&&!unknown&&!attempted&&!observed&&!stationPaid&&completed==0&&SavedPhase<=1&&Original.Current()&&
            Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,0);
        internal bool TryObserveStableOwner(PlayerDataFile data,out SavedOwnerCheckpoint checkpoint)
        {
            checkpoint=null;
            if(disposed||unknown||attempted||observed||Original.OwnerLineageCurrent()!=true||saved?.LocalObservedSaved()!=true||
                SavedCompleted!=completed||!MatchesCheckpoint(saved,data)||
                !Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,completed))return false;
            checkpoint=saved;return true;
        }
        internal SavedOwnerCheckpoint ObserveOwnedRemoteRebind()
        {
            if(disposed||unknown||attempted&&!observed||Original.OwnerLineageCurrent()!=true||saved?.LocalObservedSaved()!=true||SavedCompleted!=completed||
                Original.Actor.Buffs.GetCustomVar(Original.Receipt)!=saved.Phase||Original.Actor.Buffs.GetCustomVar(Original.Checkpoint)!=completed||
                !Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,completed))return null;
            return saved;
        }
        internal bool ArmRemotePaid(RemotePaidStation publication)
        {if(!BeforeCheckpointSaved||publication?.Current()!=true||!ReferenceEquals(publication.Source,Original))return false;stationPaid=true;return true;}
        internal bool CanReenterStation(string stage)=>!disposed&&!unknown&&!attempted&&!observed&&Original.OwnerLineageCurrent()&&
            (saved?.ObservedSaved()==true||loadedSaved?.ObservedSavedCustody()==true)&&
            (stage=="after"?stationPaid:stage=="before"&&!stationPaid&&completed==0&&SavedPhase==1)&&
            SavedCompleted==completed&&Original.Actor.Buffs.GetCustomVar(Original.Receipt)==SavedPhase&&
            Original.Actor.Buffs.GetCustomVar(Original.Checkpoint)==completed&&
            Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,completed);
        internal bool TrySaveBefore()
        {
            if(BeforeCheckpointSaved)return true;
            if(disposed||unknown||stationPaid||completed!=0||Original.Current()!=true||
                !Original.MatchesStationStage(false)||
                Original.Actor.Buffs.GetCustomVar(Original.Receipt)!=1f||Original.Actor.Buffs.GetCustomVar(Original.Checkpoint)!=0f||
                !Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,0))return false;
            SaveCheckpoint(1);return BeforeCheckpointSaved;
        }
        internal bool ArmPaidStation(StationPublication publication)
        {
            if(!BeforeCheckpointSaved||!ReferenceEquals(publication?.Original,Original)||!publication.SavedAfter())return false;
            stationPaid=true;return true;
        }
        internal XElement CopySavedEvidence()=>OwnerSaved?(saved!=null?saved.CopySavedEvidence():loadedSaved.CopyEvidence()):null;
        internal static OwnerPublication FromActualCold(Original original,LoadedOwner owner,LoadedStation station,StationPublication publication)
        {
            if(!ReferenceEquals(publication?.Original,original)||!TryClassifyLoadedOwner(owner,station,original,out int phase,out int count,out bool observed))return null;
            if(phase==0)return Begin(original);
            LoadedOwnerCheckpoint checkpoint=observed?null:LoadedOwnerCheckpoint.FromActualLoad(owner,original,phase,count);
            if(!observed&&checkpoint==null)return null;
            var result=new OwnerPublication(original){completed=count,observed=observed,stationPaid=station.Stage=="after",loadedSaved=checkpoint};
            ownerPublications.Add(result);return result;
        }
        internal static OwnerPublication FromActualRemoteHydration(RecyclerClient client,RecyclerReadReentry reentry)
        {
            var joined=client.ColdJoined;var original=client.Source;
            if(joined?.Current()!=true||!ReferenceEquals(client.Reentry,reentry)||!reentry.Returned||!client.MetadataCurrent()||client.Generation==Guid.Empty||
                !(client.ColdPhase==0&&client.ColdCompleted==0&&reentry.Stage=="before"||ValidOwnerCheckpointStage(client.ColdPhase,client.ColdCompleted,original.Slots.Length))||
                !original.MatchesOwnerCheckpoint(joined.Bag,joined.Belt,client.ColdCompleted)||
                original.Actor.Buffs.GetCustomVar(original.Receipt)!=client.ColdPhase||original.Actor.Buffs.GetCustomVar(original.Checkpoint)!=client.ColdCompleted||
                RebirthBackpackLibraryReservation.IsHeld(original.Actor)||RebirthGearOwnerReservation.IsHeld(original.Actor)||RebirthDogLifecycleService.IsHireInventoryHeld(original.Actor)||
                RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(original.Actor)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(joined.Native.Length+original.Image.Length)+65536),out var memory))return null;
            using(memory)
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.WorldKey),
                    new XElement("before",XElement.Parse(System.Text.Encoding.UTF8.GetString(original.Image))),new XElement("recyclerRemoteOwnerReentry",
                        new XAttribute("generation",client.Generation.ToString("N")),new XAttribute("phase",client.ColdPhase),new XAttribute("completed",client.ColdCompleted),
                        new XAttribute("actualJoinHydrated",true),new XAttribute("actualNativeReadReturned",true),new XAttribute("actualWindowSession",reentry.OpeningSession),
                        new XAttribute("hydrationPath",reentry.Path),new XAttribute("hydrationLength",reentry.Length),new XAttribute("hydrationDigest",reentry.Digest),
                        new XElement(reentry.Header),new XElement("nativeJoinedOwner",Convert.ToBase64String(joined.Native))));
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".reentry."+client.Generation.ToString("N")+".original.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!joined.Current())return null;
                var publication=new OwnerPublication(original){completed=client.ColdCompleted,initialPhase=client.ColdPhase==0?1:client.ColdPhase,generation=client.Generation,stationPaid=reentry.Stage=="after"};
                ownerPublications.Add(publication);
                if(client.ColdPhase==0)
                {
                    // Both actual native loaded originals are still entirely
                    // before. Only the owning actor creates its first metadata
                    // checkpoint; no station or item setter is replayed here.
                    try{original.Actor.Buffs.SetCustomVar(original.Receipt,1f);original.Actor.Buffs.SetCustomVar(original.Checkpoint,0f);publication.SaveCheckpoint(1);}
                    catch{publication.unknown=true;}
                }
                return publication;
            }
        }
        internal static OwnerPublication Begin(Original original)
        {
            if(original?.Current()!=true||RebirthCookingOutputDelivery.IsHeld(original.Actor)||
                RebirthCamperStationNativeActorPayment.Blocks(original.Actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(original.Actor.inventory)||
                RebirthBackpackLibraryReservation.IsHeld(original.Actor)||RebirthGearOwnerReservation.IsHeld(original.Actor)||
                RebirthDogLifecycleService.IsHireInventoryHeld(original.Actor)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(original.Actor)||
                !original.MatchesOwnerCheckpoint(original.Actor.bag.ItemGrid.items,original.Actor.inventory.ItemGrid.items,0)||
                !original.MatchesStationStage(false))return null;
            var publication=new OwnerPublication(original);
            var before=XElement.Parse(System.Text.Encoding.UTF8.GetString(original.Image));
            if(!publication.Record(".original",before))return null;
            ownerPublications.Add(publication);
            try
            {
                original.Actor.Buffs.SetCustomVar(original.Receipt,1f);
                original.Actor.Buffs.SetCustomVar(original.Checkpoint,0f);
                publication.SaveCheckpoint(1);
            }
            catch{publication.unknown=true;}
            return publication;
        }
        private bool Record(string suffix,XElement evidence)
        {
            var original=Original;
            var image=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),
                new XAttribute("world",original.WorldKey),new XElement("before",XElement.Parse(System.Text.Encoding.UTF8.GetString(original.Image))),
                new XElement("recyclerOwnerPublication",evidence));
            string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+(generation==Guid.Empty?"":".reentry."+generation.ToString("N"))+suffix+".xml");
            return RebirthWorkstationCompletionPublication.Write(path,image)&&XNode.DeepEquals(image,RebirthWorkstationCompletionPublication.ReadPrimary(path));
        }
        private void SaveCheckpoint(int phase)
        {
            if(saved?.Phase==phase&&saved.Completed==completed&&saved.LocalObservedSaved())return;
            var next=SaveLocalCheckpoint(Original,phase,completed,generation);if(next==null)return;
            var previous=saved;saved=next;previous?.Dispose();
            loadedSaved?.Dispose();loadedSaved=null;
        }
        // A failed native setter is never retried from a mixed postimage. Only
        // the actual final-run slot observation below advances this counter.
        internal void AdvanceOwner()
        {
            if(disposed||unknown||!stationPaid||Original.Current()!=true||!CanAdvanceRemoteRecyclerOwner(Original)||ownerSlotPermit!=null||
                !Original.MatchesStationStage(true))return;
            var actor=Original.Actor;
            if(!Original.MatchesOwnerCheckpoint(actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,completed))return;
            if(observed)
            {
                actor.Buffs.SetCustomVar(Original.Receipt,2f);
                actor.Buffs.SetCustomVar(Original.Checkpoint,completed);
                SaveCheckpoint(2);
                if(SavedCompleted!=completed||SavedPhase!=2||!CheckpointSaved)return;
                observed=false;attempted=false;
            }
            if(saved==null&&loadedSaved==null){SaveCheckpoint(initialPhase);return;}
            if(completed==Original.Slots.Length)
            {
                if(SavedPhase==3&&CheckpointSaved)return;
                actor.Buffs.SetCustomVar(Original.Receipt,3f);SaveCheckpoint(3);return;
            }
            if(!CheckpointSaved)return;
            if(attempted)return;
            var slot=Original.Slots[completed];
            if(!Record(".slot."+completed+".attempted",new XElement("slotAttempt",new XAttribute("ordinal",completed),new XAttribute("area",slot.Area),new XAttribute("slot",slot.Index))))return;
            attempted=true;
            var grid=slot.Area==0?actor.bag.ItemGrid:actor.inventory.ItemGrid;
            var value=(slot.Area==0?Original.AfterBag:Original.AfterBelt)[slot.Index].Clone();
            var permit=new OwnerSlotPermit{Publication=this,Grid=grid,Slot=slot.Index,Value=value,Completed=completed};
            ownerSlotPermit=permit;
            try
            {
                if(slot.Area==0)actor.bag.SetSlot(slot.Index,value);else actor.inventory.SetStackAt(slot.Index,value);
                if(!permit.Consumed||!permit.Observed||!Original.Current()||
                    !Original.MatchesOwnerCheckpoint(actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items,completed+1))
                {unknown=true;return;}
                completed++;observed=true;
            }
            catch
            {
                // A later postfix failure does not undo an already observed
                // final-run native slot. Save that exact postimage; never call
                // its setter again or infer another callback succeeded.
                if(ObservedPostCurrent(permit)){completed++;observed=true;}else unknown=true;
            }
            finally{ownerSlotPermit=null;}
        }
        private bool ObservedPostCurrent(OwnerSlotPermit permit)
        {
            try{return permit.Observed&&Original.Current()&&Original.MatchesStationStage(true)&&
                Original.MatchesOwnerCheckpoint(Original.Actor.bag.ItemGrid.items,Original.Actor.inventory.ItemGrid.items,completed+1);}
            catch{return false;}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;ownerPublications.Remove(this);saved?.Dispose();saved=null;loadedSaved?.Dispose();loadedSaved=null;
            // Original custody is owned by the coupled station controller.
        }
    }
    private sealed class OwnerSlotPermit
    {internal OwnerPublication Publication;internal ItemStackGrid Grid;internal int Slot,Completed;internal ItemStack Value;internal bool Consumed,Observed;}
    [ThreadStatic]private static OwnerSlotPermit ownerSlotPermit;
    private static readonly List<OwnerPublication> ownerPublications=new List<OwnerPublication>();
    internal static bool HoldsOwner(EntityPlayerLocal actor)=>actor!=null&&(RecyclerClientOwnerHeld(actor)||LoadedOwnerHeld(actor)||JoinedRecyclerOwnerHeld(actor)||AuthorityOwnerHeld(actor)||ownerPublications.Any(value=>ReferenceEquals(value.Original.Actor,actor)&&value.Original.Current()));
    internal static bool HoldsStation(TileEntityWorkstation station)=>station!=null&&(ViewedCompletionHeld(station)||LoadedUnusedStationHeld(station)||ClosedAuthorityStationHeld(station)||RecyclerClientStationHeld(station)||RemoteRecyclerStationHeld(station)||ColdStationHeld(station)||AuthorityStationHeld(station)||ownerPublications.Any(value=>
        ReferenceEquals(value.Original.World,GameManager.Instance?.World)&&ReferenceEquals(value.Original.Station,station)&&
        ReferenceEquals(value.Original.World.GetTileEntity(station.ToWorldPos()),station)));
    internal static bool ConsumeOwnerSlot(ItemStackGrid grid,int slot,ItemStack value)
    {
        var permit=ownerSlotPermit;if(permit==null||permit.Consumed||!ReferenceEquals(permit.Grid,grid)||permit.Slot!=slot||!ReferenceEquals(permit.Value,value))return false;
        var original=permit.Publication.Original;
            if(!permit.Publication.StationPaid||!original.Current()||!original.MatchesStationStage(true)||
            !original.MatchesOwnerCheckpoint(original.Actor.bag.ItemGrid.items,original.Actor.inventory.ItemGrid.items,permit.Completed))return false;
        permit.Consumed=true;return true;
    }
    internal static void ObserveOwnerSlot(ItemStackGrid grid,int slot,ItemStack value,bool run)
    {
        try
        {
        var permit=ownerSlotPermit;
        if(!run||permit==null||!permit.Consumed||!ReferenceEquals(permit.Grid,grid)||permit.Slot!=slot||!ReferenceEquals(permit.Value,value))return;
        var original=permit.Publication.Original;
        permit.Observed=permit.Publication.StationPaid&&original.Current()&&original.MatchesStationStage(true)&&
            original.MatchesOwnerCheckpoint(original.Actor.bag.ItemGrid.items,original.Actor.inventory.ItemGrid.items,permit.Completed+1);
        if(!permit.Observed)return;
        original.Actor.Buffs.SetCustomVar(original.Receipt,2f);
        original.Actor.Buffs.SetCustomVar(original.Checkpoint,permit.Completed+1);
        }
        catch{/* Physical proof remains separate from a failed stamp/save. */}
    }
}
