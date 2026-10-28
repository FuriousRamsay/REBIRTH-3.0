using System;
using System.Linq;
using System.Reflection;

// PREPARED, NOT RUN. Requires a real authenticated request already accepted
// through the installed sole native hook and its actual retained original.
// This never creates an attempted receipt or a packet identity for a test.
internal static class mpA_logistics_native_invocation_fixture
{
    internal static void AfterActualAttempt(RebirthLogisticsTransferJournal.Record actual,EntityPlayer actor,NetPackageRequestToSpawnEntity packet)
    {
        if(!RebirthLogisticsTransferJournal.TryRead(actual.Owner,actual.Operation,out var retained)||
            RebirthLogisticsTransferJournal.Encode(actual)!=RebirthLogisticsTransferJournal.Encode(retained)||
            !RebirthLogisticsNativeDeathReceipt.TryRead(retained,out var fatal)||!RebirthLogisticsNativeInvocationJournal.HasAttempt(retained))throw new Exception("Requires actual durable native invocation and fatal original.");
        if(!RebirthLogisticsNativeInvocationJournal.IsNativeBody(packet,actor.entityId))throw new Exception("Requires same actual native backpack request.");
        // Existing attempted metadata cannot authorize another native effect,
        // even if its body/publication is absent after a cold interruption.
        if(RebirthLogisticsNativeInvocationJournal.TryBegin(retained,fatal,actor,packet))throw new Exception("Original invocation replay was permitted.");
        if(!RebirthLogisticsNativeInvocationJournal.HasAttempt(retained))throw new Exception("Replay discarded original evidence.");
        object scope=RebirthLogisticsDeathCustody.Enter(packet,actor.world,out bool allow);
        try{if(allow)throw new Exception("Actual sole-hook logistics predicate permitted retained replay.");}
        finally{RebirthLogisticsDeathCustody.Leave(scope);}
    }
    internal static void VerifyUnrelatedNativeRequest(NetPackageRequestToSpawnEntity actualUnrelated,World world)
    {
        // Harness supplies an authenticated request outside any logistics
        // original death. An arbitrary null/fake packet is not this scenario.
        if(actualUnrelated?.Sender==null||world?.IsRemote()!=false)throw new Exception("Requires real unrelated native sender/request.");
        object scope=RebirthLogisticsDeathCustody.Enter(actualUnrelated,world,out bool allow);
        try{if(!allow)throw new Exception("Unrelated native request was refused by logistics.");}
        finally{RebirthLogisticsDeathCustody.Leave(scope);}
    }
}
