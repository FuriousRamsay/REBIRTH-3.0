using System;
using System.Xml;

public static partial class RebirthNpcSettlementPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcSettlementSimulation.RecoveryEpoch;
            string image=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcSettlementSimulation.RecoveryEpoch)&&QuarantineImage().DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcSettlementSimulation.RecoveryEpoch)&&RebirthNpcSettlementSimulation.SavedAdoptionEmpty();}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage().DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("settlement",scope,world,path,loaded&&!dirty&&CanRetireBacking,Original,Empty,Incoming,
                ValidateDocument,()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

