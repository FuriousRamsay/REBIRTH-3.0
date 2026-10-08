using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
sealed class FixtureIdentity { public RebirthNpcStableId StableNpcId; }
sealed class RebirthNpcPersistentRecord
{
    public FixtureIdentity Identity; public uint AggregateRevision; public string AggregateChecksum;
    internal RebirthHumanNpcAppearanceDescriptor? HumanAppearance;
}
static class RebirthNpcAggregateRecordCopy
{
    public static RebirthNpcPersistentRecord Copy(RebirthNpcPersistentRecord value){return new RebirthNpcPersistentRecord{Identity=new FixtureIdentity{StableNpcId=value.Identity.StableNpcId},AggregateRevision=value.AggregateRevision,AggregateChecksum=value.AggregateChecksum,HumanAppearance=value.HumanAppearance};}
}
sealed class FixtureScope { public string SaveDirectory; public string Fingerprint; }
static class RebirthNpcSaveScope
{
    public static FixtureScope Scope;
    public static FixtureScope ObserveCurrent(){return Scope;}
}
partial class AppearanceCommitStore
{
    internal static readonly object Sync=new object();
    internal static bool loaded=true,dirty,FailWrite,BackupPending;
    internal static string loadedSaveDirectory,loadedSaveScope,provenanceState;
    internal static int saves,Writes;
    internal static Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord> Records=new Dictionary<RebirthNpcStableId,RebirthNpcPersistentRecord>();
    static void EnsureLoaded(){}
    static void InvalidateAllReadViewsLocked(){}
    static void RecordWriteTelemetry(string directory){}
    static bool ValidatePublication(RebirthNpcPersistentRecord record,RebirthNpcStableId id,out string error){error="";return record.Identity.StableNpcId.Equals(id)&&record.HumanAppearance.HasValue&&record.HumanAppearance.Value.IsValid;}
    static string ComputeRecordChecksum(RebirthNpcPersistentRecord record){return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record.HumanAppearance.Value.Resolved.Encode())));}
    static bool WriteRecordsLocked(IList<RebirthNpcPersistentRecord> records,string directory,string scope)
    {
        Writes++;
        foreach(var row in Records.Values)if(row.HumanAppearance.HasValue&&row.HumanAppearance.Value.Resolved!=null)throw new Exception("authority memory changed before disk commit");
        if(FailWrite)throw new IOException("injected before primary publication");
        var xml=new XmlDocument();xml.AppendChild(xml.CreateElement("persons"));
        foreach(var row in records)xml.DocumentElement.AppendChild(RebirthNpcAppearanceBinaryCodec.WriteAggregate(xml,row.HumanAppearance.Value));
        string temp=Path.Combine(directory,"appearance.tmp"),final=Path.Combine(directory,"appearance.xml");
        xml.Save(temp);File.Move(temp,final,true);
        if(!File.Exists(final))throw new Exception("primary missing");
        foreach(var row in Records.Values)if(row.HumanAppearance.HasValue&&row.HumanAppearance.Value.Resolved!=null)throw new Exception("authority memory changed during writer");
        return BackupPending;
    }
}
static class CommitFixture
{
    public static int Run()
    {
        int n=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);n++;}
        string directory=Path.Combine(Path.GetTempPath(),"RebirthAppearanceFixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            AppearanceCommitStore.loadedSaveDirectory=directory;AppearanceCommitStore.loadedSaveScope="scope-a";
            RebirthNpcSaveScope.Scope=new FixtureScope{SaveDirectory=directory,Fingerprint="scope-a"};
            var id=new RebirthNpcStableId(11,22);
            var original=new RebirthHumanNpcAppearanceDescriptor(RebirthHumanNpcModelPipeline.SDCS,77,"BaseMale");
            var proposal=RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player");
            AppearanceCommitStore.Records.Clear();AppearanceCommitStore.Writes=0;
            Check(!AppearanceCommitStore.TryCommitResolvedAppearance(id,1,original,proposal,out _,out _),"no identity creation by resolution");
            AppearanceCommitStore.Records[id]=new RebirthNpcPersistentRecord{Identity=new FixtureIdentity{StableNpcId=id},AggregateRevision=4,HumanAppearance=original};
            AppearanceCommitStore.FailWrite=true;
            Check(!AppearanceCommitStore.TryCommitResolvedAppearance(id,4,original,proposal,out _,out var error)&&error.Contains("publication-failed"),"primary publication failure refused");
            Check(AppearanceCommitStore.Records[id].AggregateRevision==4&&AppearanceCommitStore.Records[id].HumanAppearance.Value.Resolved==null&&!File.Exists(Path.Combine(directory,"appearance.xml")),"failed commit retains old memory and disk witness");
            AppearanceCommitStore.FailWrite=false;
            Check(!AppearanceCommitStore.TryCommitResolvedAppearance(id,3,original,proposal,out _,out _),"stale source revision refused");
            RebirthNpcSaveScope.Scope.Fingerprint="scope-b";
            Check(!AppearanceCommitStore.TryCommitResolvedAppearance(id,4,original,proposal,out _,out _),"changed save scope refused");RebirthNpcSaveScope.Scope.Fingerprint="scope-a";
            AppearanceCommitStore.BackupPending=true;
            Check(AppearanceCommitStore.TryCommitResolvedAppearance(id,4,original,proposal,out var committed,out _)&&committed.Equals(proposal),"commit returns persisted proposal");
            Check(AppearanceCommitStore.Records[id].AggregateRevision==5&&AppearanceCommitStore.Records[id].HumanAppearance.Value.Equals(proposal)&&AppearanceCommitStore.dirty,"memory follows committed primary and retains backup retry");
            var disk=new XmlDocument();disk.Load(Path.Combine(directory,"appearance.xml"));
            Check(RebirthNpcAppearanceBinaryCodec.TryReadAggregate(disk.DocumentElement,out var saved)&&saved.HasValue&&saved.Value.Equals(proposal),"disk witness decodes exact committed descriptor");
            int writes=AppearanceCommitStore.Writes;
            UnityEngine.Resources.Asset=new UnityEngine.TextAsset("native-fixture-three");
            var alternative=RebirthNpcAppearanceAuthorityResolver.Resolve(original,true,"Player");
            Check(AppearanceCommitStore.TryCommitResolvedAppearance(id,4,original,alternative,out var retry,out _)&&retry.Equals(proposal)&&AppearanceCommitStore.Writes==writes,"retry returns owned descriptor without reroll or disk rewrite");
            Check(AppearanceCommitStore.Records[id].AggregateRevision==5,"retry preserves aggregate revision");
        }
        finally{Directory.Delete(directory,true);}
        return n;
    }
}