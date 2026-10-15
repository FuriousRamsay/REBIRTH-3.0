using System;

// Retained save-only operation. No queue removal or refund authority is granted
// by staging cached records or requesting a save.
internal sealed class RebirthCamperStationCancellationSave
{
    private readonly RebirthCamperStationCancellationCheckpoint checkpoint;
    private bool confirmed;
    private RebirthCamperStationCancellationSave(RebirthCamperStationCancellationCheckpoint checkpoint)
    {this.checkpoint=checkpoint;}
    internal static bool TryBegin(RebirthCamperStationCancellationCheckpoint checkpoint,out RebirthCamperStationCancellationSave operation)
    {
        operation=null;
        try
        {
            if(!ThreadManager.IsMainThread()||checkpoint==null||!checkpoint.MatchesBefore())return false;
            var binding=checkpoint.Binding;var progression=binding.Progression;string job=binding.Admission.JobId;
            if(progression.StationTerminalIntents.ContainsKey(job)||progression.StationCancellationRefunds.ContainsKey(job)||
                progression.StationCancellationAttempts.ContainsKey(job)||progression.StationTerminalIntents.Count>=64||
                progression.StationCancellationRefunds.Count>=64||progression.StationCancellationAttempts.Count>=64)return false;
            var choice=checkpoint.CopyIntent();var refund=checkpoint.CopyRefund();var attempt=checkpoint.CopyAttempt();
            if(!checkpoint.MatchesBefore())return false;
            // Retain the operation before any cached custody change. An exception
            // leaves existing records held; it never authorizes repeating effects.
            operation=new RebirthCamperStationCancellationSave(checkpoint);
            progression.StationTerminalIntents.Add(job,choice);
            progression.StationCancellationRefunds.Add(job,refund);
            progression.StationCancellationAttempts.Add(job,attempt);
            RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-cancellation-checkpoint");
            return checkpoint.MatchesStagedBefore();
        }
        catch{return false;}
    }
    internal bool TrySave()
    {
        try
        {
            if(!ThreadManager.IsMainThread()||!checkpoint.MatchesStagedBefore())return false;
            var binding=checkpoint.Binding;
            if(!confirmed)
            {
                RebirthWorldCharacterService.MarkDirty(binding.Owner,"camper-cancellation-checkpoint");
                if(!RebirthWorldCharacterService.FlushPlayer(binding.Player,"camper-cancellation-checkpoint")||
                    !checkpoint.MatchesStagedBefore())return false;
            }
            if(!RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(binding.Identity,binding.Admission,
                checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt())||!checkpoint.MatchesStagedBefore())return false;
            confirmed=true;return true;
        }
        catch{return false;}
    }
    internal bool IsConfirmedFor(RebirthCamperStationCancellationCheckpoint expected)
        =>confirmed&&ReferenceEquals(checkpoint,expected)&&TrySave();
    internal bool IsSavedForRemoval(RebirthCamperStationCancellationCheckpoint expected)
    {
        try
        {
            var binding=checkpoint.Binding;
            return confirmed&&ReferenceEquals(checkpoint,expected)&&checkpoint.MatchesStagedOwner()&&
                RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(binding.Identity,binding.Admission,
                    checkpoint.CopyIntent(),checkpoint.CopyRefund(),checkpoint.CopyAttempt())&&checkpoint.MatchesStagedOwner();
        }
        catch{return false;}
    }
}
