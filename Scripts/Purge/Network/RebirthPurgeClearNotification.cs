using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;

// Subscribe to live transitions only. Loading/joining a saved ledger is silent.
internal static class RebirthPurgeClearNotification
{
    private static RebirthPoiWorldStore owner;
    internal static void Reset(){if(owner!=null)owner.PublicationChanged-=Changed;owner=null;}
    internal static void Pulse()
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge){Reset();return;}
        RebirthPoiWorldStore current;
        if(!RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out current))return;
        if(ReferenceEquals(current,owner))return;
        Reset();owner=current;owner.PublicationChanged+=Changed;
    }
    private static void Changed(RebirthPoiWorldSnapshot before,RebirthPoiWorldSnapshot after,IReadOnlyList<RebirthPoiIdentity> changed)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge||!after.Binding.IsCurrent)return;
        foreach(var identity in changed)
        {
            RebirthPoiClearanceRecord prior,next;
            if(!after.TryGet(identity,out next)||next.State!=RebirthPoiClearanceState.Cleared||next.Clear==null)continue;
            if(before.TryGet(identity,out prior)&&(prior.State==RebirthPoiClearanceState.Cleared||
                prior.State==RebirthPoiClearanceState.ResetPending&&prior.BeforeResetClear!=null))continue;
            // Credit this clear's actual killers, including players who left or
            // disconnected before the final room was cleared.
            string contributor=ContributorNames(next);
            RebirthPurgePoiHistory.Purged(next,contributor);
            Show(identity.Prefab,identity.Biome,next.Clear.SpawnedParticipants,contributor);
            var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection!=null&&connection.IsServer)
                connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPurgeClearNotice>().Setup(identity.Prefab,identity.Biome,next.Clear.SpawnedParticipants,contributor));
        }
    }
    internal static string ContributorNames(RebirthPoiClearanceRecord record)
    {
        // Current epoch witnesses, not cumulative supply credits from earlier clears.
        var actors=record.Observations?.Volumes.Values.SelectMany(v=>v.Actors.Values)
            .Where(a=>a.Dead && a.TerminalKind==RebirthPoiActorTerminalKind.Death && a.Contributor!=null);
        if(actors==null)return "";
        var world=GameManager.Instance?.World;
        var tracked=world?.aiDirector?.GetComponent<AIDirectorPlayerManagementComponent>()?.trackedPlayers.list;
        var names=new Dictionary<string,string>(StringComparer.Ordinal);
        // Native saved player names survive disconnect/reload. Only inspect names
        // for this clear's contributors; never display their stable identity hashes.
        var contributorKeys=new HashSet<string>(actors.Select(a=>a.Contributor),StringComparer.Ordinal);
        var persistent=GameManager.Instance?.GetPersistentPlayerList();
        if(persistent!=null)
        foreach(var data in persistent.Players.Values)
        {
            string canonical=data?.PrimaryId?.CombinedString;
            if(string.IsNullOrWhiteSpace(canonical))continue;
            string key=RebirthStablePlayerIdentity.ComputeStorageKey(canonical.Trim());
            if(contributorKeys.Contains(key))
                names[key]=RebirthPurgeDiscoveryNotification.CleanDisplayName(data.PlayerName?.DisplayName);
        }
        if(tracked!=null)foreach(var entry in tracked)
        {
            string key=RebirthPurgeKillContributor.Identify(entry.Player,world);
            string name=RebirthPurgeDiscoveryNotification.DisplayName(entry.Player);
            if(key!=null&&!string.IsNullOrEmpty(name))names[key]=name;
        }
        // Sanitized display names cannot contain newlines. Keep this internal
        // separator distinct from punctuation that can occur inside a name.
        return string.Join("\n", contributorKeys.Select(key=>names.TryGetValue(key,out var name)?name:"")
            .Where(name=>!string.IsNullOrEmpty(name)).OrderBy(name=>name,StringComparer.OrdinalIgnoreCase));
    }
    internal static void Show(string prefab,string biome,int count,string contributor="")
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.IsPurge)return;
        var player=GameManager.Instance?.World?.GetPrimaryPlayer();if(player==null)return;
        string message=string.IsNullOrEmpty(contributor)
            ? string.Format(Localization.Get("xuiRebirthPurgePoiCleared"),Localization.Get(prefab),count,Localization.Get("biome_"+biome))
            : string.Format(Localization.Get("xuiRebirthPurgePoiClearedBy"),contributor,Localization.Get(prefab),count,Localization.Get("biome_"+biome));
        if(string.IsNullOrEmpty(contributor))GameManager.ShowTooltip(player,message);
        else
        {
            // Every page repeats the POI/context and complete color tags, so
            // pagination cannot leave a player's name clipped or miscolored.
            foreach(string page in XUiC_RebirthMessagePopup.ClearNoticePages(contributor,names=>
                string.Format(Localization.Get("xuiRebirthPurgePoiClearedBy"),names,Localization.Get(prefab),count,Localization.Get("biome_"+biome))))
                GameManager.ShowTooltip(player,page);
        }
        Audio.Manager.PlayInsidePlayerHead("purge_discovered");
    }
}
[Preserve]
public sealed class NetPackageRebirthPurgeClearNotice:NetPackage
{
    // Individual names remain sanitized to 80 characters. The aggregate must
    // accommodate all contributors rather than applying the single-name limit.
    private const int MaxContributorNamesLength=65536;
    private Guid noticeWorld,noticeSession;
    private string prefab,biome,contributor;private int count;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthPurgeClearNotice Setup(string name,string region,int killed,string player=""){noticeWorld=RebirthPoiMapSync.LocalWorld;noticeSession=RebirthPoiMapSync.LocalSession;prefab=name;biome=region;count=killed;contributor=player??"";return this;}
    public override void read(PooledBinaryReader reader){noticeWorld=new Guid(reader.ReadBytes(16));noticeSession=new Guid(reader.ReadBytes(16));prefab=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,256);biome=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);count=reader.ReadInt32();contributor=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,MaxContributorNamesLength);}
    public override void write(PooledBinaryWriter writer){base.write(writer);writer.Write(noticeWorld.ToByteArray());writer.Write(noticeSession.ToByteArray());RebirthSurvivorNetworkCodec.WriteString(writer,prefab,256);RebirthSurvivorNetworkCodec.WriteString(writer,biome,64);writer.Write(count);RebirthSurvivorNetworkCodec.WriteString(writer,contributor,MaxContributorNamesLength);}
    public int GetLength()=>48+(prefab?.Length??0)*3+(biome?.Length??0)*3+(contributor?.Length??0)*3;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            prefab==null||prefab.Length>256||biome==null||biome.Length>64||count<1||count>1000000||contributor==null||contributor.Length>MaxContributorNamesLength)return;
        string name=prefab,region=biome,players=contributor;int killed=count;
        RebirthPurgeNoticeDelivery.Receive(world,noticeWorld,noticeSession,()=>RebirthPurgeClearNotification.Show(name,region,killed,players));
    }
}
