using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Generic companion Use owns an NPC source, never a matching player bag item.
internal static partial class RebirthNpcMetabolismCustody
{
    internal sealed class Original
    {
        internal Guid Operation;internal World World;internal EntityPlayer Actor;internal EntityRebirthNPC Npc;internal RebirthNpcStableId Id;
        internal string Account,Creation,Save,Directory,Text,Key,OwnerPurpose;internal int Deaths;internal ulong Recovery;
        internal RebirthNpcInventorySnapshot Before;internal ItemStack Source;internal Guid SourceRecord;
        internal RebirthNpcNativeStackSet AfterNative;internal Dictionary<string,int> AfterQuantities;
        internal RebirthMetabolismStateRepository.NpcServingState Metabolism;internal RebirthMetabolismService.NpcSourcePlan Plan;
        internal bool Busy,SourceEntered,SourceReturned,SourceSaved,CanonicalEntered,CanonicalSaved,DeferredEntered,DeferredReturned,OwnerEntered,OwnerReturned,OwnerSaved,OwnerBeforeRequested,OwnerReady,SecondaryPdsRequested,OwnerSecondaryReturned,Terminal;
        internal IDisposable Memory;internal readonly long[] CourierAfter=new long[3];
        internal bool MetadataOnly,ObservationRetired;
        internal RebirthNpcInventoryTransactionService.MetabolismNotEntered SourceRefusal;
    }
    private static readonly Dictionary<RebirthNpcStableId,Original> pending=new Dictionary<RebirthNpcStableId,Original>();
    [ThreadStatic]private static Original sourceMutation,canonicalMutation;
    internal static bool Current(Original original)
    {
        return original!=null&&ThreadManager.IsMainThread()&&!original.Terminal&&!original.ObservationRetired&&ReferenceEquals(original.World,GameManager.Instance?.World)&&ReferenceEquals(original.Actor.world,original.World)&&ReferenceEquals(original.Npc.world,original.World)&&
            ReferenceEquals(original.World.GetEntity(original.Actor.entityId),original.Actor)&&ReferenceEquals(original.World.GetEntity(original.Npc.entityId),original.Npc)&&!original.Actor.IsDead()&&!original.Npc.IsDead()&&original.Actor.Died==original.Deaths&&
            original.Npc.RebirthRuntimeState?.StableId==original.Id&&RebirthCamperCharacterAccess.CurrentCreation(original.Actor)==original.Creation&&
            RebirthStablePlayerIdentity.TryResolveServerEntity(original.Actor,out var owner)&&owner.CanonicalId==original.Account&&
            original.Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&original.Recovery==RebirthNpcRecoveryGeneration.RequireCurrent()&&
            RemoteNpcResourceSource.IsAuthorized(original.Npc,original.Actor,false,out _)&&
            (original.Npc.position-original.Actor.position).sqrMagnitude<=LogisticsPreviewService.CompanionRadiusForPlanning*LogisticsPreviewService.CompanionRadiusForPlanning;
    }
    internal static bool AllowsSourceMutation(Original original)=>ReferenceEquals(sourceMutation,original)&&original.SourceEntered&&!original.SourceReturned&&Current(original);
    internal static bool AllowsCanonicalMutation(Original original)=>ReferenceEquals(canonicalMutation,original)&&original.SourceSaved&&original.CanonicalEntered&&!original.Metabolism.Applied&&Current(original);
    internal static bool Blocks(RebirthNpcStableId id)=>pending.TryGetValue(id,out var original)&&!original.Terminal||HasColdHold(id);
    internal static bool HoldsPlayer(EntityPlayer actor)=>pending.Values.Any(original=>!original.Terminal&&ReferenceEquals(original.Actor,actor))||HoldsNativeOwner(actor);
    internal static bool HoldsState(RebirthMetabolismState state)=>pending.Values.Any(original=>!original.Terminal&&(ReferenceEquals(original.Metabolism?.Original,state)||ReferenceEquals(original.Metabolism?.After,state)));
    internal static bool HoldsRepositoryScope(World world)=>world!=null&&pending.Values.Any(original=>!original.Terminal&&!original.ObservationRetired&&ReferenceEquals(original.World,world));
    internal static bool HasRetainedForShutdown(World world,string save)=>pending.Values.Any(original=>!original.Terminal&&!original.ObservationRetired&&ReferenceEquals(original.World,world)&&original.Save==Path.GetFullPath(save));
    private static void RequirePath(string path)
    {for(var directory=new DirectoryInfo(Path.GetFullPath(path));directory!=null;directory=directory.Parent)if(directory.Exists&&(directory.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("NPC consumption path is a reparse point");}
    private static string Native(ItemStack stack)=>stack==null||stack.IsEmpty()?string.Empty:RebirthNativeItemCodec.Encode(stack.itemValue);
    private static XElement Stack(string name,ItemStack value)=>new XElement(name,new XAttribute("count",value?.count??0),new XAttribute("item",Native(value)));
    private static byte[] ReadBoundedStageBytes(string path,int expected)
    {
        RequirePath(path);
        if(expected<1||expected>16*1024*1024||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Invalid stage bound");
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length!=expected)throw new IOException("Stage length changed");
            var bytes=new byte[expected];int offset=0;
            while(offset<expected){int count=input.Read(bytes,offset,expected-offset);if(count==0)throw new EndOfStreamException();offset+=count;}
            if(input.ReadByte()!=-1)throw new IOException("Stage grew during readback");return bytes;
        }
    }
    private static void Record(Original original,string stage,XElement detail=null)
    {
        RequirePath(original.Directory);string path=Path.Combine(original.Directory,stage+".xml");
        var image=new XElement("npcSourceConsumption",new XAttribute("version",1),new XAttribute("stage",stage),new XElement("original",original.Text),detail);
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(image.ToString(SaveOptions.DisableFormatting));if(bytes.Length>16*1024*1024)throw new InvalidDataException("NPC consumption record exceeds bound");
        if(File.Exists(path)){var info=new FileInfo(path);if((info.Attributes&FileAttributes.ReparsePoint)!=0||info.Length!=bytes.Length||!ReadBoundedStageBytes(path,bytes.Length).SequenceEqual(bytes))throw new IOException("NPC consumption immutable stage conflicts");ConvergeExistingStageHead(original,stage);return;}
        using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
        if(!ReadBoundedStageBytes(path,bytes.Length).SequenceEqual(bytes))throw new IOException("NPC consumption stage readback changed");
        AdvanceHead(original,stage);InvalidateColdIndex();
        ObserveScenario(original,stage);
    }
    internal static RebirthConsumeResult Use(EntityPlayer actor,RebirthNpcStableId id,int itemType,string key)
    {
        var retained=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="The original NPC consumption is retained; no additional item was consumed."};
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!nativeObserversInstalled)return retained;
        RetireReplacedConsumptionWorld();
        if(pending.TryGetValue(id,out var existing))return Advance(existing);
        if(RemoteResourceTransactions.ActionBlocksNpc(id)||HasColdHold(id)||pending.Count>=4||!RebirthNpcOriginalEffectAdmission.CanRecoverNpcStores(actor.world,new[]{id},out _))return retained;
        if(HoldsPlayer(actor))return retained;
        Original original=null;
        try
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity)||actor.world.GetEntity(entity) is not EntityRebirthNPC npc||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return retained;
            original=new Original{Operation=Guid.NewGuid(),World=actor.world,Actor=actor,Npc=npc,Id=id,Account=owner.CanonicalId,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),Deaths=actor.Died,
                Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Key=key,Memory=memory,Recovery=RebirthNpcRecoveryGeneration.RequireCurrent()};
            if(!Current(original))return retained;
            original.Before=RebirthNpcInventoryTransactionService.GetSnapshot(id);if(original.Before?.Quantities==null||!original.Before.Quantities.TryGetValue(key,out int available)||available<1||original.Before.Revision==uint.MaxValue)return retained;
            if(original.Before.NativeStacks!=null&&!original.Before.NativeStacks.FitsObservationBytes(2L*1024*1024))return retained;
            var native=original.Before.NativeStacks;var records=new List<RebirthNpcNativeStackRecord>();
            if(native!=null)foreach(var row in native.Write().Elements()){if(!RebirthNpcNativeStackRecord.TryRead(row,out var record))return retained;records.Add(record);}
            var chosen=records.FirstOrDefault(record=>string.Equals(record.ItemKey,key,StringComparison.OrdinalIgnoreCase));
            if(chosen!=null){if(!chosen.TryDecode(out original.Source))return retained;original.SourceRecord=chosen.StackId;}
            else
            {
                original.Before.Reservations.TryGetValue(key,out int reserved);if(available-reserved-(native?.GetQuantity(key)??0)<1)return retained;
                original.Source=new ItemStack(ItemClass.GetItem(key),1);
            }
            if(original.Source.itemValue.type!=itemType)return retained;
            original.Metabolism=RebirthMetabolismStateRepository.CaptureNpcServingState(actor,out var detached);if(original.Metabolism==null)return retained;
            original.Plan=RebirthMetabolismService.PlanNpcSource(actor,original.Source,detached,out var outcome);if(original.Plan==null)return outcome;
            if(!PlanSourceAfter(original,records)||System.Text.Encoding.UTF8.GetByteCount(Purpose(original))>48000)return retained;
            original.Directory=Path.Combine(original.Save,"RebirthData","NpcConsumption",original.Operation.ToString("N"));RequirePath(original.Directory);
            var image=new XElement("npcConsumptionOriginal",new XAttribute("version",2),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("npc",id.ToString()),new XAttribute("owner",original.Account),new XAttribute("creation",original.Creation??string.Empty),new XAttribute("deaths",original.Deaths),new XAttribute("save",original.Save),new XAttribute("recovery",original.Recovery),
                new XAttribute("revision",original.Before.Revision),new XAttribute("record",original.SourceRecord.ToString("N")),new XAttribute("key",key),CaptureOriginalNpcBody(original),RebirthMetabolismService.DescribeNpcPlan(original.Plan),Stack("serving",original.Plan.NativeItem),Stack("source",original.Source),Stack("replacement",original.Plan.Replacement),Stack("siblings",original.Plan.Siblings),
                new XElement("sourceBefore",Snapshot(original.Before.Quantities,original.Before.Reservations,original.Before.NativeStacks)),new XElement("sourceAfter",Snapshot(original.AfterQuantities,original.Before.Reservations,original.AfterNative)),
                new XElement("metabolismBefore",original.Metabolism.Before),new XElement("metabolismAfter",RebirthMetabolismStateRepository.DescribeNpcServing(original.Metabolism.Id,original.Plan.State)));
            original.Text=image.ToString(SaveOptions.DisableFormatting);Directory.CreateDirectory(original.Directory);Record(original,"prepared");pending.Add(id,original);return Advance(original);
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC] original consumption retained: "+error.Message);return retained;}
        finally{if(original!=null&&!pending.ContainsValue(original)){original.Memory?.Dispose();original.Memory=null;}}
    }
    private static XElement Snapshot(IDictionary<string,int> quantities,IDictionary<string,int> reservations,RebirthNpcNativeStackSet native)=>new XElement("inventory",
        quantities.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("quantity",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),
        reservations.OrderBy(pair=>pair.Key,StringComparer.Ordinal).Select(pair=>new XElement("reserved",new XAttribute("key",pair.Key),new XAttribute("count",pair.Value))),native?.Write());
    private static bool PlanSourceAfter(Original original,List<RebirthNpcNativeStackRecord> records)
    {
        var quantities=new Dictionary<string,int>(original.Before.Quantities,StringComparer.OrdinalIgnoreCase);int sourceCount=original.Source.count;
        if(!quantities.TryGetValue(original.Key,out int count)||count<sourceCount)return false;
        if(count==sourceCount)quantities.Remove(original.Key);else quantities[original.Key]=count-sourceCount;
        if(original.SourceRecord!=Guid.Empty)records.RemoveAll(record=>record.StackId==original.SourceRecord);
        foreach(var stack in new[]{original.Plan.Replacement,original.Plan.Siblings})
        {
            if(stack==null||stack.IsEmpty())continue;string key=stack.itemValue.ItemClass.GetItemName();quantities.TryGetValue(key,out int current);long total=(long)current+stack.count;if(total>int.MaxValue)return false;quantities[key]=(int)total;
            if(!RebirthNpcNativeStackRecord.TryCapture(Guid.NewGuid(),original.Id,stack,out var record))return false;records.Add(record);
        }
        foreach(var reservation in original.Before.Reservations)if(!quantities.TryGetValue(reservation.Key,out int remaining)||remaining<reservation.Value)return false;
        if(!RebirthNpcNativeStackSet.TryCreate(original.Id,original.Before.Revision+1,records,out var after)||!after.FitsQuantities(quantities))return false;
        original.AfterQuantities=quantities;original.AfterNative=after;return true;
    }
    private static RebirthConsumeResult Advance(Original original)
    {
        var retained=new RebirthConsumeResult{Success=false,Failure=RebirthMetabolismConsumeFailure.ItemChanged,Message="Original NPC consumption retained; no additional source debit."};
        if(original.Busy||!ThreadManager.IsMainThread())return retained;original.Busy=true;
        try
        {
            if(!original.MetadataOnly&&original.SourceSaved&&original.CanonicalSaved&&original.DeferredReturned&&original.OwnerReturned&&original.OwnerSaved)
            {var completed=original.Plan.Result;return CompleteWarmTerminalMetadata(original)?completed:retained;}
            if(!Current(original))return retained;
            if(original.MetadataOnly){CompleteColdTerminalMetadata(original);return retained;}
            if(original.SourceRefusal!=null){CompleteSourceRefusal(original);return retained;}
            if(!EnsureOwnerBefore(original))return retained;
            if(!original.SourceReturned)
            {
                if(original.SourceEntered||!RebirthMetabolismStateRepository.NpcServingBeforeCurrent(original.Metabolism))return retained;
                Record(original,"source-intent");original.SourceEntered=true;
                using(var lease=RebirthNpcInventoryAuthorityService.Issue("companions.npc-consumption",RebirthNpcInventoryAuthorityOperations.Mutate,TimeSpan.FromSeconds(30),original.Id))
                {sourceMutation=original;try
                    {
                        if(!RebirthNpcInventoryTransactionService.ApplyMetabolismOriginal(original,lease.AuthorityKey,out var refused))
                        {
                            if(refused==null||!refused.IsFor(original))return retained;
                            original.SourceRefusal=refused;CompleteSourceRefusal(original);return retained;
                        }
                        original.SourceReturned=true;
                    }finally{sourceMutation=null;}}
                Record(original,"source-returned");
            }
            Record(original,"source-returned");
            if(!original.SourceSaved)
            {
                if(!RebirthNpcPersistenceCoordinator.SaveAll(out _)||!Current(original)||!RebirthNpcInventoryPersistenceStore.CaptureConsumptionSave(original,out var saved))return retained;
                Record(original,"source-saved",new XElement("saved",saved));original.SourceSaved=true;
            }
            if(!original.Metabolism.Applied)
            {
                if(original.CanonicalEntered||!RecordFirstIngestion(original))return retained;original.CanonicalEntered=true;canonicalMutation=original;
                try{if(!RebirthMetabolismStateRepository.ApplyNpcServing(original))return retained;}finally{canonicalMutation=null;}
            }
            if(!original.CanonicalSaved)
            {
                if(!RebirthMetabolismStateRepository.SaveNpcServing(original,out var saved))return retained;Record(original,"ingestion-saved",new XElement("saved",saved));original.CanonicalSaved=true;
            }
            if(!original.DeferredReturned)
            {
                ObserveScenario(original,"before-secondary-permit");
                if(original.DeferredEntered)return retained;Record(original,"secondary-intent");original.DeferredEntered=true;
                RebirthMetabolismService.ApplyNpcSourceDeferredEffects(original.Plan);original.DeferredReturned=true;Record(original,"secondary-returned");
            }
            Record(original,"secondary-returned");
            if(!RebirthMetabolismStateRepository.SaveNpcServing(original,out var completeState))return retained;
            Record(original,"secondary-state-saved",new XElement("saved",completeState));
            if(!CompleteNativeSecondary(original))return retained;
            if(!CompleteNativeOwnerEffect(original))return retained;
            ObserveScenario(original,"before-terminal-metadata");Record(original,"committed");ArchiveCompletedOriginal(original);var result=original.Plan.Result;
            if(!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor)){if(!SendOwnerPurpose(original,2))return retained;}
            else{original.Actor.Buffs.RemoveCustomVar(OwnerReceipt(original.Operation));original.Actor.Buffs.RemoveCustomVar(SecondaryReceipt(original.Operation));original.Actor.Buffs.RemoveCustomVar(BeforeReceipt(original.Operation));GameManager.Instance.SaveLocalPlayerData();}
            original.Terminal=true;pending.Remove(original.Id);ReleaseOwner(original);return result;
        }
        catch(Exception error){Log.Warning("[REBIRTH NPC] paid consumption retained: "+error.Message);return retained;}
        finally{original.Busy=false;}
    }
    private static void CompleteSourceRefusal(Original original)
    {
        // Retain the actual pre-write refusal before any fallible durable write.
        // Publication retries must never re-enter the source setter.
        if(original.SourceRefusal==null||!original.SourceRefusal.IsFor(original)||!Current(original))return;
        Record(original,"source-not-entered");Record(original,"no-effect");ArchiveCompletedOriginal(original);
        if(!ReferenceEquals(original.World.GetPrimaryPlayer(),original.Actor)){if(!SendOwnerPurpose(original,3))return;}
        else{original.Actor.Buffs.RemoveCustomVar(BeforeReceipt(original.Operation));GameManager.Instance.SaveLocalPlayerData();}
        original.Terminal=true;pending.Remove(original.Id);ReleaseOwner(original);
    }
    private static void RetireReplacedConsumptionWorld()
    {
        if(!ThreadManager.IsMainThread())return;var world=GameManager.Instance?.World;if(world==null)return;
        foreach(var original in pending.Values.Where(value=>!ReferenceEquals(value.World,world)||value.Actor!=null&&!ReferenceEquals(world.GetEntity(value.Actor.entityId),value.Actor)).ToArray())
        {
            // The full immutable WAL remains. This retires observations only;
            // neither a financial outcome nor a native receipt is fabricated.
            lock(ownerGate){original.ObservationRetired=true;pending.Remove(original.Id);ReleaseOwner(original);}
        }
    }
    internal static void Tick()
    {if(!ThreadManager.IsMainThread()||!nativeObserversInstalled)return;RetireReplacedConsumptionWorld();PumpOwnerObservationRetirement();RetireClientNativeObservations();RetireNpcBodyObservations();RetireNativeOwnerObservations();PumpColdNeverIssuedBodies();PumpColdConsumptionOriginals();foreach(var original in pending.Values.Take(4).ToArray())if(!original.Busy)Advance(original);}
    private static void ObserveScenario(Original original,string stage)
    {
#if REBIRTH_DEBUG
        if(GamePrefs.GetString(EnumGamePrefs.GameName)=="CodexTest")
        {if(ColdSourceAudit.TryGetValue(original.Operation,out var receipt))receipt.Stage=stage;ScenarioBoundary?.Invoke(original.Operation,stage);}
#endif
    }
#if REBIRTH_DEBUG
    internal static Action<Guid,string> ScenarioBoundary;
#endif
}
