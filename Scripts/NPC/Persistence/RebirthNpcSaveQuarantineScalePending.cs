using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

// Observational scope only. Never payment, delivery or native effect authority.
internal sealed class RebirthNpcAncillaryOriginalScope
{
    internal readonly World World;internal readonly ulong Generation;
    internal readonly string Directory,Fingerprint;internal readonly int ProcessGeneration;
    private RebirthNpcAncillaryOriginalScope(World world,ulong generation,RebirthNpcSaveScopeSnapshot save){World=world;Generation=generation;Directory=save.SaveDirectory;Fingerprint=save.Fingerprint;ProcessGeneration=save.ProcessSaveGeneration;}
    internal bool Current()
    {if(!RebirthNpcControllerOriginalScope.CanTick(World)||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(World,out ulong generation)||generation!=Generation)return false;var save=RebirthNpcSaveScope.ObserveCurrent();return save!=null&&save.SaveDirectory==Directory&&save.Fingerprint==Fingerprint&&save.ProcessSaveGeneration==ProcessGeneration;}
    internal static bool Use(ref RebirthNpcAncillaryOriginalScope original,bool empty,bool observedPolicy=false)
    {
        var world=GameManager.Instance?.World;if(!RebirthNpcControllerOriginalScope.CanTick(world))return false;
        if(original!=null&&!ReferenceEquals(original.World,world))return false;
        if(original==null&&!empty)return false;
        // Only non-authoritative view/cache policy may reuse a positive exact
        // observed generation. Miss never seeds G0 or authorizes a new scope.
        if(original!=null&&observedPolicy)return original.Current();
        var save=RebirthNpcSaveScope.ObserveCurrent();if(save==null||string.IsNullOrEmpty(save.SaveDirectory)||string.IsNullOrEmpty(save.Fingerprint))return false;
        if(original!=null&&(save.SaveDirectory!=original.Directory||save.Fingerprint!=original.Fingerprint||save.ProcessSaveGeneration!=original.ProcessGeneration))return false;
        ulong generation=RebirthNpcRecoveryGeneration.RequireCurrent();
        if(original!=null&&original.Generation!=generation&&!empty)return false;
        if(original==null||original.Generation!=generation)original=new RebirthNpcAncillaryOriginalScope(world,generation,save);
        return true;
    }
    internal static XmlDocument Image(string name,RebirthNpcAncillaryOriginalScope scope,long[] counters)
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement(name);doc.AppendChild(root);root.SetAttribute("generation",(scope?.Generation??0).ToString(CultureInfo.InvariantCulture));
        for(int i=0;i<counters.Length;i++)root.SetAttribute("counter"+i,counters[i].ToString(CultureInfo.InvariantCulture));return doc;
    }
    internal static void RequireScope(RebirthNpcSaveQuarantineScope scope,RebirthNpcAncillaryOriginalScope original)
    {if(scope==null||!ThreadManager.IsMainThread()||(original!=null&&(!ReferenceEquals(scope.World,original.World)||scope.Directory!=original.Directory||scope.Fingerprint!=original.Fingerprint||scope.Generation!=original.Generation)))throw new InvalidOperationException("Original ancillary scope unavailable");}
    internal static bool TryFromNative(RebirthNpcRuntimeNativeAdoption.NativeReentry source,out RebirthNpcAncillaryOriginalScope scope)
    {
        scope=null;if(source?.Current()!=true||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(source.World,out ulong generation)||generation==0)return false;
        var save=RebirthNpcSaveScope.ObserveCurrent();if(save==null||!RebirthNpcSaveQuarantineScope.SameDirectory(save.SaveDirectory,source.Directory)||string.IsNullOrEmpty(save.Fingerprint))return false;
        scope=new RebirthNpcAncillaryOriginalScope(source.World,generation,save);return true;
    }
    internal static bool TryFromOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source,out RebirthNpcAncillaryOriginalScope scope)
    {
        scope=null;if(source?.Current()!=true||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(source.World,out ulong generation)||generation==0)return false;
        var save=RebirthNpcSaveScope.ObserveCurrent();if(save==null||!RebirthNpcSaveQuarantineScope.SameDirectory(save.SaveDirectory,source.Directory)||string.IsNullOrEmpty(save.Fingerprint))return false;
        scope=new RebirthNpcAncillaryOriginalScope(source.World,generation,save);return source.Current();
    }
}
public static partial class RebirthNpcNetworkBatcher
{
    private static RebirthNpcAncillaryOriginalScope batchScope;
    private static bool UseOriginalBatchScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref batchScope,Pending.Count==0);}
    internal static bool CanRetireEmptyBatches{get{lock(Sync)return ThreadManager.IsMainThread()&&Pending.Count==0;}}
    private static XmlDocument BatchImage()=>RebirthNpcAncillaryOriginalScope.Image("networkBatches",batchScope,new[]{messagesQueued,messagesRejected,batchesDrained,bytesQueued});
    internal static RebirthNpcSaveQuarantineDomain PrepareBatchQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,batchScope);
            var records=Pending;var original=batchScope;long a=messagesQueued,b=messagesRejected,c=batchesDrained,d=bytesQueued;var doc=BatchImage();doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<string,PendingBatch>(StringComparer.Ordinal);
            string image=AppendRetainedBatchImage(doc,records);doc.DocumentElement.SetAttribute("emptyOnly",records.Count==0?"1":"0");
            return new RebirthNpcSaveQuarantineDomain("network-batches-empty",doc,
                ()=>{lock(Sync)return ReferenceEquals(Pending,records)&&ReferenceEquals(batchScope,original)&&messagesQueued==a&&messagesRejected==b&&batchesDrained==c&&bytesQueued==d&&RetainedBatchImage(records)==image;},
                ()=>{lock(Sync){Pending=next;batchScope=null;messagesQueued=messagesRejected=batchesDrained=bytesQueued=0;}},
                ()=>{lock(Sync){Pending=records;batchScope=original;messagesQueued=a;messagesRejected=b;batchesDrained=c;bytesQueued=d;}});
        }
    }
}
public static partial class RebirthNpcRelationshipBatcher
{
    private static RebirthNpcAncillaryOriginalScope relationshipScope;
    private static bool UseOriginalRelationshipScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref relationshipScope,Pending.Count==0);}
    internal static bool CanRetireEmptyRelationships{get{lock(Sync)return ThreadManager.IsMainThread()&&Pending.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareRelationshipBatchQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,relationshipScope);
            var records=Pending;var original=relationshipScope;long a=batches,b=items,c=rejected;var doc=RebirthNpcAncillaryOriginalScope.Image("relationshipBatches",original,new[]{a,b,c});doc.DocumentElement.SetAttribute("emptyOnly","1");var next=new Dictionary<string,List<RebirthNpcRelationshipMutation>>(StringComparer.Ordinal);
            string image=AppendRetainedRelationshipImage(doc,records);doc.DocumentElement.SetAttribute("emptyOnly",records.Count==0?"1":"0");
            return new RebirthNpcSaveQuarantineDomain("relationship-batches-empty",doc,
                ()=>{lock(Sync)return ReferenceEquals(Pending,records)&&ReferenceEquals(relationshipScope,original)&&batches==a&&items==b&&rejected==c&&RetainedRelationshipImage(records)==image;},
                ()=>{lock(Sync){Pending=next;relationshipScope=null;batches=items=rejected=0;}},
                ()=>{lock(Sync){Pending=records;relationshipScope=original;batches=a;items=b;rejected=c;}});
        }
    }
}
public static partial class RebirthNpcSerializationBufferPool
{
    private static RebirthNpcAncillaryOriginalScope poolScope;
    private static bool UseOriginalPoolScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref poolScope,Buffers.Count==0&&Leased.Count==0,true);}
    internal static bool CanRetireIdlePool{get{lock(Sync)return ThreadManager.IsMainThread()&&Leased.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PreparePoolQuarantine(RebirthNpcSaveQuarantineScope scope,RebirthNpcRecoveryPrimitiveCaches.Context transition=null)
    {
        lock(Sync)
        {
            if(transition==null)RebirthNpcAncillaryOriginalScope.RequireScope(scope,poolScope);else transition.RequireOriginal(scope,poolScope);if(Buffers.Count>MaximumRetained||(poolScope==null&&(Buffers.Count!=0||Leased.Count!=0)))throw new InvalidOperationException("Original serialization scope unavailable");
            var buffers=Buffers;var leased=Leased;var original=poolScope;long a=rentals,b=returns,c=allocations,d=rejectedReturns;var rows=Buffers.ToArray();var images=new byte[rows.Length][];
            var doc=RebirthNpcAncillaryOriginalScope.Image("serializationPool",original,new[]{a,b,c,d});for(int i=0;i<rows.Length;i++){if(rows[i]==null||rows[i].Length>MaximumSize)throw new InvalidOperationException("Original pool row invalid");images[i]=(byte[])rows[i].Clone();var row=doc.CreateElement("buffer");row.InnerText=Convert.ToBase64String(images[i]);doc.DocumentElement.AppendChild(row);}
            var next=new Stack<byte[]>();var nextLeases=new HashSet<byte[]>();
            var leaseRows=new HashSet<byte[]>(leased);long leaseBytes=0;
            if(leaseRows.Count>8192)throw new InvalidOperationException("Original serialization lease count bound exceeded");
            foreach(var row in leaseRows){if(row==null||(leaseBytes=checked(leaseBytes+row.LongLength))>64L*1024*1024)throw new InvalidOperationException("Original serialization lease byte bound exceeded");var element=doc.CreateElement("leased");element.SetAttribute("length",row.Length.ToString(CultureInfo.InvariantCulture));doc.DocumentElement.AppendChild(element);}
            return new RebirthNpcSaveQuarantineDomain("serialization-pool-idle",doc,
                ()=>{lock(Sync){if(!ReferenceEquals(Buffers,buffers)||!ReferenceEquals(Leased,leased)||!Leased.SetEquals(leaseRows)||!ReferenceEquals(poolScope,original)||rentals!=a||returns!=b||allocations!=c||rejectedReturns!=d||Buffers.Count!=rows.Length)return false;var current=Buffers.ToArray();for(int i=0;i<rows.Length;i++){if(!ReferenceEquals(current[i],rows[i])||current[i].Length!=images[i].Length)return false;for(int n=0;n<current[i].Length;n++)if(current[i][n]!=images[i][n])return false;}return true;}},
                ()=>{lock(Sync){Buffers=next;Leased=nextLeases;poolScope=null;rentals=returns=allocations=rejectedReturns=0;}},
                ()=>{lock(Sync){Buffers=buffers;Leased=leased;poolScope=original;rentals=a;returns=b;allocations=c;rejectedReturns=d;}});
        }
    }
}
