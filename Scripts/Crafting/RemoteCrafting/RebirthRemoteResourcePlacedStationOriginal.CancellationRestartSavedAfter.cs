using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Both processes may lose context after Applied Terminal, before phase4
    // reaches the owner. This is NOT recovery of an attempted financial write:
    // full Applied native After/Mutation/Terminal and owner2 are mandatory.
    private static bool SavedRestartOwnerMatches(CancellationPendingOwner candidate,CancellationRestartArchive archive)
    {
        if(candidate.ReceiptPhase!=2&&candidate.ReceiptPhase!=3||candidate.ReceiptPhase==2&&archive.AckSaved||candidate.Owner.CanonicalId!=archive.Original.Owner||candidate.Deaths!=archive.Original.Deaths||
            !RebirthCamperEnergySavedOwner.TryRead(candidate.Owner,out var image,out var saved)||saved.id<1||saved.deaths!=candidate.Deaths||
            !archive.After.Matches(image)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(archive.Operation),out float phase,out bool present)||
            !present||phase!=candidate.ReceiptPhase)return false;
        // Candidate selection is never a body or financial permission. Full
        // terminal archive was reconstructed independently before this check.
        return true;
    }
    private static bool CurrentSavedAfterRestartOwner(EntityPlayer actor,CancellationRestartArchive archive)
    {
        return archive.SavedAfterRestart&&ServerScope(actor,archive.Original)&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
            RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var saved)&&saved.id==actor.entityId&&saved.deaths==archive.Original.Deaths&&
            archive.After.Matches(image)&&RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(archive.Operation),out float phase,out bool present)&&present&&phase==2f;
    }
    private static void SendRestartSavedAfter(EntityPlayer actor,CancellationRestartArchive archive,CancellationLoaded loaded)
    {
        if(archive.Memory?.Current!=true||archive.ReplyPending||!CurrentSavedAfterRestartOwner(actor,archive)||loaded==null||
            !ReferenceEquals(archive.Load?.Loaded,loaded)||!RevalidateRestartTerminal(archive)||!ValidateLoadedRestartImage(archive,loaded))return;
        // The ordinary v2 reconstruction event is from the SAME private actual
        // persisted loader/factory/read. No current-equality token is created.
        string reconstruction=loaded.Encode();long bound=checked(1024+EscapedCancellationBytes(archive.Permission)+EscapedCancellationBytes(archive.AfterEvidence)+EscapedCancellationBytes(reconstruction));
        ObserveCancellationScenarioEvent(archive.Operation,"actualNativeAfterLoad2",reconstruction);
        if(bound>MaximumResultBytes)return;long workspace=checked(8*bound+65536);
        if(workspace>archive.ReplyWorkspace){if(!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+workspace-archive.ReplyWorkspace)))return;archive.ReplyWorkspace=workspace;}
        var reply=new XElement("placedStationCancelResult",new XAttribute("version",1),new XAttribute("original",archive.Original.Operation.ToString("N")),
            new XAttribute("cancel",archive.Operation.ToString("N")),new XAttribute("phase",4),new XAttribute("grant",false),
            new XElement("permission",archive.Permission),new XElement("evidence",archive.AfterEvidence),new XElement("reconstruction",reconstruction));
        string text=reply.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(text)>MaximumResultBytes)return;
        var retained=archive.Memory;
        archive.ReplyPending=NetPackageRebirthRemoteRepairResult.SendObserved(actor.entityId,archive.Original,text,sent=>
        {
            archive.ReplyPending=false;
            // Phase4 does not retire the actual loader/source buffers. The
            // SAME owner3/full terminal +FinalAck must finish them normally.
            if(archive.Memory==null)retained.Dispose();
        });
    }
}
