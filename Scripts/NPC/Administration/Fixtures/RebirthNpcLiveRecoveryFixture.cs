#if DEBUG
using System;
using System.IO;

// Prepared scoped adapter, NOT RUN. It invokes the actual transaction driver;
// it does not fabricate backup rows, recipients, ACKs or fault outcomes.
internal static class RebirthNpcLiveRecoveryFixture
{
    internal static object Begin(string confirmation,string file)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        bool completed=RebirthNpcLiveRecoveryRuntime.Request(file,out string detail);
        return new {completed,detail,observation=RebirthNpcLiveRecoveryRuntime.CaptureAuditState()};
    }
    internal static object RequireInstalledOriginal(Guid operation,ulong generation,string file,string expectedCurrentHash)
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)throw new InvalidOperationException("Actual authoritative game thread required");
        string directory=GameIO.GetSaveGameDir();var group=RebirthNpcLiveRecoveryGroup.Read(directory);
        if(group==null||group.Operation!=operation||group.Generation!=generation||group.Phase!=2||RebirthNpcRecoveryGeneration.RequireCurrent()!=generation)
            throw new InvalidOperationException("Exact actual installed pending original required");
        bool member=false;foreach(var row in group.Members)if(row.File==file)member=true;
        if(!member)throw new InvalidOperationException("Not a member of the original extent");
        string path=Path.Combine(directory,file);
        if(RebirthNpcLiveRecoveryIntent.Hash(path)!=expectedCurrentHash||!RebirthNpcRecoveryPublications.IsInstalled(path))
            throw new InvalidOperationException("Latest installed publication did not match observed positive checkpoint");
        // expectedCurrentHash comes from the prior ACTUAL observation. An old
        // candidate hash cannot certify a later legitimate same-G save.
        return RebirthNpcLiveRecoveryRuntime.CaptureAuditState();
    }
}

#endif
