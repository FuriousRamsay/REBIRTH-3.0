using System;
using System.Collections.Generic;

public static partial class RebirthNpcDecisionEngine
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||decisionWorld!=null&&!ReferenceEquals(decisionWorld,scope.World))return null;
            var original=States;var world=decisionWorld;string image;
            try{image=DecisionImage().OuterXml;}catch{return null;}
            var copy=new Dictionary<RebirthNpcStableId,Runtime>();
            foreach(var pair in original)
            {
                if(pair.Value==null||pair.Value.Active!=null)return null;
                var row=new Runtime{GoalId=pair.Value.GoalId,LastScore=pair.Value.LastScore,
                    NextEvaluation=pair.Value.NextEvaluation,CooldownUntil=pair.Value.CooldownUntil};
                foreach(var goal in pair.Value.RecentGoals)row.RecentGoals.Enqueue(goal);
                copy.Add(pair.Key,row);
            }
            bool Current(){lock(Sync)return ReferenceEquals(States,original)&&ReferenceEquals(decisionWorld,world)&&DecisionImage().OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(decisionWorld,incoming)&&ReferenceEquals(States,copy);
                    if(ReferenceEquals(States,original)||decisionWorld!=null||States.Count!=0)return false;
                    foreach(var id in copy.Keys)if(!native.TryGetValue(id,out var source)||!source.Current()||
                        !ReferenceEquals(source.World,incoming))return false;
                    // No active task exists in this typed capture. Fresh native
                    // actors retain goal history/cooldowns; discovery and task
                    // selection use the normal incoming registry on next tick.
                    // Retained work/controller owners remain native-held by
                    // the separate per-owner release gate; carrying inactive
                    // history confers no executor or task permission.
                    States=copy;decisionWorld=incoming;npcSnapshot=Array.Empty<RebirthNpcStableId>();npcSnapshotRevision=-1;decisionCursor=0;
                    adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(copy.Keys),Current,Adopt);
        }
    }
}

public static partial class RebirthNpcDecisionSignalStore
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||signalWorld!=null&&!ReferenceEquals(signalWorld,scope.World))return null;
            var original=Signals;var world=signalWorld;string image;
            try{image=SignalsImage().OuterXml;}catch{return null;}
            var required=new HashSet<RebirthNpcStableId>(original.Keys);
            bool Current(){lock(Sync)return ReferenceEquals(Signals,original)&&ReferenceEquals(signalWorld,world)&&SignalsImage().OuterXml==image;}
            World adopted=null;Dictionary<RebirthNpcStableId,Dictionary<string,float>> incomingSignals=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(signalWorld,incoming)&&ReferenceEquals(Signals,incomingSignals);
                    if(ReferenceEquals(Signals,original)||signalWorld!=null||Signals.Count!=0)return false;
                    foreach(var id in required)if(!native.TryGetValue(id,out var source)||!source.Current()||!ReferenceEquals(source.World,incoming))return false;
                    // Environmental observations belong to the old World and
                    // remain in its retained dictionary. New actors start with
                    // fresh signals populated by real incoming observations.
                    incomingSignals=Signals;signalWorld=incoming;KnownSnapshot=Array.Empty<RebirthNpcStableId>();membershipRevision=0;snapshotRevision=-1;
                    adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,required,Current,Adopt);
        }
    }
}
