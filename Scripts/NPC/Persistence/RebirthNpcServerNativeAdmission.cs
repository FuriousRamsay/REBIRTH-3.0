using System;
using System.Collections.Generic;

// A whole-domain incoming admission is distinct from original retirement.
// Every named member must have a typed producer; partial staging is retained
// but cannot manufacture coverage for an omitted owner.
internal sealed class RebirthNpcServerNativeAdmission
{
    internal sealed class Role
    {
        internal readonly string[] Members;
        internal readonly Func<bool> Adopt;
        internal Role(string[] members,Func<bool> adopt)
        {
            if(members==null||members.Length==0||adopt==null)throw new ArgumentException("Typed incoming role required");
            Members=(string[])members.Clone();Adopt=adopt;
        }
    }
    private readonly World world;
    private readonly string directory;
    private readonly uint epoch;
    private readonly RebirthNpcSaveQuarantineDomain[] originals;
    private RebirthNpcServerNativeAdmission(RebirthNpcRuntimeNativeAdoption.NativeReentry source,
        RebirthNpcSaveQuarantineDomain[] originals,uint epoch)
    {world=source.World;directory=source.Directory;this.originals=(RebirthNpcSaveQuarantineDomain[])originals.Clone();this.epoch=epoch;}
    private RebirthNpcServerNativeAdmission(RebirthDogLifecycleService.NativeOwnerReentry source,
        RebirthNpcSaveQuarantineDomain[] originals,uint epoch)
    {world=source.World;directory=source.Directory;this.originals=(RebirthNpcSaveQuarantineDomain[])originals.Clone();this.epoch=epoch;}
    internal static bool TryQualifyOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source,
        RebirthNpcSaveQuarantineDomain[] originals,Role[] roles,out RebirthNpcServerNativeAdmission receipt)
    {
        receipt=null;
        if(source?.Current()!=true||!RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(originals)||roles==null)return false;
        var covered=new HashSet<string>(StringComparer.Ordinal);
        foreach(var role in roles)
        {
            if(role==null)return false;
            foreach(string name in role.Members)if(string.IsNullOrEmpty(name)||!covered.Add(name))return false;
        }
        if(!RebirthNpcSaveQuarantinePreparation.IsExactServerCoverage(covered))return false;
        // Every role must supply its own positive private scope eligibility.
        // The owner proves incoming native presence only, never NPC identity,
        // an unloaded store's emptiness, or completion of retained actions.
        foreach(var role in roles)if(!source.Current()||!role.Adopt())return false;
        if(!source.Current())return false;
        var candidate=new RebirthNpcServerNativeAdmission(source,originals,RebirthNpcNetworkEpoch.GetServerEpoch());
        if(!candidate.Current(source.World,source.Directory))return false;
        receipt=candidate;return true;
    }
    internal bool MatchesCapturedOutgoing(RebirthNpcSaveQuarantineCapture.Snapshot captured)
    {
        return captured!=null&&ReferenceEquals(world,captured.Scope.World)&&epoch!=0&&epoch==captured.NetworkEpoch&&
            RebirthNpcSaveQuarantineScope.SameDirectory(directory,captured.Scope.Directory)&&
            RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(originals)&&captured.IsUnchangedOriginal(out _);
    }
    internal bool Current(World incoming,string save)
    {
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(incoming,world)||!ReferenceEquals(world,GameManager.Instance?.World)||
            world?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,save)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir())||
            epoch==0||!RebirthNpcNetworkEpoch.IsObservedServerAdmissionEpoch(world,epoch))return false;
        return RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(originals);
    }
    internal static bool TryQualify(RebirthNpcRuntimeNativeAdoption.NativeReentry source,
        RebirthNpcSaveQuarantineDomain[] originals,Role[] roles,out RebirthNpcServerNativeAdmission receipt)
    {
        receipt=null;
        if(source?.Current()!=true||!RebirthNpcSaveQuarantinePreparation.IsDetachedServerMembership(originals)||roles==null)return false;
        var covered=new HashSet<string>(StringComparer.Ordinal);
        foreach(var role in roles)
        {
            if(role==null)return false;
            foreach(string name in role.Members)if(string.IsNullOrEmpty(name)||!covered.Add(name))return false;
        }
        if(!RebirthNpcSaveQuarantinePreparation.IsExactServerCoverage(covered))return false;
        // The complete membership preflight precedes every loader/owner
        // publication. Failed later roles leave prior incoming staging held.
        foreach(var role in roles)if(!source.Current()||!role.Adopt())return false;
        if(!source.Current())return false;
        uint epoch=RebirthNpcNetworkEpoch.GetServerEpoch();
        var candidate=new RebirthNpcServerNativeAdmission(source,originals,epoch);
        if(!candidate.Current(source.World,source.Directory))return false;
        receipt=candidate;return true;
    }
}

public static partial class RebirthNpcNetworkEpoch
{
    // Admission readback must not reenter the lifecycle boundary/epoch producer.
    internal static bool IsObservedServerAdmissionEpoch(World world,uint epoch)
    {
        lock(Sync)return ThreadManager.IsMainThread()&&epoch!=0&&activeServerEpoch==epoch&&
            ReferenceEquals(serverWorld,world)&&CurrentServerWorld();
    }
}
