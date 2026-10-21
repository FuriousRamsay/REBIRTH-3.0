using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;

// Durable phase gates only. Transport/session admission and native endpoint
// mutations must supply the original authenticated current-scope predicate.
// These gates never accept a packet-supplied publication digest.
internal static class RebirthLogisticsTransferProtocol
{
    private static bool ReadExact(RebirthLogisticsTransferJournal.Record expected,Func<bool> current,out RebirthLogisticsTransferJournal.Record retained)
    {
        retained=null;
        try{return expected!=null&&ThreadManager.IsMainThread()&&GameManager.Instance?.World?.IsRemote()==false&&current?.Invoke()==true&&
            RebirthLogisticsTransferJournal.TryRead(expected.Owner,expected.Operation,out retained)&&
            RebirthLogisticsTransferJournal.Encode(expected)==RebirthLogisticsTransferJournal.Encode(retained)&&current();}
        catch{retained=null;return false;}
    }
    internal static bool ConfirmOwnerDebit(RebirthStablePlayerIdentity identity,RebirthLogisticsTransferJournal.Record expected,
        Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||retained.Legs.Any(leg=>leg.Restored))return false;
        if(!string.IsNullOrEmpty(retained.OwnerDebitProof)){published=retained;return true;}
        if(!RebirthLogisticsOwnerReceipt.TrySavedDebit(identity,retained,originalCurrent,out var proof,out _)&&
            !RebirthLogisticsDeathSettlement.TryOwnerDebitProof(identity,retained,originalCurrent,out proof))return false;
        retained.OwnerDebitProof=proof;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
        published=retained;return true;
    }
    internal static bool TryObserveNpcBefore(RebirthLogisticsTransferJournal.Record expected,string stableId,Func<bool> originalCurrent,out string proof)
    {
        proof=null;
        try
        {
            if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=0||retained.Rejecting||
                !RebirthNpcStableId.TryParse(stableId,out var id))return false;
            var leg=retained.Npcs.SingleOrDefault(value=>value.StableId==stableId);if(leg==null||leg.Attempted!=0||leg.Published!=0)return false;
            RebirthNpcInventoryPersistenceStore.Save();
            if(!RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var saved)||!RebirthNpcInventoryTransactionService.ValidatePersistentRecord(saved,out _)||
                saved.Revision!=leg.Revision||(saved.NativeStacks?.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)??string.Empty)!=(leg.NativeBefore??string.Empty))return false;
            var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in leg.Entries)
            {
                known.Add(entry.Key);saved.Quantities.TryGetValue(entry.Key,out int count);saved.Reservations.TryGetValue(entry.Key,out int reserved);
                if(count!=entry.Before||reserved!=entry.Reserved||(saved.NativeStacks?.GetQuantity(entry.Key)??0)!=entry.Custodied)return false;
            }
            if(saved.Quantities.Keys.Any(key=>!known.Contains(key))||saved.Reservations.Keys.Any(key=>!known.Contains(key))||originalCurrent?.Invoke()!=true)return false;
            using(var hash=System.Security.Cryptography.SHA256.Create())proof=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)+":"+stableId+":"+saved.Revision))).Replace("-","").ToLowerInvariant();return true;
        }
        catch{proof=null;return false;}
    }
    internal static bool ConfirmNoEffect(RebirthStablePlayerIdentity identity,RebirthLogisticsTransferJournal.Record expected,
        Func<bool> originalRejectedCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalRejectedCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||!retained.Rejecting||
            !string.IsNullOrEmpty(retained.OwnerDebitProof)||retained.Legs.Any(leg=>leg.Attempted!=0||leg.Published!=0||leg.Restored)||
            retained.Npcs.Any(leg=>leg.Attempted!=0||leg.Published!=0)||
            !RebirthLogisticsOwnerReceipt.TrySavedStage(identity,retained,5,originalRejectedCurrent,out var proof,out _))return false;
        retained.OwnerNoEffectProof=proof;retained.Outcome=3;retained.Phase=2;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalRejectedCurrent))return false;
        published=retained;return true;
    }
    internal static bool PrepareRejection(RebirthLogisticsTransferJournal.Record expected,Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase>1||retained.Outcome!=0||
            retained.Legs.Any(leg=>leg.Attempted!=0)||retained.Npcs.Any(leg=>leg.Attempted!=0))return false;
        retained.Phase=1;retained.Rejecting=true;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;published=retained;return true;
    }
    internal static bool PreparePaidAbort(RebirthLogisticsTransferJournal.Record expected,Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            retained.Legs.Any(leg=>leg.Attempted>1||leg.Kind==RemoteResourceSourceKind.NpcStorage&&leg.Attempted!=0)||retained.Npcs.Any(leg=>leg.Attempted!=0))return false;
        retained.Rejecting=true;if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;published=retained;return true;
    }
    internal static bool ConfirmEndpointsPublished(RebirthLogisticsTransferJournal.Record expected,Func<bool> originalCurrent,
        out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Rejecting||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            retained.Legs.Any(leg=>leg.Published!=2||leg.Restored)||retained.Npcs.Any(leg=>leg.Published!=2))return false;
        retained.Phase=2;retained.Outcome=1;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
        published=retained;return true;
    }
    internal static bool PrepareLegAttempt(RebirthLogisticsTransferJournal.Record expected,string legId,bool logicalNpc,bool credit,Func<bool> originalCurrent,
        out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||retained.Rejecting||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            retained.Legs.Any(value=>value.Restored)||credit&&(retained.Legs.Any(value=>value.Published<1)||retained.Npcs.Any(value=>value.Published<1)))return false;
        int desired=credit?2:1;
        if(logicalNpc)
        {
            var leg=retained.Npcs.SingleOrDefault(value=>"N:"+value.StableId==legId);
            if(leg==null||leg.Attempted>desired||leg.Attempted<desired-1||credit&&leg.Published!=1)return false;
            leg.Attempted=desired;
        }
        else
        {
            var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);
            if(leg==null||leg.Attempted>desired||leg.Attempted<desired-1||credit&&leg.Published!=1)return false;
            leg.Attempted=desired;
        }
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
        published=retained;return true;
    }
    internal static bool ConfirmNpcPublished(RebirthLogisticsTransferJournal.Record expected,string stableId,bool credit,Func<bool> originalCurrent,
        out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        try
        {
            if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||string.IsNullOrEmpty(retained.OwnerDebitProof)||retained.Legs.Any(value=>value.Restored))return false;
            var leg=retained.Npcs.SingleOrDefault(value=>value.StableId==stableId);int stage=credit?2:1;
            if(leg==null||leg.Attempted!=stage||leg.Published<stage-1||leg.Published>stage||!RebirthNpcStableId.TryParse(stableId,out var id)||
                !RebirthNpcInventoryPersistenceStore.TryReadSavedTransferSnapshot(id,out var saved)||saved.Revision<leg.Revision+(uint)stage||saved.LogisticsEffects==null||
                !saved.LogisticsEffects.TryGetValue(credit?leg.CreditId:leg.DebitId,out var effect)||!RebirthLogisticsNpcEffectReceipt.MatchesLogistics(retained,leg,credit,effect)||
                !RebirthNpcInventoryTransactionService.ValidatePersistentRecord(saved,out _)||originalCurrent?.Invoke()!=true)return false;
            if(credit&&(!saved.LogisticsEffects.TryGetValue(leg.DebitId,out var debitEffect)||!RebirthLogisticsNpcEffectReceipt.MatchesLogistics(retained,leg,false,debitEffect)))return false;
            // The typed effect was installed atomically with the exact original
            // postimage. Later legitimate NPC work may change current counts;
            // the positive saved original effect remains independently bound.
            leg.Published=stage;
            if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
            published=retained;return true;
        }
        catch{return false;}
    }
    internal static bool ConfirmNativePublished(RebirthLogisticsTransferJournal.Record expected,string legId,bool credit,
        RebirthRemoteResourceSourceCustody.Lease lease,Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        try
        {
            if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||retained.Rejecting||
                string.IsNullOrEmpty(retained.OwnerDebitProof)||lease==null||!lease.Current()||lease.Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.Logistics||
                lease.Original.Operation!=retained.Operation||lease.Original.Owner!=retained.Owner||lease.Original.SourceId!=legId||
                lease.Original.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained))return false;
            var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);int stage=credit?2:1;
            if(leg==null||leg.Kind==RemoteResourceSourceKind.NpcStorage||leg.Attempted!=stage||leg.Published<stage-1||leg.Published>stage||leg.Restored||
                !lease.TryObserveSaved(stage,out _)||originalCurrent?.Invoke()!=true)return false;
            leg.Published=stage;if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;published=retained;return true;
        }
        catch{return false;}
    }
    internal static bool ConfirmDogPublished(RebirthLogisticsTransferJournal.Record expected,string legId,bool credit,Func<bool> originalCurrent,
        out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||string.IsNullOrEmpty(retained.OwnerDebitProof)||retained.Legs.Any(value=>value.Restored))return false;
        var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);int stage=credit?2:1;
        if(leg==null||leg.Attempted!=stage||leg.Published<stage-1||leg.Published>stage||!RebirthLogisticsDogTransfer.Saved(retained,leg,credit,originalCurrent))return false;
        leg.Published=stage;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
        published=retained;return true;
    }
    // The backend supplies the exact original endpoint's native final-file
    // restoration witness under its exclusive retained hold. No packet boolean
    // can call this gate. Every attempted native debit needs positive original
    // restoration publication before an abort can release owner payment.
    internal static bool ConfirmRestoredLeg(RebirthLogisticsTransferJournal.Record expected,string legId,RebirthRemoteResourceSourceCustody.Lease lease,
        Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            retained.Legs.Any(leg=>leg.Attempted>1||leg.Kind==RemoteResourceSourceKind.NpcStorage&&leg.Attempted>0)||retained.Npcs.Any(leg=>leg.Attempted!=0))return false;
        var leg=retained.Legs.SingleOrDefault(value=>value.Id==legId);
        if(leg==null||leg.Kind==RemoteResourceSourceKind.NpcStorage||lease==null||!lease.Current()||lease.Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.Logistics||
            lease.Original.Operation!=retained.Operation||lease.Original.Owner!=retained.Owner||lease.Original.SourceId!=legId||lease.Original.Binding!=RebirthLogisticsTransferJournal.ImmutablePlanDigest(retained)||
            !lease.TryObserveSaved(3)||originalCurrent?.Invoke()!=true)return false;
        leg.Restored=true;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,()=>originalCurrent()&&lease.Current()&&lease.TryObserveSaved(3)))return false;
        published=retained;return true;
    }
    internal static bool ConfirmAbort(RebirthLogisticsTransferJournal.Record expected,Func<bool> originalCurrent,
        out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||retained.Phase!=1||retained.Outcome!=0||string.IsNullOrEmpty(retained.OwnerDebitProof)||
            retained.Legs.Any(leg=>leg.Attempted>0&&!leg.Restored||leg.Attempted>1||leg.Kind==RemoteResourceSourceKind.NpcStorage&&leg.Attempted>0)||retained.Npcs.Any(leg=>leg.Attempted!=0))return false;
        retained.Outcome=2;retained.Phase=2;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,originalCurrent))return false;
        published=retained;return true;
    }
    internal static bool ConfirmOwnerCompletion(RebirthStablePlayerIdentity identity,RebirthLogisticsTransferJournal.Record expected,
        Func<bool> originalCurrent,out RebirthLogisticsTransferJournal.Record published)
    {
        published=null;
        if(!ReadExact(expected,originalCurrent,out var retained)||(retained.Phase!=2&&retained.Phase!=3)||retained.Outcome==0)return false;
        // A positively witnessed old-life partial debit may owe original
        // refund custody despite Outcome3. Only its native parcel settlement
        // can acknowledge completion; a native no-effect marker cannot.
        if(retained.OwnerEscrowRecovery)
            return RebirthLogisticsDeathSettlement.TryComplete(identity,retained,originalCurrent,out published);
        int nativePhase=retained.Outcome==3?5:retained.Phase+1;
        if(retained.Outcome==3?string.IsNullOrEmpty(retained.OwnerNoEffectProof):string.IsNullOrEmpty(retained.OwnerDebitProof))return false;
        if(!RebirthLogisticsOwnerReceipt.TrySavedStage(identity,retained,nativePhase,originalCurrent,out var proof,out _))
            return RebirthLogisticsDeathSettlement.TryComplete(identity,retained,originalCurrent,out published);
        if(retained.Outcome==3&&(proof!=retained.OwnerNoEffectProof||!RebirthLogisticsParcelReferenceJournal.ReadyForNoEffectItemTerminal(retained)))return false;
        retained.Phase++;
        if(!RebirthLogisticsTransferJournal.TryPublish(retained,()=>originalCurrent()&&(retained.Outcome!=3||RebirthLogisticsParcelReferenceJournal.ReadyForNoEffectItemTerminal(retained))))return false;
        published=retained;return true;
    }
}

internal static class RebirthLogisticsVehiclePublication
{
    private const string Key="rebirth.logistics.incarnation";
    private static readonly List<Expected> waiting=new List<Expected>();
    [ThreadStatic]private static VehicleManager writing;
    private static bool installed;
    internal sealed class Expected:IDisposable
    {
        internal EntityVehicle Vehicle;internal VehicleManager Manager;internal string Incarnation;internal DateTime Expires;
        internal RebirthCamperSavedVehicle.Expected Native;internal bool Observed,Closed;
        public void Dispose(){Closed=true;waiting.Remove(this);}
    }
    private static string Identity(EntityVehicle vehicle)
    {
        var item=vehicle?.vehicle?.GetUpdatedItemValue();
        return item?.TryGetMetadata(Key,out string text)==true&&Guid.TryParseExact(text,"N",out var id)&&id!=Guid.Empty?text:null;
    }
    internal static string CurrentIncarnation(EntityVehicle vehicle)
        =>ThreadManager.IsMainThread()&&vehicle?.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,vehicle.world)&&
            ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)&&!vehicle.IsDead()?Identity(vehicle):null;
    internal static Expected BeginIdentityAdmission(EntityVehicle vehicle,Func<bool> authorizedCurrent)
    {
        if(!installed||RebirthLogisticsPlanning.IsActive||!ThreadManager.IsMainThread()||vehicle?.world?.IsRemote()!=false||authorizedCurrent?.Invoke()!=true||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))return null;
        var item=vehicle.vehicle?.GetUpdatedItemValue();if(item==null)return null;
        if(!item.HasMetadata(Key))
        {
            if(item.Metadata?.Count>=byte.MaxValue)return null;
            item.SetMetadata(Key,Guid.NewGuid().ToString("N"));vehicle.SendSyncData((ushort)4);
        }
        return Begin(vehicle,authorizedCurrent);
    }
    internal static Expected Begin(EntityVehicle vehicle,Func<bool> current)
    {
        if(!installed||!ThreadManager.IsMainThread()||vehicle?.world?.IsRemote()!=false||current?.Invoke()!=true||Identity(vehicle)==null||
            !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))return null;
        foreach(var stale in waiting.ToArray())if(stale.Expires<=DateTime.UtcNow||!ReferenceEquals(stale.Native.World,vehicle.world))stale.Dispose();if(waiting.Count>=16)return null;
        try
        {
            var result=new Expected{Vehicle=vehicle,Manager=VehicleManager.Instance,Incarnation=Identity(vehicle),Expires=DateTime.UtcNow.AddSeconds(30),Native=RebirthCamperSavedVehicle.RequestSave(vehicle)};
            if(result.Manager==null)return null;waiting.Add(result);return result;
        }
        catch{return null;}
    }
    internal static bool Verify(Expected expected,Func<bool> current)
    {
        if(expected==null||expected.Closed||!expected.Observed||expected.Expires<=DateTime.UtcNow||!ThreadManager.IsMainThread()||current?.Invoke()!=true||
            !ReferenceEquals(VehicleManager.Instance,expected.Manager)||Identity(expected.Vehicle)!=expected.Incarnation)return false;
        return RebirthCamperSavedVehicle.Verify(expected.Vehicle,expected.Native,()=>!expected.Closed&&current()&&Identity(expected.Vehicle)==expected.Incarnation);
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.vehicle.publication.v1");
        try{RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Writer));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Snapshot));installed=true;}catch{harmony.UnpatchSelf();throw;}
    }
    [HarmonyPatch(typeof(VehicleManager),"write")]
    private static class Writer
    {
        private static void Prefix(VehicleManager __instance,out VehicleManager __state){__state=writing;writing=__instance;}
        private static void Finalizer(VehicleManager __state){writing=__state;}
    }
    [HarmonyPatch(typeof(VehicleManager),"GetVehiclesECDList")]
    private static class Snapshot
    {
        private static void Postfix(VehicleManager __instance,List<EntityCreationData> __result)
        {
            if(!ReferenceEquals(writing,__instance)||!ThreadManager.IsMainThread())return;
            try{foreach(var expected in waiting)
            {
                if(expected.Closed||expected.Expires<=DateTime.UtcNow||!ReferenceEquals(expected.Manager,__instance)||Identity(expected.Vehicle)!=expected.Incarnation||
                    !ReferenceEquals(GameManager.Instance?.World,expected.Native.World)||!ReferenceEquals(expected.Native.World.GetEntity(expected.Native.Id),expected.Vehicle)||GameIO.GetSaveGameDir()!=expected.Native.SaveDirectory)continue;
                var records=__result.Where(record=>record.id==expected.Native.Id).ToArray();if(records.Length!=1)continue;var native=records[0];
                if(native.entityClass!=expected.Native.Class||native.bagData==null||native.entityData==null||!native.bagData.ToArray().SequenceEqual(expected.Native.Bag))continue;
                // Only actual native writer metadata snapshot may advance this
                // demand witness. Original bag bytes remain immutable.
                expected.Native.Entity=native.entityData.ToArray();expected.Observed=true;
            }}catch{ /* Evidence observation cannot change native save. */ }
        }
    }
}

// Composite storage retains its existing native name-feature incarnation. This
// witness owns no packed-container receipt and never changes container contents.
internal sealed class RebirthLogisticsCompositePublication:IDisposable
{
    private static readonly List<RebirthLogisticsCompositePublication> pending=new List<RebirthLogisticsCompositePublication>();
    private static bool installed;
    private World world;private TileEntityComposite tile;private TEFeatureStorage storage;private TEFeatureRebirthContainerName name;
    private string incarnation;private byte[] items;private bool[] locks;private Func<bool> current;private RebirthStationSnapshotEvidence.Request request;
    private long offset=-1;private byte[] span;private DateTime expires;private bool closed;
    private bool Current()
    {
        if(closed||!ThreadManager.IsMainThread()||expires<=DateTime.UtcNow||current?.Invoke()!=true||!ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||
            !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile)||!ReferenceEquals(tile.GetFeature<TEFeatureStorage>(),storage)||!ReferenceEquals(tile.GetFeature<TEFeatureRebirthContainerName>(),name)||name.Incarnation!=incarnation||
            !RebirthCamperInventorySnapshot.Capture(storage.ItemGrid.items).SequenceEqual(items)||storage.ItemGrid.items.Length!=locks.Length)return false;
        var nativeLocks=storage.ItemGrid.SlotLocks;for(int i=0;i<locks.Length;i++)if((nativeLocks!=null&&i<nativeLocks.Length&&nativeLocks[i])!=locks[i])return false;
        return true;
    }
    internal static RebirthLogisticsCompositePublication Begin(World world,TileEntityComposite tile,Func<bool> originalCurrent)
    {
        if(!installed||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||tile==null||originalCurrent?.Invoke()!=true||!ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile))return null;
        foreach(var stale in pending.ToArray())if(stale.expires<=DateTime.UtcNow||!ReferenceEquals(stale.world,world))stale.Dispose();if(pending.Count>=4)return null;
        var storage=tile.GetFeature<TEFeatureStorage>();var name=tile.GetFeature<TEFeatureRebirthContainerName>();var chunk=tile.GetChunk();
        if(storage?.ItemGrid?.items==null||storage.ItemGrid.items.Length<1||storage.ItemGrid.items.Length>256||name==null||!Guid.TryParseExact(name.Incarnation,"N",out var id)||id==Guid.Empty||chunk==null||chunk.InProgressUnloading)return null;
        var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
        if(regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased)||string.IsNullOrEmpty(regions.saveDirectory))return null;
        var access=regions.regionFileAccess;string directory=Path.GetFullPath(regions.saveDirectory);var nativeLocks=storage.ItemGrid.SlotLocks;
        if(nativeLocks!=null&&nativeLocks.Length!=storage.ItemGrid.items.Length)return null;
        var result=new RebirthLogisticsCompositePublication{world=world,tile=tile,storage=storage,name=name,incarnation=name.Incarnation,items=RebirthCamperInventorySnapshot.Capture(storage.ItemGrid.items),
            locks=new bool[storage.ItemGrid.items.Length],expires=DateTime.UtcNow.AddSeconds(30)};
        for(int i=0;i<result.locks.Length;i++)result.locks[i]=nativeLocks!=null&&nativeLocks[i];
        result.current=()=>originalCurrent()&&ReferenceEquals(tile.GetChunk(),chunk)&&ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&!chunk.InProgressUnloading&&
            ReferenceEquals(world.ChunkCache?.ChunkProvider,provider)&&ReferenceEquals(provider.m_RegionFileManager,regions)&&ReferenceEquals(regions.regionFileAccess,access)&&Path.GetFullPath(regions.saveDirectory)==directory;
        if(!result.Current())return null;result.request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,result.Current,tile);if(result.request==null)return null;pending.Add(result);
        try
        {
            lock(chunk){if(!result.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting){result.Dispose();return null;}chunk.InProgressSaving=true;}
            try{if(!result.Current()){result.Dispose();return null;}regions.SaveChunkSnapshot(chunk,true);}finally{lock(chunk)chunk.InProgressSaving=false;}
            return result;
        }
        catch{result.Dispose();return null;}
    }
    internal bool Saved()
    {
        if(!Current()||span==null||!RebirthStationSnapshotEvidence.TryCopy(request,out var bytes)||!RebirthStationSnapshotEvidence.HasPublished(request)||offset<8||offset>bytes.Length-span.Length)return false;
        for(int i=0;i<span.Length;i++)if(bytes[(int)offset+i]!=span[i])return false;return Current();
    }
    public void Dispose(){if(closed)return;closed=true;request?.Dispose();pending.Remove(this);}
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.composite.publication.v1");
        try{RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Write));ModEvents.GameUpdate.RegisterHandler(Update);installed=true;}catch{harmony.UnpatchSelf();throw;}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {foreach(var item in pending.ToArray())try{if(!item.Current())item.Dispose();}catch{item.Dispose();}}
    [HarmonyPatch(typeof(TileEntityComposite),"write")]
    private static class Write
    {
        private static void Prefix(TileEntityComposite __instance,PooledBinaryWriter _bw,StreamModeWrite _eStreamMode,out long __state)
        {__state=_eStreamMode==StreamModeWrite.Persistency?RebirthStationSnapshotEvidence.StartStationSpan(__instance,_bw.BaseStream):-1;}
        private static void Postfix(TileEntityComposite __instance,PooledBinaryWriter _bw,long __state)
        {
            if(__state<8)return;
            try{foreach(var item in pending)if(ReferenceEquals(item.tile,__instance)&&item.Current())
            {
                var stream=_bw.BaseStream;long end=stream.Position,length=end-__state;if(length<=0||length>512*1024||!stream.CanRead||!stream.CanSeek)return;
                var bytes=new byte[(int)length];try{stream.Position=__state;int copied=0;while(copied<bytes.Length){int count=stream.Read(bytes,copied,bytes.Length-copied);if(count<=0)return;copied+=count;}}finally{stream.Position=end;}
                item.offset=__state;item.span=bytes;
            }}catch{ /* Failed evidence cannot change native persistence. */ }
        }
    }
}

// Native DroneManager is a separate persistence owner from VehicleManager.
// Observe only final native drones.dat records; never load/apply a replacement.
internal static class RebirthLogisticsDronePublication
{
    private const string IncarnationKey="rebirth.logistics.incarnation";
    private static readonly List<Expected> waiting=new List<Expected>();
    [ThreadStatic] private static DroneManager writing;
    private static bool installed;
    internal sealed class Expected:IDisposable
    {
        internal EntityDrone Drone;internal World World;internal DroneManager Manager;
        internal string Directory,Incarnation;internal int Id,Class,Owner;internal byte[] Bag,Entity;
        internal DateTime Expires;internal bool Closed;
        public void Dispose(){if(Closed)return;Closed=true;waiting.Remove(this);}
    }
    // Called only from authoritative initial admission, before pure planning or
    // journal creation. An uncertain saved identity is retained, never replaced.
    internal static Expected BeginIdentityAdmission(EntityDrone drone,Func<bool> authorizedCurrent)
    {
        if(!installed||RebirthLogisticsPlanning.IsActive||!ThreadManager.IsMainThread()||drone?.world?.IsRemote()!=false||authorizedCurrent?.Invoke()!=true||
            !ReferenceEquals(GameManager.Instance?.World,drone.world)||!ReferenceEquals(drone.world.GetEntity(drone.entityId),drone)||drone.OriginalItemValue==null)return null;
        if(!drone.OriginalItemValue.HasMetadata(IncarnationKey))
        {
            if(drone.OriginalItemValue.Metadata?.Count>=byte.MaxValue)return null;
            drone.OriginalItemValue.SetMetadata(IncarnationKey,Guid.NewGuid().ToString("N"));
            drone.SendSyncData((ushort)128);
        }
        return Begin(drone,authorizedCurrent);
    }
    private static string Identity(EntityDrone drone)
    {
        return drone?.OriginalItemValue?.TryGetMetadata(IncarnationKey,out string text)==true&&Guid.TryParseExact(text,"N",out var id)&&id!=Guid.Empty?text:null;
    }
    internal static string CurrentIncarnation(EntityDrone drone)
        =>ThreadManager.IsMainThread()&&drone?.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,drone.world)&&
            ReferenceEquals(drone.world.GetEntity(drone.entityId),drone)&&!drone.IsDead()?Identity(drone):null;
    internal static Expected Begin(EntityDrone drone,Func<bool> current)
    {
        if(!installed||!ThreadManager.IsMainThread()||drone?.world?.IsRemote()!=false||current?.Invoke()!=true||
            !ReferenceEquals(GameManager.Instance?.World,drone.world)||!ReferenceEquals(drone.world.GetEntity(drone.entityId),drone))return null;
        string incarnation=Identity(drone);if(incarnation==null)return null;
        foreach(var stale in waiting.ToArray())if(stale.Closed||stale.Expires<=DateTime.UtcNow||!ReferenceEquals(stale.World,drone.world))stale.Dispose();if(waiting.Count>=16)return null;
        var manager=DroneManager.Instance;if(manager==null)return null;
        // Native GetDrones constructs ECD with its default ToClient inner codec,
        // then writes that record to drones.dat using Persistency. Match that
        // actual producer rather than inventing a different inner bag codec.
        var native=new EntityCreationData(drone);if(native.bagData==null)return null;
        var expected=new Expected{Drone=drone,World=drone.world,Manager=manager,Directory=GameIO.GetSaveGameDir(),Id=native.id,Class=native.entityClass,Owner=native.belongsPlayerId,Bag=native.bagData.ToArray(),Incarnation=incarnation,Expires=DateTime.UtcNow.AddSeconds(30)};
        if(string.IsNullOrEmpty(expected.Directory))return null;
        waiting.Add(expected);
        // Schedule its normal single writer through Update. Do not start Save
        // directly while another pooled writer is registering/running.
        manager.TriggerSave();manager.saveTime=0f;return expected;
    }
    internal static bool Verify(Expected expected,Func<bool> current)
    {
        if(!ThreadManager.IsMainThread()||expected==null||expected.Closed||expected.Entity==null||expected.Expires<=DateTime.UtcNow||current?.Invoke()!=true||
            !ReferenceEquals(GameManager.Instance?.World,expected.World)||!ReferenceEquals(DroneManager.Instance,expected.Manager)||
            !ReferenceEquals(expected.World.GetEntity(expected.Id),expected.Drone)||expected.Drone.IsDead()||GameIO.GetSaveGameDir()!=expected.Directory||
            Identity(expected.Drone)!=expected.Incarnation||
            expected.Manager.saveThread!=null&&!expected.Manager.saveThread.HasTerminated())return false;
        try
        {
            string path=Path.Combine(expected.Directory,"drones.dat");if(!SdFile.Exists(path))return false;bool found=false;
            using(var stream=SdFile.OpenRead(path))using(var reader=MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if(reader.ReadChar()!='v'||reader.ReadChar()!='d'||reader.ReadChar()!='a'||reader.ReadChar()!='\0'||reader.ReadByte()!=1)return false;
                int count=reader.ReadInt32();if(count<0||count>65536)return false;
                for(int i=0;i<count;i++)
                {
                    var native=new EntityCreationData();native.read(reader,StreamModeRead.Persistency);if(native.id!=expected.Id)continue;
                    if(found||native.entityClass!=expected.Class||native.belongsPlayerId!=expected.Owner||native.bagData==null||!native.bagData.ToArray().SequenceEqual(expected.Bag)||
                        native.entityData==null||!native.entityData.ToArray().SequenceEqual(expected.Entity))return false;found=true;
                }
                if(stream.Position!=stream.Length)return false;
            }
            return found&&current()&&ReferenceEquals(GameManager.Instance?.World,expected.World)&&GameIO.GetSaveGameDir()==expected.Directory;
        }
        catch{return false;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.logistics.drone.publication.v1");
        try{RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Writer));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(Snapshot));installed=true;}
        catch{harmony.UnpatchSelf();throw;}
    }
    [HarmonyPatch(typeof(DroneManager),"write")]
    private static class Writer
    {
        private static void Prefix(DroneManager __instance,out DroneManager __state){__state=writing;writing=__instance;}
        private static void Finalizer(DroneManager __state){writing=__state;}
    }
    [HarmonyPatch(typeof(DroneManager),"GetDrones")]
    private static class Snapshot
    {
        private static void Postfix(DroneManager __instance,List<EntityCreationData> _list)
        {
            try
            {
            // Observe the exact snapshot that native write will emit, rather than
            // requiring moving drone state to equal an earlier unrelated snapshot.
            if(!ReferenceEquals(writing,__instance)||!ThreadManager.IsMainThread())return;
            foreach(var expected in waiting)
            {
                if(expected.Expires<=DateTime.UtcNow||!ReferenceEquals(expected.Manager,__instance)||Identity(expected.Drone)!=expected.Incarnation||
                    !ReferenceEquals(GameManager.Instance?.World,expected.World)||!ReferenceEquals(expected.World.GetEntity(expected.Id),expected.Drone)||GameIO.GetSaveGameDir()!=expected.Directory)continue;
                var records=_list.Where(item=>item.id==expected.Id).ToArray();if(records.Length!=1)continue;var native=records[0];
                if(native.entityClass!=expected.Class||native.belongsPlayerId!=expected.Owner||native.bagData==null||native.entityData==null||!native.bagData.ToArray().SequenceEqual(expected.Bag))continue;
                expected.Entity=native.entityData.ToArray();
            }
            }
            catch{ /* Evidence failure cannot change the native save result. */ }
        }
    }
}



// Original logistics dispatcher core. The transport binds the authenticated
// native actor; no caller-supplied slot images enter this state machine.
internal sealed class RebirthLogisticsServerOperation:IDisposable
{
    private readonly EntityPlayer actor;private readonly World world;private readonly RebirthStablePlayerIdentity owner;
    private readonly string binding,creation,directory;private readonly Guid operation;private readonly int deaths;private readonly DateTime unpaidDeadline;
    private readonly Dictionary<string,RebirthRemoteResourceSourceCustody.Lease> native=new Dictionary<string,RebirthRemoteResourceSourceCustody.Lease>(StringComparer.Ordinal);
    private RebirthNpcOriginalEffectAdmission.Lease npcAdmission;private bool closed;
    private RebirthLogisticsTransferJournal.Record record;
    private readonly ConnectionManager manager;private readonly ClientInfo peer;private readonly INetConnection connection;
    private RebirthLogisticsServerOperation(EntityPlayer actor,RebirthStablePlayerIdentity owner,RebirthLogisticsTransferJournal.Record original)
    {
        this.actor=actor;world=actor.world;this.owner=owner;record=original;operation=original.Operation;creation=original.Creation;deaths=original.Deaths;
        binding=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);directory=GameIO.GetSaveGameDir();unpaidDeadline=DateTime.UtcNow.AddSeconds(120);
        manager=SingletonMonoBehaviour<ConnectionManager>.Instance;RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out peer,out connection);
    }
    private bool Current()=>!closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&!world.IsRemote()&&GameIO.GetSaveGameDir()==directory&&
        ReferenceEquals(world.GetEntity(actor.entityId),actor)&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager)&&
        RebirthLogisticsOriginalTransport.TryCurrentPeer(actor,out var livePeer,out var liveConnection)&&ReferenceEquals(livePeer,peer)&&ReferenceEquals(liveConnection,connection)&&
        (RebirthLogisticsDeathSettlement.ActorCurrent(actor,record)||RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,record));
    private bool Refresh()=>Current()&&RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,operation,out record)&&RebirthLogisticsTransferJournal.ImmutablePlanDigest(record)==binding;
    internal static bool TryBind(EntityPlayer actor,RebirthLogisticsTransferJournal.Record original,out RebirthLogisticsServerOperation server)
    {
        server=null;
        try
        {
            if(actor?.world?.IsRemote()!=false||original==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||owner.StorageKey!=original.Owner||
                !RebirthLogisticsTransferJournal.TryRead(owner.StorageKey,original.Operation,out var saved)||RebirthLogisticsTransferJournal.Encode(saved)!=RebirthLogisticsTransferJournal.Encode(original))return false;
            var candidate=new RebirthLogisticsServerOperation(actor,owner,saved);if(!candidate.Current())return false;server=candidate;return true;
        }
        catch{return false;}
    }
    private bool ReserveNative()
    {
        // Cold bootstrap reconstructs only existing durable original claims.
        // Later-death binding below never Add/reserves/prunes a fresh claim.
        if(!RebirthNpcOriginalEffectAdmission.Bootstrap(world))return false;
        foreach(var leg in record.Legs.Where(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage))
        {
            if(!RebirthRemoteResourceSourceCustody.TryReserveLogistics(actor,record,leg.Id,Current,out var lease))return false;
            native[leg.Id]=lease;
        }
        return (RebirthLogisticsDeathSettlement.MatchesPaidSourceContinuation(actor,record)?RebirthNpcOriginalEffectAdmission.TryRebindPaidSourceOriginal(actor,record,out npcAdmission):RebirthNpcOriginalEffectAdmission.TryReserveLogistics(actor,record,out npcAdmission))&&npcAdmission.Current();
    }
    private bool PreflightUnpaid()
    {
        if(!ReserveNative())return false;
        foreach(var leg in record.Legs.Where(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage))if(!native[leg.Id].TryObserveSaved(0))return false;
        foreach(var npc in record.Npcs)
        {
            if(!RebirthNpcStableId.TryParse(npc.StableId,out var id)||!RebirthNpcInventoryTransactionService.CanAdmitLogisticsOriginalPlan(actor,record,id)||
                !RebirthLogisticsTransferProtocol.TryObserveNpcBefore(record,npc.StableId,Current,out _))return false;
        }
        foreach(var leg in record.Legs.Where(value=>value.Kind==RemoteResourceSourceKind.NpcStorage))if(!RebirthLogisticsDogTransfer.CanAdmit(actor,record,leg.Id))return false;
        return Current()&&RebirthLogisticsOwnerReceipt.TrySaved(owner,operation,0,out var saved)&&saved.deaths==deaths;
    }
    private bool Good(RebirthNpcInventoryTransactionResult value)=>value==RebirthNpcInventoryTransactionResult.Applied||value==RebirthNpcInventoryTransactionResult.Replayed;
    private bool NpcTranche()
    {
        var ids=new List<RebirthNpcStableId>();
        foreach(var npc in record.Npcs){if(!RebirthNpcStableId.TryParse(npc.StableId,out var id))return false;ids.Add(id);}
        foreach(var dog in record.Legs.Where(value=>value.Kind==RemoteResourceSourceKind.NpcStorage)){if(!RebirthLogisticsDogTransfer.Original(dog,out var id,out _))return false;ids.Add(id);}
        if(ids.Count==0)return true;
        using(var authority=RebirthNpcInventoryAuthorityService.Issue("quickstack.original-logistics",RebirthNpcInventoryAuthorityOperations.Transfer,TimeSpan.FromSeconds(30),ids.Distinct().ToArray()))
        {
            if(record.Npcs.Any(value=>value.Attempted>0))RebirthNpcInventoryPersistenceStore.Save();
            if(record.Legs.Any(value=>value.Kind==RemoteResourceSourceKind.NpcStorage&&value.Attempted>0))RebirthNpcAggregatePersistenceStore.Save();
            // Complete every NPC preflight before the first irreversible NPC
            // effect. All subsequent debit/save/credit/save calls stay in this
            // synchronous main-thread tranche; no scheduled callback/yield.
            foreach(var npc in record.Npcs.Where(value=>value.Published<2))
            {
                RebirthNpcStableId.TryParse(npc.StableId,out var id);
                if(!Good(RebirthNpcInventoryTransactionService.ApplyLogisticsOriginalPlan(actor,record,id,npc.Published>=1,authority.AuthorityKey,out _,true)))return false;
                var snapshot=RebirthNpcInventoryTransactionService.GetSnapshot(id);
                if(npc.Published==0&&snapshot?.LogisticsEffects!=null&&snapshot.LogisticsEffects.TryGetValue(npc.DebitId,out var debit)&&
                    RebirthLogisticsNpcEffectReceipt.MatchesLogistics(record,npc,false,debit)&&
                    !Good(RebirthNpcInventoryTransactionService.ApplyLogisticsOriginalPlan(actor,record,id,true,authority.AuthorityKey,out _,true)))return false;
            }
            foreach(var dog in record.Legs.Where(value=>value.Kind==RemoteResourceSourceKind.NpcStorage&&value.Published<2))
            {
                if(!RebirthLogisticsDogTransfer.TryApply(actor,record,dog.Id,dog.Published>=1,authority.AuthorityKey,Current,out _,out _,true))return false;
                if(dog.Published==0&&RebirthLogisticsDogTransfer.Original(dog,out var id,out _)&&RebirthNpcAggregatePersistenceStore.TryGet(id,out var row)&&
                    RebirthLogisticsDogTransfer.TryReceipt(row,out var receipt)&&RebirthLogisticsDogTransfer.MatchesReceipt(record,dog,false,receipt,out _)&&
                    !RebirthLogisticsDogTransfer.TryApply(actor,record,dog.Id,true,authority.AuthorityKey,Current,out _,out _,true))return false;
            }
            for(int stage=1;stage<=2;stage++)
            {
                bool credit=stage==2;
                foreach(string stableId in record.Npcs.Where(value=>value.Published<stage).Select(value=>value.StableId).ToArray())
                {
                    if(!RebirthLogisticsTransferProtocol.PrepareLegAttempt(record,"N:"+stableId,true,credit,Current,out record)||!RebirthNpcStableId.TryParse(stableId,out var id)||
                        !Good(RebirthNpcInventoryTransactionService.ApplyLogisticsOriginalPlan(actor,record,id,credit,authority.AuthorityKey,out _)))return false;
                    RebirthNpcInventoryPersistenceStore.Save();
                    if(!RebirthLogisticsTransferProtocol.ConfirmNpcPublished(record,stableId,credit,Current,out record))return false;
                }
                foreach(string legId in record.Legs.Where(value=>value.Kind==RemoteResourceSourceKind.NpcStorage&&value.Published<stage).Select(value=>value.Id).ToArray())
                {
                    if(!RebirthLogisticsTransferProtocol.PrepareLegAttempt(record,legId,false,credit,Current,out record)||
                        !RebirthLogisticsDogTransfer.TryApply(actor,record,legId,credit,authority.AuthorityKey,Current,out _,out _))return false;
                    RebirthNpcAggregatePersistenceStore.Save();
                    if(!RebirthLogisticsTransferProtocol.ConfirmDogPublished(record,legId,credit,Current,out record))return false;
                }
            }
        }
        return true;
    }
    private bool AbortPaid()
    {
        if(record.Npcs.Any(value=>value.Attempted!=0)||record.Legs.Any(value=>value.Attempted>1||value.Kind==RemoteResourceSourceKind.NpcStorage&&value.Attempted!=0))return false;
        if(!record.Rejecting&&!RebirthLogisticsTransferProtocol.PreparePaidAbort(record,Current,out record))return false;
        if(!ReserveNative())return false;
        foreach(string id in record.Legs.Where(value=>value.Attempted>0&&!value.Restored).Select(value=>value.Id).ToArray())
        {
            var lease=native[id];
            if(!lease.TryObserveSaved(3)&&!lease.TryRestoreOriginalBefore())return false;
            if(!RebirthLogisticsTransferProtocol.ConfirmRestoredLeg(record,id,lease,Current,out record))return false;
        }
        return RebirthLogisticsTransferProtocol.ConfirmAbort(record,Current,out record);
    }
    internal bool RequestAbort()
    {
        if(!Refresh()||record.Phase>1)return false;
        return string.IsNullOrEmpty(record.OwnerDebitProof)?RebirthLogisticsTransferProtocol.PrepareRejection(record,Current,out record):
            RebirthLogisticsTransferProtocol.PreparePaidAbort(record,Current,out record);
    }
    internal bool Step(out RebirthLogisticsTransferJournal.Record original)
    {
        original=null;
        try
        {
            if(!Refresh())return false;
            if(record.Phase==4){original=record;Dispose();return true;}
            if(record.Phase==0)
            {
                if(!PreflightUnpaid())
                {
                    if(DateTime.UtcNow>=unpaidDeadline)RebirthLogisticsTransferProtocol.PrepareRejection(record,Current,out record);
                    Refresh();original=record;return false;
                }
                record.Phase=1;if(!RebirthLogisticsTransferJournal.TryPublish(record,Current)){Refresh();original=record;return false;}
                original=record;return false;
            }
            if(record.Phase==1)
            {
                if(record.Rejecting&&string.IsNullOrEmpty(record.OwnerDebitProof)&&RebirthLogisticsTransferProtocol.ConfirmNoEffect(owner,record,Current,out var unpaid))record=unpaid;
                if(record.Phase==1&&string.IsNullOrEmpty(record.OwnerDebitProof)&&!RebirthLogisticsTransferProtocol.ConfirmOwnerDebit(owner,record,Current,out record)){Refresh();original=record;return false;}
                if(record.Phase==1)
                {
                    if(record.Rejecting||record.Legs.Any(value=>value.Restored)){AbortPaid();Refresh();original=record;return false;}
                    if(!ReserveNative()){original=record;return false;}
                    foreach(string id in record.Legs.Where(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage&&value.Published<1).Select(value=>value.Id).ToArray())
                    {
                        if(!RebirthLogisticsTransferProtocol.PrepareLegAttempt(record,id,false,false,Current,out record)||!native[id].ApplyStage(1)||
                            !RebirthLogisticsTransferProtocol.ConfirmNativePublished(record,id,false,native[id],Current,out record)){Refresh();original=record;return false;}
                    }
                    if(!NpcTranche())
                    {
                        if(Refresh()&&record.Npcs.All(value=>value.Attempted==0)&&record.Legs.All(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage||value.Attempted==0))AbortPaid();
                        Refresh();original=record;return false;
                    }
                    foreach(string id in record.Legs.Where(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage&&value.Published<2).Select(value=>value.Id).ToArray())
                    {
                        if(!RebirthLogisticsTransferProtocol.PrepareLegAttempt(record,id,false,true,Current,out record)||!native[id].ApplyStage(2)||
                            !RebirthLogisticsTransferProtocol.ConfirmNativePublished(record,id,true,native[id],Current,out record)){Refresh();original=record;return false;}
                    }
                    if(!RebirthLogisticsTransferProtocol.ConfirmEndpointsPublished(record,Current,out record)){Refresh();original=record;return false;}
                }
            }
            if(record.Phase==2||record.Phase==3)RebirthLogisticsTransferProtocol.ConfirmOwnerCompletion(owner,record,Current,out record);
            Refresh();original=record;if(record?.Phase==4){Dispose();return true;}return false;
        }
        catch(Exception error){Log.Warning("[QuickStack] Original transfer retained for reconciliation: "+error.Message);Refresh();original=record;return false;}
    }
    public void Dispose(){if(closed)return;foreach(var lease in native.Values)lease.Dispose();npcAdmission?.Dispose();closed=true;}
}
