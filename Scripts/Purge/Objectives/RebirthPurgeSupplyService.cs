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
    private static IEnumerator<KeyValuePair<string,long>> lifetimeContributors;
    private static Dictionary<string,long> accumulatingLifetime;
    internal static IReadOnlyDictionary<string,long> LifetimeKills {get;private set;}
    private static Dictionary<string,long> accumulating;
    private static Dictionary<string,List<RebirthPurgeSupplyOriginSource>> accumulatingOrigins,originSources;
    private static RebirthPoiClearanceRecord reading;
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
    // Only the exact currently published reservation may arm a native flight.
    // The accepted callback can then keep its identity if the store later closes.
    internal static RebirthPoiWorldBinding CurrentFlightBinding(RebirthPurgeSupplyAccount original,Guid world)
    {
        if(!TryDeliveryPublication(original,out _)||binding.WorldId!=world||original.InFlight==0||original.Delivery==null)
            return null;
        var plan=original.Delivery;
        return (plan.Version==3?plan.Phase==RebirthPurgeSupplyDeliveryPhase.Started:
            plan.Phase==RebirthPurgeSupplyDeliveryPhase.Prepared||plan.CanResumeFlight)?binding:null;
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
    internal static bool TryInvoking(RebirthPurgeSupplyAccount original,Guid world,out RebirthPurgeSupplyAccount published)
    {
        published=null;IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> expected;RebirthPurgeSupplyAccount successor;
        if(!TryDeliveryPublication(original,out expected)||!original.TryStart(world,original.Token(world),out successor))return false;
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
        RebirthPurgeSupplyEquipment.Reset();
        records?.Dispose();contributors?.Dispose();earning?.Dispose();lifetimeContributors?.Dispose();
        lifetimeContributors=null;accumulatingLifetime=null;LifetimeKills=null;
        records=null;contributors=null;earning=null;binding=null;indexed=null;scanning=null;accounts=null;accumulating=null;credits=null;accumulatingOrigins=originSources=null;reading=null;opened=false;next=0;reopen=0;
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
            accumulatingOrigins=new Dictionary<string,List<RebirthPurgeSupplyOriginSource>>(StringComparer.Ordinal);
            accumulatingLifetime=new Dictionary<string,long>(StringComparer.Ordinal);
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
                        accumulating[pair.Key]=old+pair.Value;
                        RebirthPurgeOfflineCredit offline;
                        if(reading.SupplyCredits.Offline.TryGetValue(pair.Key,out offline))
                        {List<RebirthPurgeSupplyOriginSource> sources;if(!accumulatingOrigins.TryGetValue(pair.Key,out sources))accumulatingOrigins.Add(pair.Key,sources=new List<RebirthPurgeSupplyOriginSource>());sources.Add(new RebirthPurgeSupplyOriginSource(reading.Identity.Key,offline));}
                        players++;continue;
                    }
                    contributors.Dispose();contributors=null;
                }
                if(lifetimeContributors!=null)
                {
                    if(lifetimeContributors.MoveNext())
                    {
                        var pair=lifetimeContributors.Current;long old;accumulatingLifetime.TryGetValue(pair.Key,out old);
                        if(old>long.MaxValue-pair.Value||!accumulatingLifetime.ContainsKey(pair.Key)&&accumulatingLifetime.Count>=16384){Reset();return;}
                        accumulatingLifetime[pair.Key]=old+pair.Value;players++;continue;
                    }
                    lifetimeContributors.Dispose();lifetimeContributors=null;
                }
                if(!records.MoveNext())
                {
                    records.Dispose();records=null;indexed=scanning;scanning=null;
                    credits=new ReadOnlyDictionary<string,long>(accumulating);accumulating=null;originSources=accumulatingOrigins;accumulatingOrigins=null;
                    LifetimeKills=new ReadOnlyDictionary<string,long>(accumulatingLifetime);accumulatingLifetime=null;
                    earning?.Dispose();earning=credits.GetEnumerator();break;
                }
                examined++;reading=records.Current;if(records.Current.SupplyCredits!=null)contributors=records.Current.SupplyCredits.Totals.GetEnumerator();
                if(records.Current.LifetimeKills!=null)lifetimeContributors=records.Current.LifetimeKills.Totals.GetEnumerator();
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
            // Owner-authorized offline pricing: real equipped tier when available,
            // otherwise conservative no-glove price. Clear-time origin stays durable.
            int modifierTier;if(!RebirthPurgeSupplyEquipment.TryTier(source.Key,out modifierTier))modifierTier=0;
            var rules=RebirthPurgeSupplyPolicy.Current;
            // With no new credits and too little balance for the next entitlement,
            // TryEarn cannot change the published account. A better equipment tier
            // still reaches TryEarn as soon as its lower target is affordable.
            if(source.Value==old.ObservedCredits&&modifierTier>=0&&modifierTier<=5&&
                source.Value-old.SpentCredits<rules.Target(old.EarnedDrops,modifierTier))continue;
            List<RebirthPurgeSupplyOriginSource> origins;
            IEnumerable<RebirthPurgeSupplyOriginSource> sources=originSources.TryGetValue(source.Key,out origins)
                ?origins:Array.Empty<RebirthPurgeSupplyOriginSource>();
            if(!RebirthPurgeSupplyOrigins.TryEarn(old,source.Value,sources,rules,modifierTier,out successor))continue;
            if(successor.ObservedCredits==old.ObservedCredits&&successor.EarnedDrops==old.EarnedDrops)continue;
            if(!accounts.TryCommit(expected,successor)){opened=false;reopen=Time.realtimeSinceStartup+1;}
            return;
        }
    }
}
