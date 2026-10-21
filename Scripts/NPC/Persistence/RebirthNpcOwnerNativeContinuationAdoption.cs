using System;
using System.Collections.Generic;

// Only actual incoming native player owner sources satisfy captured account
// obligations. Native NPC presence cannot stand in for the owning player.
internal sealed class RebirthNpcOwnerNativeContinuationAdoption
{
    private readonly World outgoing;private readonly string directory;private readonly HashSet<string> required;
    private readonly Func<bool> current;
    private readonly Func<World,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry>,bool> adopt;
    private readonly Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry> sources=new Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry>(StringComparer.Ordinal);
    private RebirthDogLifecycleService.NativeOwnerReentry anchor;
    internal RebirthNpcOwnerNativeContinuationAdoption(RebirthNpcSaveQuarantineScope scope,HashSet<string> accounts,Func<bool> current,
        Func<World,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry>,bool> adopt)
    {outgoing=scope.World;directory=scope.Directory;required=new HashSet<string>(accounts,StringComparer.Ordinal);this.current=current;this.adopt=adopt;}
    internal bool IsOriginalCurrent=>ThreadManager.IsMainThread()&&current();
    internal bool RequiresAccount(string account)=>account!=null&&required.Contains(account);
    internal bool Observe(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(source?.Current()!=true||ReferenceEquals(source.World,outgoing)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)||
            anchor!=null&&!ReferenceEquals(anchor.World,source.World))return false;
        anchor=source;if(required.Contains(source.Account))sources[source.Account]=source;return TryAdopt();
    }
    internal bool TryAdopt()
    {
        if(!ThreadManager.IsMainThread()||anchor?.Current()!=true||ReferenceEquals(anchor.World,outgoing)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir()))return false;
        foreach(string account in required)if(!sources.TryGetValue(account,out var source)||!ReferenceEquals(source.World,anchor.World)||!source.Current())return false;
        return adopt(anchor.World,sources);
    }
    internal bool TryAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry scopeSource)
    {
        if(scopeSource?.Current()!=true||ReferenceEquals(scopeSource.World,outgoing)||
            !RebirthNpcSaveQuarantineScope.SameDirectory(directory,scopeSource.Directory))return false;
        if(required.Count!=0)return TryAdopt();
        // No captured native player obligation exists in this role. Its empty
        // incoming namespace can use a genuine NPC scope observation; this
        // does not substitute an NPC for any required account/player source.
        return adopt(scopeSource.World,sources);
    }
}

public static partial class RebirthDogLifecycleService
{
    internal static string ResolveNativeAdoptionAccount(string owner)
    {
        if(!ThreadManager.IsMainThread()||string.IsNullOrWhiteSpace(owner)||
            !PlatformUserIdentifierAbs.TryFromCombinedString(owner,out var identity))return null;
        var row=GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerData(identity);
        return row?.PrimaryId!=null&&row.PrimaryId.ToString()==owner&&RebirthStablePlayerIdentity.TryFromPersistentPlayer(row,out var actual)?actual.CanonicalId:null;
    }
}
