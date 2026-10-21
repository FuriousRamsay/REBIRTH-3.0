using System;
using System.Xml;

public static partial class RebirthNpcEquipmentPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcEquipmentService.RecoveryEpoch;
            string image=QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()).DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()).DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&RebirthNpcEquipmentService.CapturePersistentRecords().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&CanRetireBacking&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()).DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("equipment",scope,world,path,loaded&&!dirty&&CanRetireBacking,Original,Empty,Incoming,
                Validate,()=>QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()),EnsureLoaded);
        }
    }
}

