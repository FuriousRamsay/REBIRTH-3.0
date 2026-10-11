using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

// Read cumulative credit custody in bounded slices. A newer publication does not
// cancel an in-progress sum, so continuous deaths cannot starve earned rewards.
internal static class RebirthPurgeSupplyService
{
    private static RebirthPoiWorldBinding binding;
    private static RebirthPoiWorldSnapshot indexed,scanning;
    private static RebirthPurgeSupplyAccountStore accounts;
    private static IEnumerator<RebirthPoiClearanceRecord> records;
    private static IEnumerator<KeyValuePair<string,long>> contributors,earning;
    private static Dictionary<string,long> accumulating;
    private static IReadOnlyDictionary<string,long> credits;
    private static bool opened;
    private static double next,reopen;
    internal static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published=>opened?accounts?.Published:null;
    private static bool TryDeliveryPublication(RebirthPurgeSupplyAccount original,out IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected)
    {
        expected=null;
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!ThreadManager.IsMainThread()||!opened||binding==null||!binding.IsCurrent||original==null)return false;
        RebirthPoiWorldStore world;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out world)||!ReferenceEquals(world.Published?.Binding,binding))return false;
        expected=accounts.Published;RebirthPurgeSupplyAccount current;
        return expected!=null&&expected.TryGetValue(original.Player,out current)&&ReferenceEquals(current,original);
    }
    private static bool TryDeliveryCommit(IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected,RebirthPurgeSupplyAccount successor,out RebirthPurgeSupplyAccount published)
    {
        published=null;
        if(!accounts.TryCommit(expected,successor)){opened=false;reopen=Time.realtimeSinceStartup+1;return false;}
        var current=accounts.Published;RebirthPurgeSupplyAccount saved;
        if(current==null||!current.TryGetValue(successor.Player,out saved)||saved.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)!=successor.Write().ToString(System.Xml.Linq.SaveOptions.DisableFormatting))return false;
        published=saved;return true;
    }
    internal static bool TryPrepare(RebirthPurgeSupplyAccount original,RebirthPurgeSupplyDeliveryPlan destination,out RebirthPurgeSupplyAccount published)
    {
        published=null;IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected;RebirthPurgeSupplyAccount successor;
        if(!TryDeliveryPublication(original,out expected)||!original.TryReserve(destination,out successor))return false;
        return TryDeliveryCommit(expected,successor,out published);
    }
    internal static bool TryLaunched(RebirthPurgeSupplyCrateStamp launch)
    {
        var current=Published;RebirthPurgeSupplyAccount original,successor,published;
        IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected;
        if(launch==null||current==null||!current.TryGetValue(launch.Player,out original)||
           !TryDeliveryPublication(original,out expected)||launch.World!=binding.WorldId)return false;
        if(original.DeliveredDrops>=launch.Sequence)return true;
        if(original.InFlight!=launch.Sequence||!original.TryLaunched(launch.World,launch.Token,out successor))return false;
        return TryDeliveryCommit(expected,successor,out published);
    }
    internal static void ReconcileLoadedCrate(EntitySupplyCrate crate)
    {
        if(binding==null)return;
        RebirthPurgeSupplyCrateStamp saved;RebirthPurgeSupplyAccount original,successor,published;
        IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected;
        var current=Published;
        if(current==null||!RebirthPurgeSupplyCrateSerialization.TryReadAuthoritative(crate,binding.WorldId,out saved)||
           !current.TryGetValue(saved.Player,out original)||original.InFlight!=saved.Sequence||
           !TryDeliveryPublication(original,out expected))return;
        // Existing stamped crate proves the old reward exists. No forced chunk
        // load, region hash, saved-byte inspection or access lock is needed.
        if(original.TryComplete(saved.World,saved.Token,out successor)||original.TryLaunched(saved.World,saved.Token,out successor))
            TryDeliveryCommit(expected,successor,out published);
    }
    internal static void Reset()
    {
        records?.Dispose();contributors?.Dispose();earning?.Dispose();
        records=null;contributors=null;earning=null;binding=null;indexed=null;scanning=null;accounts=null;accumulating=null;credits=null;opened=false;next=0;reopen=0;
    }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.1;
        RebirthPoiWorldStore world;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out world))return;
        var snapshot=world.Published;if(snapshot==null||!snapshot.Binding.IsCurrent)return;
        if(!ReferenceEquals(binding,snapshot.Binding))
        {Reset();binding=snapshot.Binding;accounts=new RebirthPurgeSupplyAccountStore(binding);}
        if(!opened)
        {
            if(Time.realtimeSinceStartup<reopen)return;
            opened=accounts.TryOpen();if(!opened){reopen=Time.realtimeSinceStartup+5;return;}
        }
        if(scanning==null&&!ReferenceEquals(indexed,snapshot))
        {
            scanning=snapshot;accumulating=new Dictionary<string,long>(StringComparer.Ordinal);
            records=snapshot.Shards.Values.SelectMany(s=>s.Records.Values).GetEnumerator();
        }
        if(scanning!=null)
        {
            long deadline=Stopwatch.GetTimestamp()+(long)(Stopwatch.Frequency*.004);int examined=0,players=0;
            while(examined<64&&players<256&&Stopwatch.GetTimestamp()<deadline)
            {
                if(contributors!=null)
                {
                    if(contributors.MoveNext())
                    {
                        var pair=contributors.Current;long old;accumulating.TryGetValue(pair.Key,out old);
                        if(old>long.MaxValue-pair.Value||!accumulating.ContainsKey(pair.Key)&&accumulating.Count>=16384){Reset();return;}
                        accumulating[pair.Key]=old+pair.Value;players++;continue;
                    }
                    contributors.Dispose();contributors=null;
                }
                if(!records.MoveNext())
                {
                    records.Dispose();records=null;indexed=scanning;scanning=null;
                    credits=new ReadOnlyDictionary<string,long>(accumulating);accumulating=null;
                    earning?.Dispose();earning=credits.GetEnumerator();break;
                }
                examined++;if(records.Current.SupplyCredits!=null)contributors=records.Current.SupplyCredits.Totals.GetEnumerator();
            }
        }
        if(credits==null||RebirthPurgeSupplyPolicy.Current==null)return;
        if(earning==null)earning=credits.GetEnumerator();
        // One atomic account publication per pulse; unchanged accounts do not write.
        for(int n=0;n<16;n++)
        {
            if(!earning.MoveNext()){earning.Dispose();earning=null;return;}
            var source=earning.Current;var expected=accounts.Published;if(expected==null){opened=false;return;}
            RebirthPurgeSupplyAccount old;if(!expected.TryGetValue(source.Key,out old))old=new RebirthPurgeSupplyAccount(source.Key);
            RebirthPurgeSupplyAccount successor;
            if(!old.TryEarn(source.Value,RebirthPurgeSupplyPolicy.Current,out successor))continue;
            if(successor.ObservedCredits==old.ObservedCredits&&successor.EarnedDrops==old.EarnedDrops)continue;
            if(!accounts.TryCommit(expected,successor)){opened=false;reopen=Time.realtimeSinceStartup+1;}
            return;
        }
    }
}