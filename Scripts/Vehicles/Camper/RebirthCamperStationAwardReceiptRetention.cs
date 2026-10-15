using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Associates the actual receipt set with its progression owner without keeping
// discarded snapshots alive. Protection lasts while mobile custody is retained.
internal static class RebirthCamperStationAwardReceiptRetention
{
    private const string Prefix="camper.craft.v1:";
    private static readonly ConditionalWeakTable<HashSet<string>,RebirthWorldProgressionState> owners
        =new ConditionalWeakTable<HashSet<string>,RebirthWorldProgressionState>();
    private static readonly Func<HashSet<string>,ISet<string>> provider=GetProtected;
    internal static void Register(RebirthWorldProgressionState progression)
    {
        if(progression==null)throw new ArgumentNullException(nameof(progression));
        owners.GetValue(progression.SkillAwardReceipts,_=>progression);
        RebirthTeachingOutcomeStore.RegisterReceiptProtection(provider);
    }
    internal static ISet<string> GetProtected(HashSet<string> receipts)
    {
        var result=new HashSet<string>(StringComparer.Ordinal);
        if(receipts==null)return result;
        if(!owners.TryGetValue(receipts,out var progression))
        {
            // A detached/unassociated set cannot prove that mobile custody cleared.
            foreach(var receipt in receipts)if(receipt!=null&&receipt.StartsWith(Prefix,StringComparison.Ordinal))result.Add(receipt);
            return result;
        }
        foreach(var admission in progression.StationPreparations.Values)
            if(admission!=null&&admission.IsPublicationAttempted&&admission.IsMobile)
                result.Add(RebirthCamperStationTrainingPlan.ReceiptFor(admission));
        // Retain parent digest identities even during an uncertain in-memory
        // cleanup, rather than inferring settlement from a missing preparation.
        foreach(var record in progression.CamperStationPublications.Values)
        {if(record==null)throw new InvalidOperationException("Unknown mobile publication custody.");result.Add(Prefix+(string)record.Write().Attribute("admission"));}
        foreach(var record in progression.CamperStationCompletions.Values)
        {if(record==null)throw new InvalidOperationException("Unknown mobile completion custody.");result.Add(Prefix+(string)record.Write().Attribute("admission"));}
        foreach(var record in progression.CamperStationTrainingPlans.Values)
        {if(record==null)throw new InvalidOperationException("Unknown mobile training custody.");result.Add(record.Receipt);}
        return result;
    }
}
