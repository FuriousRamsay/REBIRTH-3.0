using System;
using System.Collections.Generic;

// P6 -> B13: forwards every committed outcome to P1's durable owner-award journal. P1's policy
// (RebirthP1B13Policy.Resolve) decides from the retained outcome and, for crafts, P7's pickup receipt, so the
// outcome sink never waits for an award and there is no cycle. Qualification of storage moves is P10's
// relocation witness assigned to RebirthP1B13Policy.RelocationWitness; this bridge only has to offer the outcome.
internal static class RebirthP6B13Bridge
{
    // Only results of Unavailable are retried (the journal could not accept the entry yet); Acknowledged,
    // Journaled and Invalid are final. No attempt cap: an unavailable journal must not forfeit a saved action.
    private static readonly Queue<RebirthNpcWorkOutcomeRecord> Unavailable=new Queue<RebirthNpcWorkOutcomeRecord>();
    private static readonly HashSet<Guid> UnavailableIds=new HashSet<Guid>();
    // Receipt protection is owned by P1's journal; kept so the registration call sites stay stable.
    internal static void EnsureProtection(){}
    internal static void ClearWorldState(){Unavailable.Clear();UnavailableIds.Clear();}
    // The bounded outcome log must retain these originals until the durable
    // P1 journal accepts them. This queue is de-duplicated and cannot grow past
    // the retained outcome log while that retention rule is in force.
    internal static HashSet<Guid> PendingUnavailableOutcomeIds()=>new HashSet<Guid>(UnavailableIds);
    internal static void Offer(RebirthNpcWorkOutcomeRecord outcome)
    {
        if(outcome==null)return;
        RebirthP1AwardQueueResult result;
        try{result=RebirthP1SharedWorkAward.QueueOwnerAward(outcome);}
        catch{result=RebirthP1AwardQueueResult.Unavailable;}
        if(result==RebirthP1AwardQueueResult.Unavailable&&UnavailableIds.Add(outcome.OutcomeId))Unavailable.Enqueue(outcome);
    }
    internal static void Tick()
    {
        int count=Unavailable.Count;
        for(int i=0;i<count;i++)
        {
            var outcome=Unavailable.Dequeue();
            RebirthP1AwardQueueResult result;
            try{result=RebirthP1SharedWorkAward.QueueOwnerAward(outcome);}
            catch{result=RebirthP1AwardQueueResult.Unavailable;}
            if(result==RebirthP1AwardQueueResult.Unavailable)Unavailable.Enqueue(outcome);
            else UnavailableIds.Remove(outcome.OutcomeId);
        }
    }
}
