using System;
using System.IO;
using System.Xml;
using System.Globalization;

public static partial class RebirthNpcLifecycleGameplayService
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||dirty||LifecycleOpaque||Records.Count!=0||Replay.Count!=0||PendingReplay.Count!=0)return null;
            bool wasLoaded=persistenceLoaded;var world=lifecycleWorld;string oldPath=lifecyclePath;var epoch=lifecycleEpoch;var binding=lifecycleBinding;
            string path=Path.Combine(scope.Directory,"RebirthNpcLifecycleGameplay.xml"),image=LifecycleDocument().OuterXml;
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world,oldPath,true)))return null;
            if(!wasLoaded&&(world!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            bool Original(){lock(Sync)return persistenceLoaded==wasLoaded&&!dirty&&!LifecycleOpaque&&ReferenceEquals(lifecycleWorld,world)&&lifecyclePath==oldPath&&
                ReferenceEquals(lifecycleEpoch,epoch)&&ReferenceEquals(lifecycleBinding,binding)&&Records.Count==0&&Replay.Count==0&&PendingReplay.Count==0&&LifecycleDocument().OuterXml==image;}
            bool Detached(){lock(Sync)return !persistenceLoaded&&!dirty&&!LifecycleOpaque&&lifecycleWorld==null&&string.IsNullOrEmpty(lifecyclePath)&&
                !ReferenceEquals(lifecycleEpoch,epoch)&&Records.Count==0&&Replay.Count==0&&PendingReplay.Count==0;}
            bool Incoming(World incoming){lock(Sync)return persistenceLoaded&&!dirty&&!LifecycleOpaque&&ReferenceEquals(lifecycleWorld,incoming)&&lifecyclePath==path;}
            bool Valid(XmlDocument doc){ReadLifecycleDocument(doc);return true;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("lifecycle-gameplay",scope,path,Original,Detached,Incoming,Valid,LifecycleDocument,Load,LifecycleCanonical);
        }
    }
}

public static partial class RebirthNpcIdentityAuditLedger
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||auditDirty||Entries.Count!=0)return null;
            bool wasLoaded=loaded;var world=auditWorld;string oldPath=auditPath;var epoch=auditEpoch;var binding=auditBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world,oldPath,true)))return null;
            if(!wasLoaded&&(world!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!auditDirty&&ReferenceEquals(auditWorld,world)&&auditPath==oldPath&&ReferenceEquals(auditEpoch,epoch)&&ReferenceEquals(auditBinding,binding)&&Entries.Count==0;}
            bool Detached(){lock(Sync)return !loaded&&!auditDirty&&auditWorld==null&&string.IsNullOrEmpty(auditPath)&&!ReferenceEquals(auditEpoch,epoch)&&Entries.Count==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!auditDirty&&ReferenceEquals(auditWorld,incoming)&&auditPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("identity-audit",scope,path,Original,Detached,Incoming,ValidateDocument,()=>AuditDocument(Entries),LoadAuditBound);
        }
    }
}

public static partial class RebirthNpcAggregatePersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||dirty||Records.Count!=0||QuarantinedRecords.Count!=0||ReadViews.Count!=0||orderedReadViews!=null)return null;
            bool wasLoaded=loaded;var world=loadedNativeWorld;string save=loadedSaveDirectory;var epoch=recoveryMutationEpoch;long generation=storeEpoch;
            string path=Path.Combine(scope.Directory,FileName),image=QuarantineAggregateImage().OuterXml;
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(world,string.IsNullOrEmpty(save)?null:Path.Combine(save,FileName),true)))return null;
            if(!wasLoaded&&(world!=null||!string.IsNullOrEmpty(save)||!string.IsNullOrEmpty(loadedSaveScope)))return null;
            XmlDocument template=null;string persistedRecords=null;
            bool Valid(XmlDocument document)
            {
                if(!ValidateRecoveryDocument(document,scope.Fingerprint))return false;
                if(template==null){template=document;persistedRecords=SavedImage(document);}return true;
            }
            XmlDocument Snapshot()
            {
                if(template!=null&&RetirementImage(Records.Values)==persistedRecords)return template;
                var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("rebirthNpcPersistentRecords");doc.AppendChild(root);
                root.SetAttribute("format",CurrentFormat.ToString(CultureInfo.InvariantCulture));root.SetAttribute("writtenUtcTicks","0");root.SetAttribute("saveScope",scope.Fingerprint);
                foreach(var record in Records.Values)root.AppendChild(WriteRecord(doc,record));return doc;
            }
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&ReferenceEquals(loadedNativeWorld,world)&&loadedSaveDirectory==save&&
                ReferenceEquals(recoveryMutationEpoch,epoch)&&storeEpoch==generation&&QuarantineAggregateImage().OuterXml==image&&Records.Count==0&&QuarantinedRecords.Count==0&&ReadViews.Count==0&&orderedReadViews==null;}
            bool Detached(){lock(Sync)return !loaded&&!dirty&&loadedNativeWorld==null&&string.IsNullOrEmpty(loadedSaveDirectory)&&
                !ReferenceEquals(recoveryMutationEpoch,epoch)&&storeEpoch!=generation&&Records.Count==0&&QuarantinedRecords.Count==0&&ReadViews.Count==0&&orderedReadViews==null;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&ReferenceEquals(loadedNativeWorld,incoming)&&
                RebirthNpcSaveQuarantineScope.SameDirectory(loadedSaveDirectory,scope.Directory)&&loadedSaveScope==scope.Fingerprint&&QuarantinedRecords.Count==0;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("aggregate",scope,path,Original,Detached,Incoming,Valid,Snapshot,EnsureLoaded);
        }
    }
}
