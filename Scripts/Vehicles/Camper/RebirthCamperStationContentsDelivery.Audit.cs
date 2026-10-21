using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

internal static partial class RebirthCamperStationContentsDelivery
{
    // Bounded read-only observation. No admission, source plan, native setter,
    // save, spawn, load or journal-directory creation is reachable here.
    internal static object CaptureParcelAuditState(EntityPlayer actor)
    {
        var rows=new JArray();var result=new JObject{["available"]=false,["installed"]=parcelDeliveryInstalled,["originals"]=rows};
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor==null||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return result;
        result["available"]=true;result["localOwnerHeld"]=actor is EntityPlayerLocal local&&HoldsOwner(local);
        string path=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",actor.world.IsRemote()?"RemovalParcelOwnerOriginals":"RemovalParcelOriginals");
        if(!Directory.Exists(path))return result;
        IDisposable workspace=null;
        try
        {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(192L*1024*1024,out workspace))return result;
            var operations=Directory.EnumerateFiles(path,"*.rcj").Select(Path.GetFileName).Where(name=>name.Length>=32).Select(name=>name.Substring(0,32)).Distinct().Take(65).ToArray();
            result["bounded"]=operations.Length>64;
            bool native=RebirthCamperEnergySavedOwner.TryRead(owner,out var savedActor,out var saved);
            foreach(string text in operations.Take(64))
            {
                if(!Guid.TryParseExact(text,"N",out var operation))continue;
                var row=new JObject{["operation"]=text};rows.Add(row);
                try
                {
                    var record=RebirthCamperTransferJournal.ReadExisting(path,operation);
                    if(record==null||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After)||
                        !RebirthCamperRemovalParcelPlan.TryRead(RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024),out var plan)||
                        plan.Owner!=owner.StorageKey||plan.Creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||plan.World!=actor.world.worldState.Guid)
                    {row["unknown"]=true;continue;}
                    row["unknown"]=false;row["binding"]=plan.Binding;row["source"]=plan.Source;row["removal"]=plan.Removal.ToString("N");row["vehicle"]=plan.Vehicle.ToString("N");
                    row["installation"]=plan.Installation.ToString("N");row["owner"]=plan.Owner;row["creation"]=plan.Creation;row["deaths"]=plan.Deaths;
                    row["originalStage"]=(int)record.State;row["overflowStacks"]=plan.Overflow.Length;row["contentsStacks"]=plan.Contents.Length;
                    row["payloadFingerprint"]=RebirthStationPublicationRecord.Digest(RebirthCamperInventorySnapshot.Capture(plan.Contents));
                    bool savedPhase=native&&saved.id==actor.entityId&&saved.deaths==actor.Died&&
                        RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float observedPhase,out bool present)&&present;
                    if(savedPhase)
                    {
                        RebirthCookingRemoteAdmissionBackend.NativePhase(saved,plan.Receipt,out float phase,out _);
                        row["savedOwnerPhase"]=phase;row["savedOwnerOriginalLife"]=saved.deaths==plan.Deaths;
                        row["savedCreditedFrame"]=plan.Credited.Matches(savedActor);row["savedRetiredFrame"]=plan.Retired.Matches(savedActor);
                        int count=phase==1f?0:(int)phase-10;
                        if((phase==1f||phase==Math.Floor(phase)&&count>0)&&count<=plan.Before.Bag.Length)
                        {try{row["savedPrefixMatches"]=Prefix(plan,count).Matches(savedActor);}catch{row["savedPrefixMatches"]=false;}}
                    }
                    row["savedOverflow"]=RebirthCamperRemovalParcelOverflow.Terminal(plan,out var proof);row["overflowProof"]=proof;
                    row["nativeOverflowBagMatches"]=RebirthCamperRemovalParcelOverflow.ObserveLiveContents(plan,out int overflowEntity,out var actualBag,out var expectedBag);
                    row["nativeOverflowEntity"]=overflowEntity;row["nativeOverflowBagFingerprint"]=actualBag;row["expectedOverflowBagFingerprint"]=expectedBag;
                    row["ownerInMemory"]=parcelOwners.ContainsKey(operation);
                    if(actor is EntityPlayerLocal)
                    {
                        var ownerRecord=RebirthCamperTransferJournal.ReadExisting(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOwnerOriginals"),operation);
                        byte[] expected=RebirthCamperRemovalParcelPlan.Bytes(plan.Write());
                        if(ownerRecord!=null&&ownerRecord.Operation==operation&&ownerRecord.Vehicle==plan.Vehicle&&ownerRecord.OwnerCreation==plan.Creation&&ownerRecord.Revision==0&&
                            ownerRecord.Before.SequenceEqual(expected)&&ownerRecord.After.SequenceEqual(expected))row["ownerOriginalStage"]=(int)ownerRecord.State;
                    }
                }
                catch{row["unknown"]=true;}
            }
        }
        catch{result["unknown"]=true;}
        finally{workspace?.Dispose();}
#endif
        return result;
    }
}
