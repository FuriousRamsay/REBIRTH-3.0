using System;

public static partial class RebirthNpcLifecycleGameplayService
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=lifecycleWorld;string path=lifecyclePath;var epoch=lifecycleEpoch;var binding=lifecycleBinding;
            string image=LifecycleDocument().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return persistenceLoaded&&!dirty&&!LifecycleOpaque&&CanRetireLifecycleBacking&&
                ReferenceEquals(lifecycleWorld,world)&&lifecyclePath==path&&ReferenceEquals(lifecycleEpoch,epoch)&&
                ReferenceEquals(lifecycleBinding,binding)&&binding?.MatchesRetirement(epoch)==true&&LifecycleDocument().DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !persistenceLoaded&&!dirty&&!LifecycleOpaque&&lifecycleWorld==null&&string.IsNullOrEmpty(lifecyclePath)&&
                !ReferenceEquals(lifecycleEpoch,epoch)&&Records.Count==0&&Replay.Count==0&&PendingReplay.Count==0;}
            bool Incoming(World incoming){lock(Sync)return persistenceLoaded&&!dirty&&!LifecycleOpaque&&CanRetireLifecycleBacking&&
                ReferenceEquals(lifecycleWorld,incoming)&&lifecyclePath==path&&LifecycleDocument().DocumentElement.OuterXml==image;}
            bool Valid(System.Xml.XmlDocument document){try{ReadLifecycleDocument(document);return true;}catch{return false;}}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("lifecycle-gameplay",scope,world,path,
                persistenceLoaded&&!dirty&&!LifecycleOpaque&&CanRetireLifecycleBacking,Original,Empty,Incoming,Valid,LifecycleDocument,Load,LifecycleCanonical);
        }
    }
}
