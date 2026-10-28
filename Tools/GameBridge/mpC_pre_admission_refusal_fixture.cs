using System;
using System.IO;
using System.Linq;

// Authored, not executed. Host native-context checks require a disposable MP
// save and a real authenticated original captured before source admission.
// Calling this fixture is itself a terminal cancellation of that original.
internal static class RemotePreAdmissionRefusalChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    internal static void Fresh(EntityPlayer actualOwner,RebirthRemoteResourceClientOriginal.Intent capturedOriginal)
    {
        Require(RebirthStablePlayerIdentity.TryResolveServerEntity(actualOwner,out var identity),"actual current native owner");
        Require(RemoteResourceWorldIdentity.TryGet(actualOwner.world,out var world),"actual current world");
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,capturedOriginal.Creation,capturedOriginal.Epoch,capturedOriginal.Request,capturedOriginal.Kind);
        Require(RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(identity.StorageKey,source),"fixture original has no source or interrupted publication");
        string before=RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(actualOwner));
        Require(RebirthRemoteResourcePreAdmissionRefusal.TryRefuse(actualOwner,capturedOriginal,out var proof),"positive exact never-admitted terminal publication");
        Require(RebirthRemoteResourcePreAdmissionRefusal.BlocksAdmission(source),"durable tombstone forbids delayed admission");
        Require(RebirthRemoteResourcePreAdmissionRefusal.TryRefuse(actualOwner,capturedOriginal,out var retry)&&retry==proof,"same original retry returns same terminal proof");
        Require(RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(identity.StorageKey,source),"refusal did not manufacture a source payment/outcome");
        Require(before==RebirthRemoteResourceClientOriginal.ActorImage(RebirthCamperEnergyActorImage.Capture(actualOwner)),"no host native owner inventory mutation");
    }
    internal static void InterruptedAdmission(EntityPlayer actualOwner,RebirthRemoteResourceClientOriginal.Intent distinctCapturedOriginal)
    {
        Require(RebirthStablePlayerIdentity.TryResolveServerEntity(actualOwner,out var identity),"actual current native owner");
        Require(RemoteResourceWorldIdentity.TryGet(actualOwner.world,out var world),"actual current world");
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,distinctCapturedOriginal.Creation,distinctCapturedOriginal.Epoch,distinctCapturedOriginal.Request,distinctCapturedOriginal.Kind);
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",distinctCapturedOriginal.Kind==RemoteResourceClientOperation.Craft?"RemoteCraftSourceAdmission":"RemoteRepairSourceAdmission");
        Directory.CreateDirectory(directory);
        Require(!Directory.EnumerateFileSystemEntries(directory,source.ToString("N")+"*").Any(),"distinct disposable original only");
        string candidate=Path.Combine(directory,source.ToString("N")+".0.rcj."+Guid.NewGuid().ToString("N")+".tmp");
        using(var file=new FileStream(candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.WriteByte(1);file.Flush(true);}
        Require(!RebirthRemoteResourcePreAdmissionRefusal.TryRefuse(actualOwner,distinctCapturedOriginal,out _),"orphan initial admission candidate is unknown, never absence");
        Require(File.Exists(candidate),"unknown evidence retained unchanged");
    }
    internal static void PaidOriginal(EntityPlayer actualOwner,RebirthRemoteResourceClientOriginal.Intent actualPaidOriginal)
    {
        Require(RebirthStablePlayerIdentity.TryResolveServerEntity(actualOwner,out var identity),"actual native owner");
        Require(RemoteResourceWorldIdentity.TryGet(actualOwner.world,out var world),"actual world");
        Require(actualOwner.Died==actualPaidOriginal.Deaths&&actualOwner.world.worldState.Guid==actualPaidOriginal.World&&identity.CanonicalId==actualPaidOriginal.Owner&&
            RebirthCamperCharacterAccess.CurrentCreation(actualOwner)==actualPaidOriginal.Creation,"negative control uses the current original life");
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,actualPaidOriginal.Creation,actualPaidOriginal.Epoch,actualPaidOriginal.Request,actualPaidOriginal.Kind);
        Require(RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,source,out var root,out bool unknown)&&!unknown&&root!=null&&root.Phase>=2&&
            (int)root.Copy().Attribute("outcome")==1,"actual positively paid source fixture");
        string before=root.Copy().ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        Require(!RebirthRemoteResourcePreAdmissionRefusal.TryRefuse(actualOwner,actualPaidOriginal,out _),"paid original cannot become never-admitted");
        Require(RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,source,out var after,out unknown)&&!unknown&&after?.Copy().ToString(System.Xml.Linq.SaveOptions.DisableFormatting)==before,
            "paid original is retained without changing its disposition or refunding sources");
    }
}
