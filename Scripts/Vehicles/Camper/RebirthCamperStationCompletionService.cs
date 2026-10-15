using System;
using System.Xml.Linq;

// Save-only original completion publication. No output insertion, queue replay,
// ingredient debit, refund, reward grant or pending-effect release occurs here.
internal static class RebirthCamperStationCompletionService
{
    internal static bool TryRecord(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,
        RebirthCamperStationSaveEvidence.Publication proof)
    {
        if(!ThreadManager.IsMainThread()||completed==null||proof==null||
            !RebirthCamperStationCompletionRecord.TryCreate(completed,proof,out var receipt))return false;
        var binding=completed.Binding;var progression=binding.Progression;var job=binding.Admission.JobId;
        bool staged=false;
        Func<bool> current=()=>binding.IsCurrent()&&completed.MatchesLiveOutput()&&
            RebirthCamperStationCompletionCallsite.TryGetSuccessfulOutput(binding.Station,job,out var retainedEvent)&&
            ReferenceEquals(retainedEvent,completed)&&
            (progression.CamperStationCompletions.TryGetValue(job,out var retained)
                ?retained!=null&&XNode.DeepEquals(retained.Write(),receipt.Write()):!staged)&&
            !progression.StationCompletionPublications.ContainsKey(job)&&
            !progression.StationCompletionExpectationProjections.ContainsKey(job)&&
            !progression.StationCancellationPublications.ContainsKey(job)&&
            !progression.StationCancellationRefunds.ContainsKey(job);
        try
        {
            if(!current()||!RebirthWorldCharacterRepository.HasSavedCamperStationPublication(binding.Identity,binding.Admission,binding.Publication))return false;
            if(progression.CamperStationCompletions.TryGetValue(job,out var existing))
            {if(existing==null||!XNode.DeepEquals(existing.Write(),receipt.Write()))return false;}
            else progression.CamperStationCompletions.Add(job,receipt.Clone());
            staged=true;
            RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-station-completion-publication");
            return current()&&RebirthWorldCharacterService.FlushPlayer(binding.Player,"camper-station-completion-publication")&&current()&&
                RebirthWorldCharacterRepository.HasSavedCamperStationCompletion(binding.Identity,binding.Admission,binding.Publication,receipt)&&current();
        }
        catch{return false;}
    }
}
