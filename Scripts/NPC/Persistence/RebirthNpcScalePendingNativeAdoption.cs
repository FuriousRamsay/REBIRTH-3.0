using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

public static partial class RebirthNpcNetworkBatcher
{
    private static string AppendRetainedBatchImage(XmlDocument doc,Dictionary<string,PendingBatch> records)
    {
        var root=doc.CreateElement("pending");doc.DocumentElement.AppendChild(root);var keys=new List<string>(records.Keys);keys.Sort(StringComparer.Ordinal);
        if(keys.Count>MaxPendingBatches)throw new InvalidOperationException("Original network batch bound exceeded");long text=0;
        foreach(string key in keys)
        {
            var batch=records[key];if(batch==null||batch.Payloads.Count>MaxMessagesPerBatch||batch.TotalBytes>MaxBytesPerBatch||key.Length>65536||
                (text+=key.Length+(long)(batch.Channel?.Length??0))>4L*1024*1024)throw new InvalidOperationException("Original network batch unavailable");
            var row=doc.CreateElement("batch");root.AppendChild(row);row.SetAttribute("key",key);row.SetAttribute("recipient",batch.RecipientId.ToString(CultureInfo.InvariantCulture));
            row.SetAttribute("channel",batch.Channel??string.Empty);row.SetAttribute("bytes",batch.TotalBytes.ToString(CultureInfo.InvariantCulture));int total=0;
            foreach(var payload in batch.Payloads){if(payload==null||payload.Length<1||payload.Length>MaxBytesPerBatch)throw new InvalidOperationException("Original network payload unavailable");
                total=checked(total+payload.Length);var item=doc.CreateElement("payload");item.InnerText=Convert.ToBase64String(payload);row.AppendChild(item);}
            if(total!=batch.TotalBytes)throw new InvalidOperationException("Original network byte ledger differs");
        }
        return root.OuterXml;
    }
    private static string RetainedBatchImage(Dictionary<string,PendingBatch> records)
    {var document=new XmlDocument();document.AppendChild(document.CreateElement("network"));return AppendRetainedBatchImage(document,records);}
    private sealed class NativeBatchCustody
    {
        internal World World,Incoming;internal Dictionary<string,PendingBatch> Records;internal string Image;
        internal readonly Dictionary<int,Entity> Recipients=new Dictionary<int,Entity>();internal readonly Dictionary<int,ClientInfo> Peers=new Dictionary<int,ClientInfo>();
        internal RebirthNpcNativeTransientAdoption Receipt;
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromNative(source,out var scope)&&StageScope(source.World,scope);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromOwnerScope(source,out var scope)&&StageScope(source.World,scope);
        private bool StageScope(World incoming,RebirthNpcAncillaryOriginalScope scope)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(batchScope?.World,Incoming);
                if(Pending.Count!=0||batchScope!=null||ReferenceEquals(Pending,Records)||scope==null)return false;
                batchScope=scope;Incoming=incoming;nativeBatchCustodies.Add(this);return true;
            }
        }
    }
    private static readonly List<NativeBatchCustody> nativeBatchCustodies=new List<NativeBatchCustody>();
    internal static RebirthNpcNativeTransientAdoption CaptureNativePendingAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,batchScope);}catch{return null;}
            var custody=new NativeBatchCustody{World=scope.World,Records=Pending,Image=RetainedBatchImage(Pending)};
            foreach(var row in Pending.Values)
            {custody.Recipients[row.RecipientId]=scope.World.GetEntity(row.RecipientId);custody.Peers[row.RecipientId]=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(row.RecipientId);}
            var original=Pending;var binding=batchScope;long a=messagesQueued,b=messagesRejected,c=batchesDrained,d=bytesQueued;
            bool Current(){lock(Sync)return ReferenceEquals(Pending,original)&&ReferenceEquals(batchScope,binding)&&messagesQueued==a&&messagesRejected==b&&batchesDrained==c&&bytesQueued==d&&RetainedBatchImage(original)==custody.Image;}
            bool Available(){lock(Sync)return custody.Incoming==null?Pending.Count==0&&batchScope==null&&!ReferenceEquals(Pending,original):ReferenceEquals(batchScope?.World,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("network-batches-empty",scope,Current,Available,()=>original.Count!=0,source=>{},custody.Stage,custody.Image.Length,
                ()=>nativeBatchCustodies.Remove(custody),ownerStage:source=>custody.Records.Count==0&&custody.StageOwnerScope(source));custody.Receipt=receipt;return receipt;
        }
    }
}

public static partial class RebirthNpcRelationshipBatcher
{
    private static string AppendRetainedRelationshipImage(XmlDocument doc,Dictionary<string,List<RebirthNpcRelationshipMutation>> records)
    {
        var root=doc.CreateElement("pending");doc.DocumentElement.AppendChild(root);var keys=new List<string>(records.Keys);keys.Sort(StringComparer.Ordinal);
        if(keys.Count>MaximumSources)throw new InvalidOperationException("Original relationship bound exceeded");long text=0;
        foreach(string key in keys)
        {
            var list=records[key];if(list==null||list.Count>MaximumItemsPerSource)throw new InvalidOperationException("Original relationship list unavailable");
            var row=doc.CreateElement("source");root.AppendChild(row);row.SetAttribute("key",key);
            foreach(var value in list)
            {
                if(value==null||value.SourceNpcId!=key||(text+=(value.SourceNpcId?.Length??0)+(long)(value.TargetNpcId?.Length??0)+(value.Reason?.Length??0))>4L*1024*1024)
                    throw new InvalidOperationException("Original relationship mutation unavailable");
                var item=doc.CreateElement("mutation");row.AppendChild(item);item.SetAttribute("source",value.SourceNpcId??string.Empty);item.SetAttribute("target",value.TargetNpcId??string.Empty);
                item.SetAttribute("delta",value.Delta.ToString(CultureInfo.InvariantCulture));item.SetAttribute("reasonNull",value.Reason==null?"1":"0");item.SetAttribute("reason",value.Reason??string.Empty);
            }
        }
        return root.OuterXml;
    }
    private static string RetainedRelationshipImage(Dictionary<string,List<RebirthNpcRelationshipMutation>> records)
    {var document=new XmlDocument();document.AppendChild(document.CreateElement("relationships"));return AppendRetainedRelationshipImage(document,records);}
    private sealed class NativeRelationshipCustody
    {
        internal World World,Incoming;internal Dictionary<string,List<RebirthNpcRelationshipMutation>> Records;internal RebirthNpcNativeTransientAdoption Receipt;
        internal readonly HashSet<string> Owners=new HashSet<string>(StringComparer.Ordinal);
        internal bool Stage(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromNative(source,out var scope)&&StageScope(source.World,scope);
        internal bool StageOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)=>RebirthNpcAncillaryOriginalScope.TryFromOwnerScope(source,out var scope)&&StageScope(source.World,scope);
        private bool StageScope(World incoming,RebirthNpcAncillaryOriginalScope scope)
        {
            lock(Sync)
            {
                if(Incoming!=null)return ReferenceEquals(Incoming,incoming)&&ReferenceEquals(relationshipScope?.World,Incoming);
                if(Pending.Count!=0||relationshipScope!=null||ReferenceEquals(Pending,Records)||scope==null)return false;
                relationshipScope=scope;Incoming=incoming;nativeRelationshipCustodies.Add(this);return true;
            }
        }
    }
    private static readonly List<NativeRelationshipCustody> nativeRelationshipCustodies=new List<NativeRelationshipCustody>();
    private static bool RetainedRelationshipOwner(string source,string target)
    {
        foreach(var custody in nativeRelationshipCustodies)if(!ReferenceEquals(custody.World,GameManager.Instance?.World))
            if(custody.Owners.Contains(source)||custody.Owners.Contains(target))return true;
        return false;
    }
    internal static RebirthNpcNativeTransientAdoption CaptureNativePendingAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,relationshipScope);}catch{return null;}
            var custody=new NativeRelationshipCustody{World=scope.World,Records=Pending};var original=Pending;var binding=relationshipScope;
            foreach(var list in original.Values)foreach(var mutation in list){custody.Owners.Add(mutation.SourceNpcId);custody.Owners.Add(mutation.TargetNpcId);}
            string image=RetainedRelationshipImage(original);long a=batches,b=items,c=rejected;
            bool Current(){lock(Sync)return ReferenceEquals(Pending,original)&&ReferenceEquals(relationshipScope,binding)&&batches==a&&items==b&&rejected==c&&RetainedRelationshipImage(original)==image;}
            bool Available(){lock(Sync)return custody.Incoming==null?Pending.Count==0&&relationshipScope==null&&!ReferenceEquals(Pending,original):ReferenceEquals(relationshipScope?.World,custody.Incoming);}
            var receipt=new RebirthNpcNativeTransientAdoption("relationship-batches-empty",scope,Current,Available,()=>original.Count!=0,source=>{},custody.Stage,image.Length,
                ()=>nativeRelationshipCustodies.Remove(custody),owner=>custody.Owners.Contains(owner.ToString()),source=>custody.Records.Count==0&&custody.StageOwnerScope(source));custody.Receipt=receipt;return receipt;
        }
    }
}
