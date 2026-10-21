using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static partial class RebirthNpcAdvancedProgressionPersistenceStore
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var uninitialized=CaptureUninitializedNativeAdoption(scope);
            if(uninitialized!=null)return uninitialized;
            if(!loaded||dirty||flushing||loadFailed||originalBinding==null||!scope.MatchesBinding(boundWorld as World,boundPath,true))return null;
            var oldWorld=boundWorld;string path=boundPath;var binding=originalBinding;
            RebirthNpcAdvancedProgressionService.CaptureRetirement(out var records,out var sessions,out var epoch);
            string persistent=SnapshotDocument(records,sessions).DocumentElement.OuterXml;
            string full=QuarantineImage().OuterXml;
            bool Primary()
            {
                try
                {
                    if(!File.Exists(path)||File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
                    var data=ReadDocument(RebirthNpcRecoveryCurrentBacking.Read(path));
                    return SnapshotDocument(data.Records.ToArray(),data.Sessions.ToArray()).DocumentElement.OuterXml==persistent;
                }
                catch{return false;}
            }
            bool Current(){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==path&&
                ReferenceEquals(originalBinding,binding)&&binding.MatchesRetirement(epoch)&&ReferenceEquals(epoch,RebirthNpcAdvancedProgressionService.RecoveryEpoch)&&
                QuarantineImage().OuterXml==full&&Primary();}
            if(!Current())return null;
            var participants=new HashSet<RebirthNpcStableId>();
            foreach(var session in sessions)if(session.Active){participants.Add(session.MentorId);participants.Add(session.TraineeId);}
            bool adopted=false;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted)return ReferenceEquals(boundWorld,incoming)&&loaded&&!loadFailed;
                    if(!Primary())return false;
                    if(!loaded)
                    {
                        if(boundWorld!=null||boundPath!=null||ReferenceEquals(epoch,RebirthNpcAdvancedProgressionService.RecoveryEpoch)||
                            !RebirthNpcAdvancedProgressionService.NativeAdoptionEmpty())return false;
                        using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))EnsureLoaded();
                    }
                    if(!loaded||loadFailed||!ReferenceEquals(boundWorld,incoming)||boundPath!=path)return false;
                    // Real new actor positions replace historical presence.
                    // The unchanged saved session/credit original remains the
                    // authority for eligibility; no outcome credit is replayed.
                    RebirthNpcAdvancedProgressionService.CaptureRetirement(out var currentRecords,out var currentSessions,out _);
                    string loadedImage=SnapshotDocument(currentRecords,currentSessions).DocumentElement.OuterXml;
                    if(loadedImage!=persistent)return false;
                    foreach(var session in sessions)if(session.Active)
                    {
                        if(!native.TryGetValue(session.MentorId,out var mentor)||!native.TryGetValue(session.TraineeId,out var trainee)||
                            !mentor.Current()||!trainee.Current())return false;
                        float distance=Vector3.Distance(mentor.Actor.position,trainee.Actor.position);
                        RebirthNpcAdvancedProgressionService.UpdateMentorshipPresence(session.SessionId,true,true,distance,DateTime.UtcNow.Ticks);
                    }
                    adopted=true;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,participants,Current,Adopt);
        }
    }
}

public static partial class RebirthNpcAdvancedProgressionService
{
    internal static bool NativeAdoptionEmpty(){lock(Sync)return Records.Count==0&&Mentorships.Count==0;}
}
