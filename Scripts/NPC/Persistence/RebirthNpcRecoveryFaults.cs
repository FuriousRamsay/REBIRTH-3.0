using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

// Debug fixture control only. The boundary is reached by REAL production
// publication/install code; no failed IO, receipt, native effect or ACK is forged.
internal static class RebirthNpcRecoveryFaults
{
    private static readonly HashSet<string> Points=new HashSet<string>(StringComparer.Ordinal)
    {"group-prepared","member-prepared","primary-published","generation-published","main-installed","related-installed","execution-installed","runtime-installed","publication-initialized","installed","save-pending","save-primary","save-finished","closing-intent","member-retired","cache-retired-0","cache-retired-1","cache-retired-2","cache-retired-3","cache-retired-4"};
#if DEBUG
    private static World world;
    private static string directory,file,point;
    private static Guid operation;
    private static bool armed,paused;
    private static long reached;
#endif
    internal static string AvailablePoints=>string.Join(",",Points);
    internal static void Arm(string confirmation,string target,string boundary)
    {
#if DEBUG
        Require(confirmation);
        if(!Points.Contains(boundary))throw new ArgumentException("Unknown actual recovery boundary");
        if(armed||paused)throw new InvalidOperationException("Existing exact fixture control must release first");
        string canonical=null;foreach(var candidate in new[]{"RebirthNpcStableIdentities.xml","rebirth_npc_inventory.xml","rebirth_npc_equipment.xml","RebirthNpcExecutionState.xml","rebirth_npc_settlement.xml","RebirthNpcWorkCheckpoints.xml","RebirthNpcWorkOutcomes.xml","RebirthNpcSocialState.xml","RebirthNpcCombatState.xml","RebirthNpcPersistentRecords.xml"})
            if(string.Equals(candidate,target,StringComparison.Ordinal))canonical=candidate;
        if(canonical==null||canonical=="RebirthNpcWorldIntegration.xml")throw new ArgumentException("An exact standalone recovery member is required");
        // Exact actual driver allow-list; timeline-only sidecars cannot arm.
        var original=RebirthNpcLiveRecoveryGroup.Read(GameIO.GetSaveGameDir());
        if(original!=null&&original.Target!=canonical)throw new InvalidOperationException("Another actual recovery extent remains pending");
        world=GameManager.Instance.World;directory=GameIO.GetSaveGameDir();file=canonical;point=boundary;operation=original?.Operation??Guid.Empty;
        reached=0;armed=true;paused=false;
#else
        throw new InvalidOperationException("Recovery fault control is DEBUG-only");
#endif
    }
    internal static void Release(string confirmation)
    {
#if DEBUG
        Require(confirmation);
        if((armed||paused)&&(!ReferenceEquals(world,GameManager.Instance?.World)||directory!=GameIO.GetSaveGameDir()))throw new InvalidOperationException("Fixture belongs to another original native world/save");
        armed=false;paused=false;point=null;file=null;operation=Guid.Empty;world=null;directory=null;
#else
        throw new InvalidOperationException("Recovery fault control is DEBUG-only");
#endif
    }
    [Conditional("DEBUG")]
    internal static void PublicationCheckpoint(Guid original,string path,string boundary)
    {
#if DEBUG
        if(!armed||point!=boundary)return;
        var group=RebirthNpcLiveRecoveryGroup.Read(Path.GetDirectoryName(Path.GetFullPath(path)));
        if(group==null||group.Operation!=original)return;
        foreach(var member in group.Members)if(member.File==Path.GetFileName(path)){Checkpoint(original,group.Target,boundary);return;}
#endif
    }
    [Conditional("DEBUG")]
    internal static void Checkpoint(Guid original,string target,string boundary)
    {
#if DEBUG
        if(!armed||paused||point!=boundary||file!=target||!ReferenceEquals(world,GameManager.Instance?.World)||directory!=GameIO.GetSaveGameDir()||
            original==Guid.Empty||(operation!=Guid.Empty&&operation!=original))return;
        Require("CodexTest");operation=original;reached++;paused=true;armed=false;
        throw new IOException("DEBUG exact recovery boundary paused: "+boundary+" original="+original.ToString("N"));
#endif
    }
    internal static bool IsPaused(Guid original)
    {
#if DEBUG
        return paused&&operation==original&&ReferenceEquals(world,GameManager.Instance?.World)&&directory==GameIO.GetSaveGameDir();
#else
        return false;
#endif
    }
    internal static object Capture()
    {
#if DEBUG
        return new {debug=true,armed,paused,file,point,operation=operation.ToString("N"),reached,scopeCurrent=ReferenceEquals(world,GameManager.Instance?.World)&&directory==GameIO.GetSaveGameDir()};
#else
        return new {debug=false,armed=false,paused=false};
#endif
    }
    private static void Require(string token)
    {if(!RebirthNpcQualificationMutationGuard.Allows(token))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);}
}
