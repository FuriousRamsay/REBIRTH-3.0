using System;
using System.Xml;

public static partial class RebirthNpcProgressionPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld as World;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcProgressionService.RecoveryEpoch;
            string image=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&CanReset&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch)&&QuarantineImage().DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch)&&RebirthNpcProgressionService.ExportRecords().Length==0&&RebirthNpcProgressionService.ExportReplayIds().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&CanReset&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage().DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("progression",scope,world,path,loaded&&!dirty&&!flushing&&!loadFailed&&CanReset,Original,Empty,Incoming,
                doc=>{ReadDocument(doc);return true;},()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

