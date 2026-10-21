using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

public static partial class RebirthNpcExecutionPersistenceStore
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var uninitialized=CaptureUninitializedNativeAdoption(scope);
            if(uninitialized!=null)return uninitialized;
            var originalScope=loadedWorkScope;
            if(!loaded||originalScope==null||!scope.MatchesBinding(originalScope.NativeWorld as World,originalScope.SaveFilePath,true)||
                originalScope.ValidatedLoadSource!="primary")return null;
            string path=originalScope.SaveFilePath;
            var pending=PendingByStableId;var legacy=LegacyPendingByEntity;var custody=WorkCustody;
            string image=QuarantineExecutionImage().OuterXml;
            string primary;
            try
            {
                var doc=RebirthNpcRecoveryCurrentBacking.Read(path);if(!ValidateDocument(doc))return null;
                primary=doc.DocumentElement.OuterXml;
                var seenStable=new HashSet<RebirthNpcStableId>();var seenLegacy=new HashSet<int>();
                foreach(XmlElement element in doc.DocumentElement.SelectNodes("execution"))
                {
                    var saved=Read(element);RebirthNpcPersistedExecution old;
                    if(saved.StableId.IsEmpty)
                    {if(!seenLegacy.Add(saved.EntityId)||!legacy.TryGetValue(saved.EntityId,out old))return null;}
                    else if(!seenStable.Add(saved.StableId)||!pending.TryGetValue(saved.StableId,out old))return null;
                    // Preserve fields omitted by normal writing as well. A
                    // merely same-shaped active order cannot adopt this source.
                    var left=new XmlDocument();var right=new XmlDocument();
                    if(QuarantineExecutionRecord(left,saved).OuterXml!=QuarantineExecutionRecord(right,old).OuterXml)return null;
                }
                if(seenStable.Count!=pending.Count||seenLegacy.Count!=legacy.Count)return null;
            }
            catch{return null;}
            bool Primary()
            {
                try
                {
                    if(File.Exists(path+".tmp")||RebirthNpcPersistenceFile.IsRecoveryFenced(path))return false;
                    var doc=RebirthNpcRecoveryCurrentBacking.Read(path);
                    return ValidateDocument(doc)&&doc.DocumentElement.OuterXml==primary;
                }
                catch{return false;}
            }
            bool Current(){lock(Sync)return loaded&&ReferenceEquals(loadedWorkScope,originalScope)&&ReferenceEquals(PendingByStableId,pending)&&
                ReferenceEquals(LegacyPendingByEntity,legacy)&&ReferenceEquals(WorkCustody,custody)&&QuarantineExecutionImage().OuterXml==image&&Primary();}
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(!Primary()||ReferenceEquals(PendingByStableId,pending)||ReferenceEquals(LegacyPendingByEntity,legacy)||ReferenceEquals(WorkCustody,custody))return false;
                    if(!loaded)
                    {
                        if(loadedWorkScope!=null||PendingByStableId.Count!=0||LegacyPendingByEntity.Count!=0||WorkCustody.Count!=0)return false;
                        using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))EnsureLoaded();
                    }
                    // Normal loader creates a NEW current save scope. Work rows
                    // remain pending original custody; no TryRestore/setOrder,
                    // native lease activation or executor redispatch runs here.
                    return loaded&&loadedWorkScope!=null&&!ReferenceEquals(loadedWorkScope,originalScope)&&
                        IsCurrentWorkSaveScope(loadedWorkScope)&&ReferenceEquals(loadedWorkScope.NativeWorld,incoming)&&
                        loadedWorkScope.ValidatedLoadSource=="primary"&&Primary();
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}
