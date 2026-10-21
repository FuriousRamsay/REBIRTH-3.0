using System;
using System.Linq;

// Read-only coupling. A positive native queue publication is separate from
// source disposition; this does not advance Root4 or grant owner permission.
internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static bool ObserveUsed(EntityPlayer actor,Guid sourceOperation)
    {
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,sourceOperation,out var root,out bool unknown)||unknown||root==null||
                !TryReadAdmission(root,out var original)||!Scope(actor,original)||!TryReadPlan(root,out var plan)||!MatchesNativePermission(root,plan)||
                !TryObservePublication(actor.world,root,out string proof))return false;
            if(root.Phase==4)return (string)root.Copy().Attribute("disposition")=="used"&&(string)root.Copy().Attribute("ownerProof")==proof;
            if(root.Phase!=2||(int)root.Copy().Attribute("outcome")!=1||root.Copy().Element("ownerRefund")!=null||root.Copy().Attribute("ownerNoUseProof")!=null||
                (int?)root.Copy().Attribute("ownerRejecting")==1)return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>{body.SetAttributeValue("phase",4);body.SetAttributeValue("disposition","used");body.SetAttributeValue("ownerProof",proof);},
                ()=>Scope(actor,original),out _);
        }
        catch{return false;}
    }
    internal static bool TryObservePublication(World world,RebirthRemoteResourceDurableJournal.Record root,out string proof)
    {
        proof=null;
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||root==null||
                !TryReadPlan(root,out var plan)||world.worldState.Guid!=plan.World||
                !RebirthRemoteResourceStationPaymentWitness.TryReadSavedPayment(world,plan.Job,plan.Address,plan.OwnerKey,plan.Creation,out var saved)||
                saved.OriginalDeaths!=plan.Deaths||saved.QueueSlot!=plan.QueueSlot||saved.InstalledDefinition!=plan.InstalledDefinition||!plan.CopyAfterActor().Matches(saved.RestoreAfterActor())||
                !RebirthCamperStationAddress.TryParse(plan.Address,out var address))return false;
            var before=saved.RestoreBefore();var after=saved.RestoreAfter();
            if(!RebirthCamperStationSnapshot.Capture(address.Installation,before).SequenceEqual(plan.CopyBeforeStation()))return false;
            var row=after.Queue[plan.QueueSlot];
            if(row?.Recipe==null||!RebirthCamperStationNativeCrafting.RestoreActorImages(row.Recipe)||!RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(row.Recipe)||
                !RebirthCamperStationSnapshot.Capture(address.Installation,after).SequenceEqual(plan.CopyAfterStation()))return false;
            // All rich original fragments, quality/count, frozen output,
            // installation and unrelated queue frame participate in the byte
            // comparison. A matching job string or Applied alone cannot pass.
            proof=RebirthRemoteResourceDurableJournal.Digest("native-mobile-station-publication|"+root.Operation.ToString("N")+"|"+root.Binding+"|"+
                plan.Job.ToString("N")+"|"+saved.Proof+"|"+RebirthRemoteResourceFetchedStationPlan.Image(plan).ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
            return true;
        }
        catch{proof=null;return false;}
    }
}
