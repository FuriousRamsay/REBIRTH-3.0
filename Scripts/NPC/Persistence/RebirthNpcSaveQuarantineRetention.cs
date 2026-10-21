using System;
using System.Collections.Generic;

public static partial class RebirthNpcRetentionCoordinator
{
    private static RebirthNpcAncillaryOriginalScope retentionScope;
    private static int retentionCallbacks;
    private static bool UseRetentionScope(){lock(Sync)return RebirthNpcAncillaryOriginalScope.Use(ref retentionScope,Participants.Count==0&&retentionCallbacks==0);}
    private static bool RetentionScopeCurrent()=>retentionScope?.Current()==true;
    internal static bool CanRetireEmptyRetention{get{lock(Sync)return ThreadManager.IsMainThread()&&Participants.Count==0&&retentionCallbacks==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareRetentionQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,retentionScope);if(retentionCallbacks!=0||Participants.Count>4096)throw new InvalidOperationException("Original retention invocation is still active or exceeds retained bound");
            var records=Participants;var original=retentionScope;long a=sweeps,b=itemsReleased,c=participantFailures;var next=new Dictionary<string,Participant>(StringComparer.Ordinal);var doc=RebirthNpcAncillaryOriginalScope.Image("retentionCoordinator",original,new[]{a,b,c});doc.DocumentElement.SetAttribute("emptyOnly","1");
            var definitions=new Dictionary<string,Participant>(records,StringComparer.Ordinal);foreach(var pair in definitions)
            {if(pair.Value==null||pair.Value.Id!=pair.Key||pair.Value.Sweep==null)throw new InvalidOperationException("Original retention definition unavailable");var row=doc.CreateElement("participant");row.SetAttribute("id",pair.Key);doc.DocumentElement.AppendChild(row);}
            doc.DocumentElement.SetAttribute("emptyOnly",records.Count==0?"1":"0");
            return new RebirthNpcSaveQuarantineDomain("retention-delegates-empty",doc,
                ()=>{lock(Sync){if(retentionCallbacks!=0||Participants.Count!=definitions.Count||!ReferenceEquals(Participants,records)||!ReferenceEquals(retentionScope,original)||sweeps!=a||itemsReleased!=b||participantFailures!=c)return false;foreach(var pair in definitions)if(!records.TryGetValue(pair.Key,out var value)||!ReferenceEquals(value,pair.Value)||value.Sweep==null)return false;return true;}},
                ()=>{lock(Sync){Participants=next;retentionScope=null;sweeps=itemsReleased=participantFailures=0;}},
                ()=>{lock(Sync){Participants=records;retentionScope=original;sweeps=a;itemsReleased=b;participantFailures=c;}});
        }
    }
}
