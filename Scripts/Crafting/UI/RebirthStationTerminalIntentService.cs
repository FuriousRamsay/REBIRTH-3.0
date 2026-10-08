using System;
using System.Xml.Linq;

// Save-only terminal choice admission. Success never grants output/refund/reward mutation.
internal static class RebirthStationTerminalIntentService
{
    internal static bool TrySave(EntityPlayer player,Vector3i position,string creation,Guid job,bool completion,
        out RebirthStationTerminalIntent intent)
    {
        intent=null;
        if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            owner.Progression.StationCancellationAttempts.ContainsKey(job.ToString("N"))||
            !owner.Progression.StationPreparations.TryGetValue(job.ToString("N"),out var admission)||admission==null||
            !admission.IsPublicationAttempted)return false;
        if(!completion&&!RebirthStationCompletionReceipt.HasNoJobReceipt(station.CraftCompleteList,admission.JobId))return false;
        RebirthStationTerminalSnapshot terminalBefore=null;
        if(!completion&&!RebirthStationTerminalSnapshot.TryCapture(station,out terminalBefore))return false;
        var image=admission.Write();
        if((int)image.Attribute("x")!=position.x||(int)image.Attribute("y")!=position.y||(int)image.Attribute("z")!=position.z||
            (string)image.Attribute("block")!=station.block?.GetBlockName()||
            !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||
            !RebirthStationTerminalIntent.TryCreate(admission,completion,player.entityId,out var requested))return false;
        RebirthStationNativeQueueSnapshot queueBefore=null;
        if(!completion&&(!RebirthStationNativeQueueEntry.TryBuildCancellation(station.Queue,admission,requested,player.entityId,out _)||
            !RebirthStationNativeQueueSnapshot.TryCapture(station.Queue,out queueBefore)))return false;
        if(owner.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var retained))
        {
            if(retained==null||!XNode.DeepEquals(retained.Write(),requested.Write()))return false;
        }
        else
        {
            if(owner.Progression.StationTerminalIntents.Count>=64)return false;
            retained=requested;owner.Progression.StationTerminalIntents.Add(admission.JobId,retained);
        }
        RebirthWorldCharacterService.MarkDirty(owner,"station-terminal-intent");
        try
        {
            RebirthStationCancellationRefund refund=null;
            if(completion)
            {if(owner.Progression.StationCancellationRefunds.ContainsKey(admission.JobId))return false;}
            else if(owner.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var existingRefund))
            {
                if(existingRefund==null||!RebirthStationCancellationRefund.TryReadStored(existingRefund.Write(),admission,retained,out refund))return false;
            }
            else
            {
                if(owner.Progression.StationCancellationRefunds.Count>=64||
                    !RebirthStationCancellationRefund.TryCreate(admission,XUiM_Recipes.GetRecipes(),retained,out refund))return false;
                owner.Progression.StationCancellationRefunds.Add(admission.JobId,refund);
                RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-refund");
            }
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-terminal-intent"))return false;
            if(!RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)||
                !ReferenceEquals(current,station)||!ReferenceEquals(currentOwner,owner)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
            owner.Progression.StationCancellationAttempts.ContainsKey(job.ToString("N"))||
                (string)image.Attribute("block")!=current.block?.GetBlockName()||
                !completion&&(!queueBefore.Matches(current.Queue)||!terminalBefore.Matches(current)||!RebirthStationCompletionReceipt.HasNoJobReceipt(current.CraftCompleteList,admission.JobId))||
                !owner.Progression.StationPreparations.TryGetValue(admission.JobId,out var savedAdmission)||savedAdmission==null||
                !XNode.DeepEquals(savedAdmission.Write(),image)||
                !owner.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var currentIntent)||currentIntent==null||
                !XNode.DeepEquals(currentIntent.Write(),requested.Write())||
                !RebirthStationPreparationReservation.TryAcquire(player.world,owner,admission)||
                !RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(identity,admission,retained))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-terminal-intent-unconfirmed");return false;}
            if(!completion&&(!owner.Progression.StationCancellationRefunds.TryGetValue(admission.JobId,out var currentRefund)||
                currentRefund==null||!XNode.DeepEquals(currentRefund.Write(),refund.Write())||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationRefund(identity,admission,retained,refund)))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-cancellation-refund-unconfirmed");return false;}
            intent=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-terminal-intent-uncertain");return false;}
    }
}