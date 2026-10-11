using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

// Earned receipts reveal authored POI markers, never terrain or native rooms.
// Incremental census scans and one atomic marker batch bound the per-update work.
internal static class RebirthPurgeDiscoveryService
{
    private static RebirthPoiWorldBinding binding;
    private static object census,receipts;
    private static IEnumerator<RebirthPurgePoiCensus.Entry> scan;
    private static Dictionary<string,HashSet<int>> tiers;
    private static readonly List<RebirthPoiIdentity> pending=new List<RebirthPoiIdentity>();
    private static double next,nextSweep;
    internal static void Reset()
    {
        scan?.Dispose();scan=null;binding=null;census=null;receipts=null;tiers=null;pending.Clear();next=nextSweep=0;
    }
    private static bool NearPlayer(RebirthPoiIdentity identity)
    {
        var players=GameManager.Instance?.World?.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        if(players==null)return false;
        foreach(var tracked in players)
        {
            var player=tracked.Player;if(player==null||player.IsDead())continue;
            double dx=player.position.x-(identity.X+identity.SizeX*.5),dz=player.position.z-(identity.Z+identity.SizeZ*.5);
            if(dx*dx+dz*dz<=15000d*15000d)return true;
        }
        return false;
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        if(!ThreadManager.IsMainThread()||Time.realtimeSinceStartup<next)return;
        next=Time.realtimeSinceStartup+.1;
        RebirthPoiWorldStore store;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))return;
        var snapshot=store.Published;
        var source=RebirthPurgePoiCensus.Instance;
        var earned=RebirthPurgeMilestoneService.PublishedAll;
        if(snapshot==null||!snapshot.Binding.IsCurrent||!source.Ready||source.Published==null||earned==null)return;
        if(!ReferenceEquals(binding,snapshot.Binding)||!ReferenceEquals(census,source.Published)||!ReferenceEquals(receipts,earned))
        {
            Reset();binding=snapshot.Binding;census=source.Published;receipts=earned;
            tiers=new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
            foreach(var receipt in earned.Values)
            {
                if(receipt.Id=="completion")continue;
                HashSet<int> values;if(!tiers.TryGetValue(receipt.Biome,out values))tiers.Add(receipt.Biome,values=new HashSet<int>());
                values.Add(receipt.Tier);
            }
            scan=source.Published.Values.GetEnumerator();nextSweep=Time.realtimeSinceStartup+30;
        }
        if(scan==null&&pending.Count==0&&Time.realtimeSinceStartup>=nextSweep)
        {scan=source.Published.Values.GetEnumerator();nextSweep=Time.realtimeSinceStartup+30;}
        if(store.HasPending)return;
        if(pending.Count==0&&scan!=null)
        {
            var budget=Stopwatch.StartNew();int examined=0;
            while(pending.Count<32&&examined++<256&&budget.ElapsedMilliseconds<4)
            {
                if(!scan.MoveNext()){scan.Dispose();scan=null;break;}
                var entry=scan.Current;HashSet<int> eligible;
                if(!tiers.TryGetValue(entry.Identity.Biome,out eligible)||!eligible.Contains(entry.Tier))continue;
                if(!NearPlayer(entry.Identity))continue;
                RebirthPoiClearanceRecord existing;
                if(snapshot.TryGet(entry.Identity,out existing))
                {
                    // A saved empty reset envelope is promoted only when the authored
                    // combat census positively qualifies it; pending native resets wait.
                    if(existing.ResetOnly)
                    {
                        var result=store.TryDiscover(snapshot,entry.Identity);
                        if(result!=RebirthPoiStoreResult.Published&&result!=RebirthPoiStoreResult.Duplicate)
                        { pending.Add(entry.Identity);return; }
                        return;
                    }
                    continue;
                }
                pending.Add(entry.Identity);
            }
        }
        if(pending.Count==0)return;
        snapshot=store.Published;
        if(snapshot==null||!ReferenceEquals(snapshot.Binding,binding)||!binding.IsCurrent)return;
        RebirthPoiClearanceRecord retained;
        if(pending.Count==1&&snapshot.TryGet(pending[0],out retained)&&retained.ResetOnly)
        {
            var promoted=store.TryDiscover(snapshot,pending[0]);
            if(promoted==RebirthPoiStoreResult.Published||promoted==RebirthPoiStoreResult.Duplicate)pending.Clear();
            return;
        }
        var published=store.TryDiscoverBatch(snapshot,pending);
        if(published==RebirthPoiStoreResult.Published||published==RebirthPoiStoreResult.Duplicate)pending.Clear();
    }
}