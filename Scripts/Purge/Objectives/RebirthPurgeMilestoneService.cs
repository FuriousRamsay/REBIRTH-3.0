using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class RebirthPurgeMilestoneService
{
    private static RebirthPoiWorldBinding binding;
    private static RebirthPurgeMilestoneStore store;
    private static object evaluated;
    private static object evaluatedDefinitions;
    private static int evaluatedTarget,evaluatedMode;
    private static bool opened;
    private static double next;
    internal static IReadOnlyDictionary<string,RebirthPurgeMilestoneStore.Receipt> Published => opened?store?.Published:null;
    internal static IReadOnlyDictionary<string,RebirthPurgeMilestoneStore.Receipt> PublishedAll => opened?store?.PublishedAll:null;
    internal static void Reset() { binding=null;store=null;evaluated=null;evaluatedDefinitions=null;evaluatedTarget=0;opened=false;next=0; }
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
        // Both local and remote milestone notices belong to the published map
        // session. Retry on the next pulse before persisting a new receipt if
        // world startup has not established that identity yet.
        if(RebirthPoiMapSync.LocalWorld!=snapshot.WorldId || RebirthPoiMapSync.LocalSession==Guid.Empty)return false;
        if(!ReferenceEquals(binding,snapshot.Binding))
        { binding=snapshot.Binding;store=new RebirthPurgeMilestoneStore(binding);evaluated=null;opened=false; }
        if(!opened){opened=store.TryOpen();if(!opened)return false;}
        var progress=RebirthPurgeObjectiveProgress.Instance;
        if(progress.Published==null || progress.WorldId!=snapshot.WorldId || progress.Revision!=snapshot.Revision || progress.Revision<1 || progress.CensusGeneration<1)return false;
        int target=progress.TargetPercentage,mode=RebirthPurgeObjectivePolicy.ProgressionMode;
        var definitions=RebirthPurgeDiscoveryPolicy.Definitions;
        if(ReferenceEquals(evaluated,progress.Published)&&evaluatedTarget==target&&evaluatedMode==mode&&ReferenceEquals(evaluatedDefinitions,definitions))return true;
        var earned=progress.Published.Values.Where(b=>b.Eligible>0 && b.RemainingFor(target)==0)
            .Select(b=>new RebirthPurgeMilestoneStore.Receipt(b.Biome,target,b.Eligible,b.Cleared,progress.Revision,progress.CensusGeneration,mode:mode,eligibleUnits:b.EligibleUnits,clearedUnits:b.ClearedUnits)).ToArray();
        var receipts=earned.ToList();
        if(RebirthPurgeDiscoveryPolicy.Available)
        foreach(var biome in progress.Published.Values.Where(b=>b.Eligible>0))
        foreach(var definition in definitions)
            if(biome.ClearedUnits>=RebirthPurgeProgression.Required(biome.EligibleUnits,RebirthPurgeDiscoveryPolicy.ScaledNumerator(definition.Percent,target)))
                receipts.Add(new RebirthPurgeMilestoneStore.Receipt(biome.Biome,RebirthPurgeDiscoveryPolicy.ScaledPercent(definition.Percent,target),biome.Eligible,biome.Cleared,progress.Revision,progress.CensusGeneration,definition.Id,definition.Tier,RebirthPurgeDiscoveryPolicy.ScaledNumerator(definition.Percent,target),mode,biome.EligibleUnits,biome.ClearedUnits));
        var previous=store.PublishedAll;
        if(store.TryRecord(receipts))
        {
            evaluated=progress.Published;
            evaluatedTarget=target;evaluatedMode=mode;evaluatedDefinitions=definitions;
            foreach(var receipt in store.PublishedAll.Values)
                if(previous==null || !previous.ContainsKey(receipt.Key))
                    RebirthPurgeMilestoneNotification.Send(receipt);
        }
        else opened=false; // Re-open resolves only positively validated original candidates.
        return opened&&ReferenceEquals(evaluated,progress.Published);
    }
}
