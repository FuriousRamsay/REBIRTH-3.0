using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

// Captures the complete, already registered native reset target before any original
// iterator advances. It never loads chunks, activates sleepers or predicts native IDs.
internal sealed class RebirthPoiNativeResetEnvelope
{
    internal sealed class Target
    {
        public readonly PrefabInstance Prefab;
        internal readonly RebirthPoiNativeAuthoredManifest Authored;
        public readonly RebirthPoiNativeManifest Combat;
        public readonly RebirthPoiResetBatchEntry Entry;
        public readonly IReadOnlyDictionary<int,SleeperVolume> Sleepers;
        public readonly IReadOnlyDictionary<int,TriggerVolume> Triggers;
        public readonly IReadOnlyCollection<int> AuxiliarySleepers;
        internal readonly Func<bool> AuthoredCurrent;
        internal RebirthPoiNativeCopyReceiptScope PendingCopy;
        private readonly SleeperVolume[] originalSleeperReferences;
        private readonly TriggerVolume[] originalTriggerReferences;
        private readonly Dictionary<string,Tuple<int,object>> registered=new Dictionary<string,Tuple<int,object>>(StringComparer.Ordinal);
        internal bool Accepts(RebirthPoiNativeAuthoredManifest.Slot slot,int id,object runtime)
        {
            if(slot.Expected.OriginalNativeId.HasValue)return slot.Expected.OriginalNativeId.Value==id&&ReferenceEquals(slot.OriginalRuntime,runtime);
            Tuple<int,object> proof;if(registered.TryGetValue(slot.Expected.Key,out proof))return proof.Item1==id&&ReferenceEquals(proof.Item2,runtime);
            return PendingCopy!=null&&PendingCopy.Admits(slot,id,runtime);
        }
        internal bool CommitCopy(RebirthPoiNativeCopyReceiptScope scope,IReadOnlyList<RebirthPoiAuthoredRuntimeBinding> receipts)
        {
            if(!ReferenceEquals(PendingCopy,scope))return false;
            var next=new List<Tuple<RebirthPoiNativeAuthoredManifest.Slot,int,object>>();
            foreach(var receipt in receipts)
            {
                var slot=Authored.Slots.SingleOrDefault(s=>s.Expected.Key==receipt.ExpectationKey);int id;object runtime;
                if(slot==null||!slot.TryReadRuntime(out id,out runtime)||id!=receipt.NativeId||!scope.Admits(slot,id,runtime))return false;
                next.Add(Tuple.Create(slot,id,runtime));
            }
            foreach(var item in next)registered[item.Item1.Expected.Key]=Tuple.Create(item.Item2,item.Item3);
            PendingCopy=null;return true;
        }
        internal bool TypedRuntimeCurrent(World world)
        {
            foreach(var slot in Authored.Slots)
            {
                int raw=slot.Expected.Kind==RebirthPoiAuthoredResetKind.Sleeper?world.FindSleeperVolume(slot.WorldMinimum,slot.WorldMaximum):world.FindTriggerVolume(slot.WorldMinimum,slot.WorldMaximum);
                if(raw<0){if(slot.Expected.OriginalNativeId.HasValue||registered.ContainsKey(slot.Expected.Key))return false;continue;}
                int id;object runtime;if(!slot.TryReadRuntime(out id,out runtime)||!Accepts(slot,id,runtime))return false;
            }
            foreach(var original in originalSleeperReferences)if(!Prefab.sleeperVolumes.Contains(original)||!ReferenceEquals(world.GetSleeperVolume(world.FindSleeperVolume(original.BoxMin,original.BoxMax)),original))return false;
            foreach(var original in originalTriggerReferences)if(!Prefab.triggerVolumes.Contains(original)||!ReferenceEquals(world.GetTriggerVolume(world.FindTriggerVolume(original.BoxMin,original.BoxMax)),original))return false;
            foreach(var volume in Prefab.sleeperVolumes.Where(v=>v!=null))if(!originalSleeperReferences.Contains(volume)&&!Authored.Slots.Any(s=>s.Expected.Kind==RebirthPoiAuthoredResetKind.Sleeper&&ReferenceEquals(world.GetSleeperVolume(world.FindSleeperVolume(s.WorldMinimum,s.WorldMaximum)),volume)&&Accepts(s,world.FindSleeperVolume(s.WorldMinimum,s.WorldMaximum),volume)))return false;
            foreach(var volume in Prefab.triggerVolumes.Where(v=>v!=null))if(!originalTriggerReferences.Contains(volume)&&!Authored.Slots.Any(s=>s.Expected.Kind==RebirthPoiAuthoredResetKind.Trigger&&ReferenceEquals(world.GetTriggerVolume(world.FindTriggerVolume(s.WorldMinimum,s.WorldMaximum)),volume)&&Accepts(s,world.FindTriggerVolume(s.WorldMinimum,s.WorldMaximum),volume)))return false;
            return true;
        }
        internal Target(PrefabInstance prefab,RebirthPoiNativeManifest combat,RebirthPoiResetBatchEntry entry,Dictionary<int,SleeperVolume> sleepers,Dictionary<int,TriggerVolume> triggers,IEnumerable<int> auxiliary,Func<bool> authoredCurrent,RebirthPoiNativeAuthoredManifest authored)
        {originalSleeperReferences=prefab.sleeperVolumes.Where(v=>v!=null).ToArray();originalTriggerReferences=prefab.triggerVolumes.Where(v=>v!=null).ToArray();Authored=authored;Prefab=prefab;Combat=combat;Entry=entry;Sleepers=new System.Collections.ObjectModel.ReadOnlyDictionary<int,SleeperVolume>(sleepers);Triggers=new System.Collections.ObjectModel.ReadOnlyDictionary<int,TriggerVolume>(triggers);AuxiliarySleepers=Array.AsReadOnly(auxiliary.ToArray());AuthoredCurrent=authoredCurrent;}
    }
    public readonly World World;
    public readonly Guid Transaction;
    public readonly IReadOnlyList<Target> Targets;
    private readonly List<PrefabInstance> originalList;
    private readonly RebirthPoiWorldBinding binding;
    private readonly RebirthPoiResetCaller caller;
    private RebirthPoiNativeResetEnvelope(RebirthPoiWorldBinding scope,World world,List<PrefabInstance> list,Guid tx,RebirthPoiResetCaller role,Target[] targets)
    {binding=scope;World=world;originalList=list;Transaction=tx;caller=role;Targets=Array.AsReadOnly(targets);}
    public static bool TryCapture(RebirthPoiWorldSnapshot original,World world,List<PrefabInstance> prefabs,Guid transaction,RebirthPoiResetCaller caller,out RebirthPoiNativeResetEnvelope envelope)
    {
        envelope=null;
        try
        {
            if(original==null||transaction==Guid.Empty||prefabs==null||prefabs.Count<1||prefabs.Count>RebirthPoiClearanceLedger.MaximumRecords||
                !original.Binding.IsCurrent||!ReferenceEquals(original.Binding.Scope,world))return false;
            var targets=new List<Target>();var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var prefab in prefabs)
            {
                Target target;if(!CaptureTarget(original,world,prefab,transaction,caller,out target)||!keys.Add(target.Entry.Identity.Key))return false;targets.Add(target);
            }
            envelope=new RebirthPoiNativeResetEnvelope(original.Binding,world,prefabs,transaction,caller,targets.ToArray());return envelope.IsOriginalCurrent;
        }
        catch{return false;}
    }
    private static bool CaptureTarget(RebirthPoiWorldSnapshot snapshot,World world,PrefabInstance prefab,Guid tx,RebirthPoiResetCaller caller,out Target target)
    {
        target=null;RebirthPoiNativeManifest combat;RebirthPoiClearanceRecord record;RebirthPoiNativeAuthoredManifest typed;
        if(!RebirthPoiNativeAuthoredManifest.TryCapture(snapshot.Binding,world,prefab,out typed))return false;
        if(!RebirthPoiNativeManifest.TryResolve(world,prefab,out combat)||!snapshot.TryGet(combat.Identity,out record)||record.State==RebirthPoiClearanceState.ResetPending)return false;
        var chunks=new List<long>();foreach(long key in prefab.GetOccupiedChunks())chunks.Add(key);
        if(chunks.Count<1||chunks.Count>65536||chunks.Distinct().Count()!=chunks.Count)return false;
        var authoredGuards=new List<Func<bool>>();
        var sleepers=new Dictionary<int,SleeperVolume>();var auxiliary=new List<int>();var combatIds=new List<int>();
        var shape=new StringBuilder(combat.Identity.Key);var f=CultureInfo.InvariantCulture;
        foreach(long key in chunks.OrderBy(k=>k))shape.Append("|c:").Append(key.ToString(f));
        var originalDefinition=prefab.prefab;var authored=prefab.prefab.SleeperVolumeList;if(authored==null||authored.Count>4096)return false;int authoredCount=authored.Count;
        for(int i=0;i<authored.Count;i++)
        {
            var definition=authored[i];var minimum=prefab.boundingBoxPosition+definition.startPos;var maximum=minimum+definition.size;
            int id=world.FindSleeperVolume(minimum,maximum);var volume=id<0?null:world.GetSleeperVolume(id);
            bool noncombat=definition.spawnCountMax==0&&string.IsNullOrWhiteSpace(definition.minScript);
            string script=definition.minScript??string.Empty;if(script.Length>4096)return false;
            shape.Append("|s:").Append(i).Append(':').Append(minimum).Append(':').Append(maximum).Append(':').Append(definition.flags).Append(':').Append(definition.spawnCountMin).Append(':').Append(definition.spawnCountMax).Append(':').Append(script.Length).Append(':').Append(script).Append(':').Append(id);
            int ordinal=i;var start=definition.startPos;var size=definition.size;var flags=definition.flags;var minimumCount=definition.spawnCountMin;var maximumCount=definition.spawnCountMax;
            authoredGuards.Add(()=>authored.Count==authoredCount&&authored[ordinal].startPos.Equals(start)&&authored[ordinal].size.Equals(size)&&authored[ordinal].flags==flags&&authored[ordinal].spawnCountMin==minimumCount&&authored[ordinal].spawnCountMax==maximumCount&&(authored[ordinal].minScript??string.Empty)==script&&WorldRoomSame(world,minimum,maximum,id,volume));
            if(volume==null){if(!noncombat)return false;continue;}
            if(!ReferenceEquals(volume.prefabInstance,prefab)||!volume.BoxMin.Equals(minimum)||!volume.BoxMax.Equals(maximum)||volume.flags!=definition.flags||volume.spawnCountMin!=definition.spawnCountMin||volume.spawnCountMax!=definition.spawnCountMax||sleepers.ContainsKey(id))return false;
            sleepers.Add(id,volume);if(noncombat)auxiliary.Add(id);else{if(!combat.Volumes.Contains(volume))return false;combatIds.Add(id);}
        }
        if(prefab.sleeperVolumes.Any(v=>v!=null&&!sleepers.Values.Contains(v)))return false;
        var triggers=new Dictionary<int,TriggerVolume>();var definitions=prefab.prefab.TriggerVolumeList;if(definitions==null||definitions.Count>4096)return false;int triggerCount=definitions.Count;
        for(int i=0;i<definitions.Count;i++)
        {
            var definition=definitions[i];var minimum=prefab.boundingBoxPosition+definition.startPos;var maximum=minimum+definition.size;int id=world.FindTriggerVolume(minimum,maximum);var volume=id<0?null:world.GetTriggerVolume(id);
            if(volume==null||!ReferenceEquals(volume.prefabInstance,prefab)||!volume.BoxMin.Equals(minimum)||!volume.BoxMax.Equals(maximum)||triggers.ContainsKey(id)||!volume.TriggersIndices.SequenceEqual(definition.TriggersIndices))return false;
            int ordinal=i;var start=definition.startPos;var size=definition.size;var indices=definition.TriggersIndices.ToArray();
            authoredGuards.Add(()=>definitions.Count==triggerCount&&definitions[ordinal].startPos.Equals(start)&&definitions[ordinal].size.Equals(size)&&definitions[ordinal].TriggersIndices.SequenceEqual(indices)&&volume.TriggersIndices.SequenceEqual(indices)&&ReferenceEquals(world.GetTriggerVolume(id),volume)&&world.FindTriggerVolume(minimum,maximum)==id);
            triggers.Add(id,volume);shape.Append("|t:").Append(i).Append(':').Append(minimum).Append(':').Append(maximum).Append(':').Append(id).Append(':').Append(string.Join(",",definition.TriggersIndices));
        }
        if(prefab.triggerVolumes.Any(v=>v!=null&&!triggers.Values.Contains(v)))return false;
        string manifest;using(var hash=SHA256.Create())manifest=BitConverter.ToString(hash.ComputeHash(new UTF8Encoding(false,true).GetBytes(shape.ToString()))).Replace("-","").ToLowerInvariant();
        target=new Target(prefab,combat,new RebirthPoiResetBatchEntry(combat.Identity,new RebirthPoiResetPlan(tx,caller,manifest,chunks,combatIds,triggers.Keys)),sleepers,triggers,auxiliary,()=>ReferenceEquals(prefab.prefab,originalDefinition)&&ReferenceEquals(prefab.prefab.SleeperVolumeList,authored)&&ReferenceEquals(prefab.prefab.TriggerVolumeList,definitions)&&authored.Count==authoredCount&&definitions.Count==triggerCount&&authoredGuards.All(check=>check())&&prefab.sleeperVolumes.Count(v=>v!=null)==sleepers.Count&&prefab.sleeperVolumes.All(v=>v==null||sleepers.Values.Contains(v))&&prefab.triggerVolumes.Count(v=>v!=null)==triggers.Count&&prefab.triggerVolumes.All(v=>v==null||triggers.Values.Contains(v)),typed);return true;
    }
    private static bool WorldRoomSame(World world,Vector3i minimum,Vector3i maximum,int originalId,SleeperVolume original)
    {int current=world.FindSleeperVolume(minimum,maximum);return current==originalId&&(current<0||ReferenceEquals(world.GetSleeperVolume(current),original));}
    internal bool IsBaseCurrent
    {
        get
        {
            try
            {
                if(!binding.IsCurrent||originalList.Count!=Targets.Count)return false;
                for(int i=0;i<Targets.Count;i++)
                {
                    var target=Targets[i];if(!ReferenceEquals(originalList[i],target.Prefab)||!target.Authored.IsOriginalAuthoredCurrent)return false;
                    var keys=new List<long>();foreach(long key in target.Prefab.GetOccupiedChunks())keys.Add(key);
                    if(!keys.OrderBy(k=>k).SequenceEqual(target.Entry.Plan.Chunks))return false;
                }
                return true;
            }
            catch{return false;}
        }
    }
    public static bool TryCaptureAuthored(RebirthPoiWorldSnapshot original,World world,List<PrefabInstance> prefabs,Guid transaction,RebirthPoiResetCaller role,out RebirthPoiNativeResetEnvelope envelope)
    {
        envelope=null;
        try
        {
            if(original==null||!original.Binding.IsCurrent||!ReferenceEquals(original.Binding.Scope,world)||transaction==Guid.Empty||prefabs==null||prefabs.Count<1||prefabs.Count>RebirthPoiClearanceLedger.MaximumRecords)return false;
            var targets=new List<Target>();var identities=new HashSet<string>(StringComparer.Ordinal);
            foreach(var prefab in prefabs)
            {
                RebirthPoiNativeAuthoredManifest typed;RebirthPoiClearanceRecord record;
                if(!RebirthPoiNativeAuthoredManifest.TryCapture(original.Binding,world,prefab,out typed)||!identities.Add(typed.Identity.Key)||!original.TryGet(typed.Identity,out record)||record.State==RebirthPoiClearanceState.ResetPending)return false;
                var chunks=new List<long>();foreach(long key in prefab.GetOccupiedChunks())chunks.Add(key);
                if(chunks.Count<1||chunks.Count>65536||chunks.Distinct().Count()!=chunks.Count)return false;
                var sleepers=new Dictionary<int,SleeperVolume>();var triggers=new Dictionary<int,TriggerVolume>();var auxiliary=new List<int>();
                foreach(var slot in typed.Slots.Where(s=>s.Expected.OriginalNativeId.HasValue))
                {
                    int id=slot.Expected.OriginalNativeId.Value;
                    if(slot.Expected.Kind==RebirthPoiAuthoredResetKind.Sleeper){sleepers.Add(id,(SleeperVolume)slot.OriginalRuntime);if(!slot.Expected.Combat)auxiliary.Add(id);}
                    else triggers.Add(id,(TriggerVolume)slot.OriginalRuntime);
                }
                var plan=new RebirthPoiResetPlan(transaction,role,typed.Digest,chunks,sleepers.Keys,triggers.Keys,typed.Slots.Select(s=>s.Expected),true);
                targets.Add(new Target(prefab,null,new RebirthPoiResetBatchEntry(typed.Identity,plan),sleepers,triggers,auxiliary,()=>typed.IsOriginalAuthoredCurrent,typed));
            }
            envelope=new RebirthPoiNativeResetEnvelope(original.Binding,world,prefabs,transaction,role,targets.ToArray());return envelope.IsOriginalCurrent;
        }
        catch{return false;}
    }
    public bool IsOriginalCurrent
    {
        get
        {
            try
            {
                if(!binding.IsCurrent||originalList.Count!=Targets.Count)return false;
                // Geometry/native object mapping, not mutable spawning counters, is the lease.
                for(int i=0;i<Targets.Count;i++)
                {
                    var target=Targets[i];if(!ReferenceEquals(originalList[i],target.Prefab)||(target.Entry.Plan.IsAuthored?!target.TypedRuntimeCurrent(World):!target.Combat.StillMatches())||!target.AuthoredCurrent())return false;
                    var keys=new List<long>();foreach(long key in target.Prefab.GetOccupiedChunks())keys.Add(key);
                    if(!keys.OrderBy(k=>k).SequenceEqual(target.Entry.Plan.Chunks))return false;
                    foreach(var pair in target.Sleepers)if(!ReferenceEquals(World.GetSleeperVolume(pair.Key),pair.Value)||World.FindSleeperVolume(pair.Value.BoxMin,pair.Value.BoxMax)!=pair.Key)return false;
                    foreach(var pair in target.Triggers)if(!ReferenceEquals(World.GetTriggerVolume(pair.Key),pair.Value)||World.FindTriggerVolume(pair.Value.BoxMin,pair.Value.BoxMax)!=pair.Key)return false;
                }
                return true;
            }
            catch{return false;}
        }
    }
}
