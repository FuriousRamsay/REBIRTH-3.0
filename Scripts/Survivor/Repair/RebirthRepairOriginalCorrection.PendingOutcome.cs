using System;
using System.Linq;

internal static partial class RebirthRepairOriginalCorrection
{
    // Publish this actual native invocation's known outcome before consuming
    // a delayed admission/correction reply that could change its CAS head.
    private static bool PublishPendingOutcome(Client client)
    {
        if(client?.PendingOutcome==null)return true;
        try
        {
            var outcome=client.PendingOutcome;string expected=client.PendingExpected;
            if(client.Receipt==null||outcome.Operation!=client.Receipt.Operation||expected!=Encode(client.Receipt)||
                !SamePhysicalActor(client)||!Save(outcome,false,expected))return false;
            client.Receipt=outcome;client.PendingOutcome=null;client.PendingExpected=null;
            if(outcome.NativeDelivered||outcome.CancelReturned)
                foreach(var card in Cards.Where(pair=>ReferenceEquals(pair.Value,client)).Select(pair=>pair.Key).ToArray())Cards.Remove(card);
            return true;
        }
        catch(Exception error){Log.Warning("[REBIRTH Repair] native outcome publication retained: "+error.GetType().Name);return false;}
    }
}
