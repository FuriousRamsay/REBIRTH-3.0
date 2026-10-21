using System;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceBlockAdoption
{
    internal static string EndedOwnerProof(RebirthRemoteResourceClientOriginal.Intent original,string binding,XElement action)=>
        RebirthRemoteResourceDurableJournal.Digest("native-block-ended-owner2-no-command|"+original.Digest+"|"+binding+"|"+action.ToString(SaveOptions.DisableFormatting));
    internal static bool ValidateEndedOwnerRoot(XElement image)
    {
        try
        {
            if(!Guid.TryParseExact((string)image.Attribute("operation"),"N",out var operation)||
                !RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(operation,out var original))return false;
            var root=new RebirthRemoteResourceDurableJournal.Record(operation,0,image);
            return root.Owner==RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)&&root.Creation==original.Creation&&
                root.Operation==RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,original.Creation,original.Epoch,original.Request,original.Kind)&&
                (int?)image.Attribute("deaths")==original.Deaths&&(int?)image.Attribute("kind")== (int)original.Kind&&
                RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)&&RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)&&
                RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)&&(string)image.Attribute("ownerNoUseProof")==EndedOwnerProof(original,root.Binding,action);
        }
        catch{return false;}
    }
    private static bool TryAdoptSavedEndedOwner(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,
        out RebirthRemoteResourceDurableJournal.Record result)
    {
        result=null;
        try
        {
            if(root.Phase!=2||actor.Died<=original.Deaths||!root.HasOwnerAction||root.Copy().Attribute("ownerNoUseProof")!=null||
                !HasSavedAdmission(actor,original,root)||!RebirthRemoteResourceBlockSettlement.MatchesRootAction(original,root)||
                !RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original)||!RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var saved)||
                saved.id!=actor.entityId||saved.deaths!=actor.Died||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=2f)return false;
            // Native2 is written only after the complete original owner delta
            // and command capture returned. Current saved native2 establishes
            // that historical paid handoff; it grants no old actor restoration.
            // All host command/attempt candidates must be positively absent;
            // later old-life stage2 messages fail the original-life wire gate.
            int deaths=actor.Died;string proof=EndedOwnerProof(original,root.Binding,action);
            bool Current()=>actor.Died==deaths&&HasSavedAdmission(actor,original,root)&&
                RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original);
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("ownerRejecting",1);value.SetAttributeValue("ownerNoUseProof",proof);},Current,out result);
        }
        catch{result=null;return false;}
    }
}
