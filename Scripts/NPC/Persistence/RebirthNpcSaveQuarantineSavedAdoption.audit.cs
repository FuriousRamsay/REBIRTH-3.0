using System;

public static partial class RebirthNpcIdentityAuditLedger
{
    internal static RebirthNpcSaveQuarantineSavedAdoption.Store CaptureSavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var positiveEmpty=CapturePositiveEmptySavedAdoption(scope);if(positiveEmpty!=null)return positiveEmpty;
            var world=auditWorld;string path=auditPath;var epoch=auditEpoch;var binding=auditBinding;
            string image=AuditDocument(Entries).DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded&&!auditDirty&&CanRetireAuditBacking&&ReferenceEquals(auditWorld,world)&&auditPath==path&&
                ReferenceEquals(auditEpoch,epoch)&&ReferenceEquals(auditBinding,binding)&&binding?.MatchesRetirement(epoch)==true&&
                AuditDocument(Entries).DocumentElement.OuterXml==image;}
            bool Empty(){lock(Sync)return !loaded&&!auditDirty&&auditWorld==null&&string.IsNullOrEmpty(auditPath)&&
                !ReferenceEquals(auditEpoch,epoch)&&Entries.Count==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!auditDirty&&CanRetireAuditBacking&&ReferenceEquals(auditWorld,incoming)&&
                auditPath==path&&AuditDocument(Entries).DocumentElement.OuterXml==image;}
            return RebirthNpcSaveQuarantineSavedAdoption.Capture("identity-audit",scope,world,path,loaded&&!auditDirty&&CanRetireAuditBacking,
                Original,Empty,Incoming,ValidateDocument,()=>AuditDocument(Entries),LoadAuditBound);
        }
    }
}
