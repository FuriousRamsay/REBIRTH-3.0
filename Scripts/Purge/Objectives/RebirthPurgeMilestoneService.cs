using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class RebirthPurgeMilestoneService
{
    private static RebirthPoiWorldBinding binding;
    private static RebirthPurgeMilestoneStore store;
    private static object evaluated;
    private static bool opened;
    private static double next;
    internal static IReadOnlyDictionary<string,RebirthPurgeMilestoneStore.Receipt> Published => opened?store?.Published:null;
    internal static IReadOnlyDictionary<string,RebirthPurgeMilestoneStore.Receipt> PublishedAll => opened?store?.PublishedAll:null;
    internal static void Reset() { binding=null;store=null;evaluated=null;opened=false;next=0; }
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled || !RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.5;
        RebirthPoiWorldStore worldStore;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetStore(out worldStore))return;
        var snapshot=worldStore.Published;
        Evaluate(snapshot);
    }
    private static bool Evaluate(RebirthPoiWorldSnapshot snapshot)
    {
        if(snapshot==null || !snapshot.Binding.IsCurrent)return false;
        if(!ReferenceEquals(binding,snapshot.Binding))
        { binding=snapshot.Binding;store=new RebirthPurgeMilestoneStore(binding);evaluated=null;opened=false; }
        if(!opened){opened=store.TryOpen();if(!opened)return false;}
        var progress=RebirthPurgeObjectiveProgress.Instance;
        if(progress.Published==null || progress.WorldId!=snapshot.WorldId || progress.Revision!=snapshot.Revision || progress.Revision<1 || progress.CensusGeneration<1)return false;
        if(ReferenceEquals(evaluated,progress.Published))return true;
        var earned=progress.Published.Values.Where(b=>b.Eligible>0 && b.RemainingFor(progress.TargetPercentage)==0)
            .Select(b=>new RebirthPurgeMilestoneStore.Receipt(b.Biome,progress.TargetPercentage,b.Eligible,b.Cleared,progress.Revision,progress.CensusGeneration)).ToArray();
        var receipts=earned.ToList();
        if(RebirthPurgeDiscoveryPolicy.Available)
        foreach(var biome in progress.Published.Values.Where(b=>b.Eligible>0))
        foreach(var definition in RebirthPurgeDiscoveryPolicy.Definitions)
            if(biome.RemainingFor(RebirthPurgeDiscoveryPolicy.ScaledPercent(definition.Percent,progress.TargetPercentage))==0)
                receipts.Add(new RebirthPurgeMilestoneStore.Receipt(biome.Biome,RebirthPurgeDiscoveryPolicy.ScaledPercent(definition.Percent,progress.TargetPercentage),biome.Eligible,biome.Cleared,progress.Revision,progress.CensusGeneration,definition.Id,definition.Tier));
        var previous=store.PublishedAll;
        if(store.TryRecord(receipts))
        {
            evaluated=progress.Published;
            foreach(var receipt in store.PublishedAll.Values)
                if(previous==null || !previous.ContainsKey(receipt.Key))
                    RebirthPurgeMilestoneNotification.Send(receipt);
        }
        else opened=false; // Re-open resolves only positively validated original candidates.
        return opened&&ReferenceEquals(evaluated,progress.Published);
    }
}