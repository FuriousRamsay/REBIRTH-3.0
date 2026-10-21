using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

// Observes actual native deferred consumption only. It never calls GiveExp,
// removes a receipt, or saves/reconstructs a player on behalf of native code.
internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal sealed class DeferredWitness:IDisposable
    {
        internal RebirthProductiveEarnedUiCoordinator.Original Original;
        internal RebirthWorkstationCompletionPipeline.NativeAddOriginal NativeOriginal;
        internal RebirthProductiveEarnedUiDeferredTransport.ImportedContribution ImportedOriginal;
        internal RebirthProductiveEarnedUiDeferredTransport.HostLocalContribution HostLocalOriginal;
        internal RebirthProductiveEarnedUiQueueOwner.DeferredReceipt Receipt;
        internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal World World;internal TileEntityWorkstation Station;internal Guid Operation;
        internal string Owner,Creation,PlayerRoot,PlayerName;internal int EntityId;
        internal byte[] Before,After,Final;internal string FinalPath;
        internal byte[] BagBefore,BeltBefore,BagAfter,BeltAfter,EquipmentBefore,EquipmentAfter;
        internal System.Xml.Linq.XElement Delivery;
        internal EntityPlayerLocal Actor;internal int Deaths;
        internal bool Delivered,Consumed,Completed,Closed,OwnerTerminalSaved;internal IDisposable Allocation,SourceRetention,NativeAllocation,DeliveryAllocation;
        internal SharedCapture nativeShared;
        private int localReferences;private bool disposeRequested;
        internal IDisposable RetainLocalOwnerObservation()
        {lock(gate){if(Closed||disposeRequested||Receipt==null)return null;var receipt=Receipt.RetainLocalOwnerObservation();if(receipt==null)return null;localReferences++;return new LocalObservationLease(this,receipt);}}
        private sealed class LocalObservationLease:IDisposable
        {private DeferredWitness source;private IDisposable receipt;internal LocalObservationLease(DeferredWitness value,IDisposable receipt){source=value;this.receipt=receipt;}
         public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref source,null);if(value==null)return;lock(gate){receipt.Dispose();receipt=null;if(--value.localReferences==0&&value.disposeRequested)value.Dispose();}}}
        internal bool Current()
        {
            try{return !Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
                ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&(Receipt==null?EarlierOwner?.Current()==true:Receipt.CurrentSourceWithoutRow())&&
                (Actor==null||ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&ReferenceEquals(World.GetEntity(EntityId),Actor)&&
                 !Actor.IsDead()&&Actor.Died==Deaths&&RebirthCamperCharacterAccess.CurrentCreation(Actor)==Creation&&
                 RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&identity.StorageKey==Owner&&identity.CanonicalId==PlayerName);}
            catch{return false;}
        }
        internal bool ObserveDelivery(RebirthRecipeOutputIntegration.OwningDelivery delivery)
        {
            if(Closed||delivery?.IsCurrent()!=true||delivery.Operation!=Operation||!ReferenceEquals(delivery.Station,Station)||
                delivery.StartingOwnerEntityId!=EarlierOwner.StartingEntityId)return false;
            Delivered=true;if(!delivery.TryCopyEvidence(out var evidence))return true;RetainDelivery(evidence);return true;
        }
        internal bool RetainDelivery(System.Xml.Linq.XElement evidence)
        {
            if(Delivery!=null)return true;long bytes=System.Text.Encoding.UTF8.GetByteCount(evidence.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8*bytes+65536),out var allocation))return false;
            Delivery=evidence;DeliveryAllocation=allocation;return true;
        }
        internal bool Saved(){lock(gate)return Delivered&&Current()&&Consumed&&Completed&&After!=null&&Final!=null&&ReadFinal(FinalPath,Final);}
        internal bool ObservedSavedEvent(){lock(gate)return Delivered&&Current()&&Consumed&&Completed&&After!=null&&Final!=null&&FinalPath!=null;}
        public void Dispose(){lock(gate){if(Closed)return;disposeRequested=true;if(localReferences>0)return;Closed=true;Before=After=Final=BagBefore=BeltBefore=BagAfter=BeltAfter=EquipmentBefore=EquipmentAfter=null;Delivery=null;Receipt=null;EarlierOwner=null;Original=null;NativeOriginal=null;ImportedOriginal=null;HostLocalOriginal=null;Actor=null;Station=null;World=null;DeliveryAllocation?.Dispose();DeliveryAllocation=null;NativeAllocation?.Dispose();NativeAllocation=null;nativeShared=null;Allocation?.Dispose();Allocation=null;SourceRetention?.Dispose();SourceRetention=null;deferred.Remove(this);}}
    }
    internal sealed class Consumption
    {
        internal Consumption Previous;internal DeferredWitness Witness;internal DeferredWitness[] Members;internal EntityPlayerLocal Actor;
        internal CraftCompleteData[] Rows;internal byte[][] Images;internal byte[] Bag,Belt,Equipment;
        internal readonly HashSet<CraftCompleteData> Awarded=new HashSet<CraftCompleteData>();internal bool Unknown;
    }
    private static readonly List<DeferredWitness> deferred=new List<DeferredWitness>();
    internal sealed class SharedCapture
    {
        private IDisposable allocation;private int owners,workers;
        internal SharedCapture(IDisposable allocation,int owners){this.allocation=allocation;this.owners=owners;}
        internal IDisposable Owner()=>new Charge(this);
        internal IDisposable Retain(){lock(this){if(allocation==null||owners<1||workers>=2)return null;owners++;workers++;return new Charge(this,true);}}
        private sealed class Charge:IDisposable
        {private SharedCapture parent;private readonly bool worker;internal Charge(SharedCapture parent,bool worker=false){this.parent=parent;this.worker=worker;}
         public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref parent,null);if(value==null)return;lock(value){if(worker)value.workers--;if(--value.owners==0){value.allocation.Dispose();value.allocation=null;}}}}
    }
    [ThreadStatic]private static Consumption consuming;
    internal sealed class DeferredConsumptionEvent
    {
        private readonly Consumption source;
        internal DeferredConsumptionEvent(Consumption value){source=value;}
        internal DeferredWitness[] Members=>source.Members.ToArray();
        internal bool Current()=>ReferenceEquals(consuming,source)&&!source.Unknown&&source.Members.All(value=>value.Consumed&&value.Current())&&
            source.Awarded.Count==source.Members.Select(value=>value.Receipt.Receipt).Distinct().Count();
    }
    private static void InstallDeferred(Harmony harmony)
    {foreach(var type in new[]{typeof(DeferredCheck),typeof(DeferredGive)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);}
    internal static DeferredWitness BeginDeferred(RebirthProductiveEarnedUiCoordinator.Original original,RebirthProductiveEarnedUiQueueOwner.Witness owner)
    {
        if(!ThreadManager.IsMainThread()||original?.Binding==null||owner?.Current()!=true||!owner.SourcePublished()||
            !ReferenceEquals(original.Binding.Station,owner.Station)||original.Binding.Card.StartingEntityId!=owner.StartingEntityId)return null;
        lock(gate)
        {
            if(deferred.Count>=4||deferred.Any(value=>value.Operation==original.Binding.Operation)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16*1024,out var allocation))return null;
            var sourceRetention=owner.RetainForCompletion();if(sourceRetention==null){allocation.Dispose();return null;}
            var result=new DeferredWitness{Original=original,EarlierOwner=owner,World=owner.World,Station=owner.Station,Operation=original.Binding.Operation,
                Owner=owner.OriginalOwnerKey,Creation=owner.Creation,EntityId=owner.StartingEntityId,Allocation=allocation,SourceRetention=sourceRetention};
            deferred.Add(result);return result;
        }
    }
    internal static bool AttachReceipt(RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt)
    {
        if(!ThreadManager.IsMainThread()||receipt?.CurrentSourceWithoutRow()!=true)return false;
        lock(gate)
        {
            var target=deferred.SingleOrDefault(value=>!value.Closed&&(receipt.Original!=null?ReferenceEquals(value.Original,receipt.Original):
                receipt.NativeOriginal!=null?ReferenceEquals(value.NativeOriginal,receipt.NativeOriginal):receipt.Imported!=null?ReferenceEquals(value.ImportedOriginal,receipt.Imported):
                receipt.HostLocal!=null&&ReferenceEquals(value.HostLocalOriginal,receipt.HostLocal)));
            if(target==null&&receipt.NativeOriginal!=null)target=BeginDeferredNative(receipt.NativeOriginal);
            if(target==null&&receipt.Imported!=null)target=BeginDeferredImported(receipt);
            if(target==null&&receipt.HostLocal!=null)target=BeginDeferredHostLocal(receipt);
            if(target==null||target.Receipt!=null||target.Owner!=receipt.Source.OriginalOwnerKey||target.Creation!=receipt.Source.Creation||
                target.EntityId!=receipt.Source.StartingEntityId||!ReferenceEquals(target.Station,receipt.Source.Station)||
                receipt.RecipientEntityId!=receipt.Receipt.CrafterEntityID)return false;
            target.Receipt=receipt;target.Owner=receipt.RecipientOwnerKey;target.Creation=receipt.RecipientCreation;target.EntityId=receipt.RecipientEntityId;return true;
        }
    }
    internal static DeferredWitness BeginDeferredNative(RebirthWorkstationCompletionPipeline.NativeAddOriginal original)
    {
        if(original?.SourceCurrentWithoutRow()!=true)return null;
        lock(gate)
        {
            if(deferred.Count>=4||deferred.Any(value=>value.Operation==original.Operation)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(16*1024,out var allocation))return null;
            var retention=original.RetainForCompletion();if(retention==null){allocation.Dispose();return null;}
            var owner=original.EarlierOwner;
            var result=new DeferredWitness{NativeOriginal=original,EarlierOwner=owner,World=original.World,Station=original.Station,Operation=original.Operation,
                Owner=owner.OriginalOwnerKey,Creation=owner.Creation,EntityId=owner.StartingEntityId,Allocation=allocation,SourceRetention=retention};
            deferred.Add(result);return result;
        }
    }
    private static DeferredWitness BeginDeferredImported(RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt)
    {
        var imported=receipt.Imported;var original=imported?.Original;
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(original)||deferred.Count>=4||
            deferred.Any(value=>value.Operation==imported.Operation)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return null;
        var allocation=imported.RetainForCompletion();var retention=imported.RetainForCompletion();
        if(allocation==null||retention==null){allocation?.Dispose();retention?.Dispose();return null;}
        var result=new DeferredWitness{ImportedOriginal=imported,EarlierOwner=receipt.Source,World=original.World,Station=original.Station,Operation=imported.Operation,
            Owner=receipt.Source.OriginalOwnerKey,Creation=receipt.Source.Creation,EntityId=receipt.Source.StartingEntityId,Actor=original.Actor,Deaths=original.Deaths,
            PlayerRoot=Path.GetFullPath(GameIO.GetPlayerDataDir()),PlayerName=identity.CanonicalId,Delivered=true,Allocation=allocation,SourceRetention=retention};
        imported.Witness=result;deferred.Add(result);return result;
    }
    private static DeferredWitness BeginDeferredHostLocal(RebirthProductiveEarnedUiQueueOwner.DeferredReceipt receipt)
    {
        var member=receipt.HostLocal;var source=member?.Source;
        if(source?.Current()!=true||!source.AcceptingSource()||deferred.Count>=4||
            deferred.Any(value=>value.Operation==member.Operation))return null;
        var allocation=member.Retain();var retention=member.Retain();if(allocation==null||retention==null){allocation?.Dispose();retention?.Dispose();return null;}
        var result=new DeferredWitness{HostLocalOriginal=member,EarlierOwner=receipt.Source,World=source.Original.World,Station=source.Original.Station,
            Operation=member.Operation,Owner=receipt.Source.OriginalOwnerKey,Creation=receipt.Source.Creation,EntityId=receipt.Source.StartingEntityId,
            Allocation=allocation,SourceRetention=retention};deferred.Add(result);return result;
    }
    internal static void ObserveNativeDelivery(RebirthRecipeOutputIntegration.OwningDelivery delivery)
    {
        try{lock(gate)foreach(var witness in deferred.Where(value=>value.NativeOriginal!=null&&value.Operation==delivery.Operation).ToArray())witness.ObserveDelivery(delivery);}
        catch{/* An observer cannot change actual native delivery behavior. */}
    }
    internal static void PumpNativeTerminals()
    {
        if(!ThreadManager.IsMainThread())return;
        PumpLocalOwnerBefore();
        foreach(var group in deferred.Where(value=>value.NativeOriginal!=null&&value.Receipt!=null).GroupBy(value=>value.Receipt.Receipt).ToArray())
        {
            try
            {
                var members=group.ToArray();var contributions=members[0].Receipt.AllContributions;
                if(localOwners.Values.Any(value=>!value.Closed&&members.Any(member=>value.Members.Contains(member))))continue;
                if(contributions.Any(value=>value.Original!=null&&!value.Original.TerminalSaved)||
                    contributions.Any(value=>value.HostLocal!=null&&!value.HostLocal.Source.Original.Original.Completed)||
                    contributions.Where(value=>value.NativeOriginal!=null).Any(value=>!members.Any(member=>ReferenceEquals(member.Receipt,value)))||
                    members.Any(value=>!value.OwnerTerminalSaved&&!value.ObservedSavedEvent()))continue;
                foreach(var member in members)if(member.Delivery==null&&member.NativeOriginal.TryCopyConfirmedDelivery(out var delivery))member.RetainDelivery(delivery);
                if(members.Any(value=>!value.OwnerTerminalSaved&&(value.Delivery==null||!value.NativeOriginal.SaveOwnerTerminal(value))))continue;
                foreach(var member in members){var receipt=member.Receipt;member.Dispose();receipt.Dispose();}
            }
            catch{/* Positive terminal proof must publish before observation retirement. */}
        }
    }
    internal static object CaptureDeferredOwners(string operation)
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException();
        lock(gate)return deferred.Where(value=>operation==null||value.Operation.ToString("N")==operation).Select(value=>new{
            operation=value.Operation.ToString("N"),kind=value.HostLocalOriginal!=null?"hostLocal":value.ImportedOriginal!=null?"imported-"+value.ImportedOriginal.Kind:value.NativeOriginal!=null?"native":"uio",sourceOwner=value.EarlierOwner.OriginalOwnerKey,
            sourceCreation=value.EarlierOwner.Creation,recipient=value.Owner,recipientCreation=value.Creation,entity=value.EntityId,
            current=value.Current(),delivered=value.Delivered,consumed=value.Consumed,nativeSavedEvent=value.Completed,terminalSaved=value.OwnerTerminalSaved,
            nativeFile=value.FinalPath,nativeFileDigest=value.Final==null?null:Hash(value.Final),
            ownerBefore=value.Before==null?null:Hash(value.Before),ownerAfter=value.After==null?null:Hash(value.After),
            bagBefore=value.BagBefore==null?null:Hash(value.BagBefore),bagAfter=value.BagAfter==null?null:Hash(value.BagAfter),
            beltBefore=value.BeltBefore==null?null:Hash(value.BeltBefore),beltAfter=value.BeltAfter==null?null:Hash(value.BeltAfter),
            members=value.Receipt?.AllContributions.Select(member=>new{operation=member.Operation.ToString("N"),kind=member.Original!=null?"uio":"native"}).ToArray()}).ToArray();
    }
    private static byte[] ReceiptImage(CraftCompleteData receipt)
    {using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);receipt.Write(writer);writer.Flush();if(stream.Length>256*1024)throw new InvalidDataException();return stream.ToArray();}}
    private static byte[] Slots(ItemStack[] slots)
    {
        if(slots==null||slots.Length>255)throw new InvalidDataException();
        using(var stream=new Limited())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            var encoding=writer.Encoding;
            try{writer.Encoding=new System.Text.UTF8Encoding(false,false);writer.SetBaseStream(stream);writer.Write(slots.Length);
                foreach(var stack in slots){if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException();writer.Write(stack.count);stack.itemValue.Write(writer);}
                writer.Flush();return stream.ToArray();}
            finally{writer.Encoding=encoding;}
        }
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.CheckForCraftComplete))]
    private static class DeferredCheck
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(TileEntityWorkstation __instance,EntityPlayerLocal player,bool __runOriginal,out Consumption __state)
        {
            __state=null;if(!__runOriginal||!ThreadManager.IsMainThread()||consuming!=null)return;
            IDisposable captureBudget=null;DeferredWitness[] captured=null;
            try{lock(gate){var matches=deferred.Where(value=>value.Receipt!=null&&!value.Consumed&&value.Current()&&ReferenceEquals(value.Station,__instance)&&value.EntityId==player.entityId&&
                __instance.CraftCompleteList?.Any(row=>ReferenceEquals(row,value.Receipt.Receipt))==true).ToArray();
                if(matches.Length==0)return;var witness=matches[0];var token=witness.Receipt;
                if(token==null||matches.Any(value=>value.Receipt==null||value.Owner!=witness.Owner||value.Creation!=witness.Creation))return;
                foreach(var aggregate in matches.GroupBy(value=>value.Receipt.Receipt))
                {var proof=aggregate.First().Receipt;if(!proof.CurrentAggregateMembership())return;var contributions=proof.AllContributions;
                    if(contributions.Length!=aggregate.Count()||contributions.Any(value=>!aggregate.Any(member=>ReferenceEquals(member.Receipt,value))))return;}
                var rows=__instance.CraftCompleteList?.ToArray();
                if(rows==null||rows.Length>64||rows.Count(value=>ReferenceEquals(value,witness.Receipt.Receipt))!=1||
                    rows.Count(value=>value.CrafterEntityID==player.entityId)!=matches.Select(value=>value.Receipt.Receipt).Distinct().Count()||
                    !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=witness.Owner||
                    RebirthCamperCharacterAccess.CurrentCreation(player)!=witness.Creation||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return;
                foreach(var member in matches){member.Actor=player;member.Deaths=player.Died;member.PlayerRoot=Path.GetFullPath(GameIO.GetPlayerDataDir());member.PlayerName=identity.CanonicalId;
                    if(!member.Current()||member.Before!=null||member.NativeAllocation!=null)return;}
                // Large native capture workspace begins only at actual owner
                // consumption, never while an offline queued owner is pending.
                var imported=matches[0].ImportedOriginal?.Original;
                if(imported!=null)
                {
                    if(matches.Any(value=>!ReferenceEquals(value.ImportedOriginal?.Original,imported)))return;
                    long receiptBytes=0;
                    foreach(var row in rows){if(receiptBytes>=1024*1024)return;receiptBytes+=MeasureNative(writer=>row.Write(writer),1024*1024-receiptBytes);}
                    captureBudget=imported.RetainNativeCapture(receiptBytes);if(captureBudget==null)return;
                }
                else if(TryGetInvokingLocalOwner(__instance,player,out var local))
                {captureBudget=local.Retain();if(captureBudget==null)return;}
                else if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(16L*Maximum,out captureBudget))return;
                captured=matches;
                foreach(var member in matches)member.Before=Live(player,member.Receipt.Receipt.RecipeName);
                __state=new Consumption{Previous=consuming,Witness=witness,Members=matches,Actor=player,Rows=rows,Images=rows.Select(ReceiptImage).ToArray(),
                    Bag=Slots(player.bag.ItemGrid.items),Belt=Slots(player.inventory.ItemGrid.items),Equipment=RebirthRecipeOutputIntegration.OwningEquipmentNative(player)};
                if(matches.Sum(member=>(long)member.Before.Length)+__state.Bag.Length+__state.Belt.Length+__state.Equipment.Length>Maximum)
                    throw new InvalidDataException("Native owner before frame bound");
                var shared=new SharedCapture(captureBudget,matches.Length);foreach(var member in matches){member.nativeShared=shared;member.NativeAllocation=shared.Owner();}captureBudget=null;
                foreach(var member in matches){member.BagBefore=__state.Bag;member.BeltBefore=__state.Belt;member.EquipmentBefore=__state.Equipment;}
                consuming=__state;}}
            catch{__state=null;if(captured!=null)foreach(var member in captured)member.Before=null;}
            finally{captureBudget?.Dispose();}
        }
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(bool __runOriginal,Consumption __state)
        {
            if(__state==null)return;
            if(!__runOriginal)
            {
                try{var rows=__state.Witness.Station.CraftCompleteList;
                    if(!__state.Unknown&&__state.Awarded.Count==0&&rows!=null&&rows.Count==__state.Rows.Length&&
                        rows.Select((row,i)=>ReferenceEquals(row,__state.Rows[i])&&ReceiptImage(row).SequenceEqual(__state.Images[i])).All(value=>value)&&
                        Slots(__state.Actor.bag.ItemGrid.items).SequenceEqual(__state.Bag)&&Slots(__state.Actor.inventory.ItemGrid.items).SequenceEqual(__state.Belt)&&
                        RebirthRecipeOutputIntegration.OwningEquipmentNative(__state.Actor).SequenceEqual(__state.Equipment)&&
                        __state.Members.All(member=>member.Current()&&Live(__state.Actor,member.Receipt.Receipt.RecipeName).SequenceEqual(member.Before)))
                        foreach(var member in __state.Members){member.Before=member.BagBefore=member.BeltBefore=member.EquipmentBefore=null;member.NativeAllocation?.Dispose();member.NativeAllocation=null;member.nativeShared=null;}
                }catch{/* An uncertain skipped invocation remains retained. */}
                return;
            }
            try{var witness=__state.Witness;var rows=witness.Station.CraftCompleteList;
                var aggregates=__state.Members.Select(value=>value.Receipt.Receipt).Distinct().ToArray();
                if(__state.Unknown||__state.Awarded.Count!=aggregates.Length||aggregates.Any(value=>!__state.Awarded.Contains(value))||
                    !witness.Current()||rows==null||rows.Count!=__state.Rows.Length-aggregates.Length||
                    rows.Any(row=>aggregates.Any(value=>ReferenceEquals(value,row))))return;
                int next=0;for(int i=0;i<__state.Rows.Length;i++){if(aggregates.Any(value=>ReferenceEquals(value,__state.Rows[i])))continue;
                    if(next>=rows.Count||!ReferenceEquals(rows[next],__state.Rows[i])||!ReceiptImage(rows[next]).SequenceEqual(__state.Images[i]))return;next++;}
                foreach(var member in __state.Members){if(!member.Current())return;}
                var after=__state.Members.Select(member=>Live(__state.Actor,member.Receipt.Receipt.RecipeName)).ToArray();
                var bagAfter=Slots(__state.Actor.bag.ItemGrid.items);var beltAfter=Slots(__state.Actor.inventory.ItemGrid.items);
                var equipmentAfter=RebirthRecipeOutputIntegration.OwningEquipmentNative(__state.Actor);
                if(after.Sum(image=>(long)image.Length)+bagAfter.Length+beltAfter.Length+equipmentAfter.Length>Maximum)return;
                for(int i=0;i<__state.Members.Length;i++){__state.Members[i].After=after[i];__state.Members[i].BagAfter=bagAfter;__state.Members[i].BeltAfter=beltAfter;__state.Members[i].EquipmentAfter=equipmentAfter;__state.Members[i].Consumed=true;}
                var accepted=new DeferredConsumptionEvent(__state);
                RebirthProductiveEarnedUiDeferredTransport.ObserveImportedConsumption(accepted);
                ObserveLocalOwnerConsumption(__state);}
            catch{/* Unknown actual consumption remains retained, never replayed. */}
        }
        private static Exception Finalizer(Exception __exception,Consumption __state)
        {if(__state!=null&&ReferenceEquals(consuming,__state))consuming=__state.Previous;return __exception;}
    }
    [HarmonyPatch(typeof(EntityPlayerLocal),nameof(EntityPlayerLocal.GiveExp),new[]{typeof(CraftCompleteData)})]
    private static class DeferredGive
    {
        private static void Prefix(EntityPlayerLocal __instance,CraftCompleteData data,bool __runOriginal,out Consumption __state)
        {__state=null;var scope=consuming;if(scope==null)return;
            try{if(!__runOriginal||scope.Awarded.Contains(data)||!ReferenceEquals(scope.Actor,__instance)||!scope.Members.Any(value=>ReferenceEquals(value.Receipt.Receipt,data))||
                !ReceiptImage(data).SequenceEqual(scope.Images[Array.IndexOf(scope.Rows,data)])){scope.Unknown=true;return;}__state=scope;}
            catch{scope.Unknown=true;}}
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CraftCompleteData data,bool __runOriginal,Consumption __state)
        {
            if(__state==null)return;
            try{if(__runOriginal&&__state.Members.Any(value=>ReferenceEquals(value.Receipt.Receipt,data))&&
                ReceiptImage(data).SequenceEqual(__state.Images[Array.IndexOf(__state.Rows,data)]))__state.Awarded.Add(data);else __state.Unknown=true;}
            catch{__state.Unknown=true;}
        }
        private static Exception Finalizer(Exception __exception,Consumption __state){if(__exception!=null&&__state!=null)__state.Unknown=true;return __exception;}
    }
    private static void ObserveDeferredPlayer(PlayerDataFile data,EntityPlayer player)
    {
        try{lock(gate){var members=deferred.Where(value=>value.Consumed&&value.After!=null&&value.Current()&&ReferenceEquals(value.Actor,player)).ToArray();
            if(members.Length==0)return;var witness=members[0];
            if(data.id!=player.entityId||members.Any(value=>data.deaths!=value.Deaths||!Projection(data,value.Receipt.Receipt.RecipeName).SequenceEqual(value.After)||
                !Slots(RebirthGearPlayerDataInventory.ReadSlots(data,true)).SequenceEqual(value.BagAfter)||
                !Slots(RebirthGearPlayerDataInventory.ReadSlots(data,false)).SequenceEqual(value.BeltAfter)||
                data.equipmentData==null||!data.equipmentData.ToArray().SequenceEqual(value.EquipmentAfter)))return;
            int length=Maximum;
            var local=localOwners.Values.SingleOrDefault(value=>!value.Closed&&value.Consumed&&ReferenceEquals(value.Actor,player)&&members.All(member=>value.Members.Contains(member)));
            if(local!=null)
            {
                long measured=MeasureNative(writer=>{writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);writer.Write((byte)PlayerDataFile.cFileVersion);data.Write(writer,StreamModeWrite.Persistency);});
                if(!local.ReserveNative(measured))return;length=checked((int)measured);
            }
            files.Remove(data);files.Add(data,new NativeFile{Deferred=witness,DeferredGroup=members,LocalAfter=local,NativeLength=length,NativeHash=Hash(NativeImage(data,length))});}}
        catch{/* Actual native capture mismatch supplies no final-file proof. */}
    }
    private static bool TryDeferredSave(PlayerDataFile data,string directory,string name,bool run,out NativeFile result)
    {
        result=null;try{lock(gate){if(!run||!files.TryGetValue(data,out var native)||native.Deferred==null)return false;
            var witness=native.Deferred;if(witness.Closed||witness.After==null||Path.GetFullPath(directory)!=witness.PlayerRoot||name!=witness.PlayerName)return true;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));if(Path.GetDirectoryName(path)!=witness.PlayerRoot||!Safe(path))return true;
            // Acquire the same capture charge before allocating a detached
            // worker image. Owner teardown must not uncharge an active Save.
            var lease=native.LocalAfter!=null?native.LocalAfter.RetainWorker():witness.nativeShared?.Retain();if(lease==null)return true;
            try{var image=NativeImage(data,native.NativeLength>0?native.NativeLength:Maximum);if(Hash(image)!=native.NativeHash)return true;
                result=new NativeFile{Deferred=witness,DeferredGroup=native.DeferredGroup,Image=image,Path=path,DeferredWorkerRetention=lease};lease=null;return true;}
            finally{lease?.Dispose();}}}
        catch{return true;}
    }
    private static void ObserveDeferredSave(NativeFile native)
    {lock(gate){if(native.DeferredGroup==null||native.DeferredGroup.Any(value=>value.Closed)||!ReadFinal(native.Path,native.Image))return;
        foreach(var witness in native.DeferredGroup){witness.Final=native.Image;witness.FinalPath=native.Path;witness.Completed=true;}}}
    private static void ReleaseDeferredSaveState(NativeFile native)
    {
        if(native==null)return;
        lock(gate){native.Image=null;native.Deferred=null;native.DeferredGroup=null;native.Witness=null;native.Remote=null;native.LocalBefore=null;native.LocalAfter=null;
            native.DeferredWorkerRetention?.Dispose();native.DeferredWorkerRetention=null;}
    }
}
