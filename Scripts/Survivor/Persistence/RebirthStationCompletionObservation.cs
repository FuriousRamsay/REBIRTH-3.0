using System;
using System.Xml.Linq;

// INACTIVE original-event consumer. Caller owns retry/disposal and must invoke only at successful original native exit.
// No output replay, native reward release, XP grant or discovery activation.
internal sealed class RebirthStationCompletionObservation : IDisposable
{
    private readonly RebirthStationCompletionCapture.SuccessfulOutput completed;
    private readonly RebirthStationCompletionOriginalScope original;
    private readonly RebirthStationTerminalIntent intent;
    private readonly RebirthStationCompletionExpectation expectation;
    private readonly RebirthStationSnapshotEvidence.Request request;
    private readonly Chunk chunk;
    private readonly string saveRoot;
    private bool closed;
    private RebirthStationCompletionObservation(RebirthStationCompletionCapture.SuccessfulOutput e,RebirthStationTerminalIntent i,
        RebirthStationCompletionExpectation x,RebirthStationSnapshotEvidence.Request r,Chunk c,string root)
    {completed=e;original=e.Original;intent=i.Clone();expectation=x;request=r;chunk=c;saveRoot=root;}
    internal static bool TryBegin(RebirthStationCompletionCapture.SuccessfulOutput completed,out RebirthStationCompletionObservation observation)
    {
        observation=null;
        RebirthStationSnapshotEvidence.Request request=null;
        try
        {
            if(completed==null||!completed.MatchesReceipt())return false;
            var original=completed.Original;var key=original.Admission.JobId;
            // Completion choice is recorded AFTER authentic effect; cancellation conflicts remain refusal.
            if(!RebirthStationTerminalIntent.TryCreate(original.Admission,true,original.Actor,out var requested)||
                !RebirthStationCompletionExpectation.TryCreate(original.Station,original.Admission,requested,original.Queued,XUiM_Recipes.GetRecipes(),out var expectation))return false;
            var owner=original.Owner;
            if(owner.Progression.StationTerminalIntents.TryGetValue(key,out var retained))
            {if(retained==null||!XNode.DeepEquals(retained.Write(),requested.Write()))return false;}
            else
            {
                if(owner.Progression.StationTerminalIntents.Count>=64)return false;
                owner.Progression.StationTerminalIntents.Add(key,requested.Clone());
                RebirthWorldCharacterService.MarkDirty(owner,"station-authentic-completion-intent");
            }
            if(!original.IsCurrent()||!completed.MatchesReceipt()||
                !RebirthWorldCharacterRepository.SaveIfDirty(original.Identity,"station-authentic-completion-intent")||
                !original.IsCurrent()||!RebirthWorldCharacterRepository.HasSavedStationTerminalIntent(original.Identity,original.Admission,requested)||
                !RebirthWorldCharacterRepository.HasSavedStationPublication(original.Identity,original.Admission,original.Queued)||!expectation.MatchesLive(original.Station)||!original.IsCurrent())return false;
            var chunk=original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4) as Chunk;
            if(chunk==null)return false;
            string root=original.SaveRoot;
            Func<bool> current=()=>original.IsCurrent()&&completed.MatchesReceipt()&&
                ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),chunk)&&
                string.Equals(System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),root,RebirthStationCompletionOriginalScope.PathComparison)&&
                owner.Progression.StationTerminalIntents.TryGetValue(key,out var i)&&i!=null&&XNode.DeepEquals(i.Write(),requested.Write())&&
                expectation.MatchesLive(original.Station)&&original.IsCurrent();
            if(!current())return false;
            request=RebirthStationSnapshotEvidence.Watch(original.World,chunk,original.Position.x>>4,original.Position.z>>4,
                current,original.Station,expectation.MatchesStation,expectation.MatchesTerminal);
            if(request==null)return false; // Event remains unclaimed for exact retry; retained intent never authorizes output replay.
            if(!current()||!completed.TryClaim()){request.Dispose();return false;}
            observation=new RebirthStationCompletionObservation(completed,requested,expectation,request,chunk,root);return true;
        }
        catch{request?.Dispose();return false;}
    }
    private bool IsCurrent()
    {
        try
        {
            return !closed&&original.IsCurrent()&&completed.MatchesReceipt()&&
                ReferenceEquals(original.World.GetChunkSync(original.Position.x>>4,original.Position.z>>4),chunk)&&
                string.Equals(System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),saveRoot,RebirthStationCompletionOriginalScope.PathComparison)&&
                original.Owner.Progression.StationTerminalIntents.TryGetValue(original.Admission.JobId,out var retained)&&retained!=null&&
                XNode.DeepEquals(retained.Write(),intent.Write())&&expectation.MatchesLive(original.Station)&&original.IsCurrent();
        }
        catch{return false;}
    }
    internal bool TrySavePublication(out RebirthStationCompletionPublication publication)
    {
        publication=null;
        try
        {
            if(!IsCurrent())return false;
            var owner=original.Owner;var key=original.Admission.JobId;var state=owner.Progression;
            bool hasCompleted=state.StationCompletionPublications.TryGetValue(key,out var retained);
            bool hasProjection=state.StationCompletionExpectationProjections.TryGetValue(key,out var projection);
            if(hasCompleted!=hasProjection)return false;
            if(!hasCompleted)
            {
                if(state.StationCompletionPublications.Count>=64||state.StationCompletionExpectationProjections.Count>=64||
                    !RebirthStationSnapshotEvidence.TryGetPublished(request,out var proof)||
                    !RebirthStationCompletionPublication.TryCreate(saveRoot,original.Admission,intent,original.Queued,expectation,proof,out retained)||
                    !RebirthStationCompletionExpectationProjection.TryCapture(completed,expectation,intent,retained,proof,out projection)||!IsCurrent())return false;
                var nextCompleted=new System.Collections.Generic.Dictionary<string,RebirthStationCompletionPublication>(state.StationCompletionPublications,StringComparer.Ordinal);
                var nextProjections=new System.Collections.Generic.Dictionary<string,RebirthStationCompletionExpectationProjection>(state.StationCompletionExpectationProjections,StringComparer.Ordinal);
                nextCompleted.Add(key,retained);nextProjections.Add(key,projection);
                RebirthStationCompletionExpectationProjection.WriteAll(nextProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,nextCompleted,XUiM_Recipes.GetRecipes());
                if(!IsCurrent()||!ReferenceEquals(owner.Progression,state)||state.StationCompletionPublications.ContainsKey(key)||state.StationCompletionExpectationProjections.ContainsKey(key))return false;
                state.StationCompletionPublications.Add(key,retained);
                try{state.StationCompletionExpectationProjections.Add(key,projection);}catch{state.StationCompletionPublications.Remove(key);throw;}
                RebirthWorldCharacterService.MarkDirty(owner,"station-authentic-completion-pair-retained");
            }
            if(retained==null||projection==null||!retained.Revalidate(saveRoot,original.Admission,intent,original.Queued,expectation)||
                !projection.CompareCold(saveRoot,original.Admission,intent,original.Queued,retained,XUiM_Recipes.GetRecipes())||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var preSaveCompleted)||!ReferenceEquals(preSaveCompleted,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var preSaveProjection)||!ReferenceEquals(preSaveProjection,projection))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-authentic-completion-pair");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(original.Identity,"station-authentic-completion-pair")||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var current)||!ReferenceEquals(current,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var currentProjection)||!ReferenceEquals(currentProjection,projection)||
                !RebirthWorldCharacterRepository.HasSavedStationCompletionExpectationProjection(original.Identity,original.Admission,intent,original.Queued,retained,projection)||
                !retained.Revalidate(saveRoot,original.Admission,intent,original.Queued,expectation)||
                !projection.CompareCold(saveRoot,original.Admission,intent,original.Queued,retained,XUiM_Recipes.GetRecipes())||!IsCurrent()||
                !ReferenceEquals(owner.Progression,state)||!state.StationCompletionPublications.TryGetValue(key,out var finalCompleted)||!ReferenceEquals(finalCompleted,retained)||
                !state.StationCompletionExpectationProjections.TryGetValue(key,out var finalProjection)||!ReferenceEquals(finalProjection,projection))return false;
            publication=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(original.Owner,"station-authentic-completion-pair-uncertain");return false;}
    }

    public void Dispose(){if(closed)return;closed=true;request.Dispose();}
}
