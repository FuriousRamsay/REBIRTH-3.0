using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;

// Shared native endpoint custody for two named original domains only. Player
// bags are never endpoints. Unknown originals remain evidence, not snapshots
// that can be replayed into whatever inventory happens to occupy an old ID.
internal static class RebirthRemoteResourceSourceCustody
{
    private sealed class ReferenceComparer:IEqualityComparer<object>
    {public new bool Equals(object a,object b)=>ReferenceEquals(a,b);public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);}
    private static readonly Dictionary<object,Lease> held=new Dictionary<object,Lease>(new ReferenceComparer());
    private static readonly List<RebirthRemoteResourceNativeOriginal> originals=new List<RebirthRemoteResourceNativeOriginal>();
    private static World world;private static string directory;private static bool installed,ready,bootstrapping;
    private static DateTime retryAfter,terminalAfter;
    [ThreadStatic]private static Permit permit;
    private sealed class Permit
    {internal Lease Lease;internal int Slot;internal ItemStack Desired;internal ItemStack[] Array;internal bool Consumed;}
    internal sealed class Lease:IDisposable
    {
        internal readonly RebirthRemoteResourceNativeOriginal Original;
        internal RebirthRemoteResourceNativeBinding Source {get;private set;}
        internal ItemStackGrid Grid {get;private set;}
        private EntityPlayer actor;private Func<bool> authorized;
        private Func<bool> historicalCookingRestoration;
        private bool uninterrupted,restoredWithoutEffect;
        private byte[] ledgerImage;
        private sealed class SavedBeforeEvent
        {
            internal readonly RebirthRemoteResourceNativeOriginal Original;internal readonly object Endpoint;
            internal readonly string Proof;internal readonly byte[] Ledger;
            internal SavedBeforeEvent(RebirthRemoteResourceNativeOriginal original,object endpoint,string proof,byte[] ledger)
            {Original=original;Endpoint=endpoint;Proof=proof;Ledger=(byte[])ledger.Clone();}
        }
        private SavedBeforeEvent savedBefore;
        private SavedBeforeEvent savedDebit;
        private RebirthRemoteResourceNativePublication.Watch watch;private int watchStage=-1;
        internal Lease(RebirthRemoteResourceNativeOriginal original,RebirthRemoteResourceNativeBinding source)
        {Original=original;Source=source;Grid=source.Grid;}
        internal bool OwnsEndpoint()=>Scope()&&Source!=null&&!Original.GraphRetired&&Source.Current()&&held.TryGetValue(Source.Endpoint,out var current)&&ReferenceEquals(current,this);
        internal bool Current()=>OwnsEndpoint()&&(Grid==null||ReferenceEquals(Source.Grid,Grid));
        internal bool HasActiveObservation=>watch!=null&&!watch.Closed;
        private bool Authority()
        {
            if(!Current()||actor==null||authorized?.Invoke()!=true||(!Original.ActorCurrent(actor)&&historicalCookingRestoration?.Invoke()!=true)||!Source.IsAuthorized(actor,out _))return false;
            float radius=Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource?RemoteResourcesRuntimePolicy.Radius:
                Source.Drone!=null?30f:QuickStackService.Radius;
            var position=actor.position;
            if(Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork)
            {
                if(!RebirthNpcWorkNativeTransferOriginal.TryRead(Original.Owner,Original.Operation,Original.SourceId,out var work)||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(work.Npc,out int npcId)||
                    actor.world.GetEntity(npcId) is not EntityRebirthHumanoidNPC npc||npc.IsDead())return false;
                position=npc.position;radius=RebirthCompanionService.StorageDistance;
            }
            if(Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit)
            {
                if(!RebirthNpcWorkNativeCreditOriginal.TryRead(Original.Owner,Original.Operation,
                    Original.SourceId,out var credit)||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(credit.Npc,out int npcId)||
                    actor.world.GetEntity(npcId) is not EntityRebirthHumanoidNPC npc||npc.IsDead())return false;
                position=npc.position;radius=RebirthCompanionService.StorageDistance;
            }
            if(Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting||
                Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)
                radius=Constants.cCollectItemDistance+30f;
            var delta=Source.SourcePosition-position;double distance=(double)delta.x*delta.x+(double)delta.y*delta.y+(double)delta.z*delta.z;
            return radius>0f&&!float.IsNaN(radius)&&!float.IsInfinity(radius)&&!double.IsNaN(distance)&&!double.IsInfinity(distance)&&distance<=(double)radius*radius;
        }
        internal bool BindActor(EntityPlayer value,Func<bool> current)
        {
            if(!Current()||current?.Invoke()!=true||!Original.ActorCurrent(value)||!Source.IsAuthorized(value,out _)||!Original.Current(out _))return false;
            actor=value;authorized=current;
            if(Original.AttemptedStage()==0&&Same(Source.CopySlots(),Original.CopyStage(0))&&Locks()&&TryReadLedger(out var bytes,out _)&&
                RebirthRemoteResourceSourceEffectCodec.TryRead(bytes,Original.Incarnation,out var effects)&&!effects.Any(effect=>effect.Purpose==Original.Domain&&effect.Operation==Original.Operation))uninterrupted=true;
            return true;
        }
        private bool Locks()=>Source.CopyLocks()?.SequenceEqual(Original.CopyLocks())==true;
        private string MarkerKey()
        {var local=(Source.Endpoint as TileEntity).localChunkPos;return "RebirthOriginalSourceV2:"+local.x+":"+local.y+":"+local.z+":"+Original.Incarnation;}
        private ItemValue MetadataOwner()=>Source.Vehicle?.vehicle?.GetUpdatedItemValue()??Source.Drone?.OriginalItemValue;
        private bool TryReadLedger(out byte[] image,out bool absent)
        {
            image=null;absent=false;if(!Current())return false;
            if(Source.Endpoint is TileEntity tile)
            {
                var chunk=tile.GetChunk();if(chunk==null||chunk.InProgressUnloading)return false;
                if(!chunk.ChunkCustomData.dict.TryGetValue(MarkerKey(),out var marker)){absent=true;image=RebirthRemoteResourceSourceEffectCodec.Empty(Original.Incarnation);return true;}
                if(marker.TriggerWriteDataDelegate!=null||marker.expiresInWorldTime!=ulong.MaxValue||marker.isSavedToNetwork||marker.data==null)return false;
                image=(byte[])marker.data.Clone();
            }
            else
            {
                var item=MetadataOwner();if(item==null)return false;
                if(!item.HasMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey)){absent=true;image=RebirthRemoteResourceSourceEffectCodec.Empty(Original.Incarnation);return true;}
                if(!item.TryGetMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey,out string text)||text.Length>32768)return false;
                image=Convert.FromBase64String(text);
            }
            return RebirthRemoteResourceSourceEffectCodec.TryRead(image,Original.Incarnation,out _);
        }
        private bool WriteLedger(byte[] expected,byte[] next)
        {
            if(!Current()||!TryReadLedger(out var live,out _)||!live.SequenceEqual(expected)||!RebirthRemoteResourceSourceEffectCodec.TryRead(next,Original.Incarnation,out _))return false;
            if(Source.Endpoint is TileEntity tile)
            {
                var chunk=tile.GetChunk();string key=MarkerKey();
                if(!chunk.ChunkCustomData.dict.TryGetValue(key,out var marker))
                {marker=new ChunkCustomData(key,ulong.MaxValue,false){data=(byte[])next.Clone()};chunk.ChunkCustomData.Set(key,marker);}
                else marker.data=(byte[])next.Clone();
                chunk.isModified=true;
            }
            else
            {
                var item=MetadataOwner();if(item==null||!item.HasMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey)&&item.Metadata?.Count>=byte.MaxValue)return false;
                item.SetMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey,Convert.ToBase64String(next));
                if(Source.Vehicle!=null)Source.Vehicle.SendSyncData((ushort)4);else Source.Drone.SendSyncData((ushort)128);
            }
            ledgerImage=(byte[])next.Clone();return TryReadLedger(out live,out _)&&live.SequenceEqual(next);
        }
        private bool Terminal(RebirthRemoteResourceSourceEffectCodec.Effect effect)
            =>RebirthRemoteResourceNativeOriginal.TryRead(effect.Purpose,effect.Owner,effect.Operation,Original.SourceId,out var previous)&&
                previous.Binding==effect.Binding&&previous.Incarnation==Original.Incarnation&&previous.StageDigest(effect.Stage)==effect.Delta&&previous.SavedTerminal()||
                // A dog keeps ONE row, replaced by its next operation: once no dog record carries the
                // operation id at all (and the pending set was read coherently) nothing can ever replay
                // that effect, so it is retired instead of filling the 128-receipt ledger.
                effect.Purpose==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting&&ready&&
                RebirthDogSmartLootOriginal.OperationAbsent(effect.Operation);
        private bool PrepareLedger()
        {
            if(!Authority()||!Original.Current(out _)||!Same(Source.CopySlots(),Original.CopyStage(0))||!Locks()||!TryReadLedger(out var bytes,out bool absent)||
                !RebirthRemoteResourceSourceEffectCodec.TryRetire(bytes,Original.Incarnation,Terminal,out var pruned)||
                !RebirthRemoteResourceSourceEffectCodec.TryRead(pruned,Original.Incarnation,out var entries))return false;
            var own=entries.SingleOrDefault(effect=>effect.Purpose==Original.Domain&&effect.Operation==Original.Operation);
            if(own!=null||entries.Length>=RebirthRemoteResourceSourceEffectCodec.MaximumReceipts)return false;
            if(absent||!bytes.SequenceEqual(pruned)){if(!WriteLedger(bytes,pruned))return false;}else ledgerImage=(byte[])bytes.Clone();
            return true;
        }
        private bool Effect(int stage,out byte[] image)
        {
            image=null;if(!TryReadLedger(out var bytes,out bool absent)||absent||!RebirthRemoteResourceSourceEffectCodec.TryRead(bytes,Original.Incarnation,out var entries))return false;
            var effect=entries.SingleOrDefault(value=>value.Purpose==Original.Domain&&value.Operation==Original.Operation);
            if(effect==null||effect.Owner!=Original.Owner||effect.Binding!=Original.Binding||effect.Stage!=stage||effect.Delta!=Original.StageDigest(stage))return false;
            image=bytes;return true;
        }
        internal bool ApplyStage(int stage)
        {
            try
            {
                if(stage<1||stage>2||!Authority()||!Locks())return false;
                if(stage==2&&Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit&&
                    RebirthNpcWorkNativeCreditOriginal.TryRead(Original.Owner,Original.Operation,Original.SourceId,out var credit)&&
                    credit.Binding==Original.Binding&&credit.WriteAhead)return ApplyNpcWorkCreditWriteAhead();
                if(Effect(stage,out var committed)){ledgerImage=committed;return Same(Source.CopySlots(),Original.CopyStage(stage));}
                if(!Original.MayApply(stage))return false;
                var from=Original.CopyStage(stage-1);var to=Original.CopyStage(stage);
                if(!Same(Source.CopySlots(),from)||!TryReadLedger(out var prior,out bool absent)||absent)return false;
                if(stage==1&&!uninterrupted&&!(
                    (Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork||
                     Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit||
                     Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting||
                     // Cold dog attempt after a restart: the same stage-0 saved observation of THIS lease,
                     // with the row saved at phase 1/attempted 1, is the only allowance (MayApply re-checks).
                     Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)&&
                    savedBefore!=null&&ReferenceEquals(savedBefore.Endpoint,Source.Endpoint)&&
                    savedBefore.Original.SameOriginal(Original)&&
                    TryReadLedger(out var workLedger,out bool workAbsent)&&!workAbsent&&
                    workLedger.SequenceEqual(savedBefore.Ledger)&&
                    Same(Source.CopySlots(),Original.CopyStage(0))))return false;
                if(stage==2&&!Effect(1,out _))return false;
                if(!RebirthRemoteResourceSourceEffectCodec.TryPlan(prior,Original.Incarnation,Original.Domain,Original.Operation,Original.Owner,Original.Binding,stage,Original.StageDigest(stage),out var next))return false;
                watch?.Dispose();watch=null;watchStage=-1;
                for(int index=0;index<to.Length;index++)
                {
                    if(!Authority()||!Locks()||!Mixed(Source.CopySlots(),from,to))return false;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(Source.CopySlots()[index],to[index]))continue;
                    var desired=to[index].Clone();var array=Source.CopySlots();array[index]=desired.Clone();
                    var one=new Permit{Lease=this,Slot=index,Desired=desired,Array=array};if(permit!=null)return false;
                    permit=one;try{Source.SetSlot(index,desired);}finally{permit=null;}
                    if(!one.Consumed||!Current()||!RebirthStationGridIngredients.IsSameStackSnapshot(Source.CopySlots()[index],desired))return false;
                }
                // Only a completed actual original delta can stamp an effect.
                // Partial throws have no invented marker; the original hold
                // remains, and only the narrow restoration path may undo it.
                if(!Authority()||!Original.MayApply(stage)||!Same(Source.CopySlots(),to)||!Locks()||!WriteLedger(prior,next))return false;
                Source.MarkModified();return true;
            }
            catch{return false;}
        }
        // For new exact NPC credits the native operation marker is a write-
        // ahead intent. It is not completion: TryObserveSaved(2) still requires
        // the full actual After image and native publication before bag clear.
        private bool ApplyNpcWorkCreditWriteAhead()
        {
            if(!Authority()||!Locks()||!Original.MayApply(2))return false;
            var before=Original.CopyStage(1);var after=Original.CopyStage(2);
            bool entered=Effect(2,out var ledger);
            if(entered)
            {
                if(Same(Source.CopySlots(),after)){Source.MarkModified();return true;}
                if(!Same(Source.CopySlots(),before))return false;
            }
            else
            {
                // Old unmarked After and unknown partial writes do not acquire
                // authority from a matching shape or an attempted flag.
                if(!Same(Source.CopySlots(),before)||!Effect(1,out var prior)||
                    !RebirthRemoteResourceSourceEffectCodec.TryPlan(prior,Original.Incarnation,Original.Domain,
                        Original.Operation,Original.Owner,Original.Binding,2,Original.StageDigest(2),out ledger)||
                    !WriteLedger(prior,ledger))return false;
            }
            watch?.Dispose();watch=null;watchStage=-1;
            for(int index=0;index<after.Length;index++)
            {
                if(!Authority()||!Original.MayApply(2)||!Locks()||
                    !Same(Source.CopySlots(),before)||!Effect(2,out var live)||!live.SequenceEqual(ledger))return false;
                if(RebirthStationGridIngredients.IsSameStackSnapshot(before[index],after[index]))continue;
                var desired=after[index].Clone();var array=Source.CopySlots();array[index]=desired.Clone();
                var one=new Permit{Lease=this,Slot=index,Desired=desired,Array=array};if(permit!=null)return false;
                permit=one;try{Source.SetSlot(index,desired);}finally{permit=null;}
                // Valid workCredit has exactly one changed, initially empty
                // destination cell. No other image is replayed after a throw.
                if(!one.Consumed||!Authority()||!Locks()||!Same(Source.CopySlots(),after)||
                    !Effect(2,out live)||!live.SequenceEqual(ledger))return false;
                Source.MarkModified();return true;
            }
            return false;
        }
        internal bool TryRestoreOriginalBefore()
        {
            try
            {
                if(!Authority()||!Original.MayApply(3)||!Locks())return false;
                if(Effect(3,out var completed)){ledgerImage=completed;return Same(Source.CopySlots(),Original.CopyStage(3));}
                // Cold cancellation of a positively saved full original debit
                // returns that owed payment once. Absence/mixed images never
                // enter this branch and still require uninterrupted custody.
                if(!uninterrupted&&(!Same(Source.CopySlots(),Original.CopyStage(1))||!Effect(1,out _)||!TryObserveSaved(1)))return false;
                if(!Mixed(Source.CopySlots(),Original.CopyStage(0),Original.CopyStage(1))||
                    !TryReadLedger(out var prior,out bool absent)||absent)return false;
                var before=Original.CopyStage(0);var debited=Original.CopyStage(1);byte[] next=prior;
                if(Effect(1,out _))
                {if(!RebirthRemoteResourceSourceEffectCodec.TryPlan(prior,Original.Incarnation,Original.Domain,Original.Operation,Original.Owner,Original.Binding,3,Original.StageDigest(3),out next))return false;}
                else if(RebirthRemoteResourceSourceEffectCodec.TryRead(prior,Original.Incarnation,out var entries)&&entries.Any(effect=>effect.Purpose==Original.Domain&&effect.Operation==Original.Operation))return false;
                watch?.Dispose();watch=null;watchStage=-1;
                for(int index=0;index<before.Length;index++)
                {
                    if(!Authority()||!Mixed(Source.CopySlots(),before,debited)||!Locks())return false;
                    if(RebirthStationGridIngredients.IsSameStackSnapshot(Source.CopySlots()[index],before[index]))continue;
                    var desired=before[index].Clone();var array=Source.CopySlots();array[index]=desired.Clone();var one=new Permit{Lease=this,Slot=index,Desired=desired,Array=array};if(permit!=null)return false;
                    permit=one;try{Source.SetSlot(index,desired);}finally{permit=null;}if(!one.Consumed)return false;
                }
                if(!Original.MayApply(3)||!Same(Source.CopySlots(),before)||!WriteLedger(prior,next))return false;
                restoredWithoutEffect=!Effect(3,out _);Source.MarkModified();return true;
            }
            catch{return false;}
        }
        internal bool RestoreHistoricalCooking(EntityPlayer owner,Func<bool> current,out string proof)
        {
            proof=null;
            // Authority exists only during the existing restoration call. A
            // retained old actor callback cannot inherit this new-life scope.
            var previousActor=actor;var previousAuthorized=authorized;
            try
            {
                if(historicalCookingRestoration!=null||current?.Invoke()!=true||!Current()||
                    (!TryObserveSaved(3,out _)&&!TryObserveSaved(1,out _)))return false;
                actor=owner;authorized=current;historicalCookingRestoration=current;
                return TryRestoreOriginalBefore()&&TryObserveSaved(3,out proof);
            }
            finally{historicalCookingRestoration=null;actor=previousActor;authorized=previousAuthorized;}
        }
        internal bool TryObserveSaved(int stage)=>TryObserveSaved(stage,out _);
        internal bool TryObserveSaved(int stage,out string proof)
        {
            proof=null;
            try
            {
                if(stage<0||stage>3||!Current()||!Original.Current(out _)||!Locks())return false;
                if(stage==1&&savedDebit!=null&&SavedDebitCurrent(savedDebit.Proof)){proof=savedDebit.Proof;return true;}
                if(stage==0)
                {if(ledgerImage==null&&!PrepareLedger())return false;}
                else if(stage==3&&restoredWithoutEffect&&uninterrupted){if(!TryReadLedger(out ledgerImage,out _))return false;}
                else if(!Effect(stage,out ledgerImage))return false;
                byte[] expected=(byte[])ledgerImage.Clone();
                bool Exact()=>Current()&&Original.Current(out _)&&Same(Source.CopySlots(),Original.CopyStage(stage))&&Locks()&&TryReadLedger(out var bytes,out bool missing)&&!missing&&bytes.SequenceEqual(expected);
                if(!Exact())return false;
                if(watch!=null&&watchStage!=stage){watch.Dispose();watch=null;}
                if(watch==null)
                {watch=RebirthRemoteResourceNativePublication.Begin(Source,Exact,Source.Endpoint is TileEntity?new[]{MarkerKey()}:new string[0]);watchStage=stage;}
                if(watch?.Saved()!=true){if(watch!=null&&!watch.Current()){watch.Dispose();watch=null;}return false;}
                proof=RebirthRemoteResourceDurableJournal.Digest(Original.Binding+"|"+Original.StageDigest(stage)+"|"+Convert.ToBase64String(expected));
                if(!Exact()){proof=null;return false;}
                if(stage==0)savedBefore=new SavedBeforeEvent(Original,Source.Endpoint,proof,expected);
                if(stage==1)savedDebit=new SavedBeforeEvent(Original,Source.Endpoint,proof,expected);
                return true;
            }
            catch{proof=null;return false;}
        }
        internal void StopObservation(){watch?.Dispose();watch=null;}
        private bool SavedBeforeCurrent(string proof)
            =>savedBefore!=null&&savedBefore.Proof==proof&&ReferenceEquals(savedBefore.Endpoint,Source.Endpoint)&&
                savedBefore.Original.SameOriginal(Original)&&uninterrupted&&Original.AttemptedStage()==0&&Authority()&&Locks()&&
                Same(Source.CopySlots(),Original.CopyStage(0))&&TryReadLedger(out var current,out bool absent)&&!absent&&current.SequenceEqual(savedBefore.Ledger);
        internal bool ReleaseSavedBeforeObservation(string proof)
        {
            try
            {
                if(Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||!SavedBeforeCurrent(proof)||
                    !RebirthRemoteResourceDurableJournal.TryRead(Original.Owner,Original.Operation,out var root,out _)||root==null||root.Binding!=Original.Binding||root.Phase>1||
                    !root.HasSourceBeforeProof(Original.SourceId,proof))return false;
                // The source custody and positive private event remain alive.
                // Releasing demand serialization cannot grant a source setter.
                StopObservation();return true;
            }
            catch{return false;}
        }
        private bool SavedDebitCurrent(string proof)
            =>savedDebit!=null&&savedDebit.Proof==proof&&ReferenceEquals(savedDebit.Endpoint,Source.Endpoint)&&
                savedDebit.Original.SameOriginal(Original)&&Current()&&Original.Current(out _)&&Locks()&&
                Same(Source.CopySlots(),Original.CopyStage(1))&&Effect(1,out var ledger)&&ledger.SequenceEqual(savedDebit.Ledger);
        internal bool ReleaseSavedDebitObservation(string proof)
        {
            try
            {
                if(Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||!SavedDebitCurrent(proof)||
                    !RebirthRemoteResourceDurableJournal.TryRead(Original.Owner,Original.Operation,out var root,out bool unknown)||unknown||root==null||
                    root.Binding!=Original.Binding||root.Phase<1||root.Phase>3||!root.HasSourceDebitProof(Original.SourceId,proof))return false;
                // Actual saved stage1 event remains bound to this exclusive
                // native object/lease. A cold/new lease has no private event.
                StopObservation();return true;
            }
            catch{return false;}
        }
        // Read-only prepared capacity witness. No effect receipt is stamped by
        // planning its bytes; the actual ApplyStage permit remains mandatory.
        internal bool TryPreviewTileDebit(out TileEntity tile,out string key,out byte[] beforeLedger,out byte[] afterLedger)
        {
            tile=null;key=null;beforeLedger=afterLedger=null;
            try
            {
                if(Original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource||savedBefore==null||!SavedBeforeCurrent(savedBefore.Proof)||
                    !(Source.Endpoint is TileEntity actual)||
                    !Same(Source.CopySlots(),Original.CopyStage(0))||!TryReadLedger(out var prior,out bool absent)||absent||
                    !RebirthRemoteResourceSourceEffectCodec.TryPlan(prior,Original.Incarnation,Original.Domain,Original.Operation,Original.Owner,Original.Binding,1,Original.StageDigest(1),out var planned))return false;
                if(!Current())return false;tile=actual;key=MarkerKey();beforeLedger=(byte[])prior.Clone();afterLedger=(byte[])planned.Clone();return true;
            }
            catch{return false;}
        }
        public void Dispose()
        {
            StopObservation();
            if(Source==null)return;
            if(!Original.SavedTerminal())return; // Unknown disposal never unlocks.
            if(held.TryGetValue(Source.Endpoint,out var current)&&ReferenceEquals(current,this))held.Remove(Source.Endpoint);
            originals.RemoveAll(value=>value.SameOriginal(Original));
        }
        internal void DropTerminalReferences()
        {
            if(!Original.GraphRetired)return;
            StopObservation();
            if(Source!=null&&held.TryGetValue(Source.Endpoint,out var current)&&ReferenceEquals(current,this))held.Remove(Source.Endpoint);
            actor=null;authorized=null;ledgerImage=null;savedBefore=null;savedDebit=null;Source=null;Grid=null;
            uninterrupted=false;restoredWithoutEffect=false;watchStage=-1;
        }
    }
    internal static bool RetirePlacedTerminal(RebirthRemoteResourcePlacedStationOriginal.TerminalArchive receipt,IEnumerable<Lease> retained)
    {
        if(!Scope()||receipt==null||retained==null||!receipt.Current)return false;
        var leases=retained.Concat(held.Values.Where(value=>value.Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&value.Original.Operation==receipt.Root.Operation)).Distinct().ToArray();
        var graphs=leases.Select(value=>value.Original).Concat(originals.Where(value=>value.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&value.Operation==receipt.Root.Operation)).Distinct().ToArray();
        if(graphs.Any(value=>!value.MatchesTerminalArchive(receipt)))return false;
        foreach(var graph in graphs)if(!graph.RetireTerminalGraph(receipt))return false;
        foreach(var lease in leases)lease.DropTerminalReferences();
        originals.RemoveAll(value=>graphs.Contains(value));return graphs.All(value=>value.GraphRetired);
    }
    internal static bool RetirePlacedNeverEntered(RebirthRemoteResourcePlacedStationOriginal.NeverEnteredTerminal receipt)
    {
        if(!Scope()||receipt?.Current!=true)return false;
        var leases=held.Values.Where(value=>value.Original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&value.Original.Operation==receipt.Root.Operation).ToArray();
        var graphs=leases.Select(value=>value.Original).Concat(originals.Where(value=>value.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&value.Operation==receipt.Root.Operation)).Distinct().ToArray();
        if(graphs.Any(value=>!value.MatchesNeverEntered(receipt)))return false;
        foreach(var graph in graphs)if(!graph.RetireNeverEnteredGraph(receipt))return false;
        foreach(var lease in leases)lease.DropTerminalReferences();
        originals.RemoveAll(value=>graphs.Contains(value));return graphs.All(value=>value.GraphRetired);
    }
    // Historical cooking reconciliation observes only an existing entered
    // debit. It never binds a new actor, prepares a ledger or enters a setter.
    internal static bool TryObserveEnteredCookingDebit(RebirthRemoteResourceDurableJournal.Record expected,string id,out string proof,EntityPlayer actor=null,RebirthRemoteResourceClientOriginal.Intent craft=null)
    {
        proof=null;
        try
        {
            if(expected==null||expected.Phase!=1||(int)expected.Copy().Attribute("kind")!=(int)(craft==null?RemoteResourceClientOperation.CookingPull:craft.Kind)||
                craft!=null&&!RebirthRemoteResourceDurableOutcome.HasSavedHistoricalFetchAdmission(actor,craft,expected)||!Bootstrap()||
                !RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var current,out bool unknown)||unknown||current==null||
                current.Binding!=expected.Binding||current.Revision!=expected.Revision||
                !RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,expected.Owner,expected.Operation,id,out var original))return false;
            var source=current.Copy().Element("sources").Elements("source").Single(value=>(string)value.Attribute("id")==id);
            if((int)source.Attribute("attempted")!=1||(int)source.Attribute("published")!=0||((int?)source.Attribute("restored")??0)!=0||
                !RebirthRemoteResourceNativeBinding.TryResolve(world,id,original.Kind,original.Incarnation,out var binding)||
                !held.TryGetValue(binding.Endpoint,out var lease)||!lease.Original.SameOriginal(original))return false;
            return lease.TryObserveSaved(1,out proof);
        }
        catch{proof=null;return false;}
    }
    internal static bool TryRestoreHistoricalCookingDebit(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record expected,string id,Func<bool> current,out string proof,RebirthRemoteResourceClientOriginal.Intent craft=null)
    {
        proof=null;
        try
        {
            var network=RebirthRemoteResourceNetworkScope.Capture(actor);int deaths=actor?.Died??-1;
            bool Authenticated()=>network?.Current==true&&actor.Died==deaths&&!actor.IsDead()&&current?.Invoke()==true;
            if(expected==null||expected.Phase!=1||expected.HasOwnerAction||!Authenticated()||!Bootstrap()||
                !RebirthRemoteResourceDurableJournal.TryRead(expected.Owner,expected.Operation,out var saved,out bool unknown)||unknown||saved==null||
                saved.Revision!=expected.Revision||saved.Binding!=expected.Binding)return false;
            var root=saved.Copy();var sources=root.Element("sources").Elements("source").ToArray();
            if((int)root.Attribute("kind")!=(int)(craft==null?RemoteResourceClientOperation.CookingPull:craft.Kind)||
                craft!=null&&!RebirthRemoteResourceDurableOutcome.HasSavedHistoricalFetchAdmission(actor,craft,saved)||(int)root.Attribute("deaths")>=actor.Died||
                ((int?)root.Attribute("aborting")??0)!=1||sources.Any(source=>(int)source.Attribute("kind")==5&&(int)source.Attribute("attempted")!=0)||
                !RebirthOriginalActionScope.Matches(actor,saved.Owner,saved.Creation)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string nativeWorld)||nativeWorld!=saved.World)return false;
            var source=sources.Single(value=>(string)value.Attribute("id")==id);
            if((int)source.Attribute("kind")==5||(int)source.Attribute("attempted")!=1||((int?)source.Attribute("restored")??0)!=0||
                !RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,saved.Owner,saved.Operation,id,out var original)||
                !RebirthRemoteResourceNativeBinding.TryResolve(world,id,original.Kind,original.Incarnation,out var binding)||
                !held.TryGetValue(binding.Endpoint,out var lease)||!lease.Original.SameOriginal(original))return false;
            return lease.RestoreHistoricalCooking(actor,Authenticated,out proof);
        }
        catch{proof=null;return false;}
    }
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).All(value=>value);
    private static bool Mixed(ItemStack[] actual,ItemStack[] a,ItemStack[] b)=>actual!=null&&a!=null&&b!=null&&actual.Length==a.Length&&a.Length==b.Length&&
        actual.Select((item,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(item,a[index])||RebirthStationGridIngredients.IsSameStackSnapshot(item,b[index])).All(value=>value);
    private static bool Scope()=>installed&&ThreadManager.IsMainThread()&&world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,world)&&GameIO.GetSaveGameDir()==directory;
    private static bool BindKnown()
    {
        bool coherent=true;
        foreach(var original in originals.ToArray())
        {
            if(!RebirthRemoteResourceNativeBinding.TryResolve(world,original.SourceId,original.Kind,original.Incarnation,out var source))continue;
            if(held.TryGetValue(source.Endpoint,out var previous))
            {if(!previous.Original.SameOriginal(original)&&previous.Original.SavedTerminal())previous.Dispose();else{if(!previous.Original.SameOriginal(original))coherent=false;continue;}}
            held[source.Endpoint]=new Lease(original,source);
        }
        return coherent;
    }
    private static bool Bootstrap()
    {
        if(!installed||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||bootstrapping)return false;
        if(!ReferenceEquals(world,GameManager.Instance.World)||directory!=GameIO.GetSaveGameDir())
        {foreach(var lease in held.Values.ToArray())lease.StopObservation();held.Clear();originals.Clear();world=GameManager.Instance.World;directory=GameIO.GetSaveGameDir();ready=false;retryAfter=DateTime.MinValue;}
        if(ready)return true;if(DateTime.UtcNow<retryAfter)return false;retryAfter=DateTime.UtcNow.AddSeconds(1);bootstrapping=true;
        try
        {
            RebirthNpcAggregatePersistenceStore.EnsureLoaded();
            if(!RebirthLogisticsTransferJournal.TryReadPendingNativeOriginals(world,out var logistics)||
                !RebirthRemoteResourceDurableJournal.TryReadPending(world,out var fetch)||
                !RebirthNpcWorkNativeTransferOriginal.TryReadPending(world,out var work)||
                !RebirthNpcWorkNativeCreditOriginal.TryReadPending(world,out var workCredit)||
                !RebirthSmartLootingHumanOriginal.TryReadPending(world,out var smartLoot)||
                !RebirthDogSmartLootOriginal.TryReadPending(world,out var dogLoot))return false;
            var found=new List<RebirthRemoteResourceNativeOriginal>();
            foreach(var record in logistics)foreach(var leg in record.Legs.Where(value=>value.Kind!=RemoteResourceSourceKind.NpcStorage))
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.Logistics,record.Owner,record.Operation,leg.Id,out var original))return false;found.Add(original);}
            foreach(var record in fetch)foreach(var source in record.Copy().Element("sources").Elements("source").Where(value=>(int)value.Attribute("kind")!=5))
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,record.Owner,record.Operation,(string)source.Attribute("id"),out var original))return false;found.Add(original);}
            foreach(var record in work)
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork,record.Owner,record.Operation,record.Source,out var original))return false;found.Add(original);}
            foreach(var record in workCredit)
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit,
                record.Owner,record.Operation,record.Destination,out var original))return false;found.Add(original);}
            foreach(var record in smartLoot)
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting,
                record.Owner,record.Operation,RemoteResourceIdentity.Static(record.SourcePosition),out var original))return false;found.Add(original);}
            foreach(var record in dogLoot)
            {if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting,
                record.Owner,record.Operation,RemoteResourceIdentity.Static(record.SourcePosition),out var original))return false;found.Add(original);}
            if(found.Count>8192)return false;originals.Clear();originals.AddRange(found);ready=BindKnown();return ready;
        }
        catch{return false;}
        finally{bootstrapping=false;}
    }
    private static bool Reserve(EntityPlayer actor,RebirthRemoteResourceNativeOriginal original,Func<bool> current,out Lease lease)
    {
        lease=null;bool coherent=Bootstrap();if(original==null||!original.ActorCurrent(actor)||current?.Invoke()!=true||original.SavedTerminal()||
            !RebirthRemoteResourceNativeBinding.TryResolve(world,original.SourceId,original.Kind,original.Incarnation,out var source))return false;
        if(held.TryGetValue(source.Endpoint,out var previous))
        {if(!previous.Original.SameOriginal(original)){if(!previous.Original.SavedTerminal())return false;previous.Dispose();}else return previous.BindActor(actor,current)&&(lease=previous)!=null;}
        if(!coherent)return false;
        var candidate=new Lease(original,source);held.Add(source.Endpoint,candidate);if(!originals.Any(value=>value.SameOriginal(original)))originals.Add(original);
        if(!candidate.BindActor(actor,current))return false;lease=candidate;return true;
    }
    internal static bool TryReserveLogistics(EntityPlayer actor,RebirthLogisticsTransferJournal.Record expected,string legId,Func<bool> current,out Lease lease)
    {
        lease=null;try{return expected!=null&&RebirthLogisticsTransferJournal.TryRead(expected.Owner,expected.Operation,out var saved)&&RebirthLogisticsTransferJournal.Encode(saved)==RebirthLogisticsTransferJournal.Encode(expected)&&
            RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.Logistics,expected.Owner,expected.Operation,legId,out var original)&&Reserve(actor,original,current,out lease);}catch{return false;}
    }
    internal static bool TryReserveRemoteResource(EntityPlayer actor,Guid operation,string sourceId,Func<bool> current,out Lease lease)
    {
        lease=null;try{return RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)&&RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,identity.StorageKey,operation,sourceId,out var original)&&Reserve(actor,original,current,out lease);}catch{return false;}
    }
    internal static bool TryReserveNpcWork(EntityPlayer actor,RebirthNpcWorkNativeTransferOriginal.Record expected,
        Func<bool> current,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthNpcWorkNativeTransferOriginal.TryRead(expected.Owner,expected.Operation,expected.Source,out var saved)&&
            saved.Binding==expected.Binding&&saved.Phase==expected.Phase&&saved.Attempted==expected.Attempted&&saved.Published==expected.Published&&
            RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.NpcWork,
                expected.Owner,expected.Operation,expected.Source,out var original)&&Reserve(actor,original,current,out lease);}
        catch{return false;}
    }
    internal static bool TryReserveNpcWorkCredit(EntityPlayer actor,
        RebirthNpcWorkNativeCreditOriginal.Record expected,Func<bool> current,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthNpcWorkNativeCreditOriginal.TryRead(expected.Owner,expected.Operation,
                expected.Destination,out var saved)&&saved.Binding==expected.Binding&&
            saved.Phase==expected.Phase&&saved.Attempted==expected.Attempted&&
            saved.Published==expected.Published&&
            RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.NpcWorkCredit,
                expected.Owner,expected.Operation,expected.Destination,out var original)&&
            Reserve(actor,original,current,out lease);}
        catch{return false;}
    }
    internal static bool TryReserveSmartLooting(EntityPlayer actor,RebirthSmartLootingHumanOriginal expected,
        Func<bool> current,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthSmartLootingHumanOriginal.TryRead(expected.Owner,expected.Operation,
                RemoteResourceIdentity.Static(expected.SourcePosition),out var saved)&&
            saved.Binding==expected.Binding&&saved.Phase==expected.Phase&&
            saved.Attempted==expected.Attempted&&saved.Published==expected.Published&&
            RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.SmartLooting,
                expected.Owner,expected.Operation,RemoteResourceIdentity.Static(expected.SourcePosition),out var original)&&
            Reserve(actor,original,current,out lease);}
        catch{return false;}
    }
    // Dog twin of TryReserveSmartLooting. The expected row must still be the saved row (same binding,
    // phase and counters); a stale caller copy never binds a lease.
    internal static bool TryReserveDogSmartLooting(EntityPlayer actor,RebirthDogSmartLootOriginal expected,
        Func<bool> current,out Lease lease)
    {
        lease=null;
        try{return expected!=null&&RebirthDogSmartLootOriginal.TryRead(expected.Owner,expected.Operation,
                RemoteResourceIdentity.Static(expected.SourcePosition),out var saved)&&
            saved.Binding==expected.Binding&&saved.Phase==expected.Phase&&
            saved.Attempted==expected.Attempted&&saved.Published==expected.Published&&
            RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting,
                expected.Owner,expected.Operation,RemoteResourceIdentity.Static(expected.SourcePosition),out var original)&&
            Reserve(actor,original,current,out lease);}
        catch{return false;}
    }
    // Registers a freshly SAVED dog row (phase 0) so Blocks() covers its source from that moment on
    // instead of waiting for the first lease reservation. Evidence only: grants no setter permit.
    internal static bool TryAdmitDogSmartLooting(RebirthDogSmartLootOriginal saved)
    {
        try
        {
            if(saved==null||!Bootstrap()||!RebirthRemoteResourceNativeOriginal.TryRead(
                RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting,saved.Owner,saved.Operation,
                RemoteResourceIdentity.Static(saved.SourcePosition),out var original)||original.SavedTerminal())return false;
            if(!originals.Any(value=>value.SameOriginal(original)))originals.Add(original);
            return true;
        }
        catch{return false;}
    }
    // A dog record removed while its row was open takes the row (and the capability to settle it) with
    // it. Such a lease can never reach SavedTerminal, so it would hold the source for the whole session.
    // Only fires when the row is GENUINELY gone: never while a recovery install/world switch may be hiding
    // records, and only after the id stayed absent from every dog record on 3 consecutive passes. While
    // the dog record exists (even with its entity dead/unloaded) the lease is kept for recovery.
    private static readonly Dictionary<Guid,int> dogOrphanSightings=new Dictionary<Guid,int>();
    private static void ReleaseOrphanedDogLeases()
    {
        if(!ready)return;
        if(RebirthNpcPersistenceCoordinator.IsRecoveryInstalling){dogOrphanSightings.Clear();return;}
        foreach(var lease in held.Values.ToArray())
        {
            var original=lease.Original;
            if(original.GraphRetired||original.Domain!=RebirthRemoteResourceSourceEffectCodec.Domain.DogSmartLooting)continue;
            if(!RebirthDogSmartLootOriginal.OperationAbsent(original.Operation)){dogOrphanSightings.Remove(original.Operation);continue;}
            dogOrphanSightings.TryGetValue(original.Operation,out int seen);
            if(++seen<3){dogOrphanSightings[original.Operation]=seen;continue;}
            dogOrphanSightings.Remove(original.Operation);
            lease.StopObservation();
            if(held.TryGetValue(lease.Source.Endpoint,out var current)&&ReferenceEquals(current,lease))held.Remove(lease.Source.Endpoint);
            originals.RemoveAll(value=>value.SameOriginal(original));
            Log.Warning("[REBIRTH Smart Looting] dog row "+original.Operation.ToString("N")+" vanished with its dog record; source hold released.");
        }
    }
    internal static bool Blocks(object target)=>Blocks(target,false);
    // P7 withdrawal: the same custody answer minus the NPC station-queue row's own hold (that row is the caller itself; every other custodian still blocks).
    internal static bool BlocksStationExceptNpcQueue(TileEntityWorkstation station)=>Blocks(station,true);
    private static bool Blocks(object target,bool exceptNpcQueue)
    {
        if(!exceptNpcQueue&&RebirthNpcStationQueueOriginal.BlocksNativeInventory(target))return true;
        if(!exceptNpcQueue&&target is TileEntityWorkstation npcStation&&RebirthNpcStationQueueOriginal.BlocksStation(npcStation))return true;
        if(RebirthCookingNativeReturn.Blocks(target))return true;
        if(target==null||!installed||!ThreadManager.IsMainThread())return false;
        object endpoint=target is ItemStackGrid grid?grid.holder:target is Bag bag?bag.ItemGrid?.holder:target;
        if(endpoint is TEFeatureStorage storage)endpoint=storage.Parent;
        if(endpoint is not TileEntityComposite&&endpoint is not TileEntityWorkstation&&endpoint is not EntityVehicle&&endpoint is not EntityDrone)return false;
        Bootstrap();
        if(held.TryGetValue(endpoint,out var retained)&&retained.OwnsEndpoint())return true;
        // Camper snapshots are detached during planning/readback and have no
        // terrain position. Live mobile stations also keep vehicle/installation
        // identity; never alias them to a placed workstation at the vehicle's
        // coordinates. Exact retained endpoint holds above still take priority.
        if(endpoint is RebirthCamperStationState)return false;
        // Loaded-after-bootstrap sources bind at their first native access or
        // mutation boundary, before that original mutation body can run.
        string id=endpoint is EntityVehicle vehicle?RemoteResourceIdentity.Vehicle(vehicle.entityId):endpoint is EntityDrone drone?RemoteResourceIdentity.Drone(drone.entityId):
            endpoint is TileEntityWorkstation station?RemoteResourceIdentity.Workstation(station.ToWorldPos()):RemoteResourceIdentity.Static(((TileEntityComposite)endpoint).ToWorldPos());
        var matching=originals.Where(value=>value.SourceId==id).ToArray();int candidates=0;
        foreach(var original in matching)
            if(RebirthRemoteResourceNativeBinding.TryResolve(world,id,original.Kind,original.Incarnation,out var source)&&ReferenceEquals(source.Endpoint,endpoint))
            {if(++candidates==1)held[endpoint]=new Lease(original,source);else ready=false;}
        return candidates>0;
    }
    internal static bool BlocksVehicle(EntityVehicle vehicle)=>Blocks(vehicle);
    internal static bool BlocksStation(TileEntityWorkstation station)=>Blocks(station);
    internal static bool HasForeignPlacedStationHold(TileEntityWorkstation station,string owner,Guid operation,string binding)
    {
        if(station==null||!Bootstrap()||!RebirthLogisticsEndpointIdentity.TryGetIncarnation(GameManager.Instance?.World,station,out string incarnation))return true;
        bool Own(RebirthRemoteResourceNativeOriginal original)=>original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&
            original.Owner==owner&&original.Operation==operation&&original.Binding==binding;
        if(held.TryGetValue(station,out var current)&&!Own(current.Original))return true;
        string id=RemoteResourceIdentity.Workstation(station.ToWorldPos());
        return originals.Any(original=>original.SourceId==id&&original.Incarnation==incarnation&&!Own(original));
    }
    // Read-only exception classifier for re-binding an ALREADY admitted
    // fetched station original. It does not bootstrap/mint identities, grant
    // mutation permits or change any normal Blocks/authorization policy.
    internal static bool HasForeignFetchedVehicleHold(EntityVehicle vehicle,TileEntityWorkstation station,string owner,Guid operation,string binding)
    {
        if(vehicle==null||station==null||!ReferenceEquals(vehicle.world,GameManager.Instance?.World))return true;
        bool Own(RebirthRemoteResourceNativeOriginal original)=>original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&
            original.Owner==owner&&original.Operation==operation&&original.Binding==binding;
        foreach(var lease in held.Values)
            if(ReferenceEquals(lease.Source.World,vehicle.world)&&(ReferenceEquals(lease.Source.Endpoint,vehicle)||ReferenceEquals(lease.Source.Endpoint,station))&&!Own(lease.Original))return true;
        if(ReferenceEquals(world,vehicle.world)&&directory==GameIO.GetSaveGameDir()&&vehicle.vehicle!=null&&vehicle.vehicle.itemValue!=null&&
            vehicle.vehicle.itemValue.TryGetMetadata("rebirth.logistics.incarnation",out string incarnation))
        {
            string id=RemoteResourceIdentity.Vehicle(vehicle.entityId);
            if(originals.Any(original=>original.SourceId==id&&original.Incarnation==incarnation&&!Own(original)))return true;
        }
        return false;
    }
    internal static bool CanAdmitNewOriginal()=>Bootstrap();
    internal static bool BlocksSource(IRemoteResourceSource source)
    {
        if(source is StaticContainerResourceSource container)return Blocks(container.NativeTile);
        if(source is WorkstationOutputResourceSource station)return BlocksStation(station.NativeStation);
        if(source is VehicleResourceSource vehicle)return BlocksVehicle(vehicle.Vehicle);
        if(source is DroneResourceSource drone)return Blocks(drone.Drone);
        return false; // NPC original receipt admission is a separate service.
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        if(permit==null)return RebirthCookingNativeReturn.Consume(grid,slot,value)||RebirthNpcStationQueueOriginal.ConsumeGrid(grid,slot,value);
        var one=permit;if(one==null||one.Consumed||!one.Lease.Current()||!ReferenceEquals(one.Lease.Grid,grid)||one.Slot!=slot||!RebirthStationGridIngredients.IsSameStackSnapshot(one.Desired,value))return false;
        one.Consumed=true;return true;
    }
    internal static bool Consume(TileEntityWorkstation station,ItemStack[] value)
    {
        if(permit==null)return RebirthCookingNativeReturn.Consume(station,value);
        var one=permit;if(one==null||one.Consumed||!one.Lease.Current()||!ReferenceEquals(one.Lease.Source.Station,station)||!Same(one.Array,value))return false;one.Consumed=true;return true;
    }
    // Readonly composition for a separately held placed target which is also
    // this exact original's source. This never consumes or creates permission:
    // the installed original source setter guard must still consume it once.
    internal static bool IsExecutingExactOutput(TileEntityWorkstation station,ItemStack[] value,string owner,Guid sourceOperation,string binding)
    {
        var one=permit;var original=one?.Lease?.Original;
        return ThreadManager.IsMainThread()&&original!=null&&original.Domain==RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource&&
            original.Kind==RemoteResourceSourceKind.WorkstationOutput&&original.Operation==sourceOperation&&original.Owner==owner&&original.Binding==binding&&
            one.Lease.Current()&&ReferenceEquals(one.Lease.Source.Station,station)&&one.Array!=null&&Same(one.Array,value);
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.remote.resources.source.custody.v2");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperEnergyDetachedStackRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthCamperEnergyDetachedArrayRead));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthRemoteResourceCraftingReadObserver));
        if(!RebirthRemoteResourceDurableOutcome.RegisterNpcAdmissionReaders())throw new InvalidOperationException("Original NPC reader registration changed.");
        RebirthRemoteResourceNativePublication.Install();RebirthRemoteResourceSourceGuards.Install(harmony);
        ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {if(!installed||GameManager.Instance?.World?.IsRemote()!=false)return;Bootstrap();if(DateTime.UtcNow<terminalAfter)return;terminalAfter=DateTime.UtcNow.AddSeconds(1);foreach(var lease in held.Values.ToArray())if(lease.Original.SavedTerminal())lease.Dispose();ReleaseOrphanedDogLeases();}
}
