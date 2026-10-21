using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcWorkCheckpointPersistenceStore
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var uninitialized=CaptureUninitializedNativeAdoption(scope);
            if(uninitialized!=null)return uninitialized;
            if(!loaded||!scope.MatchesBinding(boundWorld,boundPath,true))return null;
            var oldWorld=boundWorld;string path=boundPath;var rows=Loaded;
            long version=RebirthNpcWorkExecutionTelemetry.Revision;
            var captured=CaptureCheckpointRows();string expected=RecoveryRowsImage(captured);
            string loadedRows=RecoveryRowsImage(rows);bool wasReconciled=reconciled;
            bool Primary()
            {
                try
                {
                    if(!File.Exists(path)||File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
                    var doc=RebirthNpcRecoveryCurrentBacking.Read(path);
                    if(!Validate(doc))return false;
                    var saved=new List<RebirthNpcWorkRecoveryCheckpoint>();
                    foreach(XmlElement e in doc.DocumentElement.SelectNodes("checkpoint"))
                        saved.Add(new RebirthNpcWorkRecoveryCheckpoint{AssignmentId=ulong.Parse(e.GetAttribute("assignment"),CultureInfo.InvariantCulture),
                            ExecutorId=e.GetAttribute("executor"),StableTargetId=e.GetAttribute("target"),
                            Phase=(RebirthNpcWorkExecutorPhase)int.Parse(e.GetAttribute("phase"),CultureInfo.InvariantCulture),
                            Progress=float.Parse(e.GetAttribute("progress"),CultureInfo.InvariantCulture),RetryCount=int.Parse(e.GetAttribute("retries"),CultureInfo.InvariantCulture),
                            UpdatedUtcTicks=long.Parse(e.GetAttribute("updated"),CultureInfo.InvariantCulture)});
                    return RecoveryRowsImage(saved)==expected;
                }
                catch{return false;}
            }
            bool Current(){lock(Sync)return loaded&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==path&&ReferenceEquals(Loaded,rows)&&
                reconciled==wasReconciled&&RecoveryRowsImage(rows)==loadedRows&&RebirthNpcWorkExecutionTelemetry.Revision==version&&
                RecoveryRowsImage(CaptureCheckpointRows())==expected&&Primary();}
            if(!Current())return null;
            var participants=new HashSet<RebirthNpcStableId>();
            foreach(var checkpoint in captured)
                if(RebirthNpcWorkAssignmentService.TryCaptureRetainedAssignmentOwner(scope,checkpoint.AssignmentId,out var owner))participants.Add(owner);
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(!Primary())return false;
                    if(!loaded)
                    {
                        if(boundWorld!=null||boundPath!=null||Loaded.Count!=0||RebirthNpcWorkExecutionTelemetry.Snapshot().Length!=0)return false;
                        using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))EnsureLoaded();
                    }
                    // Loaded checkpoints remain recovery custody. Do not invoke
                    // ReconcileLoaded, transition an assignment, release native
                    // reservations, restore telemetry or resume an executor.
                    return loaded&&!reconciled&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                        RecoveryRowsImage(Loaded)==expected&&RebirthNpcWorkExecutionTelemetry.Snapshot().Length==0&&Primary();
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,participants,Current,Adopt);
        }
    }
}
