using System;
using System.Collections.Generic;
using System.Xml.Linq;

// Caller owns/disposes this bounded watch. Serializer publication is not region-file or refund proof.
internal sealed class RebirthStationCancellationObservation : IDisposable
{
    private readonly RebirthStationSnapshotEvidence.Request request;
    private readonly Func<bool> observe;
    private static readonly List<RebirthStationCancellationObservation> active=new List<RebirthStationCancellationObservation>();
    private static bool installed;
    private static float nextScan;
    private readonly object world;
    private readonly int thread;
    private readonly float expires;
    private bool closed;
    private readonly EntityPlayer player;
    private readonly RebirthWorldCharacterRecord owner;
    private readonly RebirthStationGridAdmission admission;
    private readonly RebirthStationTerminalIntent intent;
    private readonly RebirthStationCancellationRefund refund;
    private readonly RebirthStationCancellationAttempt attempt;
    private readonly RebirthStationCancellationExpectation expectation;
    private RebirthStationCancellationObservation(RebirthStationSnapshotEvidence.Request r,Func<bool> o,EntityPlayer p,
        RebirthWorldCharacterRecord record,RebirthStationGridAdmission a,RebirthStationTerminalIntent i,
        RebirthStationCancellationRefund f,RebirthStationCancellationAttempt t,RebirthStationCancellationExpectation e)
    {request=r;observe=o;player=p;owner=record;admission=a.Clone();intent=i.Clone();refund=f.Clone();attempt=t.Clone();expectation=e;world=p.world;thread=System.Threading.Thread.CurrentThread.ManagedThreadId;expires=UnityEngine.Time.realtimeSinceStartup+60f;}
    internal static bool TryWatch(EntityPlayer player,Vector3i position,string creation,Guid job,
        out RebirthStationCancellationObservation observation)
    {
        observation=null;
        try
        {
            if(job==Guid.Empty||!RebirthStationLiveAccess.TryResolve(player,position,creation,out var station,out var owner)||
                !RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||active.Count>=RebirthStationSnapshotEvidence.MaximumRequests)return false;
            var key=job.ToString("N");var world=player.world;
            if(!owner.Progression.StationPreparations.TryGetValue(key,out var admission)||admission==null||
                !owner.Progression.StationTerminalIntents.TryGetValue(key,out var intent)||intent==null||
                !owner.Progression.StationCancellationRefunds.TryGetValue(key,out var refund)||refund==null||
                !owner.Progression.StationCancellationAttempts.TryGetValue(key,out var attempt)||attempt==null||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationAttempt(identity,admission,intent,refund,attempt)||
                !RebirthStationCancellationExpectation.TryCreate(station,admission,intent,refund,attempt,out var expectation))return false;
            var image=admission.Write();var attemptImage=attempt.Write();var intentImage=intent.Write();var refundImage=refund.Write();
            if((int)image.Attribute("x")!=position.x||(int)image.Attribute("y")!=position.y||(int)image.Attribute("z")!=position.z||
                (string)image.Attribute("block")!=station.block?.GetBlockName())return false;
            var chunk=world.GetChunkSync(position.x>>4,position.z>>4) as Chunk;
            if(chunk==null)return false;
            Func<bool> observe=()=>ReferenceEquals(player.world,world)&&
                RebirthStationLiveAccess.TryResolve(player,position,creation,out var current,out var currentOwner)&&
                ReferenceEquals(current,station)&&ReferenceEquals(currentOwner,owner)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)&&
                (string)image.Attribute("block")==current.block?.GetBlockName()&&
                owner.Progression.StationPreparations.TryGetValue(key,out var a)&&a!=null&&XNode.DeepEquals(a.Write(),image)&&
                owner.Progression.StationCancellationAttempts.TryGetValue(key,out var t)&&t!=null&&XNode.DeepEquals(t.Write(),attemptImage)&&
                owner.Progression.StationTerminalIntents.TryGetValue(key,out var i)&&i!=null&&XNode.DeepEquals(i.Write(),intentImage)&&
                owner.Progression.StationCancellationRefunds.TryGetValue(key,out var r)&&r!=null&&XNode.DeepEquals(r.Write(),refundImage)&&
                RebirthStationPreparationReservation.TryAcquire(world,owner,admission)&&expectation.MatchesLive(current);
            if(!observe())return false;
            var request=RebirthStationSnapshotEvidence.Watch(world,chunk,position.x>>4,position.z>>4,observe,station,
                expectation.MatchesStation,expectation.MatchesTerminal);
            if(request==null)return false;
            try{observation=new RebirthStationCancellationObservation(request,observe,player,owner,admission,intent,refund,attempt,expectation);
                if(!installed){ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));installed=true;}
                active.Add(observation);return true;}
            catch{observation=null;request.Dispose();throw;}
        }
        catch{return false;}
    }
    internal bool TryGetPublished(out RebirthStationSnapshotEvidence.Publication publication)
    {
        publication=null;
        try{return !closed&&observe()&&RebirthStationSnapshotEvidence.TryGetPublished(request,out publication);}
        catch{publication=null;return false;}
    }
    // Save-only original evidence/retry. Never repeats native removal or delivers a refund.
    internal bool TrySavePublication(string saveRoot,out RebirthStationCancellationPublication publication)
    {
        publication=null;
        try
        {
            if(closed||!observe()||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
            if(!owner.Progression.StationCancellationPublications.TryGetValue(attempt.JobId,out var retained))
            {
                if(owner.Progression.StationCancellationPublications.Count>=64||!TryGetPublished(out var proof)||
                    !RebirthStationCancellationPublication.TryCreate(saveRoot,admission,intent,refund,attempt,expectation,proof,out retained))return false;
                // Recheck live custody after region readback and before retaining the obligation.
                if(!observe()||owner.Progression.StationCancellationPublications.ContainsKey(attempt.JobId))return false;
                owner.Progression.StationCancellationPublications.Add(attempt.JobId,retained);
                RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-retained");
            }
            if(retained==null||!RebirthStationCancellationPublication.TryRead(retained.Write(),admission,intent,refund,attempt,out _)||
                !retained.Revalidate(saveRoot,admission,intent,refund,attempt))return false;
            RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication");
            if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"station-cancelled-publication"))return false;
            if(!observe()||!owner.Progression.StationCancellationPublications.TryGetValue(attempt.JobId,out var current)||current==null||
                !XNode.DeepEquals(current.Write(),retained.Write())||
                !RebirthWorldCharacterRepository.HasSavedStationCancellationPublication(identity,admission,intent,refund,attempt,retained)||
                !retained.Revalidate(saveRoot,admission,intent,refund,attempt))
            {RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-unconfirmed");return false;}
            publication=retained.Clone();return true;
        }
        catch{RebirthWorldCharacterService.MarkDirty(owner,"station-cancelled-publication-uncertain");return false;}
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(active.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        object currentWorld=GameManager.Instance?.World;
        // Teardown must release scarce serializer requests without waiting for the next retry slot.
        for(int i=active.Count-1;i>=0;i--)
            if(!ReferenceEquals(active[i].world,currentWorld))active[i].Dispose();
        if(now<nextScan)return;nextScan=now+1f;
        for(int i=active.Count-1;i>=0;i--)
        {
            var observation=active[i];
            if(observation.thread!=System.Threading.Thread.CurrentThread.ManagedThreadId)continue;
            try
            {
                if(observation.expires<=now||!observation.observe()){observation.Dispose();continue;}
                if(observation.TryGetPublished(out _)&&observation.TrySavePublication(GameIO.GetSaveGameDir(),out _))observation.Dispose();
            }
            catch{observation.Dispose();} // Retained checkpoints remain available to explicit recovery.
        }
    }
    public void Dispose(){if(closed)return;closed=true;active.Remove(this);request.Dispose();}
}