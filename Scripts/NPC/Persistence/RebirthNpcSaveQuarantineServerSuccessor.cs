using System;
using System.Collections.Generic;

internal static partial class RebirthNpcSaveQuarantineBundle
{
    // A successor carries namespace admission only. Original effects, leases,
    // fences and callbacks stay in their original retained group.
    private sealed class ServerAdmissionSuccessor
    {
        internal Retained Prior,Next;
        internal RebirthNpcServerNativeAdmission Admission;
        internal RebirthNpcSaveQuarantineCapture.Snapshot Capture;
        internal RebirthNpcSaveQuarantineDomain[] Domains;
        internal bool Committed;
    }
    private static readonly List<ServerAdmissionSuccessor> serverAdmissionSuccessors=new List<ServerAdmissionSuccessor>();
    private static bool RegisterServerAdmissionSuccessors(Retained successor)
    {
        if(!ThreadManager.IsMainThread()||successor==null||successor.Detached||successor.RollbackIncomplete||
            !retained.Contains(successor)||successor.Original?.IsRemote()!=false||successor.Capture==null||
            successor.Domains==null||successor.Domains.Length==0||!successor.Capture.IsUnchangedOriginal(out _))return false;
        var additions=new List<ServerAdmissionSuccessor>();
        foreach(var prior in retained)
        {
            if(ReferenceEquals(prior,successor)||!prior.Detached||!SameDirectory(prior.Directory,successor.Directory))continue;
            if(prior.RollbackIncomplete||prior.Original?.IsRemote()!=false)return false;
            // Earlier ancestors already have a strongly held successor; only
            // the current leaf admission can match this native outgoing scope.
            if(serverAdmissionSuccessors.Exists(link=>ReferenceEquals(link.Prior,prior)&&link.Committed))continue;
            var admission=prior.ServerIncomingAdmission;
            if(admission?.MatchesCapturedOutgoing(successor.Capture)!=true)return false;
            if(serverAdmissionSuccessors.Exists(link=>ReferenceEquals(link.Prior,prior)))return false;
            additions.Add(new ServerAdmissionSuccessor{Prior=prior,Next=successor,Admission=admission,
                Capture=successor.Capture,Domains=successor.Domains});
        }
        serverAdmissionSuccessors.AddRange(additions);return true;
    }
    private static bool CommitServerAdmissionSuccessors(Retained successor)
    {
        if(!ThreadManager.IsMainThread()||successor==null||!retained.Contains(successor)||!successor.Detached||successor.RollbackIncomplete||
            successor.Fence!=null&&!successor.Fence.IsCurrent||!RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(successor.Domains))return false;
        foreach(var link in serverAdmissionSuccessors)
            if(ReferenceEquals(link.Next,successor)&&(!ReferenceEquals(link.Capture,successor.Capture)||
                !ReferenceEquals(link.Domains,successor.Domains)||!ReferenceEquals(link.Prior.ServerIncomingAdmission,link.Admission)||
                !retained.Contains(link.Prior)||!link.Prior.Detached||link.Prior.RollbackIncomplete))return false;
        foreach(var link in serverAdmissionSuccessors)if(ReferenceEquals(link.Next,successor))link.Committed=true;
        return true;
    }
    private static void AbortServerAdmissionSuccessors(Retained successor)
    {
        // Called only after exact rollback, never to discard committed custody.
        serverAdmissionSuccessors.RemoveAll(link=>!link.Committed&&ReferenceEquals(link.Next,successor));
    }
    private static bool ResolveServerIncomingAdmission(Retained value,World incoming,string directory)
    {
        if(!ThreadManager.IsMainThread()||value==null||!SameDirectory(value.Directory,directory))return false;
        var seen=new HashSet<Retained>();
        while(seen.Add(value))
        {
            if(!retained.Contains(value)||!value.Detached||value.RollbackIncomplete||value.Original?.IsRemote()!=false||
                !SameDirectory(value.Directory,directory)||!RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(value.Domains))return false;
            if(value.ServerIncomingAdmission?.Current(incoming,directory)==true)return true;
            var link=serverAdmissionSuccessors.Find(item=>ReferenceEquals(item.Prior,value)&&item.Committed);
            if(link==null||!ReferenceEquals(link.Admission,value.ServerIncomingAdmission)||link.Next==null||
                !ReferenceEquals(link.Capture,link.Next.Capture)||!ReferenceEquals(link.Domains,link.Next.Domains)||
                !SameDirectory(link.Next.Directory,directory))return false;
            value=link.Next;
        }
        return false;
    }
}
