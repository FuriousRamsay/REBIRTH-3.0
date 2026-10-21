using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;

public static partial class RebirthNpcAggregatePersistenceStore
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            if(!loaded||dirty||loadedNativeWorld==null||QuarantinedRecords.Count!=0||string.IsNullOrEmpty(loadedSaveDirectory))return null;
            var world=loadedNativeWorld;string path=Path.Combine(loadedSaveDirectory,FileName),fingerprint=loadedSaveScope;
            var epoch=recoveryMutationEpoch;long generation=storeEpoch;
            string records=RetirementImage(Records.Values),originalImage=QuarantineAggregateImage().OuterXml;
            XmlDocument primary;
            try
            {
                primary=RebirthNpcRecoveryCurrentBacking.Read(path);
                if(!ValidateRecoveryDocument(primary,fingerprint)||SavedImage(primary)!=records)return null;
            }
            catch{return null;}
            bool Original(){lock(Sync)return loaded&&!dirty&&QuarantinedRecords.Count==0&&ReferenceEquals(loadedNativeWorld,world)&&
                Path.Combine(loadedSaveDirectory,FileName)==path&&loadedSaveScope==fingerprint&&ReferenceEquals(recoveryMutationEpoch,epoch)&&
                storeEpoch==generation&&QuarantineAggregateImage().OuterXml==originalImage;}
            bool Empty(){lock(Sync)return !loaded&&!dirty&&loadedNativeWorld==null&&string.IsNullOrEmpty(loadedSaveDirectory)&&
                Records.Count==0&&QuarantinedRecords.Count==0&&ReadViews.Count==0&&orderedReadViews==null&&
                !ReferenceEquals(recoveryMutationEpoch,epoch)&&storeEpoch!=generation;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&QuarantinedRecords.Count==0&&ReferenceEquals(loadedNativeWorld,incoming)&&
                Path.Combine(loadedSaveDirectory,FileName)==path&&loadedSaveScope==fingerprint&&RetirementImage(Records.Values)==records;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("aggregate",scope,world,path,true,Original,Empty,Incoming,
                document=>ValidateRecoveryDocument(document,fingerprint)&&SavedImage(document)==records,()=>primary,EnsureLoaded);
        }
    }
    private static string SavedImage(XmlDocument document)
    {
        var records=new List<RebirthNpcPersistentRecord>();
        foreach(XmlElement row in document.DocumentElement.SelectNodes("record"))records.Add(ReadRecord(row));
        return RetirementImage(records);
    }
}
