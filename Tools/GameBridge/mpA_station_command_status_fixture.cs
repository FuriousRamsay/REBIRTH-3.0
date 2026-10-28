using System;
// PREPARED NOT RUN; actual authenticated lease and positive native witness required.
internal static class mpA_station_command_status_fixture
{
    internal static void RequireRequestIdentity(RebirthCamperRemoteStationCommand c,string owner,string creation,RebirthCamperStationAddress address)
    {
        string original=RebirthCamperRemoteStationCommandStatus.Binding(c,owner,creation,address);
        var other=new RebirthCamperRemoteStationCommand(c.Kind,Guid.NewGuid(),c.Job,c.RecipeName,c.Batches,c.Tier);
        if(original==RebirthCamperRemoteStationCommandStatus.Binding(other,owner,creation,address))throw new Exception("Request identity omitted operation.");
    }
    internal static void RequireDuplicateStatusOnly(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object actualLease,RebirthCamperRemoteStationCommand fresh)
    {
        // Harness supplies a genuinely fresh actual authenticated operation.
        // No paid Handle is called by this fixture: count permitted dispatches.
        int permitted=0;
        if(!RebirthCamperRemoteStationCommandStatusStore.TryAdmit(actor,address,creation,actualLease,fresh,out var first,out var a)||a!=RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.NewlyPublished)throw new Exception("Fresh actual identity required.");
        if(a==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.NewlyPublished)permitted++;
        if(!RebirthCamperRemoteStationCommandStatusStore.TryAdmit(actor,address,creation,actualLease,fresh,out var second,out var b)||b!=RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.ExistingStatusOnly||first!=second)throw new Exception("Duplicate did not retain status-only identity.");
        if(b==RebirthCamperRemoteStationCommandStatusStore.AdmissionDisposition.NewlyPublished)permitted++;
        if(permitted!=1)throw new Exception("Duplicate admission permitted blind dispatch.");
    }
    internal static void RequireTypedStatus(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object actualLease,RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.State expected)
    {
        var actual=RebirthCamperRemoteStationCommandStatus.ReadStatus(actor,address,creation,actualLease,command,out var status);
        if(actual!=expected||(actual==RebirthCamperRemoteStationCommandStatus.State.Unknown)!=(status==null))throw new Exception("Unknown status fabricated original evidence.");
        if(status!=null&&(status.Status!=actual||status.AuthorizesCommandRetirement!=(actual==RebirthCamperRemoteStationCommandStatus.State.Applied||actual==RebirthCamperRemoteStationCommandStatus.State.Rejected)))throw new Exception("Pending original became terminal.");
    }
    internal static void RequireObserved(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object actualLease,RebirthCamperRemoteStationCommand command,bool expected)
    {
        if(RebirthCamperRemoteStationCommandStatus.TryReadEffect(actor,address,creation,actualLease,command,out var status)!=expected)throw new Exception("Unexpected original effect status.");
        if(status!=null&&(!status.AuthorizesCommandRetirement||status.RequestOperation!=command.Operation||status.Job!=command.Job||status.Kind!=command.Kind||status.RequestBinding!=RebirthCamperRemoteStationCommandStatus.Binding(command,status.Owner,creation,address)))throw new Exception("Positive status changed request binding.");
    }
}
// Deferred actual scenarios: lost result after saved paid CraftPlayer/recycler
// queue/cancel, exact duplicate/reconnect; queue completed/absent without saved
// witness stays unknown; conflict same request different target/count/tier/kind
// refuses BEFORE Handle; tmp-only/malformed/reparse/limit refuses; no mapping
// never implies never-admitted; later live queue cannot replace original saved
// Before/After. Station-input native Craft and Survivor-only receipts remain
// unknown until their named positive status authority is integrated. Fuel is separate.

// Deferred process interruption: crash after rename but before Handle => next
// admission ExistingStatusOnly, effect status Unknown absent positive witness;
// crash during/after Handle but before result => same status-only query; never
// use queue absence or mapping presence to execute Handle again. Readback failure
// after rename => Unknown now, later exact mapping remains status-only. Conflicting
// duplicate keeps Unknown and never invokes native Craft or any paid callback.

// Typed status vectors: exact saved Prepared/Uncertain plus saved native-before
// original witness => Pending, never dispatch permission; exact Prepared->Rejected
// full history +same historical definition/owner/command => Rejected; rejection
// without saved-before artifact => Unknown; Uncertain->Rejected malformed chain
// => Unknown; missing phase0/unknown tmp/wrong installation/recipe/tier/slot =>
// Unknown. Applied requires positive saved native payment, not phase2 file alone.
