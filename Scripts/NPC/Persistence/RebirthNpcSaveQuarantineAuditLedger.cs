using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

public static partial class RebirthNpcIdentityAuditLedger
{
    private static World auditWorld;
    private static string auditPath;
    private static bool auditDirty;
    private static object auditEpoch=new object();
    private static RebirthNpcOriginalSaveBinding auditBinding;
    private static bool AuditBoundCurrent=>auditWorld==null||(ReferenceEquals(auditWorld,GameManager.Instance?.World)&&auditPath==GetPath());
    private static void RequireAuditCurrent()
    {if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||!IsServer()||!AuditBoundCurrent||RebirthNpcPersistenceFile.HasQuarantineWriterFence)throw new InvalidOperationException("NPC audit belongs to an unavailable original domain");}
    private static void BindAuditNoLock()
    {auditWorld=GameManager.Instance.World;auditPath=GetPath();auditBinding=RebirthNpcOriginalSaveBinding.Capture(auditWorld,auditPath,auditEpoch);}
    private static void LoadAuditBound()
    {
        RequireAuditCurrent();lock(Sync)
        {
            if(loaded)return;if(Entries.Count!=0)throw new InvalidOperationException("Existing audit entries cannot be overwritten during load");string path=GetPath();
            if(!RebirthNpcPersistenceFile.TryLoad(path,ValidateDocument,out var document,out var source,out var error))
            {if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))throw new InvalidDataException("Original audit load retained: "+error);BindAuditNoLock();loaded=true;return;}
            var entries=ReadAuditDocument(document);long next=1;foreach(var entry in entries)next=Math.Max(next,entry.Sequence+1);Entries=entries;nextSequence=next;auditEpoch=new object();auditDirty=false;BindAuditNoLock();loaded=true;
            RebirthNpcPersistenceSchemaTelemetry.RecordLoad("identity-audit",path,FormatVersion,FormatVersion,source);
        }
    }
    private static List<RebirthNpcIdentityAuditEntry> ReadAuditDocument(XmlDocument document)
    {
        var root=document?.DocumentElement;if(root==null||root.Name!="rebirthNpcIdentityAudit"||root.GetAttribute("format")!=FormatVersion.ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("NPC audit format invalid");var entries=new List<RebirthNpcIdentityAuditEntry>();var ids=new HashSet<long>();
        foreach(XmlNode node in root.ChildNodes)
        {
            if(node is not XmlElement row){if(node is XmlWhitespace||node is XmlComment)continue;throw new InvalidDataException("NPC audit content invalid");}
            if(row.Name!="entry"||row.ChildNodes.Count!=0||entries.Count>=MaximumEntries||!long.TryParse(row.GetAttribute("sequence"),NumberStyles.Integer,CultureInfo.InvariantCulture,out long sequence)||sequence<1||sequence==long.MaxValue||!ids.Add(sequence)||!DateTime.TryParse(row.GetAttribute("utc"),CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out var utc)||!bool.TryParse(row.GetAttribute("succeeded"),out bool succeeded))throw new InvalidDataException("NPC audit entry invalid");
            int entity=0;if(row.HasAttribute("entity")&&!int.TryParse(row.GetAttribute("entity"),NumberStyles.Integer,CultureInfo.InvariantCulture,out entity))throw new InvalidDataException("NPC audit entity invalid");
            foreach(XmlAttribute attribute in row.Attributes)if(attribute.Name!="sequence"&&attribute.Name!="utc"&&attribute.Name!="actor"&&attribute.Name!="operation"&&attribute.Name!="entity"&&attribute.Name!="stableId"&&attribute.Name!="succeeded"&&attribute.Name!="message")throw new InvalidDataException("Unknown audit field retained");
            entries.Add(new RebirthNpcIdentityAuditEntry{Sequence=sequence,TimestampUtc=utc.ToUniversalTime(),Actor=row.GetAttribute("actor"),Operation=row.GetAttribute("operation"),EntityId=entity,StableId=row.GetAttribute("stableId"),Succeeded=succeeded,Message=row.GetAttribute("message")});
        }
        entries.Sort((a,b)=>a.Sequence.CompareTo(b.Sequence));return entries;
    }
    private static XmlDocument AuditDocument(IEnumerable<RebirthNpcIdentityAuditEntry> entries)
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("rebirthNpcIdentityAudit");doc.AppendChild(root);root.SetAttribute("format",FormatVersion.ToString(CultureInfo.InvariantCulture));foreach(var entry in entries)
        {if(entry==null)throw new InvalidOperationException("NPC audit original entry missing");var row=doc.CreateElement("entry");root.AppendChild(row);Set(row,"sequence",entry.Sequence);row.SetAttribute("utc",entry.TimestampUtc.ToString("o",CultureInfo.InvariantCulture));row.SetAttribute("actor",entry.Actor??string.Empty);row.SetAttribute("operation",entry.Operation??string.Empty);if(entry.EntityId!=0)Set(row,"entity",entry.EntityId);if(!string.IsNullOrEmpty(entry.StableId))row.SetAttribute("stableId",entry.StableId);row.SetAttribute("succeeded",entry.Succeeded?"true":"false");if(!string.IsNullOrEmpty(entry.Message))row.SetAttribute("message",entry.Message);}return doc;
    }
    private static void SaveAuditBound()
    {
        RequireAuditCurrent();if(!loaded||auditWorld==null)throw new InvalidOperationException("Audit backing unresolved");
        try
        {
            using(var writer=RebirthNpcPersistenceFile.BeginWrite(auditPath))
            {var epoch=auditEpoch;var doc=AuditDocument(Entries);ReadAuditDocument(doc);RebirthNpcPersistenceSchemaTelemetry.PrepareUpgradeSnapshot("identity-audit",auditPath,FormatVersion);RebirthNpcPersistenceFile.RequireCurrentWriter(auditPath);if(!ReferenceEquals(epoch,auditEpoch))throw new InvalidOperationException("Audit snapshot changed");RebirthNpcPersistenceFile.SaveAtomic(auditPath,doc);writes++;auditDirty=false;auditBinding=RebirthNpcOriginalSaveBinding.Capture(auditWorld,auditPath,epoch);}
        }
        catch(Exception ex){Log.Warning("[REBIRTH NPC] Audit publication retained: "+ex.GetType().Name);if(RebirthNpcPersistenceCoordinator.IsCheckpointWrite)throw;}
    }
    internal static bool CanRetireAuditBacking
    {
        get{lock(Sync)try
        {
            if(auditDirty)return false;if(auditWorld==null)return !loaded&&Entries.Count==0;if(!loaded||auditBinding?.MatchesRetirement(auditEpoch)!=true)return false;
            var current=AuditDocument(Entries);ReadAuditDocument(current);if(!File.Exists(auditPath))return Entries.Count==0&&RebirthNpcPersistenceFile.CanInitializeEmpty(auditPath);
            var saved=ReadAuditDocument(RebirthNpcRecoveryCurrentBacking.Read(auditPath));return AuditDocument(saved).OuterXml==current.OuterXml;
        }catch{return false;}}
    }
    internal static bool TryFlushAuditBacking()
    {
        lock(Sync)try
        {
            if(auditWorld==null)return CanRetireAuditBacking;if(AuditBoundCurrent){SaveAuditBound();return CanRetireAuditBacking;}if(!auditDirty)return CanRetireAuditBacking;
            if(!loaded||auditBinding==null||!auditBinding.TryPublish(()=>AuditDocument(Entries),ValidateDocument,()=>auditEpoch,out _))return false;auditDirty=false;return CanRetireAuditBacking;
        }catch{return false;}
    }
    internal static bool TryResetAuditBacking(bool save)
    {if(save&&!TryFlushAuditBacking())return false;lock(Sync){if(!CanRetireAuditBacking)return false;Entries=new List<RebirthNpcIdentityAuditEntry>();loaded=false;nextSequence=1;auditWorld=null;auditPath=null;auditDirty=false;auditBinding=null;auditEpoch=new object();return true;}}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(auditWorld,auditPath,loaded||auditDirty||Entries.Count!=0))return false;
            var records=Entries;var world=auditWorld;string path=auditPath;var binding=auditBinding;var epoch=auditEpoch;bool wasLoaded=loaded,wasDirty=auditDirty;long next=nextSequence;var nextRecords=new List<RebirthNpcIdentityAuditEntry>();var nextEpoch=new object();var document=AuditDocument(Entries);ReadAuditDocument(document);
            var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("auditQuarantine");doc.AppendChild(root);root.SetAttribute("path",path??string.Empty);root.SetAttribute("loaded",loaded?"1":"0");root.SetAttribute("dirty",auditDirty?"1":"0");root.SetAttribute("next",nextSequence.ToString(CultureInfo.InvariantCulture));root.AppendChild(doc.ImportNode(document.DocumentElement,true));
            domains.Add(new RebirthNpcSaveQuarantineDomain("identity-audit",doc,
                ()=>{lock(Sync)return ReferenceEquals(Entries,records)&&ReferenceEquals(auditWorld,world)&&auditPath==path&&ReferenceEquals(auditBinding,binding)&&ReferenceEquals(auditEpoch,epoch)&&loaded==wasLoaded&&auditDirty==wasDirty&&nextSequence==next&&AuditDocument(Entries).OuterXml==document.OuterXml;},
                ()=>{lock(Sync){Entries=nextRecords;loaded=false;nextSequence=1;auditWorld=null;auditPath=null;auditDirty=false;auditBinding=null;auditEpoch=nextEpoch;}},
                ()=>{lock(Sync){Entries=records;loaded=wasLoaded;nextSequence=next;auditWorld=world;auditPath=path;auditDirty=wasDirty;auditBinding=binding;auditEpoch=epoch;}}));return true;
        }
    }
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {lock(Sync)foreach(var entry in Entries){if(entry==null)throw new InvalidOperationException("Audit original missing");if(RebirthNpcStableId.TryParse(entry.StableId,out var id)&&!id.IsEmpty)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
}
