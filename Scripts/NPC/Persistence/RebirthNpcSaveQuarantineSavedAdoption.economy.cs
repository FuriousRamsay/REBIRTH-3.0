using System;
using System.Xml;

public static partial class RebirthNpcEconomyPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld as World;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcEconomyLogisticsService.RecoveryEpoch;
            string image=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&CanReset&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcEconomyLogisticsService.RecoveryEpoch)&&QuarantineImage().DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcEconomyLogisticsService.RecoveryEpoch)&&RebirthNpcEconomyLogisticsService.SavedAdoptionEmpty();}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&CanReset&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage().DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("economy",scope,world,path,loaded&&!dirty&&!flushing&&!loadFailed&&CanReset,Original,Empty,Incoming,
                Validate,()=>QuarantineImage(),Load);
        }
    }
}

