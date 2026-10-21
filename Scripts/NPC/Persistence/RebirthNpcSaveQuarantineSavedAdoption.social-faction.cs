using System;
using System.Xml;

public static partial class RebirthNpcSocialPersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=boundWorld as World;string path=boundPath;var binding=originalBinding;
            var epoch=RebirthNpcSocialService.RecoveryEpoch;
            string image=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!flushing&&CanReset&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&
                binding?.MatchesRetirement(epoch)==true&&ReferenceEquals(epoch,RebirthNpcSocialService.RecoveryEpoch)&&QuarantineImage().DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!ReferenceEquals(epoch,RebirthNpcSocialService.RecoveryEpoch)&&QuarantineImage().SelectNodes("/*/*/*").Count==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!flushing&&CanReset&&ReferenceEquals(boundWorld,incoming)&&boundPath==path&&
                QuarantineImage().DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("social-faction",scope,world,path,loaded&&!flushing&&CanReset,Original,Empty,Incoming,
                Validate,()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

