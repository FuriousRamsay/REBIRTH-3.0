using System;
using System.Xml;

public static partial class RebirthNpcInventoryPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcInventoryTransactionService.RecoveryEpoch;
            string image=QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()).DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()).DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()).DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("inventory",scope,world,path,loaded&&!dirty&&CanRetireBacking,Original,Empty,Incoming,
                ValidateDocument,()=>QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()),EnsureLoaded);
        }
    }
}

